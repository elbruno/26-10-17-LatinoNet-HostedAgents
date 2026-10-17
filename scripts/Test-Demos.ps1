#requires -Version 7.0
# Validacion offline de la serie, sin inferencia ni recursos Azure.
# A. Comprobar rutas, nombres de proyectos y configuracion de hosting.
# B. Compilar y ejecutar tests de PowerShell y los tres harnesses .NET.
# C. Confirmar que 01 y 10 conservan el mismo agente, salvo la credencial del runtime.
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
        $hostedPath.Groups[1].Value -cne 'samples/10-hello-hosted-agent' -or
        -not (Test-Path .\samples\10-hello-hosted-agent\Dockerfile)) {
        throw 'El manifiesto debe apuntar a samples/10-hello-hosted-agent y su Dockerfile.'
    }
    $sampleProjects = @(Get-ChildItem .\samples -Filter *.csproj -Recurse -File)
    if ($sampleProjects.Count -ne 17) { throw 'Se esperan nueve consolas y ocho Hosted Agents.' }
    foreach ($project in $sampleProjects) {
        if ($project.BaseName -cne $project.Directory.Name -or
            $project.BaseName -notmatch '^\d{2}-[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$') {
            throw "Cada carpeta y proyecto de sample deben compartir numero y descripcion: $($project.FullName)"
        }
    }
    $teachingFiles = @(Get-ChildItem .\samples, .\scripts, .\tests -Recurse -File |
        Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj|\.azure)[\\/]' -and
            ($_.Extension -in @('.cs', '.csproj', '.ps1', '.yaml') -or $_.Name -eq 'Dockerfile')
        }) + @(Get-Item .\azure.yaml, .\LatinoNet.HostedAgents.slnx)
    foreach ($file in $teachingFiles) {
        $header = (Get-Content -LiteralPath $file.FullName -TotalCount 8) -join "`n"
        if ($header -notmatch '\bA[.:)]' -or $header -notmatch '\bB[.:)]' -or $header -notmatch '\bC[.:)]') {
            throw "Falta encabezado didactico con pasos A/B/C: $($file.FullName)"
        }
    }
    $entryPoint = [regex]::Match($manifest, '(?m)^\s+entryPoint:\s*(\S+)\s*$')
    $dockerfile = Get-Content .\samples\10-hello-hosted-agent\Dockerfile -Raw
    if ($entryPoint.Groups[1].Value -cne '10-hello-hosted-agent.dll' -or
        $dockerfile -notmatch 'ENTRYPOINT \["dotnet", "10-hello-hosted-agent\.dll"\]') {
        throw 'El manifiesto y Dockerfile deben ejecutar 10-hello-hosted-agent.dll.'
    }

    dotnet build .\LatinoNet.HostedAgents.slnx --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Fallo el build de la serie.' }

    foreach ($test in @('Test-Prerequisites.Tests.ps1', 'Set-DemoUserSecrets.Tests.ps1')) {
        pwsh -NoProfile -File (Join-Path '.\tests' $test)
        if ($LASTEXITCODE -ne 0) { throw "Fallo $test." }
    }

    foreach ($harness in @('DemoChecks', 'AzureToolChecks', 'HostedBasicsChecks')) {
        dotnet run --project ".\tests\$harness\$harness.csproj" --configuration Release --no-build
        if ($LASTEXITCODE -ne 0) { throw "Fallaron los checks offline de $harness." }
    }
    foreach ($project in $sampleProjects | Where-Object { $_.BaseName -match '^(11|12|2[0-4])-' }) {
        $name = $project.BaseName
        $serviceManifest = Join-Path $project.Directory.FullName 'azure.yaml'
        $containerFile = Join-Path $project.Directory.FullName 'Dockerfile'
        if (-not (Test-Path $serviceManifest) -or -not (Test-Path $containerFile)) {
            throw "Falta configuracion independiente de despliegue para $name."
        }
        $service = Get-Content $serviceManifest -Raw
        $container = Get-Content $containerFile -Raw
        if ($service -notmatch "entryPoint:\s*$([regex]::Escape($name))\.dll" -or
            $container -notmatch "ENTRYPOINT \[`"dotnet`", `"$([regex]::Escape($name))\.dll`"\]") {
            throw "Entry point inconsistente para $name."
        }
    }

    $baseline = Get-Content .\samples\01-basic-MAF-agent\Program.cs -Raw
    $hosted = Get-Content .\samples\10-hello-hosted-agent\Program.cs -Raw
    # Los comentarios didacticos son distintos; el contrato comparado es el codigo ejecutable.
    $baseline = [regex]::Replace($baseline, '(?m)^\s*//[^\r\n]*(?:\r?\n|$)', '')
    $hosted = [regex]::Replace($hosted, '(?m)^\s*//[^\r\n]*(?:\r?\n|$)', '')
    $baselineAgent = [regex]::Match($baseline, '(?s)var configuration.*?(?=\r?\nConsole\.WriteLine)')
    $hostedAgent = [regex]::Match($hosted, '(?s)var configuration.*?(?=\r?\nvar builder)')
    $consoles = @(Get-ChildItem .\samples -Directory | Where-Object Name -Match '^0[1-9]-')
    if ($consoles.Count -ne 9) {
        throw 'La serie debe conservar las nueve consolas numeradas de 01 a 09.'
    }
    foreach ($demo in $consoles) {
        $source = Get-Content (Join-Path $demo.FullName 'Program.cs') -Raw
        if ($source -notmatch 'new AzureCliCredential\(\)' -or $source -match 'DefaultAzureCredential') {
            throw "La consola $($demo.Name) debe usar exclusivamente AzureCliCredential."
        }
    }
    if ($hosted -notmatch 'new DefaultAzureCredential\(\)') {
        throw 'El contenedor Hosted debe conservar autenticacion compatible con su identidad.'
    }
    if (-not $baselineAgent.Success -or -not $hostedAgent.Success -or
        $baselineAgent.Value.Replace('new AzureCliCredential()', 'new DefaultAzureCredential()').Trim() -cne $hostedAgent.Value.Trim()) {
        throw 'El agente base de 01-basic-MAF-agent y el Hosted Agent ya no coinciden. Revisar la historia del cambio minimo.'
    }
    Write-Output 'PASS: consolas con AzureCliCredential; Hosted conserva el agente y adapta la credencial al runtime.'
    Write-Output 'Estos checks no invocan Azure ni certifican inferencia, permisos o despliegue.'
} finally {
    Pop-Location
}
