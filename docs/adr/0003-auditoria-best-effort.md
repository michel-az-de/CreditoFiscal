# 0003 Auditoria de consultas best effort

Status: aceito.
Data: 2026-05-29.

## Contexto

O desafio adicional pede publicar evento de auditoria em cada consulta para simular logs ou eventos armazenados em sistema externo. A leitura nao pode falhar porque a publicacao de auditoria falhou: o GET tem que responder mesmo com o broker fora.

## Decisao

`PublicadorAuditoria` publica `ConsultaCreditoRealizadaDto` no topico `consulta-credito-realizada` em cada GET. O `try` envolve a chamada de `IMensagemPublisher.PublicarAsync` e o `catch (Exception)` engole tudo e loga `Warning`. Sem retry, sem outbox, sem fila local. Reutiliza o `IMensagemPublisher` do provedor escolhido em `Mensageria:Provedor`.

## Consequencias

Positivas: auditoria nunca derruba a consulta. Trocar de broker continua sendo uma mudanca de string. Codigo simples e isolado em um caso de uso de borda.

Negativas: auditoria e sincrona dentro da requisicao HTTP. Broker fora cai rapido com `connection refused` (modo de falha conhecido), mas broker lento sem timeout configurado no cliente faz o GET somar a latencia do publish. Trabalho futuro: envolver em `CancellationTokenSource` com timeout curto, ou disparar em `Task.Run` fora da thread da consulta. Documentado em README como "auditoria sincrona" em "Limitacoes conhecidas".
