using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using CreditoFiscal.Dominio.Abstracoes;

namespace CreditoFiscal.Infraestrutura.Mensageria;

// adapter Kafka. Publicar = produce (encaixa direto). No consumo, o modelo de streaming do
// Kafka (offset por particao) nao tem ack/requeue por mensagem: Confirmar = commit do offset,
// Rejeitar = seek de volta pra reler. Caveat documentado no README.
public sealed class AdaptadorKafka : IMensagemPublisher, IMensagemConsumer
{
    private readonly IProducer<Null, byte[]> _produtor;
    private readonly IConsumer<Null, byte[]> _consumidor;
    private readonly object _trava = new object();
    private string? _topicoAssinado;

    public AdaptadorKafka(IProducer<Null, byte[]> produtor, IConsumer<Null, byte[]> consumidor)
    {
        _produtor = produtor;
        _consumidor = consumidor;
    }

    public async Task PublicarAsync<T>(string fila, T mensagem, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var corpo = JsonSerializer.SerializeToUtf8Bytes(mensagem, OpcoesJsonMensageria.Padrao);
        await _produtor.ProduceAsync(fila, new Message<Null, byte[]> { Value = corpo }, ct);
    }

    public Task<IConsumerSession<T>> AbrirSessaoAsync<T>(string fila, int maximo, TimeSpan timeout, CancellationToken ct)
    {
        GarantirAssinatura(fila);

        var lote = new List<ConsumeResult<Null, byte[]>>();
        var cronometro = Stopwatch.StartNew();
        for (var i = 0; i < maximo; i++)
        {
            var restante = timeout - cronometro.Elapsed;
            if (restante <= TimeSpan.Zero)
            {
                break;
            }

            var resultado = _consumidor.Consume(restante);
            if (resultado == null || resultado.Message == null)
            {
                break;   // nada novo dentro da janela
            }

            lote.Add(resultado);
        }

        IConsumerSession<T> sessao = new KafkaConsumerSession<T>(_consumidor, lote, OpcoesJsonMensageria.Padrao);
        return Task.FromResult(sessao);
    }

    private void GarantirAssinatura(string topico)
    {
        // consumidor de vida longa: assina o topico uma vez so
        lock (_trava)
        {
            if (_topicoAssinado == topico)
            {
                return;
            }

            _consumidor.Subscribe(topico);
            _topicoAssinado = topico;
        }
    }
}
