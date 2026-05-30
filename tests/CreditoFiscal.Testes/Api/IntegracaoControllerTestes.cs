using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Api.Controllers;
using CreditoFiscal.Aplicacao.CasosDeUso;
using CreditoFiscal.Aplicacao.Dtos;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace CreditoFiscal.Testes.Api;

public sealed class IntegracaoControllerTestes
{
    [Fact]
    public async Task IntegrarAsync_DeveDelegarAoCasoDeUsoERetornar202()
    {
        var integrar = Substitute.For<IIntegrarCreditos>();
        var controller = new IntegracaoController(integrar);
        var creditos = new List<IntegrarCreditoRequisicaoDto>
        {
            new IntegrarCreditoRequisicaoDto { NumeroCredito = "1", SimplesNacional = "Sim" }
        };

        var resultado = await controller.IntegrarAsync(creditos, CancellationToken.None);

        await integrar.Received(1).ExecutarAsync(creditos, Arg.Any<CancellationToken>());
        var resposta = resultado.Should().BeOfType<ObjectResult>().Subject;
        resposta.StatusCode.Should().Be(202);
        resposta.Value.Should().BeOfType<IntegracaoRespostaDto>().Which.Success.Should().BeTrue();
    }

    [Fact]
    public async Task IntegrarAsync_QuandoLoteExcedeMaximo_DeveRetornar400ComValidationProblemDetails()
    {
        var integrar = Substitute.For<IIntegrarCreditos>();
        var controller = new IntegracaoController(integrar);
        // ModelState eh propriedade lazy do ControllerBase a partir do ControllerContext;
        // sem HttpContext aqui, AddModelError nao registra o erro.
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // 1001 itens (1 acima do limite definido no controller)
        var creditos = Enumerable.Range(0, 1001)
            .Select(i => new IntegrarCreditoRequisicaoDto { NumeroCredito = i.ToString(), SimplesNacional = "Sim" })
            .ToList();

        var resultado = await controller.IntegrarAsync(creditos, CancellationToken.None);

        var badRequest = resultado.Should().BeOfType<BadRequestObjectResult>().Subject;
        // paridade de wire: 400 manual sai como application/problem+json igual ao automatico
        badRequest.ContentTypes.Should().Contain("application/problem+json");
        var problema = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problema.Errors.Should().ContainKey("creditos");
        // sem Status=400 explicito o corpo serializaria "status": null no .NET 6 (auto-sync veio no 7)
        problema.Status.Should().Be(400);
        await integrar.DidNotReceive().ExecutarAsync(Arg.Any<List<IntegrarCreditoRequisicaoDto>>(), Arg.Any<CancellationToken>());
    }
}
