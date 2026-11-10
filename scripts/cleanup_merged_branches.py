#!/usr/bin/env python3
"""Prune only branches whose current tip is proven integrated via a merged PR.

No branch with an open PR, a protected ref, or an unmatched/changed merged PR tip
will be deleted. Default execution is read-only; --apply is restricted to exact main.
"""
import argparse
import json
import os
import sys
from urllib.error import HTTPError
from urllib.parse import quote
from urllib.request import Request, urlopen

REPO = "walidatiyaai2025-gif/Monitor"
API = "https://api.github.com"


def request(path, method="GET"):
    token = os.environ.get("GH_TOKEN", "")
    if not token:
        raise RuntimeError("GH_TOKEN is required; no anonymous/degraded cleanup")
    req = Request(
        API + path,
        method=method,
        headers={
            "Authorization": "Bearer " + token,
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "Monitor-safe-branch-cleanup",
        },
    )
    try:
        with urlopen(req, timeout=45) as response:
            body = response.read()
            return json.loads(body) if body else {}
    except HTTPError as error:
        if error.code == 404 and method == "GET":
            return None
        detail = error.read(512).decode("utf-8", errors="replace")
        raise RuntimeError(f"GitHub {method} {path}: HTTP {error.code}: {detail}") from error


def pages(path, size=50):
    for page in range(1, 50):
        separator = "&" if "?" in path else "?"
        data = request(f"{path}{separator}per_page={size}&page={page}")
        if not isinstance(data, list):
            raise RuntimeError(f"Unexpected/non-list GitHub response for {path}")
        yield from data
        if len(data) < size:
            return
    raise RuntimeError(f"Pagination safeguard exceeded for {path}")


def exact_ref(name):
    encoded = quote(name, safe="/")
    value = request(f"/repos/{REPO}/git/ref/heads/{encoded}")
    expected = "refs/heads/" + name
    if not isinstance(value, dict) or value.get("ref") != expected:
        return None
    obj = value.get("object", {})
    if obj.get("type") != "commit":
        return None
    return obj.get("sha")


def main_sha():
    value = exact_ref("main")
    if not value:
        raise RuntimeError("Cannot establish exact main SHA")
    return value


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="Delete verified merged branches")
    args = parser.parse_args()

    metadata = request(f"/repos/{REPO}")
    if not isinstance(metadata, dict) or metadata.get("full_name") != REPO:
        raise RuntimeError("Repository identity mismatch")
    if metadata.get("default_branch") != "main":
        raise RuntimeError("Default branch is no longer main")
    observed_main = main_sha()

    if args.apply:
        if os.environ.get("GITHUB_REPOSITORY") != REPO:
            raise RuntimeError("Runner repository mismatch")
        if os.environ.get("GITHUB_REF") != "refs/heads/main":
            raise RuntimeError("Apply is only permitted on refs/heads/main")
        if os.environ.get("GITHUB_SHA") != observed_main:
            raise RuntimeError("Main moved since workflow invocation; fail closed")

    branches = list(pages(f"/repos/{REPO}/branches", size=100))
    closed = list(pages(f"/repos/{REPO}/pulls?state=closed"))
    open_prs = list(pages(f"/repos/{REPO}/pulls?state=open"))

    active = {
        pr["head"]["ref"]
        for pr in open_prs
        if pr.get("head", {}).get("repo", {}).get("full_name") == REPO
    }
    proven = set()
    for pr in closed:
        head = pr.get("head") or {}
        if not pr.get("merged_at"):
            continue
        if (head.get("repo") or {}).get("full_name") != REPO:
            continue
        if head.get("ref") and head.get("sha"):
            proven.add((head["ref"], head["sha"]))

    eligible, held = [], []
    for branch in branches:
        name = branch.get("name", "")
        sha = (branch.get("commit") or {}).get("sha")
        if not name or not sha or name == "main":
            continue
        if branch.get("protected") or name in active:
            held.append((name, "protected or active PR"))
        elif (name, sha) in proven:
            eligible.append((name, sha))
        elif sha == observed_main:
            eligible.append((name, sha))
        else:
            held.append((name, "no matching merged PR at exact current head"))

    print(f"main={observed_main} all_non_main={len(eligible)+len(held)} "
          f"eligible={len(eligible)} retained={len(held)} apply={args.apply}", flush=True)
    for name, reason in held:
        print(f"RETAIN {name}: {reason}")
    for name, sha in eligible:
        print(f"{'CANDIDATE' if not args.apply else 'VERIFY'} {name} {sha}")

    if not args.apply:
        print("DRY RUN: zero writes performed", flush=True)
        return

    # Refuse to execute on a partial/inconsistent listing.
    if len(eligible) + len(held) != len(branches) - 1:
        raise RuntimeError("Incomplete/duplicate branch inventory")
    deleted = []
    for index, (name, expected_sha) in enumerate(eligible):
        if index % 10 == 0 and main_sha() != observed_main:
            raise RuntimeError("Main changed during cleanup; stopped")
        current = exact_ref(name)
        if current != expected_sha:
            print(f"SKIP {name}: branch changed/disappeared ({current})")
            continue
        # Revalidate that the branch has not acquired an open PR.
        encoded_head = quote(REPO.split("/")[0] + ":" + name, safe="")
        fresh_open = list(pages(f"/repos/{REPO}/pulls?state=open&head={encoded_head}"))
        if fresh_open:
            print(f"SKIP {name}: active PR appeared")
            continue
        request(f"/repos/{REPO}/git/refs/heads/{quote(name, safe='/')}", method="DELETE")
        deleted.append(name)
        print(f"DELETED {name}", flush=True)

    remaining = {b["name"] for b in pages(f"/repos/{REPO}/branches", size=100)}
    unexpectedly_remaining = [b for b in deleted if b in remaining]
    if unexpectedly_remaining:
        raise RuntimeError("Post-delete read-back failed: " + ",".join(unexpectedly_remaining))
    if "main" not in remaining or main_sha() != observed_main:
        raise RuntimeError("Main identity changed during cleanup")
    print(f"VERIFIED: deleted={len(deleted)} remaining={len(remaining)} "
          f"non_main_remaining={len(remaining)-1}", flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"FAIL CLOSED: {error}", file=sys.stderr)
        sys.exit(1)
