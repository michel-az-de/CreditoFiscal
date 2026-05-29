using CreditoFiscal.Api.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

// primeiro do pipeline: captura excecao de tudo que vem abaixo
app.UseMiddleware<ExcecoesMiddleware>();

app.MapControllers();

app.Run();
