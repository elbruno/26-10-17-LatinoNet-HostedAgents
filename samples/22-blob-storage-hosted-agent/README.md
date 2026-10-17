# 22: documentos de Blob Storage, solo lectura

La tool lee archivos UTF-8 de un único contenedor configurado por el backend.
Solo acepta nombres simples `.txt` (letras, números, guion y guion bajo),
sin rutas ni URLs, MIME `text/plain` y un máximo de **16384 bytes**.
Comprueba tanto el tamaño declarado como los bytes leídos y rechaza UTF-8 inválido.
No lista, sube, borra ni modifica blobs.

Configurar `FOUNDRY_PROJECT_ENDPOINT`, `AZURE_AI_MODEL_DEPLOYMENT_NAME` y
`BLOB_CONTAINER_URI` (`https://<account>.blob.core.windows.net/<container>`,
sin SAS ni query). Subir previamente un documento de demo como `agenda.txt`
con `Content-Type: text/plain; charset=utf-8`.
El nombre de deployment es opcional: si falta, usa `gpt-6-luna` ya existente.
Un valor explícito en configuración conserva prioridad.

La identidad necesita Storage Blob Data Reader con alcance del contenedor
autorizado. No usar contenedores de información privada para esta demo ni
exponer el host a usuarios sin permiso sobre todos sus documentos. El ejemplo
no resuelve autorización por usuario ni aislamiento multiempresa.
El contenido se trata como datos no confiables y el prompt pide citar el nombre
del archivo. Esas instrucciones no son una garantía contra prompt injection.

```powershell
# Desde la raíz pública:
dotnet run --project .\samples\22-blob-storage-hosted-agent
# En otra terminal:
Invoke-RestMethod -Method Post -Uri "http://localhost:8088/responses" `
    -ContentType "application/json" `
    -Body '{"input":"Lee agenda.txt y resume su contenido citando el documento."}'
```

Consultar [ejecución y despliegue común](../../README.md#continuación-implementada-11-12-y-20-a-24).
El manifiesto no crea la cuenta ni el contenedor, y no asigna roles.
Los tests offline cubren nombres, MIME, UTF-8 y límites, no lectura de Azure real
ni inferencia o despliegue.
