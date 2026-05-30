using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using CreditoFiscal.Dominio.Abstracoes;

namespace CreditoFiscal.Infraestrutura.Mensageria;

internal sealed class KafkaConsumerSession<T> : IConsumerSession<T>
{
    private readonly IConsumer<Null, byte[]> _consumidor;
    private readonly List<ReceivedMessage<T>> _mensagens = new List<ReceivedMessage<T>>();
    private readonly Dictionary<ReceivedMessage<T>, ConsumeResult<Null, byte[]>> _originais =
        new Dictionary<ReceivedMessage<T>, ConsumeResult<Null, byte[]>>();

    public KafkaConsumerSession(IConsumer<Null, byte[]> consumidor, IReadOnlyList<ConsumeResult<Null, byte[]>> lote, JsonSerializerOptions opcoes)
    {
        _consumidor = consumidor;
        foreach (var resultado in lote)
        {
            var conteudo = JsonSerializer.Deserialize<T>(resultado.Message.Value, opcoes);
            if (conteudo == null)
            {
                continue;
            }

            var envelope = new ReceivedMessage<T>(conteudo);
            _mensagens.Add(envelope);
            _originais[envelope] = resultado;
        }
    }

    public IReadOnlyList<ReceivedMessage<T>> Mensagens
    {
        get { return _mensagens; }
    }

    public Task ConfirmarAsync(ReceivedMessage<T> mensagem, CancellationToken ct)
    {
        // Kafka confirma por offset, nao por mensagem: commit do offset desta entrega
        _consumidor.Commit(_originais[mensagem]);
        return Task.CompletedTask;
    }

    public Task RejeitarAsync(ReceivedMessage<T> mensagem, bool reencaminhar, CancellationToken ct)
    {
        // Kafka nao tem requeue: pra reler, reposiciona o offset no ponto da mensagem
        if (reencaminhar)
        {
            _consumidor.Seek(_originais[mensagem].TopicPartitionOffset);
        }

        return Task.CompletedTask;
    }

    public Task EnviarParaDlqAsync(ReceivedMessage<T> mensagem, string motivo, CancellationToken ct)
    {
        // implementacao real em F.2c: publica no topic de DLQ e comita o offset original
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        // o consumidor e de vida longa (singleton do adapter); a sessao nao o fecha
        return ValueTask.CompletedTask;
    }
}
