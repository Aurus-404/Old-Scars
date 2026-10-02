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


## TEST-20261001-001 — IMPL-0020 — First diagnostic launch blocked by compile

- Date: 2026-10-01. Type: focused automated gate attempt in canonical warm Unity 6000.4.6f1 batchmode.
- Result: **FAIL**. Runtime compilation reported CS0103 for `definition`/`requested` in WorldItemEquipmentTransactionService; the functional fixture and Editor diagnostic did not execute.
- Cause/fix: admission-guard removal accidentally removed adjacent slot-selection statements. Reconstructed the task-owned file from its unchanged HEAD baseline and removed only the two weight guards plus their unused helper; all legitimate slot logic retained. No ItemWeightResolver, Persistence, Medical or user-owned change was involved.
- Evidence: `Temp/impl0020-run1.log`. This attempt does not count as a functional PASS; corrected compilation/diagnostic execution receives a new ID.

## TEST-20261001-002 — IMPL-0020 — Static contract migration gate

- Date: 2026-10-01. Type: STATIC / REPOSITORY CONTRACT CHECK.
- Result: **PASS**. Exhaustive targeted searches of OldScars C#, prefabs and scenes found no remaining physical admission/clamp contracts, old state or hard-limit tuning: ICarryWeightLimitedOwner, acceptance/quantity-limit structs, CarryWeightLimitExceeded, ClampIncomingToActorHardLimit, EvaluateIncoming APIs, HardBlocked, hardLimitMultiplier or weight-limited receipt metadata.
- Snapshot/formula: only ActorCarryWeightComponent classifies load and derives the locomotion factor. Player/Navigation consume it; debug panels display it. Default shared capacity remains 30 kg; minimum movement factor at 100% is provisional configurable 0.15, with ratio tolerance 0.000001.
- Boundaries: diff-path check confirms Persistence, its schema, ActorCondition/Medical and ItemWeightResolver remain unchanged. Runtime spawn adds the shared Carry component after Inventory/Ownership and before profile/loadout application. Transfer mutation/rollback/identity hooks remain in the existing services.
- Evidence: targeted repository searches and scoped diff review; compilation and real displacement/restore remain separate pending gates. Unrelated user-owned whitespace in MainMenu.unity is preserved and excluded from task checks/staging.


## TEST-20261001-003 — IMPL-0020 — Functional fixture first execution

- Date: 2026-10-01. Type: AUTOMATED / PLAY MODE / real CharacterController.
- Result: **FAIL**. Runtime/Editor compilation completed, then the fixture failed its physical displacement assertion at exactly 100% load. It had passed state/factor checks and real displacement at 50% (1.0009 m), 75% (1.0010 m) and 87.5% (0.5757 m). Storage, NPC, Search and Persistence cases did not execute.
- Setup caveat: this first fixture used an uncontrolled batchmode timestep and placed the CharacterController at world coordinate 1000; neither matches a deterministic representative movement measurement. No production failure cause is claimed from this run alone.
- Next run: keep the production movement/factor code and assertions, use a controlled 1/60-second game timestep and a fixture near the origin; report precise factor/speed/dt/travel on any recurrence. No tolerance was relaxed.
- Evidence: `Temp/impl0020-run2.log`. Historical failure retained; rerun receives a new ID.


## TEST-20261001-004 — IMPL-0020 — Controlled physical/storage/restore execution

