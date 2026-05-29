using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CreditoFiscal.Api.HealthChecks;

// reaproveita o handle do producer pra pedir metadata: round-trip leve no broker com timeout curto
internal sealed class KafkaHealthCheck : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);
    private readonly IProducer<Null, byte[]> _producer;

    public KafkaHealthCheck(IProducer<Null, byte[]> producer)
    {
        _producer = producer;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var admin = new DependentAdminClientBuilder(_producer.Handle).Build();
            admin.GetMetadata(Timeout);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception excecao)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka indisponivel", excecao));
        }
    }
}
