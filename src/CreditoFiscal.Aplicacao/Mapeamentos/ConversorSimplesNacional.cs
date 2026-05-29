using System;
using CreditoFiscal.Dominio.Entidades;

namespace CreditoFiscal.Aplicacao.Mapeamentos;

// anti-corruption: traduz o "Sim"/"Não" do JSON pro enum do dominio
public static class ConversorSimplesNacional
{
    private const string Sim = "Sim";
    private const string Nao = "Não";

    public static SimplesNacional ParaEnum(string valor)
    {
        switch (valor)
        {
            case Sim:
                return SimplesNacional.Optante;
            case Nao:
                return SimplesNacional.NaoOptante;
            default:
                throw new ArgumentException(
                    $"Valor invalido para SimplesNacional: '{valor}'. Esperado '{Sim}' ou '{Nao}'.",
                    nameof(valor));
        }
    }

    public static string ParaString(SimplesNacional valor)
    {
        switch (valor)
        {
            case SimplesNacional.Optante:
                return Sim;
            case SimplesNacional.NaoOptante:
                return Nao;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(valor), valor, "Valor de SimplesNacional fora do dominio.");
        }
    }
}
