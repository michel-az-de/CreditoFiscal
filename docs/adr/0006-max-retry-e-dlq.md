# 0006 Max retry e DLQ por broker

Status: aceito.
Data: 2026-05-29.

## Contexto

O `CreditoConsumer` chamava `RejeitarAsync(reencaminhar: true)` em qualquer excecao que nao fosse `DbUpdateException`. Uma mensagem com defeito de dominio nao previsto entrava em loop em todos os brokers, consumindo CPU e poluindo logs sem progresso. A primeira analise propunha um header `x-tentativas` carregado no envelope, uniforme nos tres brokers. Investigacao revelou que isso falha em dois dos tres: o broker nao modifica headers customizados em redelivery no RabbitMQ classic, e o `seek` no Kafka rele o mesmo registro bit a bit.

## Decisao

Estrategia por broker, com gate unico no consumer (`Mensageria:MaxTentativasConsumer`, default 5). `ReceivedMessage<T>.Tentativas` e populado em base 1 conforme o sinal de cada broker:

- Service Bus: `ServiceBusReceivedMessage.DeliveryCount` (system property, broker incrementa em cada `AbandonMessageAsync`). DLQ via `DeadLetterMessageAsync` nativa. `MaxDeliveryCount: 10` no emulator e rede de seguranca.
- RabbitMQ: fila migrada para quorum com `x-delivery-limit: 10`, `x-dead-letter-exchange` e `x-dead-letter-routing-key` na declaracao. Sessao le `x-delivery-count` do header e soma 1. DLQ `<fila>-dlq` classic durable declarada antes da principal.
- Kafka: `AdaptadorKafka` mantem `RegistroDeTentativasKafka` em memoria, indexado por `TopicPartitionOffset`, com `lock`. Sessao consulta na construcao, incrementa em `RejeitarAsync(reencaminhar:true)` e esquece em `ConfirmarAsync`. DLQ publicada em `<fila>-dlq` (auto-criado pelo broker em dev).

## Consequencias

Positivas: fecha o poison loop. Cada broker faz o que sabe fazer melhor sem violar abstracao. Testes unitarios cobrem o gate do consumer e o populamento de `Tentativas` em cada adapter (`Tentativas` cresce, vai pra DLQ na N-esima tentativa).

Negativas: o limite no Kafka e por instancia de consumer, nao absoluto. Em restart do processo ou rebalance do consumer group, o contador zera e o ciclo recomeca. Aceito como melhor que loop infinito; documentado em README "Limitacoes conhecidas".
