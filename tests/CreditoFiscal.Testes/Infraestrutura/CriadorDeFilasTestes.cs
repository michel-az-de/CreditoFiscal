using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Infraestrutura.Mensageria;
using FluentAssertions;
using NSubstitute;
using RabbitMQ.Client;
using Xunit;

namespace CreditoFiscal.Testes.Infraestrutura;

public sealed class CriadorDeFilasTestes
{
    [Fact]
    public async Task StartAsync_DeveDeclararDlqAntesDaFilaPrincipal()
    {
        var canal = Substitute.For<IModel>();
        var conexao = Substitute.For<IConnection>();
        conexao.CreateModel().Returns(canal);
        var criador = new CriadorDeFilas(conexao, "fila");

        await criador.StartAsync(CancellationToken.None);

        Received.InOrder(() =>
        {
            canal.QueueDeclare("fila-dlq", durable: true, exclusive: false, autoDelete: false, arguments: Arg.Is<IDictionary<string, object>?>(a => a == null));
            canal.QueueDeclare("fila", durable: true, exclusive: false, autoDelete: false, arguments: Arg.Any<IDictionary<string, object>>());
        });
    }

    [Fact]
    public async Task StartAsync_FilaPrincipalDeveSerQuorumComDeliveryLimitEDeadLetterRoutingKey()
    {
        var canal = Substitute.For<IModel>();
        var conexao = Substitute.For<IConnection>();
        conexao.CreateModel().Returns(canal);
        var criador = new CriadorDeFilas(conexao, "fila");

        await criador.StartAsync(CancellationToken.None);

        canal.Received(1).QueueDeclare(
            "fila",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: Arg.Is<IDictionary<string, object>>(args =>
                args.ContainsKey("x-queue-type") && (string)args["x-queue-type"] == "quorum"
                && args.ContainsKey("x-delivery-limit")
                && args.ContainsKey("x-dead-letter-exchange") && (string)args["x-dead-letter-exchange"] == string.Empty
                && args.ContainsKey("x-dead-letter-routing-key") && (string)args["x-dead-letter-routing-key"] == "fila-dlq"));
    }
}
