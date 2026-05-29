using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Api.Controllers;
using CreditoFiscal.Aplicacao.CasosDeUso;
using CreditoFiscal.Aplicacao.Dtos;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace CreditoFiscal.Testes.Api;

public sealed class CreditosControllerTestes
{
    [Fact]
    public async Task ObterPorNumeroNfseAsync_QuandoExistem_DeveRetornar200ComLista()
    {
        var porNfse = Substitute.For<IConsultarCreditosPorNfse>();
        porNfse.ExecutarAsync("nfse-1", Arg.Any<CancellationToken>()).Returns(new List<CreditoRespostaDto>
        {
            new CreditoRespostaDto { NumeroCredito = "1" },
            new CreditoRespostaDto { NumeroCredito = "2" }
        });
        var controller = new CreditosController(porNfse, Substitute.For<IConsultarCreditoPorNumero>());

        var resultado = await controller.ObterPorNumeroNfseAsync("nfse-1", CancellationToken.None);

        var ok = resultado.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IReadOnlyList<CreditoRespostaDto>>().Which.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObterPorNumeroNfseAsync_QuandoNaoExistem_DeveRetornar404()
    {
        var porNfse = Substitute.For<IConsultarCreditosPorNfse>();
        porNfse.ExecutarAsync("nfse-x", Arg.Any<CancellationToken>()).Returns(new List<CreditoRespostaDto>());
        var controller = new CreditosController(porNfse, Substitute.For<IConsultarCreditoPorNumero>());

        var resultado = await controller.ObterPorNumeroNfseAsync("nfse-x", CancellationToken.None);

        resultado.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ObterPorNumeroCreditoAsync_QuandoExiste_DeveRetornar200()
    {
        var porNumero = Substitute.For<IConsultarCreditoPorNumero>();
        porNumero.ExecutarAsync("123", Arg.Any<CancellationToken>()).Returns(new CreditoRespostaDto { NumeroCredito = "123" });
        var controller = new CreditosController(Substitute.For<IConsultarCreditosPorNfse>(), porNumero);

        var resultado = await controller.ObterPorNumeroCreditoAsync("123", CancellationToken.None);

        var ok = resultado.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<CreditoRespostaDto>().Which.NumeroCredito.Should().Be("123");
    }

    [Fact]
    public async Task ObterPorNumeroCreditoAsync_QuandoNaoExiste_DeveRetornar404()
    {
        var porNumero = Substitute.For<IConsultarCreditoPorNumero>();
        porNumero.ExecutarAsync("naoexiste", Arg.Any<CancellationToken>()).Returns((CreditoRespostaDto?)null);
        var controller = new CreditosController(Substitute.For<IConsultarCreditosPorNfse>(), porNumero);

        var resultado = await controller.ObterPorNumeroCreditoAsync("naoexiste", CancellationToken.None);

        resultado.Should().BeOfType<NotFoundResult>();
    }
}
