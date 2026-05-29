using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Auditoria;
using CreditoFiscal.Aplicacao.Dtos;
using CreditoFiscal.Aplicacao.Mapeamentos;
using CreditoFiscal.Aplicacao.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;

namespace CreditoFiscal.Aplicacao.CasosDeUso;

public sealed class ConsultarCreditosPorNfse : IConsultarCreditosPorNfse
{
    private const string TipoConsulta = "PorNfse";

    private readonly ICreditoRepository _repositorio;
    private readonly IPublicadorAuditoria _auditoria;

    public ConsultarCreditosPorNfse(ICreditoRepository repositorio, IPublicadorAuditoria auditoria)
    {
        _repositorio = repositorio;
        _auditoria = auditoria;
    }

    public async Task<IReadOnlyList<CreditoRespostaDto>> ExecutarAsync(string numeroNfse, CancellationToken ct)
    {
        var creditos = await _repositorio.ObterPorNumeroNfseAsync(numeroNfse, ct);

        var resposta = new List<CreditoRespostaDto>();
        foreach (var credito in creditos)
        {
            resposta.Add(CreditoRespostaMapeador.Mapear(credito));
        }

        await _auditoria.PublicarConsultaAsync(new ConsultaCreditoRealizadaDto
        {
            Tipo = TipoConsulta,
            Chave = numeroNfse,
            QuantidadeRetornada = resposta.Count,
            OcorridoEm = DateTime.UtcNow
        }, ct);

        return resposta;
    }
}
