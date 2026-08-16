#!/usr/bin/env bash
set -euo pipefail

fail() {
  echo "Durable release toolchain preflight failed: $*" >&2
  exit 1
}

required_commands=(
  gh jq realpath stat mktemp find sort mv sha256sum awk
  dirname basename chmod mkdir rm rmdir cat
)
for required in "${required_commands[@]}"; do
  command -v "$required" >/dev/null 2>&1 || fail "required command is unavailable: ${required}"
done

[[ "$(jq -n -r '"durable-release-toolchain-ok"')" == "durable-release-toolchain-ok" ]] || fail "jq functional probe failed"

probe_root="$(mktemp -d)" || fail "mktemp could not create the capability-probe root"
probe_identity=""
cleanup_probe() {
  [[ -n "${probe_root:-}" && -d "$probe_root" && ! -L "$probe_root" ]] || return 0
  rm -f -- \
    "$probe_root/stat-file" \
    "$probe_root/hash-file" \
    "$probe_root/mv-file-destination" \
    "$probe_root/mv-file-collision-source" \
    "$probe_root/mv-file-collision-destination" \
    "$probe_root/mv-dir-destination/sentinel" \
    "$probe_root/mv-dir-collision-source/source-sentinel" \
    "$probe_root/mv-dir-collision-destination/destination-sentinel" \
    "$probe_root/find-probe/alpha" \
    "$probe_root/find-probe/beta" 2>/dev/null || true
  rmdir -- \
    "$probe_root/mv-dir-destination" \
    "$probe_root/mv-dir-source" \
    "$probe_root/mv-dir-collision-source" \
    "$probe_root/mv-dir-collision-destination" \
    "$probe_root/find-probe" 2>/dev/null || true
  if [[ -n "${mktemp_child:-}" ]]; then
    rmdir -- "$mktemp_child" 2>/dev/null || true
  fi
  rmdir -- "$probe_root" 2>/dev/null || true
}
trap cleanup_probe EXIT HUP INT TERM

chmod 700 -- "$probe_root"
canonical_probe_root="$(realpath -e -- "$probe_root")" || fail "realpath -e -- functional probe failed"
[[ "$canonical_probe_root" == "$probe_root" ]] || fail "realpath -e -- did not preserve an already-canonical existing path"
probe_identity="$(stat -Lc '%d:%i' "$probe_root")" || fail "stat device/inode probe failed"
[[ "$probe_identity" =~ ^[0-9]+:[0-9]+$ ]] || fail "stat device/inode format is incompatible"
[[ "$(stat -Lc '%a' "$probe_root")" == 700 ]] || fail "stat mode probe is incompatible"

printf 'abc' >"$probe_root/stat-file"
chmod 600 -- "$probe_root/stat-file"
[[ "$(stat -Lc '%h' "$probe_root/stat-file")" == 1 ]] || fail "stat hard-link-count probe is incompatible"
[[ "$(stat -c%s "$probe_root/stat-file")" == 3 ]] || fail "stat byte-size probe is incompatible"

mktemp_child="$(mktemp -d -p "$probe_root" '.monitor-toolchain.XXXXXXXXXX')" || fail "mktemp -d -p <root> <template> is unsupported"
[[ "$(dirname -- "$mktemp_child")" == "$probe_root" ]] || fail "mktemp -d -p created a child outside the requested root"
[[ -d "$mktemp_child" && ! -L "$mktemp_child" ]] || fail "mktemp staging probe did not create a real directory"
[[ "$(stat -Lc '%a' "$mktemp_child")" == 700 ]] || fail "mktemp staging probe did not create a private 0700 directory"

mkdir "$probe_root/find-probe"
printf 'a' >"$probe_root/find-probe/alpha"
printf 'b' >"$probe_root/find-probe/beta"
mapfile -t find_entries < <(find "$probe_root/find-probe" -mindepth 1 -maxdepth 1 -printf '%f\n' | sort)
[[ "${#find_entries[@]}" -eq 2 && "${find_entries[0]}" == alpha && "${find_entries[1]}" == beta ]] || fail "find -printf plus sort probe is incompatible"

