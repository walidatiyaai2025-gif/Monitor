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
mkdir -p "$fixture_dir" "$fake_bin" "$state_dir"

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
    -H)
      shift 2
      ;;
    *)
      break
      ;;
  esac
done
endpoint="${1:-}"
case "$endpoint" in
  repos/example-owner/Monitor/releases/tags/v1.2.3-rc.1)
    count_file="${FAKE_GH_STATE_DIR}/release-count"
    count=0
    [[ -f "$count_file" ]] && count="$(cat "$count_file")"
    count=$((count + 1))
    printf '%s' "$count" >"$count_file"
    if [[ "${FAKE_GH_MUTATE_ON_SECOND:-0}" == 1 && "$count" -ge 2 ]]; then
      cat "${FAKE_GH_FIXTURE_DIR}/release-mutated.json"
    else
      cat "${FAKE_GH_FIXTURE_DIR}/release.json"
    fi
    ;;
  repos/example-owner/Monitor/releases/assets/101)
    cat "${FAKE_GH_FIXTURE_DIR}/Monitor-1.2.3-rc.1-win-x64.zip"
    ;;
  repos/example-owner/Monitor/releases/assets/102)
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
  bash "$verifier" \
    --repository "$repository" \
    --tag "$tag" \
    --version "$version" \
    --product-sha256 "$product_sha" \
    --destination "$destination"
}

printf '0' >"${state_dir}/release-count"
unset FAKE_GH_MUTATE_ON_SECOND || true
run_verifier "${work}/positive" >/dev/null
[[ "$(sha256sum "${work}/positive/${zip_name}" | awk '{print $1}')" == "$product_sha" ]]
[[ "$(cat "${work}/positive/${checksum_name}")" == "${product_sha}  ${zip_name}" ]]

printf '0' >"${state_dir}/release-count"
export FAKE_GH_MUTATE_ON_SECOND=1
if run_verifier "${work}/mutated" >"${work}/mutated.stdout" 2>"${work}/mutated.stderr"; then
  echo 'TOCTOU mutation case unexpectedly passed durable release verification.' >&2
  exit 1
fi
grep -Fq 'release or asset security metadata changed during verification' "${work}/mutated.stderr"

echo 'Durable release verifier synthetic positive and TOCTOU mutation checks passed.'
