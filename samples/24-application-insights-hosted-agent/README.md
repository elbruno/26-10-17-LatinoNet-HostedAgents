# 24. Application Insights en un Hosted Agent

Exporta traces reales mediante el SDK estable `Azure.Monitor.OpenTelemetry.Exporter`.
Incluye el ActivitySource MAF `LatinoNet.HostedAgent24`, peticiones ASP.NET Core
y dependencias HttpClient. Conserva la correlacion W3C del host.
No usa un listener de consola como sustituto de Application Insights.

## Configuracion y ejecucion

Desde esta carpeta:

```powershell
dotnet user-secrets set FOUNDRY_PROJECT_ENDPOINT "https://<recurso>.services.ai.azure.com/api/projects/<proyecto>"
dotnet user-secrets set AZURE_AI_MODEL_DEPLOYMENT_NAME "<deployment>"
dotnet user-secrets set APPLICATIONINSIGHTS_CONNECTION_STRING "<connection-string>"
dotnet run
```

Las tres claves tambien admiten variables de entorno. La conexion de Application
Insights es obligatoria: no se inicia silenciosamente sin exporter.
La identidad de `DefaultAzureCredential` necesita acceso al proyecto y al modelo.
El host publica `/responses` y `/health` en `PORT` (8088 por defecto).
La exportacion necesita conectividad saliente al endpoint de ingestion.

`AgentHost` ya incorpora un exporter Azure Monitor cuando la conexion esta en
el entorno. El sample reutiliza ese exporter para no duplicar traces.
Si se configura mediante user-secrets, agrega explicitamente el exporter de traces.
El host puede publicar sus metricas automaticas cuando usa su exporter integrado.
Usa el exporter estable 1.10.0. La version 1.7.0 transitiva del host no exportaba
los spans en el check integrado; no se debe bajar la version explicita.

## Privacidad

- `EnableSensitiveData = false`: no habilita captura de prompts ni completions.
- No registra el texto de solicitudes o respuestas desde el codigo del sample.
- Desactiva la exportacion de logs del puente OpenTelemetry del host.
- No habilita captura de excepciones en ASP.NET Core ni HttpClient.
- Desactiva el almacenamiento offline del exporter.
- Un processor se registra antes de los exporters. Solo conserva tags numericos
  de codigo HTTP, puerto y cantidad de tokens. Elimina prompts, completions,
  argumentos de tools, tokens de autenticacion, URLs completas y sus queries,
  nombres de modelos/agentes, exception messages y baggage.
- Reemplaza el nombre visible por uno fijo y borra la descripcion de estado.
  Conserva trace/span IDs, duracion, tipo de span y estado.
- Descarta por completo spans con eventos o links, ya que Activity no permite
  quitar sus atributos mediante APIs publicas. Asi evita exportar cuerpos
  de excepciones o contenido de modelos en esas estructuras.
- No pongas datos sensibles en la configuracion de recursos del host, identidades
  de servicio o endpoints de ingestion. El processor no anonimiza recursos.

## Verificacion offline y limite de validacion

Desde `public-example`:

```powershell
dotnet run --project .\tests\HostedBasicsChecks\HostedBasicsChecks.csproj
```

El check valida configuracion, crea el exporter real y captura su payload con
un transporte HTTP simulado que bloquea destinos no simulados.
Ejecuta HTTP real contra un servidor ASP.NET Core loopback para comprobar
RequestData y RemoteDependencyData. Verifica la eliminacion de datos sensibles
del payload, tanto con exporter explicito como con el integrado del host,
y que este ultimo exporte exactamente una copia de un span.
No prueba ingestion en Azure ni visibilidad en el portal.
**El deployment no esta validado.** Inferencia y telemetry reales pueden generar costos.
