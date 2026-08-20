# Old Scars — Task And Milestone Template

[Project_Roadmap.md](Project_Roadmap.md) owns milestone ID, state, dependencies, and gates. Match prompt detail to risk; do not add irrelevant fields merely to fill a template.

## Small Task

- Goal:
- Important constraints / do not touch:
- Done condition:
- Validation:

Use for localized fixes, routine content/docs, and mechanical transformations. Model: Luna when truly predictable, otherwise Terra. Objective mode; Standard speed.

## Normal Task

- Goal and expected state:
- Relevant existing systems/contracts to inspect:
- Included / excluded scope:
- Done condition:
- Validation proportional to blast radius:
- Documentation whose truth may change:

Use for normal implementation, bugs, diagnostics, and integration. Terra is the default; Objective mode and Standard speed.

## Cross-System Or Architecture Task

- Milestone or authorization context; current and expected state:
- Goal and product decision enabled:
- Authorities, dependencies, and contracts to preserve:
- Included / excluded scope; explicit no-goals:
- Identity, data, transaction, persistence, or ownership implications:
- System-harmony check and blast radius:
- Required regressions, validation, and any manual gate:
- Explicit subagents: count, read/write boundary, and purpose (default `0`):
- Git/worktree strategy and publication condition:

Use for authority migrations, delicate persistence, durable identity, worldgen, streaming, machine runtime, high-risk refactors, or difficult cross-system faults. Terra normally; Sol when the architectural or transactional risk justifies it. Plan mode only when design/research is materially unresolved. Standard speed.

## Closeout Addendum

- Result and acceptance evidence:
- Checks run / not applicable / pending manual evidence:
- Documentation changed because its truth changed:
- Deferred work and trigger:
- Commit title and body summary:
- Review required: `yes/no + reason`:
- Publication: branch, push, `HEAD == origin/dev`, divergence `0/0`, clean tree:

Use `$old-scars-milestone-closeout` for the closeout workflow. Do not call a compile pass manual validation, and do not publish a body-less commit.
