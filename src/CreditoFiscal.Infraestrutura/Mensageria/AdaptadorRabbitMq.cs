using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Dominio.Abstracoes;
using RabbitMQ.Client;

namespace CreditoFiscal.Infraestrutura.Mensageria;

// adapter RabbitMQ: so a IConnection e compartilhada; o IModel vive local (nao e thread-safe)
public sealed class AdaptadorRabbitMq : IMensagemPublisher, IMensagemConsumer
{
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
        var corpo = JsonSerializer.SerializeToUtf8Bytes(mensagem, OpcoesJsonMensageria.Padrao);

        var propriedades = canal.CreateBasicProperties();
        propriedades.Persistent = true;
        propriedades.ContentType = "application/json";

        canal.BasicPublish(exchange: "", routingKey: fila, mandatory: false, basicProperties: propriedades, body: corpo);
        return Task.CompletedTask;
    }

    public Task<IConsumerSession<T>> AbrirSessaoAsync<T>(string fila, int maximo, TimeSpan timeout, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // a sessao assume a posse do canal e o descarta no fim (o DeliveryTag e preso a ele)
        var canal = _conexao.CreateModel();
        try
        {
            IConsumerSession<T> sessao = new RabbitMqConsumerSession<T>(canal, fila, maximo, timeout, OpcoesJsonMensageria.Padrao);
            return Task.FromResult(sessao);
        }
        catch
        {
            canal.Dispose();
            throw;
        }
    }
}