- Date: 2026-10-01. Type: AUTOMATED / INTEGRATION / real Unity Play Mode at fixed 1/60-second game timestep, canonical Library.
- Result: **FAIL** overall: the fixture dereferenced an optional ActorThreatAcquisitionController absent on the selected runtime profile, after all Player/Storage/Equipment/Persistence assertions passed. NPC/Search cases did not execute. Runtime/Editor compile PASS.
- Passed evidence: Player 50% factor 1/travel 1.0000 m; 75% factor 1/travel 1.0667 m; 87.5% factor 0.575/travel 0.5750 m; exactly 100% factor 0.15/travel 0.1500 m; 100.0833% factor 0/travel 0.0000 m. Real gravity and rotation remained, fake sprint/stamina drain were absent, retained input recovered after a committed drop.
- Passed storage: Add over capacity, full exact overweight transfer with identity, quantity/grid/no-nesting atomic rejection, same-root bag mass conservation and external bag ingress. Existing transaction/snapshot/rollback authorities were retained.
- Passed Equipment: overload allowed personal equip/replacement, world equip/replacement and equipped-item transfer into another overloaded actor. Exact incoming/displaced IDs, unique ownership and real slot rejection were asserted.
- Passed Persistence: real Current Slice capture/apply preserved overloaded content, quantity/identity/state comparison and derived mass; dropping content recovered the factor. No Carry save field or schema changed. Fixture capacity was intentionally configured around the scene player's existing load; shared production tuning remains 30 kg.
- Caveat: the prior uncontrolled far-origin batch measurement failed at 100%; the unchanged production implementation passed this deterministic representative displacement measurement. No uncontrolled-framerate performance claim is made.
- Evidence: `Temp/impl0020-run3.log`, `[IMPL0020][PLAYER/STORAGE/EQUIPMENT/PERSISTENCE]` lines. Correct optional NPC reference and run only pending Navigation/Search cases; retain this FAIL and the already observed passing seams.

## TEST-20261001-005 — IMPL-0020 — Inventory Interaction UX regression

- Date: 2026-10-01. Type: AUTOMATED / REGRESSION, same Play session as TEST-20261001-004.
- Result: **PASS**. `Inventory Interaction UX Correction Diagnostics: PASS` covers external use, quantities, no implicit personal transfer, context actions, repeated exact one-unit transfers and full stack quick transfer.
- Change to fixture: mechanical migration to transfer APIs without the retired weight-policy parameter; assertions retained.
- Evidence: `Temp/impl0020-run3.log`. No additional broad M41 suite was run.


## TEST-20261001-006 — IMPL-0020 — Pending Navigation/Search gates

- Date: 2026-10-01. Type: AUTOMATED / PLAY MODE / focused remainder of the same Carry fixture, fixed 1/60-second game timestep.
- Result: **PASS**. Runtime/Editor compilation PASS; `[IMPL0020][NPC]` and `[IMPL0020][SEARCH]` PASS; diagnostic completion PASS. Player/storage/restore were not repeated after TEST-20261001-004 had supplied their passing evidence.
- NPC: real runtime composition supplied Carry at 30 kg; the configured NavMeshAgent speed matched the shared factor; real displacement slowed, then paused while preserving Moving/HasDestination/complete path with Failure None; clearing carried content automatically resumed the same path.
- Search: production Search ownership and a real valid path were preconditioned with a frozen last-known anchor. The test shortened the initial bounded travel deadline to 0.75 s, overloaded the actor for 1.3 s, verified Navigating beyond the original deadline, then unloaded and observed physical continuation within the preserved budget. No recovery deadline reset was used. A deliberately expired unblocked deadline produced Failed, and a real off-NavMesh request retained normal Navigation failure semantics.
- Fixture correction: optional ThreatAcquisition is guarded as in the existing Search diagnostic. The hidden target was spawned on valid NavMesh, then placed outside perception with its agent disabled, following the established fixture pattern.
- Evidence: `Temp/impl0020-navigation.log`. Expected NAVIGATION_FAILED warning belongs to the deliberate off-NavMesh negative case; no functional exception or compiler error. This validates physical/AI contracts, not a manual visual playtest.


## TEST-20261001-007 — IMPL-0020 — Mass regression launch blocked by shutdown lock

- Date: 2026-10-01. Type: AUTOMATED REGRESSION LAUNCH / TOOLING.
- Result: **FAIL** to launch; no mass or identity assertions executed. Unity reported another instance holding the canonical project.
- Cause: the task-created Navigation batch process remained alive after logging diagnostic PASS and `CodeReloadManager destroyed`; a bounded wait did not release its project lock. Its exact PID/command/project/executeMethod were verified before terminating only that hung batch process and its own crash/package helpers. No user-owned Unity GUI was stopped. The stale lock was removed only after verifying no valid canonical Editor remained.
- Evidence: `Temp/impl0020-navigation.log` (functional PASS) and `Temp/impl0020-mass-regression.log` (startup lock rejection). This does not invalidate TEST-20261001-006's observed physical/AI assertions and does not establish a gameplay failure.
- Next: launch the mass/identity regressions on the same canonical Library with a new Test ID.


