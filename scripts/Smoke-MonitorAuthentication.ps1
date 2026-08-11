[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Uri]$BaseUri,

    [Parameter(Mandatory = $true)]
    [string]$Username,

    [Parameter(Mandatory = $true)]
    [string]$Password,

    [ValidateRange(1, 60)]
    [int]$TimeoutSeconds = 10,

    [switch]$AllowHttpLoopback,

    [switch]$AllowUntrustedLoopbackCertificate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
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

    throw 'Authentication smoke requires HTTPS. HTTP is allowed only for explicit loopback checks with -AllowHttpLoopback.'
}

function Invoke-SafeWebRequest {
    param([hashtable]$Parameters)

    if ($AllowUntrustedLoopbackCertificate) {
        $Parameters.SkipCertificateCheck = $true
    }
    Invoke-WebRequest @Parameters
}

Assert-SafeBaseUri -Uri $BaseUri

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginUri = [Uri]::new($BaseUri, '/login')
$loginPage = Invoke-SafeWebRequest -Parameters @{
    Uri = $loginUri
    Method = 'Get'
    WebSession = $session
    TimeoutSec = $TimeoutSeconds
    UseBasicParsing = $true
}

if ($loginPage.StatusCode -ne 200) {
    throw 'Authentication smoke could not load the login page.'
}

$tokenMatch = [regex]::Match(
    [string]$loginPage.Content,
    'name="__RequestVerificationToken"[^>]*value="([^"]+)"',
    [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
if (-not $tokenMatch.Success) {
    throw 'Authentication smoke could not obtain the anti-forgery token.'
}

$form = @{
    username = $Username
    password = $Password
    __RequestVerificationToken = $tokenMatch.Groups[1].Value
}

try {
    Invoke-SafeWebRequest -Parameters @{
        Uri = $loginUri
        Method = 'Post'
        Body = $form
        ContentType = 'application/x-www-form-urlencoded'
        WebSession = $session
        TimeoutSec = $TimeoutSeconds
        UseBasicParsing = $true
    } | Out-Null
}
catch {
    throw 'Authentication smoke login request failed.'
}

$protectedUri = [Uri]::new($BaseUri, '/servers/connections')
try {
    $protected = Invoke-SafeWebRequest -Parameters @{
        Uri = $protectedUri
        Method = 'Get'
        WebSession = $session
        TimeoutSec = $TimeoutSeconds
        MaximumRedirection = 0
        SkipHttpErrorCheck = $true
        UseBasicParsing = $true
    }
}
catch {
    throw 'Authentication smoke could not verify the protected operator route.'
}

if ($protected.StatusCode -ne 200) {
    throw 'Authentication smoke did not establish an authenticated Administrator session.'
}

Write-Host 'Monitor authentication smoke passed for an ephemeral Administrator credential.'