using System;

namespace CreditoFiscal.Dominio.Excecoes;

public sealed class CreditoDuplicadoException : Exception
{
    public CreditoDuplicadoException(string numeroCredito)
        : base($"Credito ja existente: {numeroCredito}")
    {
        NumeroCredito = numeroCredito;
    }

    public string NumeroCredito { get; }
}
