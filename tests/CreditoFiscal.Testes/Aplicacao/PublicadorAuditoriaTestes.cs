using System;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Auditoria;
using CreditoFiscal.Aplicacao.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CreditoFiscal.Testes.Aplicacao;

public sealed class PublicadorAuditoriaTestes
{
    [Fact]
    public async Task PublicarConsultaAsync_QuandoPublisherOk_DevePublicarNaFilaCerta()
    {
        var publisher = Substitute.For<IMensagemPublisher>();
        var sut = new PublicadorAuditoria(publisher, NullLogger<PublicadorAuditoria>.Instance);
        var evento = NovoEvento();

        await sut.PublicarConsultaAsync(evento, CancellationToken.None);

        await publisher.Received(1).PublicarAsync(
            Filas.ConsultaCreditoRealizada,
            Arg.Is<ConsultaCreditoRealizadaDto>(e => e == evento),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublicarConsultaAsync_QuandoPublisherFalha_NaoDevePropagar()
    {
        var publisher = Substitute.For<IMensagemPublisher>();
        publisher
            .PublicarAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("broker fora"));
        var sut = new PublicadorAuditoria(publisher, NullLogger<PublicadorAuditoria>.Instance);

        var acao = async () => await sut.PublicarConsultaAsync(NovoEvento(), CancellationToken.None);

        await acao.Should().NotThrowAsync();
    }

    private static ConsultaCreditoRealizadaDto NovoEvento()
    {
        return new ConsultaCreditoRealizadaDto
        {
            Tipo = "PorNumero",
            Chave = "X-1",
            QuantidadeRetornada = 1,
            OcorridoEm = new DateTime(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc)
        };
    }
}
