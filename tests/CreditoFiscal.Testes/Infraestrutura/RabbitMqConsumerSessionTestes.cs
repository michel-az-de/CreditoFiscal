using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Infraestrutura.Mensageria;
using FluentAssertions;
using NSubstitute;
using RabbitMQ.Client;
using Xunit;

namespace CreditoFiscal.Testes.Infraestrutura;

public sealed class RabbitMqConsumerSessionTestes
{
    [Fact]
    public async Task AbrirSessaoAsync_QuandoFilaTemUmaMensagem_DeveDrenarAteOPrimeiroNull()
    {
        var canal = Substitute.For<IModel>();
        canal.BasicGet("fila", false).Returns(CriarResultado(1, Corpo("x")), (BasicGetResult?)null);
        var adapter = MontarAdapter(canal);

        await using var sessao = await adapter.AbrirSessaoAsync<Conteudo>("fila", 10, UmSegundo(), CancellationToken.None);

        sessao.Mensagens.Should().HaveCount(1);
        canal.Received(2).BasicGet("fila", false);
    }

    [Fact]
    public async Task AbrirSessaoAsync_QuandoHaMaisQueOMaximo_DeveRespeitarOMaximo()
    {
        var canal = Substitute.For<IModel>();
        canal.BasicGet("fila", false).Returns(
            CriarResultado(1, Corpo("a")),
            CriarResultado(2, Corpo("b")),
            CriarResultado(3, Corpo("c")));
        var adapter = MontarAdapter(canal);

        await using var sessao = await adapter.AbrirSessaoAsync<Conteudo>("fila", 2, UmSegundo(), CancellationToken.None);

        sessao.Mensagens.Should().HaveCount(2);
        canal.Received(2).BasicGet("fila", false);
    }

    [Fact]
    public async Task Sessao_QuandoDuasEntregasComMesmoConteudo_DeveConfirmarCadaUmaSeparadamente()
    {
        // C13: ReceivedMessage e identidade por referencia. Duas entregas iguais sao envelopes
        // distintos; se fosse record, colidiriam no dicionario e um DeliveryTag ficaria orfao.
        var canal = Substitute.For<IModel>();
        canal.BasicGet("fila", false).Returns(
            CriarResultado(1, Corpo("igual")),
            CriarResultado(2, Corpo("igual")),
            null);
        var adapter = MontarAdapter(canal);

        await using var sessao = await adapter.AbrirSessaoAsync<Conteudo>("fila", 10, UmSegundo(), CancellationToken.None);

        sessao.Mensagens.Should().HaveCount(2);
        foreach (var mensagem in sessao.Mensagens)
        {
            await sessao.ConfirmarAsync(mensagem, CancellationToken.None);
        }

        canal.Received(1).BasicAck(1, false);
        canal.Received(1).BasicAck(2, false);
    }

    [Fact]
    public async Task RejeitarAsync_ComReencaminhar_DeveDarBasicNackComRequeue()
    {
        var canal = Substitute.For<IModel>();
        canal.BasicGet("fila", false).Returns(CriarResultado(7, Corpo("x")), (BasicGetResult?)null);
        var adapter = MontarAdapter(canal);

        await using var sessao = await adapter.AbrirSessaoAsync<Conteudo>("fila", 10, UmSegundo(), CancellationToken.None);
        var mensagem = sessao.Mensagens[0];

        await sessao.RejeitarAsync(mensagem, reencaminhar: true, CancellationToken.None);

        canal.Received(1).BasicNack(7, false, true);
    }

    [Fact]
    public async Task DisposeAsync_DeveDescartarOCanalDaSessao()
    {
        var canal = Substitute.For<IModel>();
        canal.BasicGet("fila", false).Returns((BasicGetResult?)null);
        var adapter = MontarAdapter(canal);
        var sessao = await adapter.AbrirSessaoAsync<Conteudo>("fila", 10, UmSegundo(), CancellationToken.None);

        await sessao.DisposeAsync();

        canal.Received(1).Dispose();
    }

    private static AdaptadorRabbitMq MontarAdapter(IModel canal)
    {
        var conexao = Substitute.For<IConnection>();
        conexao.CreateModel().Returns(canal);
        return new AdaptadorRabbitMq(conexao);
    }

    private static BasicGetResult CriarResultado(ulong tag, byte[] corpo)
    {
        return new BasicGetResult(
            deliveryTag: tag,
            redelivered: false,
            exchange: "",
            routingKey: "fila",
            messageCount: 0,
            basicProperties: Substitute.For<IBasicProperties>(),
            body: corpo);
    }

    private static byte[] Corpo(string valor)
    {
        return Encoding.UTF8.GetBytes("{\"valor\":\"" + valor + "\"}");
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
