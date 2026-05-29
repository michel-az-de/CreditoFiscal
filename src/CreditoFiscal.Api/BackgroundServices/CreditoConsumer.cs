using System;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Api.Observabilidade;
using CreditoFiscal.Aplicacao.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CreditoFiscal.Api.BackgroundServices;

public sealed class CreditoConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IMensagemConsumer _consumer;
    private readonly ILogger<CreditoConsumer> _logger;

    public CreditoConsumer(IServiceScopeFactory escopos, IMensagemConsumer consumer, ILogger<CreditoConsumer> logger)
    {
        _escopos = escopos;
        _consumer = consumer;
        _logger = logger;
    }

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
                    // escopo por mensagem: cada uma comeca com um DbContext limpo
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
                // defesa: nenhum defeito de iteracao pode escapar do ExecuteAsync e derrubar o host
                _logger.LogError(excecao, "Falha na iteracao do consumidor; continuando");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
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
            // idempotencia 1: ja persistido -> confirma sem reprocessar
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
            // idempotencia 2: corrida entre instancias bateu no unique do banco -> confirma
            _logger.LogWarning("Credito {Numero} duplicado no commit; confirmando", dto.NumeroCredito);
            await sessao.ConfirmarAsync(mensagem, ct);
        }
        catch (Exception excecao)
        {
            // falha real (banco fora, etc.): devolve pra fila pra tentar de novo
            _logger.LogError(excecao, "Falha ao processar credito {Numero}; reenfileirando", dto.NumeroCredito);
            await sessao.RejeitarAsync(mensagem, reencaminhar: true, ct);
        }
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
