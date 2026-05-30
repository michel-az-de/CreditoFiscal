namespace CreditoFiscal.Aplicacao.Mensagens;

// nome unico da fila, compartilhado entre publisher e consumer (evita typo divergente).
// A DLQ segue a convencao <fila>-dlq; adapters em Infraestrutura derivam o nome do
// parametro de fila recebido, e este const e a fonte de verdade para quem consome a
// constante (consumer, narracao em log/teste). Convencao explicita evita divergencia.
public static class Filas
{
    public const string IntegrarCreditoConstituido = "integrar-credito-constituido-entry";
    public const string IntegrarCreditoConstituidoDlq = IntegrarCreditoConstituido + "-dlq";
    public const string ConsultaCreditoRealizada = "consulta-credito-realizada";
}
