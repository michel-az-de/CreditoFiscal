namespace CreditoFiscal.Dominio.Entidades;

// regime fiscal (LC 123/2006): afeta o ISSQN, por isso e enum de dominio e nao bool
public enum SimplesNacional
{
    NaoOptante = 0,
    Optante = 1
}
