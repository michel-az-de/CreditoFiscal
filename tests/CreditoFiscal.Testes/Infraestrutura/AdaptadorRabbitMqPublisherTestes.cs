using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Infraestrutura.Mensageria;
using FluentAssertions;
using NSubstitute;
using RabbitMQ.Client;
using Xunit;

namespace CreditoFiscal.Testes.Infraestrutura;

public sealed class AdaptadorRabbitMqPublisherTestes
{
    [Fact]
    public async Task PublicarAsync_DeveCriarCanalLocalPublicarDuravelEDescartar()
    {
        var conexao = Substitute.For<IConnection>();
        var canal = Substitute.For<IModel>();
        var propriedades = Substitute.For<IBasicProperties>();
        conexao.CreateModel().Returns(canal);
        canal.CreateBasicProperties().Returns(propriedades);
        var adapter = new AdaptadorRabbitMq(conexao);

        await adapter.PublicarAsync("fila-teste", new Conteudo { Valor = "abc" }, CancellationToken.None);

        conexao.Received(1).CreateModel();
        propriedades.Received().Persistent = true;
        canal.Received(1).BasicPublish(
            "",
            "fila-teste",
            false,
            propriedades,
            Arg.Is<ReadOnlyMemory<byte>>(corpo => CorpoContem(corpo, "abc")));
        canal.Received(1).Dispose();
    }

    [Fact]
    public void Adapter_NaoDeveGuardarIModelComoCampo()
    {
        // C17: IModel nao e thread-safe; o publisher singleton cria um canal local por chamada
        var campos = typeof(AdaptadorRabbitMq).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        campos.Should().NotContain(campo => typeof(IModel).IsAssignableFrom(campo.FieldType));
    }

    private static bool CorpoContem(ReadOnlyMemory<byte> corpo, string esperado)
    {
        var json = Encoding.UTF8.GetString(corpo.ToArray());
        return json.Contains(esperado);
    }

    private sealed record Conteudo
    {
        public string Valor { get; init; } = string.Empty;
    }
}
