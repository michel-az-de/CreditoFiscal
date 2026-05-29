using System;
using System.Threading.Tasks;
using CreditoFiscal.Api.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CreditoFiscal.Testes.Api;

public sealed class CorrelacaoMiddlewareTestes
{
    private const string Cabecalho = "X-Correlation-Id";

    [Fact]
    public async Task InvokeAsync_QuandoNaoVemCorrelationId_DeveGerarUmNoResponse()
    {
        var contexto = new DefaultHttpContext();

        Task Proximo(HttpContext ctx)
        {
            return Task.CompletedTask;
        }

        var middleware = new CorrelacaoMiddleware(Proximo, NullLogger<CorrelacaoMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);

        var gerado = contexto.Response.Headers[Cabecalho].ToString();
        gerado.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(gerado, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_QuandoVemCorrelationId_DeveReaproveitarNoResponse()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Headers[Cabecalho] = "rastreio-123";

        Task Proximo(HttpContext ctx)
        {
            return Task.CompletedTask;
        }

        var middleware = new CorrelacaoMiddleware(Proximo, NullLogger<CorrelacaoMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);

        contexto.Response.Headers[Cabecalho].ToString().Should().Be("rastreio-123");
    }
}
