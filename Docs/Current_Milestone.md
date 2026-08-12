# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M38.0 — Actor Runtime & Lifecycle V1

Estado actual:

`DONE — ACTOR RUNTIME & LIFECYCLE VALIDATED`

Validation — `AUTOMATED + MANUAL FRESH-SESSION PASSED`

Pass 1 implementa identidad durable separada para player/NPCs, lifecycle `Alive/Dead`, persistencia de pose/health/storages de actores authored y runtime, spawn/restauración mínima y rollback posterior a reconciliación. M37.1 continúa `DONE — CURRENT SLICE PERSISTENCE VALIDATED`; `Persistence Ready` continúa `APPROVED`.

## Closeout Confirmado

- Runtime/Editor compilation, Content ID Foundation, M36.1, M37.0 y ambos diagnostics M37.1: `PASS`.
- `M38.0 Actor Runtime & Lifecycle Diagnostics: PASS`, incluido rollback post-reconciliation; `SampleScene` unchanged.
- Mauro confirmó authored actor Alive → Save → fresh Play session → Load → Alive restaurado correctamente.
- Mauro confirmó actor Dead → corpse lootable → Save Dead; una fresh Play session mostró bootstrap Alive antes de Load y Load lo reemplazó correctamente por el estado Dead persistido.
- El corpse conservó Inventory y Equipment; no se observó actor vivo + corpse duplicado ni errores de lifecycle, ownership o persistence.
- Los warnings visibles son los legacy Content ID ya conocidos y aceptados.

## Persistence Ready — Alcance Aprobado

El gate cubre solamente el Current Slice: player pose, health/needs representados, `ItemInstance` identity, `DefinitionId`, `Condition`, stacks/quantities, grid placements, Inventory, Equipment, ownership, item-owned storage, containers, corpse surfaces actuales, doors, authored world items, runtime dropped world items y estado runtime mutable incluido por M37.1.

M38.0 extiende el payload del Current Slice con actores NPC sin reabrir el gate: el player conserva su autoridad M37.1 y no duplica pose/storages; `ActorState` referencia las tablas únicas de items/storages. AI, combat, world streaming, world-scale spawn y M38.1 permanecen fuera.

## Deuda Aceptada

Migrar las referencias Global Content ID authored restantes en escenas/prefabs a `core:*` canónico y retirar luego la compatibilidad temporal Core legacy cuando ningún path authored o schema-v1 soportado la requiera.

La foundation actual de Content IDs cubre `namespace:local_id`, namespace `core`, identidad canónica de `GameDatabase`, migración Core, compatibilidad temporal, normalización schema-v1 y diagnostics. No existen todavía mod manifests, provenance completo, dependency resolution ni patch/load-order system.

Los authored roots aceptan un `authoredActorInstanceId` serializado explícito, pero `SampleScene` permanece intacta en este pass. Mientras no exista ese override, el ID estable se deriva una vez por sesión mediante SHA-256 sobre el locator authored congelado y un salt/version fijo; renombrar ese locator exige materializar antes el override.

## Próximo Trabajo

- M38.1 queda `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`; no se inicia en este commit.
- AI, combat, needs/world clock, world-scale spawning, streaming y el pequeño playable exploration prototype permanecen fuera de M38.0.
