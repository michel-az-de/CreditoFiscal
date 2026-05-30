# 0003 Auditoria de consultas best effort

Status: aceito.
Data: 2026-05-29.

## Contexto

Cada consulta deve publicar evento de auditoria para um sistema externo (logs/eventos). A leitura nao pode falhar porque a publicacao de auditoria falhou: o GET tem que responder mesmo com o broker fora.

## Decisao

`PublicadorAuditoria` publica `ConsultaCreditoRealizadaDto` no topico `consulta-credito-realizada` em cada GET. O publish acontece dentro de um `CancellationTokenSource.CreateLinkedTokenSource(ct)` com `CancelAfter(200ms)`: broker lento estoura o teto local sem amarrar o GET. Tres caminhos:

- Sucesso: publish completa antes do teto.
- Estouro do teto local (`cts.IsCancellationRequested && !ct.IsCancellationRequested`): vira `LogWarning` distinto (mostra `TimeoutMs` no template) sem propagar.
- Falha de broker (conexao recusada, serialization, etc.) ou cancelamento do caller: `catch (Exception)` loga `Warning` generico e nao propaga.

Sem retry, sem outbox, sem fila local. Reutiliza o `IMensagemPublisher` do provedor escolhido em `Mensageria:Provedor`.

## Consequencias

Positivas: auditoria nunca derruba a consulta. O teto de 200ms acota a latencia adicional que um GET paga pelo publish, independente de timeout configurado no cliente do broker. Trocar de broker continua sendo uma mudanca de string. Codigo simples e isolado em um caso de uso de borda.

Negativas: ainda `at-most-once`. Broker fora ou estouro de teto perde o evento de auditoria (warning logado, sem retry). Se a auditoria virar requisito forte (compliance, audit trail nao podendo perder), a evolucao natural e um outbox transacional (ver ADR 0004) ou uma fila local de eventos drenada por um worker dedicado.
