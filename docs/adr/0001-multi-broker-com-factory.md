# 0001 Multi broker com Factory

Status: aceito.
Data: 2026-05-29.

## Contexto

O enunciado pede ingestao via mensageria. A entrega tem que demonstrar competencia em multiplos brokers (Kafka e Azure Service Bus citados no requisito) e nao acoplar Controller e Consumer a um broker especifico. RabbitMQ entrou como terceira opcao por ser leve e familiar em CI.

## Decisao

Domino define `IMensagemPublisher` e `IMensagemConsumer` separados (ISP). Cada broker tem um adapter concreto (`AdaptadorKafka`, `AdaptadorRabbitMq`, `AdaptadorServiceBus`) implementando os dois e registrado uma vez no DI. `MensageriaExtensions.AdicionarMensageria` escolhe o adapter pela chave `Mensageria:Provedor` em tempo de composicao. Kafka e o default.

## Consequencias

Positivas: trocar de broker vira mudanca de string em `appsettings.json` ou variavel de ambiente, sem tocar Controller, Consumer ou casos de uso. O health check de `/ready` segue o mesmo seletor, entao `dotnet test` e cada `--profile` continuam saudaveis. Cada adapter e singleton, alinhado com a thread-safety de cada SDK.

Negativas: o modelo de streaming do Kafka nao tem ack ou requeue por mensagem como AMQP e Service Bus. `Confirmar` vira commit de offset e `Rejeitar(reencaminhar:true)` vira `seek` de volta. Isso e documentado no codigo e em ADR posterior sobre DLQ.