## TEST-20261001-008 — IMPL-0020 — Identity/committed ownership/rollback regression

- Date: 2026-10-01. Type: AUTOMATED / EXISTING REGRESSION, Edit Mode before the mass fixture's Play session.
- Result: **PASS**. `M36.1 Checkpoint A Item Identity Diagnostics: PASS`.
- Justification: transfer APIs and their weight-only receipt metadata changed; exact identity, split/merge retirement, direct/root binding, committed transfer notifications and forced equipment/ownership rollback are required matching regressions. Existing assertions were retained; the one affected API call only lost its retired policy parameter.
- Evidence: `Logs/IMPL0020/impl0020-mass-regression-rerun.log`; the existing fixture resets its own runtime identity session before Play starts. No live user scene/session was reset.

## TEST-20261001-009 — IMPL-0020 — Loaded ammo mass regression while overloaded

- Date: 2026-10-01. Type: AUTOMATED / PLAY MODE / INTEGRATION / MASS REGRESSION.
- Result: **PASS**. `Loaded Ammo Mass Conservation Diagnostics: PASS`; Runtime/Editor compile PASS.
- Setup: existing diagnostic, existing content and unchanged mass assertions; configured scene actor capacity temporarily to 1 kg so its productive firearm/reload/equipment/drop/pickup paths operated Overloaded. Capacity restored to its initial configuration before cleanup; Carry is not saved.
- Covered: cancelled/partial/full reload, loaded firearm exact mass, Current Slice restoration, equip/unequip, owned backpack subtree, same-root transfers, productive fire mass subtraction, dry fire, invalid ammo-profile rejection, real loaded firearm drop/pickup and initial snapshot comparison/cleanup. All existing assertions retained.
- Evidence: `Logs/IMPL0020/impl0020-mass-regression-rerun.log`. No ItemWeightResolver or AmmoProfile changes. Navigation/Search were already covered by TEST-20261001-006, so no broad M41 suites were run.

## TEST-20261001-010 — IMPL-0020 — Acceptance evidence and console review

- Date: 2026-10-01. Type: ACCEPTANCE EVIDENCE REVIEW / CONSOLE REVIEW.
- Result: **PASS** for required functional coverage across TEST-20261001-004/005/006/008/009; prior FAIL records remain. TEST-20261001-004 is an overall fixture FAIL but its earlier Player/storage/Equipment/restore assertions provide explicit passing evidence; TEST-20261001-006 completes only the pending NPC/Search gates.
- Console: the accepted controlled Player/storage/restore run's only terminating exception was its corrected optional NPC fixture reference. The final Navigation/Search and mass/identity runs contain no compiler or functional exceptions; expected Navigation off-mesh and M36 negative-path warnings were reviewed as asserted failures. Startup package duplicate-assembly/test-assembly and licensing entitlement messages are environment diagnostics, not gameplay PASS claims.
- Manual/visual acceptance: **N/A** for this scope. Required consequences were measured using real CharacterController displacement/gravity/rotation/stamina, NavMeshAgent path pause/resume and production transaction/restore services. Debug UI only substitutes existing read-only labels/values; no visual layout, art, scene or final HUD design is accepted by compilation or invented screenshots.
- Environment caveat: validation ran in the canonical warm checkout with Mauro's pre-existing local Social/combat changes preserved. Those changes are excluded from IMPL-0020 publication; no isolated clean-checkout claim is made.
- Evidence retention: available run logs copied to ignored `Logs/IMPL0020` before further Unity use. Early Temp logs may be cleared by Unity; their observed errors/threshold excerpts are durably preserved in TEST-20261001-001/003. Available controlled-run/navigation/mass logs are retained locally. No other pending manual gate (including IMPL-0061) was accepted or published by this task.


## TEST-20261001-011 — IMPL-0020 — Final scoped publication review

