namespace CreditoFiscal.Dominio.Abstracoes;

// identidade por referencia: cada entrega e unica, mesmo com Conteudo igual
public sealed class ReceivedMessage<T>
{
    public ReceivedMessage(T conteudo)
    {
        Conteudo = conteudo;
    }

    public T Conteudo { get; }
}
