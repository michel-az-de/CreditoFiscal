using System;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Dominio.Entidades;
using CreditoFiscal.Infraestrutura.Persistencia;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CreditoFiscal.Testes.Infraestrutura;

public sealed class CreditoRepositoryTestes
{
    private static CreditoFiscalDbContext CriarContextoEmMemoria()
    {
        var opcoes = new DbContextOptionsBuilder<CreditoFiscalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CreditoFiscalDbContext(opcoes);
    }

    private static Credito MontarCredito(string numeroCredito, string numeroNfse)
    {
        return new Credito
        {
            NumeroCredito = numeroCredito,
            NumeroNfse = numeroNfse,
            DataConstituicao = new DateTime(2024, 5, 28),
            ValorIssqn = 1500m,
            TipoCredito = "ISSQN",
            SimplesNacional = false,
            Aliquota = 5m,
            ValorFaturado = 30000m,
            ValorDeducao = 5000m,
            BaseCalculo = 25000m
        };
    }

    [Fact]
    public async Task AdicionarAsync_QuandoCreditoNovo_DevePersistirNoBanco()
    {
        using var contexto = CriarContextoEmMemoria();
        var repositorio = new CreditoRepository(contexto);
        var credito = MontarCredito("123456", "7891011");

        await repositorio.AdicionarAsync(credito, CancellationToken.None);
        await contexto.SaveChangesAsync();

        var persistido = await contexto.Creditos.SingleAsync();
        persistido.NumeroCredito.Should().Be("123456");
        persistido.NumeroNfse.Should().Be("7891011");
    }

    [Fact]
    public async Task ObterPorNumeroCreditoAsync_QuandoExiste_DeveRetornarCredito()
    {
        using var contexto = CriarContextoEmMemoria();
        contexto.Creditos.Add(MontarCredito("123456", "7891011"));
        await contexto.SaveChangesAsync();
        var repositorio = new CreditoRepository(contexto);

        var resultado = await repositorio.ObterPorNumeroCreditoAsync("123456", CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.NumeroCredito.Should().Be("123456");
    }

    [Fact]
    public async Task ObterPorNumeroCreditoAsync_QuandoNaoExiste_DeveRetornarNull()
    {
        using var contexto = CriarContextoEmMemoria();
        var repositorio = new CreditoRepository(contexto);

        var resultado = await repositorio.ObterPorNumeroCreditoAsync("naoexiste", CancellationToken.None);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ObterPorNumeroNfseAsync_QuandoExistemMultiplos_DeveRetornarTodos()
    {
        using var contexto = CriarContextoEmMemoria();
        contexto.Creditos.Add(MontarCredito("credito-1", "nfse-comum"));
        contexto.Creditos.Add(MontarCredito("credito-2", "nfse-comum"));
        contexto.Creditos.Add(MontarCredito("credito-3", "outra-nfse"));
        await contexto.SaveChangesAsync();
        var repositorio = new CreditoRepository(contexto);

        var resultado = await repositorio.ObterPorNumeroNfseAsync("nfse-comum", CancellationToken.None);

        resultado.Should().HaveCount(2);
        resultado.Should().OnlyContain(c => c.NumeroNfse == "nfse-comum");
    }

    [Fact]
    public async Task ObterPorNumeroNfseAsync_QuandoVazio_DeveRetornarListaVazia()
    {
        using var contexto = CriarContextoEmMemoria();
        var repositorio = new CreditoRepository(contexto);

        var resultado = await repositorio.ObterPorNumeroNfseAsync("naoexiste", CancellationToken.None);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task ExisteAsync_QuandoCreditoPresente_DeveRetornarTrue()
    {
        using var contexto = CriarContextoEmMemoria();
        contexto.Creditos.Add(MontarCredito("123456", "7891011"));
        await contexto.SaveChangesAsync();
        var repositorio = new CreditoRepository(contexto);

        var existe = await repositorio.ExisteAsync("123456", CancellationToken.None);

        existe.Should().BeTrue();
    }

    [Fact]
    public async Task ExisteAsync_QuandoCreditoAusente_DeveRetornarFalse()
    {
        using var contexto = CriarContextoEmMemoria();
        var repositorio = new CreditoRepository(contexto);

        var existe = await repositorio.ExisteAsync("naoexiste", CancellationToken.None);

        existe.Should().BeFalse();
    }
}