- Date: 2026-10-01. Type: SCOPED DIFF / DOCUMENTATION / GIT PRESERVATION REVIEW.
- Result: **PASS**. Reviewed the complete task diff by changed seam and the 31-file staged manifest. `git diff --cached --check` passes; no unrelated assets, settings, packages, content/schema or user-owned Social/combat implementation is staged.
- Shared staging: HumanEncounterAIController publishes only three deadline-pause lines and the three-line base-speed fallback change; its pre-existing local changes remain unstaged. Development/Test history remains append-only; only IMPL-0020 entries and changed architecture/backlog truths are staged. All 13 unrelated pre-existing tracked files are byte-identical to the post-sync preservation snapshot.
- Contracts: final searches find no legacy physical admission/clamp APIs in staged code/prefabs. A single Carry formula serves Player/NPC/UI; no Medical coupling, mass-resolver change, schema bump or duplicated saved Carry state. Transfer receipt quantity/identity and existing transaction/rollback notification authorities remain.
- Documentation: IMPL-0020 closed, provisional 0.15/30 kg tuning explicit, genuine manual acceptance N/A explained, earlier FAIL results retained, IMPL-0063 promoted only as the next bounded task; IMPL-0021 remains behind it. Project_Roadmap owns no newly changed truth and was not edited ceremonially.
- Review method: direct complete scoped diff and evidence review in this zero-subagent task; no claim of an independent agent or manual Unity playtest.
- Next action: commit the exact reviewed staged content, push dev, and verify remote equality/divergence. Preserve the intentionally dirty user-owned checkout.


## TEST-20261001-012 — IMPL-0066 — Focused container capacity / equipped ergonomics

- Date: 2026-10-01. Type: AUTOMATED / CONNECTED UNITY EDITOR, canonical warm checkout; Unity 6000.4.6f1, SampleScene. Base cc5fc746731a9b4cf6808d25561140b4eb6bcd03 with preserved local Social/combat changes. No independent clean-checkout claim, alternate project, cold Library, GUI termination or restart.
- Result: **PASS**. Runtime/Editor compile passed; ContainerCarryDiagnostics completed DATA/STORAGE/CARRY/PERSISTENCE assertions and exact initial Current Slice cleanup.
- DATA: omitted JSON fields deserialize null; default unlimited max/multiplier 1, valid optional parameters and loaded Core 20/30/40 kg with 0.80/0.70/0.60. Existing DataValidator rejects max zero/negative/NaN/+Infinity/-Infinity and multiplier zero/negative/>1/NaN/+Infinity/-Infinity. Eleven intentionally emitted example:carrier validation errors are expected negative-test evidence, bounded by BEGIN/END markers, not unexpected runtime defects.
- STORAGE: unlimited/default ingress, below/equal cap quantity ingress, atomic overweight full-stack/exact rejection with actionable kg message; independent grid-bounds failure and valid exact placement; no-nesting retained. Directed merge of a 999-round source into a 998-round destination checks/commits one round, not 999. Separate over-cap merge with stack room rejects without mutations. Loaded firearm rounds count toward max; Equipment→storage rejects over cap and commits at equal loaded mass. Failure fingerprints compare entry IDs/quantities, placements, root ownership and content/layout versions. No weight clamp or automatic partial fitting was introduced.
- CARRY: personal→unequipped backpack preserves physical/effective mass; equip discounts only content, own mass full; unequip removes benefit immediately; equipped ingress/egress conserves physical mass and changes effective load. 75%/87.5% effective thresholds and overload use effective load, even when physical mass exceeds actor capacity. Overloaded actors can reorganize/unload; unloading restores factor. Loaded carrier drop removes complete subtree once, pickup into personal restores physical mass without ergonomics.
- PERSISTENCE: seed a pre-existing over-cap backpack via temporarily unlimited fixture ingress, restore original Core maximum before real Current Slice capture/load; exact snapshot comparison preserves contents, equipment, identities/quantities, physical/effective derived values. Future ingress rejects while over cap, content removal remains allowed, subsequent ingress succeeds after capacity is freed. The temporary profile/carry fixture values and initial slice are restored in finally; no production persistence schema change.
- Evidence: Logs/IMPL0066/focused-console.json and focused-result.txt; connected Editor SessionState result PASS. Functional assertions completed on the first run. Manual/game-feel/UI acceptance remains pending; tuning is provisional.

## TEST-20261001-013 — IMPL-0066 — Inventory interaction UX regression