printf 'durable-release-toolchain\n' >"$probe_root/hash-file"
probe_hash="$(sha256sum "$probe_root/hash-file" | awk '{print $1}')" || fail "sha256sum/awk functional probe failed"
[[ "$probe_hash" =~ ^[a-f0-9]{64}$ ]] || fail "sha256sum/awk did not return one canonical lowercase SHA-256 digest"

printf 'source-file\n' >"$probe_root/mv-file-source"
source_file_identity="$(stat -Lc '%d:%i' "$probe_root/mv-file-source")"
mv -T --no-clobber -- "$probe_root/mv-file-source" "$probe_root/mv-file-destination" || fail "mv -T --no-clobber cannot finalize an absent file destination"
[[ ! -e "$probe_root/mv-file-source" && -f "$probe_root/mv-file-destination" ]] || fail "mv -T --no-clobber file rename semantics are incompatible"
[[ "$(stat -Lc '%d:%i' "$probe_root/mv-file-destination")" == "$source_file_identity" ]] || fail "mv file finalization did not preserve source identity"

printf 'collision-source\n' >"$probe_root/mv-file-collision-source"
printf 'collision-destination\n' >"$probe_root/mv-file-collision-destination"
mv -T --no-clobber -- "$probe_root/mv-file-collision-source" "$probe_root/mv-file-collision-destination" || true
[[ -f "$probe_root/mv-file-collision-source" ]] || fail "mv -T --no-clobber consumed a file source when the destination existed"
[[ "$(cat "$probe_root/mv-file-collision-source")" == collision-source ]] || fail "mv file collision mutated the source"
[[ "$(cat "$probe_root/mv-file-collision-destination")" == collision-destination ]] || fail "mv -T --no-clobber overwrote an existing file destination"

mkdir "$probe_root/mv-dir-source"
printf 'directory-source\n' >"$probe_root/mv-dir-source/sentinel"
source_dir_identity="$(stat -Lc '%d:%i' "$probe_root/mv-dir-source")"
mv -T --no-clobber -- "$probe_root/mv-dir-source" "$probe_root/mv-dir-destination" || fail "mv -T --no-clobber cannot publish an absent directory destination"
[[ ! -e "$probe_root/mv-dir-source" && -d "$probe_root/mv-dir-destination" ]] || fail "mv -T --no-clobber directory rename semantics are incompatible"
[[ "$(stat -Lc '%d:%i' "$probe_root/mv-dir-destination")" == "$source_dir_identity" ]] || fail "mv directory publication did not preserve source identity"
[[ "$(cat "$probe_root/mv-dir-destination/sentinel")" == directory-source ]] || fail "mv directory publication changed source bytes"

mkdir "$probe_root/mv-dir-collision-source" "$probe_root/mv-dir-collision-destination"
printf 'source-directory\n' >"$probe_root/mv-dir-collision-source/source-sentinel"
printf 'destination-directory\n' >"$probe_root/mv-dir-collision-destination/destination-sentinel"
mv -T --no-clobber -- "$probe_root/mv-dir-collision-source" "$probe_root/mv-dir-collision-destination" || true
[[ -d "$probe_root/mv-dir-collision-source" ]] || fail "mv -T --no-clobber consumed a directory source when the destination existed"
[[ "$(cat "$probe_root/mv-dir-collision-source/source-sentinel")" == source-directory ]] || fail "mv directory collision mutated the source"
[[ "$(cat "$probe_root/mv-dir-collision-destination/destination-sentinel")" == destination-directory ]] || fail "mv -T --no-clobber overwrote an existing directory destination"
[[ ! -e "$probe_root/mv-dir-collision-destination/source-sentinel" ]] || fail "mv directory collision merged source content into the destination"

cleanup_probe
trap - EXIT HUP INT TERM

echo 'Durable release toolchain capability preflight passed.'
