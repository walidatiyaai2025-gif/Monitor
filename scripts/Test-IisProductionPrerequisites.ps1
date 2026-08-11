[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$HostName,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Fa-f0-9 ]+$')]
    [string]$CertificateThumbprint,

    [ValidateNotNullOrEmpty()]
    [string]$SiteName = 'Monitor',

    [ValidateNotNullOrEmpty()]
    [string]$AppPoolName = 'Monitor',

    [ValidateRange(1, 65535)]
    [int]$HttpsPort = 443,

    [switch]$PassThru
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Normalize-Thumbprint {
    param([string]$Value)
    return ($Value -replace '[^A-Fa-f0-9]', '').ToUpperInvariant()
}

function Assert-WindowsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'IIS production preflight must run from an elevated PowerShell session.'
    }
}

if (-not $IsWindows) {
    throw 'IIS production preflight is supported only on Windows Server.'
}

if ($HostName -match '^[\s\.]*$' -or $HostName.Contains('*') -or $HostName.Contains('/') -or $HostName.Contains(':')) {
    throw 'HostName must be one concrete DNS host name without scheme, wildcard, path, or port.'
}

Assert-WindowsAdministrator

if (-not (Get-Module -ListAvailable -Name WebAdministration)) {
    throw 'IIS WebAdministration module is unavailable. Install IIS management scripting tools before deployment.'
}
Import-Module WebAdministration -ErrorAction Stop

$runtime = & dotnet --list-runtimes 2>$null | Where-Object { $_ -match '^Microsoft\.AspNetCore\.App 8\.' } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace([string]$runtime)) {
    throw '.NET 8 ASP.NET Core runtime was not found. Install the .NET 8 Hosting Bundle before deployment.'
}

$ancmCandidates = @(
    (Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'),
    (Join-Path ${env:ProgramFiles(x86)} 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll')
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$ancmPath = $ancmCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace([string]$ancmPath)) {
    throw 'ASP.NET Core Module v2 was not found. Install/repair the .NET 8 Hosting Bundle for IIS.'
}

$appPoolPath = "IIS:\AppPools\$AppPoolName"
if (-not (Test-Path -LiteralPath $appPoolPath)) {
    throw "Required IIS application pool '$AppPoolName' does not exist. Create it with the approved service identity before deployment."
}

$appPool = Get-Item -LiteralPath $appPoolPath
$managedRuntimeVersion = [string]$appPool.managedRuntimeVersion
if (-not [string]::IsNullOrWhiteSpace($managedRuntimeVersion)) {
    throw "Application pool '$AppPoolName' must use No Managed Code (empty managedRuntimeVersion)."
}

$identityType = [string]$appPool.processModel.identityType
$forbiddenIdentityTypes = @('LocalSystem', 'LocalService', 'NetworkService')
if ($forbiddenIdentityTypes -contains $identityType) {
    throw "Application pool '$AppPoolName' uses forbidden high/shared privilege identity type '$identityType'. Use ApplicationPoolIdentity or an approved dedicated SpecificUser identity."
}

$sitePath = "IIS:\Sites\$SiteName"
if (-not (Test-Path -LiteralPath $sitePath)) {
    throw "Required IIS site '$SiteName' does not exist. Create the site and trusted HTTPS binding before applying a Monitor release."
}

$site = Get-Item -LiteralPath $sitePath
$sitePool = [string]$site.applicationPool
if ($sitePool -ne $AppPoolName) {
    throw "IIS site '$SiteName' is assigned to application pool '$sitePool' instead of required '$AppPoolName'."
}

$normalizedThumbprint = Normalize-Thumbprint $CertificateThumbprint
$certificatePath = "Cert:\LocalMachine\My\$normalizedThumbprint"
if (-not (Test-Path -LiteralPath $certificatePath)) {
    throw "Certificate '$normalizedThumbprint' was not found in Cert:\LocalMachine\My."
}

$certificate = Get-Item -LiteralPath $certificatePath
if (-not $certificate.HasPrivateKey) {
    throw 'The production HTTPS certificate has no accessible private key.'
}
if ($certificate.NotAfter -le (Get-Date).AddDays(1)) {
    throw 'The production HTTPS certificate is expired or expires within 24 hours.'
}

$expectedBindingSuffix = ":$HttpsPort`:$HostName"
$httpsBindings = @(Get-WebBinding -Name $SiteName -Protocol https)
$binding = $httpsBindings | Where-Object {
    ([string]$_.bindingInformation).EndsWith($expectedBindingSuffix, [StringComparison]::OrdinalIgnoreCase)
} | Select-Object -First 1
if ($null -eq $binding) {
    throw "IIS site '$SiteName' has no HTTPS binding for $HostName`:$HttpsPort. Configure the trusted production binding before deployment."
}

$bindingThumbprint = Normalize-Thumbprint ([string]$binding.certificateHash)
if ($bindingThumbprint -ne $normalizedThumbprint) {
    throw "HTTPS binding certificate does not match the approved certificate thumbprint."
}

$result = [pscustomobject]@{
    HostName = $HostName
    HttpsPort = $HttpsPort
    SiteName = $SiteName
    AppPoolName = $AppPoolName
    AppPoolIdentityType = $identityType
    SitePhysicalPath = [string]$site.physicalPath
    CertificateThumbprint = $normalizedThumbprint
    CertificateNotAfter = $certificate.NotAfter.ToUniversalTime().ToString('O')
    AspNetCoreRuntime = [string]$runtime
    AspNetCoreModulePath = [string]$ancmPath
    HttpsBinding = [string]$binding.bindingInformation
    Ready = $true
}

if ($PassThru) {
    return $result
}

$result | Format-List
Write-Host 'Monitor IIS production prerequisites passed. No configuration was changed.'
