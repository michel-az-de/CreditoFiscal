using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace CreditoFiscal.Infraestrutura.Mensageria;

// quorum queue + x-delivery-limit: o broker e a rede de seguranca; o consumer e o gate primario
// via Tentativas (que cobre race condition e restart).
public sealed class CriadorDeFilas : IHostedService
{
    private const int LimiteDeEntregasFila = 10;

    private readonly IConnection _conexao;
    private readonly string _nomeDaFila;
    private readonly string _nomeDaDlq;

    public CriadorDeFilas(IConnection conexao, string nomeDaFila)
    {
        _conexao = conexao;
        _nomeDaFila = nomeDaFila;
        _nomeDaDlq = nomeDaFila + "-dlq";
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var canal = _conexao.CreateModel();

        // DLQ existe antes da principal: a principal aponta pra ela via x-dead-letter-routing-key
        // e o broker so deixa de descartar mensagens dead-lettered quando a fila destino existe.
        canal.QueueDeclare(queue: _nomeDaDlq, durable: true, exclusive: false, autoDelete: false, arguments: null);

        var argumentos = new Dictionary<string, object>
        {
            ["x-queue-type"] = "quorum",
            ["x-delivery-limit"] = LimiteDeEntregasFila,
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = _nomeDaDlq
        };
        canal.QueueDeclare(queue: _nomeDaFila, durable: true, exclusive: false, autoDelete: false, arguments: argumentos);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
