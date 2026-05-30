using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Api.BackgroundServices;
using CreditoFiscal.Aplicacao.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CreditoFiscal.Testes.Api;

public sealed class CreditoConsumerTestes
{
    [Fact]
    public async Task ProcessarAsync_QuandoCreditoNovo_DevePersistirEConfirmar()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ExisteAsync("123", Arg.Any<CancellationToken>()).Returns(false);
        var unidade = Substitute.For<IUnidadeDeTrabalho>();
        var sessao = Substitute.For<IConsumerSession<CreditoConstituidoDto>>();
        var consumer = MontarConsumer();
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(MontarDto("123")) { Tentativas = 1 };

        await consumer.ProcessarAsync(repositorio, unidade, sessao, mensagem, CancellationToken.None);

        await repositorio.Received(1).AdicionarAsync(Arg.Is<Credito>(c => c.NumeroCredito == "123"), Arg.Any<CancellationToken>());
        await unidade.Received(1).SalvarAsync(Arg.Any<CancellationToken>());
        await sessao.Received(1).ConfirmarAsync(mensagem, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessarAsync_QuandoJaExistePorExisteAsync_NaoPersisteMasConfirma()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ExisteAsync("123", Arg.Any<CancellationToken>()).Returns(true);
        var unidade = Substitute.For<IUnidadeDeTrabalho>();
        var sessao = Substitute.For<IConsumerSession<CreditoConstituidoDto>>();
        var consumer = MontarConsumer();
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(MontarDto("123")) { Tentativas = 1 };

        await consumer.ProcessarAsync(repositorio, unidade, sessao, mensagem, CancellationToken.None);

        await repositorio.DidNotReceive().AdicionarAsync(Arg.Any<Credito>(), Arg.Any<CancellationToken>());
        await unidade.DidNotReceive().SalvarAsync(Arg.Any<CancellationToken>());
        await sessao.Received(1).ConfirmarAsync(mensagem, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessarAsync_QuandoDbUpdateExceptionNoCommit_DeveConfirmar()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ExisteAsync("123", Arg.Any<CancellationToken>()).Returns(false);
        var unidade = Substitute.For<IUnidadeDeTrabalho>();
        unidade.SalvarAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DbUpdateException("conflito de unique"));
        var sessao = Substitute.For<IConsumerSession<CreditoConstituidoDto>>();
        var consumer = MontarConsumer();
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(MontarDto("123")) { Tentativas = 1 };

        await consumer.ProcessarAsync(repositorio, unidade, sessao, mensagem, CancellationToken.None);

        await sessao.Received(1).ConfirmarAsync(mensagem, Arg.Any<CancellationToken>());
        await sessao.DidNotReceive().RejeitarAsync(Arg.Any<ReceivedMessage<CreditoConstituidoDto>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessarAsync_QuandoExcecaoGenericaEAindaTemOrcamento_DeveRejeitarComReencaminhar()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ExisteAsync("123", Arg.Any<CancellationToken>()).Returns(false);
        repositorio.AdicionarAsync(Arg.Any<Credito>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("falha"));
        var unidade = Substitute.For<IUnidadeDeTrabalho>();
        var sessao = Substitute.For<IConsumerSession<CreditoConstituidoDto>>();
        var consumer = MontarConsumer(maxTentativas: 5);
        // Tentativas = 3 esta dentro do orcamento (< 5): ainda reenfileira
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(MontarDto("123")) { Tentativas = 3 };

        await consumer.ProcessarAsync(repositorio, unidade, sessao, mensagem, CancellationToken.None);

        await sessao.Received(1).RejeitarAsync(mensagem, true, Arg.Any<CancellationToken>());
        await sessao.DidNotReceive().EnviarParaDlqAsync(Arg.Any<ReceivedMessage<CreditoConstituidoDto>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await sessao.DidNotReceive().ConfirmarAsync(Arg.Any<ReceivedMessage<CreditoConstituidoDto>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessarAsync_QuandoExcecaoGenericaEExcedeuMaxTentativas_DeveEnviarParaDlq()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ExisteAsync("123", Arg.Any<CancellationToken>()).Returns(false);
        repositorio.AdicionarAsync(Arg.Any<Credito>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("falha persistente"));
        var unidade = Substitute.For<IUnidadeDeTrabalho>();
        var sessao = Substitute.For<IConsumerSession<CreditoConstituidoDto>>();
        var consumer = MontarConsumer(maxTentativas: 5);
        // Tentativas = 5 == max: a entrega Nesima falhou e o consumer encaminha pra DLQ
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(MontarDto("123")) { Tentativas = 5 };

        await consumer.ProcessarAsync(repositorio, unidade, sessao, mensagem, CancellationToken.None);

        await sessao.Received(1).EnviarParaDlqAsync(mensagem, Arg.Is<string>(motivo => motivo.Contains("5 tentativas")), Arg.Any<CancellationToken>());
        await sessao.DidNotReceive().RejeitarAsync(Arg.Any<ReceivedMessage<CreditoConstituidoDto>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await sessao.DidNotReceive().ConfirmarAsync(Arg.Any<ReceivedMessage<CreditoConstituidoDto>>(), Arg.Any<CancellationToken>());
    }

    private static CreditoConsumer MontarConsumer(int maxTentativas = 5)
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var consumer = Substitute.For<IMensagemConsumer>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mensageria:MaxTentativasConsumer"] = maxTentativas.ToString(CultureInfo.InvariantCulture)
            })
            .Build();
        return new CreditoConsumer(scopeFactory, consumer, configuration, NullLogger<CreditoConsumer>.Instance);
    }

    private static CreditoConstituidoDto MontarDto(string numeroCredito)
    {
        return new CreditoConstituidoDto
        {
            NumeroCredito = numeroCredito,
            NumeroNfse = "nfse-1",
            DataConstituicao = new DateTime(2024, 2, 25),
            ValorIssqn = 1500.75m,
            TipoCredito = "ISSQN",
            SimplesNacional = SimplesNacional.NaoOptante,
            Aliquota = 5.0m,
            ValorFaturado = 30000m,
            ValorDeducao = 5000m,
            BaseCalculo = 25000m
        };
    }
}
