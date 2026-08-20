---
name: old-scars-persistence-change
description: "Change Old Scars persistence, save/load recovery, durable identity, or rehydration. Use only when those boundaries change; not for ordinary gameplay changes that merely read saved state."
---

# Old Scars Persistence Change

Read the applicable sections of `Docs/Technical_Architecture.md`, `Docs/DataDriven_JSON_Rules.md`, and the active Roadmap before changing code or schemas.

- Preserve durable identity domains and Definition/Instance separation. Do not regenerate authored IDs, convert fungible stack units into individual identities, or add parallel persistence authorities without explicit authorization.
- Use the established transactional seam: semantic preflight before mutation, capture enough state for rollback, apply through existing services, canonical comparison where applicable, and actionable recovery logs.
- Treat migration/legacy normalization, invalid-present-data failure, rollback/recovery, and fresh-session rehydration as distinct cases. Add only the diagnostic/fault-injection coverage necessary to establish the changed boundary.
- Run focused persistence diagnostics and regressions for intersecting ownership, lifecycle, inventory/equipment, or world-state seams. A unit test or compile does not replace fresh-session evidence when the change requires it.
- Keep schema/envelope and package/asset changes minimal. Record any manual fresh-session gate explicitly; do not claim it from automation.
