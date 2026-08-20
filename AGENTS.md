# Old Scars — Codex Instructions

Old Scars is a Unity/C# game. Codex supports implementation and technical execution; Mauro retains creative and product authority. Do not autonomously redefine the game's direction or production scope.

## Source Of Truth

- The repository establishes what exists technically; it does not turn a prototype into design canon.
- [Docs/Game_Design_Document.md](Docs/Game_Design_Document.md) is the maintainable design baseline. An external GDD is historical/reference material.
- [Docs/Project_Roadmap.md](Docs/Project_Roadmap.md) owns milestone IDs, states, dependencies, and gates. [Docs/Current_Milestone.md](Docs/Current_Milestone.md) is the operating snapshot; [Docs/Next_Sprints.md](Docs/Next_Sprints.md) is the real near-term queue.
- [Docs/Development_Log.md](Docs/Development_Log.md) is append-only. [Docs/Technical_Architecture.md](Docs/Technical_Architecture.md) and [Docs/DataDriven_JSON_Rules.md](Docs/DataDriven_JSON_Rules.md) own implemented technical and data contracts.
- Escalate a material creative, product, authority, or scope ambiguity to Mauro. Do not silently reconcile conflicting sources.

## System Harmony

Before changing a feature, inspect the relevant authorities, interfaces, services, registries, events, Definitions/Instances, IDs, and existing contracts. Reuse them before adding anything new.

- A locally correct feature is not done if it needlessly blocks reasonable interaction with an existing system.
- Do not create parallel authorities, per-content C# exceptions, speculative universal managers, or foundations without a current consumer.
- Assess regressions by systemic blast radius, not lines changed. Preserve identity, ownership, atomicity, rollback, Core/mod infrastructure, and current contracts when they are in scope.
- Do not begin an unauthorized milestone or expand gameplay, data, persistence, or scenes merely for convenience.

## Data-Driven And Modding

- JSON declares content and parameters; C# implements closed system logic. Avoid content-specific classes such as `Crowbar.cs`.
- Definitions are not Instances. Preserve the established identity domains; Global Content IDs are canonical `namespace:local_id`, `core` is reserved, and tags/runtime IDs/save IDs/asset keys remain separate domains.
- Core content and external mods should use the same infrastructure when reasonable. Load and validate content rather than reading JSON continuously during gameplay.
- When content/data changes are in scope, use `$old-scars-data-content`; read the current JSON Rules and technical contracts rather than copying them into prompts.

## Proportional Workflow

Use the smallest prompt and investigation that can safely establish the requested outcome.

- Small task: goal, important constraints, done condition, and validation.
- Normal task: goal, relevant systems, included/excluded scope, done condition, and validation.
- Cross-system or architectural task: authorities, dependencies, integration, blast radius, regressions, explicit subagent roles/count, and Git strategy.

Use [Docs/Milestone_Template.md](Docs/Milestone_Template.md) as a proportional prompt template, not a compulsory mega-prompt. Work in **Objective** mode by default; use **Plan** only when material investigation/design is needed (for example authority migration, delicate persistence, worldgen, streaming, machine runtime, or a cross-system refactor).

Model policy: use **Terra** by default for normal implementation, bugs, diagnostics, and integration; **Luna** for mechanical, predictable, repetitive, or routine content/documentation work; **Sol** for delicate architecture, persistence, durable identity, worldgen, streaming, authority changes, or hard cross-system bugs. Use **Ultra** only when its extra reasoning is justified. Use **Standard** speed; do not recommend Fast for Old Scars.

Default to zero subagents. Request a deliberate number and bounded roles only when independent investigation, integration audit, regression review, or specialized QA materially reduces risk. One integrator owns coupled edits and the final decision.

For a long interruptible task, maintain only this short state in the active Codex task/conversation: `ACTIVE TASK`, `GOAL`, `BASE`, `CHANGES MADE`, `VALIDATION PASSED`, `VALIDATION PENDING`, `KNOWN FAILURE`, `DO NOT TOUCH`, `NEXT EXACT STEP`. Do not put ephemeral checkpoints in canonical docs or commit them by default.

## Git And Worktrees

Preserve user work. Before material mutations, inspect proportional Git state; for isolated or risky work, use a real Git worktree from the intended base.

- Never reset, clean, restore, stash, rebase, amend a published commit, or force-push without explicit authorization.
- Review `git diff --stat` before a large diff, then inspect the relevant files. Use filtered searches/log excerpts before opening huge output.
- A validated mutating task normally closes with a descriptive commit body, `git log -1 --format=full`, push, `HEAD == origin/dev`, divergence `0/0`, and a clean tree. Exceptions: read-only audit, failed checks, a scope block, unrelated local changes, or explicit no-publish instruction.
- The checkout that hosts the user must remain untouched when the task explicitly requires an isolated worktree. Remove only the worktree created for the completed task after a safe integration; never remove another worktree.

## Unity And Validation

Terminal/CLI/batchmode and deterministic diagnostics are allowed when relevant. Never control the desktop graphically or terminate Mauro's Unity GUI. A task-created hung batchmode process may be stopped only after confirming its identity; remove `Temp/UnityLockfile` only when no valid project Unity process remains.

- Prefer official/deterministic Unity tooling, structured output, exit codes, project diagnostics, and focused tests. Raw batchmode remains a fallback.
- Compilation is not completion. Keep static checks, runtime/editor compile, automated diagnostics, manual Unity acceptance, console review, and documentation review distinct.
- Do not rerun suites that cannot reveal new information. Fix real failures; do not invent preventative refactors after the acceptance criteria pass.
- Filter logs first (`ERROR`, `FAIL`, `Exception`, `CSxxxx`, diagnostic name, head/tail/context). Failure-boundary logs must be actionable; important success logs must be brief and correlatable.
- For a visual task, attach and inspect the actual screenshots. Describe the visible defect and connect it to code/layout/scene/asset; request visual confirmation afterwards. Compilation alone is not visual evidence.

Use `$old-scars-unity-validation` for Unity validation and `$old-scars-persistence-change` only when changing persistence or durable identity.

## Documentation And Closeout

Update only documentation whose truth changed. Keep the Development Log append-only; do not rewrite historical statuses. Use `$old-scars-milestone-closeout` for a milestone, significant refactor, or other closeout that needs review and publication.

The technical-contract map is [Docs/OldScars_Development_Rules.md](Docs/OldScars_Development_Rules.md). It is a navigation reference, not a second workflow policy.
