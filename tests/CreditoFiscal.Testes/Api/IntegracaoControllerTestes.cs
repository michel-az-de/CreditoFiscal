using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Api.Controllers;
using CreditoFiscal.Api.Dtos;
using CreditoFiscal.Api.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace CreditoFiscal.Testes.Api;

public sealed class IntegracaoControllerTestes
{
    [Fact]
    public async Task IntegrarAsync_QuandoCreditosValidos_DevePublicarCadaUmIndividualmente()
    {
        var publisher = Substitute.For<IMensagemPublisher>();
        var controller = new IntegracaoController(publisher);
        var creditos = new List<IntegrarCreditoRequisicaoDto>
        {
            MontarRequisicao("1", "Sim"),
            MontarRequisicao("2", "Não")
        };

        var resultado = await controller.IntegrarAsync(creditos, CancellationToken.None);

        await publisher.Received(2).PublicarAsync(
            Filas.IntegrarCreditoConstituido,
            Arg.Any<CreditoConstituidoDto>(),
            Arg.Any<CancellationToken>());

        var resposta = resultado.Should().BeOfType<ObjectResult>().Subject;
        resposta.StatusCode.Should().Be(202);
        resposta.Value.Should().BeOfType<IntegracaoRespostaDto>().Which.Success.Should().BeTrue();
    }

    [Fact]
    public async Task IntegrarAsync_QuandoSimplesNacionalInvalido_DeveLancarArgumentException()
    {
        var publisher = Substitute.For<IMensagemPublisher>();
        var controller = new IntegracaoController(publisher);
        var creditos = new List<IntegrarCreditoRequisicaoDto> { MontarRequisicao("1", "Talvez") };

        Func<Task> acao = AcaoDe(controller, creditos);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task IntegrarAsync_QuandoUmCreditoDoLoteEhInvalido_NaoDevePublicarNenhum()
    {
        var publisher = Substitute.For<IMensagemPublisher>();
        var controller = new IntegracaoController(publisher);
        var creditos = new List<IntegrarCreditoRequisicaoDto>
        {
            MontarRequisicao("1", "Sim"),
            MontarRequisicao("2", "Talvez")
        };

        Func<Task> acao = AcaoDe(controller, creditos);

        await acao.Should().ThrowAsync<ArgumentException>();
        await publisher.DidNotReceive().PublicarAsync(
            Arg.Any<string>(),
            Arg.Any<CreditoConstituidoDto>(),
            Arg.Any<CancellationToken>());
    }

    private static Func<Task> AcaoDe(IntegracaoController controller, List<IntegrarCreditoRequisicaoDto> creditos)
    {
        return delegate
        {
            return controller.IntegrarAsync(creditos, CancellationToken.None);
        };
    }

    private static IntegrarCreditoRequisicaoDto MontarRequisicao(string numeroCredito, string simplesNacional)
    {
        return new IntegrarCreditoRequisicaoDto
        {
            NumeroCredito = numeroCredito,
            NumeroNfse = "nfse-1",
            DataConstituicao = new DateTime(2024, 2, 25),
            ValorIssqn = 1500.75m,
            TipoCredito = "ISSQN",
            SimplesNacional = simplesNacional,
            Aliquota = 5.0m,
            ValorFaturado = 30000m,
            ValorDeducao = 5000m,
            BaseCalculo = 25000m
        };
    }
}
