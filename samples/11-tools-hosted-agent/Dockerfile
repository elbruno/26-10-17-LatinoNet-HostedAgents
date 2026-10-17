# 11: empaquetar el host con la tool enlazada desde 04.
# A. Usar la raiz publica como contexto para incluir las fuentes compartidas.
# B. Publicar el proyecto con el SDK .NET.
# C. Ejecutar el DLL como usuario no root, sin secretos locales.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY samples ./samples
RUN dotnet publish samples/11-tools-hosted-agent/11-tools-hosted-agent.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8088
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "11-tools-hosted-agent.dll"]
