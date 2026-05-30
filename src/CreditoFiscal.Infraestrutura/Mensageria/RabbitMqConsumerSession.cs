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
    private readonly List<ReceivedMessage<T>> _mensagens = new List<ReceivedMessage<T>>();
    private readonly Dictionary<ReceivedMessage<T>, ulong> _tags = new Dictionary<ReceivedMessage<T>, ulong>();

    public RabbitMqConsumerSession(IModel canal, string fila, int maximo, TimeSpan timeout, JsonSerializerOptions opcoes)
    {
        _canal = canal;
        Drenar(fila, maximo, timeout, opcoes);
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
        // implementacao real em F.2b: publica na fila DLQ e da ack na original
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        // fechar o canal devolve pra fila tudo que ainda nao recebeu ack (redelivered=true)
        _canal.Dispose();
        return ValueTask.CompletedTask;
    }

    private void Drenar(string fila, int maximo, TimeSpan timeout, JsonSerializerOptions opcoes)
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

            var conteudo = JsonSerializer.Deserialize<T>(resultado.Body.Span, opcoes);
            if (conteudo == null)
            {
                // corpo ilegivel: rejeita sem reenfileirar pra nao travar a fila num veneno
                _canal.BasicNack(resultado.DeliveryTag, multiple: false, requeue: false);
                continue;
            }

            var envelope = new ReceivedMessage<T>(conteudo);
            _mensagens.Add(envelope);
            _tags[envelope] = resultado.DeliveryTag;
        }
    }
}
