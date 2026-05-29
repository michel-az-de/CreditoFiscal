using CreditoFiscal.Api.BackgroundServices;
using CreditoFiscal.Api.Middlewares;
using CreditoFiscal.Infraestrutura.Json;
using CreditoFiscal.Infraestrutura.Mensageria;
using CreditoFiscal.Infraestrutura.Persistencia;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(opcoes =>
{
    opcoes.JsonSerializerOptions.Converters.Add(new ConversorDeDataSemFusoHorario());
});

builder.Services.AdicionarPersistencia(builder.Configuration);
builder.Services.AdicionarMensageria(builder.Configuration);
builder.Services.AddHostedService<CreditoConsumer>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!, name: "postgres", tags: new[] { "ready" })
    .AddRabbitMQ(sp => sp.GetRequiredService<IConnection>(), name: "rabbitmq", tags: new[] { "ready" });

var app = builder.Build();

// primeiro do pipeline: captura excecao de tudo que vem abaixo
app.UseMiddleware<ExcecoesMiddleware>();

app.MapControllers();

// /self = liveness (processo de pe); /ready = readiness (Postgres e RabbitMQ alcancaveis)
app.MapHealthChecks("/self", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = registro => registro.Tags.Contains("ready") });

app.Run();
