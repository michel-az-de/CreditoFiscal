using CreditoFiscal.Dominio.Abstracoes;
using FluentAssertions;
using Xunit;

namespace CreditoFiscal.Testes.Dominio;

public sealed class ReceivedMessageTestes
{
    [Fact]
    public void Construtor_QuandoSemInit_DeveTerTentativasZero()
    {
        var envelope = new ReceivedMessage<string>("conteudo");

        envelope.Conteudo.Should().Be("conteudo");
        envelope.Tentativas.Should().Be(0);
    }

    [Fact]
    public void Init_QuandoTentativasInformado_DeveExporOValor()
    {
        var envelope = new ReceivedMessage<string>("conteudo") { Tentativas = 3 };

        envelope.Tentativas.Should().Be(3);
    }
}
