using System;
using CreditoFiscal.Dominio.Entidades;
using FluentAssertions;
using Xunit;

namespace CreditoFiscal.Testes.Dominio;

public sealed class CreditoTestes
{
    [Fact]
    public void Construtor_QuandoTodosOsCamposPreenchidos_DeveExporValores()
    {
        var data = new DateTime(2024, 5, 28);

        var credito = new Credito
        {
            Id = 1,
            NumeroCredito = "123456",
            NumeroNfse = "7891011",
            DataConstituicao = data,
            ValorIssqn = 1500.75m,
            TipoCredito = "ISSQN",
            SimplesNacional = SimplesNacional.Optante,
            Aliquota = 5.0m,
            ValorFaturado = 30000m,
            ValorDeducao = 5000m,
            BaseCalculo = 25000m
        };

        credito.Id.Should().Be(1);
        credito.NumeroCredito.Should().Be("123456");
        credito.NumeroNfse.Should().Be("7891011");
        credito.DataConstituicao.Should().Be(data);
        credito.ValorIssqn.Should().Be(1500.75m);
        credito.TipoCredito.Should().Be("ISSQN");
        credito.SimplesNacional.Should().Be(SimplesNacional.Optante);
        credito.Aliquota.Should().Be(5.0m);
        credito.ValorFaturado.Should().Be(30000m);
        credito.ValorDeducao.Should().Be(5000m);
        credito.BaseCalculo.Should().Be(25000m);
    }
}
