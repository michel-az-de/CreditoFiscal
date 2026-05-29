using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Dtos;

namespace CreditoFiscal.Aplicacao.CasosDeUso;

public interface IConsultarCreditoPorNumero
{
    Task<CreditoRespostaDto?> ExecutarAsync(string numeroCredito, CancellationToken ct);
}
