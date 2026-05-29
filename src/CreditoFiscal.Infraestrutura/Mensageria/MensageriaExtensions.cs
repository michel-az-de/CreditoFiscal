using System;
using System.Globalization;
using System.Threading;
using CreditoFiscal.Dominio.Abstracoes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace CreditoFiscal.Infraestrutura.Mensageria;

public static class MensageriaExtensions
{
    public static IServiceCollection AdicionarMensageria(this IServiceCollection services, IConfiguration configuration)
    {
        var provedor = configuration["Mensageria:Provedor"];
        if (provedor != "RabbitMQ")
        {
            throw new InvalidOperationException($"Provedor de mensageria nao suportado: {provedor}");
        }

        var nomeDaFila = configuration["Mensageria:Fila"]
            ?? throw new InvalidOperationException("Mensageria:Fila nao configurada.");

        // mesma instancia singleton atende as duas interfaces (publisher e consumer)
        services.AddSingleton<IConnection>(CriarConexaoComRetry);
        services.AddSingleton<AdaptadorRabbitMq>();
        services.AddSingleton<IMensagemPublisher>(sp => sp.GetRequiredService<AdaptadorRabbitMq>());
        services.AddSingleton<IMensagemConsumer>(sp => sp.GetRequiredService<AdaptadorRabbitMq>());
        services.AddHostedService(sp => new CriadorDeFilas(sp.GetRequiredService<IConnection>(), nomeDaFila));
        return services;
    }

    private static IConnection CriarConexaoComRetry(IServiceProvider sp)
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("CreditoFiscal.Mensageria");

        var fabrica = new ConnectionFactory
        {
            HostName = configuration["Mensageria:Host"] ?? "localhost",
            Port = int.Parse(configuration["Mensageria:Port"] ?? "5672", CultureInfo.InvariantCulture),
            UserName = configuration["Mensageria:Usuario"] ?? "guest",
            Password = configuration["Mensageria:Senha"] ?? "guest",
            AutomaticRecoveryEnabled = true
        };

        // 6 tentativas com backoff 2/4/6/8/10s: o broker pode subir depois da API no compose
        var atrasos = new[] { 2, 4, 6, 8, 10 };
        for (var tentativa = 1; tentativa <= atrasos.Length + 1; tentativa++)
        {
            try
            {
                return fabrica.CreateConnection();
            }
            catch (Exception excecao)
            {
                if (tentativa > atrasos.Length)
                {
                    throw;
                }

                var atraso = atrasos[tentativa - 1];
                logger.LogWarning(excecao, "RabbitMQ indisponivel (tentativa {Tentativa}); nova tentativa em {Atraso}s", tentativa, atraso);
                Thread.Sleep(TimeSpan.FromSeconds(atraso));
            }
        }

        throw new InvalidOperationException("Nao foi possivel conectar ao RabbitMQ.");
    }
}
