# 10: empaquetar el agente minimo como servidor .NET.
# A. Definir runtime y puerto del host.
# B. Restaurar y publicar con SDK, sin User Secrets ni archivos de entorno.
# C. Ejecutar solamente el artefacto publicado, no el SDK.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8088

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY 10-hello-hosted-agent.csproj .
RUN dotnet restore 10-hello-hosted-agent.csproj
COPY . .
RUN dotnet publish 10-hello-hosted-agent.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "10-hello-hosted-agent.dll"]
