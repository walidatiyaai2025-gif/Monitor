[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishPath,

    [string]$PackagePath,

    [string]$ChecksumPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$publish = [System.IO.Path]::GetFullPath($PublishPath)
Assert-Condition (Test-Path -LiteralPath $publish -PathType Container) "Publish directory does not exist: $publish"

$requiredFiles = @(
    'Monitor.Web.dll',
    'Monitor.Web.deps.json',
    'Monitor.Web.runtimeconfig.json',
    'web.config',
    'appsettings.json',
    '_operations/scripts/Setup-MonitorServer.ps1',
    '_operations/scripts/Install-Monitor.ps1',
    '_operations/docs/MONITOR_SERVER_SETUP.md'
)
foreach ($file in $requiredFiles) {
    Assert-Condition (Test-Path -LiteralPath (Join-Path $publish $file) -PathType Leaf) "Required production file is missing: $file"
}

$bootstrapScripts = @(
    (Join-Path $publish '_operations/scripts/Setup-MonitorServer.ps1'),
    (Join-Path $publish '_operations/scripts/Install-Monitor.ps1')
)
foreach ($scriptPath in $bootstrapScripts) {
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile(
        $scriptPath,
        [ref]$tokens,
        [ref]$errors) | Out-Null
    Assert-Condition ($errors.Count -eq 0) "Packaged bootstrap PowerShell contains parser errors: $scriptPath :: $($errors.Message -join '; ')"
}

$forbiddenFiles = @(
    'appsettings.Development.json',
    'appsettings.Production.json'
)
foreach ($file in $forbiddenFiles) {
    Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $publish $file))) "Forbidden environment-specific file is present in publish output: $file"
}

$appSettingsPath = Join-Path $publish 'appsettings.json'
$appSettings = Get-Content -LiteralPath $appSettingsPath -Raw | ConvertFrom-Json
$developmentAdminProperty = $appSettings.PSObject.Properties['DevelopmentAdmin']
Assert-Condition ($null -eq $developmentAdminProperty) 'Published appsettings.json must not contain DevelopmentAdmin credentials.'
Assert-Condition ([string]$appSettings.Deployment.Mode -eq 'SingleNode') 'Published baseline must default to Deployment:Mode=SingleNode.'
Assert-Condition ([string]$appSettings.SharedState.Provider -eq 'Disabled') 'Published baseline must keep SharedState disabled.'
Assert-Condition (-not [bool]$appSettings.HaState.UseSharedRegistrations) 'Published baseline must not enable shared registrations.'
Assert-Condition (-not [bool]$appSettings.HaState.UseSharedOperationalState) 'Published baseline must not enable shared operational state.'
Assert-Condition (-not [bool]$appSettings.Coordination.Enabled) 'Published baseline must keep distributed coordination disabled.'
Assert-Condition ([string]$appSettings.DataProtectionKeyStore.Mode -eq 'LocalFile') 'Published baseline must use LocalFile Data Protection keys for the first SingleNode release.'

$appData = Join-Path $publish 'App_Data'
if (Test-Path -LiteralPath $appData) {
    $persistedFiles = @(Get-ChildItem -LiteralPath $appData -Recurse -File -Force)
    Assert-Condition ($persistedFiles.Count -eq 0) 'Publish output must not contain persisted App_Data state.'
}

$jsonFiles = @(Get-ChildItem -LiteralPath $publish -Recurse -File -Filter '*.json')
foreach ($jsonFile in $jsonFiles) {
    $text = Get-Content -LiteralPath $jsonFile.FullName -Raw
    Assert-Condition ($text -notmatch '"ConnectionSecrets"\s*:') "Publish output contains a ConnectionSecrets configuration section: $($jsonFile.Name)"
    Assert-Condition ($text -notmatch '"DevelopmentAdmin"\s*:') "Publish output contains DevelopmentAdmin configuration: $($jsonFile.Name)"
}

if ($PackagePath) {
    $package = [System.IO.Path]::GetFullPath($PackagePath)
    Assert-Condition (Test-Path -LiteralPath $package -PathType Leaf) "Package does not exist: $package"
    Assert-Condition ([System.IO.Path]::GetExtension($package) -ieq '.zip') 'Production package must be a ZIP file.'

    if ($ChecksumPath) {
        $checksumFile = [System.IO.Path]::GetFullPath($ChecksumPath)
        Assert-Condition (Test-Path -LiteralPath $checksumFile -PathType Leaf) "Checksum file does not exist: $checksumFile"
        $expectedLine = (Get-Content -LiteralPath $checksumFile -Raw).Trim()
        $expected = ($expectedLine -split '\s+')[0].ToUpperInvariant()
        $actual = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToUpperInvariant()
        Assert-Condition ($expected -eq $actual) "SHA-256 mismatch. Expected $expected but calculated $actual."
    }
}

[pscustomobject]@{
    PublishPath = $publish
    Files = @(Get-ChildItem -LiteralPath $publish -Recurse -File).Count
    SingleNode = $true
    DevelopmentCredentialPublished = $false
    PersistedStatePublished = $false
    PackageValidated = [bool]$PackagePath
} | Format-List
