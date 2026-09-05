# Old Scars — Codex Instructions

Old Scars is a Unity/C# game. Codex supports implementation and technical execution; Mauro retains creative and product authority. Do not autonomously redefine the game's direction or production scope.

## Source Of Truth

- Start continuity work from [Docs/Development_Context_Index.md](Docs/Development_Context_Index.md). It explains which document answers which question and what to read when a development chat/session changes.
- The repository establishes what exists technically; it does not turn a prototype into design canon.
- [Docs/Game_Design_Document.md](Docs/Game_Design_Document.md) is the maintainable design baseline. An external GDD is historical/reference material.
- [Docs/Project_Roadmap.md](Docs/Project_Roadmap.md) owns milestone IDs, states, dependencies, and gates. [Docs/Current_Milestone.md](Docs/Current_Milestone.md) is the operating snapshot; [Docs/Next_Sprints.md](Docs/Next_Sprints.md) is the real near-term queue.
- [Docs/Issue_Registry.md](Docs/Issue_Registry.md) stores bugs, confirmed/suspected defects and resolved history. [Docs/Implementation_Backlog.md](Docs/Implementation_Backlog.md) stores approved smaller mechanics/technical improvements that should not be forgotten but are not roadmap milestones or bugs.
- [Docs/Development_Log.md](Docs/Development_Log.md) is append-only. [Docs/Technical_Architecture.md](Docs/Technical_Architecture.md) and [Docs/DataDriven_JSON_Rules.md](Docs/DataDriven_JSON_Rules.md) own implemented technical and data contracts.
- Research/decision notes may exist for active areas. In the current NPC combat block, [Docs/NPC_Combat_Targeting_Research.md](Docs/NPC_Combat_Targeting_Research.md) is the research basis for aim/accuracy decisions; it does not claim unimplemented behavior is already architecture.
- Escalate a material creative, product, authority, or scope ambiguity to Mauro. Do not silently reconcile conflicting sources.

## System Harmony

Before changing a feature, inspect the relevant authorities, interfaces, services, registries, events, Definitions/Instances, IDs, and existing contracts. Reuse them before adding anything new.

- A locally correct feature is not done if it needlessly blocks reasonable interaction with an existing system.
- Do not create parallel authorities, per-content C# exceptions, speculative universal managers, or foundations without a current consumer.
- Assess regressions by systemic blast radius, not lines changed. Preserve identity, ownership, atomicity, rollback, Core/mod infrastructure, and current contracts when they are in scope.
- Do not begin an unauthorized milestone or expand gameplay, data, persistence, or scenes merely for convenience.
- Sunk cost is not a reason to preserve a bad seam. Remove or replace obsolete coordination/compatibility after its real consumers have migrated and tests prove the replacement.

## Research-First Routing

Do not spend Codex quota re-auditing a repository problem when the cause can be established from GitHub/repository review outside Codex and the task already provides that evidence.

Preferred flow:

1. repository research establishes likely/confirmed cause, affected authorities, scope and DONE criteria;
2. Codex verifies the named seam in the canonical checkout rather than restarting an exhaustive audit;
3. Codex implements the smallest correct change and runs Unity/local validation that cannot be done from repository review alone;
4. Codex reports exactly what changed, evidence, regressions and Git state;
5. the published commit is reviewed again before the next phase.

Codex should still investigate locally when the answer depends on uncommitted files, Unity runtime/editor state, generated assets, logs, scene state or other evidence unavailable from the repository.

## Execution Autonomy And Instruction Precedence

For an authorized implementation or fix, carry the task through the smallest correct implementation and proportional verification. Do not stop at a plan or ask for confirmation merely because a routine implementation detail could be chosen safely.

- Make routine, reversible assumptions when they do not change product direction, authorized scope, authority boundaries, destructive Git state, or required manual/visual acceptance.
- Ask Mauro only when missing information materially affects correctness, creative/product authority, scope, an irreversible or destructive action, or a required manual Unity/visual gate.
- Explicit task instructions from Mauro take precedence over conflicting repo-local skill guidance unless a higher-priority safety, permission, or destructive-operation boundary applies.
- If a repo-local skill blocks, pauses, or redirects requested work, identify the skill and the concrete rule responsible rather than stopping vaguely.
- Complete all unaffected authorized work before waiting on a manual gate. Never fabricate manual, visual, runtime, MCP, or fresh-session evidence.

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

Use [Docs/Milestone_Template.md](Docs/Milestone_Template.md) as a proportional prompt template, not a compulsory mega-prompt. Work in **Objective** mode by default; use **Plan** only when material local investigation/design is genuinely needed (for example authority migration, delicate persistence, worldgen, streaming, machine runtime, or a cross-system refactor whose cause was not already established).

Model policy: use **Terra** by default for normal implementation, bugs, diagnostics, and integration; **Luna** for mechanical, predictable, repetitive, or routine content/documentation work; **Sol** for delicate architecture, persistence, durable identity, worldgen, streaming, authority changes, or hard cross-system bugs. Use **Astra**, when available, selectively for unusually large or ambiguous end-to-end tasks where one model must coordinate substantial research, implementation, validation, and review across several systems, or where its larger-context orchestration materially reduces risk; Astra is not a project prerequisite and does not replace Terra as the default or Sol for every delicate seam. Use **Ultra** only when its extra reasoning is justified. Use **Standard** speed; do not recommend Fast for Old Scars.

Default to zero subagents. Request a deliberate number and bounded roles only when independent investigation, integration audit, regression review, or specialized QA materially reduces risk. One integrator owns coupled edits and the final decision. Astra does not change this rule: do not delegate merely because delegation is available.

For a long interruptible task, maintain only this short state in the active Codex task/conversation: `ACTIVE TASK`, `GOAL`, `BASE`, `CHANGES MADE`, `VALIDATION PASSED`, `VALIDATION PENDING`, `KNOWN FAILURE`, `DO NOT TOUCH`, `NEXT EXACT STEP`. Do not put ephemeral checkpoints in canonical docs or commit them by default.

## Git And Canonical Checkout

Preserve user work. Codex works directly in the canonical checkout `D:\Programs\UnityProject\Old Scarss` unless Mauro explicitly changes this workflow.

- Do not create Git worktrees, clones, temporary Unity project copies, or alternate project folders for normal Old Scars tasks.
- Before material mutations, inspect the current branch/status and distinguish user-owned changes from task changes.
- Never reset, clean, restore, stash, rebase, amend a published commit, or force-push without explicit authorization.
- Review `git diff --stat` before a large diff, then inspect the relevant files. Use filtered searches/log excerpts before opening huge output.
- Stage only files intentionally changed by the task. Unrelated user-owned dirty files may remain dirty and must be preserved.
- A validated mutating task normally closes with a descriptive commit body, `git log -1 --format=full`, push, `HEAD == origin/dev`, and divergence `0/0`. A clean tree is not required when unrelated user-owned changes are intentionally preserved.

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

When an implementation reveals a future mechanic/improvement rather than a defect, put it in [Docs/Implementation_Backlog.md](Docs/Implementation_Backlog.md), not in the milestone roadmap and not as a fake bug. When a real defect is discovered, use [Docs/Issue_Registry.md](Docs/Issue_Registry.md).

The technical-contract map is [Docs/OldScars_Development_Rules.md](Docs/OldScars_Development_Rules.md). It is a navigation reference, not a second workflow policy.
