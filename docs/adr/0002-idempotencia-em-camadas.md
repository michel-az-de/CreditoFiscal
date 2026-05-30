# 0002 Idempotencia em camadas

Status: aceito.
Data: 2026-05-29.

## Contexto

Entrega `at-least-once` e a postura padrao para brokers de mensageria. Sem idempotencia, o consumer duplica entidades a cada redelivery. Como cada mensagem e persistida distintamente (sem bulk), a janela de duplicata existe e precisa ser fechada.

## Decisao

Duas camadas independentes evitam duplicata:

1. Verificacao previa em memoria: `ICreditoRepository.ExisteAsync(numeroCredito)` antes de persistir. Se ja existe, o consumer confirma sem reprocessar.
2. Indice unico em `numero_credito` no banco. Em corrida entre instancias, `SaveChanges` lanca `DbUpdateException`, tratada como duplicata e confirmada.

O `ack` so acontece depois do commit no banco. Se o processo morre entre persistir e ack, a reentrega cai em uma das duas camadas.

## Consequencias

Positivas: tolera redelivery sem efeito colateral. Tolera corrida entre instancias paralelas. `Reenvio_do_mesmo_credito_nao_duplica` em integracao prova ponta a ponta.

Negativas: a checagem previa custa um `SELECT` por mensagem em paralelo ao `INSERT`. Em fluxo de alta vazao, o gargalo do `INSERT` continua dominando (k6 mediu o consumer drenando ~20 msg/s no RabbitMQ classic, ver `docs/carga/resultados.md`).
