using System.Threading;
using System.Threading.Tasks;

namespace CreditoFiscal.Dominio.Abstracoes;

public interface IPublicadorDeMensagens
{
    Task PublicarAsync<T>(string fila, T mensagem, CancellationToken ct);
}
