[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem

$version = '9.9.9-rc.safety'
$sourceSha = ('a' * 40) -join ''
$testedSha = ('b' * 40) -join ''
$zipName = "Monitor-$version-win-x64.zip"
$validator = Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts/Test-ExistingCandidatePromotion.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ("monitor-promotion-safety-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null

function New-TestPayload {
    param(
        [Parameter(Mandatory = $true)][string]$CaseName,
        [Parameter(Mandatory = $true)][object[]]$Entries
    )

    $directory = Join-Path $root $CaseName
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $zipPath = Join-Path $directory $zipName
    $checksumPath = "$zipPath.sha256"

    $manifest = [ordered]@{
        schemaVersion = 2
        product = 'Monitor'
        version = $version
        sourceHeadSha = $sourceSha
        testedMergeSha = $testedSha
        runtime = 'win-x64'
        deploymentMode = 'SingleNode'
        configuration = 'Release'
        candidateVerification = @{
            sourceOfTruth = '#116'
            embeddedWorkflowRunIds = $false
        }
    } | ConvertTo-Json -Depth 6

    $fileStream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $archive = [IO.Compression.ZipArchive]::new($fileStream, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        $manifestEntry = $archive.CreateEntry('_operations/release-manifest.json', [IO.Compression.CompressionLevel]::Optimal)
        $writer = [IO.StreamWriter]::new($manifestEntry.Open(), [Text.UTF8Encoding]::new($false))
        try { $writer.Write($manifest) } finally { $writer.Dispose() }

        foreach ($spec in $Entries) {
            $entry = $archive.CreateEntry([string]$spec.Name, [IO.Compression.CompressionLevel]::Optimal)
            if ($null -ne $spec.ExternalAttributes) {
                $entry.ExternalAttributes = [int]$spec.ExternalAttributes
            }
            $byteCount = [int]$spec.ByteCount
            if ($byteCount -gt 0) {
                $stream = $entry.Open()
                try {
                    $buffer = New-Object byte[] ([Math]::Min($byteCount, 65536))
                    $remaining = $byteCount
                    while ($remaining -gt 0) {
                        $writeCount = [Math]::Min($remaining, $buffer.Length)
                        $stream.Write($buffer, 0, $writeCount)
                        $remaining -= $writeCount
                    }
                }
                finally { $stream.Dispose() }
            }
        }
    }
    finally {
        $archive.Dispose()
        $fileStream.Dispose()
    }

    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($checksumPath, "$hash  $zipName`n", [Text.Encoding]::ASCII)
    [pscustomobject]@{ Zip = $zipPath; Checksum = $checksumPath; Hash = $hash }
}

function Invoke-Validation {
    param([Parameter(Mandatory = $true)]$Payload)
    & $validator `
        -ArtifactPath $Payload.Zip `
        -ChecksumPath $Payload.Checksum `
        -CandidateVersion $version `
        -ExpectedProductSha256 $Payload.Hash `
        -SourceCommit $sourceSha `
        -TestedMergeCommit $testedSha | Out-Null
}

function Assert-Rejected {
    param(
        [Parameter(Mandatory = $true)]$Payload,
        [Parameter(Mandatory = $true)][string]$ExpectedMessage
    )
    try {
        Invoke-Validation -Payload $Payload
    }
    catch {
        if ($_.Exception.Message -notlike "*$ExpectedMessage*") {
            throw "Candidate was rejected for an unexpected reason. Expected '$ExpectedMessage'; got '$($_.Exception.Message)'."
        }
        return
    }
    throw "Unsafe candidate unexpectedly passed validation; expected rejection containing '$ExpectedMessage'."
}

try {
    $safe = New-TestPayload -CaseName 'safe' -Entries @(
        [pscustomobject]@{ Name = 'payload/Monitor.Web.dll'; ByteCount = 4096; ExternalAttributes = $null }
    )
    Invoke-Validation -Payload $safe

    Assert-Rejected `
        -Payload (New-TestPayload -CaseName 'reserved-device' -Entries @([pscustomobject]@{ Name = 'payload/CON.txt'; ByteCount = 1; ExternalAttributes = $null })) `
        -ExpectedMessage 'reserved device-name'

    Assert-Rejected `
        -Payload (New-TestPayload -CaseName 'trailing-dot' -Entries @([pscustomobject]@{ Name = 'payload/name.'; ByteCount = 1; ExternalAttributes = $null })) `
        -ExpectedMessage 'trailing dot or space'

    Assert-Rejected `
        -Payload (New-TestPayload -CaseName 'forbidden-character' -Entries @([pscustomobject]@{ Name = 'payload/bad?.txt'; ByteCount = 1; ExternalAttributes = $null })) `
        -ExpectedMessage 'Windows-forbidden or control character'

    $controlCharacterName = "payload/bad$([char]0x001F).txt"
    Assert-Rejected `
        -Payload (New-TestPayload -CaseName 'control-character' -Entries @([pscustomobject]@{ Name = $controlCharacterName; ByteCount = 1; ExternalAttributes = $null })) `
        -ExpectedMessage 'Windows-forbidden or control character'

    $composed = "payload/caf$([char]0x00E9).txt"
    $decomposed = "payload/cafe$([char]0x0301).txt"
    Assert-Rejected `
        -Payload (New-TestPayload -CaseName 'unicode-collision' -Entries @(
            [pscustomobject]@{ Name = $composed; ByteCount = 1; ExternalAttributes = $null },
            [pscustomobject]@{ Name = $decomposed; ByteCount = 1; ExternalAttributes = $null }
        )) `
        -ExpectedMessage 'Unicode normalization'

    $longName = 'payload/' + ('a' * 241) + '.txt'
    Assert-Rejected `
        -Payload (New-TestPayload -CaseName 'overlong-path' -Entries @([pscustomobject]@{ Name = $longName; ByteCount = 1; ExternalAttributes = $null })) `
        -ExpectedMessage 'overlong normalized entry path'

    Assert-Rejected `
        -Payload (New-TestPayload -CaseName 'symlink' -Entries @([pscustomobject]@{ Name = 'payload/link'; ByteCount = 0; ExternalAttributes = -1577123840 })) `
        -ExpectedMessage 'symlink or reparse-point'

    Assert-Rejected `
        -Payload (New-TestPayload -CaseName 'compression-ratio' -Entries @([pscustomobject]@{ Name = 'payload/bomb.bin'; ByteCount = 2097152; ExternalAttributes = $null })) `
        -ExpectedMessage 'compression ratio above 200:1'

    Write-Host 'Existing-candidate promotion ZIP safety runtime checks passed.'
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
