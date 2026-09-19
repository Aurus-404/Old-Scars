# Old Scars — Current Milestone

Este archivo es el snapshot operativo breve. `Project_Roadmap.md` conserva IDs/dependencias de milestones grandes; `Next_Sprints.md` es la cola inmediata; `Issue_Registry.md` almacena defectos; `Implementation_Backlog.md` guarda mecánicas/mejoras aprobadas para después; `Development_Context_Index.md` indica qué leer al cambiar de chat/sesión.

## Estado actual

### M41 — NPC Combat / AI Foundation

Estado operativo:

DONE / ACCEPTED / PUBLISHED — M41.4 and P9 closed 2026-09-14; documentation closeout commit recorded in Git history.

Plan activo: `NPC_AI_Sanitation_Plan.md`.

Evidencia manual activa: `Prueba_3_Findings.md`.

Research/decision record de targeting: `NPC_Combat_Targeting_Research.md`.

## Foundation ya cerrada

Están implementados y validados como base:

- F2 — `ActorBehaviorController`: ownership `Ambient / Encounter / Search / Inactive`, roaming físico White/Blue/Red y reanudación;
- F3 — `ActorGazeController`: gaze/attention bounded;
- F4 — tracking visual por movimiento observado;
- F5 — `ActorVisualPerceptionService` centrado en Current Gaze;
- F6 — LostContact/Search V1 con SearchAnchor congelado, navegación, reacquire/release;
- F7 — representación humana, `ActorLocomotionCollider` y seis `ActorCombatHitRegion` explícitos;
- Correction Pass A — Player Debug `Invisible to AI`.

También están cerradas capacidades recientes que deben conservarse en regresiones:

- Timed Bandaging V1 + NPC self-treatment;
- Blood Trails V1/V1.1;
- NPC Opportunistic Reload (`4b90b9f4c8f5ae3c896d8b1fc21d688095172b0a`).

No reabrir estas autoridades por inercia. Una regresión real puede justificar cambios; el tamaño de una clase o una preferencia estética no.

## P1/F6 cerrado

P1/F6 / Correction Pass B: **DONE / ACCEPTED / PUBLISHED** el 2026-09-06 en `5aac763c14c399bfe09a3e925c50698658ad2716`. CURRENT Gaze/FOV multi-NPC desde origen productivo actual; selección sólo para inspector detallado. LAST usa ObserverOrigin → ObservedPosition históricos, visual secundario y toggle independiente, sin reconstruir blocker hit con collider actual. Sin evidencia: LAST: No evidence. Dead/Inactive sin CURRENT engañoso. Sin nueva Perception/raycasts productivos.

F6 Observability, Gaze/Perception, LostContact/Search y compile Runtime/Editor PASS previos; aceptación visual manual final confirmada por Mauro. P8/F10 completó el tooling diferido sobre F6 y quedó DONE / ACCEPTED / PUBLISHED el 2026-09-13; ISSUE-0023 continúa SUSPECTED.

## P2, P3, P4, targeting F8, P7, P8 y P9 cerrados

P2 — Minimum real-time Unconscious dwell: **DONE / PUBLISHED** el 2026-09-07 en `9ca0335cdc8b85bd49d20ddbe97ad814f44c8578`. Condition compartida Player/NPC; Core `5 s` es tuning inicial de prueba, no balance final. Current Slice v1 conserva restante y continuidad de recuperación; offline no consume el mínimo y un save legacy que deriva Unconscious inicia el mínimo completo. Diagnostic P2, sesión Play nueva y siete regresiones proporcionales PASS. No sustituye aceptación manual integrada de M41.

P3 — KO / combat-memory continuity: **DONE / PUBLISHED** el 2026-09-07 en `394d01886b8c6697ca2d492c4450282f561ba688`. Encounter conserva una identidad reciente separada de Threat, sin posición ni Search propia. La ventana real configurable de `60 s` Core es tuning provisional; se pausa sólo durante incapacidad propia y se renueva con observaciones legítimas. Recovery requiere Recognition/Perception y retoma el contexto sin otro ciclo Alerted. Death/invalidation/expiry/reemplazo limpian el recuerdo; memoria sola no bloquea treatment ni AmbientTopOff. Diagnostic P3 y ocho regresiones proporcionales PASS; ISSUE-0020 RESOLVED, IMPL-0014 DONE.

P4 — Prueba 3.3: **DONE / ACCEPTED**. Mauro confirmó que esta validación manual ya se realizó; este cierre reconcilia el estado sin volver a ejecutarla ni inventar evidencia adicional.

