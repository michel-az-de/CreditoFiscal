using System.Threading;
using System.Threading.Tasks;

namespace CreditoFiscal.Dominio.Abstracoes;

public interface IMensagemPublisher
{
    Task PublicarAsync<T>(string fila, T mensagem, CancellationToken ct);
}
