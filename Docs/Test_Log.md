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

## P9 gate status after TEST-20260914-002

- Automated QA: PASS.
- NPC-only manual integration: PASS — `TEST-20260914-001`.
- Current authored visual-rig namespace migration: PASS — `TEST-20260914-002`; schema-v1 compatibility code unchanged (not separately exercised by this test).
- NPC↔Player manual integration: PENDING.
- M41: IN PROGRESS; P9 is not closed.

Historical next action after TEST-002: run the P9 NPC↔Player manual integration gate; do not close M41 until that acceptance and remaining P9 work are complete.

## TEST-20260914-003 — M41 P9 — NPC↔Player manual gate — Crowbar melee

- Date: 2026-09-14
- Milestone/slice: M41.4 / P9
- Objective: validate real Player threat acquisition, NPC encounter transition/navigation, productive melee damage and medical reception with Invincible enabled.
- Type: MANUAL / INTEGRATION
- HEAD/build: not specified in the manual evidence received.
- Scenario/setup: WorldRuntime; Player was a real target; Invisible-to-AI OFF during the productive test; Invincible ON; one Red combat NPC equipped with `core:rusted_crowbar_01`; F6/F10 available. Player did not need to kill the test NPC.
- Actors: Red NPC `actor_22a65e6767f343179e701ce202c5538a`; Player `actor_428ff8c97755a501eb6709c3adc65ded`.
- Expected: Red acquires Player, transitions through Encounter/Alerted/Fighting, navigates to engage, and melee damage reaches Player's productive medical pipeline without terminal death under Invincible.
- Observed (Mauro-reported): Red acquired Player as threat; behavior owner Ambient → Encounter; Idle → Alerted → Fighting; engagement navigation worked; melee hit the Player and medical processing applied damage. Health UI showed torso with 2 Moderate contusions; real pain was present. Player remained non-Dead with Invincible ON. No relevant new runtime AI/combat/health error was observed.
- Result: **PASS** — NPC↔Player crowbar melee manual gate.
- Evidence: Mauro-reported WorldRuntime/F6/F10/Health UI observations. No screenshot or exported runtime log was attached to this record.
- Warnings/findings: no KO or bleeding is claimed; neither was part of the reported evidence.
- Related/created issues: none.
- Next action: complete/report the firearm subtest and reconcile P9 closeout.

## TEST-20260914-004 — M41 P9 — NPC↔Player manual gate — Lee-Enfield firearm

- Date: 2026-09-14
- Milestone/slice: M41.4 / P9
- Objective: validate real Player threat acquisition, firearm physical hit/region resolution and medical damage reception with Invincible enabled.
- Type: MANUAL / INTEGRATION
- HEAD/build: not specified in the manual evidence received.
- Scenario/setup: WorldRuntime; Player `actor_428ff8c97755a501eb6709c3adc65ded`; Invincible ON. Setup required preconditioning: the initial attempt continued spawning crowbar NPCs; Mauro temporarily enabled Invisible-to-AI, removed the interfering crowbar NPC, left one Red with a rifle, restored Player acquisition eligibility, then let the rifle NPC execute the productive fight. This is not represented as a clean isolated spawn from t=0; the later productive flow still tested reacquisition and combat.
- Actor/loadout: rifle NPC `actor_d93769d4f432485687caab1f1aac2bf9`; `core:lee_enfield_rifle_01`; `.303 British` per productive observed loadout.
- Expected: rifle NPC reacquires Player and enters combat; physical shots resolve misses/world impacts and actor hits through semantic trace, BodyRegion, wound and medical consequences; Invincible prevents terminal death without making Player invisible/peaceful.
- Observed (Mauro-reported): rifle NPC acquired Player; Idle → Alerted → Fighting; navigation/engagement worked; Invincible did not make Player invisible or peaceful. CURRENT AIM was visible; physical shots included misses/world impacts and actor hits; semantic trace registered hits, including `ACTOR HIT | LeftArm`. Health UI later showed a severe untreated torso puncture, general Critical condition, slight bleeding and intense pain; debug vital reserve was approximately 18.8/100. Player remained non-Dead.
- Result: **PASS** — NPC↔Player Lee-Enfield firearm manual gate.
- Evidence: Mauro-reported WorldRuntime/F6/F10/semantic trace/Health UI/Console observations. No screenshot or exported runtime log was attached to this record.
- Warnings/findings: setup removal may appear in the log as `Lifecycle: Dead / FunctionalState: Unconscious`; this is the manually removed crowbar NPC setup artifact, not Player death and not an Invincible failure. Console reported OldScars/Data 0 errors and 0 warnings, with no relevant new AI/combat/Health/Condition/lifecycle/navigation exception. `Editor is not in automated mode` was a non-blocking Unity/Pipeline tooling warning. Do not claim Player KO/Unconscious; automated P7 covers Invincible/KO.
- Related/created issues: none.
- Next action: close P9/M41; next defect is ISSUE-0022 — Loaded Ammo Mass Conservation.

