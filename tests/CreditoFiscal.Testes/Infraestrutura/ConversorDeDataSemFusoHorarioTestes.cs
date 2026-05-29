using System;
using System.Text.Json;
using CreditoFiscal.Infraestrutura.Json;
using FluentAssertions;
using Xunit;

namespace CreditoFiscal.Testes.Infraestrutura;

public sealed class ConversorDeDataSemFusoHorarioTestes
{
    private static readonly JsonSerializerOptions Opcoes = CriarOpcoes();

    private static JsonSerializerOptions CriarOpcoes()
    {
        var opcoes = new JsonSerializerOptions();
        opcoes.Converters.Add(new ConversorDeDataSemFusoHorario());
        return opcoes;
    }

    [Theory]
    [InlineData("\"2024-02-25\"")]
    [InlineData("\"2024-02-25T00:00:00Z\"")]
    [InlineData("\"2024-02-25T09:30:00-03:00\"")]
    public void Read_QuandoFormatosVariados_DeveNormalizarParaDataSemFuso(string json)
    {
        var data = JsonSerializer.Deserialize<DateTime>(json, Opcoes);

        data.Kind.Should().Be(DateTimeKind.Unspecified);
        data.Should().Be(new DateTime(2024, 2, 25));
    }

    [Fact]
    public void Write_QuandoData_DeveEscreverApenasODia()
    {
        var json = JsonSerializer.Serialize(new DateTime(2024, 2, 25), Opcoes);

        json.Should().Be("\"2024-02-25\"");
    }
}
