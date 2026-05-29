using System;
using CreditoFiscal.Dominio.Entidades;

namespace CreditoFiscal.Aplicacao.Mensagens;

// mensagem da fila: ja validada na borda, com SimplesNacional como enum (nao "Sim"/"Não")
public sealed record CreditoConstituidoDto
{
    public string NumeroCredito { get; init; } = string.Empty;
    public string NumeroNfse { get; init; } = string.Empty;
    public DateTime DataConstituicao { get; init; }
    public decimal ValorIssqn { get; init; }
    public string TipoCredito { get; init; } = string.Empty;
    public SimplesNacional SimplesNacional { get; init; }
    public decimal Aliquota { get; init; }
    public decimal ValorFaturado { get; init; }
    public decimal ValorDeducao { get; init; }
    public decimal BaseCalculo { get; init; }
}
