using System;

namespace CreditoFiscal.Aplicacao.Dtos;

// espelha Credito; SimplesNacional sai como "Sim"/"Não" no JSON
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
