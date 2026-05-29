using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Dominio.Abstracoes;

namespace CreditoFiscal.Infraestrutura.Persistencia;

public sealed class UnidadeDeTrabalho : IUnidadeDeTrabalho
{
    private readonly CreditoFiscalDbContext _contexto;

    public UnidadeDeTrabalho(CreditoFiscalDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task SalvarAsync(CancellationToken ct)
    {
        return _contexto.SaveChangesAsync(ct);
    }
}
