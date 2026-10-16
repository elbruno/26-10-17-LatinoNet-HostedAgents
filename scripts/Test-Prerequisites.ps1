#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$manifest = Get-Content (Join-Path $PSScriptRoot '..\azure.yaml') -Raw
$azdRequirement = [regex]::Match($manifest, "(?m)^  azd: '>=([^']+)'\r?$")
$agentRequirement = [regex]::Match($manifest, "(?m)^    azure\.ai\.agents: '>=([^']+)'\r?$")
if (-not $azdRequirement.Success -or -not $agentRequirement.Success) {
    throw 'No se pudieron leer los requisitos de azure.yaml. Revisar el manifiesto antes de continuar.'
}

$azd = Get-Command azd -ErrorAction Stop
$versionOutput = (& $azd version) -join "`n"
if ($LASTEXITCODE -ne 0) {
    throw 'azd version fallo.'
}
$versionMatch = [regex]::Match($versionOutput, 'azd version (\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)')
if (-not $versionMatch.Success) {
    throw 'No se pudo identificar la version instalada de azd.'
}
$azdVersion = [semver]$versionMatch.Groups[1].Value
if ($azdVersion -lt [semver]$azdRequirement.Groups[1].Value) {
    throw "azd $azdVersion no cumple >=$($azdRequirement.Groups[1].Value). No se modificaron herramientas."
}

$extensionOutput = (& $azd extension list --installed --output json) -join "`n"
if ($LASTEXITCODE -ne 0) {
    throw 'No se pudieron consultar las extensiones instaladas de azd.'
}
$extensions = $extensionOutput | ConvertFrom-Json -ErrorAction Stop
$agent = @($extensions | Where-Object id -EQ 'azure.ai.agents')
if ($agent.Count -ne 1 -or -not $agent[0].installedVersion) {
    throw 'azure.ai.agents no esta instalada o no informa installedVersion.'
}
$agentVersion = [semver]$agent[0].installedVersion
if ($agentVersion -lt [semver]$agentRequirement.Groups[1].Value) {
    throw "azure.ai.agents instalada: $agentVersion; requiere >=$($agentRequirement.Groups[1].Value). El campo version del catalogo no es la version instalada. No se modificaron herramientas."
}

$sdks = & dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^10\.\d+\.\d+ \[')) {
    throw 'Se requiere un SDK estable de .NET 10 compatible con global.json.'
}

Write-Output "Requisitos de version cumplidos: azd $azdVersion, azure.ai.agents $agentVersion y SDK .NET 10."
Write-Output 'Esto no valida autenticacion, permisos, disponibilidad regional ni despliegue. Ejecutar azd ai agent doctor en el entorno elegido.'
