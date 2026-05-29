using CreditoFiscal.Aplicacao.Dtos;
using CreditoFiscal.Dominio.Entidades;

namespace CreditoFiscal.Aplicacao.Mapeamentos;

// mapeia a entidade de dominio pro DTO de resposta (SimplesNacional vira "Sim"/"Não")
public static class CreditoRespostaMapeador
{
    public static CreditoRespostaDto Mapear(Credito credito)
    {
        return new CreditoRespostaDto
        {
            NumeroCredito = credito.NumeroCredito,
            NumeroNfse = credito.NumeroNfse,
            DataConstituicao = credito.DataConstituicao,
            ValorIssqn = credito.ValorIssqn,
            TipoCredito = credito.TipoCredito,
            SimplesNacional = ConversorSimplesNacional.ParaString(credito.SimplesNacional),
            Aliquota = credito.Aliquota,
            ValorFaturado = credito.ValorFaturado,
            ValorDeducao = credito.ValorDeducao,
            BaseCalculo = credito.BaseCalculo
        };
    }
}
