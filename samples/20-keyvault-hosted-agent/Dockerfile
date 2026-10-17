# 20: empaquetar el backend de Key Vault sin incorporar el secreto a la imagen.
# A. Incluir fuentes desde la raiz publica, con .dockerignore activo.
# B. Publicar el proyecto con SDK .NET.
# C. Ejecutar como usuario no root; la identidad se resuelve en el runtime.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY samples ./samples
RUN dotnet publish samples/20-keyvault-hosted-agent/20-keyvault-hosted-agent.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8088
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "20-keyvault-hosted-agent.dll"]
