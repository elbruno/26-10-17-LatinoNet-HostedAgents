# 12: empaquetar escritor y revisor en el mismo host.
# A. Incluir fuentes enlazadas desde la raiz publica.
# B. Publicar el workflow con SDK .NET.
# C. Ejecutar el DLL como usuario no root, sin convertirlo en workflow durable.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY samples ./samples
RUN dotnet publish samples/12-sequential-workflow-hosted-agent/12-sequential-workflow-hosted-agent.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8088
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "12-sequential-workflow-hosted-agent.dll"]
