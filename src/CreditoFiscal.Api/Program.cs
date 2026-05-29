using CreditoFiscal.Api.BackgroundServices;
using CreditoFiscal.Api.Middlewares;
using CreditoFiscal.Api.Observabilidade;
using CreditoFiscal.Aplicacao.CasosDeUso;
using CreditoFiscal.Infraestrutura.Json;
using CreditoFiscal.Infraestrutura.Mensageria;
using CreditoFiscal.Infraestrutura.Persistencia;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(opcoes =>
{
    opcoes.JsonSerializerOptions.Converters.Add(new ConversorDeDataSemFusoHorario());
});

// Swagger sempre on (sem guard de ambiente), como o enunciado pede
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opcoes =>
{
    opcoes.SwaggerDoc("v1", new OpenApiInfo { Title = "CreditoFiscal", Version = "v1" });
});

// observabilidade: traces (HTTP + span do consumer) e metricas, exportados pro console
builder.Services.AddOpenTelemetry()
    .ConfigureResource(recurso => recurso.AddService("CreditoFiscal"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddSource(Telemetria.Nome);
        tracing.AddConsoleExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddRuntimeInstrumentation();
        metrics.AddConsoleExporter();
    });

builder.Services.AdicionarPersistencia(builder.Configuration);
builder.Services.AdicionarMensageria(builder.Configuration);
builder.Services.AddHostedService<CreditoConsumer>();
builder.Services.AdicionarCasosDeUso();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!, name: "postgres", tags: new[] { "ready" })
    .AddRabbitMQ(sp => sp.GetRequiredService<IConnection>(), name: "rabbitmq", tags: new[] { "ready" });

var app = builder.Build();

// aplica migrations pendentes no startup: schema pronto antes de servir
using (var escopo = app.Services.CreateScope())
{
    var contexto = escopo.ServiceProvider.GetRequiredService<CreditoFiscalDbContext>();
    await contexto.Database.MigrateAsync();
}

// correlation id primeiro: o escopo de log vale pra tudo abaixo, inclusive o middleware de erro
app.UseMiddleware<CorrelacaoMiddleware>();

// captura excecao de tudo que vem abaixo
app.UseMiddleware<ExcecoesMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// /self = liveness (processo de pe); /ready = readiness (Postgres e RabbitMQ alcancaveis)
app.MapHealthChecks("/self", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = registro => registro.Tags.Contains("ready") });

app.Run();

// expoe Program pro WebApplicationFactory dos testes de integracao
public partial class Program { }