## P9 / M41 final gate status after TEST-20260914-004

- Automated QA: PASS (previously completed; not rerun for this documentation closeout).
- Legacy audit and current authored visual-rig migration: PASS — `TEST-20260914-002`; generic fallback/schema-v1 compatibility retained.
- NPC-only manual integration: PASS — `TEST-20260914-001`.
- NPC↔Player crowbar melee: PASS — `TEST-20260914-003`.
- NPC↔Player Lee-Enfield firearm: PASS — `TEST-20260914-004`, with setup caveat recorded above.
- Console review: PASS per Mauro's report; no new blocking M41 exception.
- M41 / M41.4 / P9: DONE / ACCEPTED / PUBLISHED — 2026-09-14.
- Next exact step: ISSUE-0022 — Loaded Ammo Mass Conservation.

## TEST-20260915-001 — ISSUE-0022 — Loaded ammo mass diagnostic first execution

- Date: 2026-09-15
- Milestone/slice: ISSUE-0022 — Loaded Ammo Mass Conservation.
- Objective: validate canonical round mass, reload/fire conservation, ownership transfers, drop/pickup, invalid states and Current Slice restore through production authorities.
- Type: AUTOMATED / INTEGRATION / EXPECTED-HISTORY FAIL.
- HEAD/build: `d92123ff7b5694c2b1cf4bd5272f6e55c85bccb4` plus the scoped ISSUE-0022 working tree later committed as `481183ddfac82f9ff9e547da6c72a1af13ecc088`; Unity `6000.4.6f1`, canonical checkout/Library, batchmode `-nographics`.
- Setup: `LoadedAmmoMassConservationDiagnostics`; Core Lee-Enfield, `.303` ammo/profile, real Player ownership/equipment/carry component, productive reload/fire/drop/pickup and Current Slice services.
- Expected: all conservation and transfer cases pass and the diagnostic restores its initial snapshot.
- Observed: data contract, cancel, partial/full reload, loaded rifle `4.45 kg`, save/load and equip/unequip cases passed before fixture setup attempted to add `core:medium_backpack_01`; the authored personal grid had no contiguous space for its `4x5` footprint.
- Result: **FAIL** — diagnostic fixture could not create the backpack; no mass-contract failure observed.
- Evidence: `Logs/Issue0022_LoadedAmmoMass.log`; final verdict `Loaded Ammo Mass Conservation Diagnostics: FAIL` with `NoGridSpace` immediately before the fixture exception.
- Findings: Play Mode discarded runtime mutations. The fixture, not gameplay, required deterministic grid preparation.
- Next action: clear temporary personal entries after the already-verified save/load stage, use the small backpack, rerun with a new Test ID.

## TEST-20260915-002 — ISSUE-0022 — Loaded ammo mass conservation diagnostic

