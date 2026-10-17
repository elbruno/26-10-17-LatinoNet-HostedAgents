# Foundry Hosted Agents: de localhost a Foundry con C#/.NET

Ejemplos públicos de LatinoNet, 17 de octubre de 2026: de una aplicación de
consola con Microsoft Agent Framework (MAF) en .NET 10 a un Hosted Agent con Responses.

El punto de partida es `samples\01-basic-MAF-agent\Program.cs`. Los ejemplos 02 a 09
exploran capacidades de MAF. El cierre vuelve al agente de 01 y lo conecta al
host en `samples\10-hello-hosted-agent\Program.cs`, conservando la lógica.
Son ejemplos didácticos con datos ficticios, no aplicaciones de producción.
Las tools no hacen cobros ni reservas. El ejemplo 23 sí envía un mensaje real
a Service Bus, únicamente después de una confirmación autenticada fuera del chat.

## Requisitos y comprobación previa

Para las consolas: PowerShell 7, SDK estable de .NET 10, Azure CLI para
`AzureCliCredential` y `az login`, proyecto Foundry y deployment con soporte Responses.
`global.json` evita seleccionar un SDK preview de otra versión.
Para el cierre Hosted, además se requieren las versiones de `azure.yaml`:
`azd >=1.27.1` y `azure.ai.agents >=1.0.0-beta.16`.

