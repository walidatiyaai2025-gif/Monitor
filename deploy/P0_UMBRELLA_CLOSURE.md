# #111 P0 umbrella closure — closure-only fail-closed handoff

Issue #111 is **not** an independent production or OWNER_ONLY acceptance gate. It has no separate 16th production gate and must never be used to convert repository evidence into production acceptance.

The authoritative execution order remains:

```text
#162 durable RC.61 publication + separate independent verification
  -> #116 real trusted-IIS/HTTPS SingleNode 15/15 acceptance
  -> #111 closure-only issue finalization
```

The executable #162 and #116 procedures are in `deploy/REMAINING_OWNER_EXTERNAL_GATES.md`.

## Immutable inherited release identity

#111 inherits the exact product identity that #116 accepted; it does not select a new artifact.

```text
Repository                  walidatiyaai2025-gif/Monitor
Repository ID               1329517438
Version                      0.1.0-rc.61
Tag                          v0.1.0-rc.61
Artifact                     Monitor-0.1.0-rc.61-win-x64.zip
Checksum                     Monitor-0.1.0-rc.61-win-x64.zip.sha256
Product SHA-256              d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
Source commit                e28158da67b36dfc5dbf8f4c38b5c43d99c7c728
Tested merge                 158148d8bfd05f724014541bc7a0b1eab5dae1b5
Acceptance toolkit commit    b422eaaee53d931a62a43b3c36a53b68cd4f3e27
```

If #116 accepted a later equivalently verified candidate under its own authority, STOP and update this inherited identity through a reviewed repository change before closing #111.

## Required environment / prerequisites

Run from a trusted Windows operator/admin workstation with:

- PowerShell 7;
- authenticated GitHub CLI `gh` authorized for issue read/write in `walidatiyaai2025-gif/Monitor`;
- network access to GitHub;
- final #116 acceptance evidence available locally from the exact immutable production session;
- the independently preserved #116 `SessionManifestSha256` available outside mutable session files.

Required #116 evidence paths for RC.61:

```text
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\session-manifest.json
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\session-manifest.sha256
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\evidence\p0-5-evidence-pack.json
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\evidence\p0-5-closure-summary.json
C:\ProgramData\Monitor\Acceptance\p0-5-rc-61\evidence\p0-5-independent-review.json
C:\ProgramData\Monitor\App_Data\deployment-current.json
```

## Step 0 — fail-closed prerequisite read-back

```powershell
$ErrorActionPreference = 'Stop'
$repo = 'walidatiyaai2025-gif/Monitor'
$evidence111 = 'C:\ProgramData\Monitor\OwnerEvidence\111'
$sessionRoot = 'C:\ProgramData\Monitor\Acceptance\p0-5-rc-61'
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
if ($actualManifestSha -cne $sessionManifestSha256.ToLowerInvariant()) {
  throw 'STOP: #116 session manifest does not match the independently preserved SHA-256.'
}

$manifestLock = (Get-Content -LiteralPath (Join-Path $sessionRoot 'session-manifest.sha256') -Raw).Trim()
if ($manifestLock -cne "$($sessionManifestSha256.ToLowerInvariant())  session-manifest.json") {
  throw 'STOP: #116 session-manifest.sha256 lock mismatch.'
}

$pack = Get-Content -LiteralPath (Join-Path $sessionRoot 'evidence\p0-5-evidence-pack.json') -Raw | ConvertFrom-Json -Depth 20
$gates = @($pack.gates.PSObject.Properties)
if ($gates.Count -ne 15 -or @($gates | Where-Object { -not [bool]$_.Value.passed }).Count -ne 0) {
  throw 'STOP: #116 evidence pack is not exact real 15/15 PASS.'
}
if ([string]::IsNullOrWhiteSpace([string]$pack.acceptedBy) -or $null -eq $pack.acceptedAtUtc) {
  throw 'STOP: #116 final operator acceptance metadata is absent.'
}
if ([string]$pack.candidate.version -cne '0.1.0-rc.61' -or
    ([string]$pack.candidate.sha256).ToLowerInvariant() -cne 'd0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5' -or
    ([string]$pack.candidate.sourceCommit).ToLowerInvariant() -cne 'e28158da67b36dfc5dbf8f4c38b5c43d99c7c728' -or
    ([string]$pack.candidate.testedMergeCommit).ToLowerInvariant() -cne '158148d8bfd05f724014541bc7a0b1eab5dae1b5') {
  throw 'STOP: #116 accepted candidate identity differs from the locked RC.61 identity.'
}

New-Item -ItemType Directory -Path $evidence111 -Force | Out-Null
$issue162 | ConvertTo-Json -Depth 4 | Set-Content "$evidence111\issue-162-before.json" -Encoding utf8NoBOM
$issue116 | ConvertTo-Json -Depth 4 | Set-Content "$evidence111\issue-116-before.json" -Encoding utf8NoBOM
$issue111 | ConvertTo-Json -Depth 4 | Set-Content "$evidence111\issue-111-before.json" -Encoding utf8NoBOM
$sessionManifestSha256.ToLowerInvariant() | Set-Content "$evidence111\accepted-session-manifest-sha256.txt" -Encoding ascii
```