- Date: 2026-09-15
- Milestone/slice: ISSUE-0022 — Loaded Ammo Mass Conservation.
- Objective: prove physical mass conservation and deterministic mod-safe round mass across the complete focused matrix.
- Type: AUTOMATED / INTEGRATION / REGRESSION.
- HEAD/build: same scoped implementation committed as `481183ddfac82f9ff9e547da6c72a1af13ecc088`; Unity `6000.4.6f1`, canonical checkout/Library, batchmode `-nographics`.
- Setup: empty firearm + 7 loose rounds; cancelled reload; insufficient/partial `0→7`; full `7→10` with 10 loose; one/multiple misses; dry fire; equip/unequip; loaded rifle into/out of owned small backpack; drop/pickup; Current Slice write/load; two mod item fixtures referencing one ammo profile; explicit item/profile mismatch and missing loaded profile.
- Expected: reload and same-root moves preserve total; one shot changes mass by `-0.025 kg`; 10-round rifle resolves `4.2 + 0.25 = 4.45 kg`; drop/pickup transfers `4.45 kg`; save/load preserves state and derived mass; ambiguous item ordering is irrelevant; invalid data/state is rejected.
- Observed: all assertions passed, initial snapshot cleanup compared equivalent, and Core data loaded with 0 errors/0 warnings.
- Result: **PASS**.
- Evidence: `Logs/Issue0022_LoadedAmmoMass_Rerun.log`; `Loaded Ammo Mass Conservation Diagnostics: PASS`.
- Findings: no manual visual gate is applicable; all acceptance criteria are numeric/state contracts.
- Next action: run M40 combat weapons regression and complete closeout.

## TEST-20260915-003 — ISSUE-0022 — Carry Weight / ItemWeightResolver regression gate

- Date: 2026-09-15
- Milestone/slice: ISSUE-0022; IMPL-0020 remains unstarted.
- Objective: verify the existing carry snapshot authority counts loaded ammo once across direct equipment, personal inventory, owned-storage subtree and world transfer boundaries.
- Type: AUTOMATED / COMPONENT-INTEGRATION REGRESSION.
- HEAD/build: same execution/build as `TEST-20260915-002`, committed as `481183ddfac82f9ff9e547da6c72a1af13ecc088`.
- Setup: `ActorCarryWeightComponent.GetSnapshot` through the focused diagnostic; 4.2 kg rifle, 10 × 0.025 kg rounds, equip/unequip, owned backpack, drop/pickup and invalid profile state.
- Expected: `4.45 kg` loaded entry; zero same-root delta; exact `4.45 kg` drop/pickup delta; no double count; invalid profile is not treated as zero.
- Observed: all carry-weight assertions in the focused execution passed. The repo has no separate historical Carry Weight diagnostic entrypoint, so this durable subgate records the direct authority coverage rather than inventing another suite.
- Result: **PASS**.
- Evidence: `Logs/Issue0022_LoadedAmmoMass_Rerun.log`; focused diagnostic final PASS and source assertions in `LoadedAmmoMassConservationDiagnostics`.
- Findings: no Encumbrance behavior, capacity policy or locomotion was changed/tested.
- Next action: keep IMPL-0020 separate; run M40 regression for the firearm seam.

## TEST-20260915-004 — ISSUE-0022 — M40 Combat Weapons regression

- Date: 2026-09-15
- Milestone/slice: ISSUE-0022 regression of M40 firearm/reload/persistence authority.
- Objective: ensure the new derived internal mass does not regress productive combat weapons behavior.
- Type: AUTOMATED / REGRESSION / FRESH-SESSION ROUND-TRIP.
- HEAD/build: scoped implementation committed as `481183ddfac82f9ff9e547da6c72a1af13ecc088`; Unity `6000.4.6f1`, canonical checkout/Library, batchmode `-nographics`.
- Setup: existing `M40CombatWeaponsDiagnostics.Run`, two Play sessions; partial/full/cancel reload, dry fire, world obstruction, actor hit, miss, drop/pickup, firearm save/load, legacy unloaded payload, semantic preflight failures and injected post-firearm-state rollback.
- Expected: existing M40 verdict PASS with exact firearm state/round consumption and rollback behavior.
- Observed: Core data loaded with 0 errors/0 warnings; expected injected apply failure rolled back; final M40 verdict PASS.
- Result: **PASS**.
- Evidence: `Logs/Issue0022_M40_Regression.log`; `M40.0 Combat Resolution & Weapons Diagnostics: PASS`.
- Findings: package duplicate-assembly and licensing token/entitlement messages remain preexisting tooling warnings; no C# compile error or diagnostic failure.
- Next action: documentation closeout, scoped commit, push and synchronization verification.
