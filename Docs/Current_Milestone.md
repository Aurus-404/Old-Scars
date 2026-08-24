# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M41.1 — Human Encounter AI V1

Estado final:

`DONE — HUMAN ENCOUNTER AI V1 VALIDATED`

Validation: `AUTOMATED + MANUAL UNITY PASSED`.

M41.0 permanece `DONE — NAVIGATION / PERCEPTION FOUNDATION VALIDATED`; M40.1 conserva `Combat Ready — APPROVED`. M41.1 conecta Navigation, Perception y Combat sin una autoridad paralela. `AI Ready — APPROVED`.

### Open World Rebaseline

Estado de dirección:

`APPROVED DESIGN DIRECTION — NOT IMPLEMENTED`

[Open_World_Architecture.md](Open_World_Architecture.md) define el futuro mundo lógico persistente, sectores grandes interconectados, macro planning, blueprints sectoriales, materialización Unity y mutación persistente. Las foundations mínimas de content source identity/provenance, world identity/topology/determinism y Macro World Plan V1, más la shell acotada de World Session/New Game/Save/Load, están implementadas y validadas; elevation/landforms, geography/features, gameplay world persistence, sector gameplay/materialization, transición y generation compatibility continúan no implementados.

### ID TBD — Minimum Content Source Identity & Provenance Foundation

Estado final:

`VALIDATED — FOUNDATION COMPLETE`

Cada content source requiere manifest mínimo, identidad/namespace/version estables y ownership único antes de registrar Definitions. Core usa el mismo pipeline. `LoadedContentSet` publica orden y SHA-256 de provenance sólo después de loader + `DataValidator` exitosos; no decide compatibilidad.

### ID TBD — World Identity, Topology & Determinism Foundation

Estado final:

`VALIDATED — FOUNDATION COMPLETE`

`WorldId`, `WorldSeed`, `GeneratorVersion`, `SectorId`, derivación SHA-256 por scope/pass y `WorldTopology` conectado/multiconexión existen como datos lógicos puros. `WorldId` no entra en generación; provenance tampoco se convierte en compatibilidad ni input automático.

### ID TBD — World Session + Persistence V1 / New Game Save-Load Application Shell

Estado final:

`VALIDATED — APPLICATION SHELL COMPLETE`

`WorldSessionService` posee una única session activa y lifecycle Create/Load/Save/Close. `world_session_v1` es hermano de `current_slice_v1` sobre el envelope/store M37; usa `WorldId` como slot, persiste identity/topology/active sector/provenance evidence y preflighta antes de publicar. Main Menu es el startup de producto, `WorldRuntime` es un placeholder separado y `SampleScene` permanece laboratorio. Macro World Plan V1 extendió después esa misma session/persistencia sin implementar geography, history, terrain, gameplay world state, streaming ni sector transitions.

### ID TBD — Macro World Plan V1

Estado final:

`VALIDATED — FOUNDATION COMPLETE`

New Game usa `WorldGenerationSettings` con `Small`, `Medium`, `Large` y `Huge` —default `Large`— para crear un `MacroWorldPlan` finito completo antes de entrar a runtime. Settings resueltos, bounds, placements, topology y hash canónico se persisten en `world_session_v1` schema `2`; schema `1` conserva un path legacy explícito sin plan inventado. WorldId, provenance y futuro worker budget no alteran generación.

## Evidencia De Cierre

- Runtime/Editor compile y `M41.1 Human Encounter AI Diagnostics`: `PASS`.
- Mauro confirmó `Idle → Alerted → Avoiding`, `Fleeing` y `Fighting`, con navegación física y `NAVIGATION_MOVING` observable.
- Fight reutilizó Lee-Enfield, ammo/reload/disparo y armor del contrato M40; el estado final `0 loaded / 0 reserve` fue consumo manual deliberado, no un defecto.
- LOS confirmó `Perceived → Occluded → LostContact → Idle`; al retirar la barrera y reasignar explícitamente el threat volvió a `Alerted`, sin omnisciencia.
- El menú Editor muestra sólo la fixture M41.1 explícita; diagnostics históricos siguen invocables por automatización sin exposición manual obsoleta.
- Runtime/Editor compile, real Core + `DataValidator`, `Minimum Content Source Identity & Provenance Foundation` y `Global Content ID Namespace Foundation`: `PASS` en Editor batchmode aislado.
- Runtime/Editor compile y `World Identity / Topology / Determinism Foundation`: `PASS` en dos procesos aislados con domain/topology golden hashes estables; M36.1 Foundation Identity y M37.0 Persistence Core permanecen `PASS`.
- Runtime/Editor compile, `World Session / Persistence V1 Application Shell Diagnostics`, flujo Play Mode real Main Menu→Runtime→Menu→Main Menu→Load y scene wiring: `PASS`.
- Fresh process A/B creó y reabrió desde disco el mismo `WorldId`, seed, topology hash y active sector; `M37.0`, M37.1 semantic preflight, World Identity/Topology/Determinism y Content Source Identity/Provenance permanecen `PASS`.
- Runtime/Editor compile y `Macro World Plan V1 Diagnostics`: `PASS`; cubre same-input/WorldId independence, cuatro escalas, bounds/spacing/uniqueness/connectivity, insertion order, golden plan, 12 seeds por preset, schema 2 round-trip, schema 1 legacy y timing de los cuatro presets.
- Fresh process A/B reconstruyó exactamente `WorldId`, seed, size `Huge`, MacroWorldPlan hash, topology hash y active sector desde disco; el flujo Play Mode Main Menu con size seleccionado, Save/Return/Load y las regresiones M37.0/M37.1, World Foundation y Content Provenance permanecen `PASS`.

## Contratos Cerrados

- `HumanEncounterAIController` sólo posee decision state/target/timers/response y requiere bloques data-driven `navigation`, `visual_perception` y `encounter_ai`.
- Perception conserva LOS; Navigation conserva path; `WeaponCombatService` conserva ammo, reload, impacto y consecuencias. Player y NPC comparten `PhysicalShotPathResolver`.
- `LostContact` usa sólo last-known de percepción positiva, cancela acción y exige reacquisition explícita tras timeout; `Dead` deja IA y Navigation inactivas.
- Encounter state, target, timers, órdenes y resultados de percepción siguen efímeros; M41.1 no cambia schema/envelope.

## Próximo Trabajo

No hay milestone de implementación activo. El siguiente coding unit candidato es `ID TBD — Macro Elevation / Landforms V1`, `PLANNED — NOT AUTHORIZED`. Debe consumir el Macro World Plan global sin implementar terrain/materialization, hydrology, roads, history o sector transitions fuera de su alcance autorizado.

M42.0 conserva su ID y alcance planificado, pero ya no es el siguiente trabajo automático. La secuencia M42.0–M47.1 requiere reconciliación posterior sin renumeración ni reutilización silenciosa.
