[CmdletBinding()]
param(
    [string]$StateRoot = "$env:ProgramData\Monitor\upgrades",
    [string]$InstallRoot = "$env:ProgramFiles\Monitor",
    [string]$ServiceName = "Monitor",
    [string]$HealthUrl = "http://127.0.0.1:5080/health/ready",
    [int]$HealthTimeoutSeconds = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-ResultFile {
    param([string]$Version,[string]$Status,[string]$Message)
    $result = [ordered]@{
        version = $Version
        status = $Status
        message = $Message
        completedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    }
    $path = Join-Path $StateRoot 'last-upgrade-result.json'
    $temp = "$path.tmp-$([Guid]::NewGuid().ToString('N'))"
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $temp -Encoding UTF8
    Move-Item -LiteralPath $temp -Destination $path -Force
}

function Invoke-RobocopyChecked {
    param([string]$Source,[string]$Destination,[switch]$Mirror,[string[]]$ExcludeDirectories = @(),[string[]]$ExcludeFiles = @())
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $args = @($Source,$Destination,'/R:2','/W:2','/NFL','/NDL','/NJH','/NJS','/NP','/COPY:DAT','/DCOPY:DAT')
    if ($Mirror) { $args += '/MIR' } else { $args += '/E' }
    if ($ExcludeDirectories.Count -gt 0) { $args += '/XD'; $args += $ExcludeDirectories }
    if ($ExcludeFiles.Count -gt 0) { $args += '/XF'; $args += $ExcludeFiles }
    & robocopy @args | Out-Host
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
}

function Test-Health {
    param([string]$Url,[int]$TimeoutSeconds)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return $true }
        } catch { }
        Start-Sleep -Seconds 2
    }
    return $false
}

function Assert-SafePackage {
    param([string]$ZipPath,[string]$ExpectedHash)
    $resolved = (Resolve-Path -LiteralPath $ZipPath).Path
    $stagedRoot = [IO.Path]::GetFullPath((Join-Path $StateRoot 'staged'))
    if (-not $resolved.StartsWith($stagedRoot,[StringComparison]::OrdinalIgnoreCase)) {
        throw 'Pending package is outside the approved staged package root.'
    }
    $actual = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actual -ne $ExpectedHash.ToUpperInvariant()) { throw 'Pending package SHA-256 verification failed.' }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($resolved)
    try {
        if ($archive.Entries.Count -eq 0 -or $archive.Entries.Count -gt 5000) { throw 'Upgrade archive entry count is invalid.' }
        $manifest = $null
        $hasAppPayload = $false
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\','/')
            if ([string]::IsNullOrWhiteSpace($name) -or $name.StartsWith('/') -or $name.Contains('../') -or [IO.Path]::IsPathRooted($name)) {
                throw "Unsafe archive entry: $name"
            }
            if ($name -ieq 'monitor-upgrade-manifest.json') { $manifest = $entry }
            if ($name.StartsWith('app/',[StringComparison]::OrdinalIgnoreCase) -and -not $name.EndsWith('/')) { $hasAppPayload = $true }
        }
        if ($null -eq $manifest) { throw 'monitor-upgrade-manifest.json is missing.' }
        if (-not $hasAppPayload) { throw 'Upgrade package must contain an app/ payload.' }
    } finally { $archive.Dispose() }
    return $resolved
}

$pendingPath = Join-Path $StateRoot 'pending-upgrade.json'
if (-not (Test-Path -LiteralPath $pendingPath)) { exit 0 }

$pending = Get-Content -LiteralPath $pendingPath -Raw | ConvertFrom-Json
$version = [string]$pending.package.version
$packagePath = [string]$pending.package.packagePath
$expectedHash = [string]$pending.package.sha256
if ([string]::IsNullOrWhiteSpace($version) -or [string]::IsNullOrWhiteSpace($packagePath) -or [string]::IsNullOrWhiteSpace($expectedHash)) {
    Write-ResultFile -Version ($version ?? 'unknown') -Status 'Rejected' -Message 'Pending upgrade request is invalid.'
    throw 'Pending upgrade request is invalid.'
}

$workRoot = Join-Path $StateRoot ("work\" + [Guid]::NewGuid().ToString('N'))
$extractRoot = Join-Path $workRoot 'extract'
$backupRoot = Join-Path $StateRoot ("backups\{0}-{1}" -f $version,[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path $extractRoot,$backupRoot | Out-Null
$serviceWasRunning = $false

try {
    $safePackage = Assert-SafePackage -ZipPath $packagePath -ExpectedHash $expectedHash
    Expand-Archive -LiteralPath $safePackage -DestinationPath $extractRoot -Force
    $payloadRoot = Join-Path $extractRoot 'app'
    if (-not (Test-Path -LiteralPath (Join-Path $payloadRoot 'Monitor.Web.dll'))) { throw 'app/ payload does not contain Monitor.Web.dll.' }

    if (Test-Path -LiteralPath $InstallRoot) {
        Invoke-RobocopyChecked -Source $InstallRoot -Destination $backupRoot
    }

    $service = Get-Service -Name $ServiceName -ErrorAction Stop
    $serviceWasRunning = $service.Status -eq 'Running'
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $ServiceName -Force
        (Get-Service -Name $ServiceName).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30))
    }

    # Preserve machine-local operational state and production overrides while replacing application binaries.
    Invoke-RobocopyChecked -Source $payloadRoot -Destination $InstallRoot -Mirror -ExcludeDirectories @('App_Data','data','logs') -ExcludeFiles @('appsettings.Production.json')

    Start-Service -Name $ServiceName
    (Get-Service -Name $ServiceName).WaitForStatus('Running',[TimeSpan]::FromSeconds(30))
    if (-not (Test-Health -Url $HealthUrl -TimeoutSeconds $HealthTimeoutSeconds)) {
        throw "Monitor did not become ready at $HealthUrl within $HealthTimeoutSeconds seconds."
    }

    Write-ResultFile -Version $version -Status 'Applied' -Message 'Upgrade applied successfully and readiness health check passed.'
    Remove-Item -LiteralPath $pendingPath -Force
    exit 0
}
catch {
    $failure = $_.Exception.Message
    try {
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($null -ne $service -and $service.Status -ne 'Stopped') { Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue }
        if (Test-Path -LiteralPath $backupRoot) {
            Invoke-RobocopyChecked -Source $backupRoot -Destination $InstallRoot -Mirror -ExcludeDirectories @('App_Data','data','logs') -ExcludeFiles @('appsettings.Production.json')
        }
        if ($null -ne $service -and $serviceWasRunning) { Start-Service -Name $ServiceName -ErrorAction SilentlyContinue }
        Write-ResultFile -Version $version -Status 'RolledBack' -Message ("Upgrade failed and rollback was attempted. Reason: " + $failure)
    } catch {
        Write-ResultFile -Version $version -Status 'RollbackFailed' -Message ("Upgrade failed: " + $failure + "; rollback also failed: " + $_.Exception.Message)
    }
    Remove-Item -LiteralPath $pendingPath -Force -ErrorAction SilentlyContinue
    throw
}
finally {
    Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
}
