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

[Open_World_Architecture.md](Open_World_Architecture.md) define el futuro mundo lógico persistente, sectores grandes interconectados, macro planning, blueprints sectoriales, materialización Unity y mutación persistente. Las foundations mínimas de content source identity/provenance y world identity/topology/determinism están implementadas y validadas; macro worldgen, geography/features, session/save, sector gameplay/materialization, transición y generation compatibility continúan no implementados.

### ID TBD — Minimum Content Source Identity & Provenance Foundation

Estado final:

`VALIDATED — FOUNDATION COMPLETE`

Cada content source requiere manifest mínimo, identidad/namespace/version estables y ownership único antes de registrar Definitions. Core usa el mismo pipeline. `LoadedContentSet` publica orden y SHA-256 de provenance sólo después de loader + `DataValidator` exitosos; no decide compatibilidad.

### ID TBD — World Identity, Topology & Determinism Foundation

Estado final:

`VALIDATED — FOUNDATION COMPLETE`

`WorldId`, `WorldSeed`, `GeneratorVersion`, `SectorId`, derivación SHA-256 por scope/pass y `WorldTopology` conectado/multiconexión existen como datos lógicos puros. `WorldId` no entra en generación; provenance tampoco se convierte en compatibilidad ni input automático. No se implementaron session, save, menu, geography, history, materialización ni GameObjects.

## Evidencia De Cierre

- Runtime/Editor compile y `M41.1 Human Encounter AI Diagnostics`: `PASS`.
- Mauro confirmó `Idle → Alerted → Avoiding`, `Fleeing` y `Fighting`, con navegación física y `NAVIGATION_MOVING` observable.
- Fight reutilizó Lee-Enfield, ammo/reload/disparo y armor del contrato M40; el estado final `0 loaded / 0 reserve` fue consumo manual deliberado, no un defecto.
- LOS confirmó `Perceived → Occluded → LostContact → Idle`; al retirar la barrera y reasignar explícitamente el threat volvió a `Alerted`, sin omnisciencia.
- El menú Editor muestra sólo la fixture M41.1 explícita; diagnostics históricos siguen invocables por automatización sin exposición manual obsoleta.
- Runtime/Editor compile, real Core + `DataValidator`, `Minimum Content Source Identity & Provenance Foundation` y `Global Content ID Namespace Foundation`: `PASS` en Editor batchmode aislado.
- Runtime/Editor compile y `World Identity / Topology / Determinism Foundation`: `PASS` en dos procesos aislados con domain/topology golden hashes estables; M36.1 Foundation Identity y M37.0 Persistence Core permanecen `PASS`.

## Contratos Cerrados

- `HumanEncounterAIController` sólo posee decision state/target/timers/response y requiere bloques data-driven `navigation`, `visual_perception` y `encounter_ai`.
- Perception conserva LOS; Navigation conserva path; `WeaponCombatService` conserva ammo, reload, impacto y consecuencias. Player y NPC comparten `PhysicalShotPathResolver`.
- `LostContact` usa sólo last-known de percepción positiva, cancela acción y exige reacquisition explícita tras timeout; `Dead` deja IA y Navigation inactivas.
- Encounter state, target, timers, órdenes y resultados de percepción siguen efímeros; M41.1 no cambia schema/envelope.

## Próximo Trabajo

No hay milestone de implementación activo. El siguiente coding unit candidato es `ID TBD — World Session + Persistence V1 / New Game Save-Load Path`; permanece `PLANNED — NOT AUTHORIZED`, debe reutilizar M37 sin cambiar `current_slice_v1` y requiere autorización/alcance específico.

M42.0 conserva su ID y alcance planificado, pero ya no es el siguiente trabajo automático. La secuencia M42.0–M47.1 requiere reconciliación posterior sin renumeración ni reutilización silenciosa.
