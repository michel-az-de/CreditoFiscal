using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Infraestrutura.Json;
using RabbitMQ.Client;

namespace CreditoFiscal.Infraestrutura.Mensageria;

// adapter RabbitMQ: so a IConnection e compartilhada; o IModel vive local (nao e thread-safe)
public sealed class AdaptadorRabbitMq : IMensagemPublisher
{
    private static readonly JsonSerializerOptions OpcoesJson = CriarOpcoes();

    private readonly IConnection _conexao;

    public AdaptadorRabbitMq(IConnection conexao)
    {
        _conexao = conexao;
    }

    public Task PublicarAsync<T>(string fila, T mensagem, CancellationToken ct)
    {
        // driver 6.x e sincrono: checamos o cancelamento aqui, nao da pra propagar no BasicPublish
        ct.ThrowIfCancellationRequested();

        using var canal = _conexao.CreateModel();
        var corpo = JsonSerializer.SerializeToUtf8Bytes(mensagem, OpcoesJson);

        var propriedades = canal.CreateBasicProperties();
        propriedades.Persistent = true;
        propriedades.ContentType = "application/json";

        canal.BasicPublish(exchange: "", routingKey: fila, mandatory: false, basicProperties: propriedades, body: corpo);
        return Task.CompletedTask;
    }

    private static JsonSerializerOptions CriarOpcoes()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opcoes.Converters.Add(new ConversorDeDataSemFusoHorario());
        opcoes.Converters.Add(new JsonStringEnumConverter());
        return opcoes;
    }
}
