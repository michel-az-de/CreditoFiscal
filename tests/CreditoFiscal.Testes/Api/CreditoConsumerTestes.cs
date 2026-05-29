using System;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Api.BackgroundServices;
using CreditoFiscal.Api.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
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
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(MontarDto("123"));

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
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(MontarDto("123"));

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
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(MontarDto("123"));

        await consumer.ProcessarAsync(repositorio, unidade, sessao, mensagem, CancellationToken.None);

        await sessao.Received(1).ConfirmarAsync(mensagem, Arg.Any<CancellationToken>());
        await sessao.DidNotReceive().RejeitarAsync(Arg.Any<ReceivedMessage<CreditoConstituidoDto>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessarAsync_QuandoExcecaoGenerica_DeveRejeitarComReencaminhar()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ExisteAsync("123", Arg.Any<CancellationToken>()).Returns(false);
        repositorio.AdicionarAsync(Arg.Any<Credito>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("falha"));
        var unidade = Substitute.For<IUnidadeDeTrabalho>();
        var sessao = Substitute.For<IConsumerSession<CreditoConstituidoDto>>();
        var consumer = MontarConsumer();
        var mensagem = new ReceivedMessage<CreditoConstituidoDto>(MontarDto("123"));

        await consumer.ProcessarAsync(repositorio, unidade, sessao, mensagem, CancellationToken.None);

        await sessao.Received(1).RejeitarAsync(mensagem, true, Arg.Any<CancellationToken>());
        await sessao.DidNotReceive().ConfirmarAsync(Arg.Any<ReceivedMessage<CreditoConstituidoDto>>(), Arg.Any<CancellationToken>());
    }

    private static CreditoConsumer MontarConsumer()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var consumer = Substitute.For<IMensagemConsumer>();
        return new CreditoConsumer(scopeFactory, consumer, NullLogger<CreditoConsumer>.Instance);
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
