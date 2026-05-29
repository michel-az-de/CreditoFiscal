using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CreditoFiscal.Infraestrutura.Persistencia;

// migrations usam esta fabrica, nao o Program.cs (que tentaria conectar no container)
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CreditoFiscalDbContext>
{
    public CreditoFiscalDbContext CreateDbContext(string[] args)
    {
        var opcoes = new DbContextOptionsBuilder<CreditoFiscalDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=creditofiscal;Username=postgres;Password=postgres")
            .Options;
        return new CreditoFiscalDbContext(opcoes);
    }
}
