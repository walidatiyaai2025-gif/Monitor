param(
    [Parameter(Mandatory = $true)][string]$BaseUrl,
    [string]$AuthCookie = ""
)

$ErrorActionPreference = "Stop"
$base = $BaseUrl.TrimEnd('/')

function Invoke-MonitorProbe {
    param([string]$Path, [bool]$Authenticated = $false)
    $headers = @{}
    if ($Authenticated -and -not [string]::IsNullOrWhiteSpace($AuthCookie)) {
        $headers["Cookie"] = $AuthCookie
    }
    $response = Invoke-WebRequest -Uri "$base$Path" -Method Get -Headers $headers -MaximumRedirection 0 -SkipHttpErrorCheck
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) {
        throw "Probe $Path failed with HTTP $($response.StatusCode)."
    }
    Write-Host "PASS $Path -> $($response.StatusCode)"
}

Invoke-MonitorProbe -Path "/health/live"
Invoke-MonitorProbe -Path "/health/ready"

if (-not [string]::IsNullOrWhiteSpace($AuthCookie)) {
    Invoke-MonitorProbe -Path "/enterprise/readiness" -Authenticated $true
    Invoke-MonitorProbe -Path "/enterprise/help" -Authenticated $true
    Invoke-MonitorProbe -Path "/enterprise/fleet" -Authenticated $true
    Invoke-MonitorProbe -Path "/reports/servers-v2.csv" -Authenticated $true
}
else {
    Write-Host "Authenticated BATCH-200 probes skipped because AuthCookie was not supplied."
}

Write-Host "BATCH-200 smoke completed. Probes are control-plane/cache-only and do not call snapshot refresh routes."
