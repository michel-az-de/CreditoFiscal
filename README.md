# CreditoFiscal

Microsserviço .NET 6 para consulta de créditos fiscais constituídos (ISSQN / NFS-e).
A API recebe créditos, publica cada um numa fila do RabbitMQ; um `BackgroundService`
consome a fila e persiste no PostgreSQL de forma **idempotente**; dois `GET`s consultam
os créditos por NFS-e e por número de crédito.

## Fluxo

```
POST /api/creditos/integrar-credito-constituido
        │  (valida e publica 1 mensagem por crédito)
        ▼
   RabbitMQ  ──fila: integrar-credito-constituido-entry──►  CreditoConsumer (BackgroundService)
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
| `CreditoFiscal.Aplicacao` | Casos de uso (`IIntegrarCreditos`, `IConsultarCreditosPorNfse`, `IConsultarCreditoPorNumero`), DTOs de entrada/saída, conversor de `SimplesNacional` e contratos de mensagem. Depende só do domínio — é a camada reutilizável (DLL própria). |
| `CreditoFiscal.Infraestrutura` | EF Core + Npgsql (`CreditoFiscalDbContext`, `CreditoRepository`, `UnidadeDeTrabalho`), mensageria (adapters RabbitMQ / Kafka / ServiceBus + sessões de consumo), conversor de data, extensões de DI. |
| `CreditoFiscal.Api` | Controllers (finos), middlewares (erro + correlation id), `CreditoConsumer`, health checks, OpenTelemetry, Swagger, composição (`Program.cs`). |

Padrões usados: **Repository** + **Unit of Work**, **Casos de uso** (camada de aplicação reutilizável), **Factory** (extension `AdicionarMensageria` escolhe o provedor), **Adapter** (RabbitMQ / Kafka / ServiceBus), **Anti-corruption layer** (`ConversorSimplesNacional`), **Session** descartável para consumo (`IConsumerSession<T>`), **Middleware**, **BackgroundService**.

## Como rodar

Pré-requisito: Docker.

```bash
docker compose up --build
```

Sobe Postgres, RabbitMQ e a API. A API espera o Postgres ficar saudável (barreira para aplicar as
migrations no startup) e resolve a conexão com o broker via retry. Fica disponível em:

- API: `http://localhost:8080`
- Swagger: `http://localhost:8080/swagger`
- Painel RabbitMQ: `http://localhost:15672` (guest / guest)

### Trocar de provedor de mensageria

O provedor é escolhido por configuração (`Mensageria:Provedor`). Os adapters de **Kafka** e **ServiceBus** sobem em *profiles* do compose:

```bash
# Kafka (broker local em KRaft)
MENSAGERIA_PROVEDOR=Kafka docker compose --profile kafka up --build

# ServiceBus (emulator oficial + SQL de apoio)
MENSAGERIA_PROVEDOR=ServiceBus docker compose --profile servicebus up --build
```

A config de cada provedor fica aninhada no `appsettings.json` (`Mensageria:RabbitMQ`, `Mensageria:Kafka`, `Mensageria:ServiceBus`) e o compose sobrepõe por variável de ambiente. A fila `integrar-credito-constituido-entry` é criada automaticamente: RabbitMQ via `CriadorDeFilas`, Kafka via auto-create de tópico, ServiceBus via `servicebus-emulator-config.json`.

Para rodar build e testes localmente (precisa do SDK .NET 6 — versão pinada no `global.json`):

```bash
dotnet test
```

## Endpoints

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
- **400** `ProblemDetails` se algum `simplesNacional` for diferente de `"Sim"`/`"Não"` — o lote inteiro é rejeitado e **nada** é publicado.

### GET `/api/creditos/{numeroNfse}`
Lista os créditos da NFS-e. **200** com array, **404** se não houver nenhum.

### GET `/api/creditos/credito/{numeroCredito}`
Um crédito específico. **200** com o objeto (`simplesNacional` volta como `"Sim"`/`"Não"`), **404** se não existir.

### GET `/self` e `/ready`
`/self` = liveness (200 enquanto o processo está de pé). `/ready` = readiness (200 quando Postgres **e** RabbitMQ respondem).

## Decisões técnicas