- Date: 2026-10-01. Type: AUTOMATED / REGRESSION, same Play session as TEST-20261001-012.
- Result: **PASS**. Existing InventoryInteractionUxDiagnostics unmodified: external consumable use/exact consumption, actor needs, context actions and one-unit/full-stack transfers. The new structural rule reuses the existing guard/rejection path and focused TEST-012 covers its rejection message and atomicity.
- Evidence: Inventory Interaction UX Correction Diagnostics: PASS in Logs/IMPL0066/focused-console.json.

## TEST-20261001-014 — IMPL-0066 — Identity / committed ownership / rollback regression

- Date: 2026-10-01. Type: AUTOMATED / REGRESSION, Edit Mode prerequisite in existing LoadedAmmoMassConservationDiagnostics.Run().
- Result: **PASS**. M36ItemIdentityDiagnostics.RunAndLog().Passed prerequisite succeeded before entering mass Play session. Existing split/merge/transfer identity, committed ownership binding and forced rollback assertions were retained unchanged. No exception from the prerequisite or diagnostic launch.
- Evidence: successful connected-Editor launch of LoadedAmmoMassConservationDiagnostics.Run(), whose Require blocks entry if M36 fails; final mass PASS recorded in TEST-015. The Play transition clears the captured Console buffer, so this is prerequisite/control-flow evidence rather than a separately retained M36 console dump.

## TEST-20261001-015 — IMPL-0066 — Loaded ammo physical mass conservation

- Date: 2026-10-01. Type: AUTOMATED / REGRESSION, separate Play session in the same connected canonical Editor.
- Result: **PASS**. Existing LoadedAmmoMassConservationDiagnostics unmodified, all assertions retained: partial/full/cancelled reload, equipped and owned-backpack movement, Current Slice restore, productive fire mass loss, dry-fire no mass loss, invalid loaded-profile rejection, exact loaded-rifle drop/pickup and initial-state cleanup. Actor Carry capacity temporarily 1 kg still exercises overload without vetoing actions. CurrentWeightKg remains physical despite the new effective-load derivation.
- Evidence: Loaded Ammo Mass Conservation Diagnostics: PASS and successful issue0022_loaded/initial load records in Logs/IMPL0066/regressions-console.json. No unexpected Error/Exception/Assert in the captured regression Console. SampleScene returned to Edit Mode, not dirty.

## TEST-20261001-016 — IMPL-0066 — Final compile / scoped preservation / pending-gate review

- Date: 2026-10-01. Type: AUTOMATED COMPILE / STATIC REVIEW.
- Result: **PASS**. Final Runtime/Editor recompile reports completed, failed=false, errors=[] after using existing cached Equipment references in debug readouts and correcting the diagnostic evidence output to the canonical absolute Logs path. These final edits change presentation reference reuse/output path/comments only; functional suites were not redundantly rerun.
- Reviewed all nine changed productive files plus focused diagnostic/meta and eight changed docs. Task-only whitespace check passes. Thirteen unrelated dirty tracked files remain byte-identical to the post-sync snapshot; shared prior doc changes remain preserved. Append-only Development/Test history retained, no staged files, commit/push or protected settings/scenes mutation.
- No physical ItemWeightResolver, persistence schema, Medical, actor admission, IDs/slot-specific carrier logic, additional modifiers or IMPL-0063 implementation. Existing Carry threshold/locomotion consumers remain unchanged; focused effective-load tests cover that changed input seam. No broad M41/whole-project rerun.
- Evidence: Logs/IMPL0066/final-review.txt, task-manifest.json and connected recompile status. Expected TEST-012 validation errors are accounted for; no new known functional failure.
- **Manual acceptance: PENDING / NOT EXECUTED.** Required by Mauro's IMPL-0066 request; SampleScene I/F3 checklist in Pending_Manual_Validations.md. Automated PASS does not accept tuning, mark DONE or permit final publication. NEXT EXACT STEP remains IMPL-0066 manual acceptance.

- Observability limitation after final review: one additional read-only Pipeline eval of Editor scene/play state timed out at 5000 ms. No functional diagnostic or compile failed; the last successful scene/play read (after TEST-015) reported SampleScene, Edit Mode, not dirty. No retry, Unity restart or GUI control was performed. This tooling timeout does not constitute fresh scene-state confirmation.

