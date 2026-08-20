---
name: old-scars-milestone-closeout
description: "Close an Old Scars milestone or significant change: audit Git, review the scoped diff and blast-radius validation, update only changed truths, review, commit, push, and verify synchronization. Do not use for trivial edits."
---

# Old Scars Milestone Closeout

Use for milestones, significant refactors, persistence, combat, worldgen, machines, streaming, or another change with meaningful blast radius. Read `AGENTS.md`, the active Roadmap/Current Milestone context, and only affected technical contracts.

1. Before staging, audit branch/base, `git status --short`, and the relevant diff. Preserve unrelated local changes; never reset, clean, amend published work, rebase, or force-push without authorization.
2. Review `git diff --stat` before detailed diffs. Confirm scope, System Harmony, identity/Definition/Instance boundaries, no parallel authority, and proportional regressions.
3. Run the applicable compile, diagnostic, automated, and manual-validation gates. Do not call historical diagnostics obsolete merely because a later milestone exists; run only seams the change can affect.
4. Update canonical documentation only when its truth changed; keep `Development_Log.md` append-only. Record pending manual evidence honestly.
5. Use `/review` or the available dedicated review flow for this skill's triggering changes. Address material findings, then re-review the affected diff when needed.
6. Run `git diff --check`; verify links/paths, no secrets, no versioned local absolute paths, and no accidental package/tooling changes.
7. Commit with a descriptive body, inspect `git log -1 --format=full`, push the authorized target, and verify `HEAD == origin/dev`, divergence `0/0`, and clean state. Where an isolated worktree was required, integrate safely first and remove only that task worktree afterwards.

If a manual Unity gate is required and not passed, stop before claiming closeout or integrating.
