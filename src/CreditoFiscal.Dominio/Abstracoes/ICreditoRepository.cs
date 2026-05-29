using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Dominio.Entidades;

namespace CreditoFiscal.Dominio.Abstracoes;

public interface ICreditoRepository
{
    Task AdicionarAsync(Credito credito, CancellationToken ct);

    Task<Credito?> ObterPorNumeroCreditoAsync(string numeroCredito, CancellationToken ct);

    Task<IReadOnlyList<Credito>> ObterPorNumeroNfseAsync(string numeroNfse, CancellationToken ct);

    Task<bool> ExisteAsync(string numeroCredito, CancellationToken ct);
}
