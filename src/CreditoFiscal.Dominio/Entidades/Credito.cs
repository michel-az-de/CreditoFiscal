using System;

namespace CreditoFiscal.Dominio.Entidades;

public sealed class Credito
{
    public long Id { get; init; }
    public string NumeroCredito { get; init; } = string.Empty;
    public string NumeroNfse { get; init; } = string.Empty;
    public DateTime DataConstituicao { get; init; }
    public decimal ValorIssqn { get; init; }
    public string TipoCredito { get; init; } = string.Empty;
    public bool SimplesNacional { get; init; }
    public decimal Aliquota { get; init; }
    public decimal ValorFaturado { get; init; }
    public decimal ValorDeducao { get; init; }
    public decimal BaseCalculo { get; init; }
}
