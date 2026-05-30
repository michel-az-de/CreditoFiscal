namespace CreditoFiscal.Dominio.Abstracoes;

// identidade por referencia: cada entrega e unica, mesmo com Conteudo igual.
// Tentativas e populado pelo adapter conforme o que cada broker expoe: DeliveryCount
// (Service Bus), x-delivery-count de fila quorum (RabbitMQ) ou contagem em memoria
// indexada por offset (Kafka). Default 0 quando o broker nao tem esse sinal.
public sealed class ReceivedMessage<T>
{
    public ReceivedMessage(T conteudo)
    {
        Conteudo = conteudo;
    }

    public T Conteudo { get; }

    public int Tentativas { get; init; }
}
