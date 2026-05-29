using System;

namespace CreditoFiscal.Api.Dtos;

// resposta dos GETs: espelha o credito, mas com SimplesNacional em "Sim"/"Não" pro cliente
public sealed record CreditoRespostaDto
{
    public string NumeroCredito { get; init; } = string.Empty;
    public string NumeroNfse { get; init; } = string.Empty;
    public DateTime DataConstituicao { get; init; }
    public decimal ValorIssqn { get; init; }
    public string TipoCredito { get; init; } = string.Empty;
    public string SimplesNacional { get; init; } = string.Empty;
    public decimal Aliquota { get; init; }
    public decimal ValorFaturado { get; init; }
    public decimal ValorDeducao { get; init; }
    public decimal BaseCalculo { get; init; }
}
