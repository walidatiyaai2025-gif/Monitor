[CmdletBinding()]
param(
    [string]$DefaultPayloadRoot = (Join-Path $PSScriptRoot 'app'),
    [string]$DefaultInstallRoot = "$env:ProgramFiles\Monitor",
    [int]$DefaultPort = 5080
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function New-Label([string]$Text,[int]$X,[int]$Y,[int]$Width=580,[int]$Height=24) {
    $control = New-Object Windows.Forms.Label
    $control.Text = $Text; $control.Location = New-Object Drawing.Point($X,$Y); $control.Size = New-Object Drawing.Size($Width,$Height)
    return $control
}

function New-TextBox([int]$X,[int]$Y,[int]$Width=560,[switch]$Password) {
    $control = New-Object Windows.Forms.TextBox
    $control.Location = New-Object Drawing.Point($X,$Y); $control.Size = New-Object Drawing.Size($Width,26)
    if ($Password) { $control.UseSystemPasswordChar = $true }
    return $control
}

function Get-Pbkdf2Material([string]$Password) {
    $salt = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($salt) } finally { $rng.Dispose() }
    $iterations = 210000
    $derive = New-Object Security.Cryptography.Rfc2898DeriveBytes($Password,$salt,$iterations,[Security.Cryptography.HashAlgorithmName]::SHA256)
    try { $hash = $derive.GetBytes(32) } finally { $derive.Dispose() }
    return [pscustomobject]@{
        Iterations = $iterations
        SaltBase64 = [Convert]::ToBase64String($salt)
        HashBase64 = [Convert]::ToBase64String($hash)
    }
}

function Invoke-RobocopyChecked([string]$Source,[string]$Destination) {
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    & robocopy $Source $Destination /E /R:2 /W:2 /NFL /NDL /NJH /NJS /NP /COPY:DAT /DCOPY:DAT | Out-Host
    if ($LASTEXITCODE -ge 8) { throw "Application copy failed with robocopy exit code $LASTEXITCODE." }
}

