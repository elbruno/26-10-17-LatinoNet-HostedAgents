# 21: AI Search con acceso acotado y referencias

La tool busca hasta tres documentos usando identidad, devuelve extractos de
hasta 2000 caracteres y referencias `SourceId`, título y URL HTTPS.
El backend fija `group_ids/any(g: g eq '<grupo>')` con escape OData.
El modelo recibe solo la consulta, nunca un filtro o grupo que pueda modificar.

Configurar `FOUNDRY_PROJECT_ENDPOINT`, `AZURE_AI_MODEL_DEPLOYMENT_NAME`,
`AZURE_SEARCH_ENDPOINT`, `AZURE_SEARCH_INDEX` y `SEARCH_ALLOWED_GROUP`.
Preparar un índice con estos campos:

| Campo | Tipo / atributos |
|---|---|
| `id` | Edm.String, key, retrievable |
| `title` | Edm.String, searchable, retrievable |
| `content` | Edm.String, searchable, retrievable |
| `url` | Edm.String, retrievable, URL HTTPS autorizada |
| `group_ids` | Collection(Edm.String), filterable |

El administrador del índice controla las ACL y los enlaces. Los documentos son
datos no confiables, no instrucciones. El prompt pide referencias y honestidad
si no hay resultados, pero no garantiza calidad ni neutraliza toda prompt injection.
No se implementa ingesta, embeddings ni ranking semántico en este ejemplo.

La identidad requiere Search Index Data Reader en el servicio de búsqueda.
Es una demo para **un único grupo fijo de usuarios autorizados**.
No inferir permisos a partir del texto del usuario. En producción, autenticar
cada solicitud y resolver sus grupos desde claims confiables en el backend,
además de aislar sesiones. No exponer este host a usuarios de otros grupos.

```powershell
# Desde la raíz pública, después de configurar las variables:
dotnet run --project .\samples\21-ai-search-hosted-agent
# En otra terminal:
Invoke-RestMethod -Method Post -Uri "http://localhost:8088/responses" `
    -ContentType "application/json" `
    -Body '{"input":"Busca documentos sobre hosting y cita las fuentes."}'
```

Consultar [ejecución y despliegue común](../../README.md#continuación-implementada-11-12-y-20-a-24).
El manifiesto no crea el servicio ni su índice y no asigna roles. Las pruebas
offline verifican el filtro emitido, límites y referencias con transporte simulado,
no acceso a Search real ni inferencia o despliegue Azure.
