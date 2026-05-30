using System.Collections.Generic;
using Confluent.Kafka;

namespace CreditoFiscal.Infraestrutura.Mensageria;

// Kafka nao tem contador de entrega no protocolo: o adapter mantem o estado em memoria
// indexado por offset. Limitacao aceita: o contador zera em restart do processo e em
// rebalance do consumer group (o offset pode mudar de dono). Em poison loop, o limite
// vira "Nesima tentativa por instancia de consumer", nao "Nesima absoluta".
internal sealed class RegistroDeTentativasKafka
{
    private readonly Dictionary<TopicPartitionOffset, int> _contagens = new Dictionary<TopicPartitionOffset, int>();
    private readonly object _trava = new object();

    // 1-based: primeira entrega vale 1 mesmo sem registro previo
    public int Ler(TopicPartitionOffset offset)
    {
        lock (_trava)
        {
            return _contagens.TryGetValue(offset, out var anteriores) ? anteriores + 1 : 1;
        }
    }

    public void Incrementar(TopicPartitionOffset offset)
    {
        lock (_trava)
        {
            _contagens[offset] = _contagens.TryGetValue(offset, out var atual) ? atual + 1 : 1;
        }
    }

    public void Esquecer(TopicPartitionOffset offset)
    {
        lock (_trava)
        {
            _contagens.Remove(offset);
        }
    }
}
