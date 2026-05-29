using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Dtos;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CreditoFiscal.TestesIntegracao;

// exercita o fluxo real: POST -> RabbitMQ -> consumer -> Postgres -> GET
public sealed class IntegracaoEndToEndTestes : IClassFixture<IntegracaoFixture>
{
    private const string Endpoint = "/api/creditos/integrar-credito-constituido";
    private readonly IntegracaoFixture _fixture;

    public IntegracaoEndToEndTestes(IntegracaoFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Post_publica_consumidor_persiste_e_consulta_por_nfse()
    {
        var lote = new[]
        {
            NovoCredito("INT-1001", "INT-NFSE-1"),
            NovoCredito("INT-1002", "INT-NFSE-1")
        };

        var resposta = await _fixture.Cliente.PostAsJsonAsync(Endpoint, lote);
        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var creditos = await AguardarPorNfseAsync("INT-NFSE-1", 2);
        creditos.Should().NotBeNull();
        creditos!.Should().HaveCount(2);
        creditos.Should().Contain(c => c.NumeroCredito == "INT-1001");
    }

    [Fact]
    public async Task Get_por_numero_retorna_credito_persistido()
    {
        var lote = new[] { NovoCredito("INT-2001", "INT-NFSE-2") };
        await _fixture.Cliente.PostAsJsonAsync(Endpoint, lote);

        var credito = await AguardarPorNumeroAsync("INT-2001");
        credito.Should().NotBeNull();
        credito!.NumeroNfse.Should().Be("INT-NFSE-2");
        credito.SimplesNacional.Should().Be("Sim");
    }

    [Fact]
    public async Task Reenvio_do_mesmo_credito_nao_duplica()
    {
        var lote = new[] { NovoCredito("INT-3001", "INT-NFSE-3") };

        await _fixture.Cliente.PostAsJsonAsync(Endpoint, lote);
        await AguardarPorNumeroAsync("INT-3001");

        // segundo envio do mesmo numero: a idempotencia deve ignorar a duplicata
        await _fixture.Cliente.PostAsJsonAsync(Endpoint, lote);

        // poll por ~5s garantindo que o count nunca vira 2: se a idempotencia falhar, pega na primeira tentativa
        for (var tentativa = 0; tentativa < 10; tentativa++)
        {
            await Task.Delay(500);
            var resposta = await _fixture.Cliente.GetAsync("/api/creditos/INT-NFSE-3");
            if (resposta.StatusCode == HttpStatusCode.OK)
            {
                var lista = await resposta.Content.ReadFromJsonAsync<List<CreditoRespostaDto>>();
                lista.Should().NotBeNull();
                lista!.Should().HaveCount(1, $"idempotencia tem que manter 1 (tentativa {tentativa + 1})");
            }
        }
    }

    [Fact]
    public async Task Get_por_numero_retorna_credito_com_simples_nacional_nao()
    {
        var lote = new[] { NovoCredito("INT-5001", "INT-NFSE-5", "Não") };
        await _fixture.Cliente.PostAsJsonAsync(Endpoint, lote);

        var credito = await AguardarPorNumeroAsync("INT-5001");
        credito.Should().NotBeNull();
        credito!.SimplesNacional.Should().Be("Não");
    }

    [Fact]
    public async Task Simples_nacional_invalido_retorna_400()
    {
        var lote = new[] { NovoCredito("INT-4001", "INT-NFSE-4", "Talvez") };

        var resposta = await _fixture.Cliente.PostAsJsonAsync(Endpoint, lote);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Numero_credito_vazio_retorna_400_pela_validacao_de_modelo()
    {
        // ModelState do [ApiController] devolve 400 antes do caso de uso rodar -> nada chega na fila
        var lote = new[]
        {
            new
            {
                numeroCredito = "",
                numeroNfse = "INT-NFSE-5",
                dataConstituicao = "2026-02-25",
                valorIssqn = 1500.75m,
                tipoCredito = "ISSQN",
                simplesNacional = "Sim",
                aliquota = 5.0m,
                valorFaturado = 30000.0m,
                valorDeducao = 5000.0m,
                baseCalculo = 25000.0m
            }
        };

        var resposta = await _fixture.Cliente.PostAsJsonAsync(Endpoint, lote);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DataConstituicao_null_retorna_400_com_errors_no_campo()
    {
        // Anonimo com a propriedade explicita como null exercita o caminho do STJ
        // devolvendo null no wrapper Nullable<DateTime>, sem chamar o converter de data.
        var lote = new[]
        {
            new
            {
                numeroCredito = "INT-DT-NULL",
                numeroNfse = "INT-NFSE-DT-1",
                dataConstituicao = (string?)null,
                valorIssqn = 1500.75m,
                tipoCredito = "ISSQN",
                simplesNacional = "Sim",
                aliquota = 5.0m,
                valorFaturado = 30000.0m,
                valorDeducao = 5000.0m,
                baseCalculo = 25000.0m
            }
        };

        var resposta = await _fixture.Cliente.PostAsJsonAsync(Endpoint, lote);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problema = await resposta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problema.Should().NotBeNull();
        // ModelState do [Required] usa o nome da propriedade do DTO (PascalCase)
        problema!.Errors.Keys.Should().Contain(k => k.EndsWith("DataConstituicao", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task DataConstituicao_ausente_retorna_400_com_errors_no_campo()
    {
        // Campo omitido do JSON: STJ devolve null no Nullable<DateTime> e o [Required] dispara.
        var lote = new[]
        {
            new
            {
                numeroCredito = "INT-DT-AUSENTE",
                numeroNfse = "INT-NFSE-DT-2",
                valorIssqn = 1500.75m,
                tipoCredito = "ISSQN",
                simplesNacional = "Sim",
                aliquota = 5.0m,
                valorFaturado = 30000.0m,
                valorDeducao = 5000.0m,
                baseCalculo = 25000.0m
            }
        };

        var resposta = await _fixture.Cliente.PostAsJsonAsync(Endpoint, lote);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problema = await resposta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problema.Should().NotBeNull();
        problema!.Errors.Keys.Should().Contain(k => k.EndsWith("DataConstituicao", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task Aliquota_fora_do_intervalo_retorna_400()
    {
        var lote = new[]
        {
            new
            {
                numeroCredito = "INT-VAL-6",
                numeroNfse = "INT-NFSE-6",
                dataConstituicao = "2026-02-25",
                valorIssqn = 1500.75m,
                tipoCredito = "ISSQN",
                simplesNacional = "Sim",
                aliquota = 999.0m,
                valorFaturado = 30000.0m,
                valorDeducao = 5000.0m,
                baseCalculo = 25000.0m
            }
        };

        var resposta = await _fixture.Cliente.PostAsJsonAsync(Endpoint, lote);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Readiness_fica_healthy_com_dependencias_de_pe()
    {
        var resposta = await _fixture.Cliente.GetAsync("/ready");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<List<CreditoRespostaDto>?> AguardarPorNfseAsync(string nfse, int minimo)
    {
        for (var tentativa = 0; tentativa < 40; tentativa++)
        {
            var resposta = await _fixture.Cliente.GetAsync($"/api/creditos/{nfse}");
            if (resposta.StatusCode == HttpStatusCode.OK)
            {
                var lista = await resposta.Content.ReadFromJsonAsync<List<CreditoRespostaDto>>();
                if (lista != null && lista.Count >= minimo)
                {
                    return lista;
                }
            }

            await Task.Delay(500);
        }

        return null;
    }

    private async Task<CreditoRespostaDto?> AguardarPorNumeroAsync(string numero)
    {
        for (var tentativa = 0; tentativa < 40; tentativa++)
        {
            var resposta = await _fixture.Cliente.GetAsync($"/api/creditos/credito/{numero}");
            if (resposta.StatusCode == HttpStatusCode.OK)
            {
                return await resposta.Content.ReadFromJsonAsync<CreditoRespostaDto>();
            }

            await Task.Delay(500);
        }

        return null;
    }

    private static object NovoCredito(string numeroCredito, string numeroNfse)
    {
        return NovoCredito(numeroCredito, numeroNfse, "Sim");
    }

    private static object NovoCredito(string numeroCredito, string numeroNfse, string simplesNacional)
    {
        return new
        {
            numeroCredito = numeroCredito,
            numeroNfse = numeroNfse,
            dataConstituicao = "2026-02-25",
            valorIssqn = 1500.75m,
            tipoCredito = "ISSQN",
            simplesNacional = simplesNacional,
            aliquota = 5.0m,
            valorFaturado = 30000.0m,
            valorDeducao = 5000.0m,
            baseCalculo = 25000.0m
        };
    }
}
