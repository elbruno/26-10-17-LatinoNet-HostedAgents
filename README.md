# Foundry Hosted Agents: de localhost a Foundry con C#/.NET

Ejemplos públicos de LatinoNet, 17 de octubre de 2026: de una aplicación de
consola con Microsoft Agent Framework (MAF) en .NET 10 a un Hosted Agent con Responses.

El punto de partida es `samples\01-demo\Program.cs`. Los ejemplos 02 a 09
exploran capacidades de MAF. El cierre vuelve al agente de 01 y lo conecta al
host en `samples\hello-hosted-agent\Program.cs`, conservando la lógica.
Son ejemplos didácticos con datos ficticios, no aplicaciones de producción.
Las tools de ejemplo no hacen cobros, reservas, envíos ni cambios en sistemas externos.

## Requisitos y comprobación previa

Para las consolas: PowerShell 7, SDK estable de .NET 10, Azure CLI para
`DefaultAzureCredential`, proyecto Foundry y deployment con soporte Responses.
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

| Carpeta | Capacidad | Qué observar |
|---|---|---|
| [01-demo](samples/01-demo/) | Un `AIAgent` de consola | `AsAIAgent` y una llamada a `RunAsync` |
| [02-demo](samples/02-demo/) | Streaming | `RunStreamingAsync` y escritura incremental |
| [03-demo](samples/03-demo/) | Sesiones y contexto | Dos turnos con `CreateSessionAsync` y una sesión aislada |
| [04-demo](samples/04-demo/) | Tools de C# | `AIFunctionFactory`, cálculo ficticio validado y contador de llamadas reales |
| [05-demo](samples/05-demo/) | Salida estructurada | `RunAsync<TalkPlan>`, esquema JSON y validación de negocio |
| [06-demo](samples/06-demo/) | Middleware de agente | Medición de intentos sin registrar prompts, conservando errores |
| [07-demo](samples/07-demo/) | Agente como tool | Un coordinador consulta un especialista mediante `AsAIFunction` |
| [08-demo](samples/08-demo/) | Workflow secuencial | `AgentWorkflowBuilder`: escritor, revisor y resultado final |
| [09-demo](samples/09-demo/) | Telemetría | `UseOpenTelemetry` y un listener de spans local, sin registrar contenido sensible |
| [hello-hosted-agent](samples/hello-hosted-agent/) | El agente de 01 preparado para alojar | `AgentHost`, `AddFoundryResponses`, `RegisterProtocol` |

Desde la raíz, configurar User Secrets una vez y ejecutar la consola elegida:

```powershell
pwsh -File .\scripts\Set-DemoUserSecrets.ps1
az login
dotnet run --project .\samples\01-demo
# Cambiar el numero para elegir otra consola:
dotnet run --project .\samples\04-demo
# El servidor del cierre:
dotnet run --project .\samples\hello-hosted-agent
```

El script muestra campos de entrada normales, no ocultos, para pedir una sola vez
el **endpoint del proyecto Foundry** y el **nombre del deployment del modelo**,
y aplica ambos valores a todos los
`.csproj` bajo `samples` que tengan `UserSecretsId`. No pide API keys ni passwords:
el agente usa `DefaultAzureCredential`. Conserva las demás claves existentes.
Repetirlo actualiza esas dos claves. Los valores se envían a `dotnet user-secrets`
por stdin, no como argumentos del proceso.

User Secrets se almacena fuera del repositorio, en el perfil local del usuario,
y **no está cifrado**: no es un almacén de producción. Cada demo tiene su propio
`UserSecretsId`; el script configura los diez proyectos con las mismas dos entradas.
Las copias pública y privada de cada proyecto comparten ID: configurarlo en una
copia sirve para la otra bajo el mismo usuario. No hace falta ejecutar el script dos veces.
El sample carga estos valores explícitamente, también al correr un build Release.

Como alternativa, configurar variables de entorno solo en la terminal. Tienen
prioridad sobre User Secrets, lo que conserva el funcionamiento en Foundry.
Los siguientes valores son marcadores, no endpoints reales:

```powershell
$env:FOUNDRY_PROJECT_ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>"
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = "<model-deployment>"
az login
dotnet run --project .\samples\01-demo
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

El sample `hello-hosted-agent` mantiene exactamente la configuración, instrucciones,
nombre y construcción del agente de `01-demo`. Agrega el paquete
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
dotnet run --project .\samples\hello-hosted-agent
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

## Referencias

- [Artículo: From dotnet run to Foundry Hosted Agent in 3 lines of C#](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/)
- [Conceptos de Hosted Agents](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents)
- [Desplegar un Hosted Agent](https://learn.microsoft.com/azure/foundry/agents/how-to/deploy-hosted-agent)
- [Sample oficial de C# con Agent Framework](https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework/hello-world)

Este repositorio contiene exclusivamente código, configuración y documentación pública del ejemplo. No contiene slides, guion de speaker, runbooks internos, credenciales ni archivos de entorno.
