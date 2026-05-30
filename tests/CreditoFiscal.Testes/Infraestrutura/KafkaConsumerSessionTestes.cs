using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using CreditoFiscal.Infraestrutura.Mensageria;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CreditoFiscal.Testes.Infraestrutura;

public sealed class KafkaConsumerSessionTestes
{
    [Fact]
    public async Task Tentativas_PrimeiraEntrega_DeveSerUm()
    {
        var resultado = CriarResultado(offset: 10, corpo: "{\"valor\":\"x\"}");
        var consumidor = Substitute.For<IConsumer<Null, byte[]>>();
        consumidor.Consume(Arg.Any<TimeSpan>()).Returns(resultado, (ConsumeResult<Null, byte[]>?)null);
        var adapter = new AdaptadorKafka(Substitute.For<IProducer<Null, byte[]>>(), consumidor);

        await using var sessao = await adapter.AbrirSessaoAsync<Conteudo>("fila", maximo: 10, UmSegundo(), CancellationToken.None);

        sessao.Mensagens.Should().HaveCount(1);
        sessao.Mensagens[0].Tentativas.Should().Be(1);
    }

    [Fact]
    public async Task Tentativas_AposRejeitarComReencaminhar_DeveCrescerNaProximaSessao()
    {
        var resultado = CriarResultado(offset: 10, corpo: "{\"valor\":\"x\"}");
        var consumidor = Substitute.For<IConsumer<Null, byte[]>>();
        consumidor.Consume(Arg.Any<TimeSpan>()).Returns(
            resultado, (ConsumeResult<Null, byte[]>?)null,
            resultado, (ConsumeResult<Null, byte[]>?)null);
        var adapter = new AdaptadorKafka(Substitute.For<IProducer<Null, byte[]>>(), consumidor);

        var sessao1 = await adapter.AbrirSessaoAsync<Conteudo>("fila", maximo: 10, UmSegundo(), CancellationToken.None);
        sessao1.Mensagens[0].Tentativas.Should().Be(1);
        await sessao1.RejeitarAsync(sessao1.Mensagens[0], reencaminhar: true, CancellationToken.None);
        await sessao1.DisposeAsync();

        var sessao2 = await adapter.AbrirSessaoAsync<Conteudo>("fila", maximo: 10, UmSegundo(), CancellationToken.None);

        sessao2.Mensagens[0].Tentativas.Should().Be(2);
        consumidor.Received(1).Seek(resultado.TopicPartitionOffset);
        await sessao2.DisposeAsync();
    }

    [Fact]
    public async Task ConfirmarAsync_DeveEsquecerOContador()
    {
        var resultado = CriarResultado(offset: 10, corpo: "{\"valor\":\"x\"}");
        var consumidor = Substitute.For<IConsumer<Null, byte[]>>();
        consumidor.Consume(Arg.Any<TimeSpan>()).Returns(
            resultado, (ConsumeResult<Null, byte[]>?)null,
            resultado, (ConsumeResult<Null, byte[]>?)null);
        var adapter = new AdaptadorKafka(Substitute.For<IProducer<Null, byte[]>>(), consumidor);

        var sessao1 = await adapter.AbrirSessaoAsync<Conteudo>("fila", maximo: 10, UmSegundo(), CancellationToken.None);
        await sessao1.RejeitarAsync(sessao1.Mensagens[0], reencaminhar: true, CancellationToken.None);
        await sessao1.DisposeAsync();

        var sessao2 = await adapter.AbrirSessaoAsync<Conteudo>("fila", maximo: 10, UmSegundo(), CancellationToken.None);
        await sessao2.ConfirmarAsync(sessao2.Mensagens[0], CancellationToken.None);
        await sessao2.DisposeAsync();

        // proxima leitura do mesmo offset (caso ele reaparecesse) volta a contar do 1
        var sessao3 = await adapter.AbrirSessaoAsync<Conteudo>("fila", maximo: 10, UmSegundo(), CancellationToken.None);
        sessao3.Mensagens.Should().BeEmpty();
        await sessao3.DisposeAsync();
        consumidor.Received(1).Commit(resultado);
    }

    [Fact]
    public async Task EnviarParaDlqAsync_DevePublicarNaDlqEComitarOffset()
    {
        var resultado = CriarResultado(offset: 10, corpo: "{\"valor\":\"x\"}");
        var consumidor = Substitute.For<IConsumer<Null, byte[]>>();
        consumidor.Consume(Arg.Any<TimeSpan>()).Returns(resultado, (ConsumeResult<Null, byte[]>?)null);
        var produtor = Substitute.For<IProducer<Null, byte[]>>();
        produtor.ProduceAsync(Arg.Any<string>(), Arg.Any<Message<Null, byte[]>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DeliveryResult<Null, byte[]>>(null!));
        var adapter = new AdaptadorKafka(produtor, consumidor);
        await using var sessao = await adapter.AbrirSessaoAsync<Conteudo>("fila", maximo: 10, UmSegundo(), CancellationToken.None);
        var mensagem = sessao.Mensagens[0];

        await sessao.EnviarParaDlqAsync(mensagem, "motivo-x", CancellationToken.None);

        await produtor.Received(1).ProduceAsync("fila-dlq", Arg.Any<Message<Null, byte[]>>(), Arg.Any<CancellationToken>());
        consumidor.Received(1).Commit(resultado);
    }

    private static ConsumeResult<Null, byte[]> CriarResultado(int offset, string corpo)
    {
        return new ConsumeResult<Null, byte[]>
        {
            Topic = "fila",
            Partition = new Partition(0),
            Offset = new Offset(offset),
            Message = new Message<Null, byte[]> { Value = Encoding.UTF8.GetBytes(corpo) }
        };
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
