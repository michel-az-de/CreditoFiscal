using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using CreditoFiscal.Dominio.Abstracoes;

namespace CreditoFiscal.Infraestrutura.Mensageria;

internal sealed class ServiceBusConsumerSession<T> : IConsumerSession<T>
{
    private readonly ServiceBusReceiver _receptor;
    private readonly List<ReceivedMessage<T>> _mensagens = new List<ReceivedMessage<T>>();
    private readonly Dictionary<ReceivedMessage<T>, ServiceBusReceivedMessage> _originais =
        new Dictionary<ReceivedMessage<T>, ServiceBusReceivedMessage>();

    public ServiceBusConsumerSession(ServiceBusReceiver receptor, IReadOnlyList<ServiceBusReceivedMessage> recebidas, JsonSerializerOptions opcoes)
    {
        _receptor = receptor;
        foreach (var recebida in recebidas)
        {
            var conteudo = JsonSerializer.Deserialize<T>(recebida.Body.ToArray(), opcoes);
            if (conteudo == null)
            {
                continue;   // corpo ilegivel: deixa o lock expirar e o ServiceBus reentrega
            }

            var envelope = new ReceivedMessage<T>(conteudo);
            _mensagens.Add(envelope);
            _originais[envelope] = recebida;
        }
    }

    public IReadOnlyList<ReceivedMessage<T>> Mensagens
    {
        get { return _mensagens; }
    }

    public Task ConfirmarAsync(ReceivedMessage<T> mensagem, CancellationToken ct)
    {
        return _receptor.CompleteMessageAsync(_originais[mensagem], ct);
    }

    public Task RejeitarAsync(ReceivedMessage<T> mensagem, bool reencaminhar, CancellationToken ct)
    {
        if (reencaminhar)
        {
            return _receptor.AbandonMessageAsync(_originais[mensagem], cancellationToken: ct);
        }

        // sem reencaminhar: manda pra dead-letter em vez de descartar silenciosamente
        return _receptor.DeadLetterMessageAsync(_originais[mensagem], cancellationToken: ct);
    }

    public Task EnviarParaDlqAsync(ReceivedMessage<T> mensagem, string motivo, CancellationToken ct)
    {
        // implementacao real em F.2a: dead-letter com motivo explicito via DeadLetterMessageAsync
        throw new NotImplementedException();
    }

    public async ValueTask DisposeAsync()
    {
        await _receptor.DisposeAsync();
    }
}
