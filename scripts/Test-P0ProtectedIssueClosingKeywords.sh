#!/usr/bin/env bash
set -euo pipefail

pr_title="${PR_TITLE:-}"
pr_body="${PR_BODY:-}"
metadata="${pr_title}"$'\n'"${pr_body}"

# GitHub treats closing keywords in merged PR metadata as issue-closing directives.
# Protected P0 production gates are intentionally closed only by an explicit
# post-evidence issue action, never as a merge side effect.
closing_keyword_pattern='(^|[^[:alnum:]_])(close|closes|closed|fix|fixes|fixed|resolve|resolves|resolved)[[:space:]:]+((walidatiyaai2025-gif/Monitor)?#|https://github\.com/walidatiyaai2025-gif/Monitor/issues/)(111|116|162)([^0-9]|$)'

if printf '%s\n' "$metadata" | grep -Eiq "$closing_keyword_pattern"; then
  echo "Unsafe PR metadata: a GitHub closing keyword targets protected P0 issue #111, #116, or #162." >&2
  echo "Use non-closing wording in the PR. Protected P0 gates must be closed explicitly after their evidence contract is satisfied." >&2
  exit 1
fi

echo "Protected P0 issue closing-keyword guard passed."
