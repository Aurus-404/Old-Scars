---
name: old-scars-unity-validation
description: "Validate an Old Scars Unity change with the smallest relevant compile, diagnostic, test, log, and manual-evidence set. Use for Unity runtime, editor, scene, or visual changes; do not use for docs-only work."
---

# Old Scars Unity Validation

Read the affected contracts and identify the actual validation seam before executing tools.

## Cost And Checkout Guardrails

- Validate in the canonical warm checkout `D:\Programs\UnityProject\Old Scarss` by default. This skill does **not** authorize creating a Git worktree, clone, temporary Unity project copy, alternate checkout, or fresh project folder.
- A fresh Unity checkout can trigger package resolution, a cold `Library`, project-wide asset import, shader/model/texture processing, compilation, and cache generation. Treat that as a major-cost operation in both elapsed time and Codex quota.
- Never create or recommend an alternate checkout merely for isolation, clean diffs, dirty-file avoidance, asset/content work, editor tooling, diagnostics, or a small/normal feature. Preserve unrelated dirty files and stage/review explicit task files instead.
- If the current task is running in a noncanonical/cold checkout that Mauro did not explicitly authorize after being warned about the import/cache cost, stop and report the workflow violation rather than triggering or continuing a cold import.
- A generated prompt or agent preference is not Mauro's explicit authorization for a worktree. The exception in `AGENTS.md` must be satisfied explicitly.
- Time and quota are validation resources. Once the requested acceptance criteria and regressions justified by the actual blast radius pass, stop.

## Validation Procedure

- Prefer the project-proven deterministic path: focused diagnostics, structured output, exit codes, and relevant tests. Unity CLI/Pipeline may be used when already available and useful, but are not a workflow dependency; raw Unity batchmode remains a fallback.
- Prefer an already-open or already-warm canonical Editor context when it can perform the needed checks safely. Avoid repeated Unity startups/import/compile cycles.
- The configured Unity MCP bridge needs a reachable Editor Pipeline server. Keep `com.unity.pipeline` only while this project accepts MCP for real work; neither Pipeline nor the globally installed CLI is a universal Old Scars prerequisite.
- Compile the affected Runtime/Editor surfaces, run the direct diagnostic, and run only regressions that match the systemic blast radius. Keep automated, fresh-session/manual Unity, Console, and visual acceptance as separate evidence.
- Do not widen validation after a task-specific PASS just to make closeout more exhaustive. Extra fixtures or suites need a concrete changed seam, an explicit acceptance requirement, or a real failure that expands the blast radius.
- If two diagnostics require clean Play sessions because their fixtures contaminate one another, run exactly the required separate sessions; do not turn that into a broad repeated regression sweep.
- Filter logs before opening them: `ERROR`, `FAIL`, `Exception`, `CSxxxx`, the diagnostic name, head/tail, then narrow context. Do not paste or read a large log by default.
- Never operate Mauro's desktop graphically or stop a user-owned Unity GUI. If a GUI connection is required for a read-only MCP check, provide the minimum opening/configuration steps and wait for confirmation.
- For visual defects, inspect provided screenshots and request visual confirmation after the targeted fix. Compilation does not prove a visual result.
- Report what passed, what was not run, and the exact manual step still pending. Do not migrate existing wrappers or diagnostics preemptively merely because a new CLI exists.