function Install-Monitor {
    param([string]$PayloadRoot,[string]$InstallRoot,[int]$Port,[string]$AdminUsername,[string]$AdminPassword)

    if (-not (Test-Administrator)) { throw 'Run this installer as Administrator.' }
    $payload = [IO.Path]::GetFullPath($PayloadRoot)
    if (-not (Test-Path -LiteralPath $payload -PathType Container)) { throw 'Published application folder does not exist.' }
    $exeSource = Join-Path $payload 'Monitor.Web.exe'
    $dllSource = Join-Path $payload 'Monitor.Web.dll'
    if (-not (Test-Path -LiteralPath $exeSource) -and -not (Test-Path -LiteralPath $dllSource)) { throw 'Published application must contain Monitor.Web.exe or Monitor.Web.dll.' }
    if ($Port -lt 1 -or $Port -gt 65535) { throw 'Port must be between 1 and 65535.' }
    if ([string]::IsNullOrWhiteSpace($AdminUsername) -or $AdminUsername.Trim().Length -lt 3) { throw 'Administrator username must contain at least 3 characters.' }
    if ($AdminPassword.Length -lt 12) { throw 'Administrator password must contain at least 12 characters.' }

    $credentials = Get-Pbkdf2Material -Password $AdminPassword
    $install = [IO.Path]::GetFullPath($InstallRoot)
    $programDataRoot = Join-Path $env:ProgramData 'Monitor'
    $upgradeRoot = Join-Path $programDataRoot 'upgrades'
    $toolsRoot = Join-Path $programDataRoot 'tools'
    New-Item -ItemType Directory -Force -Path $install,$upgradeRoot,$toolsRoot | Out-Null
    Invoke-RobocopyChecked -Source $payload -Destination $install

    $serviceExe = Join-Path $install 'Monitor.Web.exe'
    if (Test-Path -LiteralPath $serviceExe) {
        $binaryPath = '"{0}"' -f $serviceExe
    } else {
        $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
        $binaryPath = '"{0}" "{1}"' -f $dotnet,(Join-Path $install 'Monitor.Web.dll')
    }

    $existingService = Get-Service -Name 'Monitor' -ErrorAction SilentlyContinue
    if ($null -ne $existingService) { throw 'A Windows service named Monitor already exists. Use Admin > Monitor Upgrades for subsequent versions instead of reinstalling.' }

    & sc.exe create Monitor binPath= $binaryPath start= auto DisplayName= 'Monitor SQL Operations' | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Windows service creation failed.' }
    & sc.exe description Monitor 'Monitor SQL Server operations, health and DBA command center.' | Out-Null
    & sc.exe failure Monitor reset= 86400 actions= restart/5000/restart/15000/restart/30000 | Out-Null

    $serviceRegistry = 'HKLM:\SYSTEM\CurrentControlSet\Services\Monitor'
    $serviceEnvironment = @(
        'ASPNETCORE_ENVIRONMENT=Production',
        "ASPNETCORE_URLS=http://0.0.0.0:$Port",
        "DevelopmentAdmin__Username=$($AdminUsername.Trim())",
        "DevelopmentAdmin__Iterations=$($credentials.Iterations)",
        "DevelopmentAdmin__SaltBase64=$($credentials.SaltBase64)",
        "DevelopmentAdmin__HashBase64=$($credentials.HashBase64)"
    )
    New-ItemProperty -Path $serviceRegistry -Name Environment -PropertyType MultiString -Value $serviceEnvironment -Force | Out-Null

    $updaterSource = Join-Path (Split-Path $PSScriptRoot -Parent) 'upgrade\Apply-MonitorUpgrade.ps1'
    if (-not (Test-Path -LiteralPath $updaterSource)) { throw 'Apply-MonitorUpgrade.ps1 was not found next to the installer package.' }
    $updaterTarget = Join-Path $toolsRoot 'Apply-MonitorUpgrade.ps1'
    Copy-Item -LiteralPath $updaterSource -Destination $updaterTarget -Force

    $healthUrl = "http://127.0.0.1:$Port/health/ready"
    $taskCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$updaterTarget`" -StateRoot `"$upgradeRoot`" -InstallRoot `"$install`" -ServiceName Monitor -HealthUrl `"$healthUrl`""
    & schtasks.exe /Create /F /TN 'Monitor Upgrade Agent' /SC MINUTE /MO 1 /RU SYSTEM /RL HIGHEST /TR $taskCommand | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Upgrade Agent scheduled task creation failed.' }

    Start-Service -Name Monitor
    (Get-Service -Name Monitor).WaitForStatus('Running',[TimeSpan]::FromSeconds(30))

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(90)
    $ready = $false
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { $ready = $true; break }
        } catch { }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw "Monitor service started but readiness did not pass at $healthUrl." }

    return "Monitor installed successfully. Open http://localhost:$Port and sign in with the administrator account you created. Future releases can be staged under Admin > Monitor Upgrades."
}

$form = New-Object Windows.Forms.Form
$form.Text = 'Monitor Setup'
$form.StartPosition = 'CenterScreen'
$form.ClientSize = New-Object Drawing.Size(720,520)
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.BackColor = [Drawing.Color]::FromArgb(9,22,40)
$form.ForeColor = [Drawing.Color]::White
$form.Font = New-Object Drawing.Font('Segoe UI',10)

$title = New-Label 'Monitor SQL Operations - Setup' 32 24 650 38
$title.Font = New-Object Drawing.Font('Segoe UI Semibold',20)
$form.Controls.Add($title)
$subtitle = New-Label 'Guided first installation. Subsequent releases use Admin > Monitor Upgrades.' 34 67 640 28
$subtitle.ForeColor = [Drawing.Color]::FromArgb(204,214,229)
$form.Controls.Add($subtitle)

$pages = @()
for ($i=0; $i -lt 4; $i++) {
    $panel = New-Object Windows.Forms.Panel
    $panel.Location = New-Object Drawing.Point(32,108); $panel.Size = New-Object Drawing.Size(656,330); $panel.Visible = $false
    $form.Controls.Add($panel); $pages += $panel
}

$pages[0].Controls.Add((New-Label 'Welcome' 0 0 620 34))
$pages[0].Controls[0].Font = New-Object Drawing.Font('Segoe UI Semibold',16)
$welcome = New-Label "This wizard installs Monitor as an automatic Windows Service, creates the production administrator credential, configures the HTTP port, and installs the safe upgrade agent. No SQL Server credentials are requested here; monitored servers are added later from the Connections page." 0 52 620 120
$welcome.ForeColor = [Drawing.Color]::FromArgb(204,214,229); $pages[0].Controls.Add($welcome)
$adminState = New-Label ($(if (Test-Administrator) { 'Administrator check: PASS' } else { 'Administrator check: REQUIRED - restart this script as Administrator' })) 0 190 620 30
$adminState.ForeColor = $(if (Test-Administrator) { [Drawing.Color]::FromArgb(92,220,146) } else { [Drawing.Color]::FromArgb(255,172,94) })
$pages[0].Controls.Add($adminState)

$pages[1].Controls.Add((New-Label 'Installation location' 0 0 620 34)); $pages[1].Controls[0].Font = New-Object Drawing.Font('Segoe UI Semibold',16)
$pages[1].Controls.Add((New-Label 'Published application folder' 0 55)); $payloadBox = New-TextBox 0 82 620; $payloadBox.Text = $DefaultPayloadRoot; $pages[1].Controls.Add($payloadBox)
$pages[1].Controls.Add((New-Label 'Install folder' 0 126)); $installBox = New-TextBox 0 153 620; $installBox.Text = $DefaultInstallRoot; $pages[1].Controls.Add($installBox)
$pages[1].Controls.Add((New-Label 'HTTP port' 0 198)); $portBox = New-TextBox 0 225 180; $portBox.Text = [string]$DefaultPort; $pages[1].Controls.Add($portBox)

$pages[2].Controls.Add((New-Label 'Production administrator' 0 0 620 34)); $pages[2].Controls[0].Font = New-Object Drawing.Font('Segoe UI Semibold',16)
$pages[2].Controls.Add((New-Label 'Administrator username' 0 55)); $usernameBox = New-TextBox 0 82 620; $pages[2].Controls.Add($usernameBox)
$pages[2].Controls.Add((New-Label 'Administrator password (minimum 12 characters)' 0 126)); $passwordBox = New-TextBox 0 153 620 -Password; $pages[2].Controls.Add($passwordBox)
$pages[2].Controls.Add((New-Label 'Confirm password' 0 198)); $confirmBox = New-TextBox 0 225 620 -Password; $pages[2].Controls.Add($confirmBox)
$credentialNote = New-Label 'The password is converted to a salted PBKDF2-SHA256 hash. The service receives only the username, iteration count, salt and hash.' 0 270 620 52
$credentialNote.ForeColor = [Drawing.Color]::FromArgb(204,214,229); $pages[2].Controls.Add($credentialNote)

$pages[3].Controls.Add((New-Label 'Ready to install' 0 0 620 34)); $pages[3].Controls[0].Font = New-Object Drawing.Font('Segoe UI Semibold',16)
$review = New-Object Windows.Forms.TextBox
$review.Location = New-Object Drawing.Point(0,50); $review.Size = New-Object Drawing.Size(620,210); $review.Multiline = $true; $review.ReadOnly = $true
$review.BackColor = [Drawing.Color]::FromArgb(5,13,25); $review.ForeColor = [Drawing.Color]::White; $review.BorderStyle = 'FixedSingle'
$pages[3].Controls.Add($review)
$status = New-Label '' 0 275 620 50; $pages[3].Controls.Add($status)

$back = New-Object Windows.Forms.Button; $back.Text = '< Back'; $back.Location = New-Object Drawing.Point(390,462); $back.Size = New-Object Drawing.Size(92,34)
$next = New-Object Windows.Forms.Button; $next.Text = 'Next >'; $next.Location = New-Object Drawing.Point(490,462); $next.Size = New-Object Drawing.Size(92,34)
$cancel = New-Object Windows.Forms.Button; $cancel.Text = 'Cancel'; $cancel.Location = New-Object Drawing.Point(590,462); $cancel.Size = New-Object Drawing.Size(92,34)
$form.Controls.AddRange(@($back,$next,$cancel))

$current = 0
function Show-Page([int]$Index) {
    for ($i=0; $i -lt $pages.Count; $i++) { $pages[$i].Visible = ($i -eq $Index) }
    $back.Enabled = $Index -gt 0
    $next.Text = $(if ($Index -eq 3) { 'Install' } else { 'Next >' })
    if ($Index -eq 3) {
        $review.Text = "Published app: $($payloadBox.Text)`r`nInstall folder: $($installBox.Text)`r`nService: Monitor (Automatic)`r`nHTTP port: $($portBox.Text)`r`nAdministrator: $($usernameBox.Text.Trim())`r`nUpgrade agent: Monitor Upgrade Agent (SYSTEM, every minute)`r`nUpgrade UI: Admin > Monitor Upgrades"
    }
}

$back.Add_Click({ if ($current -gt 0) { $script:current--; Show-Page $script:current } })
$cancel.Add_Click({ $form.Close() })
$next.Add_Click({
    try {
        if ($current -eq 0 -and -not (Test-Administrator)) { throw 'Restart this wizard as Administrator before continuing.' }
        if ($current -eq 1) {
            if (-not (Test-Path -LiteralPath $payloadBox.Text -PathType Container)) { throw 'Choose a valid published application folder.' }
            $parsedPort = 0; if (-not [int]::TryParse($portBox.Text,[ref]$parsedPort) -or $parsedPort -lt 1 -or $parsedPort -gt 65535) { throw 'Enter a valid HTTP port.' }
        }
        if ($current -eq 2) {
            if ($usernameBox.Text.Trim().Length -lt 3) { throw 'Administrator username must contain at least 3 characters.' }
            if ($passwordBox.Text.Length -lt 12) { throw 'Administrator password must contain at least 12 characters.' }
            if ($passwordBox.Text -cne $confirmBox.Text) { throw 'Password confirmation does not match.' }
        }
        if ($current -lt 3) { $script:current++; Show-Page $script:current; return }

        $next.Enabled = $false; $back.Enabled = $false; $cancel.Enabled = $false
        $status.Text = 'Installing and validating Monitor...'; $status.ForeColor = [Drawing.Color]::FromArgb(240,214,124); $form.Refresh()
        $message = Install-Monitor -PayloadRoot $payloadBox.Text -InstallRoot $installBox.Text -Port ([int]$portBox.Text) -AdminUsername $usernameBox.Text -AdminPassword $passwordBox.Text
        $status.Text = $message; $status.ForeColor = [Drawing.Color]::FromArgb(92,220,146)
        $next.Text = 'Close'; $next.Enabled = $true; $cancel.Enabled = $true
        $script:current = 4
    } catch {
        $status.Text = $_.Exception.Message; $status.ForeColor = [Drawing.Color]::FromArgb(255,135,135)
        [Windows.Forms.MessageBox]::Show($_.Exception.Message,'Monitor Setup',[Windows.Forms.MessageBoxButtons]::OK,[Windows.Forms.MessageBoxIcon]::Error) | Out-Null
        $next.Enabled = $true; $back.Enabled = $true; $cancel.Enabled = $true
    }
})
$next.Add_Click({ if ($current -eq 4) { $form.Close() } })

Show-Page 0
[void]$form.ShowDialog()
