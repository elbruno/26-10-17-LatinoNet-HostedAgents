# 23: proponer no es autorizar ni ejecutar

El modelo tiene una sola tool, `ProposeAsync`: crea un trabajo ficticio con un
tema acotado a 200 caracteres, un ID, caducidad de cinco minutos y estado
`awaiting-human-confirmation`. **No envía mensajes.**
La aplicación expone por separado `POST /jobs/{id}/confirm`, exige un bearer
token secreto y solo entonces envía el payload a Service Bus. `ConfirmAsync`
no está registrada como tool y ningún booleano del modelo autoriza el envío.

Configurar `FOUNDRY_PROJECT_ENDPOINT`, `AZURE_AI_MODEL_DEPLOYMENT_NAME`,
`SERVICE_BUS_NAMESPACE` (`<namespace>.servicebus.windows.net`),
`SERVICE_BUS_QUEUE` y `JOB_APPROVAL_TOKEN`. Este último debe ser un valor
criptográficamente aleatorio de al menos 32 caracteres, almacenado fuera de Git,
conocido solo por la aplicación/humano que aprueba. No ponerlo en prompts, URLs,
logs ni capturas. Configurarlo en User Secrets o en el mecanismo secreto del
runtime, y conservarlo por separado en el cliente humano que confirma.
El nombre de deployment es opcional: si falta, usa `gpt-6-luna` ya existente.
Un valor explícito en configuración conserva prioridad.

La identidad requiere Azure Service Bus Data Sender al alcance de la cola.
Crear la cola con **duplicate detection habilitado**. Cada mensaje usa el ID
de propuesta como `MessageId` y JSON `{"type":"demo-summary","Id":"...","Topic":"..."}`.
Un consumidor real debe persistir los IDs procesados y ser idempotente;
este ejemplo muestra el productor, no implementa ese consumidor.

```powershell
# Desde la raíz pública:
dotnet run --project .\samples\23-service-bus-hosted-agent
# En otra terminal, primero solicitar la propuesta, todavía sin envío:
Invoke-RestMethod -Method Post -Uri "http://localhost:8088/responses" `
    -ContentType "application/json" `
    -Body '{"input":"Propone un resumen ficticio sobre agentes .NET."}'
```

El humano consulta `GET /jobs/{id}` con el mismo bearer token y revisa tema e ID
**del registro del backend**, no solo el texto que escribió el modelo. La propuesta
es inmutable hasta su confirmación o caducidad. En su cliente,
`$approvalToken` debe cargarse de un almacén seguro por fuera del chat:

```powershell
$proposalId = "<id-revisado-de-la-propuesta>"
Invoke-RestMethod -Uri "http://localhost:8088/jobs/$proposalId" `
    -Headers @{ Authorization = "Bearer $approvalToken" }
# Solo DESPUES de revisar y aprobar el registro recibido:
Invoke-RestMethod -Method Post `
    -Uri "http://localhost:8088/jobs/$proposalId/confirm" `
    -Headers @{ Authorization = "Bearer $approvalToken" }
```

Solo la respuesta HTTP `queued` confirma que el SDK completó el envío.
No significa que un consumidor ejecutó el trabajo. Los fallos del broker se
propagan, no se convierten en éxito. Un retry conserva el `MessageId`; la
deduplicación del broker debe cubrir su ventana de reintentos.

La lista pendiente está en memoria, máximo 100 propuestas. Un lock evita
confirmaciones concurrentes duplicadas dentro de una instancia; un reinicio
pierde propuestas y varias réplicas no comparten estado. Es una demo de
**una instancia**, no un sistema durable ni exactly-once. Antes de producción,
usar autenticación Entra con aprobación vinculada al usuario, estado durable,
outbox, rate limits, auditoría sin secretos y consumidor idempotente.
El endpoint de confirmación adicional debe ser accesible por una ruta autenticada
de la aplicación; el proxy de Responses de Foundry no garantiza publicarlo.
No exponer HTTP sin TLS fuera de loopback. No conectar una cola de producción.

Consultar [ejecución y despliegue común](../../README.md#continuación-implementada-11-12-y-20-a-24).
El manifiesto no crea namespace, cola ni roles. Los tests no contactan un broker
ni prueban entrega, inferencia o despliegue Azure.
