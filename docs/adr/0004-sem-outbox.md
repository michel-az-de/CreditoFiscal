# 0004 Sem outbox pattern na ingestao

Status: aceito.
Data: 2026-05-29.

## Contexto

`Outbox pattern transacional` foi considerado como bonus porque resolve dual write entre commit de estado e emissao de evento. A primeira analise descreveu como "transacional, resolve dual write API broker", mas isso enquadra mal a arquitetura atual.

## Decisao

Nao implementar outbox. Manter o fluxo direto: o POST publica no broker, o `CreditoConsumer` persiste depois.

## Consequencias

Positivas: economia de tempo (estimativa ~5h) reinvestida em DLQ por broker (ADR 0006) e ADRs (este bloco), com maior retorno na entrega.

Negativas honestas:
- Em "publish parcial do lote", se a publicacao da mensagem `N` falhar (broker indisponivel, timeout), as mensagens `1..N-1` ja foram aceitas pelo broker e serao processadas. O cliente recebe `500` e o retry do lote inteiro e seguro pela idempotencia por `numero_credito` (ADR 0002). Trade-off documentado no README como `at-least-once com retry idempotente, sem outbox`.
- Se um dia o consumer comecar a emitir eventos de dominio apos persistir, ai sim faria sentido outbox canonico (commit de estado + evento na mesma transacao). Hoje o consumer nao emite, fora a auditoria fire-and-forget que vive no GET (ADR 0003), nao no commit.

## Por que nao reverter

Reverter exigiria reescrever a secao `Publish parcial do lote` em `Idempotencia e garantia de entrega` no README. O texto atual ja documenta a escolha como consciente. Acrescentar outbox cria contradicao com o paragrafo existente. Se o trade-off mudar (ex.: contrato de cliente exigir entrega exatamente-uma-vez), reabrir o ADR.