## TEST-20261002-001 — IMPL-0066 — Mauro manual core / productive save-load

- Date recorded: 2026-10-02. Type: MANUAL / MAURO-REPORTED; formalizes the earlier remote playtest findings and Mauro's explicit checkpoint report. Not a new Codex-observed manual execution.
- Result: **PASS (MANUAL CORE)**. Physical/effective ergonomics; small backpack structural maximum 20 kg; rejection over capacity with free cells and no partial transfer; ingress resumes after freeing capacity. Earlier observed fixture: 6.30 kg physical → 5.34 kg effective; 800 × .303 = 20 kg rejects the extra 0.025 kg; after removal 17.53 kg content / 19.03 kg physical / 15.52 kg effective.
- Productive persistence: Mauro reports **PASS** through MainMenu → WorldRuntime → Save Game / Load Game. This evidence is separate from the automated Current Slice fixture. No persistence schema or derived-load state added.
- Evidence: remote Development Log playtest entries of 2026-10-01 and Mauro's 2026-10-02 publication/checkpoint instruction. Existing ISSUE-0028/0029/0030 and IMPL-0067/0068 follow-ups remain outside this implementation.
- **TUNING ACCEPTANCE PENDING:** comparative small/medium/large acceptance was not performed before Mauro left home. No DONE / ACCEPTED claim. Implementation checkpoint publication explicitly authorized despite that pending decision.

## TEST-20261002-002 — IMPL-0066 — Reconciled focused Container Carry gate

- Date: 2026-10-02, 09:21:26–09:21:39 UTC. Type: AUTOMATED / CONNECTED CANONICAL WARM UNITY 6000.4.6f1. Base 9c44c1377860ef0c47f67016954e34065a39c94f after preserving all four incoming documentation commits and unrelated local work; no source changed by reconciliation, independent clean-checkout claim, cold import, restart or GUI termination.
- Result: **PASS**. Recompile status up_to_date, failed=false, errors=[]; focused ContainerCarryDiagnostics completed DATA/STORAGE/CARRY/PERSISTENCE and exact initial-state cleanup. Covers optional/default/invalid profile validation, independent grid/kg admission, actual directed-merge quantity, atomic rejection, loaded-ammo structural mass, equip-only ergonomics, full own mass, effective locomotion thresholds, overload/unload, loaded carrier drop/pickup and real Current Slice over-cap restore/recovery.
- Console: eleven intentional example:carrier negative-validation errors between DATA BEGIN/END markers; no unexpected Error/Exception/Assert in the successful execution. Fresh SessionState PASS, phase empty, focused-result.txt written at 09:21:39 UTC; SampleScene returned to clean Edit Mode. Original saved MainMenu scene setup restored without saving scenes.
- Evidence: Logs/IMPL0066/checkpoint-console-20261002.json and focused-result.txt; connected Editor recompile/SessionState result. Historical TEST-20261001-012..016 retained unchanged; loaded-ammo/M36 suites reused rather than redundantly rerun.
- Tooling finding: an initial Pipeline launch request timed out at 20000 ms before diagnostic launch; a subsequent read confirmed MainMenu / idle / historical PASS. Only the later explicit SampleScene launch above produced this fresh PASS. No timeout is reported as a functional PASS. Background ticking was session-only; no graphical desktop control.

## TEST-20261002-003 — IMPL-0066 — Reconciled embedded Inventory UX regression

- Date: 2026-10-02. Type: AUTOMATED / REGRESSION, same single Play session as TEST-20261002-002.
- Result: **PASS**. Existing InventoryInteractionUxDiagnostics unmodified, invoked by the focused gate: consumable use/exact consumption, actor needs, context actions and quantity/full-stack transfers. No separate session or broad regression sweep.
- Evidence: Inventory Interaction UX Correction Diagnostics: PASS, console seq 5340 at 09:21:38 UTC, in Logs/IMPL0066/checkpoint-console-20261002.json.

## TEST-20261002-004 — IMPL-0066 — Checkpoint scope / preservation / dedicated review

