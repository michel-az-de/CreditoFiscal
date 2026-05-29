using System;
using CreditoFiscal.Api.Mapeamentos;
using CreditoFiscal.Dominio.Entidades;
using FluentAssertions;
using Xunit;

namespace CreditoFiscal.Testes.Api;

public sealed class ConversorSimplesNacionalTestes
{
    [Theory]
    [InlineData("Sim", SimplesNacional.Optante)]
    [InlineData("Não", SimplesNacional.NaoOptante)]
    public void ParaEnum_QuandoValorValido_DeveRetornarEnumEsperado(string entrada, SimplesNacional esperado)
    {
        var resultado = ConversorSimplesNacional.ParaEnum(entrada);

        resultado.Should().Be(esperado);
    }

    [Theory]
    [InlineData("Talvez")]
    [InlineData("")]
    [InlineData("sim")]
    [InlineData("não")]
    [InlineData("SIM")]
    [InlineData("S")]
    public void ParaEnum_QuandoValorInvalido_DeveLancarArgumentException(string entrada)
    {
        var acao = () => ConversorSimplesNacional.ParaEnum(entrada);

        acao.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(SimplesNacional.Optante, "Sim")]
    [InlineData(SimplesNacional.NaoOptante, "Não")]
    public void ParaString_QuandoValorEnum_DeveRetornarStringEsperada(SimplesNacional entrada, string esperado)
    {
        var resultado = ConversorSimplesNacional.ParaString(entrada);

        resultado.Should().Be(esperado);
    }
}
