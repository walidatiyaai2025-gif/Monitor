# AGENTS.md

## Team operating rules

1. Work from `docs/IMPLEMENTATION_PLAN.md`; do not invent an untracked parallel plan.
2. Every material feature/change must update `docs/STATUS.md` and `docs/FEATURE_CATALOG.md` in the same PR.
3. Keep `main` stable. Development happens on task/agent branches and is merged only after validation.
4. Never present mock monitoring values as production data.
5. Never store plaintext credentials. Development credentials must be represented by secure hashes or external secret configuration.
6. Browser/UI components must never connect directly to monitored SQL Servers.
7. UI animation must be client-side and must not generate database collection calls.
8. Prefer snapshot-first collection: one collector result can feed many UI components.
9. Keep visual design consistent with the design tokens in `wwwroot/css/site.css` and the rules in `docs/UI_DESIGN_SYSTEM.md`.
10. Before completing a task: restore, build, run applicable tests, visually validate affected screens, commit, push, and update project tracking docs.
