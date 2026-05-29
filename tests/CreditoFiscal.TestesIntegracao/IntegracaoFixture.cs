using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CreditoFiscal.TestesIntegracao;

// sobe Postgres + RabbitMQ reais via Testcontainers e a API em processo apontada pra eles
public sealed class IntegracaoFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private readonly RabbitMqContainer _rabbitmq;
    private readonly List<string> _variaveis = new List<string>();
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

        var rabbit = new Uri(_rabbitmq.GetConnectionString());
        var usuarioSenha = rabbit.UserInfo.Split(':');
        var portaPostgres = _postgres.GetMappedPublicPort(5432);
        var portaRabbit = _rabbitmq.GetMappedPublicPort(5672);

        // variaveis de ambiente sobrescrevem o appsettings (que aponta pros nomes do compose, postgres/rabbitmq).
        // conecta por 127.0.0.1 + porta publicada: fora do compose esses nomes nao resolvem
        Definir("ConnectionStrings__Postgres", $"Host=127.0.0.1;Port={portaPostgres};Database=creditofiscal;Username=postgres;Password=postgres");
        Definir("Mensageria__Provedor", "RabbitMQ");
        Definir("Mensageria__Fila", "integrar-credito-constituido-entry");
        Definir("Mensageria__RabbitMQ__Host", "127.0.0.1");
        Definir("Mensageria__RabbitMQ__Port", portaRabbit.ToString(CultureInfo.InvariantCulture));
        Definir("Mensageria__RabbitMQ__Usuario", usuarioSenha[0]);
        Definir("Mensageria__RabbitMQ__Senha", usuarioSenha.Length > 1 ? usuarioSenha[1] : string.Empty);

        _fabrica = new WebApplicationFactory<Program>();

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

        foreach (var nome in _variaveis)
        {
            Environment.SetEnvironmentVariable(nome, null);
        }
    }

    private void Definir(string nome, string valor)
    {
        _variaveis.Add(nome);
        Environment.SetEnvironmentVariable(nome, valor);
    }
}
