# Old Scars — Current Milestone

Este archivo es el snapshot operativo breve. `Project_Roadmap.md` conserva IDs/dependencias de milestones grandes; `Next_Sprints.md` es la cola inmediata; `Issue_Registry.md` almacena defectos; `Implementation_Backlog.md` guarda mecánicas/mejoras aprobadas para después; `Development_Context_Index.md` indica qué leer al cambiar de chat/sesión.

## Estado actual

### M41 — NPC Combat / AI Foundation

Estado operativo:

`IN PROGRESS — P3 DONE/PUBLISHED; NEXT P4 PRUEBA 3.3`

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

F6 Observability, Gaze/Perception, LostContact/Search y compile Runtime/Editor PASS previos; aceptación visual manual final confirmada por Mauro. IMPL-0010 minimum slice completado; F10 completo pendiente. Posible desajuste eye origin/representación humana separado en ISSUE-0023, sin resolver.

## P2 y P3 cerrados — próximo paso exacto P4

P2 — Minimum real-time Unconscious dwell: **DONE / PUBLISHED** el 2026-09-07 en `9ca0335cdc8b85bd49d20ddbe97ad814f44c8578`. Condition compartida Player/NPC; Core `5 s` es tuning inicial de prueba, no balance final. Current Slice v1 conserva restante y continuidad de recuperación; offline no consume el mínimo y un save legacy que deriva Unconscious inicia el mínimo completo. Diagnostic P2, sesión Play nueva y siete regresiones proporcionales PASS. No sustituye aceptación manual integrada de M41.

P3 — KO / combat-memory continuity: **DONE / PUBLISHED** el 2026-09-07 en `394d01886b8c6697ca2d492c4450282f561ba688`. Encounter conserva una identidad reciente separada de Threat, sin posición ni Search propia. La ventana real configurable de `60 s` Core es tuning provisional; se pausa sólo durante incapacidad propia y se renueva con observaciones legítimas. Recovery requiere Recognition/Perception y retoma el contexto sin otro ciclo Alerted. Death/invalidation/expiry/reemplazo limpian el recuerdo; memoria sola no bloquea treatment ni AmbientTopOff. Diagnostic P3 y ocho regresiones proporcionales PASS; ISSUE-0020 RESOLVED, IMPL-0014 DONE.

Próximo: **P4 — Prueba 3.3**. No iniciado; aceptación manual integrada de M41 pendiente.

## Orden operativo aprobado para cerrar M41

1. **P1 — F6 DONE / ACCEPTED / PUBLISHED**, Correction Pass B cerrado.
2. **P2 — minimum real-time KO dwell: DONE / PUBLISHED**; physiology sigue siendo autoridad después del mínimo.
3. **P3 — KO / combat-memory continuity: DONE / PUBLISHED**; identidad/contexto mínimo separado del threat activo y sin posición oculta.
4. **P4 — Prueba 3.3**: 1 Blue vs 1 Red, Player Invisible ON, observabilidad simultánea, KO/recovery/contexto interpretable.
5. **P5 — F8A Aim Bias Evidence**: medir antes de cambiar aim/accuracy.
6. **P6 — F8B/C y F8D sólo si la evidencia lo justifica**: Primary Aim Point genérico y comparación controlada; ningún retuning por intuición.
7. **P7 — Player Debug Invincible**: pipeline físico/médico real continúa, pero QA puede bloquear el desenlace terminal Dead. OFF = gameplay normal.
8. **P8 — completar Observability V2/F10** sobre el mismo tooling, incluyendo targeting/shot evidence cuando existan esos contratos.
9. **P9 — migración legacy + QA integrada + aceptación manual + cleanup** y cierre formal de NPC Foundation V1.

## Punto de DONE de NPC Foundation V1

M41 no se considera DONE sólo por compilación o diagnostics aislados. Requiere:

- F6 aceptado y publicado;
- KO dwell y KO memory coherentes;
- Prueba 3.3 aceptada;
- `ISSUE-0008` con conclusión sustentada y before/after si hubo corrección;
- Invincible usable para QA Player sin romper OFF;
- F10 suficiente para explicar percepción/aim/hits/KO;
- consumers legacy migrados cuando corresponda;
- regresiones de Search, Bandaging, Reload, Perception, ownership y combat;
- prueba NPC↔NPC y NPC↔Player en sesión fresca;
- aceptación manual de Mauro.

## Después de M41

La secuencia sistémica aprobada es:

1. `IMPL-0016` Equipment visuals humanoides, cuando convenga para lectura visual; puede intercambiar posición con la reparación de masa de munición porque no existe dependencia técnica entre ambos.
2. `ISSUE-0022` loaded ammo mass: corregir conservación de masa antes de que Carry Weight gobierne locomoción.
3. `IMPL-0020` Carry Weight / Encumbrance compartido Player/NPC.
4. `IMPL-0021` Localized Limb Impairment, primero sobre consumidores concretos de locomoción y después brazos/handling cuando estén definidos.

No introducir Encumbrance entre F8A y su comparación, porque velocidad NPC ya participa en las condiciones de accuracy y contaminaría la medición.

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

Antes de este bloque debe resolverse `ISSUE-0022` para que recargar/disparar no cambie masa de forma artificial.

## Issues activos relevantes

- `ISSUE-0008` — posible sesgo de impactos piernas/pies; medir en F8A antes de tocar gameplay.
- `ISSUE-0023` — posible desajuste eye origin/representación humana; SUSPECTED.
- `ISSUE-0012` — falta Player Debug Invincible.
- `ISSUE-0022` — munición cargada deja de contribuir correctamente a la masa agregada.

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
