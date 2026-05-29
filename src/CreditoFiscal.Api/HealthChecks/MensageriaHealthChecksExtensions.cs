using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace CreditoFiscal.Api.HealthChecks;

public static class MensageriaHealthChecksExtensions
{
    // escolhe o health check certo conforme o provedor de mensageria configurado:
    // sem isso o /ready quebraria ao trocar de broker (o IConnection do Rabbit nem existe quando o provedor e Kafka/SB)
    public static IHealthChecksBuilder AdicionarHealthCheckMensageria(
        this IHealthChecksBuilder builder,
        IConfiguration configuration)
    {
        var provedor = configuration["Mensageria:Provedor"];
        switch (provedor)
        {
            case "RabbitMQ":
                builder.AddRabbitMQ(sp => sp.GetRequiredService<IConnection>(), name: "rabbitmq", tags: new[] { "ready" });
                break;
            case "Kafka":
                builder.Services.AddSingleton<KafkaHealthCheck>();
                builder.AddCheck<KafkaHealthCheck>("kafka", tags: new[] { "ready" });
                break;
            case "ServiceBus":
                builder.Services.AddSingleton<ServiceBusHealthCheck>();
                builder.AddCheck<ServiceBusHealthCheck>("servicebus", tags: new[] { "ready" });
                break;
            default:
                throw new InvalidOperationException($"Provedor de mensageria nao suportado: {provedor}");
        }

        return builder;
    }
}
