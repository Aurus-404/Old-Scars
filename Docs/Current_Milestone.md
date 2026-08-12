# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M39.0 — Localized Health & Medicine V1

Estado actual:

`IMPLEMENTED — AUTOMATED LOCALIZED HEALTH / MEDICINE VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`

Validation — `AUTOMATED PASS; MANUAL FRESH-SESSION PENDING`

Functional Pass 1 implementa seis regiones humanas V1, heridas durables localizadas, severidad, sangrado por `WorldClock`, dolor derivado y tratamiento data-driven con venda. `ActorHealthComponent` conserva la reserva vital escalar y la autoridad Alive/Dead de M38; el estado médico manda sobre heridas, bleeding, pain y bandage. M38.1 continúa `DONE — WORLD TIME / NEEDS / RECOVERY VALIDATED`; `Persistence Ready` continúa `APPROVED`.

## Implementación Y Automatización

- Runtime/Editor compilation, M36.1 Checkpoint A/Foundation, M37.0, ambos M37.1, M38.0, M38.1, Player Controls & Health Window e Inventory Interaction UX: `PASS`.
- `M39.0 Localized Health & Medicine Diagnostics: PASS` en dos Play sessions: baseline, localización, bleeding sin double tick, rest, pain, venda x1, aislamiento regional, muerte/corpse, actor runtime, save/load, legacy V1, preflight y rollback.
- Fault post-medical-state: `ApplyFailed` esperado, `RollbackAttempted: True`, `RollbackSucceeded: True`; pre-state y post-rollback canónicamente equivalentes.
- `SampleScene` unchanged, SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`.
- Cero warnings nuevos atribuibles a M39.0; permanecen los seis warnings C# preexistentes documentados.
- La validación manual de M39.0 permanece pendiente y el milestone no está `DONE`.

## Contrato Funcional

- Regiones: `Head`, `Torso`, `LeftArm`, `RightArm`, `LeftLeg`, `RightLeg`; son un dominio técnico cerrado V1, no Content IDs.
- Cada herida conserva `WoundId`, región, tipo `Laceration/Puncture/Blunt`, severidad, tasa de sangrado, contribución de dolor y estado `Unbandaged/Bandaged`.
- El mismo evento `WorldClock.GameTimeAdvanced` procesa directamente el delta normal o de Rest/Sleep. Sangrar reduce la reserva vital de `ActorHealthComponent`; al agotarla conserva lifecycle Dead/corpse de M38 y no progresa después de muerte.
- La venda Core usa `consumable.wound_treatment`, consume exactamente x1 y reduce el sangrado de una herida concreta sin `Heal(+X)` ni borrar la herida. Core y mods usan el mismo loader/validator/servicio.
- La ventana H existente muestra cuerpo esquemático, regiones, heridas y evaluaciones cualitativas; los números escalares quedan confinados al área DEBUG. Mantiene H/X/Escape, WASD, bloqueo local de input y exclusividad con Inventory.

## Persistence Y Compatibilidad

`PlayerState` y `ActorState` agregan DTOs médicos planos dentro del Current Slice schema/envelope V1. Capture, preflight, canonical compare, apply y rollback reutilizan la transacción M37/M38. Un save V1 anterior que omite `medicalState` deriva baseline sin heridas desde su health escalar, sin inventar etiología; un objeto presente null o inválido se rechaza antes de mutar. Los legacy `CorpseState[]` reciben baseline médico sano conservando health 0.

## Deuda Y Fuera De Alcance

Quedan fuera healing de tejido, vendajes saturados, infección, enfermedades, fracturas, cirugía, órganos, balística, armor, penalizaciones regionales, combate M40, IA, UI final y fisiología avanzada. La deuda Content ID authored preexistente permanece sin cambios.

## Próximo Trabajo

- M39.0: `Manual Unity Validation & Closeout` pendiente.
- M40.0 queda `PLANNED — BLOCKED BY M39.0 MANUAL CLOSEOUT`.
