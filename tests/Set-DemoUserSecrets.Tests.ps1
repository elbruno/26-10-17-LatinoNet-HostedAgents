#requires -Version 7.0
# Tests de configuracion: no escriben secretos reales del usuario.
# A. Preparar proyectos temporales y respuestas de entrada simuladas.
# B. Verificar stdin, validaciones, valores por defecto y propagacion de fallos.
# C. Comprobar descubrimiento e IDs unicos de los diecisiete samples.
$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString())
$state = @{
    answers = [Collections.Generic.Queue[string]]::new()
    prompts = [Collections.Generic.List[string]]::new()
    calls = [Collections.Generic.List[object]]::new()
    fail = $false
}

Set-Item Function:Read-Host -Value ({
    param([string]$Prompt, [switch]$MaskInput)
    if ($MaskInput) { throw 'Los campos no deben ocultarse.' }
    $state.prompts.Add($Prompt)
    return $state.answers.Dequeue()
}.GetNewClosure())

Set-Item Function:dotnet -Value ({
    $payload = @($input) -join "`n"
    $state.calls.Add(@{ arguments = @($args); payload = $payload })
    $global:LASTEXITCODE = if ($state.fail) { 1 } else { 0 }
}.GetNewClosure())

function Set-Answers {
    param([string]$Endpoint, [string]$Deployment)
    $state.answers.Clear()
    $state.prompts.Clear()
    $state.answers.Enqueue($Endpoint)
    $state.answers.Enqueue($Deployment)
    $state.calls.Clear()
    $state.fail = $false
}

function Invoke-Setup {
    param([bool]$ShouldPass, [string]$ErrorText = '')
    $failure = $null
    try {
        & (Join-Path $fixture 'scripts\Set-DemoUserSecrets.ps1') 6>$null
    } catch {
        $failure = $_
    }
    if ($ShouldPass -and $failure) { throw "Fallo inesperado: $failure" }
    if (-not $ShouldPass -and ($null -eq $failure -or "$failure" -notlike "*$ErrorText*")) {
        throw "No se obtuvo el error esperado: $failure"
    }
}

