# #111 P0 umbrella closure — closure-only fail-closed handoff

Issue #111 is **not** an independent production or OWNER_ONLY acceptance gate. It has no separate 16th production gate and must never be used to convert repository evidence into production acceptance.

The authoritative execution order remains:

```text
#162 durable selected-candidate publication + separate independent verification
  -> #116 real trusted-IIS/HTTPS SingleNode 15/15 acceptance
  -> #111 closure-only issue finalization
```

The executable #162 and #116 procedures are in `deploy/REMAINING_OWNER_EXTERNAL_GATES.md`.

## Immutable inherited release identity

#111 inherits the exact product identity that #116 accepted; it does not select a new artifact.

```text
Repository                  walidatiyaai2025-gif/Monitor
Repository ID               1329517438
Version                      0.1.0-rc.854
Tag                          v0.1.0-rc.854
Artifact                     Monitor-0.1.0-rc.854-win-x64.zip
Checksum                     Monitor-0.1.0-rc.854-win-x64.zip.sha256
Product SHA-256              b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7
Source commit                ef7209cbf099da65330887508ab4380a8b4196d2
Tested merge                 e1d0daedf8b2209934a1bcd01bff5d46229df20a
```

RC.61 is historical only; its Actions artifact expired and it is not an executable closure input.

## Required environment / prerequisites

Run from a trusted Windows operator/admin workstation with PowerShell 7, authenticated GitHub CLI, network access, and the final #116 evidence from the exact immutable production acceptance session. The independently preserved #116 `SessionManifestSha256` must remain available outside mutable session files.

Canonical acceptance root for the selected candidate:

```text
C:\ProgramData\Monitor\Acceptance\p0-5-rc-854
```

Required files include `session-manifest.json`, `session-manifest.sha256`, `evidence\p0-5-evidence-pack.json`, `evidence\p0-5-closure-summary.json`, and `evidence\p0-5-independent-review.json`.

## Step 0 — fail-closed prerequisite read-back

```powershell
$ErrorActionPreference = 'Stop'
$repo = 'walidatiyaai2025-gif/Monitor'
$evidence111 = 'C:\ProgramData\Monitor\OwnerEvidence\111'
$sessionRoot = 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-854'
$sessionManifestSha256 = Read-Host 'Independently preserved #116 SessionManifestSha256'

if ($sessionManifestSha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'STOP: invalid #116 session-manifest SHA-256.' }
if (Test-Path -LiteralPath $evidence111) { throw 'STOP: #111 evidence root already exists; preserve the previous attempt.' }

$issue162 = gh issue view 162 --repo $repo --json number,state,stateReason,url | ConvertFrom-Json
$issue116 = gh issue view 116 --repo $repo --json number,state,stateReason,url | ConvertFrom-Json
$issue111 = gh issue view 111 --repo $repo --json number,state,stateReason,url | ConvertFrom-Json

if ([string]$issue162.state -cne 'CLOSED') { throw 'STOP: #162 is not CLOSED.' }
if ([string]$issue116.state -cne 'CLOSED') { throw 'STOP: #116 is not CLOSED.' }
if ([string]$issue111.state -cne 'OPEN') { throw 'STOP: #111 is not OPEN at closure start.' }

$requiredFiles = @(
  'session-manifest.json',
  'session-manifest.sha256',
  'evidence\p0-5-evidence-pack.json',
  'evidence\p0-5-closure-summary.json',
  'evidence\p0-5-independent-review.json'
)
foreach ($relative in $requiredFiles) {
  if (-not (Test-Path -LiteralPath (Join-Path $sessionRoot $relative) -PathType Leaf)) {
    throw "STOP: missing authoritative #116 evidence: $relative"
  }
}

$manifest = Join-Path $sessionRoot 'session-manifest.json'
$actualManifestSha = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualManifestSha -cne $sessionManifestSha256.ToLowerInvariant()) { throw 'STOP: #116 session manifest SHA-256 mismatch.' }

$manifestLock = (Get-Content -LiteralPath (Join-Path $sessionRoot 'session-manifest.sha256') -Raw).Trim()
if ($manifestLock -cne "$($sessionManifestSha256.ToLowerInvariant())  session-manifest.json") { throw 'STOP: #116 session-manifest.sha256 lock mismatch.' }

$pack = Get-Content -LiteralPath (Join-Path $sessionRoot 'evidence\p0-5-evidence-pack.json') -Raw | ConvertFrom-Json -Depth 20
$gates = @($pack.gates.PSObject.Properties)
if ($gates.Count -ne 15 -or @($gates | Where-Object { -not [bool]$_.Value.passed }).Count -ne 0) { throw 'STOP: #116 evidence pack is not exact real 15/15 PASS.' }
if ([string]::IsNullOrWhiteSpace([string]$pack.acceptedBy) -or $null -eq $pack.acceptedAtUtc) { throw 'STOP: #116 final operator acceptance metadata is absent.' }

if ([string]$pack.candidate.version -cne '0.1.0-rc.854' -or
    ([string]$pack.candidate.sha256).ToLowerInvariant() -cne 'b0370b3efa984393d833958850734c67c69b78bfe32e4b47c844ddb10f0f27b7' -or
    ([string]$pack.candidate.sourceCommit).ToLowerInvariant() -cne 'ef7209cbf099da65330887508ab4380a8b4196d2' -or
    ([string]$pack.candidate.testedMergeCommit).ToLowerInvariant() -cne 'e1d0daedf8b2209934a1bcd01bff5d46229df20a') {
  throw 'STOP: #116 accepted candidate identity differs from the selected rc.854 identity.'
}

New-Item -ItemType Directory -Path $evidence111 -Force | Out-Null
$issue162 | ConvertTo-Json -Depth 4 | Set-Content "$evidence111\issue-162-before.json" -Encoding utf8NoBOM
$issue116 | ConvertTo-Json -Depth 4 | Set-Content "$evidence111\issue-116-before.json" -Encoding utf8NoBOM
$issue111 | ConvertTo-Json -Depth 4 | Set-Content "$evidence111\issue-111-before.json" -Encoding utf8NoBOM
$sessionManifestSha256.ToLowerInvariant() | Set-Content "$evidence111\accepted-session-manifest-sha256.txt" -Encoding ascii
```

