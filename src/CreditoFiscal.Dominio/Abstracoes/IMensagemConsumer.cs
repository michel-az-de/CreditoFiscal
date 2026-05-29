using System;
using System.Threading;
using System.Threading.Tasks;

namespace CreditoFiscal.Dominio.Abstracoes;

public interface IMensagemConsumer
{
    Task<ISessaoDeConsumo<T>> AbrirSessaoAsync<T>(string fila, int maximo, TimeSpan timeout, CancellationToken ct);
}