- Date: 2026-10-02. Type: STATIC / PUBLICATION REVIEW. Result: **PASS**.
- Complete task diff reviewed before staging: nine productive files, focused diagnostic/meta, eight scoped docs. Dedicated Codex review found no actionable defects in structural admission, actual merge quantity, mass conservation, equipped-only load, atomicity or unchanged persistence. Documentation-only final review entry added after that review.
- Task-only whitespace check PASS; ItemWeightResolver, persistence/WorldSession schemas, worldgen and Packages unchanged; no global actor Carry admission limit, IMPL-0063 implementation or expanded scope. TEST-20261001-012..016 unchanged; unique test IDs and historical append-only records retained.
- Preservation: 13 unrelated tracked and 13 unrelated untracked files byte-identical to snapshots; prior Social/combat/doc changes excluded from the checkpoint. All four incoming remote doc commits remain ancestors; remote backlog IMPL-0067/0068/0069 tail intact and Development Log remote history unchanged. Shared document staging contains only IMPL-0066 changes.
- Evidence: scoped task-manifest, diff/stat, dedicated review output and preservation snapshots/checks retained outside Git; TEST-20261002-002/003 record the single final focused Unity execution. Implementation publication authorized with MANUAL CORE PASS / TUNING ACCEPTANCE PENDING; no DONE / ACCEPTED claim.


## TEST-20261002-005 — IMPL-0063 Stage 1 — Combined identity / seams / real runtime gate

- Date: 2026-10-02, 10:05:43–10:05:56 UTC. Type: AUTOMATED / CONNECTED CANONICAL WARM UNITY 6000.4.6f1, one Play session. Base 5965d23fed583c8f5168aa45ad4c405b24bcb01a; protected local work retained. No alternate checkout, cold Library, restart/GUI termination or broad suite.
- Result: **PASS**. Runtime/Editor compile completed, failed=false, errors=[] before execution. Initial compile caught CS0165 in the new diagnostic's short-circuit out-variable setup; corrected before the first functional gate, no runtime failure erased or rerun. The combined functional execution passed on its first run.
- Phase 0: real WorldRuntime loads fixed world_00630000000000000000000000000001 from committed schema-7 truth through existing world-session semantic preflight and temporary fixture store, world_pipeline_v5; active region projects MacroGeography, creates eight 2×2×2 technical chunks with Indexed MC, colliders, one local NavMesh and shared Player/runtime composition. Original default/release selection and schemas unchanged.
- A-H identity/baseline: duplicate committed snapshot reconstruction and reverse/different chunk order give identical key→baseline evidence; different WorldIds over identical seed/settings/committed fields yield different keys and identical baseline field evidence; material-layer layout change yields different layout/key; rendering-only road width does not. Both MT and Indexed MC consume the same volume and baseline mapping. Renaming/moving/reordering chunk representation and recreating the existing controller's generated representation with both backends leaves identity unchanged.
- Seams: exact shared baseline density AND material agreement for all adjacent X/Y/Z faces and independently reconstructed volume: X 1700, Y 2500, Z 1700 samples. Closed sample bounds include shared endpoints; no duplicated authoritative lattice.
- Mutation/runtime: real SubtractSphere and SubtractCapsule change current density while keeping keys/per-chunk baseline evidence unchanged. Exact rebuild-count checks cover affected chunks +1, unaffected +0, strict subset each; existing NavMesh contribution rebuilds once per mutation. Reset reproduces original mutable density. Single local NavMesh completes a physical surface path; real Player traverses >=4 Unity units on the existing collider with shared movement authority. Committed session payload remains byte-equivalent canonically.
- Evidence: Logs/IMPL0063/stage1-result.txt, stage1-console.json; fresh SessionState PASS/phase empty. Layout SHA-256 ff0ab7b4ba21ecd3da62e4d742b2e861c52bac5a25cff25bdd5812e9f8cb60d8; full eight-key/baseline mapping in evidence. Console: zero new Error/Exception/Assert during the successful gate. Returned to idle clean MainMenu scene setup; temporary fixture store removed, transient backend override cleared.
- Limits: reconstructed within one Unity process, no fresh-process claim, streaming or production terrain persistence. Stage 1 only; overall IMPL-0063 not DONE. IMPL-0066 remains IMPLEMENTED / AUTOMATED PASS / MANUAL CORE PASS / TUNING ACCEPTANCE PENDING.


