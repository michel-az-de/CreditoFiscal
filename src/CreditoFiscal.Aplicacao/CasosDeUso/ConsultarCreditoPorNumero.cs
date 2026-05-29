using System;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Auditoria;
using CreditoFiscal.Aplicacao.Dtos;
using CreditoFiscal.Aplicacao.Mapeamentos;
using CreditoFiscal.Aplicacao.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;

namespace CreditoFiscal.Aplicacao.CasosDeUso;

public sealed class ConsultarCreditoPorNumero : IConsultarCreditoPorNumero
{
    private const string TipoConsulta = "PorNumero";

    private readonly ICreditoRepository _repositorio;
    private readonly IPublicadorAuditoria _auditoria;

    public ConsultarCreditoPorNumero(ICreditoRepository repositorio, IPublicadorAuditoria auditoria)
    {
        _repositorio = repositorio;
        _auditoria = auditoria;
    }

    public async Task<CreditoRespostaDto?> ExecutarAsync(string numeroCredito, CancellationToken ct)
    {
        var credito = await _repositorio.ObterPorNumeroCreditoAsync(numeroCredito, ct);

        await _auditoria.PublicarConsultaAsync(new ConsultaCreditoRealizadaDto
        {
            Tipo = TipoConsulta,
            Chave = numeroCredito,
            QuantidadeRetornada = credito == null ? 0 : 1,
            OcorridoEm = DateTime.UtcNow
        }, ct);

        if (credito == null)
        {
            return null;
        }

        return CreditoRespostaMapeador.Mapear(credito);
    }
}
