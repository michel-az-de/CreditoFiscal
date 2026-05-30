using System;
using Bogus;
using CreditoFiscal.Aplicacao.Dtos;
using CreditoFiscal.Aplicacao.Mensagens;
using CreditoFiscal.Dominio.Entidades;

namespace CreditoFiscal.Testes.Suporte;

internal static class CreditoMother
{
    public static CreditoConstituidoDto Constituido(string numeroCredito = "123456", int semente = 42)
    {
        // CustomInstantiator: caminho do Bogus para record com init-only
        var faker = new Faker<CreditoConstituidoDto>("pt_BR")
            .UseSeed(semente)
            .CustomInstantiator(f => new CreditoConstituidoDto
            {
                NumeroCredito = numeroCredito,
                NumeroNfse = f.Random.Number(100_000, 999_999).ToString(System.Globalization.CultureInfo.InvariantCulture),
                DataConstituicao = f.Date.Past(2),
                ValorIssqn = Math.Round(f.Random.Decimal(100m, 10_000m), 2),
                TipoCredito = f.PickRandom("ISSQN", "Outros"),
                SimplesNacional = f.PickRandom<SimplesNacional>(),
                Aliquota = Math.Round(f.Random.Decimal(2m, 5m), 2),
                ValorFaturado = Math.Round(f.Random.Decimal(10_000m, 100_000m), 2),
                ValorDeducao = Math.Round(f.Random.Decimal(0m, 5_000m), 2),
                BaseCalculo = Math.Round(f.Random.Decimal(5_000m, 95_000m), 2)
            });

        return faker.Generate();
    }

    public static IntegrarCreditoRequisicaoDto Requisicao(string numeroCredito = "1", string simplesNacional = "Sim")
    {
        // deterministico: shape/validacao nao ganham com aleatoriedade
        return new IntegrarCreditoRequisicaoDto
        {
            NumeroCredito = numeroCredito,
            NumeroNfse = "nfse-1",
            DataConstituicao = new DateTime(2024, 2, 25),
            ValorIssqn = 1500.75m,
            TipoCredito = "ISSQN",
            SimplesNacional = simplesNacional,
            Aliquota = 5.0m,
            ValorFaturado = 30000m,
            ValorDeducao = 5000m,
            BaseCalculo = 25000m
        };
    }
}
