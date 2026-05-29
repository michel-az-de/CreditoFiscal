using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CreditoFiscal.Api.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CreditoFiscal.Testes.Api;

public sealed class ExcecoesMiddlewareTestes
{
    [Fact]
    public async Task InvokeAsync_QuandoArgumentException_DeveResponder400ComProblemDetails()
    {
        var contexto = CriarContexto();
        var logger = new LoggerFalso<ExcecoesMiddleware>();

        Task Proximo(HttpContext ctx)
        {
            throw new ArgumentException("SimplesNacional invalido");
        }

        var middleware = new ExcecoesMiddleware(Proximo, logger);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be(400);
        contexto.Response.ContentType.Should().Be("application/problem+json");

        var problema = await LerProblemaAsync(contexto);
        problema.Status.Should().Be(400);
        logger.Niveis.Should().Contain(LogLevel.Warning);
    }

    [Fact]
    public async Task InvokeAsync_QuandoExcecaoInesperada_DeveResponder500SemVazarDetalheInterno()
    {
        var contexto = CriarContexto();
        var logger = new LoggerFalso<ExcecoesMiddleware>();

        Task Proximo(HttpContext ctx)
        {
            throw new InvalidOperationException("detalhe interno sensivel");
        }

        var middleware = new ExcecoesMiddleware(Proximo, logger);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be(500);

        var problema = await LerProblemaAsync(contexto);
        problema.Status.Should().Be(500);
        problema.Detail.Should().NotContain("sensivel");
        logger.Niveis.Should().Contain(LogLevel.Error);
    }

    [Fact]
    public async Task InvokeAsync_QuandoBadHttpRequestException_DeveResponderComStatusDaException()
    {
        // 413 do RequestSizeLimit (e demais 4xx do leitor de body do MVC) chega aqui:
        // o input formatter so captura JsonException/FormatException/OverflowException,
        // entao BadHttpRequestException propagaria para o catch generico (500) sem o branch dedicado.
        var contexto = CriarContexto();
        var logger = new LoggerFalso<ExcecoesMiddleware>();

        Task Proximo(HttpContext ctx)
        {
            throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
        }

        var middleware = new ExcecoesMiddleware(Proximo, logger);

        await middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be(413);
        contexto.Response.ContentType.Should().Be("application/problem+json");

        var problema = await LerProblemaAsync(contexto);
        problema.Status.Should().Be(413);
        logger.Niveis.Should().Contain(LogLevel.Warning);
    }

    [Fact]
    public async Task InvokeAsync_QuandoNaoHaExcecao_DeveSeguirOPipelineSemAlterarResposta()
    {
        var contexto = CriarContexto();
        var logger = new LoggerFalso<ExcecoesMiddleware>();
        var proximoFoiChamado = false;

        Task Proximo(HttpContext ctx)
        {
            proximoFoiChamado = true;
            return Task.CompletedTask;
        }

        var middleware = new ExcecoesMiddleware(Proximo, logger);

        await middleware.InvokeAsync(contexto);

        proximoFoiChamado.Should().BeTrue();
        contexto.Response.StatusCode.Should().Be(200);
    }

    private static DefaultHttpContext CriarContexto()
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        return contexto;
    }

    private static async Task<ProblemDetails> LerProblemaAsync(HttpContext contexto)
    {
        contexto.Response.Body.Seek(0, SeekOrigin.Begin);

        using var leitor = new StreamReader(contexto.Response.Body, Encoding.UTF8);
        var corpo = await leitor.ReadToEndAsync();

        var opcoes = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var problema = JsonSerializer.Deserialize<ProblemDetails>(corpo, opcoes);
        return problema!;
    }

    // duble em vez de mock: LogWarning/LogError sao extensions que o NSubstitute nao casa
    private sealed class LoggerFalso<T> : ILogger<T>
    {
        public List<LogLevel> Niveis { get; } = new List<LogLevel>();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return new EscopoVazio();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Niveis.Add(logLevel);
        }

        private sealed class EscopoVazio : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
