using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CreditoFiscal.Dominio.Abstracoes;

public interface ISessaoDeConsumo<T> : IAsyncDisposable
{
    IReadOnlyList<MensagemRecebida<T>> Mensagens { get; }

    Task ConfirmarAsync(MensagemRecebida<T> mensagem, CancellationToken ct);

    Task RejeitarAsync(MensagemRecebida<T> mensagem, bool reencaminhar, CancellationToken ct);
}
