# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M41.1 — Human Encounter AI V1

Estado final:

`DONE — HUMAN ENCOUNTER AI V1 VALIDATED`

Validation: `AUTOMATED + MANUAL UNITY PASSED`.

M41.0 permanece `DONE — NAVIGATION / PERCEPTION FOUNDATION VALIDATED`; M40.1 conserva `Combat Ready — APPROVED`. M41.1 conecta Navigation, Perception y Combat sin una autoridad paralela. `AI Ready — APPROVED`.

## Evidencia De Cierre

- Runtime/Editor compile y `M41.1 Human Encounter AI Diagnostics`: `PASS`.
- Mauro confirmó `Idle → Alerted → Avoiding`, `Fleeing` y `Fighting`, con navegación física y `NAVIGATION_MOVING` observable.
- Fight reutilizó Lee-Enfield, ammo/reload/disparo y armor del contrato M40; el estado final `0 loaded / 0 reserve` fue consumo manual deliberado, no un defecto.
- LOS confirmó `Perceived → Occluded → LostContact → Idle`; al retirar la barrera y reasignar explícitamente el threat volvió a `Alerted`, sin omnisciencia.
- El menú Editor muestra sólo la fixture M41.1 explícita; diagnostics históricos siguen invocables por automatización sin exposición manual obsoleta.

## Contratos Cerrados

- `HumanEncounterAIController` sólo posee decision state/target/timers/response y requiere bloques data-driven `navigation`, `visual_perception` y `encounter_ai`.
- Perception conserva LOS; Navigation conserva path; `WeaponCombatService` conserva ammo, reload, impacto y consecuencias. Player y NPC comparten `PhysicalShotPathResolver`.
- `LostContact` usa sólo last-known de percepción positiva, cancela acción y exige reacquisition explícita tras timeout; `Dead` deja IA y Navigation inactivas.
- Encounter state, target, timers, órdenes y resultados de percepción siguen efímeros; M41.1 no cambia schema/envelope.

## Próximo Trabajo

No iniciar un milestone nuevo: el siguiente trabajo autorizado es hardening de workflow, fuera de M41.1.
