using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CreditoFiscal.Dominio.Abstracoes;

public interface IConsumerSession<T> : IAsyncDisposable
{
    IReadOnlyList<ReceivedMessage<T>> Mensagens { get; }

    Task ConfirmarAsync(ReceivedMessage<T> mensagem, CancellationToken ct);

    Task RejeitarAsync(ReceivedMessage<T> mensagem, bool reencaminhar, CancellationToken ct);

    Task EnviarParaDlqAsync(ReceivedMessage<T> mensagem, string motivo, CancellationToken ct);
}
