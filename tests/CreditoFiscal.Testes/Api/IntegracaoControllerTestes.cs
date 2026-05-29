using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Api.Controllers;
using CreditoFiscal.Aplicacao.CasosDeUso;
using CreditoFiscal.Aplicacao.Dtos;
using FluentAssertions;
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
}