Expected prerequisite state:

```text
#162 state   CLOSED
#116 state   CLOSED
#111 state   OPEN
#116 gates   15/15 PASS
RC version   0.1.0-rc.61
Product SHA  d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5
```

Anything else is a STOP; do not close #111.

## Step 1 — closure-only action

```powershell
gh issue close 111 --repo $repo --reason completed
```

This command changes only issue #111 state. It does not publish a release, mutate IIS/SQL, change acceptance evidence, or create another production PASS.

## Step 2 — independent read-back

```powershell
$after111 = gh issue view 111 --repo $repo --json number,state,stateReason,url,closedAt | ConvertFrom-Json
$after111 | ConvertTo-Json -Depth 4 | Set-Content "$evidence111\issue-111-after.json" -Encoding utf8NoBOM

if ([string]$after111.state -cne 'CLOSED') { throw 'STOP: #111 did not close.' }
if ([string]$after111.stateReason -cne 'COMPLETED') { throw 'STOP: #111 state reason is not COMPLETED.' }

'P0_UMBRELLA_CLOSURE_VERIFIED' | Set-Content "$evidence111\result.txt" -Encoding ascii
```

Expected terminal PASS for this **closure-only** handoff:

```text
P0_UMBRELLA_CLOSURE_VERIFIED
```

This is not an external production gate PASS; it is only verification that the umbrella issue was closed after its prerequisites were genuinely accepted.

## Authoritative #111 evidence paths

```text
C:\ProgramData\Monitor\OwnerEvidence\111\issue-162-before.json
C:\ProgramData\Monitor\OwnerEvidence\111\issue-116-before.json
C:\ProgramData\Monitor\OwnerEvidence\111\issue-111-before.json
C:\ProgramData\Monitor\OwnerEvidence\111\accepted-session-manifest-sha256.txt
C:\ProgramData\Monitor\OwnerEvidence\111\issue-111-after.json
C:\ProgramData\Monitor\OwnerEvidence\111\result.txt
```

The underlying real production evidence remains under the immutable #116 session and must not be copied or rewritten merely to close #111.

## Rollback / correction command

If #111 was closed prematurely, closed with the wrong reason, or later read-back proves its prerequisites were not genuinely satisfied, immediately reopen it:

```powershell
gh issue reopen 111 --repo 'walidatiyaai2025-gif/Monitor'
```

Then independently require:

```powershell
$reopened = gh issue view 111 --repo 'walidatiyaai2025-gif/Monitor' --json state,stateReason,url | ConvertFrom-Json
if ([string]$reopened.state -cne 'OPEN') { throw 'STOP: #111 correction/reopen failed.' }
```

Never alter #116 evidence to justify an incorrect #111 closure.

## Explicit STOP conditions

STOP and keep/reopen #111 if any of the following is true:

- #162 is not genuinely closed under its durable-release publication + separate verifier + tag/assets/hash closure rule;
- #116 is not genuinely closed after real trusted-IIS/HTTPS 15/15 acceptance;
- the #116 session manifest or external preserved SHA-256 is missing/mismatched;
- the #116 pack is not exactly 15/15 PASS with final operator acceptance metadata;
- the accepted version/product/source/tested-merge identity differs from the locked RC.61 identity without a reviewed replacement of this handoff;
- any required #116 closure or independent-review evidence file is missing;
- GitHub authentication/read-back is ambiguous or unavailable;
- #111 is already closed unexpectedly at closure start;
- post-close read-back is not `CLOSED` with `COMPLETED` reason.

No repository CI, PR merge, synthetic acceptance pack, helper preview or documentation update can satisfy these prerequisites.