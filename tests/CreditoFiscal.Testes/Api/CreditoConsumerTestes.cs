using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Api.BackgroundServices;
using CreditoFiscal.Aplicacao.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Dominio.Entidades;
using CreditoFiscal.Testes.Suporte;
using FluentAssertions;
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
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(CreditoMother.Constituido("123")) { Tentativas = 1 };

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
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(CreditoMother.Constituido("123")) { Tentativas = 1 };

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
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(CreditoMother.Constituido("123")) { Tentativas = 1 };

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
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(CreditoMother.Constituido("123")) { Tentativas = 3 };

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
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(CreditoMother.Constituido("123")) { Tentativas = 5 };

        await consumer.ProcessarAsync(repositorio, unidade, sessao, mensagem, CancellationToken.None);

        await sessao.Received(1).EnviarParaDlqAsync(mensagem, Arg.Is<string>(motivo => motivo.Contains("5 tentativas")), Arg.Any<CancellationToken>());
        await sessao.DidNotReceive().RejeitarAsync(Arg.Any<ReceivedMessage<CreditoConstituidoDto>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await sessao.DidNotReceive().ConfirmarAsync(Arg.Any<ReceivedMessage<CreditoConstituidoDto>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void IntervaloPolling_QuandoConfigAusente_DeveSer500ms()
    {
        var consumer = MontarConsumer();

        consumer.IntervaloPolling.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Theory]
    [InlineData("250", 250)]
    [InlineData("1000", 1000)]
    public void IntervaloPolling_QuandoConfigPresente_DeveLerValorEmMs(string bruto, int esperadoMs)
    {
        var consumer = MontarConsumer(intervaloPollingMs: bruto);

        consumer.IntervaloPolling.Should().Be(TimeSpan.FromMilliseconds(esperadoMs));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void IntervaloPolling_QuandoConfigInvalida_DeveCairNoPadrao(string bruto)
    {
        var consumer = MontarConsumer(intervaloPollingMs: bruto);

        consumer.IntervaloPolling.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    private static CreditoConsumer MontarConsumer(int maxTentativas = 5, string? intervaloPollingMs = null)
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var consumer = Substitute.For<IMensagemConsumer>();
        var chaves = new Dictionary<string, string?>
        {
            ["Mensageria:MaxTentativasConsumer"] = maxTentativas.ToString(CultureInfo.InvariantCulture)
        };
        if (intervaloPollingMs != null)
        {
            chaves["Mensageria:IntervaloPollingMs"] = intervaloPollingMs;
        }
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(chaves)
            .Build();
        return new CreditoConsumer(scopeFactory, consumer, configuration, NullLogger<CreditoConsumer>.Instance);
    }

}
