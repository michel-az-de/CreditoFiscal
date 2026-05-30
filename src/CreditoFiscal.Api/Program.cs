using System;
using CreditoFiscal.Api.BackgroundServices;
using CreditoFiscal.Api.HealthChecks;
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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(opcoes =>
{
    opcoes.JsonSerializerOptions.Converters.Add(new ConversorDeDataSemFusoHorario());
});

// Swagger ligado fora de Development (override do guard padrao do template) pra facilitar a inspecao da API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opcoes =>
{
    opcoes.SwaggerDoc("v1", new OpenApiInfo { Title = "CreditoFiscal", Version = "v1" });
});

// trace HTTP + span do consumer; metricas runtime; export console
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

var conexaoPostgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres nao configurada.");

builder.Services.AddHealthChecks()
    .AddNpgSql(conexaoPostgres, name: "postgres", tags: new[] { "ready" })
    .AdicionarHealthCheckMensageria(builder.Configuration);

var app = builder.Build();

// aplica migrations pendentes no startup: schema pronto antes de servir
using (var escopo = app.Services.CreateScope())
{
    var contexto = escopo.ServiceProvider.GetRequiredService<CreditoFiscalDbContext>();
    await contexto.Database.MigrateAsync();
}

// correlation id primeiro: o escopo de log vale pra tudo abaixo, inclusive o middleware de erro
app.UseMiddleware<CorrelacaoMiddleware>();
app.UseMiddleware<ExcecoesMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// /self = liveness (processo de pe); /ready = readiness (Postgres e o broker configurado alcancaveis)
app.MapHealthChecks("/self", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = registro => registro.Tags.Contains("ready") });

app.Run();

// expoe Program pro WebApplicationFactory dos testes de integracao
public partial class Program { }
