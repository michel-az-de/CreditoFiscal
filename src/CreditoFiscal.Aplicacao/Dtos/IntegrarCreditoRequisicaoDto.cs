using System;
using System.ComponentModel.DataAnnotations;

namespace CreditoFiscal.Aplicacao.Dtos;

// contrato de entrada do POST: SimplesNacional chega como "Sim"/"Não" (texto do cliente)
public sealed record IntegrarCreditoRequisicaoDto
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string NumeroCredito { get; init; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string NumeroNfse { get; init; } = string.Empty;

    [Required]
    public DateTime? DataConstituicao { get; init; }

    [Range(typeof(decimal), "0", "9999999999999.99")]
    public decimal ValorIssqn { get; init; }

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string TipoCredito { get; init; } = string.Empty;

    [Required]
    public string SimplesNacional { get; init; } = string.Empty;

    [Range(typeof(decimal), "0", "100")]
    public decimal Aliquota { get; init; }

    [Range(typeof(decimal), "0", "9999999999999.99")]
    public decimal ValorFaturado { get; init; }

    [Range(typeof(decimal), "0", "9999999999999.99")]
    public decimal ValorDeducao { get; init; }

    [Range(typeof(decimal), "0", "9999999999999.99")]
    public decimal BaseCalculo { get; init; }
}
