# 23: empaquetar el productor y su API de aprobacion, sin incluir tokens.
# A. Incluir fuentes desde la raiz publica.
# B. Publicar con SDK .NET.
# C. Ejecutar como usuario no root; revisar ingreso autenticado y estado antes de escalar.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY samples ./samples
RUN dotnet publish samples/23-service-bus-hosted-agent/23-service-bus-hosted-agent.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8088
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "23-service-bus-hosted-agent.dll"]
