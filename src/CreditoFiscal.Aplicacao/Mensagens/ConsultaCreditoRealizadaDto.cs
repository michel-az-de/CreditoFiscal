using System;

namespace CreditoFiscal.Aplicacao.Mensagens;

// evento de auditoria publicado em "consulta-credito-realizada" a cada GET:
// permite consumidor externo guardar log/metrica sem acoplar a API
public sealed record ConsultaCreditoRealizadaDto
{
    public string Tipo { get; init; } = string.Empty;
    public string Chave { get; init; } = string.Empty;
    public int QuantidadeRetornada { get; init; }
    public DateTime OcorridoEm { get; init; }
}
