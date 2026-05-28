namespace CreditoFiscal.Dominio.Abstracoes;

// Envelope de identidade, nao de valor: cada entrega e unica.
// Igualdade por referencia (sem override de Equals/GetHashCode) garante que duas
// entregas com o mesmo Conteudo ocupem chaves distintas no dicionario interno da sessao.
public sealed class MensagemRecebida<T>
{
    public MensagemRecebida(T conteudo)
    {
        Conteudo = conteudo;
    }

    public T Conteudo { get; }
}
