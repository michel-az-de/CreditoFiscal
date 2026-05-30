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
        // pre-valida o lote: SimplesNacional invalido cancela tudo, sem publicar parcial
        var mensagens = new List<CreditoConstituidoDto>();
        foreach (var requisicao in creditos)
        {
            mensagens.Add(ConverterParaMensagem(requisicao));
        }

        // uma mensagem por credito: idempotencia, auditoria e DLQ operam por item
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
            // invariante: ModelState [Required] garante nao-nulo aqui; ! suprime CS8629
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
