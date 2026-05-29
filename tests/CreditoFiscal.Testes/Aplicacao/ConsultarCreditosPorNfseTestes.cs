using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.CasosDeUso;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Dominio.Entidades;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CreditoFiscal.Testes.Aplicacao;

public sealed class ConsultarCreditosPorNfseTestes
{
    [Fact]
    public async Task ExecutarAsync_QuandoExistem_DeveRetornarDtosComSimplesNacionalEmTexto()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ObterPorNumeroNfseAsync("nfse-1", Arg.Any<CancellationToken>())
            .Returns(new List<Credito> { MontarCredito("1"), MontarCredito("2") });
        var casoDeUso = new ConsultarCreditosPorNfse(repositorio);

        var resultado = await casoDeUso.ExecutarAsync("nfse-1", CancellationToken.None);

        resultado.Should().HaveCount(2);
        resultado[0].SimplesNacional.Should().Be("Não");
    }

    [Fact]
    public async Task ExecutarAsync_QuandoNaoExistem_DeveRetornarListaVazia()
    {
        var repositorio = Substitute.For<ICreditoRepository>();
        repositorio.ObterPorNumeroNfseAsync("nfse-x", Arg.Any<CancellationToken>())
            .Returns(new List<Credito>());
        var casoDeUso = new ConsultarCreditosPorNfse(repositorio);

        var resultado = await casoDeUso.ExecutarAsync("nfse-x", CancellationToken.None);

        resultado.Should().BeEmpty();
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
