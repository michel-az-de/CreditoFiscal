using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using CreditoFiscal.Infraestrutura.Mensageria;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CreditoFiscal.Testes.Infraestrutura;

public sealed class ServiceBusConsumerSessionTestes
{
    [Fact]
    public async Task Tentativas_DeveRefletirDeliveryCountDoBroker()
    {
        var recebida = ConstruirRecebida(corpo: "{\"valor\":\"x\"}", deliveryCount: 3);
        var receptor = ReceptorRetornando(recebida);
        var adapter = new AdaptadorServiceBus(ClienteUsando(receptor));

        await using var sessao = await adapter.AbrirSessaoAsync<Conteudo>("fila", 10, UmSegundo(), CancellationToken.None);

        sessao.Mensagens.Should().HaveCount(1);
        sessao.Mensagens[0].Tentativas.Should().Be(3);
    }

    [Fact]
    public async Task EnviarParaDlqAsync_DeveChamarDeadLetterComMotivoExplicito()
    {
        var recebida = ConstruirRecebida(corpo: "{\"valor\":\"x\"}", deliveryCount: 5);
        var receptor = ReceptorRetornando(recebida);
        var adapter = new AdaptadorServiceBus(ClienteUsando(receptor));
        await using var sessao = await adapter.AbrirSessaoAsync<Conteudo>("fila", 10, UmSegundo(), CancellationToken.None);
        var mensagem = sessao.Mensagens[0];

        await sessao.EnviarParaDlqAsync(mensagem, "motivo-x", CancellationToken.None);

        await receptor.Received(1).DeadLetterMessageAsync(recebida, "motivo-x", null, Arg.Any<CancellationToken>());
    }

    private static ServiceBusReceivedMessage ConstruirRecebida(string corpo, int deliveryCount)
    {
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromBytes(Encoding.UTF8.GetBytes(corpo)),
            deliveryCount: deliveryCount);
    }

    private static ServiceBusReceiver ReceptorRetornando(ServiceBusReceivedMessage recebida)
    {
        var receptor = Substitute.For<ServiceBusReceiver>();
        receptor.ReceiveMessagesAsync(Arg.Any<int>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(new List<ServiceBusReceivedMessage> { recebida });
        return receptor;
    }

    private static ServiceBusClient ClienteUsando(ServiceBusReceiver receptor)
    {
        var cliente = Substitute.For<ServiceBusClient>();
        cliente.CreateReceiver(Arg.Any<string>()).Returns(receptor);
        return cliente;
    }

    private static TimeSpan UmSegundo()
    {
        return TimeSpan.FromSeconds(1);
    }

    private sealed record Conteudo
    {
        public string Valor { get; init; } = string.Empty;
    }
}
