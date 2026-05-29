using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Dtos;
using CreditoFiscal.Aplicacao.Mapeamentos;
using CreditoFiscal.Dominio.Abstracoes;

namespace CreditoFiscal.Aplicacao.CasosDeUso;

public sealed class ConsultarCreditoPorNumero : IConsultarCreditoPorNumero
{
    private readonly ICreditoRepository _repositorio;

    public ConsultarCreditoPorNumero(ICreditoRepository repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<CreditoRespostaDto?> ExecutarAsync(string numeroCredito, CancellationToken ct)
    {
        var credito = await _repositorio.ObterPorNumeroCreditoAsync(numeroCredito, ct);
        if (credito == null)
        {
            return null;
        }

        return CreditoRespostaMapeador.Mapear(credito);
    }
}
