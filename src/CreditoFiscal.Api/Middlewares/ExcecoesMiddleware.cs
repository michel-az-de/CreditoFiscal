using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CreditoFiscal.Api.Middlewares;

// traducao central de excecao -> HTTP; os controllers nao repetem try/catch
public sealed class ExcecoesMiddleware
{
    private static readonly JsonSerializerOptions OpcoesJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _proximo;
    private readonly ILogger<ExcecoesMiddleware> _logger;

    public ExcecoesMiddleware(RequestDelegate proximo, ILogger<ExcecoesMiddleware> logger)
    {
        _proximo = proximo;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _proximo(contexto);
        }
        catch (ArgumentException excecao)
        {
            // erro do cliente (ex.: SimplesNacional invalido), nao do servidor
            _logger.LogWarning(excecao, "Requisicao invalida: {Mensagem}", excecao.Message);
            await EscreverProblemaAsync(contexto, StatusCodes.Status400BadRequest, "Requisicao invalida", excecao.Message);
        }
        catch (BadHttpRequestException excecao)
        {
            // 413 do RequestSizeLimit e demais 4xx do leitor de body do pipeline MVC;
            // o input formatter nao captura isso, entao chega aqui via catch dedicado.
            _logger.LogWarning(excecao, "Requisicao HTTP rejeitada ({StatusCode}): {Mensagem}", excecao.StatusCode, excecao.Message);
            await EscreverProblemaAsync(contexto, excecao.StatusCode, "Requisicao rejeitada", excecao.Message);
        }
        catch (Exception excecao)
        {
            // loga tudo no servidor, mas nao devolve stack/detalhe interno pro cliente
            _logger.LogError(excecao, "Erro nao tratado ao processar a requisicao");
            await EscreverProblemaAsync(contexto, StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado ao processar a requisicao.");
        }
    }

    private async Task EscreverProblemaAsync(HttpContext contexto, int status, string titulo, string detalhe)
    {
        // resposta ja comecou: nao da pra trocar status nem corpo
        if (contexto.Response.HasStarted)
        {
            _logger.LogWarning("Resposta ja iniciada; ProblemDetails nao pode ser escrito.");
            return;
        }

        var problema = new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Detail = detalhe
        };

        contexto.Response.Clear();
        contexto.Response.StatusCode = status;
        contexto.Response.ContentType = "application/problem+json";

        var corpo = JsonSerializer.Serialize(problema, OpcoesJson);
        await contexto.Response.WriteAsync(corpo);
    }
}
