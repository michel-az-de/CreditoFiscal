using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Dtos;
using CreditoFiscal.Aplicacao.Mapeamentos;
using CreditoFiscal.Dominio.Abstracoes;

namespace CreditoFiscal.Aplicacao.CasosDeUso;

public sealed class ConsultarCreditosPorNfse : IConsultarCreditosPorNfse
{
    private readonly ICreditoRepository _repositorio;

    public ConsultarCreditosPorNfse(ICreditoRepository repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<IReadOnlyList<CreditoRespostaDto>> ExecutarAsync(string numeroNfse, CancellationToken ct)
    {
        var creditos = await _repositorio.ObterPorNumeroNfseAsync(numeroNfse, ct);

        var resposta = new List<CreditoRespostaDto>();
        foreach (var credito in creditos)
        {
            resposta.Add(CreditoRespostaMapeador.Mapear(credito));
        }

        return resposta;
    }
}
