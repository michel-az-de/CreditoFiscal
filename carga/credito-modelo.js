// modelo de credito valido reutilizado pelos cenarios de escrita
export function montarCredito(numeroCredito, numeroNfse) {
  return {
    numeroCredito: numeroCredito,
    numeroNfse: numeroNfse,
    dataConstituicao: '2024-02-25',
    valorIssqn: 1500.75,
    tipoCredito: 'ISSQN',
    simplesNacional: 'Sim',
    aliquota: 5.0,
    valorFaturado: 30000.0,
    valorDeducao: 5000.0,
    baseCalculo: 25000.0,
  };
}
