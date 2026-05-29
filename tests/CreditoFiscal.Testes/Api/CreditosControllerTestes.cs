using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Api.Controllers;
using CreditoFiscal.Api.Dtos;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Dominio.Entidades;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace CreditoFiscal.Testes.Api;

public sealed class CreditosControllerTestes
{
    [Fact]
    public async Task ObterPorNumeroNfseAsync_QuandoExistemCreditos_DeveRetornar200ComLista()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        var creditos = new List<Credito>
        {
            MontarCredito("1", "nfse-1"),
            MontarCredito("2", "nfse-1")
        };
        repositorio.ObterPorNumeroNfseAsync("nfse-1", Arg.Any<CancellationToken>()).Returns(creditos);
        var controller = new CreditosController(repositorio);

        var resultado = await controller.ObterPorNumeroNfseAsync("nfse-1", CancellationToken.None);

        var ok = resultado.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = ok.Value.Should().BeAssignableTo<IEnumerable<CreditoRespostaDto>>().Subject;
        dtos.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObterPorNumeroNfseAsync_QuandoNaoExistem_DeveRetornar404()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ObterPorNumeroNfseAsync("nfse-x", Arg.Any<CancellationToken>()).Returns(new List<Credito>());
        var controller = new CreditosController(repositorio);

        var resultado = await controller.ObterPorNumeroNfseAsync("nfse-x", CancellationToken.None);

        resultado.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ObterPorNumeroCreditoAsync_QuandoExiste_DeveRetornar200ComSimplesNacionalEmTexto()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ObterPorNumeroCreditoAsync("123", Arg.Any<CancellationToken>()).Returns(MontarCredito("123", "nfse-1"));
        var controller = new CreditosController(repositorio);

        var resultado = await controller.ObterPorNumeroCreditoAsync("123", CancellationToken.None);

        var ok = resultado.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<CreditoRespostaDto>().Subject;
        dto.NumeroCredito.Should().Be("123");
        dto.SimplesNacional.Should().Be("Não");
    }

    [Fact]
    public async Task ObterPorNumeroCreditoAsync_QuandoNaoExiste_DeveRetornar404()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ObterPorNumeroCreditoAsync("naoexiste", Arg.Any<CancellationToken>()).Returns((Credito?)null);
        var controller = new CreditosController(repositorio);

        var resultado = await controller.ObterPorNumeroCreditoAsync("naoexiste", CancellationToken.None);

        resultado.Should().BeOfType<NotFoundResult>();
    }

    private static Credito MontarCredito(string numeroCredito, string numeroNfse)
    {
        return new Credito
        {
            NumeroCredito = numeroCredito,
            NumeroNfse = numeroNfse,
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
