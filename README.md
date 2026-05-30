# CreditoFiscal

Microsserviço .NET 6 para consulta de créditos fiscais constituídos (ISSQN / NFS-e).
A API recebe créditos, publica cada um num tópico do broker configurado (Kafka por padrão,
com RabbitMQ e Azure Service Bus como alternativas); um `BackgroundService` consome o tópico
e persiste no PostgreSQL de forma **idempotente**; dois `GET`s consultam os créditos por
NFS-e e por número de crédito.

> Runtime: .NET 6 (versão mínima do desafio). .NET 6 está fora de suporte desde nov/2024; em produção rodaria em .NET 8 LTS.

## Índice

- [Fluxo](#fluxo)
- [Arquitetura](#arquitetura)
- [Como rodar](#como-rodar)
- [Configuração](#configuração)
- [Endpoints](#endpoints)
- [Decisões técnicas](#decisões-técnicas)
- [Idempotência e garantia de entrega](#idempotência-e-garantia-de-entrega)
- [Testes](#testes)
- [CI](#ci)
- [Validação executada](#validação-executada-stack-real)
- [Convenções](#convenções)
- [Limitações conhecidas](#limitações-conhecidas)

## Fluxo

- `POST /api/creditos/integrar-credito-constituido` valida o lote e publica uma mensagem por crédito no tópico `integrar-credito-constituido-entry`.
- `CreditoConsumer` (`BackgroundService`) faz polling de 500 ms, lê em lotes de até 10 e persiste cada mensagem no PostgreSQL com idempotência em camadas (seção própria).
- `GET /api/creditos/{numeroNfse}` e `GET /api/creditos/credito/{numeroCredito}` leem do banco e publicam um evento de auditoria no broker.

## Arquitetura

Quatro projetos, dependência sempre apontando para o domínio (`Api → Aplicacao → Domínio` e `Api → Infraestrutura → Domínio`):

| Projeto | Responsabilidade |
|---------|------------------|
| `CreditoFiscal.Dominio` | Entidade `Credito`, enum `SimplesNacional`, abstrações (`ICreditoRepository`, `IUnidadeDeTrabalho`, `IMensagemPublisher`, `IMensagemConsumer`, `IConsumerSession<T>`, `ReceivedMessage<T>`), exceções. Sem dependência de framework. |
| `CreditoFiscal.Aplicacao` | Casos de uso (`IIntegrarCreditos`, `IConsultarCreditosPorNfse`, `IConsultarCreditoPorNumero`), DTOs de entrada/saída, conversor de `SimplesNacional` e contratos de mensagem. Depende só do domínio, é a camada reutilizável (DLL própria). |
| `CreditoFiscal.Infraestrutura` | EF Core + Npgsql (`CreditoFiscalDbContext`, `CreditoRepository`, `UnidadeDeTrabalho`), mensageria (adapters RabbitMQ / Kafka / ServiceBus + sessões de consumo), conversor de data, extensões de DI. |
| `CreditoFiscal.Api` | Controllers (finos), middlewares (erro + correlation id), `CreditoConsumer`, health checks, OpenTelemetry, Swagger, composição (`Program.cs`). |

Padrões usados: **Repository** + **Unit of Work**, **Casos de uso** (camada de aplicação reutilizável), **Factory** (extension `AdicionarMensageria` escolhe o provedor), **Adapter** (RabbitMQ / Kafka / ServiceBus), **Anti-corruption layer** (`ConversorSimplesNacional`), **Session** descartável para consumo (`IConsumerSession<T>`), **Middleware**, **BackgroundService**.

## Como rodar

Pré-requisito: Docker.

```bash
docker compose up --build
```

Sobe Postgres, Kafka (broker local em KRaft) e a API. A API espera o Postgres e o Kafka ficarem
saudáveis antes de aceitar tráfego (Postgres é barreira para aplicar as migrations no startup).
Fica disponível em:

- API: `http://localhost:8080`
- Swagger: `http://localhost:8080/swagger`

### Trocar de provedor de mensageria

O provedor é escolhido por configuração (`Mensageria:Provedor`). **Kafka** é o padrão.
Os adapters de **RabbitMQ** e **ServiceBus** sobem em *profiles* do compose:

```bash
# RabbitMQ (painel de gerência em http://localhost:15672, login guest/guest)
MENSAGERIA_PROVEDOR=RabbitMQ docker compose --profile rabbitmq up --build

# ServiceBus (emulator oficial + SQL de apoio)
MENSAGERIA_PROVEDOR=ServiceBus docker compose --profile servicebus up --build
```

O tópico/fila `integrar-credito-constituido-entry` é criado automaticamente: Kafka via auto-create de tópico, RabbitMQ via `CriadorDeFilas`, ServiceBus via `servicebus-emulator-config.json`. As chaves de cada provedor estão em [Configuração](#configuração).

Para rodar build e testes localmente (precisa do SDK .NET 6, versão pinada no `global.json`):

```bash
dotnet test
```

## Configuração

Tudo é configurável por `appsettings.json` ou por variável de ambiente (o compose usa as variáveis, que têm precedência). A chave aninhada `Mensageria:Provedor` vira a variável `Mensageria__Provedor` (separador `__`).

| Chave (`appsettings.json`) | Variável de ambiente | Padrão | O que é |
|---|---|---|---|
| `ConnectionStrings:Postgres` | `ConnectionStrings__Postgres` | `Host=postgres;Port=5432;Database=creditofiscal;Username=postgres;Password=postgres` | conexão do EF Core/Npgsql |
| `Mensageria:Provedor` | `Mensageria__Provedor` | `Kafka` | provedor ativo: `Kafka`, `RabbitMQ` ou `ServiceBus` |
| `Mensageria:Fila` | `Mensageria__Fila` | `integrar-credito-constituido-entry` | tópico/fila de integração |
| `Mensageria:MaxTentativasConsumer` | `Mensageria__MaxTentativasConsumer` | `5` | limite de tentativas antes do consumer encaminhar para DLQ |
| `Mensageria:IntervaloPollingMs` | `Mensageria__IntervaloPollingMs` | `500` | intervalo entre iteracoes do consumer (ms) |
| `Mensageria:Kafka:BootstrapServers` | `Mensageria__Kafka__BootstrapServers` | `kafka:9092` | brokers do Kafka |
| `Mensageria:Kafka:GrupoConsumidor` | `Mensageria__Kafka__GrupoConsumidor` | `credito-fiscal` | consumer group |
| `Mensageria:RabbitMQ:Host` | `Mensageria__RabbitMQ__Host` | `rabbitmq` | host do RabbitMQ |
| `Mensageria:RabbitMQ:Port` | `Mensageria__RabbitMQ__Port` | `5672` | porta AMQP |
| `Mensageria:RabbitMQ:Usuario` | `Mensageria__RabbitMQ__Usuario` | `guest` | usuário |
| `Mensageria:RabbitMQ:Senha` | `Mensageria__RabbitMQ__Senha` | `guest` | senha |
| `Mensageria:ServiceBus:ConnectionString` | `Mensageria__ServiceBus__ConnectionString` | `Endpoint=sb://servicebus-emulator;...;UseDevelopmentEmulator=true` | connection string do Service Bus |

No `docker-compose.yml` o provedor sai de `Mensageria__Provedor: "${MENSAGERIA_PROVEDOR:-Kafka}"`, então `MENSAGERIA_PROVEDOR=RabbitMQ docker compose ... up` troca o broker sem editar arquivo. A API também recebe `ASPNETCORE_URLS=http://+:8080`.

## Endpoints

Há uma **Postman Collection** com todos os requests (POST de sucesso e o inválido de 400, os dois GETs e os health checks, já com scripts de teste e a variável `baseUrl`) em [`docs/postman/CreditoFiscal.postman_collection.json`](docs/postman/CreditoFiscal.postman_collection.json).

### POST `/api/creditos/integrar-credito-constituido`
Recebe um **array** de créditos e publica cada um na fila. `simplesNacional` chega como texto `"Sim"`/`"Não"`.

```json
[
  {
    "numeroCredito": "123456",
    "numeroNfse": "7891011",
    "dataConstituicao": "2024-02-25",
    "valorIssqn": 1500.75,
    "tipoCredito": "ISSQN",
    "simplesNacional": "Sim",
    "aliquota": 5.0,
    "valorFaturado": 30000.00,
    "valorDeducao": 5000.00,
    "baseCalculo": 25000.00
  }
]
```

- **202** `{ "success": true }` quando aceito.
- **400** `ProblemDetails` se algum `simplesNacional` for diferente de `"Sim"`/`"Não"`, ou se a validação de modelo falhar (campo obrigatório vazio, valor fora de range). O lote inteiro é rejeitado e **nada** é publicado.

### GET `/api/creditos/{numeroNfse}`
Lista os créditos da NFS-e. **200** com array, **404** se não houver nenhum. Cada GET publica um evento `ConsultaCreditoRealizadaDto` no tópico/fila `consulta-credito-realizada` (auditoria, ver decisão técnica abaixo).

```json
[
  {
    "numeroCredito": "123456",
    "numeroNfse": "7891011",
    "dataConstituicao": "2024-02-25",
    "valorIssqn": 1500.75,
    "tipoCredito": "ISSQN",
    "simplesNacional": "Sim",
    "aliquota": 5.00,
    "valorFaturado": 30000.00,
    "valorDeducao": 5000.00,
    "baseCalculo": 25000.00
  }
]
```

### GET `/api/creditos/credito/{numeroCredito}`
Um crédito específico. **200** com o objeto (`simplesNacional` volta como `"Sim"`/`"Não"`), **404** se não existir. Também publica auditoria em `consulta-credito-realizada`.

```json
{
  "numeroCredito": "123456",
  "numeroNfse": "7891011",
  "dataConstituicao": "2024-02-25",
  "valorIssqn": 1500.75,
  "tipoCredito": "ISSQN",
  "simplesNacional": "Sim",
  "aliquota": 5.00,
  "valorFaturado": 30000.00,
  "valorDeducao": 5000.00,
  "baseCalculo": 25000.00
}
```

`dataConstituicao` sai como `"yyyy-MM-dd"` (sem fuso) e os decimais seguem a precisão do banco: valores monetários com 2 casas e `aliquota` `numeric(5,2)`.

### GET `/self` e `/ready`
`/self` = liveness (200 enquanto o processo está de pé). `/ready` = readiness (200 quando o Postgres **e** o broker configurado respondem; o broker pode ser Kafka, RabbitMQ ou ServiceBus). O check de mensageria é selecionado por `Mensageria:Provedor` em runtime.

## Decisões técnicas

Decisões com peso de longo prazo vivem como ADRs (formato Michael Nygard) em [`docs/adr/`](docs/adr/): multi broker com factory, idempotência em camadas, auditoria best effort, sem outbox, sem FluentValidation, max retry e DLQ por broker. Notas de engenharia complementares:

- **Pin de SDK (`global.json`).** Fixa o SDK 6.0.428. CI e local usam o mesmo Roslyn, evitando que `TreatWarningsAsErrors=true` quebre por um aviso novo de outra versão do compilador.
- **`SimplesNacional` fala três línguas.** `enum` no domínio (regra fiscal da LC 123/2006, afeta a apuração do ISSQN), `boolean` no banco (via `HasConversion`) e `"Sim"`/`"Não"` no JSON (via `ConversorSimplesNacional`, anti-corruption na borda).
- **RabbitMQ.Client 6.x é síncrono.** `BasicPublish`/`BasicGet` não são `async`. As abstrações retornam `Task` (com `Task.CompletedTask`) e o `CancellationToken` é checado entre chamadas AMQP (`ThrowIfCancellationRequested()`), porque o driver 6.x não aceita token nativo.
- **Thread-safety do publisher.** `IModel` (canal) **não** é thread-safe e vive como variável local em `PublicarAsync` (`using var canal = ...`); só a `IConnection` (thread-safe) é singleton compartilhado. Há teste de reflexão garantindo que o adapter não guarde `IModel` em campo.
- **Sessão de consumo.** O `DeliveryTag` do AMQP só vale no canal que recebeu a mensagem, então cada `RabbitMqConsumerSession<T>` é dona de um canal próprio (ack/nack no mesmo canal, `DisposeAsync` fecha e devolve o não-confirmado para a fila). `ReceivedMessage<T>` tem **identidade por referência**: duas entregas com conteúdo igual viram chaves distintas no dicionário `mensagem → DeliveryTag`; se fosse `record`, colidiriam e um tag ficaria órfão.
- **Conexão resiliente.** `IConnection` é criada com retry manual (6 tentativas, backoff 2/4/6/8/10s) e `AutomaticRecoveryEnabled = true`, porque o broker pode subir depois da API.
- **Data fiscal sem fuso.** `ConversorDeDataSemFusoHorario` lê só o dia (qualquer `Z`/offset que o cliente mande) e grava `Kind=Unspecified` — Npgsql 6 quebra ao escrever `DateTime` `Utc` em coluna `date`. Também serializa como `"yyyy-MM-dd"`.
- **Segregação de interface (ISP).** `IMensagemPublisher` e `IMensagemConsumer` são interfaces separadas (o controller só publica, o consumer só consome). Cada adapter implementa as duas; o DI registra a mesma instância singleton para ambas.
- **Middleware de erro.** Borda única: `ArgumentException` → **400** (`Warning`), demais → **500** (`Error` com log completo no servidor; o corpo nunca expõe stack trace nem detalhe interno).
- **Limites do POST de integração e shape do 400.** `[RequestSizeLimit(2_097_152)]` (2 MiB) barra payload abusivo antes do parsing; lotes acima de **1000 itens** voltam **400 `ValidationProblemDetails`** (`errors["creditos"]`). Acima do limite de body, o cliente vê **413 `ProblemDetails`** (branch dedicado para `BadHttpRequestException` no `ExcecoesMiddleware`) ou `connection reset/broken pipe` — sob HTTP/1.1 + Kestrel, o servidor pode abortar antes de drenar o body. O E2E cobre o count check; o 413 real fica como verificação manual (o `TestServer` do `WebApplicationFactory` não impõe `MaxRequestBodySize` como o Kestrel).
- **Forma das respostas de erro.** Dois envelopes, ambos em `application/problem+json`: `ValidationProblemDetails` para validação sintática (`[Required]`/`[Range]` no DTO e o count check; chaves do DTO em PascalCase, chave manual `creditos` em camelCase); `ProblemDetails` plain para falhas de domínio (`ArgumentException` → 400) e runtime (default → 500; `BadHttpRequestException` → status da exception).
- **Endurecimento parcial dos decimais.** `dataConstituicao` é `[Required]`. `valorIssqn`, `aliquota`, `valorFaturado`, `valorDeducao` e `baseCalculo` rejeitam negativos via `[Range(0, ...)]` mas aceitam zero — subir o piso (`[Range(0.01, ...)]`) exige decisão de domínio (`ValorDeducao = 0` é caso comum válido).
- **Architecture tests (NetArchTest).** Cinco regras em `RegrasDeArquiteturaTestes` mantêm as setas da clean architecture (`Api → {Aplicacao, Infraestrutura} → Dominio`), exigem controllers finos (sem `DbContext` nem `IConfiguration`) e casos de uso `sealed`. Roda no `dotnet test`, falha sem revisor.

## Idempotência e garantia de entrega

Entrega **at-least-once**: o `ack` (`ConfirmarAsync`) só acontece **depois** do commit no banco. Dois filtros evitam duplicata:

1. `ExisteAsync(numeroCredito)` antes de persistir → se já existe, confirma sem reprocessar.
2. **Índice único** em `numero_credito` no banco → numa corrida entre instâncias, o `SaveChanges` lança `DbUpdateException`, que também é tratada como duplicata (confirma).

Se cair entre persistir e confirmar, a reentrega cai no filtro 1 (ou no índice único). Falha real (banco fora) → `RejeitarAsync(reencaminhar: true)`, a mensagem volta pra fila e o broker incrementa o contador de tentativas; ao alcançar `Mensageria:MaxTentativasConsumer` (default 5), o consumer encaminha para DLQ em vez de reenfileirar (ver "Limitações conhecidas" para a semântica por broker). Um `try/catch` externo no `ExecuteAsync` impede que qualquer defeito de iteração derrube o host (no .NET 6, exceção que escapa de `ExecuteAsync` mata o processo).

**Publish parcial do lote.** O POST publica uma mensagem por crédito, em sequência (sem bulk). Se a publicação da mensagem `N` falhar (broker indisponível, timeout), as mensagens `1..N-1` já foram aceitas pelo broker e serão processadas. O cliente recebe **500** e o retry do lote inteiro é seguro: a idempotência por `numero_credito` cobre os já publicados sem duplicar. Trade-off consciente: at-least-once com retry idempotente, sem outbox.

## Testes

**Unitários** (`tests/CreditoFiscal.Testes`), 78 testes (xUnit + FluentAssertions + NSubstitute +
EF Core InMemory + NetArchTest), escritos em TDD (vermelho → verde) nas fases de lógica: domínio, repositório,
conversor, middleware, controllers, casos de uso, publicador de auditoria, o `CreditoConsumer`
(incluindo `DbUpdateException` e o gate de DLQ por `Tentativas`), os três adapters de mensageria
(`Tentativas` populado em cada broker, `EnviarParaDlqAsync` em cada implementação), o
`CriadorDeFilas` (declaração quorum + DLQ no RabbitMQ) e 5 regras de arquitetura (clean
architecture, controllers finos, casos de uso `sealed`).

```bash
dotnet test tests/CreditoFiscal.Testes/CreditoFiscal.Testes.csproj
```

**Integração** (`tests/CreditoFiscal.TestesIntegracao`) sobem Postgres e RabbitMQ reais via
**Testcontainers** e a API em processo (`WebApplicationFactory`), exercitando o fluxo completo
POST → fila → consumer → banco → GET, idempotência, entrada inválida (400) e readiness. Precisa de Docker.

```bash
dotnet test tests/CreditoFiscal.TestesIntegracao/CreditoFiscal.TestesIntegracao.csproj
```

**Mutation testing** (`stryker-config.json` na raiz) mede qualidade da suite além da cobertura simples: o Stryker muta o código (inverte operadores, remove blocos, etc.) e verifica se algum teste falha. Score baixo aponta testes que executam código sem detectar defeitos. Config foca em `CreditoFiscal.Aplicacao` (onde a lógica de caso de uso vive); não roda no CI por padrão (custo de tempo). Para rodar local:

```bash
dotnet tool install -g dotnet-stryker
dotnet stryker
```

O relatório HTML sai em `StrykerOutput/<data>/reports/mutation-report.html`.

## CI

`.github/workflows/ci.yml` roda em todo `push`/`pull_request` para `main` e `develop`, com o SDK do
`global.json`, em dois jobs:

- **build-e-teste** roda `restore` + `build -c Release` + os testes unitários (não precisa de Docker). Coleta cobertura via `coverlet.collector` (`--collect:"XPlat Code Coverage"`), converte com `ReportGenerator` para HTML e publica o relatório como artefato `cobertura-html` na execução do workflow. O resumo (`Summary.txt`) é impresso no log do job para olhar rápido em PR.
- **integracao** roda os testes de integração com Testcontainers (o runner `ubuntu-latest` já tem Docker).

Complementam o pipeline:

- `.github/workflows/codeql.yml`: análise CodeQL semanal (segunda 07:00 UTC) e em todo push/PR para `main` ou `develop`, focada em C#.
- `.github/dependabot.yml`: atualizações automáticas semanais para NuGet, GitHub Actions e Docker. Cada ecossistema abre até 5 PRs simultâneos.

## Validação executada (stack real)

A stack foi subida em WSL2 + Docker, no perfil **RabbitMQ** (`MENSAGERIA_PROVEDOR=RabbitMQ docker compose --profile rabbitmq up --build`), escolhido pelo painel de gerência que ajuda a inspecionar fila e mensagens.

- `/self` e `/ready` → **200** (Postgres e RabbitMQ alcançáveis).
- `POST` de um lote → **202**; o consumer persistiu; `GET` por NFS-e e por número → **200** com os dados.
- Reenvio do mesmo lote → continua 1 linha por crédito; o log do consumer mostra `... ja existe; duplicata ignorada`.
- `POST` com `simplesNacional` inválido → **400** (`ProblemDetails`).
- **k6** nos três cenários, com métricas em [`docs/carga/resultados.md`](docs/carga/resultados.md): pico 214 req/s, leitura 1.361 req/s, ambos 0% de falha. Números do RabbitMQ; ver [Limitações conhecidas](#limitações-conhecidas) sobre o Kafka.

## Convenções

- **Nomenclatura.** Radical em PT-BR + sufixo de pattern canônico do .NET em inglês quando aplicável (`CreditoRepository`, `CreditoFiscalDbContext`, `ExcecoesMiddleware`, `CreditoConsumer`); mensageria em inglês (`IMensagemPublisher`, `IConsumerSession`, `ReceivedMessage`); papéis de domínio e fiscais em PT (`ConversorSimplesNacional`, `CriadorDeFilas`, `UnidadeDeTrabalho`).
- **Git.** `main`/`develop`/`feature/*`, cada fase revisada e mergeada com `--no-ff`.

## Limitações conhecidas

- O teste de carga foi rodado no perfil **RabbitMQ**, não no Kafka padrão (resultados em [Validação executada](#validação-executada-stack-real)). O comportamento qualitativo (POST aceita 100%, consumer é o gargalo) é o mesmo, mas os números absolutos de throughput variam para Kafka. Re-rodar é um passo simples: `MENSAGERIA_PROVEDOR=Kafka docker compose up --build` e executar os mesmos cenários k6.
- O round-trip real de **ServiceBus** depende de subir o emulator (`--profile servicebus`). Os adapters têm cobertura de unit test (factory + thread-safety), mas o teste de integração com Testcontainers usa RabbitMQ por ser o broker mais leve para subir em CI.
- Propagação de `CancellationToken` dentro de uma chamada AMQP única não é possível no driver síncrono RabbitMQ 6.x (checado entre chamadas).
- Sob stress extremo (500 VUs no k6 com RabbitMQ), o POST não falha, mas a latência cresce muito: o consumer drena ~20 msg/s, então o broker vira o gargalo de vazão (ver `docs/carga/resultados.md`).
- **Poison message no consumer.** Tratado: o `CreditoConsumer` encaminha para DLQ explícita quando `mensagem.Tentativas` alcança `Mensageria:MaxTentativasConsumer` (default 5). A semântica de "tentativas" é assimétrica por broker, e o README expõe isso por escolha consciente:
  - **Service Bus.** `ServiceBusReceivedMessage.DeliveryCount`, *system property* que o broker incrementa a cada `AbandonMessageAsync` ou expiração de lock. A fila ainda tem `MaxDeliveryCount: 10` no `servicebus-emulator-config.json` como segunda barreira automática.
  - **RabbitMQ.** Fila migrada para **quorum** com `x-delivery-limit: 10`, `x-dead-letter-exchange` e `x-dead-letter-routing-key` na declaração (ver `CriadorDeFilas`). O broker incrementa `x-delivery-count` em cada redelivery; a sessão lê esse header e popula `Tentativas` em base 1. A DLQ `integrar-credito-constituido-entry-dlq` é *classic durable*, declarada pelo mesmo `CriadorDeFilas` antes da principal.
  - **Kafka.** Sem contador no protocolo: `AdaptadorKafka` mantém `RegistroDeTentativasKafka` em memória, indexado por `TopicPartitionOffset` com `lock`. Limitação aceita: em restart do processo ou rebalance do *consumer group*, o contador zera e o ciclo de tentativas recomeça; em poison loop o limite vira "5 tentativas por instância de consumer", não "5 absolutas". A DLQ é publicada no topic `integrar-credito-constituido-entry-dlq` (auto-criado pelo broker porque `KAFKA_AUTO_CREATE_TOPICS_ENABLE: "true"`; em produção real, declarar o topic seria boa prática).
- **Auditoria com teto curto.** O `PublicadorAuditoria` envolve o publish num `CancellationTokenSource` linkado ao `ct` do caller com `CancelAfter(200ms)`. Broker lento estoura o teto local e o GET responde sem amarrar — o estouro vira `LogWarning` distinto do erro genérico. Cancelamento real do caller (cliente fechou a conexão) cai no catch padrão sem propagar.
