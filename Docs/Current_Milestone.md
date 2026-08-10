# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M38.0 — Actor Runtime & Lifecycle V1

Estado actual:

`IMPLEMENTED — AUTOMATED ACTOR LIFECYCLE VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`

Pass 1 implementa identidad durable separada para player/NPCs, lifecycle `Alive/Dead`, persistencia de pose/health/storages de actores authored y runtime, spawn/restauración mínima y rollback posterior a reconciliación. M37.1 continúa `DONE — CURRENT SLICE PERSISTENCE VALIDATED`; `Persistence Ready` continúa `APPROVED`. M38.0 no está `DONE` hasta completar la validación manual fresh-session de Mauro.

## Closeout Confirmado

- Runtime/Editor compilation, Content ID Namespace Foundation Diagnostics, M36.1, M37.0 y ambos diagnostics M37.1: `PASS`.
- `M38.0 Actor Runtime & Lifecycle Diagnostics: PASS` en dos entradas reales a Play Mode: bootstrap authored fresco, load Alive/Dead, continuidad de corpse, spawn/restore runtime, unicidad, selectividad y rollback post-reconciliation.
- `SampleScene` no se guardó y conservó SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`; no aparecieron warnings nuevos atribuibles a M38.0.
- La validación manual fresh-session guardó 23 items, 11 storages, 3 world items, 8 containers, 0 corpses y 3 doors; el load fresh-session terminó `Success`, con `MutationStarted: True` y rollback no requerido.
- Mauro confirmó visualmente Inventory, Equipment, item-owned storage, containers, world state y ausencia de duplicados, pérdidas, fallos de ownership, rehydration o persistence.
- Inventory Interaction UX Correction queda `VALIDATED — AUTOMATED + MANUAL RECHECK PASSED`.

## Persistence Ready — Alcance Aprobado

El gate cubre solamente el Current Slice: player pose, health/needs representados, `ItemInstance` identity, `DefinitionId`, `Condition`, stacks/quantities, grid placements, Inventory, Equipment, ownership, item-owned storage, containers, corpse surfaces actuales, doors, authored world items, runtime dropped world items y estado runtime mutable incluido por M37.1.

M38.0 extiende el payload del Current Slice con actores NPC sin reabrir el gate: el player conserva su autoridad M37.1 y no duplica pose/storages; `ActorState` referencia las tablas únicas de items/storages. AI, combat, world streaming, world-scale spawn y M38.1 permanecen fuera.

## Deuda Aceptada

Migrar las referencias Global Content ID authored restantes en escenas/prefabs a `core:*` canónico y retirar luego la compatibilidad temporal Core legacy cuando ningún path authored o schema-v1 soportado la requiera.

La foundation actual de Content IDs cubre `namespace:local_id`, namespace `core`, identidad canónica de `GameDatabase`, migración Core, compatibilidad temporal, normalización schema-v1 y diagnostics. No existen todavía mod manifests, provenance completo, dependency resolution ni patch/load-order system.

Los authored roots aceptan un `authoredActorInstanceId` serializado explícito, pero `SampleScene` permanece intacta en este pass. Mientras no exista ese override, el ID estable se deriva una vez por sesión mediante SHA-256 sobre el locator authored congelado y un salt/version fijo; renombrar ese locator exige materializar antes el override.

## Próximo Trabajo

- ejecutar `M38.0 — Manual Unity Validation & Closeout` con Alive, Dead/corpse y runtime actor en sesiones frescas;
- mantener M38.1 como `PLANNED — BLOCKED BY M38.0 MANUAL CLOSEOUT`;
- el pequeño playable exploration prototype no está iniciado; sólo podrá evaluarse después del closeout correspondiente, sin declararlo vertical slice final ni crearle un ID nuevo.
