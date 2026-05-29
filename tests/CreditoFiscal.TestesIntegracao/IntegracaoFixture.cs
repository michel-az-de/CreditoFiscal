using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CreditoFiscal.TestesIntegracao;

// sobe Postgres + RabbitMQ reais via Testcontainers e a API em processo apontada pra eles
public sealed class IntegracaoFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private readonly RabbitMqContainer _rabbitmq;
    private WebApplicationFactory<Program>? _fabrica;

    public IntegracaoFixture()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("creditofiscal")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _rabbitmq = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .Build();
    }

    public HttpClient Cliente { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _rabbitmq.StartAsync();

        // le credenciais/porta reais do container (a porta e mapeada para uma aleatoria do host)
        var rabbit = new Uri(_rabbitmq.GetConnectionString());
        var usuarioSenha = rabbit.UserInfo.Split(':');

        // loopback IPv4 explicito: evita resolver "localhost" (IPv6/getaddrinfo) que falha em alguns hosts
        var postgres = _postgres.GetConnectionString().Replace("localhost", "127.0.0.1");
        var rabbitHost = rabbit.Host;
        if (rabbitHost == "localhost")
        {
            rabbitHost = "127.0.0.1";
        }

        var ajustes = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = postgres,
            ["Mensageria:Provedor"] = "RabbitMQ",
            ["Mensageria:Fila"] = "integrar-credito-constituido-entry",
            ["Mensageria:RabbitMQ:Host"] = rabbitHost,
            ["Mensageria:RabbitMQ:Port"] = rabbit.Port.ToString(CultureInfo.InvariantCulture),
            ["Mensageria:RabbitMQ:Usuario"] = usuarioSenha[0],
            ["Mensageria:RabbitMQ:Senha"] = usuarioSenha.Length > 1 ? usuarioSenha[1] : string.Empty
        };

        _fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(delegate (IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(delegate (WebHostBuilderContext contexto, IConfigurationBuilder config)
            {
                config.AddInMemoryCollection(ajustes);
            });
        });

        // CreateClient dispara o build do host: roda as migrations e sobe o consumer
        Cliente = _fabrica.CreateClient();
    }

    public async Task DisposeAsync()
    {
        if (_fabrica != null)
        {
            await _fabrica.DisposeAsync();
        }

        await _rabbitmq.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
