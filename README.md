# CreditoFiscal
Microsserviço de créditos fiscais constituídos com .NET 6, Kafka, PostgreSQL e Docker. Recebe créditos via API, publica em tópico Kafka, e um BackgroundService consome e persiste de forma idempotente. Inclui health checks, Swagger e Postman Collection.
