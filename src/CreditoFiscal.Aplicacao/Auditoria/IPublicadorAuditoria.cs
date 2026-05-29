using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Mensagens;

namespace CreditoFiscal.Aplicacao.Auditoria;

public interface IPublicadorAuditoria
{
    Task PublicarConsultaAsync(ConsultaCreditoRealizadaDto evento, CancellationToken ct);
}
