using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Api.Dtos;
using CreditoFiscal.Api.Mapeamentos;
using CreditoFiscal.Api.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CreditoFiscal.Api.Controllers;

[ApiController]
[Route("api/creditos")]
public sealed class IntegracaoController : ControllerBase
{
    private readonly IMensagemPublisher _publisher;

    public IntegracaoController(IMensagemPublisher publisher)
    {
        _publisher = publisher;
    }

    [HttpPost("integrar-credito-constituido")]
    public async Task<IActionResult> IntegrarAsync([FromBody] List<IntegrarCreditoRequisicaoDto> creditos, CancellationToken ct)
    {
        // valida o lote inteiro antes de publicar: um SimplesNacional invalido derruba
        // tudo com 400, sem deixar metade dos creditos na fila
        var mensagens = new List<CreditoConstituidoDto>();
        foreach (var requisicao in creditos)
        {
            mensagens.Add(ConverterParaMensagem(requisicao));
        }

        // publica um a um: cada credito e uma mensagem independente (sem bulk)
        foreach (var mensagem in mensagens)
        {
            await _publisher.PublicarAsync(Filas.IntegrarCreditoConstituido, mensagem, ct);
        }

        return StatusCode(StatusCodes.Status202Accepted, new IntegracaoRespostaDto { Success = true });
    }

    private static CreditoConstituidoDto ConverterParaMensagem(IntegrarCreditoRequisicaoDto requisicao)
    {
        return new CreditoConstituidoDto
        {
            NumeroCredito = requisicao.NumeroCredito,
            NumeroNfse = requisicao.NumeroNfse,
            DataConstituicao = requisicao.DataConstituicao,
            ValorIssqn = requisicao.ValorIssqn,
            TipoCredito = requisicao.TipoCredito,
            // "Sim"/"Não" invalido lanca ArgumentException aqui -> middleware devolve 400
            SimplesNacional = ConversorSimplesNacional.ParaEnum(requisicao.SimplesNacional),
            Aliquota = requisicao.Aliquota,
            ValorFaturado = requisicao.ValorFaturado,
            ValorDeducao = requisicao.ValorDeducao,
            BaseCalculo = requisicao.BaseCalculo
        };
    }
}
