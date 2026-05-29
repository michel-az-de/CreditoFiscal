using System.Threading;
using System.Threading.Tasks;

namespace CreditoFiscal.Dominio.Abstracoes;

// commit explicito: o repositorio so monta o agregado; quem fecha a transacao e a unidade
public interface IUnidadeDeTrabalho
{
    Task SalvarAsync(CancellationToken ct);
}
