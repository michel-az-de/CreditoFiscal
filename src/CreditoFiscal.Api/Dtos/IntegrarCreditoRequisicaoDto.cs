using System;

namespace CreditoFiscal.Api.Dtos;

// contrato de entrada do POST: SimplesNacional chega como "Sim"/"Não" (texto do cliente)
public sealed record IntegrarCreditoRequisicaoDto
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
