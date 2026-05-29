using System.Text.Json;
using System.Text.Json.Serialization;
using CreditoFiscal.Infraestrutura.Json;

namespace CreditoFiscal.Infraestrutura.Mensageria;

// opcoes de JSON compartilhadas pelos adapters: camelCase, data sem fuso e enum como texto
internal static class OpcoesJsonMensageria
{
    public static readonly JsonSerializerOptions Padrao = Criar();

    private static JsonSerializerOptions Criar()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opcoes.Converters.Add(new ConversorDeDataSemFusoHorario());
        opcoes.Converters.Add(new JsonStringEnumConverter());
        return opcoes;
    }
}