F8A Aim Bias Evidence, F8B Generic Primary Aim Point y F8C Controlled Paired Before/After están `DONE / PASS / PUBLISHED`. `ISSUE-0008` queda `RESOLVED`: el detalle causal y la evidencia pareada están en `Issue_Registry.md`. No se retuneó accuracy. Esos diagnostics no sustituyeron P4; su aceptación manual fue confirmada separadamente por Mauro.

P8/F10 Observability V2 quedó DONE / ACCEPTED / PUBLISHED el 2026-09-13 en commit funcional f4d07434d2b0ea0387584320c81b48dd248bd280. El panel F6 muestra overlay multi-NPC, targeting/aim productivo, Focus/Spread, último disparo real con collider/Combat.Region/clasificación y semantic trace bounded; no introduce autoridad de gameplay.

Validación automática previa: Runtime/Editor compile, M41 F10 Observability Diagnostics y M41 F6 Observability Diagnostics PASS. Mauro confirmó aceptación visual manual: Blue/Red visibles simultáneamente, selección clara, CURRENT Gaze/FOV separado de LAST, condiciones Conscious/Incapacitated/Dead, Dead sin targeting CURRENT, shot WORLD/OBSTACLE con collider y Combat.Region NONE, semantic trace legible e inspector navegable. El clutter ocasional de líneas es aceptable para QA.

## Cierre formal M41 / P9 — 2026-09-14

M41.4 and P9 are DONE / ACCEPTED / PUBLISHED. Documentation closeout commit: `docs: close M41 NPC foundation` (2026-09-14). P9 completed the legacy authored visual-rig migration (`TEST-20260914-002`), automated QA, NPC-only manual integration (`TEST-20260914-001`), and NPC↔Player melee/firearm manual integration (`TEST-20260914-003` and `TEST-20260914-004`). The firearm run included the setup preconditioning documented in Test_Log; its subsequent productive acquisition and combat flow passed. Mauro reported no new blocking M41 AI/combat/health/condition/lifecycle/navigation exception. No Unity rerun was performed for this documentation closeout.

P9 legacy audit found no current authored visual-rig consumer requiring a legacy targeting migration; generic fallbacks and schema-v1 compatibility remain preserved. The authored visual-rig reference migration passed. ISSUE-0023 remains SUSPECTED; ISSUE-0026/0027 remain deferred. The approved Player Debug Heal All / Reset Medical State tooling is tracked separately as IMPL-0041 PLANNED / DEFERRED.

**M41: DONE / ACCEPTED / PUBLISHED. M41.4: DONE. P9: DONE / ACCEPTED / PUBLISHED.**

ISSUE-0022 — Loaded Ammo Mass Conservation: **DONE / RESOLVED / PUBLISHED** el 2026-09-15; commit funcional `481183ddfac82f9ff9e547da6c72a1af13ecc088`, gates `TEST-20260915-001` a `TEST-20260915-004`. M41 permanece cerrado.

**NEXT EXACT STEP: IMPL-0020 — Carry Weight / Encumbrance.** Sigue PLANNED; no iniciado por este cierre.

## Orden operativo aprobado para cerrar M41 (histórico)

1. **P1 — F6 DONE / ACCEPTED / PUBLISHED**, Correction Pass B cerrado.
2. **P2 — minimum real-time KO dwell: DONE / PUBLISHED**; physiology sigue siendo autoridad después del mínimo.
3. **P3 — KO / combat-memory continuity: DONE / PUBLISHED**; identidad/contexto mínimo separado del threat activo y sin posición oculta.
4. **P4 — Prueba 3.3: DONE / ACCEPTED** por confirmación manual previa de Mauro; no se repitió en P7.
5. **P5 — F8A Aim Bias Evidence: DONE / PASS / PUBLISHED**; `ISSUE-0008 RESOLVED`.
6. **P6 — F8B Generic Primary Aim Point + F8C paired control: DONE / PASS / PUBLISHED**; sin retuning. F8D no está iniciado ni autorizado por inercia.
7. **P7 — Player Debug Invincible: DONE / PASS / PUBLISHED** en `c96900589816239bfdf6553fba699bca6b79a54f`; marker efímero Player-only desde F3, terminal protection coherente en Health/Condition, KO permitido y OFF normal.
8. **P8 — Observability V2/F10: DONE / ACCEPTED / PUBLISHED**; commit funcional f4d07434d2b0ea0387584320c81b48dd248bd280.
9. **P9 — legacy migration + QA integrada + cleanup + cierre formal de NPC Foundation V1: DONE / ACCEPTED / PUBLISHED (2026-09-14)**.

