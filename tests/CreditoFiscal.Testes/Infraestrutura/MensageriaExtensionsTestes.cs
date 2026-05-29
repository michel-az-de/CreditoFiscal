using System;
using System.Collections.Generic;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Infraestrutura.Mensageria;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CreditoFiscal.Testes.Infraestrutura;

public sealed class MensageriaExtensionsTestes
{
    // A factory escolhe o adapter por config (OCP): trocar de broker = trocar uma string,
    // sem mexer em controller nem consumer. RabbitMQ nao entra aqui porque resolve-lo
    // abriria conexao (com retry) no teste; Kafka e ServiceBus criam clientes preguicosos.
    [Theory]
    [InlineData("Kafka", typeof(AdaptadorKafka))]
    [InlineData("ServiceBus", typeof(AdaptadorServiceBus))]
    public void AdicionarMensageria_SelecionaOAdapterDoProvedor(string provedor, Type esperado)
    {
        var configuration = MontarConfig(provedor);
        var services = new ServiceCollection();
        services.AddLogging();

        services.AdicionarMensageria(configuration);

        using var di = services.BuildServiceProvider();
        var publisher = di.GetRequiredService<IMensagemPublisher>();
        var consumer = di.GetRequiredService<IMensagemConsumer>();

        publisher.Should().BeOfType(esperado);
        consumer.Should().BeSameAs(publisher);
    }

    [Fact]
    public void AdicionarMensageria_QuandoProvedorNaoSuportado_DeveLancar()
    {
        var configuration = MontarConfig("Pombo-Correio");
        var services = new ServiceCollection();

        Func<IServiceCollection> acao = delegate
        {
            return services.AdicionarMensageria(configuration);
        };

        acao.Should().Throw<InvalidOperationException>();
    }

    private static IConfiguration MontarConfig(string provedor)
    {
        var dados = new Dictionary<string, string?>
        {
            ["Mensageria:Provedor"] = provedor,
            ["Mensageria:Fila"] = "fila-teste",
            ["Mensageria:Kafka:BootstrapServers"] = "localhost:9092",
            ["Mensageria:ServiceBus:ConnectionString"] =
                "Endpoint=sb://teste.servicebus.windows.net/;SharedAccessKeyName=chave;SharedAccessKey=dGVzdGtleXZhbG9y",
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dados).Build();
    }
}
