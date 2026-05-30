# CreditoFiscal

[![CI](https://github.com/michel-az-de/CreditoFiscal/actions/workflows/ci.yml/badge.svg)](https://github.com/michel-az-de/CreditoFiscal/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-6.0-512BD4)
[![Licença: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-blue)](LICENSE)

Microsserviço .NET 6 para consulta de créditos fiscais constituídos (ISSQN / NFS-e).
A API recebe créditos, publica cada um num tópico do broker configurado (Kafka por padrão,
com RabbitMQ e Azure Service Bus como alternativas); um `BackgroundService` consome o tópico
e persiste no PostgreSQL de forma **idempotente**; dois `GET`s consultam os créditos por
NFS-e e por número de crédito.

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

```
POST /api/creditos/integrar-credito-constituido
        │  (valida e publica 1 mensagem por crédito)
        ▼
   Kafka    ──tópico: integrar-credito-constituido-entry──►  CreditoConsumer (BackgroundService)
                                                                 │ polling 500ms, lote de 10
                                                                 │ idempotência (ver abaixo)
                                                                 ▼
                                                            PostgreSQL
                                                                 ▲
        GET /api/creditos/{numeroNfse}            ────────────────┘
        GET /api/creditos/credito/{numeroCredito}
```

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

<details>
<summary>Decisões de engenharia (clique para expandir): SimplesNacional em três línguas, driver síncrono, thread-safety do publisher, sessão de consumo, idempotência, ISP, factory de provedores, auditoria de consultas e mais.</summary>

- **Pin de SDK (`global.json`).** Fixa o SDK 6.0.428. CI e local usam o mesmo Roslyn, evitando que `TreatWarningsAsErrors=true` quebre por um aviso novo de outra versão do compilador.
- **`SimplesNacional` fala três línguas.** `enum` no domínio (regra fiscal da LC 123/2006, afeta a apuração do ISSQN), `boolean` no banco (como o enunciado pede, via `HasConversion`) e `"Sim"`/`"Não"` no JSON (via `ConversorSimplesNacional`, anti-corruption na borda).
- **RabbitMQ.Client 6.x é síncrono.** `BasicPublish`/`BasicGet` não são `async`. Os métodos das abstrações retornam `Task` mas usam `Task.CompletedTask`; o `CancellationToken` é checado com `ThrowIfCancellationRequested()` entre chamadas AMQP (o driver 6.x não aceita token nativo).
- **Thread-safety do publisher.** `IModel` (canal) **não** é thread-safe e é sempre variável local em `PublicarAsync` (`using var canal = ...`); só a `IConnection` (thread-safe) é compartilhada no singleton. Há teste de regressão por reflexão garantindo que o adapter não guarde `IModel` em campo.
- **Sessão de consumo.** O `DeliveryTag` do AMQP só vale no canal que recebeu a mensagem, então cada sessão (`RabbitMqConsumerSession<T>`) é dona de um canal próprio: `BasicGet` → `BasicAck`/`BasicNack` acontecem no mesmo canal, e o `DisposeAsync` fecha o canal (o que não recebeu ack volta pra fila). `ReceivedMessage<T>` é `sealed class` com **identidade por referência**: duas entregas com conteúdo igual são chaves distintas no dicionário `mensagem → DeliveryTag` (se fosse `record`, colidiriam e um tag ficaria órfão, derrubando o host).
- **Conexão resiliente.** `IConnection` é singleton criado com retry manual (6 tentativas, backoff 2/4/6/8/10s) e `AutomaticRecoveryEnabled = true`, porque o broker pode subir depois da API.
- **Data fiscal sem fuso.** `ConversorDeDataSemFusoHorario` lê só o dia (qualquer `Z`/offset que o cliente mande) e grava `Kind=Unspecified`, porque o Npgsql 6 quebra ao escrever `DateTime` `Utc` em coluna `date`. Também serializa como `"yyyy-MM-dd"`.
- **Segregação de interface (ISP).** Em vez de um `IBarramentoDeMensagens` único, há `IMensagemPublisher` e `IMensagemConsumer` separados (o controller só publica, o consumer só consome). Cada adapter implementa as duas, e o DI registra **a mesma instância singleton** para ambas.
- **Múltiplos provedores via Factory (OCP).** `AdicionarMensageria` escolhe o adapter por config (`Mensageria:Provedor`): **Kafka** (padrão, `AdaptadorKafka`), **RabbitMQ** (`AdaptadorRabbitMq`) ou **ServiceBus** (`AdaptadorServiceBus`). Trocar de broker é mudar uma string, sem tocar em controller nem consumer. O health check de `/ready` também segue a config (ver `MensageriaHealthChecksExtensions`), então `dotnet test` e `docker compose --profile rabbitmq` continuam saudáveis sem ajuste manual. *Caveat honesto:* o modelo de streaming/offset do Kafka não tem ack/requeue por mensagem como AMQP/ServiceBus; no `AdaptadorKafka`, `Confirmar` faz commit de offset e `Rejeitar` faz `seek` de volta (re-leitura), documentado no código.
- **Unit of Work.** O consumer depende só de abstrações do domínio (`ICreditoRepository` + `IUnidadeDeTrabalho`), sem tocar o `DbContext` concreto. Mantém o consumer testável e nas camadas certas.
- **Camada de aplicação (projeto próprio).** Os casos de uso vivem em `CreditoFiscal.Aplicacao`, uma DLL que depende só do domínio. Os controllers apenas traduzem HTTP e delegam; nenhum controller fala direto com repositório ou publisher. Camada reutilizável fora da API.
- **Middleware de erro.** Borda única: `ArgumentException` → **400** (`Warning`), demais → **500** (`Error` com log completo no servidor, mas o corpo nunca expõe stack trace nem detalhe interno).
- **Publisher de auditoria de consultas (desafio extra).** Cada GET (`PorNfse` ou `PorNumero`) publica um `ConsultaCreditoRealizadaDto` (tipo + chave + quantidade retornada + timestamp UTC) no tópico/fila `consulta-credito-realizada`. Comportamento **best-effort engole-e-loga**: o `PublicadorAuditoria` espera o publish completar dentro do `try` e captura `Exception` para logar `Warning` sem propagar. **Auditoria nunca derruba a consulta**, mas:
  - **Broker fora** (conexão recusada): falha rápida, log e segue, o GET responde rápido.
  - **Broker lento / em backpressure**: o GET soma a latência/timeout do publish (assumindo timeout configurado no cliente do broker; sem timeout, pode bloquear até nível de socket). Modo de falha aditivo conhecido.
  Reutiliza o mesmo `IMensagemPublisher` da integração, então a auditoria sai pelo broker selecionado em `Mensageria:Provedor`.
- **Limites do POST de integração e shape do 400.** O action tem `[RequestSizeLimit(2_097_152)]` (2 MiB) como defesa de transporte e rejeita lotes com mais de **1000 itens** com `400 ValidationProblemDetails` (`errors["creditos"]`). Acima do limite de body, o cliente vê **413 `ProblemDetails`** (capturado por um branch dedicado para `BadHttpRequestException` no `ExcecoesMiddleware`) **ou** `connection reset/broken pipe`: sob HTTP/1.1 + Kestrel, o servidor pode abortar a conexão antes de drenar o body, e ambos os resultados observáveis indicam o limite atuando. O E2E cobre o count check (`400`); o 413 real fica como verificação manual (`docker compose up` + curl com body grande), porque o `TestServer` in-memory do `WebApplicationFactory` não impõe `MaxRequestBodySize` da mesma forma que o Kestrel.
- **Forma das respostas de erro.** A API emite dois envelopes, ambos em `application/problem+json`:
  - **`ValidationProblemDetails`** para validação sintática: `[Required]`/`[Range]` no DTO e o count check do lote. Caminho automático do `[ApiController]` e o count check (manual) produzem `status: 400` no corpo, `errors` populado e mesmo content-type; o manual difere apenas em `type`/`traceId` (não passa pela `ProblemDetailsFactory`). Convenção estrutural do ASP.NET: chaves de propriedades do DTO saem em **PascalCase** (`DataConstituicao`); chaves adicionadas manualmente (parâmetro do action) saem em **camelCase** (`creditos`). String malformada de data (ex.: `"abc"`) também cai em `ValidationProblemDetails`, mas pode aparecer com chave `$` (root path) e mensagem genérica do framework, porque o `FormatException` do converter perde o path da propriedade.
  - **`ProblemDetails` plain** para falhas de domínio (`ArgumentException` → 400) e runtime capturadas no `ExcecoesMiddleware` (default → 500; `BadHttpRequestException` → status da exception, ex.: 413).
- **Endurecimento parcial.** `dataConstituicao` é `[Required]` (`DateTime?` no DTO). Os decimais (`valorIssqn`, `aliquota`, `valorFaturado`, `valorDeducao`, `baseCalculo`) **continuam com `[Range(0, ...)]`**: rejeitam **negativos**, mas **aceitam zero**. Subir o piso semântico (ex.: `[Range(0.01, ...)]` onde fizer sentido fiscal) exige decisão de domínio (`ValorDeducao = 0` é caso comum válido) e fica como trabalho futuro.
- **Max retry e DLQ por broker.** `ReceivedMessage<T>.Tentativas` é a contagem 1-based exposta no envelope. Cada adapter popula a partir do que o broker oferece: `DeliveryCount` no Service Bus, `x-delivery-count + 1` em fila quorum no RabbitMQ, `RegistroDeTentativasKafka` em memória no Kafka. O `CreditoConsumer` decide DLQ quando `Tentativas >= Mensageria:MaxTentativasConsumer` (default 5, configurável). O destino da DLQ é assimétrico: sub-queue nativa `<fila>/$DeadLetterQueue` no Service Bus, fila *classic durable* `<fila>-dlq` no RabbitMQ (declarada pelo `CriadorDeFilas`), topic `<fila>-dlq` no Kafka (auto-criado pelo broker em dev). Detalhes por broker e *trade-offs* na seção "Limitações conhecidas".
- **Architecture tests (NetArchTest).** Cinco regras automatizadas em `RegrasDeArquiteturaTestes` garantem que as setas de dependência da clean architecture continuem apontando para o domínio (`Api → {Aplicacao, Infraestrutura} → Dominio`), que controllers fiquem finos (sem `DbContext`, `IConfiguration` ou tipos de Infraestrutura) e que casos de uso sejam `sealed`. Pegado em CI no `dotnet test`, falha sem precisar revisor humano.

</details>

## Idempotência e garantia de entrega

Entrega **at-least-once**: o `ack` (`ConfirmarAsync`) só acontece **depois** do commit no banco. Dois filtros evitam duplicata:

1. `ExisteAsync(numeroCredito)` antes de persistir → se já existe, confirma sem reprocessar.
2. **Índice único** em `numero_credito` no banco → numa corrida entre instâncias, o `SaveChanges` lança `DbUpdateException`, que também é tratada como duplicata (confirma).

Se cair entre persistir e confirmar, a reentrega cai no filtro 1 (ou no índice único). Falha real (banco fora) → `RejeitarAsync(reencaminhar: true)`, a mensagem volta pra fila e o broker incrementa o contador de tentativas; ao alcançar `Mensageria:MaxTentativasConsumer` (default 5), o consumer encaminha para DLQ em vez de reenfileirar (ver "Limitações conhecidas" para a semântica por broker). Um `try/catch` externo no `ExecuteAsync` impede que qualquer defeito de iteração derrube o host (no .NET 6, exceção que escapa de `ExecuteAsync` mata o processo).

**Publish parcial do lote.** O POST publica uma mensagem por crédito, em sequência (sem bulk, como o enunciado pede). Se a publicação da mensagem `N` falhar (broker indisponível, timeout), as mensagens `1..N-1` já foram aceitas pelo broker e serão processadas. O cliente recebe **500** e o retry do lote inteiro é seguro: a idempotência por `numero_credito` cobre os já publicados sem duplicar. Trade-off consciente: at-least-once com retry idempotente, sem outbox.

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

## CI

`.github/workflows/ci.yml` roda em todo `push`/`pull_request` para `main` e `develop`, com o SDK do
`global.json`, em dois jobs:

- **build-e-teste** roda `restore` + `build -c Release` + os testes unitários (não precisa de Docker).
- **integracao** roda os testes de integração com Testcontainers (o runner `ubuntu-latest` já tem Docker).

## Validação executada (stack real)

<details>
<summary>A stack foi subida de verdade (WSL2 + Docker) e o fluxo ponta-a-ponta foi exercitado, incluindo k6. Clique para expandir.</summary>

Rodado no perfil **RabbitMQ** (`MENSAGERIA_PROVEDOR=RabbitMQ docker compose --profile rabbitmq up --build`), escolhido pelo painel de gerência que ajuda a inspecionar fila e mensagens durante o teste:

1. `/self` e `/ready` → **200** (Postgres e RabbitMQ alcançáveis).
2. `POST` de um lote → **202**; o consumer persistiu; `GET` por NFS-e e por número → **200** com os dados.
3. **Reenvio do mesmo lote** → continua 1 linha por crédito; o log do consumer mostra `... ja existe; duplicata ignorada`.
4. `POST` com `simplesNacional` inválido → **400** (ProblemDetails).
5. **k6** nos três cenários, com métricas reais em [`docs/carga/resultados.md`](docs/carga/resultados.md): pico 214 req/s, leitura 1.361 req/s, ambos 0% de falha. Os números são do RabbitMQ; ver [Limitações conhecidas](#limitações-conhecidas) sobre o Kafka.

</details>

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
- **Auditoria síncrona.** O `PublicadorAuditoria` engole exceções mas espera o publish completar (ver Decisões técnicas). Para brokers lentos sem timeout configurado no cliente, o GET pode bloquear; trabalho futuro é envolver o publish em `CancellationTokenSource` com timeout curto ou disparar em `Task.Run` fora da thread da consulta.
