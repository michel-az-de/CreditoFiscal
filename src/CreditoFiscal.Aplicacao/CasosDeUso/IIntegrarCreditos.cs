using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Dtos;

namespace CreditoFiscal.Aplicacao.CasosDeUso;

public interface IIntegrarCreditos
{
    Task ExecutarAsync(IReadOnlyList<IntegrarCreditoRequisicaoDto> creditos, CancellationToken ct);
}
