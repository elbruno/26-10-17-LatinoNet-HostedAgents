# 11. Tools en un Hosted Agent

Aloja `LatinoNetBudgetAgent` con el protocolo Responses de Foundry.Hosting.
MAF ejecuta la funcion `CalculateBudget` localmente y devuelve su resultado al
modelo antes de generar la respuesta. Reutiliza por enlace el codigo
`WorkshopTools.cs` del sample 04, sin copiarlo.

## Configuracion y ejecucion

Desde esta carpeta:

```powershell
dotnet user-secrets set FOUNDRY_PROJECT_ENDPOINT "https://<recurso>.services.ai.azure.com/api/projects/<proyecto>"
dotnet user-secrets set AZURE_AI_MODEL_DEPLOYMENT_NAME "<deployment>"
dotnet run
```

Tambien admite variables de entorno con esos mismos nombres. Usa
`DefaultAzureCredential`: la identidad necesita acceso al proyecto y al modelo.
El modelo debe admitir function calling. El host publica `/responses` y `/health`
en el puerto `PORT` (8088 por defecto).

Pide, por ejemplo: `Calcula el presupuesto de 12 asistentes a 25 por persona`.
El resultado esperado de la tool es 300. No realiza cobros ni reservas.
Valida entre 1 y 1000 asistentes y un precio entre 0 y 10000.

## Verificacion offline

Desde `public-example`:

```powershell
dotnet run --project .\tests\HostedBasicsChecks\HostedBasicsChecks.csproj
```

Estos checks prueban la llamada y devolucion de la tool con un cliente simulado.
No prueban inferencia, credenciales ni Responses en Azure.
**El deployment no esta validado.** La inferencia real puede generar costos.
