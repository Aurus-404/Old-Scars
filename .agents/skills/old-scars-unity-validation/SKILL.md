---
name: old-scars-unity-validation
description: "Validate an Old Scars Unity change with the smallest relevant compile, diagnostic, test, log, and manual-evidence set. Use for Unity runtime, editor, scene, or visual changes; do not use for docs-only work."
---

# Old Scars Unity Validation

Read the affected contracts and identify the actual validation seam before executing tools.

- Prefer the project-proven deterministic path: focused diagnostics, structured output, exit codes, and relevant tests. Unity CLI/Pipeline may be used when already available and useful, but are not a workflow dependency; raw Unity batchmode remains a fallback.
- The configured Unity MCP bridge needs a reachable Editor Pipeline server. Keep `com.unity.pipeline` only while this project accepts MCP for real work; neither Pipeline nor the globally installed CLI is a universal Old Scars prerequisite.
- Compile the affected Runtime/Editor surfaces, run the direct diagnostic, and run only regressions that match the systemic blast radius. Keep automated, fresh-session/manual Unity, Console, and visual acceptance as separate evidence.
- Filter logs before opening them: `ERROR`, `FAIL`, `Exception`, `CSxxxx`, the diagnostic name, head/tail, then narrow context. Do not paste or read a large log by default.
- Never operate Mauro's desktop graphically or stop a user-owned Unity GUI. If a GUI connection is required for a read-only MCP check, provide the minimum opening/configuration steps and wait for confirmation.
- For visual defects, inspect provided screenshots and request visual confirmation after the targeted fix. Compilation does not prove a visual result.
- Report what passed, what was not run, and the exact manual step still pending. Do not migrate existing wrappers or diagnostics preemptively merely because a new CLI exists.
