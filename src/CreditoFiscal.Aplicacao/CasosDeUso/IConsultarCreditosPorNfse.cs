using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Dtos;

namespace CreditoFiscal.Aplicacao.CasosDeUso;

public interface IConsultarCreditosPorNfse
{
    Task<IReadOnlyList<CreditoRespostaDto>> ExecutarAsync(string numeroNfse, CancellationToken ct);
}
