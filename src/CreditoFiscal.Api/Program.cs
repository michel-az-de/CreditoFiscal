using CreditoFiscal.Api.Middlewares;
using CreditoFiscal.Infraestrutura.Json;
using CreditoFiscal.Infraestrutura.Mensageria;
using CreditoFiscal.Infraestrutura.Persistencia;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(opcoes =>
{
    opcoes.JsonSerializerOptions.Converters.Add(new ConversorDeDataSemFusoHorario());
});

builder.Services.AdicionarPersistencia(builder.Configuration);
builder.Services.AdicionarMensageria(builder.Configuration);

var app = builder.Build();

// primeiro do pipeline: captura excecao de tudo que vem abaixo
app.UseMiddleware<ExcecoesMiddleware>();

app.MapControllers();

app.Run();