try {
    New-Item (Join-Path $fixture 'scripts') -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $PSScriptRoot '..\scripts\Set-DemoUserSecrets.ps1') (Join-Path $fixture 'scripts')
    foreach ($name in @('first', 'second', 'without-secrets')) {
        $folder = Join-Path $fixture "samples\$name"
        New-Item $folder -ItemType Directory -Force | Out-Null
        $content = if ($name -eq 'without-secrets') {
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>'
        } else {
            "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><UserSecretsId>test-$name</UserSecretsId></PropertyGroup></Project>"
        }
        [IO.File]::WriteAllText((Join-Path $folder "$name.csproj"), $content)
    }

    Set-Answers ' https://example.invalid/api/projects/demo ' ' demo-model '
    Invoke-Setup $true
    if ($state.calls.Count -ne 2 -or $state.answers.Count -ne 0 -or
        $state.prompts.Count -ne 2 -or
        $state.prompts[0] -notmatch 'Endpoint del proyecto Foundry' -or
        $state.prompts[1] -notmatch 'Nombre del deployment.*\[gpt-6\.1-sol\]') {
        throw 'Se debe preguntar una vez y configurar solo los dos proyectos con UserSecretsId.'
    }
    foreach ($call in $state.calls) {
        $values = $call.payload | ConvertFrom-Json
        if ($values.FOUNDRY_PROJECT_ENDPOINT -ne 'https://example.invalid/api/projects/demo' -or
            $values.AZURE_AI_MODEL_DEPLOYMENT_NAME -ne 'demo-model') {
            throw 'Los valores enviados por stdin no coinciden.'
        }
        if (($call.arguments -join ' ') -match 'example.invalid|demo-model' -or
            ($call.arguments[0..2] -join ' ') -ne 'user-secrets set --project') {
            throw 'Argumentos inesperados o valores expuestos en la linea de comandos.'
        }
    }
    Write-Output 'PASS: dos campos visibles, stdin y proyecto sin secretos omitido'

    foreach ($endpoint in @(
        'http://example.invalid/api/projects/demo',
        'https://example.invalid/api/projects/demo?token=test',
        'https://user:pass@example.invalid/api/projects/demo',
        'https://example.invalid/not-a-project',
        'https://example.invalid/api/projects/%3Cproject%3E'
    )) {
        Set-Answers $endpoint 'demo-model'
        Invoke-Setup $false 'Endpoint invalido'
        if ($state.calls.Count -ne 0) { throw 'Se escribieron valores antes de validar.' }
    }
    Write-Output 'PASS: cinco endpoints invalidos rechazados sin escrituras'

    foreach ($deployment in @('', '   ')) {
        Set-Answers 'https://example.invalid/api/projects/demo' $deployment
        Invoke-Setup $true
        if ($state.calls.Count -ne 2) { throw 'No se configuraron ambos proyectos con el deployment por defecto.' }
        foreach ($call in $state.calls) {
            $values = $call.payload | ConvertFrom-Json
            if ($values.AZURE_AI_MODEL_DEPLOYMENT_NAME -ne 'gpt-6.1-sol') {
                throw 'Enter o espacios deben seleccionar gpt-6.1-sol.'
            }
        }
    }
    Write-Output 'PASS: Enter o espacios seleccionan gpt-6.1-sol en todos los proyectos'

    Set-Answers 'https://example.invalid/api/projects/demo' 'invalid deployment'
    Invoke-Setup $false 'Deployment invalido'
    if ($state.calls.Count -ne 0) { throw 'Se escribio un deployment invalido.' }
    Write-Output 'PASS: deployment invalido rechazado'

    Set-Answers 'https://example.invalid/api/projects/demo' 'demo-model'
    $state.fail = $true
    Invoke-Setup $false 'No se pudieron guardar'
    if ($state.calls.Count -ne 1) { throw 'Se continuo tras una escritura fallida.' }
    Write-Output 'PASS: fallo de dotnet explicito y sin continuar con otros proyectos'

    foreach ($name in @('first', 'second')) {
        [IO.File]::WriteAllText(
            (Join-Path $fixture "samples\$name\$name.csproj"),
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
    }
    Set-Answers 'https://example.invalid/api/projects/demo' 'demo-model'
    Invoke-Setup $false 'No hay proyectos'
    if ($state.calls.Count -ne 0 -or $state.answers.Count -ne 2) {
        throw 'No se debe preguntar ni escribir si no hay proyectos configurables.'
    }
    Write-Output 'PASS: sin proyectos con UserSecretsId, error antes de preguntar'
} finally {
    Remove-Item -LiteralPath $fixture -Recurse -Force
}

Set-Answers 'https://example.invalid/api/projects/demo' 'demo-model'
& (Join-Path $PSScriptRoot '..\scripts\Set-DemoUserSecrets.ps1') 6>$null
$projects = @(Get-ChildItem (Join-Path $PSScriptRoot '..\samples') -Filter *.csproj -Recurse -File)
if ($projects.Count -ne 17 -or $state.calls.Count -ne $projects.Count -or $state.prompts.Count -ne 2) {
    throw 'La serie debe configurar los nueve proyectos de consola y ocho Hosted Agents con dos preguntas.'
}
foreach ($project in $projects) {
    if ($project.Directory.Parent.FullName -ne (Resolve-Path (Join-Path $PSScriptRoot '..\samples')).Path) {
        throw "Las demos deben estar directamente bajo samples: $($project.FullName)"
    }
    if (-not ($state.calls | Where-Object { $_.arguments -contains $project.FullName })) {
        throw "No se configuro el proyecto de la serie: $($project.Name)"
    }
}
$ids = @($projects | ForEach-Object { ([xml](Get-Content $_.FullName -Raw)).Project.PropertyGroup.UserSecretsId })
if (@($ids | Sort-Object -Unique).Count -ne 17 -or @($ids | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -ne 0) {
    throw 'Los diecisiete samples deben tener UserSecretsId propios y unicos.'
}
Write-Output 'PASS: descubrimiento real de los diecisiete proyectos, sin escribir secretos reales'
