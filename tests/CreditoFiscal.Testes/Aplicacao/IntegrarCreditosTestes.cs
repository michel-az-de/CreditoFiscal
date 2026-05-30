using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.CasosDeUso;
using CreditoFiscal.Aplicacao.Dtos;
using CreditoFiscal.Aplicacao.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;
using CreditoFiscal.Testes.Suporte;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CreditoFiscal.Testes.Aplicacao;

public sealed class IntegrarCreditosTestes
{
    [Fact]
    public async Task ExecutarAsync_QuandoCreditosValidos_DevePublicarCadaUmIndividualmente()
    {
        var publisher = Substitute.For<IMensagemPublisher>();
        var casoDeUso = new IntegrarCreditos(publisher);
        var creditos = new List<IntegrarCreditoRequisicaoDto>
        {
            CreditoMother.Requisicao("1", "Sim"),
            CreditoMother.Requisicao("2", "Não")
        };

        await casoDeUso.ExecutarAsync(creditos, CancellationToken.None);

        await publisher.Received(2).PublicarAsync(
            Filas.IntegrarCreditoConstituido,
            Arg.Any<CreditoConstituidoDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecutarAsync_QuandoSimplesNacionalInvalido_DeveLancarArgumentException()
    {
        var publisher = Substitute.For<IMensagemPublisher>();
        var casoDeUso = new IntegrarCreditos(publisher);
        var creditos = new List<IntegrarCreditoRequisicaoDto> { CreditoMother.Requisicao("1", "Talvez") };

        Func<Task> acao = AcaoDe(casoDeUso, creditos);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecutarAsync_QuandoUmCreditoDoLoteEhInvalido_NaoDevePublicarNenhum()
    {
        var publisher = Substitute.For<IMensagemPublisher>();
        var casoDeUso = new IntegrarCreditos(publisher);
        var creditos = new List<IntegrarCreditoRequisicaoDto>
        {
            CreditoMother.Requisicao("1", "Sim"),
            CreditoMother.Requisicao("2", "Talvez")
        };

        Func<Task> acao = AcaoDe(casoDeUso, creditos);

        await acao.Should().ThrowAsync<ArgumentException>();
        await publisher.DidNotReceive().PublicarAsync(Arg.Any<string>(), Arg.Any<CreditoConstituidoDto>(), Arg.Any<CancellationToken>());
    }

    private static Func<Task> AcaoDe(IntegrarCreditos casoDeUso, List<IntegrarCreditoRequisicaoDto> creditos)
    {
        return delegate
        {
            return casoDeUso.ExecutarAsync(creditos, CancellationToken.None);
        };
    }

}
