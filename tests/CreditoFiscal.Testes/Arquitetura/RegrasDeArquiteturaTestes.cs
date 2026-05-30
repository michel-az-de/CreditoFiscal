using CreditoFiscal.Aplicacao.CasosDeUso;
using CreditoFiscal.Dominio.Entidades;
using CreditoFiscal.Infraestrutura.Persistencia;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace CreditoFiscal.Testes.Arquitetura;

// guardrail automatico contra regressao de dependencia. Reflete as setas de arquitetura
// do README: Api -> {Aplicacao, Infraestrutura} -> Dominio. Tudo o que quebrar isso
// rompe a clean architecture e e merecedor de reprovacao no CI.
public sealed class RegrasDeArquiteturaTestes
{
    [Fact]
    public void Dominio_NaoDependeDeOutroProjetoDaSolucao()
    {
        var resultado = Types.InAssembly(typeof(Credito).Assembly)
            .Should()
            .NotHaveDependencyOnAll("CreditoFiscal.Aplicacao", "CreditoFiscal.Infraestrutura", "CreditoFiscal.Api")
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(NomesEmFalha(resultado));
    }

    [Fact]
    public void Aplicacao_NaoDependeDeInfraestruturaNemApi()
    {
        var resultado = Types.InAssembly(typeof(IIntegrarCreditos).Assembly)
            .Should()
            .NotHaveDependencyOnAll("CreditoFiscal.Infraestrutura", "CreditoFiscal.Api")
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(NomesEmFalha(resultado));
    }

    [Fact]
    public void Infraestrutura_NaoDependeDeAplicacaoNemApi()
    {
        var resultado = Types.InAssembly(typeof(CreditoFiscalDbContext).Assembly)
            .Should()
            .NotHaveDependencyOnAll("CreditoFiscal.Aplicacao", "CreditoFiscal.Api")
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(NomesEmFalha(resultado));
    }

    [Fact]
    public void Controllers_NaoDependemDeDbContextIConfigurationNemDeTiposDeInfraestrutura()
    {
        // controllers devem ser finos: chamar caso de uso e traduzir HTTP, nunca abrir
        // DbContext direto nem ler configuracao bruta. Esse teste fixa essa convencao.
        var resultado = Types.InAssembly(typeof(Program).Assembly)
            .That()
            .ResideInNamespace("CreditoFiscal.Api.Controllers")
            .Should()
            .NotHaveDependencyOnAll("Microsoft.EntityFrameworkCore", "Microsoft.Extensions.Configuration", "CreditoFiscal.Infraestrutura")
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(NomesEmFalha(resultado));
    }

    [Fact]
    public void CasosDeUso_DevemSerSealed()
    {
        // intencao: caso de uso e ponto de entrada de fluxo, nao base para heranca.
        // Inclui os tipos concretos do namespace CasosDeUso e exclui o tipo de extensao
        // estatico (que ja e sealed implicito).
        var resultado = Types.InAssembly(typeof(IIntegrarCreditos).Assembly)
            .That()
            .ResideInNamespace("CreditoFiscal.Aplicacao.CasosDeUso")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveName("AplicacaoExtensions")
            .Should()
            .BeSealed()
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(NomesEmFalha(resultado));
    }

    private static string NomesEmFalha(TestResult resultado)
    {
        if (resultado.FailingTypeNames == null)
        {
            return "tipos em falha indisponiveis";
        }

        return "tipos em falha: " + string.Join(", ", resultado.FailingTypeNames);
    }
}
