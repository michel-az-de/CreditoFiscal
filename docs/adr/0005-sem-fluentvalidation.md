# 0005 Sem FluentValidation no DTO de entrada

Status: aceito.
Data: 2026-05-29.

## Contexto

FluentValidation foi considerado como bonus para validacao em camadas com regras de negocio fiscal (alicota, base de calculo, valor faturado e deducao). A primeira analise propunha regras como `baseCalculo == valorFaturado - valorDeducao` e `valorIssqn ~ baseCalculo * aliquota / 100`.

## Decisao

Nao introduzir FluentValidation. Manter DataAnnotations no `IntegrarCreditoRequisicaoDto` (`[Required]`, `[Range(0, ...)]`, `[StringLength(50, MinimumLength = 1)]`, `DateTime?` em `DataConstituicao`).

## Consequencias

Por que ficar com DataAnnotations:

1. Contrato de erro ja esta fechado: `ValidationProblemDetails` em `application/problem+json` com `status: 400` no corpo, chaves do DTO em PascalCase, chave manual `creditos` em camelCase. Trocar para FluentValidation com paridade de wire exige carpintaria significativa com risco real de regressao no shape do 400, e o cliente recebe o mesmo `ValidationProblemDetails` no fim. Ganho percebido marginal.

2. Aritmetica fiscal nao pertence aqui. Este servico ingere creditos ja constituidos, nao calcula ISSQN. Revalidar `baseCalculo == valorFaturado - valorDeducao` rejeita registros legitimos por arredondamento, ISS retido na fonte e regimes especiais. Sai do papel do servico.

3. Piso de alicota: o piso legal do ISS e 2% (LC 116/2003 art. 8-A via LC 157/2016), nao `0,01`. Ha excecoes por tipo de servico (itens 7.02, 7.05, 16.01 da Lista anexa). Range fixo no DTO seria fragil. Preferimos `[Range(0, 100)]` como guardrail defensivo e documentar em "Endurecimento parcial" no README.

## Quando reabrir

Se houver pedido para validar regras de negocio fiscais via servico (e nao via consumidor da API), a opcao e (a) FluentValidation substituindo DataAnnotations com paridade de wire exata para `ValidationProblemDetails`. Opcao (b), nao adicionar, segue valida ate la.
