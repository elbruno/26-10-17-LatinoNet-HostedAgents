# 22: empaquetar el host lector, sin copiar blobs ni SAS a la imagen.
# A. Incluir fuentes y validacion compartida desde la raiz publica.
# B. Publicar con SDK .NET.
# C. Ejecutar como usuario no root; el contenedor Blob es externo.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY samples ./samples
RUN dotnet publish samples/22-blob-storage-hosted-agent/22-blob-storage-hosted-agent.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8088
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "22-blob-storage-hosted-agent.dll"]
