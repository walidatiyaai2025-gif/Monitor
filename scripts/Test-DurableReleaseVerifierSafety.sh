#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
verifier="${repo_root}/scripts/Verify-DurableRelease.sh"
work="$(mktemp -d)"
trap 'rm -rf -- "$work"' EXIT

version="1.2.3-rc.1"
tag="v${version}"
repository="example-owner/Monitor"
zip_name="Monitor-${version}-win-x64.zip"
checksum_name="${zip_name}.sha256"
fixture_dir="${work}/fixtures"
fake_bin="${work}/fake-bin"
state_dir="${work}/state"
trusted_root="${work}/trusted"
mkdir -p "$fixture_dir" "$fake_bin" "$state_dir" "$trusted_root"
chmod 700 "$trusted_root"

printf 'synthetic verified candidate bytes\n' >"${fixture_dir}/${zip_name}"
product_sha="$(sha256sum "${fixture_dir}/${zip_name}" | awk '{print $1}')"
printf '%s  %s\n' "$product_sha" "$zip_name" >"${fixture_dir}/${checksum_name}"
checksum_sha="$(sha256sum "${fixture_dir}/${checksum_name}" | awk '{print $1}')"
zip_size="$(stat -c%s "${fixture_dir}/${zip_name}")"
checksum_size="$(stat -c%s "${fixture_dir}/${checksum_name}")"

jq -n \
  --arg tag "$tag" \
  --arg title "Monitor ${version}" \
  --arg zip "$zip_name" \
  --arg checksum "$checksum_name" \
  --arg zipDigest "sha256:${product_sha}" \
  --arg checksumDigest "sha256:${checksum_sha}" \
  --arg zipUrl "https://github.com/${repository}/releases/download/${tag}/${zip_name}" \
  --arg checksumUrl "https://github.com/${repository}/releases/download/${tag}/${checksum_name}" \
  --argjson zipSize "$zip_size" \
  --argjson checksumSize "$checksum_size" \
  '{id:77,tag_name:$tag,name:$title,draft:false,prerelease:true,assets:[{id:101,name:$zip,state:"uploaded",size:$zipSize,digest:$zipDigest,browser_download_url:$zipUrl},{id:102,name:$checksum,state:"uploaded",size:$checksumSize,digest:$checksumDigest,browser_download_url:$checksumUrl}]}' \
  >"${fixture_dir}/release.json"

jq '.assets[1].id = 103' "${fixture_dir}/release.json" >"${fixture_dir}/release-mutated.json"

cat >"${fake_bin}/gh" <<'FAKE_GH'
#!/usr/bin/env bash
set -euo pipefail
[[ "${1:-}" == api ]] || { echo "fake gh only supports api" >&2; exit 64; }
shift
while [[ $# -gt 0 ]]; do
  case "$1" in
    -H) shift 2 ;;
    *) break ;;
  esac
done
endpoint="${1:-}"

assert_hidden_stage_contract() {
  [[ "${FAKE_GH_ASSERT_STAGE:-0}" == 1 ]] || return 0
  [[ ! -e "${FAKE_GH_EXPECT_DEST}" && ! -L "${FAKE_GH_EXPECT_DEST}" ]] || { echo 'destination became visible before verification finished' >&2; exit 66; }
  mapfile -t stages < <(find "${FAKE_GH_TRUSTED_ROOT}" -mindepth 1 -maxdepth 1 -type d -name '.monitor-durable-release.*' -print)
  [[ "${#stages[@]}" -eq 1 ]] || { echo 'expected exactly one hidden staging directory' >&2; exit 67; }
  [[ "$(stat -c '%a' "${stages[0]}")" == 700 ]] || { echo 'staging directory is not private' >&2; exit 68; }
}

case "$endpoint" in
  repos/example-owner/Monitor/releases/tags/v1.2.3-rc.1)
    count_file="${FAKE_GH_STATE_DIR}/release-count"
    count=0
    [[ -f "$count_file" ]] && count="$(cat "$count_file")"
    count=$((count + 1))
    printf '%s' "$count" >"$count_file"
    if [[ "${FAKE_GH_CREATE_COLLISION_ON_SECOND:-0}" == 1 && "$count" -ge 2 ]]; then
      mkdir "${FAKE_GH_COLLISION_DEST}"
      printf 'collision-sentinel\n' >"${FAKE_GH_COLLISION_DEST}/sentinel.txt"
      unset FAKE_GH_CREATE_COLLISION_ON_SECOND
    fi
    if [[ "${FAKE_GH_MUTATE_ON_SECOND:-0}" == 1 && "$count" -ge 2 ]]; then
      cat "${FAKE_GH_FIXTURE_DIR}/release-mutated.json"
    else
      cat "${FAKE_GH_FIXTURE_DIR}/release.json"
    fi
    ;;
  repos/example-owner/Monitor/releases/assets/101)
    assert_hidden_stage_contract
    cat "${FAKE_GH_FIXTURE_DIR}/Monitor-1.2.3-rc.1-win-x64.zip"
    ;;
  repos/example-owner/Monitor/releases/assets/102)
    assert_hidden_stage_contract
    cat "${FAKE_GH_FIXTURE_DIR}/Monitor-1.2.3-rc.1-win-x64.zip.sha256"
    ;;
  *)
    echo "unexpected fake gh endpoint: $endpoint" >&2
    exit 65
    ;;
esac
FAKE_GH
chmod +x "${fake_bin}/gh"

export FAKE_GH_FIXTURE_DIR="$fixture_dir"
export FAKE_GH_STATE_DIR="$state_dir"
export PATH="${fake_bin}:${PATH}"

