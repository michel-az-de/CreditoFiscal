using CreditoFiscal.Aplicacao.Auditoria;
using Microsoft.Extensions.DependencyInjection;

namespace CreditoFiscal.Aplicacao.CasosDeUso;

public static class AplicacaoExtensions
{
    public static IServiceCollection AdicionarCasosDeUso(this IServiceCollection services)
    {
        services.AddScoped<IIntegrarCreditos, IntegrarCreditos>();
        services.AddScoped<IConsultarCreditosPorNfse, ConsultarCreditosPorNfse>();
        services.AddScoped<IConsultarCreditoPorNumero, ConsultarCreditoPorNumero>();
        services.AddScoped<IPublicadorAuditoria, PublicadorAuditoria>();
        return services;
    }
}
