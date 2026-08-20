# Old Scars — Development Contract Map

This document is a compact navigation reference. It does not define a second set of Codex workflow instructions and does not replace the authority of the Roadmap, Current Milestone, or technical contracts.

## Authority By Question

| Question | Source of truth |
| --- | --- |
| Product direction, decision pending, design baseline | [Game_Design_Document.md](Game_Design_Document.md), with Mauro as final authority |
| Milestone ID, state, dependency, gate | [Project_Roadmap.md](Project_Roadmap.md) |
| Active operational state and next real work | [Current_Milestone.md](Current_Milestone.md), [Next_Sprints.md](Next_Sprints.md) |
| Historical evidence | [Development_Log.md](Development_Log.md), append-only |
| Implemented C# authority, services, persistence, identity, ownership, transactions | [Technical_Architecture.md](Technical_Architecture.md) |
| Content schemas, IDs, validation, mod loading | [DataDriven_JSON_Rules.md](DataDriven_JSON_Rules.md) |
| Gate criteria, evidence, and risk register | [Production_Gates_and_Risks.md](Production_Gates_and_Risks.md) |
| Durable agent workflow | [../AGENTS.md](../AGENTS.md) and repo-local skills |

## Technical Invariants To Locate Before Changing Them

- Content is data-driven: JSON declares Definitions and C# runs closed logic. Core content and mods use the same content infrastructure where reasonable.
- Definitions, item instances, actor instance IDs, persistent scene IDs, save slots, tags, and asset keys are separate identity domains. Read the current contract before changing an ID or reference boundary.
- `ItemStorage`, ownership, Equipment, and item-owned storage retain their existing transactional and identity guarantees. UI/visual consumers do not become storage authorities.
- Current Slice load/recovery, durable identity, and save compatibility live in the persistence contracts. A persistence change requires preflight, transaction/rollback, recovery, and fresh-session considerations.
- Existing diagnostics and validation seams remain available unless the authorized change supersedes them with equivalent evidence.

## Reading Route

Read only the contracts that intersect the requested blast radius. For a new milestone, a rebaseline, a concrete contradiction, or a status/continuity change, read in this order:

1. `AGENTS.md`;
2. Roadmap, Current Milestone, Development Log, and Next Sprints;
3. JSON Rules and the technical/design contracts directly implicated.

Do not treat a past milestone label as a future restriction: M36-M41 foundations are implemented according to their current canonical documents. Their expansion still needs authorized scope.

## Optional Tooling

Unity MCP uses a reachable Editor Pipeline server. That technical requirement does not make Unity CLI or Pipeline a general Old Scars workflow dependency.
