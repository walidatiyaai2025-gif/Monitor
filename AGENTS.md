# AGENTS.md

## Team operating rules

1. **Live state first.** Inspect exact GitHub `main`, open PRs/issues, active claims/branches and exact workflow/check results before selecting work.
2. Use `docs/CURRENT_EXECUTION_PLAN.md` for the live work queue and dependency order. `docs/IMPLEMENTATION_PLAN.md` is historical implementation context unless a section is explicitly revalidated against current live state; never revive stale branches/merge gates from it by inference.
3. Prevent duplicate work. Recover a legitimate current branch/PR/claim before creating a new implementation, and prioritize exact-main regressions or stale READY integration over new features.
4. Every material feature/change must update `docs/STATUS.md` and `docs/FEATURE_CATALOG.md` in the same PR.
5. Keep `main` stable. Development happens on task/agent branches and is merged only after exact-head validation.
6. Never present mock monitoring values as production data.
7. Never store plaintext credentials. Development credentials must be represented by secure hashes or external secret configuration.
8. Browser/UI components must never connect directly to monitored SQL Servers.
9. UI animation must be client-side and must not generate database collection calls.
10. Prefer snapshot-first collection: one collector result can feed many UI components.
11. Keep visual design consistent with the design tokens in `wwwroot/css/site.css` and the rules in `docs/UI_DESIGN_SYSTEM.md`.
12. Before completing a task: restore, build, run applicable tests, visually validate affected screens where a real harness exists, commit/push, reconcile live project tracking, and leave no recoverable local-only work.
13. Owner-only and external gates remain fail-closed. Do not count previews, repository CI, synthetic acceptance or merged tooling as PASS without the required real owner/environment evidence.
14. Never claim `VERIFIED_FINAL_COMPLETE` while any required repository, OWNER_ONLY or EXTERNAL acceptance gate is unproven.
