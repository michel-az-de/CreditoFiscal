using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace CreditoFiscal.Infraestrutura.Persistencia;

public sealed class CreditoRepository : ICreditoRepository
{
    private readonly CreditoFiscalDbContext _contexto;

    public CreditoRepository(CreditoFiscalDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task AdicionarAsync(Credito credito, CancellationToken ct)
    {
        await _contexto.Creditos.AddAsync(credito, ct);
    }

    public Task<Credito?> ObterPorNumeroCreditoAsync(string numeroCredito, CancellationToken ct)
    {
        return _contexto.Creditos
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.NumeroCredito == numeroCredito, ct);
    }

    public async Task<IReadOnlyList<Credito>> ObterPorNumeroNfseAsync(string numeroNfse, CancellationToken ct)
    {
        return await _contexto.Creditos
            .AsNoTracking()
            .Where(c => c.NumeroNfse == numeroNfse)
            .ToListAsync(ct);
    }

    public Task<bool> ExisteAsync(string numeroCredito, CancellationToken ct)
    {
        return _contexto.Creditos
            .AsNoTracking()
            .AnyAsync(c => c.NumeroCredito == numeroCredito, ct);
    }
}
