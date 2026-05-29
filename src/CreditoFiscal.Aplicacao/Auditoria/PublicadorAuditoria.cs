using System;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;
using Microsoft.Extensions.Logging;

namespace CreditoFiscal.Aplicacao.Auditoria;

// efeito colateral: nunca deve derrubar a consulta. Se o broker estiver fora,
// loga warning e segue - o GET continua respondendo 200.
public sealed class PublicadorAuditoria : IPublicadorAuditoria
{
    private readonly IMensagemPublisher _publisher;
    private readonly ILogger<PublicadorAuditoria> _logger;

    public PublicadorAuditoria(IMensagemPublisher publisher, ILogger<PublicadorAuditoria> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublicarConsultaAsync(ConsultaCreditoRealizadaDto evento, CancellationToken ct)
    {
        try
        {
            await _publisher.PublicarAsync(Filas.ConsultaCreditoRealizada, evento, ct);
        }
        catch (Exception excecao)
        {
            _logger.LogWarning(excecao, "Falha ao publicar auditoria de consulta {Tipo}/{Chave}", evento.Tipo, evento.Chave);
        }
    }
}
