# 21: empaquetar el host de retrieval, sin copiar documentos ni credenciales.
# A. Incluir fuentes y validacion compartida desde la raiz publica.
# B. Publicar con SDK .NET.
# C. Ejecutar como usuario no root; indice y permisos son dependencias externas.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY samples ./samples
RUN dotnet publish samples/21-ai-search-hosted-agent/21-ai-search-hosted-agent.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8088
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "21-ai-search-hosted-agent.dll"]
