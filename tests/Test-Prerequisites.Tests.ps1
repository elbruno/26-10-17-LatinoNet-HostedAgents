#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$state = @{
    coreVersion = '1.34.1'
    installedVersion = '1.0.0-beta.16'
    sdkOutput = '10.0.401 [test-sdk]'
    extensionFailure = $false
    invalidJson = $false
}

Set-Item Function:azd -Value ({
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'version') {
        return "azd version $($state.coreVersion) (test)"
    }
    if ($state.extensionFailure) {
        $global:LASTEXITCODE = 1
        return
    }
    if ($state.invalidJson) {
        return 'not-json'
    }
    return ConvertTo-Json -InputObject @(@{
        id = 'azure.ai.agents'
        version = '1.0.0-beta.20'
        installedVersion = $state.installedVersion
    })
}.GetNewClosure())

Set-Item Function:dotnet -Value ({
    $global:LASTEXITCODE = 0
    return $state.sdkOutput
}.GetNewClosure())

function Assert-Preflight {
    param([string]$Name, [bool]$ShouldPass, [string]$ExpectedError = '')
    $failure = $null
    try {
        & (Join-Path $PSScriptRoot '..\scripts\Test-Prerequisites.ps1') | Out-Null
    } catch {
        $failure = $_
    }
    if ($ShouldPass -and $null -ne $failure) {
        throw "$Name fallo: $failure"
    }
    if (-not $ShouldPass -and ($null -eq $failure -or "$failure" -notlike "*$ExpectedError*")) {
        throw "$Name no rechazo el caso con el error esperado: $failure"
    }
    Write-Output "PASS: $Name"
}

Assert-Preflight 'Minimo beta.16' $true
$state.installedVersion = '1.0.0-beta.12'
Assert-Preflight 'Catalogo beta.20 no oculta beta.12 instalada' $false 'instalada: 1.0.0-beta.12'
$state.installedVersion = '1.0.0-beta.9'
Assert-Preflight 'Comparacion numerica de prerelease, no lexicografica' $false 'instalada: 1.0.0-beta.9'
$state.installedVersion = '1.0.0-beta.20'
Assert-Preflight 'Beta posterior' $true
$state.installedVersion = '1.0.0'
Assert-Preflight 'Version estable posterior' $true
$state.installedVersion = $null
Assert-Preflight 'Falta installedVersion' $false 'installedVersion'
$state.installedVersion = '1.0.0-beta.16'
$state.coreVersion = '1.27.0'
Assert-Preflight 'azd por debajo del minimo' $false 'no cumple'
$state.coreVersion = '1.34.1'
$state.sdkOutput = '11.0.100-rc.2 [test-sdk]'
Assert-Preflight 'Solo SDK preview de otra version' $false 'SDK estable'
$state.sdkOutput = '10.0.401 [test-sdk]'
$state.extensionFailure = $true
Assert-Preflight 'Falla del comando de extensiones' $false 'consultar las extensiones'
$state.extensionFailure = $false
$state.invalidJson = $true
Assert-Preflight 'JSON invalido' $false
