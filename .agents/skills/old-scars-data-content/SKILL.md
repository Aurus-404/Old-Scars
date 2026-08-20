---
name: old-scars-data-content
description: "Change Old Scars JSON content, definitions, tags, validators, or mod-loading behavior while preserving data-driven and moddable contracts. Do not use for runtime-only C# changes with no content boundary."
---

# Old Scars Data Content

Read `Docs/DataDriven_JSON_Rules.md` and the directly implicated runtime/validator contracts before editing.

- JSON declares content and parameters; C# owns closed behavior. Do not solve a content request with content-specific C# classes.
- Preserve canonical `namespace:local_id` Global Content IDs; `core` is reserved. Tags, runtime/instance IDs, persistent scene IDs, save slots, and asset keys are distinct domains.
- Preserve Definitions versus Instances. Core content and external mods should use shared infrastructure where reasonable.
- Update the loader, validator, and runtime together only when the authorized schema change truly requires all three. Validate `type`, registered tags, duplicate IDs, and broken references; unknown or future placeholders are not contracts.
- Do not put save/runtime state or free scripting into Definitions, and do not introduce a mod override/dependency system unless authorized.
- Run the narrow validator and affected diagnostics; document a JSON contract only if it changed.
