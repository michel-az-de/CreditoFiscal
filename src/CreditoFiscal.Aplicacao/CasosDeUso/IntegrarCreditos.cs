using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CreditoFiscal.Aplicacao.Dtos;
using CreditoFiscal.Aplicacao.Mapeamentos;
using CreditoFiscal.Aplicacao.Mensagens;
using CreditoFiscal.Dominio.Abstracoes;

namespace CreditoFiscal.Aplicacao.CasosDeUso;

public sealed class IntegrarCreditos : IIntegrarCreditos
{
    private readonly IMensagemPublisher _publisher;

    public IntegrarCreditos(IMensagemPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task ExecutarAsync(IReadOnlyList<IntegrarCreditoRequisicaoDto> creditos, CancellationToken ct)
    {
        // valida o lote inteiro antes de publicar: um SimplesNacional invalido derruba tudo
        // com 400, sem deixar metade dos creditos na fila
        var mensagens = new List<CreditoConstituidoDto>();
        foreach (var requisicao in creditos)
        {
            mensagens.Add(ConverterParaMensagem(requisicao));
        }

        // publica um a um: cada credito e uma mensagem independente (sem bulk)
        foreach (var mensagem in mensagens)
        {
            await _publisher.PublicarAsync(Filas.IntegrarCreditoConstituido, mensagem, ct);
        }
    }

    private static CreditoConstituidoDto ConverterParaMensagem(IntegrarCreditoRequisicaoDto requisicao)
    {
        return new CreditoConstituidoDto
        {
            NumeroCredito = requisicao.NumeroCredito,
            NumeroNfse = requisicao.NumeroNfse,
            // invariante: ModelState ([Required]) garante DataConstituicao != null aqui.
            // Caller que invocar fora do pipeline HTTP precisa setar o valor antes.
            // O '!' suprime o CS8629 (flow analysis nao consegue inferir o invariante a partir do atributo).
            DataConstituicao = requisicao.DataConstituicao!.Value,
            ValorIssqn = requisicao.ValorIssqn,
            TipoCredito = requisicao.TipoCredito,
            // "Sim"/"Não" invalido lanca ArgumentException aqui -> middleware devolve 400
            SimplesNacional = ConversorSimplesNacional.ParaEnum(requisicao.SimplesNacional),
            Aliquota = requisicao.Aliquota,
            ValorFaturado = requisicao.ValorFaturado,
            ValorDeducao = requisicao.ValorDeducao,
            BaseCalculo = requisicao.BaseCalculo
        };
    }
}