## TEST-20261002-006 — IMPL-0063 Stage 1 — Dedicated review / diagnostic failure paths

- Date: 2026-10-02. Type: STATIC / DEDICATED CODE REVIEW. Result: **FAIL (diagnostic failure paths)**. The first functional combined gate TEST-20261002-005 remains PASS; no identity/layout/baseline/persistence defect was found.
- Findings: unexpected Play interruption left phase/root/scene cleanup pending and could resume the fixture in another Play session; content bootstrap had no readiness deadline and could wait indefinitely after data-validation failure.
- Scoped fix: detect unexpected idle Edit Mode as INCONCLUSIVE and run the existing exit/cleanup path; persist a 45-second bootstrap deadline across initial Play domain reload and reject reported content errors. No runtime terrain, mesher, identity encoding, save schema or protected files changed.
- Evidence: dedicated review report retained outside Git. TEST-005 original output preserved in Logs/IMPL0063/stage1-first-result.txt and stage1-console.json. Recompile and one combined gate rerun required after this real review/fix; this entry does not claim those later results.


## TEST-20261002-007 — IMPL-0063 Stage 1 — Combined rerun after diagnostic review fixes

- Date: 2026-10-02, 10:19:02–10:19:17 UTC. Type: AUTOMATED / CONNECTED CANONICAL WARM UNITY, one Play session after the concrete TEST-006 review/fix. No broader suite.
- Result: **PASS**. Runtime/Editor compile completed, failed=false, errors=[]; the complete unchanged functional assertions from TEST-005 passed again: committed WorldSession/runtime, A-H identity/baseline invariants, both meshers, X/Y/Z exact density/material seams, object recreation/Transform independence, sphere/capsule current-state mutation with unchanged baseline and affected-only rebuild counts, single local navigation path and real Player traversal.
- Cross-run evidence: stage1-first-result.txt and stage1-result.txt are exactly equal, including layout SHA-256, all eight canonical keys/baseline hashes and boundary sample counts. Final fresh SessionState PASS, phase empty, MainMenu restored in clean Edit Mode. Console has zero new errors/exceptions during this execution; temp fixture removed and selection override cleared.
- Evidence: Logs/IMPL0063/stage1-rerun-console.json (combined PASS seq 5540 at 10:19:17 UTC), stage1-result.txt and original stage1-first-result.txt. TEST-005 PASS and TEST-006 static FAIL history retained.
- Limits: normal combined run was exercised; interruption/content-error exits are statically re-reviewed, not claimed as separate injected runtime tests. Two combined executions total, second justified only by the concrete diagnostic lifecycle fixes; no fresh-process, streaming/persistence productization or manual visual claim.


## TEST-20261002-008 — IMPL-0063 Stage 1 — Affected diagnostic re-review / publication scope

- Date: 2026-10-02. Type: STATIC / DEDICATED AFFECTED-DIFF REVIEW. Result: **PASS**. Both TEST-006 findings fixed: interrupted Play records INCONCLUSIVE and executes existing exit/scene/temp cleanup; bootstrap rejects reported data errors and enforces a SessionState deadline surviving initial Play domain reload. This is static evidence, not an injected failure-path runtime test.
- Complete task diff and final docs reviewed; canonical SHA-256/helper, closed baseline sample bounds, current immutable material field and compatibility input list retained. No additional identity/baseline/persistence defect found by the full dedicated review. Final functional TEST-007 PASS and initial TEST-005 PASS preserved; TEST-006 static FAIL retained.
- Publication scope: 13 named Stage-1 code/meta/docs files only, task-only whitespace check PASS, protected tracked/untracked snapshots preserved and prior local doc portions excluded. No runtime/default mesher/navigation/save-schema changes, IMPL-0066 tuning acceptance, streaming/LOD/geology/optimization or further IMPL-0063 phase.
- Next action: commit/push this bounded Stage-1 checkpoint on dev and verify remote equality/divergence; stop before further phases. IMPL-0063 umbrella not DONE; IMPL-0066 tuning remains pending.
