# Old Scars — Current Milestone

Este archivo es el snapshot operativo breve. `Project_Roadmap.md` conserva IDs/dependencias de milestones grandes; `Next_Sprints.md` es la cola inmediata; `Issue_Registry.md` almacena defectos; `Implementation_Backlog.md` guarda mecánicas/mejoras aprobadas para después; `Development_Context_Index.md` indica qué leer al cambiar de chat/sesión.

## Estado actual

### M41 — NPC Combat / AI Foundation

Estado operativo:

`IN PROGRESS — CLOSE LOCAL F6 CANDIDATE, THEN KO DWELL / KO MEMORY / PRUEBA 3.3 / F8`

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

## Próximo paso exacto — P1

### Cerrar el candidato local de F6 / Observability minimum slice

Existe trabajo local no publicado en:

- `Assets/_OldScars/Scripts/Core/Actors/SandboxNpcObservabilityPanel.cs`;
- `Assets/_OldScars/Editor/M41F6ObservabilityDiagnostics.cs`;
- `Assets/_OldScars/Editor/M41F6ObservabilityDiagnostics.cs.meta`.

Ese candidato ya obtuvo PASS automático, pero todavía NO está aceptado manualmente ni publicado.

El cierre debe verificar:

- Gaze/FOV CURRENT desde eye/origin actual;
- `CURRENT` separado explícitamente de `LAST`;
- world visuals simultáneos para varios NPC;
- selección sólo para inspector profundo;
- Dead/Inactive sin presentar evidencia histórica como percepción actual;
- coherencia temporal de la evidencia `LAST` completa, incluido cualquier blocker histórico;
- ninguna segunda autoridad de Perception/raycasts de gameplay.

La aceptación visual/manual en Game View es obligatoria antes del commit.

## Orden operativo aprobado para cerrar M41

1. **P1 — cerrar F6 local** y publicarlo sólo después de aceptación visual.
2. **P2 — minimum real-time KO dwell**: estabilizar primero cuándo un actor Unconscious puede recuperar actividad; physiology sigue siendo autoridad después del mínimo.
3. **P3 — KO / combat-memory continuity**: recordar identidad/contexto mínimo sin conservar al KO como threat activo ni otorgar posición oculta.
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
- `ISSUE-0010` / `ISSUE-0011` / `ISSUE-0019` — cierre F6/observabilidad.
- `ISSUE-0012` — falta Player Debug Invincible.
- `ISSUE-0020` — KO borra contexto de enemigo.
- `ISSUE-0021` — no existe minimum real-time KO dwell.
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
