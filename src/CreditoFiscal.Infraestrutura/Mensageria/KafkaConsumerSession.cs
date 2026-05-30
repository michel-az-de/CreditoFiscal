using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using CreditoFiscal.Dominio.Abstracoes;

namespace CreditoFiscal.Infraestrutura.Mensageria;

internal sealed class KafkaConsumerSession<T> : IConsumerSession<T>
{
    private readonly IConsumer<Null, byte[]> _consumidor;
    private readonly IProducer<Null, byte[]> _produtor;
    private readonly RegistroDeTentativasKafka _registro;
    private readonly string _nomeDaDlq;
    private readonly JsonSerializerOptions _opcoes;
    private readonly List<ReceivedMessage<T>> _mensagens = new List<ReceivedMessage<T>>();
    private readonly Dictionary<ReceivedMessage<T>, ConsumeResult<Null, byte[]>> _originais =
        new Dictionary<ReceivedMessage<T>, ConsumeResult<Null, byte[]>>();

    public KafkaConsumerSession(
        IConsumer<Null, byte[]> consumidor,
        IProducer<Null, byte[]> produtor,
        RegistroDeTentativasKafka registro,
        string nomeDaDlq,
        IReadOnlyList<ConsumeResult<Null, byte[]>> lote,
        JsonSerializerOptions opcoes)
    {
        _consumidor = consumidor;
        _produtor = produtor;
        _registro = registro;
        _nomeDaDlq = nomeDaDlq;
        _opcoes = opcoes;

        foreach (var resultado in lote)
        {
            var conteudo = JsonSerializer.Deserialize<T>(resultado.Message.Value, opcoes);
            if (conteudo == null)
            {
                continue;
            }

            var envelope = new ReceivedMessage<T>(conteudo)
            {
                Tentativas = _registro.Ler(resultado.TopicPartitionOffset)
            };
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
        var resultado = _originais[mensagem];
        _consumidor.Commit(resultado);
        _registro.Esquecer(resultado.TopicPartitionOffset);
        return Task.CompletedTask;
    }

    public Task RejeitarAsync(ReceivedMessage<T> mensagem, bool reencaminhar, CancellationToken ct)
    {
        // Kafka nao tem requeue: pra reler, reposiciona o offset no ponto da mensagem
        if (reencaminhar)
        {
            var resultado = _originais[mensagem];
            // incrementa antes do seek: a proxima leitura desse offset ja le o valor novo
            _registro.Incrementar(resultado.TopicPartitionOffset);
            _consumidor.Seek(resultado.TopicPartitionOffset);
        }

        return Task.CompletedTask;
    }

    public async Task EnviarParaDlqAsync(ReceivedMessage<T> mensagem, string motivo, CancellationToken ct)
    {
        var resultado = _originais[mensagem];
        var corpo = JsonSerializer.SerializeToUtf8Bytes(mensagem.Conteudo, _opcoes);
        var mensagemDlq = new Message<Null, byte[]>
        {
            Value = corpo,
            Headers = new Headers
            {
                new Header("x-dlq-motivo", Encoding.UTF8.GetBytes(motivo))
            }
        };
        await _produtor.ProduceAsync(_nomeDaDlq, mensagemDlq, ct);

        // commita o offset original so depois do publish: se o produce falhar, a mensagem
        // permanece e na proxima sessao (apos restart ou novo seek) sera relida.
        _consumidor.Commit(resultado);
        _registro.Esquecer(resultado.TopicPartitionOffset);
    }

    public ValueTask DisposeAsync()
    {
        // o consumidor e de vida longa (singleton do adapter); a sessao nao o fecha
        return ValueTask.CompletedTask;
    }
}