```powershell
# Build y pruebas offline de toda la serie, sin Azure ni secretos:
pwsh -File .\scripts\Test-Demos.ps1
# Solo para comprobar las herramientas del cierre Hosted:
pwsh -File .\scripts\Test-Prerequisites.ps1
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

### Dónde están las demos

Todas las demos están bajo `samples`. Cada consola es un proyecto
independiente: ejecutar una no requiere haber ejecutado las anteriores.
La solución `LatinoNet.HostedAgents.slnx` permite abrir toda la serie junta.
Los fuentes C# tienen un encabezado con propósito y pasos A/B/C y comentarios
en los puntos que conviene explicar. Los proyectos, scripts, Dockerfiles y
manifiestos también indican su responsabilidad. Estos comentarios son una
guía didáctica, no sustituyen validaciones ni controles del backend.

| Carpeta | Capacidad | Qué observar |
|---|---|---|
| [01-basic-MAF-agent](samples/01-basic-MAF-agent/) | Un `AIAgent` de consola | `AsAIAgent` y una llamada a `RunAsync` |
| [02-streaming-MAF-agent](samples/02-streaming-MAF-agent/) | Streaming | `RunStreamingAsync` y escritura incremental |
| [03-sessions-MAF-agent](samples/03-sessions-MAF-agent/) | Sesiones y contexto | Dos turnos con `CreateSessionAsync` y una sesión aislada |
| [04-tools-MAF-agent](samples/04-tools-MAF-agent/) | Tools de C# | `AIFunctionFactory`, cálculo ficticio validado y contador de llamadas reales |
| [05-structured-output-MAF-agent](samples/05-structured-output-MAF-agent/) | Salida estructurada | `RunAsync<TalkPlan>`, esquema JSON y validación de negocio |
| [06-middleware-MAF-agent](samples/06-middleware-MAF-agent/) | Middleware de agente | Medición de intentos sin registrar prompts, conservando errores |
| [07-agent-as-tool-MAF-agent](samples/07-agent-as-tool-MAF-agent/) | Agente como tool | Un coordinador consulta un especialista mediante `AsAIFunction` |
| [08-sequential-workflow-MAF-agent](samples/08-sequential-workflow-MAF-agent/) | Workflow secuencial | `AgentWorkflowBuilder`: escritor, revisor y resultado final |
| [09-telemetry-MAF-agent](samples/09-telemetry-MAF-agent/) | Telemetría | `UseOpenTelemetry` y un listener de spans local, sin registrar contenido sensible |
| [10-hello-hosted-agent](samples/10-hello-hosted-agent/) | El agente de 01 preparado para alojar | `AgentHost`, `AddFoundryResponses`, `RegisterProtocol` |
| [11-tools-hosted-agent](samples/11-tools-hosted-agent/) | Tools con Responses | La misma tool de presupuesto de 04, sin cobros ni reservas |
| [12-sequential-workflow-hosted-agent](samples/12-sequential-workflow-hosted-agent/) | Workflow con Responses | Escritor y revisor de 08 en un proceso, no ejecución durable |
| [20-keyvault-hosted-agent](samples/20-keyvault-hosted-agent/) | Key Vault | Credencial de una API externa solo en el backend |
| [21-ai-search-hosted-agent](samples/21-ai-search-hosted-agent/) | AI Search | Filtro de acceso fijo en backend y referencias documentales |
| [22-blob-storage-hosted-agent](samples/22-blob-storage-hosted-agent/) | Blob Storage | Lectura de texto UTF-8 acotada a un contenedor |
| [23-service-bus-hosted-agent](samples/23-service-bus-hosted-agent/) | Service Bus | Propuesta, confirmación humana autenticada y encolado |
| [24-application-insights-hosted-agent](samples/24-application-insights-hosted-agent/) | Application Insights | Exportación de trazas sin contenido del modelo |

Desde la raíz, configurar User Secrets una vez y ejecutar la consola elegida:

```powershell
pwsh -File .\scripts\Set-DemoUserSecrets.ps1
az login
dotnet run --project .\samples\01-basic-MAF-agent
# Elegir la carpeta numerada y descriptiva de otra consola:
dotnet run --project .\samples\04-tools-MAF-agent
# El servidor del cierre:
dotnet run --project .\samples\10-hello-hosted-agent
```

El script muestra campos de entrada normales, no ocultos, para pedir una sola vez
el **endpoint del proyecto Foundry** y el **nombre del deployment del modelo**,
proponiendo **`gpt-6.1-sol`** como deployment por defecto: presionar Enter lo acepta,
o escribir otro nombre para reemplazarlo. Debe existir con ese nombre en tu proyecto.
Aplica ambos valores a todos los
`.csproj` bajo `samples` que tengan `UserSecretsId`. No pide API keys ni passwords:
las consolas usan `AzureCliCredential`, exclusivamente con la sesión de `az login`.
El cierre Hosted conserva `DefaultAzureCredential` para admitir la identidad del
runtime remoto sin depender de Azure CLI. Conserva las demás claves existentes.
Repetirlo actualiza esas dos claves. Los valores se envían a `dotnet user-secrets`
por stdin, no como argumentos del proceso.

User Secrets se almacena fuera del repositorio, en el perfil local del usuario,
y **no está cifrado**: no es un almacén de producción. Cada demo tiene su propio
`UserSecretsId`; el script configura los diecisiete proyectos con las mismas dos entradas.
Las copias pública y privada de cada proyecto comparten ID: configurarlo en una
copia sirve para la otra bajo el mismo usuario. No hace falta ejecutar el script dos veces.
Los nombres de carpeta y `.csproj` ahora incluyen número y descripción;
los `UserSecretsId` anteriores se conservan para no perder la configuración local.
El sample carga estos valores explícitamente, también al correr un build Release.

Como alternativa, configurar variables de entorno solo en la terminal. Tienen
prioridad sobre User Secrets, lo que conserva el funcionamiento en Foundry.
Los siguientes valores son marcadores, no endpoints reales:

```powershell
$env:FOUNDRY_PROJECT_ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>"
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = "<model-deployment>"
az login
dotnet run --project .\samples\01-basic-MAF-agent
```

Las consolas ejecutan el proceso en tu máquina, **pero llaman al modelo de
Foundry**: no son inferencia offline ni ejemplos de Foundry Local. Las demos 04
y 07 requieren tool calling; la 05 requiere structured output. No asumir que
cualquier deployment admite estas capacidades. Los contadores de 04 y 07 fallan
explícitamente si el modelo no usa la tool o no delega.

La 03 no implementa memoria durable de la aplicación. La 08 ejecuta ambos agentes
en el mismo proceso, no crea agentes remotos. La 09 escucha spans del agente en
la consola: no configura un exporter OTLP, Application Insights ni un dashboard,
y una traza no es una evaluación de calidad.

### De 01 al Hosted Agent

El sample `10-hello-hosted-agent` mantiene la configuración, instrucciones y nombre
del agente de `01-basic-MAF-agent`. Cambia `AzureCliCredential` por `DefaultAzureCredential`
para admitir la identidad del runtime remoto; en local también puede usar el login
de Azure CLI. Agrega el paquete
`Microsoft.Agents.AI.Foundry.Hosting`, sus imports, y reemplaza la llamada de consola por:

```csharp
var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
```

El resto de las features no es un requisito para alojar un agente. No hay que
convertir primero las nueve demos en una aplicación gigante.

Para probar el HTTP local del cierre:

```powershell
dotnet run --project .\samples\10-hello-hosted-agent
```

En otra terminal, invocar Responses directamente, sin depender del estado local de `azd`:

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:8088/responses" `
    -ContentType "application/json" `
    -Body '{"input":"Explica en una frase que hace este agente."}'
```

`GET http://localhost:8088/readiness` solo comprueba el host, no la conectividad del modelo. La inferencia requiere un proyecto, un deployment y permisos válidos. Mantener el puerto 8088 local; no es una frontera de autenticación de producción.

## Loop con azd y despliegue

User Secrets configura el proceso .NET local, no `azd doctor`, el entorno `.azure`
ni el contenedor remoto. Para desplegar, seguir configurando el entorno de `azd`
como se indica abajo. Nunca copiar `secrets.json` a la imagen: Foundry inyecta el
endpoint y `azure.yaml` proporciona el nombre del deployment.

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

## Continuación implementada: 11, 12 y 20 a 24

