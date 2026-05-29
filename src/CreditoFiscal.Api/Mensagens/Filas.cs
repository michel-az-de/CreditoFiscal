namespace CreditoFiscal.Api.Mensagens;

// nome unico da fila, compartilhado entre publisher e consumer (evita typo divergente)
public static class Filas
{
    public const string IntegrarCreditoConstituido = "integrar-credito-constituido-entry";
}
