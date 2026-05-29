# Resultados dos testes de carga

> **Regra de honestidade:** as tabelas abaixo foram preenchidas com dados de **execução real**.
> Nenhum número foi inventado — todos vêm do resumo que o k6 imprime ao final de cada cenário.

## Ambiente de execução

| Item | Valor |
|------|-------|
| Data da execução | 2026-05-29 |
| Plataforma | WSL2 Ubuntu 24.04, Docker 29.1.3 |
| CPU / RAM | 14 vCPU / 15 GiB |
| Versão do k6 | v2.0.0 (imagem `grafana/k6:latest`) |
| Stack | API (1 réplica) + Postgres 15-alpine + RabbitMQ 3-management |
| Vazão observada do consumer | ~20 msg/s (150 mensagens drenadas em ~9s; polling 500ms × lote 10) |

## Cenário 1 — Pico (50 VUs, rampa 30s + 2min + 30s)

| Métrica | Valor |
|---------|-------|
| Requisições totais | 38.524 |
| Throughput | 214 req/s |
| http_req_duration (avg / med / p95) | 194,6ms / 172,3ms / 256,5ms |
| http_req_duration (max) | 14,54s |
| http_req_failed | 0,00% (0 de 38.524) |
| Checks (status 202) | 100,00% |

Cada iteração publica 3 créditos, então o POST sustentou ~642 créditos/s na fila sem nenhuma falha.

## Cenário 2 — Leitura (100 VUs, 1min)

| Métrica | Valor |
|---------|-------|
| Requisições totais | 81.749 |
| Throughput | 1.361 req/s |
| http_req_duration (avg / med / p95) | 73,0ms / 67,3ms / 127,6ms |
| http_req_duration (max) | 547,3ms |
| http_req_failed (threshold < 0,5%) | 0,00% — threshold **OK** |
| Checks (status 200) | 100,00% |

As 50 chaves de leitura foram semeadas e drenadas antes do cenário (ver "Como executar"), então nenhum GET bateu em chave inexistente.

## Cenário 3 — Stress (até 500 VUs, rampa 5min + 1min + 1min)

| Métrica | Valor |
|---------|-------|
| Requisições totais | 24.490 |
| Throughput | 58 req/s |
| http_req_duration (avg / med / p95) | 4,91s / 929ms / 31,88s |
| http_req_duration (max) | 57,24s |
| http_req_failed | 0,00% (0 de 24.490) |
| Checks (status 202) | 100,00% |
| Ponto de saturação observado | a partir de ~150-200 VUs a latência dispara (p95 31,9s), mas a API segue aceitando 100% dos POSTs |

Leitura do stress: o endpoint de escrita **não derrubou nem rejeitou** nenhuma requisição mesmo a 500 VUs — o gargalo aparece como latência crescente (backpressure ao publicar no RabbitMQ), não como erro. Throughput de POST estabiliza em ~58 req/s sob concorrência extrema.

## Como executar

1. Subir a stack na raiz do projeto:
   ```
   docker compose up --build -d
   ```
2. Cenário 1 (pico):
   ```
   docker compose -f carga/docker-compose.carga.yml run --rm k6-pico
   ```
3. **Drenar a fila antes do cenário 2** — o consumidor persiste a ~20 msg/s, então as escritas do
   cenário 1 ainda estão na fila quando ele termina. Sem esperar, os GETs do cenário 2 batem em
   chaves não persistidas e retornam 404. Verifique a fila zerada:
   ```
   docker exec credito-fiscal-rabbitmq rabbitmqctl list_queues name messages | grep integrar
   ```
   (Nesta execução as 50 chaves de leitura foram semeadas e drenadas diretamente, ao invés de
   esperar o backlog gigante do cenário 1 — o cenário 2 só consulta `nfse-1..50` e `carga-N-1-1`.)
4. Cenário 2 (leitura) e cenário 3 (stress):
   ```
   docker compose -f carga/docker-compose.carga.yml run --rm k6-leitura
   docker compose -f carga/docker-compose.carga.yml run --rm k6-stress
   ```
5. O k6 imprime o resumo de cada cenário ao final — foi dele que as tabelas acima foram preenchidas.
