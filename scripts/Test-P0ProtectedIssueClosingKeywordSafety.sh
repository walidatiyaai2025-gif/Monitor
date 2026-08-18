#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
guard="$root/scripts/Test-P0ProtectedIssueClosingKeywords.sh"

run_allowed() {
  local title="$1"
  local body="$2"
  PR_TITLE="$title" PR_BODY="$body" bash "$guard" >/dev/null
}

run_blocked() {
  local title="$1"
  local body="$2"
  local output
  if output="$(PR_TITLE="$title" PR_BODY="$body" bash "$guard" 2>&1)"; then
    echo "Expected protected closing-keyword case to fail: title='$title' body='$body'" >&2
    exit 1
  fi
  grep -Fq 'Unsafe PR metadata' <<<"$output"
}

# Ordinary child-issue closeout remains available.
run_allowed 'P0.5: add guard' 'Closes #347'
run_allowed 'P0.5: docs only' 'Do not mark #162/#116/#111 complete.'
run_allowed 'P0.5: unrelated issue' 'Fixes #1620'

# All GitHub closing-keyword families are rejected for the protected parents.
run_blocked 'close #162' ''
run_blocked 'docs' 'Closes: #116 after evidence.'
run_blocked 'docs' 'fixed walidatiyaai2025-gif/Monitor#111'
run_blocked 'docs' 'Resolves https://github.com/walidatiyaai2025-gif/Monitor/issues/162'
run_blocked 'docs' 'do not close #162 from CI'

echo 'Protected P0 issue closing-keyword positive and fail-closed cases passed.'
