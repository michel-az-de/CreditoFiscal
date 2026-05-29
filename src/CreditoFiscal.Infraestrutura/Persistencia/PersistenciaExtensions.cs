using CreditoFiscal.Dominio.Abstracoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CreditoFiscal.Infraestrutura.Persistencia;

public static class PersistenciaExtensions
{
    public static IServiceCollection AdicionarPersistencia(this IServiceCollection services, IConfiguration configuration)
    {
        var conexao = configuration.GetConnectionString("Postgres");
        services.AddDbContext<CreditoFiscalDbContext>(opcoes => opcoes.UseNpgsql(conexao));
        services.AddScoped<ICreditoRepository, CreditoRepository>();
        services.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();
        return services;
    }
}
