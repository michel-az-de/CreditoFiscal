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
    private readonly IIntegrarCreditos _integrar;

    public IntegracaoController(IIntegrarCreditos integrar)
    {
        _integrar = integrar;
    }

    [HttpPost("integrar-credito-constituido")]
    public async Task<IActionResult> IntegrarAsync([FromBody] List<IntegrarCreditoRequisicaoDto> creditos, CancellationToken ct)
    {
        await _integrar.ExecutarAsync(creditos, ct);
        return StatusCode(StatusCodes.Status202Accepted, new IntegracaoRespostaDto { Success = true });
    }
}