run_verifier() {
  local destination="$1"
  local root="${2:-$trusted_root}"
  bash "$verifier" \
    --repository "$repository" \
    --tag "$tag" \
    --version "$version" \
    --product-sha256 "$product_sha" \
    --trusted-root "$root" \
    --destination "$destination"
}

reset_snapshot_state() {
  printf '0' >"${state_dir}/release-count"
  unset FAKE_GH_MUTATE_ON_SECOND FAKE_GH_CREATE_COLLISION_ON_SECOND FAKE_GH_COLLISION_DEST FAKE_GH_ASSERT_STAGE FAKE_GH_EXPECT_DEST FAKE_GH_TRUSTED_ROOT || true
}

assert_no_hidden_staging() {
  [[ -z "$(find "$trusted_root" -mindepth 1 -maxdepth 1 -type d -name '.monitor-durable-release.*' -print -quit)" ]]
}

reset_snapshot_state
positive="${trusted_root}/positive"
export FAKE_GH_ASSERT_STAGE=1 FAKE_GH_EXPECT_DEST="$positive" FAKE_GH_TRUSTED_ROOT="$trusted_root"
run_verifier "$positive" >/dev/null
[[ "$(stat -c '%a' "$positive")" == 700 ]]
[[ "$(stat -c '%a' "${positive}/${zip_name}")" == 600 ]]
[[ "$(stat -c '%a' "${positive}/${checksum_name}")" == 600 ]]
[[ "$(sha256sum "${positive}/${zip_name}" | awk '{print $1}')" == "$product_sha" ]]
[[ "$(cat "${positive}/${checksum_name}")" == "${product_sha}  ${zip_name}" ]]
[[ "$(find "$positive" -mindepth 1 -maxdepth 1 -type f | wc -l)" -eq 2 ]]
assert_no_hidden_staging

reset_snapshot_state
existing="${trusted_root}/existing"
mkdir "$existing"
printf 'sentinel\n' >"${existing}/sentinel.txt"
if run_verifier "$existing" >"${work}/existing.stdout" 2>"${work}/existing.stderr"; then
  echo 'Existing-destination case unexpectedly passed durable release verification.' >&2
  exit 1
fi
grep -Fq 'destination must not already exist' "${work}/existing.stderr"
[[ "$(cat "${existing}/sentinel.txt")" == sentinel ]]

reset_snapshot_state
outside="${work}/outside"
mkdir "$outside"
symlink_destination="${trusted_root}/symlinked"
ln -s "$outside" "$symlink_destination"
if run_verifier "$symlink_destination" >"${work}/symlink.stdout" 2>"${work}/symlink.stderr"; then
  echo 'Symlink-destination case unexpectedly passed durable release verification.' >&2
  exit 1
fi
grep -Fq 'destination must not already exist' "${work}/symlink.stderr"
[[ -z "$(find "$outside" -mindepth 1 -maxdepth 1 -print -quit)" ]]

reset_snapshot_state
traversal_destination="${trusted_root}/../escape"
if run_verifier "$traversal_destination" >"${work}/traversal.stdout" 2>"${work}/traversal.stderr"; then
  echo 'Traversal-destination case unexpectedly passed durable release verification.' >&2
  exit 1
fi
grep -Fq 'destination must be a direct child of the trusted root' "${work}/traversal.stderr"
[[ ! -e "${work}/escape" && ! -L "${work}/escape" ]]

reset_snapshot_state
mutated="${trusted_root}/mutated"
export FAKE_GH_MUTATE_ON_SECOND=1 FAKE_GH_ASSERT_STAGE=1 FAKE_GH_EXPECT_DEST="$mutated" FAKE_GH_TRUSTED_ROOT="$trusted_root"
if run_verifier "$mutated" >"${work}/mutated.stdout" 2>"${work}/mutated.stderr"; then
  echo 'TOCTOU mutation case unexpectedly passed durable release verification.' >&2
  exit 1
fi
grep -Fq 'release or asset security metadata changed during verification' "${work}/mutated.stderr"
[[ ! -e "$mutated" && ! -L "$mutated" ]]
assert_no_hidden_staging

reset_snapshot_state
collision="${trusted_root}/collision"
export FAKE_GH_CREATE_COLLISION_ON_SECOND=1 FAKE_GH_COLLISION_DEST="$collision" FAKE_GH_ASSERT_STAGE=1 FAKE_GH_EXPECT_DEST="$collision" FAKE_GH_TRUSTED_ROOT="$trusted_root"
if run_verifier "$collision" >"${work}/collision.stdout" 2>"${work}/collision.stderr"; then
  echo 'Late destination collision unexpectedly passed durable release verification.' >&2
  exit 1
fi
grep -Fq 'destination appeared before atomic directory publication' "${work}/collision.stderr"
[[ "$(cat "${collision}/sentinel.txt")" == collision-sentinel ]]
assert_no_hidden_staging

reset_snapshot_state
bad_version_destination="${trusted_root}/bad-version"
if bash "$verifier" \
  --repository "$repository" \
  --tag 'v1.2.3-rc..1' \
  --version '1.2.3-rc..1' \
  --product-sha256 "$product_sha" \
  --trusted-root "$trusted_root" \
  --destination "$bad_version_destination" >"${work}/version.stdout" 2>"${work}/version.stderr"; then
  echo 'Non-canonical version case unexpectedly passed durable release verification.' >&2
  exit 1
fi
grep -Fq 'version format is invalid' "${work}/version.stderr"
[[ ! -e "$bad_version_destination" && ! -L "$bad_version_destination" ]]
assert_no_hidden_staging

echo 'Durable release verifier synthetic positive, cleanup, collision and TOCTOU checks passed.'
