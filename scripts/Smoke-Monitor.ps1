[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Uri]$BaseUri,

    [ValidateRange(1, 60)]
    [int]$TimeoutSeconds = 10,

    [switch]$AllowHttpLoopback,

    [switch]$AllowUntrustedLoopbackCertificate
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$loopbackHosts = @('localhost', '127.0.0.1', '::1')

function Assert-SafeBaseUri {
    param([Uri]$Uri)

    if (-not $Uri.IsAbsoluteUri) {
        throw 'BaseUri must be an absolute URI.'
    }

    if ($AllowUntrustedLoopbackCertificate -and ($Uri.Scheme -ne 'https' -or $loopbackHosts -notcontains $Uri.Host)) {
        throw '-AllowUntrustedLoopbackCertificate is permitted only for HTTPS loopback targets.'
    }

    if ($Uri.Scheme -eq 'https') {
        return
    }

    if ($AllowHttpLoopback -and $Uri.Scheme -eq 'http' -and $loopbackHosts -contains $Uri.Host) {
        return
    }

    throw 'Smoke tests require HTTPS. HTTP is allowed only for explicit loopback checks with -AllowHttpLoopback.'
}

function Invoke-MonitorProbe {
    param(
        [string]$Path,
        [string]$ExpectedStatus
    )

    $target = [Uri]::new($BaseUri, $Path)
    $parameters = @{
        Uri = $target
        Method = 'Get'
        TimeoutSec = $TimeoutSeconds
        Headers = @{ 'Accept' = 'application/json' }
    }
    if ($AllowUntrustedLoopbackCertificate) {
        $parameters.SkipCertificateCheck = $true
    }

    try {
        $result = Invoke-RestMethod @parameters
    }
    catch {
        throw "Monitor probe failed for $Path. HTTP/readiness verification did not succeed."
    }

    if ($null -eq $result -or [string]::IsNullOrWhiteSpace([string]$result.status)) {
        throw "Monitor probe $Path returned no bounded status field."
    }

    if ([string]$result.status -ne $ExpectedStatus) {
        throw "Monitor probe $Path returned status '$($result.status)' instead of '$ExpectedStatus'."
    }

    [pscustomobject]@{
        Path = $Path
        Status = [string]$result.status
        Passed = $true
    }
}

Assert-SafeBaseUri -Uri $BaseUri

$probes = @(
    Invoke-MonitorProbe -Path '/health/live' -ExpectedStatus 'Live'
    Invoke-MonitorProbe -Path '/health/ready' -ExpectedStatus 'Ready'
    Invoke-MonitorProbe -Path '/health' -ExpectedStatus 'Ready'
)

$probes | Format-Table -AutoSize
Write-Host 'Monitor deployment smoke test passed.'