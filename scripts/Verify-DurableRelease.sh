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
mkdir -m 700 -- "$destination"
[[ -d "$destination" && ! -L "$destination" ]] || fail "verifier-owned destination was not created as a real directory"
[[ "$(stat -Lc '%a' "$destination")" == 700 ]] || fail "verifier-owned destination permissions must be 0700"
workspace_identity="$(stat -Lc '%d:%i' "$destination")"

assert_workspace_identity() {
  [[ -d "$destination" && ! -L "$destination" ]] || fail "verifier-owned destination was replaced during verification"
  [[ "$(realpath -e -- "$destination")" == "$destination" ]] || fail "verifier-owned destination escaped its canonical path"
  [[ "$(stat -Lc '%d:%i' "$destination")" == "$workspace_identity" ]] || fail "verifier-owned destination identity changed during verification"
  [[ "$(stat -Lc '%a' "$destination")" == 700 ]] || fail "verifier-owned destination permissions changed during verification"
}

zip_path="${destination}/${zip_name}"
checksum_path="${destination}/${checksum_name}"
zip_tmp="${destination}/.${zip_name}.download"
checksum_tmp="${destination}/.${checksum_name}.download"

assert_workspace_identity
set -o noclobber
gh api -H "Accept: application/octet-stream" "repos/${repository}/releases/assets/${first_zip_id}" >"$zip_tmp"
assert_workspace_identity
gh api -H "Accept: application/octet-stream" "repos/${repository}/releases/assets/${first_checksum_id}" >"$checksum_tmp"
set +o noclobber
assert_workspace_identity

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

assert_workspace_identity
[[ ! -e "$zip_path" && ! -L "$zip_path" && ! -e "$checksum_path" && ! -L "$checksum_path" ]] || fail "final durable-release output names must not pre-exist"
mv -T --no-clobber -- "$zip_tmp" "$zip_path"
[[ ! -e "$zip_tmp" && ! -L "$zip_tmp" ]] || fail "ZIP finalization encountered an unexpected name collision"
mv -T --no-clobber -- "$checksum_tmp" "$checksum_path"
[[ ! -e "$checksum_tmp" && ! -L "$checksum_tmp" ]] || fail "checksum finalization encountered an unexpected name collision"
assert_workspace_identity

[[ -f "$zip_path" && ! -L "$zip_path" && -f "$checksum_path" && ! -L "$checksum_path" ]] || fail "atomic finalization did not produce both expected regular files"
[[ "$(sha256sum "$zip_path" | awk '{print $1}')" == "$product_sha256" ]] || fail "final ZIP bytes changed during atomic publication"
[[ "$(cat "$checksum_path")" == "${product_sha256}  ${zip_name}" ]] || fail "final checksum bytes changed during atomic publication"

echo "Durable release verification passed for ${tag}: release ${first_release_id}, assets ${first_zip_id}/${first_checksum_id}."
