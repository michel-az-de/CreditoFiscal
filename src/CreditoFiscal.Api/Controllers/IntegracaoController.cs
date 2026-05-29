using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.CasosDeUso;
using CreditoFiscal.Aplicacao.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CreditoFiscal.Api.Controllers;

[ApiController]
[Route("api/creditos")]
public sealed class IntegracaoController : ControllerBase
{
    // teto de transporte: 2 MiB cobre ~4x o pior caso plausivel (1000 itens x ~520 bytes/item),
    // mantendo defesa real contra payload abusivo. O count check abaixo cobre tráfego dentro
    // da política; este atributo barra os bytes antes de qualquer parsing.
    private const int TamanhoMaximoBody = 2_097_152;
    private const int MaxLote = 1000;

    private readonly IIntegrarCreditos _integrar;

    public IntegracaoController(IIntegrarCreditos integrar)
    {
        _integrar = integrar;
    }

    [HttpPost("integrar-credito-constituido")]
    [RequestSizeLimit(TamanhoMaximoBody)]
    public async Task<IActionResult> IntegrarAsync([FromBody] List<IntegrarCreditoRequisicaoDto> creditos, CancellationToken ct)
    {
        if (creditos.Count > MaxLote)
        {
            ModelState.AddModelError(nameof(creditos), $"Lote excede o maximo de {MaxLote} itens.");
            // Status = 400 evita "status: null" no corpo (auto-sync so existe no .NET 7+).
            var problema = new ValidationProblemDetails(ModelState) { Status = StatusCodes.Status400BadRequest };
            var resultado = new BadRequestObjectResult(problema);
            // paridade de wire com o 400 automatico do [ApiController]: ambos em application/problem+json
            resultado.ContentTypes.Add("application/problem+json");
            return resultado;
        }

        await _integrar.ExecutarAsync(creditos, ct);
        return StatusCode(StatusCodes.Status202Accepted, new IntegracaoRespostaDto { Success = true });
    }
}
