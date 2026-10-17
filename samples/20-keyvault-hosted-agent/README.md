# 20: Key Vault y credenciales solo en el backend

Una tool obtiene del vault la API key de un catálogo externo y realiza un POST
a su URL HTTPS fija. El modelo solo recibe asistentes, precio unitario, total,
moneda USD e indicador de demo. Nunca recibe el secreto ni el JSON crudo de la API.
No hay cobros ni reservas. Si el destino admite identidad administrada, usarla
directamente en lugar de introducir una API key o Key Vault innecesarios.

Configurar `FOUNDRY_PROJECT_ENDPOINT`, `AZURE_AI_MODEL_DEPLOYMENT_NAME`,
`KEY_VAULT_URI` (`https://<vault>.vault.azure.net`),
`WORKSHOP_API_SECRET_NAME` y `WORKSHOP_API_ENDPOINT` (URL HTTPS completa del
endpoint de presupuesto, sin query, fragmento ni credenciales).

El catálogo es una **dependencia externa que debes proporcionar**, no un servicio
incluido ni un endpoint Azure inventado. Contrato de demo: POST con JSON
`{"attendees":4}`, header `X-Api-Key` con la credencial leída de Key Vault,
respuesta `{"pricePerPerson":12.50}` en USD. El backend valida asistentes de
1 a 1000, precio de 0 a 10000 y respuesta de hasta 4096 bytes. No sigue redirects
para evitar reenviar la credencial a otro destino. Configurar solo una API
de confianza administrada por ti. No pasar la URL o el nombre del secreto como
argumentos de la tool.

La identidad efectiva requiere Key Vault Secrets User, con el menor alcance
necesario. Revisar también firewall, red privada, acceso de inferencia Foundry
y la política de autenticación de la API. La identidad del host no obtiene esos
permisos automáticamente.

Desde la raíz del repositorio:

```powershell
dotnet run --project .\samples\20-keyvault-hosted-agent
```

En otra terminal, con el puerto 8088 solo local:

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:8088/responses" `
    -ContentType "application/json" `
    -Body '{"input":"Consulta el presupuesto ficticio para 4 asistentes."}'
```

Consultar [ejecución y despliegue común](../../README.md#continuación-implementada-11-12-y-20-a-24).
El manifiesto no crea el vault, secreto ni catálogo. Las pruebas usan transports
simulados, no prueban lectura de un secreto real ni inferencia o despliegue Azure.
