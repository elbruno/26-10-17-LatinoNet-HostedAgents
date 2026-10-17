# 24: empaquetar el host instrumentado sin capturar contenido del modelo.
# A. Incluir fuentes desde la raiz publica.
# B. Publicar el agente y su exporter con SDK .NET.
# C. Ejecutar como usuario no root; la conexion de telemetria se configura al arrancar.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY samples ./samples
RUN dotnet publish samples/24-application-insights-hosted-agent/24-application-insights-hosted-agent.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8088
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "24-application-insights-hosted-agent.dll"]