Son siete hosts independientes, no pasos obligatorios antes de 10. Cada uno
incluye README, Dockerfile y `azure.yaml` propios. Los helpers de 04 y 08 se
enlazan desde sus fuentes originales; no hay copias divergentes de la lógica.
Los ejemplos 20 a 23 comparten solamente validación de configuración en
`samples/shared/HostedSettings.cs`.

Desde la raíz, después de configurar los dos valores Foundry y las variables
específicas del README del ejemplo elegido:

```powershell
dotnet run --project .\samples\11-tools-hosted-agent
# En otra terminal:
Invoke-RestMethod -Method Post -Uri "http://localhost:8088/responses" `
    -ContentType "application/json" `
    -Body '{"input":"Calcula el presupuesto ficticio para 12 asistentes a 25 USD."}'
```

Ejecutar solo un host por puerto. El 12 necesita dos llamadas al modelo
como mínimo, no es durable ni distribuido. Los ejemplos 20 a 24 requieren
recursos externos existentes; sus manifiestos **no crean Key Vault, Search,
Storage, Service Bus ni Application Insights, ni asignan sus roles**.

| Ejemplo | Configuración adicional obligatoria | Permiso mínimo a revisar para la identidad efectiva |
|---|---|---|
| 11 y 12 | Ninguna además de Foundry | Acceso de inferencia al proyecto/modelo |
| 20 | `KEY_VAULT_URI`, `WORKSHOP_API_SECRET_NAME`, `WORKSHOP_API_ENDPOINT` | Key Vault Secrets User, limitado al secreto necesario; API externa propia |
| 21 | `AZURE_SEARCH_ENDPOINT`, `AZURE_SEARCH_INDEX`, `SEARCH_ALLOWED_GROUP` | Search Index Data Reader; índice con ACL preparado por el administrador |
| 22 | `BLOB_CONTAINER_URI` | Storage Blob Data Reader, limitado al contenedor de demo |
| 23 | `SERVICE_BUS_NAMESPACE`, `SERVICE_BUS_QUEUE`, `JOB_APPROVAL_TOKEN` | Azure Service Bus Data Sender, limitado a la cola; duplicate detection habilitado |
| 24 | `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recurso de Application Insights y política de ingesta compatibles |

Los nombres de roles no equivalen a permisos ya concedidos. En local,
`DefaultAzureCredential` puede usar el login del desarrollador; en remoto debe
revisarse la identidad concreta del host. Los ejemplos 21 y 22 son demos de un
grupo/contenedor fijo, no autenticación multiusuario. No exponer sus endpoints
a usuarios de otros grupos. La autenticación de entrada, red y autorización
por usuario deben resolverse antes de producción.

### Desplegar solo el ejemplo elegido

El manifiesto raíz sigue desplegando únicamente el 10. Los manifiestos nuevos
son proyectos `azd` independientes. No copiar ni sustituir el manifiesto raíz.
Por ejemplo, para el 11:

```powershell
Set-Location .\samples\11-tools-hosted-agent
azd auth login
azd env new latinonet-tools-demo
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME gpt-5.4-mini
azd ai agent doctor --local-only
# Revisar suscripción, región, cuotas, identidad y costos ANTES de estos pasos:
azd provision
azd deploy
```

Cada manifiesto declara un proyecto Foundry/modelo propio, por lo que provisionar
varios puede duplicar recursos y costos. Para reutilizar un proyecto existente,
seguir el flujo oficial de vinculación de Foundry, no suponer que basta un
endpoint. En 20 a 24 configurar también cada variable adicional con `azd env set`
en el entorno seleccionado. User Secrets no viaja al contenedor. Los secretos
de aprobación de 23 no deben mostrarse en pantalla ni guardarse en Git; el estado
local de `azd` y User Secrets no son almacenes de secretos de producción.

Los Dockerfiles nuevos usan como contexto la raíz pública (`context: ../..`
respecto del servicio) para resolver las fuentes enlazadas. Para un build manual,
desde la raíz pública:

```powershell
docker build --platform linux/amd64 `
    -f .\samples\11-tools-hosted-agent\Dockerfile -t latinonet-tools .
```

Las pruebas offline compilan los hosts y ejercen las tools y workflows con
clientes/transports simulados. No certifican inferencia, roles, entrega del
broker, ingesta de telemetría, build remoto ni un despliegue Azure exitoso.
El código y los manifiestos están preparados para ese recorrido, pero la
validación cloud sigue siendo un paso explícito con recursos reales.

## Referencias

- [Artículo: From dotnet run to Foundry Hosted Agent in 3 lines of C#](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/)
- [Conceptos de Hosted Agents](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents)
- [Desplegar un Hosted Agent](https://learn.microsoft.com/azure/foundry/agents/how-to/deploy-hosted-agent)
- [Sample oficial de C# con Agent Framework](https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework/hello-world)

Este repositorio contiene exclusivamente código, configuración y documentación pública del ejemplo. No contiene slides, guion de speaker, runbooks internos, credenciales ni archivos de entorno.
