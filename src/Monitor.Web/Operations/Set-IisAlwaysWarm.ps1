#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9._-]+$')]
    [string]$SiteName = 'MonitorHealth',

    [ValidatePattern('^[A-Za-z0-9._-]+$')]
    [string]$AppPoolName = 'MonitorHealth'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) {
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

Write-Step 'Load IIS management module'
if (-not (Get-Module -ListAvailable -Name WebAdministration)) {
    throw 'WebAdministration is unavailable. Install IIS Management Scripting Tools and rerun.'
}
Import-Module WebAdministration -ErrorAction Stop

$poolPath = "IIS:\AppPools\$AppPoolName"
$sitePath = "IIS:\Sites\$SiteName"
if (-not (Test-Path -LiteralPath $poolPath)) { throw "IIS app pool '$AppPoolName' was not found." }
if (-not (Test-Path -LiteralPath $sitePath)) { throw "IIS site '$SiteName' was not found." }

Write-Step 'Ensure IIS Application Initialization support'
$getWindowsFeature = Get-Command Get-WindowsFeature -ErrorAction SilentlyContinue
if ($null -ne $getWindowsFeature) {
    $feature = Get-WindowsFeature -Name Web-AppInit
    if ($null -ne $feature -and -not $feature.Installed) {
        $result = Install-WindowsFeature -Name Web-AppInit
        if (-not $result.Success) { throw 'Failed to install the IIS Application Initialization feature.' }
        if ([string]$result.RestartNeeded -eq 'Yes') {
            Write-Warning 'Windows reports that a restart is required before Application Initialization is fully active.'
        }
        Write-Host 'Installed Web-AppInit.' -ForegroundColor Green
    } else {
        Write-Host 'Web-AppInit is already installed.' -ForegroundColor Green
    }
} else {
    Write-Warning 'Get-WindowsFeature is unavailable. Continuing with IIS warm settings; verify Application Initialization is installed on this host.'
}

Write-Step 'Configure dedicated Monitor application pool to stay warm'
Set-ItemProperty -LiteralPath $poolPath -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty -LiteralPath $poolPath -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty -LiteralPath $sitePath -Name serverAutoStart -Value $true

$appcmd = Join-Path $env:windir 'System32\inetsrv\appcmd.exe'
if (-not (Test-Path -LiteralPath $appcmd -PathType Leaf)) {
    throw "appcmd.exe was not found at '$appcmd'."
}

$preloadOutput = & $appcmd set app "$SiteName/" /preloadEnabled:true 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Failed to enable preload for '$SiteName/'. appcmd: $($preloadOutput -join ' ')"
}

Write-Host "AppPool startMode       : AlwaysRunning" -ForegroundColor Green
Write-Host "AppPool idleTimeout     : 00:00:00" -ForegroundColor Green
Write-Host "Site serverAutoStart    : True" -ForegroundColor Green
Write-Host "Root application preload: True" -ForegroundColor Green
Write-Host 'Routine IIS periodic recycling remains unchanged.' -ForegroundColor Yellow

Write-Step 'Recycle once so the warm policy is active now'
try {
    if ((Get-WebAppPoolState -Name $AppPoolName).Value -eq 'Started') {
        Restart-WebAppPool -Name $AppPoolName
    } else {
        Start-WebAppPool -Name $AppPoolName
    }
} catch {
    Write-Warning "App-pool recycle/start first attempt failed: $($_.Exception.Message)"
    Start-Sleep -Seconds 3
    Start-WebAppPool -Name $AppPoolName
}

if ((Get-Website -Name $SiteName).State -ne 'Started') {
    Start-Website -Name $SiteName
}

Start-Sleep -Seconds 2

Write-Step 'Verified IIS state'
$pool = Get-Item -LiteralPath $poolPath
$site = Get-Item -LiteralPath $sitePath
Write-Host "Site '$SiteName'       : $($site.State)" -ForegroundColor Green
Write-Host "AppPool '$AppPoolName' : $((Get-WebAppPoolState -Name $AppPoolName).Value)" -ForegroundColor Green
Write-Host "startMode              : $([string]$pool.startMode)"
Write-Host "idleTimeout            : $([string]$pool.processModel.idleTimeout)"
Write-Host "serverAutoStart        : $([string]$site.serverAutoStart)"
Write-Host ''
Write-Host 'Monitor IIS keep-warm configuration is applied.' -ForegroundColor Green
