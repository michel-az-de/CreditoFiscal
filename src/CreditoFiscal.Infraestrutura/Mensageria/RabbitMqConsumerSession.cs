using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Dominio.Abstracoes;
using RabbitMQ.Client;

namespace CreditoFiscal.Infraestrutura.Mensageria;

// uma sessao = um canal proprio; ReceivedMessage (identidade) mapeia o DeliveryTag pro ack/nack
internal sealed class RabbitMqConsumerSession<T> : IConsumerSession<T>
{
    private readonly IModel _canal;
    private readonly JsonSerializerOptions _opcoes;
    private readonly string _nomeDaDlq;
    private readonly List<ReceivedMessage<T>> _mensagens = new List<ReceivedMessage<T>>();
    private readonly Dictionary<ReceivedMessage<T>, ulong> _tags = new Dictionary<ReceivedMessage<T>, ulong>();

    public RabbitMqConsumerSession(IModel canal, string fila, int maximo, TimeSpan timeout, JsonSerializerOptions opcoes)
    {
        _canal = canal;
        _opcoes = opcoes;
        _nomeDaDlq = fila + "-dlq";
        Drenar(fila, maximo, timeout);
    }

    public IReadOnlyList<ReceivedMessage<T>> Mensagens
    {
        get { return _mensagens; }
    }

    public Task ConfirmarAsync(ReceivedMessage<T> mensagem, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _canal.BasicAck(_tags[mensagem], multiple: false);
        return Task.CompletedTask;
    }

    public Task RejeitarAsync(ReceivedMessage<T> mensagem, bool reencaminhar, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _canal.BasicNack(_tags[mensagem], multiple: false, requeue: reencaminhar);
        return Task.CompletedTask;
    }

    public Task EnviarParaDlqAsync(ReceivedMessage<T> mensagem, string motivo, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var propriedades = _canal.CreateBasicProperties();
        propriedades.Persistent = true;
        propriedades.ContentType = "application/json";
        propriedades.Headers = new Dictionary<string, object>
        {
            ["x-dlq-motivo"] = motivo
        };

        // publica na DLQ via default exchange + routing key. A DLQ existe (declarada pelo
        // CriadorDeFilas), entao a mensagem nao se perde mesmo com mandatory:false.
        var corpo = JsonSerializer.SerializeToUtf8Bytes(mensagem.Conteudo, _opcoes);
        _canal.BasicPublish(exchange: string.Empty, routingKey: _nomeDaDlq, mandatory: false, basicProperties: propriedades, body: corpo);

        // ack da original so depois do publish na DLQ: se o publish falhar, a mensagem
        // continua disponivel pra reentrega ate o broker atingir x-delivery-limit.
        _canal.BasicAck(_tags[mensagem], multiple: false);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        // fechar o canal devolve pra fila tudo que ainda nao recebeu ack (redelivered=true)
        _canal.Dispose();
        return ValueTask.CompletedTask;
    }

    private void Drenar(string fila, int maximo, TimeSpan timeout)
    {
        var cronometro = Stopwatch.StartNew();
        for (var i = 0; i < maximo; i++)
        {
            if (cronometro.Elapsed >= timeout)
            {
                break;
            }

            var resultado = _canal.BasicGet(fila, autoAck: false);
            if (resultado == null)
            {
                break;   // fila vazia: sai no primeiro null, sem busy-spin
            }

            var conteudo = JsonSerializer.Deserialize<T>(resultado.Body.Span, _opcoes);
            if (conteudo == null)
            {
                // corpo ilegivel: rejeita sem reenfileirar pra nao travar a fila num veneno
                _canal.BasicNack(resultado.DeliveryTag, multiple: false, requeue: false);
                continue;
            }

            var envelope = new ReceivedMessage<T>(conteudo)
            {
                Tentativas = LerTentativasDeHeader(resultado.BasicProperties)
            };
            _mensagens.Add(envelope);
            _tags[envelope] = resultado.DeliveryTag;
        }
    }

    private static int LerTentativasDeHeader(IBasicProperties propriedades)
    {
        // quorum queue adiciona x-delivery-count: 0 na primeira entrega, +1 a cada redelivery.
        // Tentativas e 1-based ("esta e a Nesima entrega"), alinhado com Service Bus DeliveryCount.
        if (propriedades?.Headers == null)
        {
            return 1;
        }

        if (!propriedades.Headers.TryGetValue("x-delivery-count", out var valor))
        {
            return 1;
        }

        return valor switch
        {
            long l => (int)l + 1,
            int i => i + 1,
            _ => 1
        };
    }
}
