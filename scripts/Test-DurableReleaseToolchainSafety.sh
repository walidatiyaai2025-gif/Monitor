#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
preflight="${repo_root}/scripts/Verify-DurableReleaseToolchain.sh"
work="$(mktemp -d)"
trap 'rm -rf -- "$work"' EXIT
mkdir -p "$work/tmp" "$work/good-bin" "$work/bad-bin"
real_mv="$(command -v mv)"

cat >"$work/good-bin/gh" <<'GOOD_GH'
#!/usr/bin/env bash
set -euo pipefail
if [[ -n "${GH_CALL_LOG:-}" ]]; then
  printf 'called\n' >>"$GH_CALL_LOG"
fi
exit 0
GOOD_GH
chmod +x "$work/good-bin/gh"
cp "$work/good-bin/gh" "$work/bad-bin/gh"

cat >"$work/bad-bin/mv" <<BAD_MV
#!/usr/bin/env bash
set -euo pipefail
args=()
for arg in "\$@"; do
  case "\$arg" in
    -T|--no-clobber|--) ;;
    *) args+=("\$arg") ;;
  esac
done
exec "$real_mv" -T -f -- "\${args[@]}"
BAD_MV
chmod +x "$work/bad-bin/mv"

GH_CALL_LOG="$work/good-gh.log" TMPDIR="$work/tmp" PATH="$work/good-bin:$PATH" bash "$preflight" >/dev/null
[[ ! -e "$work/good-gh.log" ]]
[[ -z "$(find "$work/tmp" -mindepth 1 -maxdepth 1 -print -quit)" ]]

bad_destination="$work/durable-destination"
if GH_CALL_LOG="$work/bad-gh.log" TMPDIR="$work/tmp" PATH="$work/bad-bin:$work/good-bin:$PATH" bash -c '
  set -euo pipefail
  bash "$1" >/dev/null
  gh api repos/example-owner/Monitor/releases/tags/v1.2.3 >/dev/null
' _ "$preflight" >"$work/bad.stdout" 2>"$work/bad.stderr"; then
  echo 'Broken mv no-clobber semantics unexpectedly passed toolchain preflight.' >&2
  exit 1
fi
grep -Eq 'mv -T --no-clobber (consumed a file source|overwrote an existing file destination)' "$work/bad.stderr"
[[ ! -e "$work/bad-gh.log" ]]
[[ ! -e "$bad_destination" && ! -L "$bad_destination" ]]
[[ -z "$(find "$work/tmp" -mindepth 1 -maxdepth 1 -print -quit)" ]]

echo 'Durable release toolchain positive and fail-fast no-clobber drift checks passed.'
