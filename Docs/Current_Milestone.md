# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M40.0 — Combat Resolution & Weapons V1

Estado actual:

`DONE — COMBAT RESOLUTION & WEAPONS V1 VALIDATED`

Validation — `AUTOMATED + MANUAL FRESH-SESSION PASSED`

Functional Pass 1 gradúa el prototipo M29 a un único adaptador de input de combate, resuelve melee/firearms mediante servicios data-driven y traduce impactos deterministas a heridas M39. El estado cargado vive en cada `ItemInstance`; reload consume ammo compatible desde ownership real; M38/M39 conservan autoridad sobre reserva vital, Dead/corpse, wounds, bleeding y pain. `Persistence Ready` continúa `APPROVED`.

## Evidencia De Validación

- Runtime/Editor compilation, Global Content ID, M36.1, M37.0, ambos M37.1, M38.0, M38.1, M39.0, Player Controls & Health Window e Inventory Interaction UX: `PASS`.
- `M40.0 Combat Resolution & Weapons Diagnostics: PASS` en dos Play sessions: seis regiones, melee/range, dry-fire, reload parcial/completo/cancelado, fire/miss/cycle, bleeding-to-Dead, drop/pickup, equipment, fresh-session round-trip, legacy V1, preflight, rollback y near-cover Correction Pass 1.
- Fault post-firearm-state: `ApplyFailed` esperado, `RollbackAttempted: True`, `RollbackSucceeded: True`; pre-state y post-rollback canónicamente equivalentes.
- `SampleScene` unchanged, SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`.
- Cero warnings nuevos atribuibles a M40.0; permanecen los seis warnings C# preexistentes documentados.
- Mauro confirmó manualmente Lee-Enfield equipable, F/LMB/R, unloaded, reload completo/parcial, capacity 10, consumo exacto, bolt cycle, heridas regionales `Puncture`, world blocking y continuidad Dead/corpse.
- Near-cover Correction Pass 1 pasó: una pared inmediata bloqueó el impacto y evitó la wound del actor detrás; con línea limpia el actor volvió a recibir impacto.
- Crowbar pasó equip, melee temporizado, heridas `Blunt`, regiones reales, out-of-range, geometría interpuesta y cancelación por WASD sin ruta médica paralela.
- Drop/pickup preservó `Loaded 8/10` y `InstanceId: item_c0f66d58249e4892aa4632028975816e`. Save, salida de Play, fresh Play y Load restauraron el rifle equipado en `Loaded 8/10` con `Phase: Complete`, `FailureCode: Success` y `Result: Success`.
- No hubo errores nuevos atribuibles a M40. Los warnings legacy `core:*` permanecen como deuda aceptada y no bloqueante.

## Contrato Funcional

- `CombatResolutionService` es la ruta única de impacto a `ActorMedicalStateComponent.TryApplyWound`; no aplica daño escalar paralelo.
- `WeaponCombatService` consulta Equipment, perfiles y ownership; firearm y melee se distinguen por datos, nunca por IDs de contenido productivos.
- Firearms conservan `ammoProfileId + loadedRounds` por `ItemInstance`; capacidad deriva del `FirearmProfile`. Reload es temporizable/cancelable, consume exactamente el faltante y revalida la misma arma equipada.
- El adaptador conserva F/LMB/R, raycast/cycle/feedback del prototipo, mantiene WASD y respeta Inventory/Health input blocking.
- El resolver usa bounds/impact point para `Head/Torso/LeftArm/RightArm/LeftLeg/RightLeg`; M39 y M38 continúan resolviendo bleeding, vitalidad, muerte y corpse.

## Persistence Y Compatibilidad

`ItemState.firearmState` agrega el estado cargado al Current Slice schema/envelope V1. Capture cubre todos los owners mediante la tabla única de items; preflight valida definición, profile, compatibilidad y capacidad; apply/compare/rollback reutilizan la transacción M37–M39. Un save V1 anterior que omite `firearmState` deriva unloaded sin inventar munición; null presente o estado inválido se rechaza antes de mutar.

## Deuda Y Fuera De Alcance

Quedan fuera armor/penetration, proyectiles físicos, critical hits, spread/balance final, animación/audio final, condition/desgaste, AI combat, dual wield, attachments y UI final. El balance severity/bleeding sigue siendo deuda no bloqueante de M39.

## Próximo Trabajo

- M40.1 — Armor & Penetration V1 queda `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`.
- No diseñar ni implementar M40.1 sin autorización explícita.
