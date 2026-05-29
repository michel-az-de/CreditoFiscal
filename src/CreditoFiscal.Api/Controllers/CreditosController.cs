using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.CasosDeUso;
using Microsoft.AspNetCore.Mvc;

namespace CreditoFiscal.Api.Controllers;

[ApiController]
[Route("api/creditos")]
public sealed class CreditosController : ControllerBase
{
    private readonly IConsultarCreditosPorNfse _porNfse;
    private readonly IConsultarCreditoPorNumero _porNumero;

    public CreditosController(IConsultarCreditosPorNfse porNfse, IConsultarCreditoPorNumero porNumero)
    {
        _porNfse = porNfse;
        _porNumero = porNumero;
    }

    [HttpGet("{numeroNfse}")]
    public async Task<IActionResult> ObterPorNumeroNfseAsync(string numeroNfse, CancellationToken ct)
    {
        var creditos = await _porNfse.ExecutarAsync(numeroNfse, ct);
        if (creditos.Count == 0)
        {
            return NotFound();
        }

        return Ok(creditos);
    }

    [HttpGet("credito/{numeroCredito}")]
    public async Task<IActionResult> ObterPorNumeroCreditoAsync(string numeroCredito, CancellationToken ct)
    {
        var credito = await _porNumero.ExecutarAsync(numeroCredito, ct);
        if (credito == null)
        {
            return NotFound();
        }

        return Ok(credito);
    }
}
