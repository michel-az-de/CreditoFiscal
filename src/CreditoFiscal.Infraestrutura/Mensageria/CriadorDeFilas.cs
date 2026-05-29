using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace CreditoFiscal.Infraestrutura.Mensageria;

// declara a fila (idempotente) no startup, antes de qualquer publicacao ou consumo
public sealed class CriadorDeFilas : IHostedService
{
    private readonly IConnection _conexao;
    private readonly string _nomeDaFila;

    public CriadorDeFilas(IConnection conexao, string nomeDaFila)
    {
        _conexao = conexao;
        _nomeDaFila = nomeDaFila;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var canal = _conexao.CreateModel();
        canal.QueueDeclare(queue: _nomeDaFila, durable: true, exclusive: false, autoDelete: false, arguments: null);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
