# build: SDK 6 (a versao exata respeita o global.json)
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# so os manifestos primeiro: cacheia o restore enquanto o codigo nao muda
COPY global.json Directory.Build.props CreditoFiscal.sln ./
COPY src/CreditoFiscal.Dominio/CreditoFiscal.Dominio.csproj src/CreditoFiscal.Dominio/
COPY src/CreditoFiscal.Aplicacao/CreditoFiscal.Aplicacao.csproj src/CreditoFiscal.Aplicacao/
COPY src/CreditoFiscal.Infraestrutura/CreditoFiscal.Infraestrutura.csproj src/CreditoFiscal.Infraestrutura/
COPY src/CreditoFiscal.Api/CreditoFiscal.Api.csproj src/CreditoFiscal.Api/
RUN dotnet restore src/CreditoFiscal.Api/CreditoFiscal.Api.csproj

# o resto do codigo e o publish
COPY src/ src/
RUN dotnet publish src/CreditoFiscal.Api/CreditoFiscal.Api.csproj -c Release -o /app/publish --no-restore

# runtime: so o ASP.NET 6, imagem final menor
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CreditoFiscal.Api.dll"]
