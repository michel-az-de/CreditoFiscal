using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CreditoFiscal.Api.Middlewares;

// correlation id por requisicao: do header ou gerado, vai no response e no escopo de log
public sealed class CorrelacaoMiddleware
{
    private const string Cabecalho = "X-Correlation-Id";

    private readonly RequestDelegate _proximo;
    private readonly ILogger<CorrelacaoMiddleware> _logger;

    public CorrelacaoMiddleware(RequestDelegate proximo, ILogger<CorrelacaoMiddleware> logger)
    {
        _proximo = proximo;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        var correlacao = ObterOuCriar(contexto);
        contexto.Response.Headers[Cabecalho] = correlacao;

        var escopo = new Dictionary<string, object> { ["CorrelationId"] = correlacao };
        using (_logger.BeginScope(escopo))
        {
            await _proximo(contexto);
        }
    }

    private static string ObterOuCriar(HttpContext contexto)
    {
        var existente = contexto.Request.Headers[Cabecalho].ToString();
        if (!string.IsNullOrWhiteSpace(existente))
        {
            return existente;
        }

        return Guid.NewGuid().ToString();
    }
}
