# Test Log

This is the durable, append-only record of significant test executions and gates. Do not renumber historical entries or overwrite results. Each execution, including a rerun, receives its own `TEST-YYYYMMDD-NNN` ID. Routine isolated compiles that are not meaningful tests do not require entries.

Record enough to reproduce and assess each run: date, related milestone/slice, objective, type, tested commit/build, exact scenario/setup, relevant debug configuration and actors/seeds, expected and observed behavior, explicit result (`PASS`, `FAIL`, `PARTIAL`, or `INCONCLUSIVE`), available evidence, warnings/findings, related or created issues, and next action. A passing test can still have findings that need follow-up.

## TEST-20260914-001 — M41 P9 — NPC-only integrated manual gate — 1 Blue vs 1 Red

- Date: 2026-09-14
- Milestone/slice: M41.4 / P9 — legacy migration and integrated QA
- Objective: validate NPC-only behavioral integration, threat ownership/release, incapacitation/recovery continuity, terminal cleanup, and F6/F10 interpretability in WorldRuntime.
- Type: MANUAL / INTEGRATION
- HEAD/build: `76f29524548326d8d7a606ecb13e9eb474b3be0a`
- Scenario/setup: WorldRuntime; Player present with Invisible-to-AI ON; Invincible ON available/active in debug, with no Player intervention; exactly one Blue and one Red NPC; F6/F10 observability visible; no manual combat intervention.
- Actors and setup data:
  - Blue: `actor_0b3b705339b945e09875f824fd735c79`; SpawnSequence `0`; DerivedSpawnSeed `-8496665147806961861`; observed primary loadout: rusted crowbar.
  - Red: `actor_f90f941df6dd46028e9580a7416f98ef`; SpawnSequence `1`; DerivedSpawnSeed `-2584258118139957038`; observed primary loadout: Lee-Enfield; protective vest equipped.
- Expected: both NPCs roam; Red recognizes Blue as a legitimate threat and fights; LostContact permits reacquisition; incapacitation/recovery preserves only valid combat context; death releases the target; F6/F10 makes the flow interpretable without persistent deliberate attacks on incapacitated/dead actors.
- Observed: both NPCs navigated/roamed; Red acquired Blue as a legitimate threat and transitioned Alerted → Fighting; LostContact was followed by reacquisition; Blue became Incapacitated; Red released Blue as an active hostile candidate and returned to Ambient/Idle; Blue recovered functional capacity and reacquired Red; Red resumed context via `COMBAT_CONTINUITY_RESUMED` and combat resumed; Blue later died; Red released the dead target and returned to Idle/Ambient. No deliberate persistent attack on the incapacitated/dead actor was observed. F6/F10 made the flow interpretable.
- Result: **PASS** — NPC-only behavioral/integration gate.
- Evidence: Mauro's reported WorldRuntime observations with F6/F10 visible. No screenshot or exported runtime log was attached to this record.
- Warnings/findings:
  - **P9 FOLLOW-UP REQUIRED:** runtime emitted legacy unqualified Global Content ID lookup `'human_standard_visual_rig'` resolving to `'core:human_standard_visual_rig'`. The message describes temporary Core-only compatibility for authored scene data/schema-v1 saves and says the reference should migrate. The source has not been identified. Before M41 closes, inspect whether it comes from an authored prefab/scene, `PlayerGameplayComposition`, legitimate save/schema-v1 compatibility, or another consumer. Do not assume the cause.
  - Non-blocking setup: WorldRuntime was opened once without a WorldSession and returned to MainMenu; a valid session was then created and the test ran normally.
  - `NAVIGATION_FAILED` due to `Incapacitated` was an expected state transition, not a P9 navigation defect.
- Related/created issues: no issue ID assigned by this test record; retain the legacy lookup as a P9 follow-up until its source is established.
- Next action: complete NPC↔Player manual integration gate and investigate/migrate the source of the legacy visual-rig reference before formal M41 closeout.

## P9 gate status after TEST-20260914-001

- Automated QA: PASS (recorded in the preceding P9 validation; no diagnostics were rerun for this documentation task).
- NPC-only manual integration: PASS — `TEST-20260914-001`.
- NPC↔Player manual integration: PENDING.
- Legacy visual-rig warning source review/migration: PENDING.
- M41: IN PROGRESS; P9 is not closed.
