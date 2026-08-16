#!/usr/bin/env bash
set -euo pipefail

fail() {
  echo "Durable release verification failed: $*" >&2
  exit 1
}

repository=""
tag=""
version=""
product_sha256=""
trusted_root=""
destination=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repository)
      [[ $# -ge 2 ]] || fail "--repository requires a value"
      repository="$2"
      shift 2
      ;;
    --tag)
      [[ $# -ge 2 ]] || fail "--tag requires a value"
      tag="$2"
      shift 2
      ;;
    --version)
      [[ $# -ge 2 ]] || fail "--version requires a value"
      version="$2"
      shift 2
      ;;
    --product-sha256)
      [[ $# -ge 2 ]] || fail "--product-sha256 requires a value"
      product_sha256="$2"
      shift 2
      ;;
    --trusted-root)
      [[ $# -ge 2 ]] || fail "--trusted-root requires a value"
      trusted_root="$2"
      shift 2
      ;;
    --destination)
      [[ $# -ge 2 ]] || fail "--destination requires a value"
      destination="$2"
      shift 2
      ;;
    *)
      fail "unexpected argument: $1"
      ;;
  esac
done

[[ "$repository" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] || fail "repository must be an exact owner/name slug"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([.-][0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$ ]] || fail "version format is invalid"
[[ "$tag" == "v${version}" ]] || fail "tag must equal v<version>"
[[ "$product_sha256" =~ ^[a-f0-9]{64}$ ]] || fail "product SHA-256 must be 64 lowercase hex characters"

[[ -n "$trusted_root" ]] || fail "trusted root is required"
[[ "$trusted_root" == /* ]] || fail "trusted root must be an absolute path"
[[ "$trusted_root" != "/" ]] || fail "trusted root must not be the filesystem root"
[[ -d "$trusted_root" && ! -L "$trusted_root" ]] || fail "trusted root must be an existing non-symlink directory"
trusted_root_canonical="$(realpath -e -- "$trusted_root")"
[[ "$trusted_root_canonical" == "$trusted_root" ]] || fail "trusted root must already be canonical"
trusted_root_identity="$(stat -Lc '%d:%i' "$trusted_root_canonical")"

assert_trusted_root_identity() {
  [[ -d "$trusted_root_canonical" && ! -L "$trusted_root_canonical" ]] || fail "trusted root was replaced during verification"
  [[ "$(realpath -e -- "$trusted_root_canonical")" == "$trusted_root_canonical" ]] || fail "trusted root canonical path changed during verification"
  [[ "$(stat -Lc '%d:%i' "$trusted_root_canonical")" == "$trusted_root_identity" ]] || fail "trusted root identity changed during verification"
}

[[ -n "$destination" ]] || fail "destination is required"
[[ "$destination" == /* ]] || fail "destination must be an absolute path"
[[ "$destination" != "/" ]] || fail "destination must not be the filesystem root"
[[ ! -e "$destination" && ! -L "$destination" ]] || fail "destination must not already exist"
destination_parent="$(dirname -- "$destination")"
destination_name="$(basename -- "$destination")"
[[ "$destination_name" != "." && "$destination_name" != ".." ]] || fail "destination basename is invalid"
destination_parent_canonical="$(realpath -e -- "$destination_parent")"
[[ "$destination_parent_canonical" == "$trusted_root_canonical" ]] || fail "destination must be a direct child of the trusted root"
[[ "$destination" == "${trusted_root_canonical}/${destination_name}" ]] || fail "destination must be canonical and contained by the trusted root"
assert_trusted_root_identity

expected_prerelease=false
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  expected_prerelease=true
fi

zip_name="Monitor-${version}-win-x64.zip"
checksum_name="${zip_name}.sha256"
zip_url="https://github.com/${repository}/releases/download/${tag}/${zip_name}"
checksum_url="https://github.com/${repository}/releases/download/${tag}/${checksum_name}"

snapshot_release() {
  gh api "repos/${repository}/releases/tags/${tag}"
}

validate_snapshot() {
  local release_json="$1"
  local release_id
  local zip_meta
  local checksum_meta
  local zip_id
  local checksum_id
  local zip_size
  local checksum_size
  local zip_digest
  local checksum_digest
  local observed_zip_url
  local observed_checksum_url

  release_id="$(jq -r '.id // empty' <<<"$release_json")"
  [[ "$release_id" =~ ^[1-9][0-9]*$ ]] || fail "release ID must be a positive integer"
  [[ "$(jq -r '.tag_name // empty' <<<"$release_json")" == "$tag" ]] || fail "release tag does not match the verified tag"
  [[ "$(jq -r '.name // empty' <<<"$release_json")" == "Monitor ${version}" ]] || fail "release title does not match the verified version"
  [[ "$(jq -r '.draft' <<<"$release_json")" == false ]] || fail "draft releases are not accepted"
  [[ "$(jq -r '.prerelease' <<<"$release_json")" == "$expected_prerelease" ]] || fail "release prerelease classification does not match the version"

  mapfile -t names < <(jq -r '.assets[]?.name // empty' <<<"$release_json" | sort)
  [[ "${#names[@]}" -eq 2 ]] || fail "release must contain exactly two assets"
  [[ "${names[0]}" == "$zip_name" && "${names[1]}" == "$checksum_name" ]] || fail "release asset names do not match the exact ZIP/checksum contract"

  zip_meta="$(jq -c --arg name "$zip_name" '[.assets[] | select(.name == $name)] | if length == 1 then .[0] else empty end' <<<"$release_json")"
  checksum_meta="$(jq -c --arg name "$checksum_name" '[.assets[] | select(.name == $name)] | if length == 1 then .[0] else empty end' <<<"$release_json")"
  [[ -n "$zip_meta" && -n "$checksum_meta" ]] || fail "release must expose exactly one metadata record for each expected asset"

  zip_id="$(jq -r '.id // empty' <<<"$zip_meta")"
  checksum_id="$(jq -r '.id // empty' <<<"$checksum_meta")"
  zip_size="$(jq -r '.size // empty' <<<"$zip_meta")"
  checksum_size="$(jq -r '.size // empty' <<<"$checksum_meta")"
  zip_digest="$(jq -r '.digest // empty' <<<"$zip_meta")"
  checksum_digest="$(jq -r '.digest // empty' <<<"$checksum_meta")"
  observed_zip_url="$(jq -r '.browser_download_url // empty' <<<"$zip_meta")"
  observed_checksum_url="$(jq -r '.browser_download_url // empty' <<<"$checksum_meta")"

  [[ "$(jq -r '.state // empty' <<<"$zip_meta")" == uploaded ]] || fail "ZIP asset is not fully uploaded"
  [[ "$(jq -r '.state // empty' <<<"$checksum_meta")" == uploaded ]] || fail "checksum asset is not fully uploaded"
  [[ "$zip_id" =~ ^[1-9][0-9]*$ && "$checksum_id" =~ ^[1-9][0-9]*$ ]] || fail "asset IDs must be positive integers"
  [[ "$zip_id" != "$checksum_id" ]] || fail "ZIP and checksum assets must have distinct IDs"
  [[ "$zip_size" =~ ^[1-9][0-9]*$ && "$checksum_size" =~ ^[1-9][0-9]*$ ]] || fail "asset sizes must be positive integers"
  [[ "$zip_digest" =~ ^sha256:[a-f0-9]{64}$ && "$checksum_digest" =~ ^sha256:[a-f0-9]{64}$ ]] || fail "asset API digests must be canonical SHA-256 values"
  [[ "$zip_digest" == "sha256:${product_sha256}" ]] || fail "ZIP API digest does not match the approved product SHA-256"
  [[ "$observed_zip_url" == "$zip_url" ]] || fail "ZIP browser-download URL does not match the exact repository/tag/name contract"
  [[ "$observed_checksum_url" == "$checksum_url" ]] || fail "checksum browser-download URL does not match the exact repository/tag/name contract"

  SNAP_RELEASE_ID="$release_id"
  SNAP_ZIP_ID="$zip_id"
  SNAP_CHECKSUM_ID="$checksum_id"
  SNAP_ZIP_SIZE="$zip_size"
  SNAP_CHECKSUM_SIZE="$checksum_size"
  SNAP_ZIP_DIGEST="$zip_digest"
  SNAP_CHECKSUM_DIGEST="$checksum_digest"
  SNAP_SECURITY="$(jq -cS --arg zip "$zip_name" --arg checksum "$checksum_name" '{release:{id:.id,tag_name:.tag_name,name:.name,draft:.draft,prerelease:.prerelease},assets:(.assets | map(select(.name == $zip or .name == $checksum) | {name,id,state,size,digest,browser_download_url}) | sort_by(.name))}' <<<"$release_json")"
}

first_json="$(snapshot_release)"
validate_snapshot "$first_json"
first_release_id="$SNAP_RELEASE_ID"
first_zip_id="$SNAP_ZIP_ID"
first_checksum_id="$SNAP_CHECKSUM_ID"
first_zip_size="$SNAP_ZIP_SIZE"
first_checksum_size="$SNAP_CHECKSUM_SIZE"
first_zip_digest="$SNAP_ZIP_DIGEST"
first_checksum_digest="$SNAP_CHECKSUM_DIGEST"
first_security="$SNAP_SECURITY"

umask 077
assert_trusted_root_identity
staging_dir="$(mktemp -d -p "$trusted_root_canonical" '.monitor-durable-release.XXXXXXXXXX')"
chmod 700 -- "$staging_dir"
[[ -d "$staging_dir" && ! -L "$staging_dir" ]] || fail "verifier-owned staging directory was not created as a real directory"
[[ "$(dirname -- "$staging_dir")" == "$trusted_root_canonical" ]] || fail "verifier-owned staging directory escaped the trusted root"
[[ "$(realpath -e -- "$staging_dir")" == "$staging_dir" ]] || fail "verifier-owned staging directory is not canonical"
[[ "$(stat -Lc '%a' "$staging_dir")" == 700 ]] || fail "verifier-owned staging directory permissions must be 0700"
staging_identity="$(stat -Lc '%d:%i' "$staging_dir")"

zip_tmp_name=".${zip_name}.download"
checksum_tmp_name=".${checksum_name}.download"
zip_tmp="${staging_dir}/${zip_tmp_name}"
checksum_tmp="${staging_dir}/${checksum_tmp_name}"
zip_path="${staging_dir}/${zip_name}"
checksum_path="${staging_dir}/${checksum_name}"
cleanup_armed=true

assert_staging_identity() {
  assert_trusted_root_identity
  [[ -d "$staging_dir" && ! -L "$staging_dir" ]] || fail "verifier-owned staging directory was replaced during verification"
  [[ "$(dirname -- "$staging_dir")" == "$trusted_root_canonical" ]] || fail "verifier-owned staging directory was reparented during verification"
  [[ "$(realpath -e -- "$staging_dir")" == "$staging_dir" ]] || fail "verifier-owned staging directory escaped its canonical path"
  [[ "$(stat -Lc '%d:%i' "$staging_dir")" == "$staging_identity" ]] || fail "verifier-owned staging directory identity changed during verification"
  [[ "$(stat -Lc '%a' "$staging_dir")" == 700 ]] || fail "verifier-owned staging directory permissions changed during verification"
}

cleanup_owned_path() {
  local path="$1"
  [[ -n "${staging_identity:-}" ]] || return 0
  [[ "$(dirname -- "$path")" == "$trusted_root_canonical" ]] || return 0
  [[ -d "$path" && ! -L "$path" ]] || return 0
  [[ "$(stat -Lc '%d:%i' "$path" 2>/dev/null || true)" == "$staging_identity" ]] || return 0
  rm -f -- \
    "${path}/${zip_tmp_name}" \
    "${path}/${checksum_tmp_name}" \
    "${path}/${zip_name}" \
    "${path}/${checksum_name}" 2>/dev/null || true
  rmdir -- "$path" 2>/dev/null || true
}

cleanup_workspace() {
  [[ "${cleanup_armed:-false}" == true ]] || return 0
  [[ -d "$trusted_root_canonical" && ! -L "$trusted_root_canonical" ]] || return 0
  [[ "$(realpath -e -- "$trusted_root_canonical" 2>/dev/null || true)" == "$trusted_root_canonical" ]] || return 0
  [[ "$(stat -Lc '%d:%i' "$trusted_root_canonical" 2>/dev/null || true)" == "$trusted_root_identity" ]] || return 0
  cleanup_owned_path "$destination"
  cleanup_owned_path "$staging_dir"
}

trap cleanup_workspace EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

assert_staging_identity
[[ ! -e "$destination" && ! -L "$destination" ]] || fail "destination appeared before durable-release verification completed"
set -o noclobber
if ! gh api -H "Accept: application/octet-stream" "repos/${repository}/releases/assets/${first_zip_id}" >"$zip_tmp"; then
  set +o noclobber
  fail "exact-ID ZIP asset download failed"
fi
assert_staging_identity
[[ ! -e "$destination" && ! -L "$destination" ]] || fail "destination appeared before durable-release verification completed"
if ! gh api -H "Accept: application/octet-stream" "repos/${repository}/releases/assets/${first_checksum_id}" >"$checksum_tmp"; then
  set +o noclobber
  fail "exact-ID checksum asset download failed"
fi
set +o noclobber
assert_staging_identity
[[ ! -e "$destination" && ! -L "$destination" ]] || fail "destination appeared before durable-release verification completed"

for path in "$zip_tmp" "$checksum_tmp"; do
  [[ -f "$path" && ! -L "$path" ]] || fail "downloaded asset must be a regular non-symlink file"
  [[ "$(stat -Lc '%h' "$path")" == 1 ]] || fail "downloaded asset must have exactly one hard link"
  [[ "$(stat -Lc '%a' "$path")" == 600 ]] || fail "downloaded asset permissions must be 0600"
done

[[ "$(stat -c%s "$zip_tmp")" == "$first_zip_size" ]] || fail "downloaded ZIP size differs from the first REST snapshot"
[[ "$(stat -c%s "$checksum_tmp")" == "$first_checksum_size" ]] || fail "downloaded checksum size differs from the first REST snapshot"

zip_hash="$(sha256sum "$zip_tmp" | awk '{print $1}')"
checksum_hash="$(sha256sum "$checksum_tmp" | awk '{print $1}')"
[[ "$zip_hash" == "$product_sha256" ]] || fail "downloaded ZIP bytes do not match the approved product SHA-256"
[[ "$first_zip_digest" == "sha256:${zip_hash}" ]] || fail "downloaded ZIP bytes do not match the first REST API digest"
[[ "$first_checksum_digest" == "sha256:${checksum_hash}" ]] || fail "downloaded checksum bytes do not match the first REST API digest"

checksum_line="$(cat "$checksum_tmp")"
[[ "$checksum_line" == "${product_sha256}  ${zip_name}" ]] || fail "checksum asset is not the canonical approved product checksum line"

second_json="$(snapshot_release)"
validate_snapshot "$second_json"
[[ "$SNAP_RELEASE_ID" == "$first_release_id" ]] || fail "release ID changed during verification"
[[ "$SNAP_SECURITY" == "$first_security" ]] || fail "release or asset security metadata changed during verification"
[[ "$SNAP_ZIP_ID" == "$first_zip_id" && "$SNAP_CHECKSUM_ID" == "$first_checksum_id" ]] || fail "asset IDs changed during verification"
[[ "$SNAP_ZIP_SIZE" == "$first_zip_size" && "$SNAP_CHECKSUM_SIZE" == "$first_checksum_size" ]] || fail "asset sizes changed during verification"
[[ "$SNAP_ZIP_DIGEST" == "$first_zip_digest" && "$SNAP_CHECKSUM_DIGEST" == "$first_checksum_digest" ]] || fail "asset digests changed during verification"

assert_staging_identity
[[ ! -e "$destination" && ! -L "$destination" ]] || fail "destination appeared before atomic directory publication"
[[ ! -e "$zip_path" && ! -L "$zip_path" && ! -e "$checksum_path" && ! -L "$checksum_path" ]] || fail "final durable-release output names must not pre-exist in staging"
mv -T --no-clobber -- "$zip_tmp" "$zip_path"
[[ ! -e "$zip_tmp" && ! -L "$zip_tmp" ]] || fail "ZIP finalization encountered an unexpected name collision"
mv -T --no-clobber -- "$checksum_tmp" "$checksum_path"
[[ ! -e "$checksum_tmp" && ! -L "$checksum_tmp" ]] || fail "checksum finalization encountered an unexpected name collision"
assert_staging_identity

mapfile -t staged_entries < <(find "$staging_dir" -mindepth 1 -maxdepth 1 -printf '%f\n' | sort)
[[ "${#staged_entries[@]}" -eq 2 && "${staged_entries[0]}" == "$zip_name" && "${staged_entries[1]}" == "$checksum_name" ]] || fail "final staging payload must contain exactly the ZIP and checksum"
for path in "$zip_path" "$checksum_path"; do
  [[ -f "$path" && ! -L "$path" ]] || fail "final staged asset must be a regular non-symlink file"
  [[ "$(stat -Lc '%h' "$path")" == 1 ]] || fail "final staged asset must have exactly one hard link"
  [[ "$(stat -Lc '%a' "$path")" == 600 ]] || fail "final staged asset permissions must be 0600"
done
[[ "$(sha256sum "$zip_path" | awk '{print $1}')" == "$product_sha256" ]] || fail "final staged ZIP bytes changed before publication"
[[ "$(cat "$checksum_path")" == "${product_sha256}  ${zip_name}" ]] || fail "final staged checksum bytes changed before publication"

assert_trusted_root_identity
assert_staging_identity
[[ ! -e "$destination" && ! -L "$destination" ]] || fail "destination appeared before atomic directory publication"
mv -T --no-clobber -- "$staging_dir" "$destination"
[[ ! -e "$staging_dir" && ! -L "$staging_dir" ]] || fail "atomic directory publication encountered an unexpected destination collision"

assert_trusted_root_identity
[[ -d "$destination" && ! -L "$destination" ]] || fail "atomic directory publication did not produce the destination directory"
[[ "$(realpath -e -- "$destination")" == "$destination" ]] || fail "published destination is not canonical"
[[ "$(stat -Lc '%d:%i' "$destination")" == "$staging_identity" ]] || fail "published destination identity differs from verified staging"
[[ "$(stat -Lc '%a' "$destination")" == 700 ]] || fail "published destination permissions must remain 0700"

published_zip="${destination}/${zip_name}"
published_checksum="${destination}/${checksum_name}"
mapfile -t published_entries < <(find "$destination" -mindepth 1 -maxdepth 1 -printf '%f\n' | sort)
[[ "${#published_entries[@]}" -eq 2 && "${published_entries[0]}" == "$zip_name" && "${published_entries[1]}" == "$checksum_name" ]] || fail "published durable-release payload must contain exactly the ZIP and checksum"
for path in "$published_zip" "$published_checksum"; do
  [[ -f "$path" && ! -L "$path" ]] || fail "published durable-release asset must be a regular non-symlink file"
  [[ "$(stat -Lc '%h' "$path")" == 1 ]] || fail "published durable-release asset must have exactly one hard link"
  [[ "$(stat -Lc '%a' "$path")" == 600 ]] || fail "published durable-release asset permissions must be 0600"
done
[[ "$(sha256sum "$published_zip" | awk '{print $1}')" == "$product_sha256" ]] || fail "published ZIP bytes changed during atomic directory publication"
[[ "$(cat "$published_checksum")" == "${product_sha256}  ${zip_name}" ]] || fail "published checksum bytes changed during atomic directory publication"

cleanup_armed=false
trap - EXIT HUP INT TERM

echo "Durable release verification passed for ${tag}: release ${first_release_id}, assets ${first_zip_id}/${first_checksum_id}."
