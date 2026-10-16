#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    if (Test-Path .\samples\dotnet) {
        throw 'Las demos deben estar directamente bajo samples, sin carpeta de lenguaje.'
    }
    [xml]$solution = Get-Content .\LatinoNet.HostedAgents.slnx -Raw
    foreach ($project in $solution.SelectNodes('//Project')) {
        if (-not (Test-Path -LiteralPath $project.Path.Replace('/', '\'))) {
            throw "Ruta de proyecto invalida en la solucion: $($project.Path)"
        }
    }
    $manifest = Get-Content .\azure.yaml -Raw
    $hostedPath = [regex]::Match($manifest, '(?m)^\s+project:\s*(\S+)\s*$')
    if (-not $hostedPath.Success -or
        $hostedPath.Groups[1].Value -cne 'samples/hello-hosted-agent' -or
        -not (Test-Path .\samples\hello-hosted-agent\Dockerfile)) {
        throw 'El manifiesto debe apuntar a samples/hello-hosted-agent y su Dockerfile.'
    }

    dotnet build .\LatinoNet.HostedAgents.slnx --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Fallo el build de la serie.' }

    foreach ($test in @('Test-Prerequisites.Tests.ps1', 'Set-DemoUserSecrets.Tests.ps1')) {
        pwsh -NoProfile -File (Join-Path '.\tests' $test)
        if ($LASTEXITCODE -ne 0) { throw "Fallo $test." }
    }

    dotnet run --project .\tests\DemoChecks\DemoChecks.csproj --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron los checks offline de MAF.' }

    $baseline = Get-Content .\samples\01-demo\Program.cs -Raw
    $hosted = Get-Content .\samples\hello-hosted-agent\Program.cs -Raw
    $baselineAgent = [regex]::Match($baseline, '(?s)var configuration.*?(?=\r?\nConsole\.WriteLine)')
    $hostedAgent = [regex]::Match($hosted, '(?s)var configuration.*?(?=\r?\nvar builder)')
    if (-not $baselineAgent.Success -or -not $hostedAgent.Success -or
        $baselineAgent.Value.Trim() -cne $hostedAgent.Value.Trim()) {
        throw 'El agente base de 01-demo y el Hosted Agent ya no coinciden. Revisar la historia del cambio minimo.'
    }
    Write-Output 'PASS: la configuracion y construccion del agente de 01-demo se conservan en Hosted.'
    Write-Output 'Estos checks no invocan Azure ni certifican inferencia, permisos o despliegue.'
} finally {
    Pop-Location
}
