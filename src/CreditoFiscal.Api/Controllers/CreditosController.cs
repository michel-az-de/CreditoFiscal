using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Api.Dtos;
using CreditoFiscal.Api.Mapeamentos;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Dominio.Entidades;
using Microsoft.AspNetCore.Mvc;

namespace CreditoFiscal.Api.Controllers;

[ApiController]
[Route("api/creditos")]
public sealed class CreditosController : ControllerBase
{
    private readonly ICreditoRepository _repositorio;

    public CreditosController(ICreditoRepository repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet("{numeroNfse}")]
    public async Task<IActionResult> ObterPorNumeroNfseAsync(string numeroNfse, CancellationToken ct)
    {
        var creditos = await _repositorio.ObterPorNumeroNfseAsync(numeroNfse, ct);
        if (creditos.Count == 0)
        {
            return NotFound();
        }

        var resposta = new List<CreditoRespostaDto>();
        foreach (var credito in creditos)
        {
            resposta.Add(MapearParaDto(credito));
        }

        return Ok(resposta);
    }

    [HttpGet("credito/{numeroCredito}")]
    public async Task<IActionResult> ObterPorNumeroCreditoAsync(string numeroCredito, CancellationToken ct)
    {
        var credito = await _repositorio.ObterPorNumeroCreditoAsync(numeroCredito, ct);
        if (credito == null)
        {
            return NotFound();
        }

        return Ok(MapearParaDto(credito));
    }

    private static CreditoRespostaDto MapearParaDto(Credito credito)
    {
        return new CreditoRespostaDto
        {
            NumeroCredito = credito.NumeroCredito,
            NumeroNfse = credito.NumeroNfse,
            DataConstituicao = credito.DataConstituicao,
            ValorIssqn = credito.ValorIssqn,
            TipoCredito = credito.TipoCredito,
            SimplesNacional = ConversorSimplesNacional.ParaString(credito.SimplesNacional),
            Aliquota = credito.Aliquota,
            ValorFaturado = credito.ValorFaturado,
            ValorDeducao = credito.ValorDeducao,
            BaseCalculo = credito.BaseCalculo
        };
    }
}
