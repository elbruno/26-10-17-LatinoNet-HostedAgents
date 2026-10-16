# Foundry Hosted Agents: de localhost a Foundry con C#/.NET

Ejemplo público de LatinoNet, 17 de octubre de 2026: un agente de Microsoft Agent Framework en .NET 10, expuesto mediante Responses.

La lógica está en `samples\dotnet\hello-hosted-agent\Program.cs`. La integración de hosting aporta el servidor del protocolo. Es un ejemplo didáctico, no una aplicación de producción: no incluye datos de negocio, herramientas personalizadas ni autorización de sistemas externos.

## Requisitos y comprobación previa

Usar PowerShell 7, SDK estable de .NET 10, Azure CLI para `DefaultAzureCredential`, y las versiones de `azure.yaml`: `azd >=1.27.1` y `azure.ai.agents >=1.0.0-beta.16`. `global.json` evita seleccionar un SDK preview de otra versión.

```powershell
pwsh -File .\scripts\Test-Prerequisites.ps1
dotnet build .\samples\dotnet\hello-hosted-agent\hello-agent.csproj -c Release
pwsh -File .\tests\Test-Prerequisites.Tests.ps1
```

El preflight consulta versiones, no instala ni actualiza nada. Compara `installedVersion`, no `version`, que puede describir el catálogo. Que `azd ai agent --help` funcione no demuestra que la extensión cumpla el manifiesto.

Si necesitás probar otra extensión, hacerlo primero con una configuración aislada en una terminal nueva, sin cambiar la instalación habitual:

```powershell
$env:AZD_CONFIG_DIR = Join-Path $env:TEMP "latinonet-azd-evaluation"
azd extension install azure.ai.agents --version 1.0.0-beta.16
pwsh -File .\scripts\Test-Prerequisites.ps1
```

Esto instala también las dependencias declaradas en esa configuración aislada. No copiar credenciales ni configuración global a esa carpeta. Cerrar la terminal al terminar; no usar `extension update --all` como solución automática. Este control de versiones no certifica todas las operaciones cloud.

Los paquetes NuGet están fijados en el proyecto. La disponibilidad del servicio Hosted Agents y el estado prerelease de la integración .NET son cosas distintas.

## Ejecutar localmente

Desde la raíz, configurar valores propios solo en la terminal. Los siguientes valores son marcadores, no endpoints reales:

```powershell
$env:FOUNDRY_PROJECT_ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>"
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = "<model-deployment>"
az login
dotnet run --project .\samples\dotnet\hello-hosted-agent\hello-agent.csproj
```

En otra terminal, invocar Responses directamente, sin depender del estado local de `azd`:

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:8088/responses" `
    -ContentType "application/json" `
    -Body '{"input":"Explica en una frase que hace este agente."}'
```

`GET http://localhost:8088/readiness` solo comprueba el host, no la conectividad del modelo. La inferencia requiere un proyecto, un deployment y permisos válidos. Mantener el puerto 8088 local; no es una frontera de autenticación de producción.

## Loop con azd y despliegue

El manifiesto ya existe, no ejecutar un scaffold que lo reemplace. Crear o seleccionar un entorno local de `azd`, autenticar y configurar el deployment:

```powershell
azd auth login
azd env new latino-net-demo
# Si ya existe: azd env select latino-net-demo
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME gpt-5.4-mini
azd ai agent doctor --local-only
```

La plantilla declara un proyecto Foundry y el modelo `gpt-5.4-mini`, versión `2026-03-17`. Antes de provisionar, comprobar disponibilidad regional, cuota, suscripción, permisos y costos. No asumir que un proyecto existente queda enlazado solo por establecer una variable; seguir la documentación de despliegue para reutilizar recursos.

Solo después de revisar el entorno:

```powershell
azd provision
azd ai agent doctor
azd ai agent run --no-client
```

En otra terminal con el mismo entorno:

```powershell
azd ai agent invoke --local --new-session "Explica en una frase que hace este agente."
```

Detener el host local cuando termine la prueba. Con infraestructura preparada:

```powershell
azd deploy
azd ai agent invoke --new-session "Explica en una frase que hace este agente."
```

`remoteBuild: true` solicita build remoto; no certifica que Docker local funcione. La imagen de destino debe ser `linux/amd64`. Foundry inyecta `FOUNDRY_PROJECT_ENDPOINT` al contenedor; no declararlo otra vez en el manifiesto.

Provisionar genera recursos facturables. Scale-to-zero no significa factura total cero. La identidad dedicada no concede automáticamente acceso a recursos externos, y los guardrails de un agente declarativo no se aplican sin más a código propio. Revisar permisos, datos, mitigaciones y telemetría. Ejecutar `azd down` únicamente tras revisar qué recursos se eliminarían.

## Referencias

- [Artículo: From dotnet run to Foundry Hosted Agent in 3 lines of C#](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/)
- [Conceptos de Hosted Agents](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents)
- [Desplegar un Hosted Agent](https://learn.microsoft.com/azure/foundry/agents/how-to/deploy-hosted-agent)
- [Sample oficial de C# con Agent Framework](https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework/hello-world)

Este repositorio contiene exclusivamente código, configuración y documentación pública del ejemplo. No contiene slides, guion de speaker, runbooks internos, credenciales ni archivos de entorno.