- **Pin de SDK (`global.json`)** — fixa o SDK 6.0.428. CI e local usam o mesmo Roslyn, evitando que `TreatWarningsAsErrors=true` quebre por um aviso novo de outra versão do compilador.
- **`SimplesNacional` fala três línguas** — `enum` no domínio (regra fiscal da LC 123/2006, afeta a apuração do ISSQN), `boolean` no banco (como o enunciado pede, via `HasConversion`) e `"Sim"`/`"Não"` no JSON (via `ConversorSimplesNacional`, anti-corruption na borda).
- **RabbitMQ.Client 6.x é síncrono** — `BasicPublish`/`BasicGet` não são `async`. Os métodos das abstrações retornam `Task` mas usam `Task.CompletedTask`; o `CancellationToken` é checado com `ThrowIfCancellationRequested()` entre chamadas AMQP (o driver 6.x não aceita token nativo).
- **Thread-safety do publisher** — `IModel` (canal) **não** é thread-safe e é sempre variável local em `PublicarAsync` (`using var canal = ...`); só a `IConnection` (thread-safe) é compartilhada no singleton. Há teste de regressão por reflexão garantindo que o adapter não guarde `IModel` em campo.
- **Sessão de consumo** — o `DeliveryTag` do AMQP só vale no canal que recebeu a mensagem, então cada sessão (`RabbitMqConsumerSession<T>`) é dona de um canal próprio: `BasicGet` → `BasicAck`/`BasicNack` acontecem no mesmo canal, e o `DisposeAsync` fecha o canal (o que não recebeu ack volta pra fila). `ReceivedMessage<T>` é `sealed class` com **identidade por referência** — duas entregas com conteúdo igual são chaves distintas no dicionário `mensagem → DeliveryTag` (se fosse `record`, colidiriam e um tag ficaria órfão, derrubando o host).
- **Conexão resiliente** — `IConnection` é singleton criado com retry manual (6 tentativas, backoff 2/4/6/8/10s) e `AutomaticRecoveryEnabled = true`, porque o broker pode subir depois da API.
- **Data fiscal sem fuso** — `ConversorDeDataSemFusoHorario` lê só o dia (qualquer `Z`/offset que o cliente mande) e grava `Kind=Unspecified`, porque o Npgsql 6 quebra ao escrever `DateTime` `Utc` em coluna `date`. Também serializa como `"yyyy-MM-dd"`.
- **Segregação de interface (ISP)** — em vez de um `IBarramentoDeMensagens` único, há `IMensagemPublisher` e `IMensagemConsumer` separados (o controller só publica, o consumer só consome). Cada adapter implementa as duas, e o DI registra **a mesma instância singleton** para ambas.
- **Múltiplos provedores via Factory (OCP)** — `AdicionarMensageria` escolhe o adapter por config (`Mensageria:Provedor`): **RabbitMQ** (padrão), **Kafka** (`AdaptadorKafka`) ou **ServiceBus** (`AdaptadorServiceBus`). Trocar de broker é mudar uma string — sem tocar em controller nem consumer. *Caveat honesto:* o modelo de streaming/offset do Kafka não tem ack/requeue por mensagem como AMQP/ServiceBus; no `AdaptadorKafka`, `Confirmar` faz commit de offset e `Rejeitar` faz `seek` de volta (re-leitura), documentado no código.
- **Unit of Work** — o consumer depende só de abstrações do domínio (`ICreditoRepository` + `IUnidadeDeTrabalho`), sem tocar o `DbContext` concreto. Mantém o consumer testável e nas camadas certas.
- **Camada de aplicação (projeto próprio)** — os casos de uso vivem em `CreditoFiscal.Aplicacao`, uma DLL que depende só do domínio. Os controllers apenas traduzem HTTP e delegam; nenhum controller fala direto com repositório ou publisher. Camada reutilizável fora da API.
- **Middleware de erro** — borda única: `ArgumentException` → **400** (`Warning`), demais → **500** (`Error` com log completo no servidor, mas o corpo nunca expõe stack trace nem detalhe interno).

## Idempotência e garantia de entrega

Entrega **at-least-once**: o `ack` (`ConfirmarAsync`) só acontece **depois** do commit no banco. Dois filtros evitam duplicata:

1. `ExisteAsync(numeroCredito)` antes de persistir → se já existe, confirma sem reprocessar.
2. **Índice único** em `numero_credito` no banco → numa corrida entre instâncias, o `SaveChanges` lança `DbUpdateException`, que também é tratada como duplicata (confirma).

Se cair entre persistir e confirmar, a reentrega cai no filtro 1 (ou no índice único). Falha real (banco fora) → `RejeitarAsync(reencaminhar: true)`, a mensagem volta pra fila. Um `try/catch` externo no `ExecuteAsync` impede que qualquer defeito de iteração derrube o host (no .NET 6, exceção que escapa de `ExecuteAsync` mata o processo).

## Testes

**Unitários** (`tests/CreditoFiscal.Testes`) — 53 testes (xUnit + FluentAssertions + NSubstitute +
EF Core InMemory), escritos em TDD (vermelho → verde) nas fases de lógica: domínio, repositório,
conversor, middleware, controllers, casos de uso e o `CreditoConsumer` (incluindo `DbUpdateException`).

```bash
dotnet test tests/CreditoFiscal.Testes/CreditoFiscal.Testes.csproj
```

**Integração** (`tests/CreditoFiscal.TestesIntegracao`) — sobem Postgres e RabbitMQ reais via
**Testcontainers** e a API em processo (`WebApplicationFactory`), exercitando o fluxo completo
POST → fila → consumer → banco → GET, idempotência, entrada inválida (400) e readiness. Precisa de Docker.

```bash
dotnet test tests/CreditoFiscal.TestesIntegracao/CreditoFiscal.TestesIntegracao.csproj
```

## CI

`.github/workflows/ci.yml` roda em todo `push`/`pull_request` para `main` e `develop`, com o SDK do
`global.json`, em dois jobs:

- **build-e-teste** — `restore` + `build -c Release` + testes unitários (não precisa de Docker).
- **integracao** — testes de integração com Testcontainers (o runner `ubuntu-latest` já tem Docker).

## Validação executada (stack real)

A stack foi subida de verdade (`docker compose up --build`, em WSL2 + Docker 29) e o fluxo
ponta-a-ponta foi exercitado:

1. `/self` e `/ready` → **200** (Postgres e RabbitMQ alcançáveis).
2. `POST` de um lote → **202**; o consumer persistiu; `GET` por NFS-e e por número → **200** com os dados.
3. **Reenvio do mesmo lote** → continua 1 linha por crédito; o log do consumer mostra `... ja existe; duplicata ignorada`.
4. `POST` com `simplesNacional` inválido → **400** (ProblemDetails).
5. **k6** nos três cenários, com métricas reais em `docs/carga/resultados.md` (pico 214 req/s, leitura 1.361 req/s, ambos 0% de falha).

## Convenções

- **Nomenclatura** — radical em PT-BR + sufixo de pattern canônico do .NET em inglês quando aplicável (`CreditoRepository`, `CreditoFiscalDbContext`, `ExcecoesMiddleware`, `CreditoConsumer`); mensageria em inglês (`IMensagemPublisher`, `IConsumerSession`, `ReceivedMessage`); papéis de domínio e fiscais em PT (`ConversorSimplesNacional`, `CriadorDeFilas`, `UnidadeDeTrabalho`).
- **Git** — `main`/`develop`/`feature/*`, cada fase revisada e mergeada com `--no-ff`.

## Limitações conhecidas

- Apenas o provedor **RabbitMQ** foi validado ponta-a-ponta com a stack real. **Kafka** e **ServiceBus** têm os adapters e a seleção por config testados, mas o round-trip real depende de subir o broker correspondente (`--profile kafka` / `--profile servicebus`). O health check `/ready` está cabeado para o RabbitMQ; trocar o provedor pede ajustar o check.
- Propagação de `CancellationToken` dentro de uma chamada AMQP única não é possível no driver síncrono 6.x (checado entre chamadas).
- Sob stress extremo (500 VUs no k6) o POST não falha, mas a latência cresce muito: o consumer drena ~20 msg/s, então a fila vira o gargalo de vazão (ver `docs/carga/resultados.md`).
