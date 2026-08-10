# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M38.0 — Actor Runtime & Lifecycle V1

Estado actual:

`PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`

No está iniciado. M37.1 queda `DONE — CURRENT SLICE PERSISTENCE VALIDATED`; `Persistence Ready` está `APPROVED` para el Current Slice. ID TBD — Global Content ID Namespace Foundation queda `VALIDATED — FOUNDATION COMPLETE` sin adelantar el sistema de modding de producción.

## Closeout Confirmado

- Runtime/Editor compilation, Content ID Namespace Foundation Diagnostics, M36.1, M37.0 y ambos diagnostics M37.1: `PASS`.
- La validación manual fresh-session guardó 23 items, 11 storages, 3 world items, 8 containers, 0 corpses y 3 doors; el load fresh-session terminó `Success`, con `MutationStarted: True` y rollback no requerido.
- Mauro confirmó visualmente Inventory, Equipment, item-owned storage, containers, world state y ausencia de duplicados, pérdidas, fallos de ownership, rehydration o persistence.
- Inventory Interaction UX Correction queda `VALIDATED — AUTOMATED + MANUAL RECHECK PASSED`.

## Persistence Ready — Alcance Aprobado

El gate cubre solamente el Current Slice: player pose, health/needs representados, `ItemInstance` identity, `DefinitionId`, `Condition`, stacks/quantities, grid placements, Inventory, Equipment, ownership, item-owned storage, containers, corpse surfaces actuales, doors, authored world items, runtime dropped world items y estado runtime mutable incluido por M37.1.

Permanece fuera: lifecycle general de actores vivos, posición durable general de NPCs, transición alive/dead entre sesiones frescas, spawn/despawn runtime de NPCs y AI. Esos contratos pertenecen a M38.0.

## Deuda Aceptada

Migrar las referencias Global Content ID authored restantes en escenas/prefabs a `core:*` canónico y retirar luego la compatibilidad temporal Core legacy cuando ningún path authored o schema-v1 soportado la requiera.

La foundation actual de Content IDs cubre `namespace:local_id`, namespace `core`, identidad canónica de `GameDatabase`, migración Core, compatibilidad temporal, normalización schema-v1 y diagnostics. No existen todavía mod manifests, provenance completo, dependency resolution ni patch/load-order system.

## Próximo Trabajo

- solicitar autorización explícita antes de iniciar M38.0;
- mantener M38.1 como siguiente dependencia jugable;
- durante/después de M38.x, reutilizar la infraestructura en un pequeño playable exploration prototype para evaluar gameplay y presentación, sin declararlo aún vertical slice final ni crearle un ID nuevo.
