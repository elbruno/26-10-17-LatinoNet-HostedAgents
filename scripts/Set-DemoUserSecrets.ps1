#requires -Version 7.0
# Configurar los valores Foundry de todos los proyectos, sin escribirlos en Git.
# A. Descubrir proyectos con UserSecretsId y pedir endpoint/deployment una sola vez.
# B. Validar ambos valores y enviarlos a dotnet user-secrets por stdin.
# C. Conservar las demas claves; este script no configura azd ni concede permisos.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$dotnet = Get-Command dotnet -ErrorAction Stop
$samplesRoot = Join-Path $PSScriptRoot '..\samples'
$projects = @(
    Get-ChildItem $samplesRoot -Filter *.csproj -Recurse -File |
        Sort-Object FullName |
        Where-Object {
            [xml]$projectXml = Get-Content $_.FullName -Raw
            @($projectXml.SelectNodes('/Project/PropertyGroup/UserSecretsId') |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_.InnerText) }).Count -gt 0
        }
)
if ($projects.Count -eq 0) {
    throw 'No hay proyectos de demo con UserSecretsId bajo samples. No se modifico ningun proyecto.'
}

Write-Host "Se configuraran $($projects.Count) proyectos con User Secrets."
Write-Host 'Usar el endpoint del proyecto Foundry y el nombre del deployment, no el nombre del modelo del catalogo.'
$endpoint = (Read-Host 'Endpoint del proyecto Foundry (https://.../api/projects/...)').Trim()
$defaultDeployment = 'gpt-6.1-sol'
$deployment = (Read-Host "Nombre del deployment del modelo [$defaultDeployment] (Enter para usarlo)").Trim()
if ([string]::IsNullOrWhiteSpace($deployment)) {
    $deployment = $defaultDeployment
}

$uri = $null
if (-not [Uri]::TryCreate($endpoint, [UriKind]::Absolute, [ref]$uri) -or
    $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or
    $uri.AbsolutePath -notmatch '^/api/projects/[^/]+/?$' -or
    $uri.Host -match '[<>]' -or $uri.AbsolutePath -match '(?i)%3[ce]|[<>]') {
    throw 'Endpoint invalido: usar HTTPS y la ruta /api/projects/<proyecto>, sin credenciales, query ni fragmento. No se guardaron valores.'
}
if ([string]::IsNullOrWhiteSpace($deployment) -or $deployment -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') {
    throw 'Deployment invalido: usar su nombre real con letras, numeros, puntos, guiones o guiones bajos. No se guardaron valores.'
}

$values = @{
    FOUNDRY_PROJECT_ENDPOINT = $endpoint
    AZURE_AI_MODEL_DEPLOYMENT_NAME = $deployment
} | ConvertTo-Json -Compress

foreach ($project in $projects) {
    $values | & $dotnet user-secrets set --project $project.FullName | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "No se pudieron guardar User Secrets para $($project.Name). Revisar los proyectos ya configurados; no se revirtieron sus valores."
    }
    Write-Host "Configurado: $($project.FullName)"
}

Write-Host 'Listo. Los demas secretos existentes se conservan. No se escribieron valores en el repositorio.'
Write-Host 'No se necesita API key: las consolas usan AzureCliCredential. Autenticar con az login antes de invocar el modelo.'
Write-Host 'Las variables de entorno tienen prioridad sobre User Secrets. Este script no configura el entorno de despliegue de azd.'
