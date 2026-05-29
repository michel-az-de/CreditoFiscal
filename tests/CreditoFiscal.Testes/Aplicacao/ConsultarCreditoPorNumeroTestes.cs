using System;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.CasosDeUso;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Dominio.Entidades;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CreditoFiscal.Testes.Aplicacao;

public sealed class ConsultarCreditoPorNumeroTestes
{
    [Fact]
    public async Task ExecutarAsync_QuandoExiste_DeveRetornarDtoMapeado()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ObterPorNumeroCreditoAsync("123", Arg.Any<CancellationToken>()).Returns(MontarCredito("123"));
        var casoDeUso = new ConsultarCreditoPorNumero(repositorio);

        var resultado = await casoDeUso.ExecutarAsync("123", CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.NumeroCredito.Should().Be("123");
        resultado.SimplesNacional.Should().Be("Não");
    }

    [Fact]
    public async Task ExecutarAsync_QuandoNaoExiste_DeveRetornarNull()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ObterPorNumeroCreditoAsync("naoexiste", Arg.Any<CancellationToken>()).Returns((Credito?)null);
        var casoDeUso = new ConsultarCreditoPorNumero(repositorio);

        var resultado = await casoDeUso.ExecutarAsync("naoexiste", CancellationToken.None);

        resultado.Should().BeNull();
    }

    private static Credito MontarCredito(string numeroCredito)
    {
        return new Credito
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
