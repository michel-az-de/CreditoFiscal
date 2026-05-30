using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Api.Observabilidade;
using CreditoFiscal.Aplicacao.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CreditoFiscal.Api.BackgroundServices;

public sealed class CreditoConsumer : BackgroundService
{
    private const int MaxTentativasPadrao = 5;
    private const int IntervaloPollingPadraoMs = 500;

    private readonly IServiceScopeFactory _escopos;
    private readonly IMensagemConsumer _consumer;
    private readonly ILogger<CreditoConsumer> _logger;
    private readonly int _maxTentativas;
    private readonly TimeSpan _intervaloPolling;

    public CreditoConsumer(IServiceScopeFactory escopos, IMensagemConsumer consumer, IConfiguration configuration, ILogger<CreditoConsumer> logger)
    {
        _escopos = escopos;
        _consumer = consumer;
        _logger = logger;
        _maxTentativas = LerMaxTentativas(configuration);
        _intervaloPolling = LerIntervaloPolling(configuration);
    }

    internal TimeSpan IntervaloPolling => _intervaloPolling;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var sessao = await _consumer.AbrirSessaoAsync<CreditoConstituidoDto>(
                    Filas.IntegrarCreditoConstituido, maximo: 10, TimeSpan.FromMilliseconds(400), stoppingToken);

                foreach (var mensagem in sessao.Mensagens)
                {
                    // escopo por mensagem: DbContext limpo, sem tracking compartilhado
                    using var escopo = _escopos.CreateScope();
                    var repositorio = escopo.ServiceProvider.GetRequiredService<ICreditoRepository>();
                    var unidade = escopo.ServiceProvider.GetRequiredService<IUnidadeDeTrabalho>();
                    await ProcessarAsync(repositorio, unidade, sessao, mensagem, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception excecao)
            {
                // .NET 6: excecao escapando de ExecuteAsync mata o host; engole e segue
                _logger.LogError(excecao, "Falha na iteracao do consumidor; continuando");
            }

            await Task.Delay(_intervaloPolling, stoppingToken);
        }
    }

    internal async Task ProcessarAsync(
        ICreditoRepository repositorio,
        IUnidadeDeTrabalho unidade,
        IConsumerSession<CreditoConstituidoDto> sessao,
        ReceivedMessage<CreditoConstituidoDto> mensagem,
        CancellationToken ct)
    {
        var dto = mensagem.Conteudo;

        using var atividade = Telemetria.Fonte.StartActivity("processar-credito");
        atividade?.SetTag("credito.numero", dto.NumeroCredito);

        try
        {
            // idempotencia 1: ja persistido -> confirma e sai
            if (await repositorio.ExisteAsync(dto.NumeroCredito, ct))
            {
                _logger.LogWarning("Credito {Numero} ja existe; duplicata ignorada", dto.NumeroCredito);
                await sessao.ConfirmarAsync(mensagem, ct);
                return;
            }

            await repositorio.AdicionarAsync(ConverterParaCredito(dto), ct);
            await unidade.SalvarAsync(ct);
            await sessao.ConfirmarAsync(mensagem, ct);
        }
        catch (DbUpdateException)
        {
            // idempotencia 2: corrida entre instancias bateu no unique -> confirma
            _logger.LogWarning("Credito {Numero} duplicado no commit; confirmando", dto.NumeroCredito);
            await sessao.ConfirmarAsync(mensagem, ct);
        }
        catch (Exception excecao)
        {
            // gate de poison message: alcancou max -> DLQ; senao reenfileira e o broker incrementa Tentativas
            if (mensagem.Tentativas >= _maxTentativas)
            {
                _logger.LogError(excecao, "Credito {Numero} excedeu {Max} tentativas; enviando para DLQ", dto.NumeroCredito, _maxTentativas);
                await sessao.EnviarParaDlqAsync(mensagem, $"Excedeu {_maxTentativas} tentativas: {excecao.Message}", ct);
                return;
            }

            _logger.LogError(excecao, "Falha ao processar credito {Numero} (tentativa {Tentativa} de {Max}); reenfileirando", dto.NumeroCredito, mensagem.Tentativas, _maxTentativas);
            await sessao.RejeitarAsync(mensagem, reencaminhar: true, ct);
        }
    }

    private static int LerMaxTentativas(IConfiguration configuration)
    {
        var bruto = configuration["Mensageria:MaxTentativasConsumer"];
        if (int.TryParse(bruto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) && valor > 0)
        {
            return valor;
        }

        return MaxTentativasPadrao;
    }

    private static TimeSpan LerIntervaloPolling(IConfiguration configuration)
    {
        var bruto = configuration["Mensageria:IntervaloPollingMs"];
        if (int.TryParse(bruto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) && valor > 0)
        {
            return TimeSpan.FromMilliseconds(valor);
        }

        return TimeSpan.FromMilliseconds(IntervaloPollingPadraoMs);
    }

    private static Credito ConverterParaCredito(CreditoConstituidoDto dto)
    {
        return new Credito
        {
            NumeroCredito = dto.NumeroCredito,
            NumeroNfse = dto.NumeroNfse,
            DataConstituicao = dto.DataConstituicao,
            ValorIssqn = dto.ValorIssqn,
            TipoCredito = dto.TipoCredito,
            SimplesNacional = dto.SimplesNacional,
            Aliquota = dto.Aliquota,
            ValorFaturado = dto.ValorFaturado,
            ValorDeducao = dto.ValorDeducao,
            BaseCalculo = dto.BaseCalculo
        };
    }
}
