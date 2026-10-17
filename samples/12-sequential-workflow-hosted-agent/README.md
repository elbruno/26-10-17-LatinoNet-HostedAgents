# 12. Workflow secuencial en un Hosted Agent

Expone mediante Responses un workflow real de MAF:
`LatinoNetAgendaWriter` escribe una propuesta y `LatinoNetAgendaReviewer`
recibe el borrador y devuelve la agenda revisada. Reutiliza por enlace
`AgendaWorkflow.cs` del sample 08 y `AgentWorkflowBuilder.BuildSequential`.
No es una simulacion de dos respuestas concatenadas.

## Configuracion y ejecucion

Desde esta carpeta:

```powershell
dotnet user-secrets set FOUNDRY_PROJECT_ENDPOINT "https://<recurso>.services.ai.azure.com/api/projects/<proyecto>"
dotnet user-secrets set AZURE_AI_MODEL_DEPLOYMENT_NAME "<deployment>"
dotnet run
```

Tambien admite las mismas claves como variables de entorno.
El endpoint es obligatorio. El deployment es opcional: si no se configura,
usa el deployment existente `gpt-6-luna`. Un valor explicito tiene prioridad;
un valor vacio o solo espacios es invalido. No crea ni modifica deployments.
Ambos agentes usan el deployment configurado y `DefaultAzureCredential`. La identidad necesita
acceso al proyecto y al modelo.
El host publica `/responses` y `/health` en `PORT` (8088 por defecto).
Ejemplo de solicitud: `Prepara una agenda de 45 minutos sobre C# y Hosted Agents`.

## Verificacion offline

Desde `public-example`:

```powershell
dotnet run --project .\tests\HostedBasicsChecks\HostedBasicsChecks.csproj
```

El check ejecuta el workflow MAF con clientes simulados y verifica el orden,
que el revisor reciba el borrador y que su salida llegue al resultado.
Tambien comprueba streaming y cancelacion previa sin invocar a los agentes.
`HostedAgendaWorkflow` agrega un guard de cancelacion porque el adaptador
WorkflowAgent 1.22.0 puede devolver una respuesta vacia ante un token ya cancelado.
No valida inferencia ni deployment en Foundry.
**El deployment no esta validado.** Una ejecucion real invoca ambos agentes
y puede generar costos.
