# Resultados dos testes de carga

> **Regra de honestidade:** as tabelas abaixo só são preenchidas com dados de **execução real**.
> Nesta entrega os cenários estão marcados como **NAO EXECUTADO** — nenhum número foi inventado.

## Ambiente de execução

| Item | Valor |
|------|-------|
| Data da execução | NAO EXECUTADO |
| Máquina / CPU / RAM | NAO EXECUTADO |
| Versão do k6 | NAO EXECUTADO |
| Réplicas da API | NAO EXECUTADO |

## Cenário 1 — Pico (50 VUs, ~3min)

| Métrica | Valor |
|---------|-------|
| Requisições totais | NAO EXECUTADO |
| Throughput (req/s) | NAO EXECUTADO |
| http_req_duration p95 | NAO EXECUTADO |
| http_req_failed | NAO EXECUTADO |
| Checks (status 202) | NAO EXECUTADO |

## Cenário 2 — Leitura (100 VUs, 1min)

| Métrica | Valor |
|---------|-------|
| Requisições totais | NAO EXECUTADO |
| Throughput (req/s) | NAO EXECUTADO |
| http_req_duration p95 | NAO EXECUTADO |
| http_req_failed (threshold < 0,5%) | NAO EXECUTADO |
| Checks (status 200) | NAO EXECUTADO |

## Cenário 3 — Stress (até 500 VUs, ~7min)

| Métrica | Valor |
|---------|-------|
| Requisições totais | NAO EXECUTADO |
| Throughput (req/s) | NAO EXECUTADO |
| http_req_duration p95 | NAO EXECUTADO |
| http_req_failed | NAO EXECUTADO |
| Ponto de saturação observado | NAO EXECUTADO |

## Problemas encontrados na execução

O ambiente desta sessão de scaffold **não tem Docker disponível** (nem no Windows nem no
Git Bash), então não foi possível subir a stack (`docker compose up --build`) nem rodar o k6
em container. Os scripts dos três cenários e o `docker-compose.carga.yml` estão prontos e
revisados; a execução real precisa ser feita manualmente em um ambiente com Docker.

## Como executar (quando houver Docker)

1. Subir a stack na raiz do projeto:
   ```
   docker compose up --build -d
   ```
2. Cenário 1 (pico):
   ```
   docker compose -f carga/docker-compose.carga.yml run --rm k6-pico
   ```
3. **Drenar a fila antes do cenário 2** — o consumidor persiste a ~20 msg/s (polling 500ms,
   lote 10), então as escritas do cenário 1 ainda estão na fila quando ele termina. Sem esperar,
   os GETs do cenário 2 batem em chaves não persistidas e retornam 404. Verifique a fila zerada:
   ```
   docker exec credito-fiscal-rabbitmq rabbitmqctl list_queues name messages | grep integrar
   ```
4. Cenário 2 (leitura) e cenário 3 (stress):
   ```
   docker compose -f carga/docker-compose.carga.yml run --rm k6-leitura
   docker compose -f carga/docker-compose.carga.yml run --rm k6-stress
   ```
5. Preencher as tabelas acima com o resumo que o k6 imprime ao final de cada cenário.