Expected prerequisite state is #162 CLOSED, #116 CLOSED, #111 OPEN, exact real **15/15 PASS**, version `0.1.0-rc.854`, and product SHA `b0370b3e...f0f27b7`. Anything else is a STOP.

## Step 1 — closure-only action

```powershell
gh issue close 111 --repo $repo --reason completed
```

This changes only issue #111 state. It does not publish a release, mutate IIS/SQL, change acceptance evidence, or create another production PASS.

## Step 2 — independent read-back

```powershell
$after111 = gh issue view 111 --repo $repo --json number,state,stateReason,url,closedAt | ConvertFrom-Json
$after111 | ConvertTo-Json -Depth 4 | Set-Content "$evidence111\issue-111-after.json" -Encoding utf8NoBOM
if ([string]$after111.state -cne 'CLOSED') { throw 'STOP: #111 did not close.' }
if ([string]$after111.stateReason -cne 'COMPLETED') { throw 'STOP: #111 state reason is not COMPLETED.' }
'P0_UMBRELLA_CLOSURE_VERIFIED' | Set-Content "$evidence111\result.txt" -Encoding ascii
```

Expected terminal result is `P0_UMBRELLA_CLOSURE_VERIFIED`. This is closure-only verification, not a 16th external production gate.

## Rollback / correction command

If #111 was closed prematurely or later read-back invalidates prerequisites:

```powershell
gh issue reopen 111 --repo 'walidatiyaai2025-gif/Monitor'
```

Then independently require issue #111 to be OPEN. Never alter #116 evidence to justify an incorrect #111 closure.

## Explicit STOP conditions

STOP and keep/reopen #111 if #162 or #116 is not genuinely CLOSED, the #116 session lock/evidence is missing or mismatched, the pack is not exactly **15/15 PASS**, accepted candidate identity differs from rc.854, GitHub read-back is ambiguous, #111 is unexpectedly closed at start, or post-close read-back is not `CLOSED` with `COMPLETED` reason.

No repository CI, PR merge, synthetic pack, helper preview or documentation update can satisfy these prerequisites.