## Punto de DONE de NPC Foundation V1

M41 no se considera DONE sólo por compilación o diagnostics aislados. Requiere:

- F6 aceptado y publicado;
- KO dwell y KO memory coherentes;
- Prueba 3.3 aceptada;
- `ISSUE-0008 RESOLVED` con causa y before/after pareado en `Issue_Registry.md`;
- Invincible usable para QA Player sin romper OFF;
- F10 suficiente para explicar percepción/aim/hits/KO;
- consumers legacy migrados cuando corresponda;
- regresiones de Search, Bandaging, Reload, Perception, ownership y combat;
- prueba NPC↔NPC y NPC↔Player en sesión fresca;
- aceptación manual de Mauro.

## Después de M41

La secuencia sistémica aprobada es:

`IMPL-0016` Equipment visuals humanoides fue adelantado por necesidad de validación visual y quedó `DONE / ACCEPTED` sin alterar Equipment ni combat.

1. `ISSUE-0022` loaded ammo mass: DONE / RESOLVED / PUBLISHED; conservación corregida antes de Encumbrance.
2. `IMPL-0020` Carry Weight / Encumbrance compartido Player/NPC.
3. `IMPL-0021` Localized Limb Impairment, primero sobre consumidores concretos de locomoción y después brazos/handling cuando estén definidos.

No introducir Encumbrance en comparaciones futuras de accuracy, porque velocidad NPC participa en sus condiciones. F8A/F8C ya cerraron ISSUE-0008 sin retuning.

## Regla operativa — un solo scope activo y cerrable

A partir de 2026-09-19, la vision amplia del proyecto no autoriza trabajo paralelo. Debe existir un unico scope de implementacion activo, con limites y `DONE` verificable. Una idea nueva puede planearse o registrarse en backlog, pero no implementarse hasta cerrar y publicar el scope activo.

Solo puede incorporarse trabajo no previsto cuando sea una dependencia critica demostrable para completar el scope vigente. Esa excepcion debe registrarse, limitarse al minimo necesario y devolver el trabajo al objetivo original.

Aplicacion al estado actual:

- M41, Invisible to AI, Player Debug Invincible e ISSUE-0022 Loaded Ammo Mass Conservation permanecen cerrados; no forman una lista abierta.
- Antes de abrir trabajo nuevo, deben reconciliarse/cerrarse las validaciones pendientes que realmente sigan vigentes en el checkout canonico.
- Despues, el unico scope implementable es `IMPL-0020 — Carry Weight / Encumbrance`.
- `IMPL-0021` y cualquier otra idea permanecen en cola hasta que IMPL-0020 complete implementacion, validacion, documentation closeout, review, commit, push, verificacion de sincronizacion y nuevo `NEXT EXACT STEP`.

## Carry Weight — contrato de producto ya fijado

Carry Capacity NO es límite de almacenamiento.

- `0..75%` de carga: locomoción normal;
- `>75%..100%`: penalización progresiva;
- `100%`: todavía móvil, lentamente;
- `>100%`: traslación cero;
- Inventory/transfer/drop/equipment/use/reload/treatment siguen dependiendo de sus propias autoridades;
- descargar suficiente peso recupera locomoción;
- Player y NPC deben compartir el contrato;
- `Overloaded` no equivale a `Incapacitated`.

La dependencia `ISSUE-0022` ya está resuelta: reload conserva masa y cada round disparado reduce exactamente su masa canónica.

## Issues activos relevantes

- `ISSUE-0023` — posible desajuste eye origin/representación humana; SUSPECTED.

## Reglas de alcance

Conservar salvo evidencia contraria:

- Behavior ownership;
- Navigation;
- Gaze;
- Perception;
- Recognition/Threat Acquisition;
- Search V1;
- `WeaponCombatService`;
- `PhysicalShotPathResolver`;
- explicit combat hit regions;
- Focus/error angular físico;
- Health/Medical/Condition como autoridades separadas.

No iniciar ahora:

- Behavior Trees/GOAP/Utility AI;
- memory framework general;
- cover/squad/hearing/schedules;
- weak-point scoring/head targeting;
- full ballistics/drop/wind;
- Strength/stats o backpack carry modifiers;
- Limb HP paralelo;
- blood tracking AI/footprints/puddles;
- weapon viability/fallback sin tarea propia.
