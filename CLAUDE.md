# CreditoFiscal

## Fontes

1. Consulte o Atlas no projeto `credito-fiscal` e registre âncora e frescor.
2. Confirme estado pelo Git e GitHub Actions.
3. Leia `README.md` para arquitetura e execução.
4. Fluxo de entrega: `docs/adr/0001-adocao-policy-v4.md`; decisões técnicas: `docs/adr/`.

## Projeto

- Trunk: `develop`.
- Stack: .NET 6, PostgreSQL e brokers Kafka, RabbitMQ ou Service Bus.
- Preserve as dependências da Clean Architecture descritas no README.
- Alteração fiscal, de idempotência ou mensageria exige teste de integração.

## Gates

```powershell
dotnet build --configuration Release
dotnet test tests/CreditoFiscal.Testes/CreditoFiscal.Testes.csproj --configuration Release
dotnet test tests/CreditoFiscal.TestesIntegracao/CreditoFiscal.TestesIntegracao.csproj --configuration Release
```

O último gate exige Docker. `docker compose` local não é deploy.

Uma tarefa usa issue, branch e PR. Preserve WIP, faça stage seletivo e só conclua após aceite, checks, mergeabilidade, merge, limpeza e atualização do Atlas.
