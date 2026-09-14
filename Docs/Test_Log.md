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

## TEST-20260914-002 — P9 — Current authored visual-rig namespace migration validation

- Date: 2026-09-14
- Milestone/slice: M41.4 / P9 — current authored visual-rig reference migration
- Objective: verify canonical visual-rig IDs in current authored assets and validate Player bootstrap through the real MainMenu → WorldRuntime application flow, leaving legacy loader compatibility unchanged.
- Type: REGRESSION / INTEGRATION
- HEAD/build: `b49dee4477aab59e33898465c24c5727d41bff37`, with the two listed authored asset changes in the working tree during the run.
- Scenario/setup: canonical checkout and `Library`; Unity `6000.4.6f1` batchmode; `OldScars.EditorTools.WorldSessionApplicationDiagnostics.RunBatchPlayMode`. MainMenu created a valid WorldSession, entered WorldRuntime, bound the shared Player composition, and continued through this existing application-flow diagnostic. Static asset search checked current `visualRigProfileId` references.
- Files changed for this test: `Assets/_OldScars/Resources/PFB_PlayerGameplayComposition.prefab` and `Assets/Scenes/SampleScene.unity`, each changing `visualRigProfileId` from `human_standard_visual_rig` to `core:human_standard_visual_rig`.
- Expected: runtime compilation succeeds; MainMenu creates/opens a valid session; Player spawns/binds and Gameplay Runtime reaches ready; current Player authored visual-rig lookup emits no legacy-unqualified warning; schema-v1/old-authored compatibility code remains unchanged by this scoped edit.
- Observed: Tundra build succeeded; `[OldScars/Data] Load OK — 0 errors, 0 warnings`; MainMenu created a session; WorldRuntime emitted `PLAYER_BOUND` for ActorInstanceId `actor_428ff8c97755a501eb6709c3adc65ded`, source `NewGameSafeSpawn`, profile `core:debug_player_inventory_01`; `GAMEPLAY_RUNTIME_READY` reported Player/Camera/InventorySession/NeedsPanel/HealthWindow/WorldClock/WorldInteraction each ready; diagnostic ended `World Session Application Play Flow: PASS`. No `human_standard_visual_rig` legacy lookup warning occurred in the log. Static search found no remaining authored `visualRigProfileId: human_standard_visual_rig`; the existing humanoid representation and Core JSON references remain qualified.
- Result: **PASS** — compile, authored reference scan, Player spawn/bind and WorldRuntime readiness passed.
- Evidence: `Logs/P9_CurrentVisualRigMigration.log` (workspace diagnostic output; not staged). It includes the build result, Player bind/readiness, and final diagnostic verdict.
- Warnings/findings:
  - Unity reported `Failed to convert -1 to a unsigned 32 bit int` while importing `PFB_PlayerGameplayComposition.prefab`. The prefab's `m_Bits: -1` value is present unchanged at HEAD; this field was not altered by the namespace migration.
  - Other legacy unqualified Global Content ID warnings appeared for `debug_house_npc_01`, `debug_closed_door_01`, `debug_locked_door_01`, `house_oven_loot_01`, `house_fridge_loot_01`, `house_countertop_loot_01`, `house_cupboard_loot_01`, `house_upper_cupboard_loot_01`, `rusted_crowbar_01`, and `lee_enfield_rifle_01`. These are outside the visual-rig reference change and were not migrated or source-audited by this test.
  - Unity startup reported an unavailable licensing access token and duplicate assemblies from AI Assistant/Pipeline packages. No C# compile error was reported.
  - The diagnostic intentionally exercised gameplay-save capture failure and cross-world payload rejection; those expected failure branches did not prevent its final PASS.
- Related/created issues: no issue ID assigned. The unrelated unqualified-ID warnings remain visible findings for future scoped review.
- Next action: run the P9 NPC↔Player manual integration gate; do not close M41 until that acceptance and remaining P9 work are complete.

## P9 gate status after TEST-20260914-002

- Automated QA: PASS.
- NPC-only manual integration: PASS — `TEST-20260914-001`.
- Current authored visual-rig namespace migration: PASS — `TEST-20260914-002`; schema-v1 compatibility code unchanged (not separately exercised by this test).
- NPC↔Player manual integration: PENDING.
- M41: IN PROGRESS; P9 is not closed.
