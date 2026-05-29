using System;
using System.Globalization;
using System.Threading;
using Azure.Messaging.ServiceBus;
using Confluent.Kafka;
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
        // factory: o provedor concreto e escolhido por config, sem o resto do app saber qual e
        var provedor = configuration["Mensageria:Provedor"];
        switch (provedor)
        {
            case "RabbitMQ":
                RegistrarRabbitMq(services, configuration);
                break;
            case "Kafka":
                RegistrarKafka(services, configuration);
                break;
            case "ServiceBus":
                RegistrarServiceBus(services, configuration);
                break;
            default:
                throw new InvalidOperationException($"Provedor de mensageria nao suportado: {provedor}");
        }

        return services;
    }

    private static void RegistrarRabbitMq(IServiceCollection services, IConfiguration configuration)
    {
        var nomeDaFila = configuration["Mensageria:Fila"]
            ?? throw new InvalidOperationException("Mensageria:Fila nao configurada.");

        services.AddSingleton<IConnection>(CriarConexaoComRetry);
        RegistrarComoPublisherEConsumer<AdaptadorRabbitMq>(services);
        services.AddHostedService(sp => new CriadorDeFilas(sp.GetRequiredService<IConnection>(), nomeDaFila));
    }

    private static void RegistrarServiceBus(IServiceCollection services, IConfiguration configuration)
    {
        var conexao = configuration["Mensageria:ServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("Mensageria:ServiceBus:ConnectionString nao configurada.");

        services.AddSingleton(new ServiceBusClient(conexao));
        RegistrarComoPublisherEConsumer<AdaptadorServiceBus>(services);
    }

    private static void RegistrarKafka(IServiceCollection services, IConfiguration configuration)
    {
        var bootstrap = configuration["Mensageria:Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Mensageria:Kafka:BootstrapServers nao configurada.");
        var grupo = configuration["Mensageria:Kafka:GrupoConsumidor"] ?? "credito-fiscal";

        services.AddSingleton<IProducer<Null, byte[]>>(_ =>
            new ProducerBuilder<Null, byte[]>(new ProducerConfig { BootstrapServers = bootstrap }).Build());
        services.AddSingleton<IConsumer<Null, byte[]>>(_ =>
            new ConsumerBuilder<Null, byte[]>(new ConsumerConfig
            {
                BootstrapServers = bootstrap,
                GroupId = grupo,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            }).Build());
        RegistrarComoPublisherEConsumer<AdaptadorKafka>(services);
    }

    // registra o adapter como singleton unico atendendo as duas interfaces (ISP)
    private static void RegistrarComoPublisherEConsumer<TAdapter>(IServiceCollection services)
        where TAdapter : class, IMensagemPublisher, IMensagemConsumer
    {
        services.AddSingleton<TAdapter>();
        services.AddSingleton<IMensagemPublisher>(sp => sp.GetRequiredService<TAdapter>());
        services.AddSingleton<IMensagemConsumer>(sp => sp.GetRequiredService<TAdapter>());
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
