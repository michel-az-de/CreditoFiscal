using System;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;
using Microsoft.Extensions.Logging;

namespace CreditoFiscal.Aplicacao.Auditoria;

// best-effort com timeout: o GET nao espera mais que TimeoutPadrao pelo publish; falha vira warning
public sealed class PublicadorAuditoria : IPublicadorAuditoria
{
    internal static readonly TimeSpan TimeoutPadrao = TimeSpan.FromMilliseconds(200);

    private readonly IMensagemPublisher _publisher;
    private readonly ILogger<PublicadorAuditoria> _logger;
    private readonly TimeSpan _timeout;

    public PublicadorAuditoria(IMensagemPublisher publisher, ILogger<PublicadorAuditoria> logger)
        : this(publisher, logger, TimeoutPadrao)
    {
    }

    internal PublicadorAuditoria(IMensagemPublisher publisher, ILogger<PublicadorAuditoria> logger, TimeSpan timeout)
    {
        _publisher = publisher;
        _logger = logger;
        _timeout = timeout;
    }

    public async Task PublicarConsultaAsync(ConsultaCreditoRealizadaDto evento, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        try
        {
            await _publisher.PublicarAsync(Filas.ConsultaCreditoRealizada, evento, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // estourou o teto local sem que o caller cancelasse: trata como modo de falha do broker
            _logger.LogWarning(
                "Auditoria de {Tipo}/{Chave} cancelada por timeout de {TimeoutMs}ms",
                evento.Tipo, evento.Chave, _timeout.TotalMilliseconds);
        }
        catch (Exception excecao)
        {
            _logger.LogWarning(excecao, "Falha ao publicar auditoria de consulta {Tipo}/{Chave}", evento.Tipo, evento.Chave);
        }
    }
}
