using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CreditoFiscal.Api.HealthChecks;

// peek na fila configurada: se o broker responde, esta saudavel
internal sealed class ServiceBusHealthCheck : IHealthCheck
{
    private readonly ServiceBusClient _client;
    private readonly string _fila;

    public ServiceBusHealthCheck(ServiceBusClient client, IConfiguration configuration)
    {
        _client = client;
        _fila = configuration["Mensageria:Fila"]
            ?? throw new InvalidOperationException("Mensageria:Fila nao configurada.");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var receiver = _client.CreateReceiver(_fila);
            await receiver.PeekMessageAsync(cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception excecao)
        {
            return HealthCheckResult.Unhealthy("ServiceBus indisponivel", excecao);
        }
    }
}
