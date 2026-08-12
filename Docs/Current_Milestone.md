# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M38.1 — Needs, World Clock & Recovery V1

Estado actual:

`IMPLEMENTED — AUTOMATED WORLD TIME / NEEDS / RECOVERY VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`

Validation — `AUTOMATED PASS; MANUAL FRESH-SESSION PENDING`

Pass 1 implementa `WorldClock` como autoridad runtime única sobre segundos absolutos de game time, deriva `Day N / HH:MM`, gobierna Hunger/Thirst mediante el mismo delta normal o explícito y agrega `ActorRestService.TryRest` más acciones debug `Rest 1h` / `Sleep 8h`. M38.0 continúa `DONE — ACTOR RUNTIME & LIFECYCLE VALIDATED`; M37.1 continúa `DONE — CURRENT SLICE PERSISTENCE VALIDATED`; `Persistence Ready` continúa `APPROVED`.

## Implementación Y Automatización

- Runtime/Editor compilation, Content ID Foundation, M36.1 Checkpoint A/Foundation, M37.0, ambos M37.1, M38.0 e Inventory Interaction UX: `PASS`.
- `M38.1 Needs, World Clock & Recovery Diagnostics: PASS` en dos Play sessions, incluido Day/HH:MM, progresión exacta, food/water, rest/sleep, actor Dead, save/load fresh-session, compatibilidad V1 sin clock, preflight sin mutación y rollback post-clock/needs.
- Fault post-runtime-state: `ApplyFailed` esperado, `RollbackAttempted: True`, `RollbackSucceeded: True`; pre-state y post-rollback equivalentes.
- `SampleScene` unchanged, SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`.
- Cero warnings nuevos atribuibles a M38.1; permanecen los seis warnings C# preexistentes documentados.

## Contrato Funcional

- `elapsedGameSeconds` monotónico y durable; bootstrap/legacy default `Day 1 00:00`; límite finito/no negativo; `writtenUtc` permanece metadata separada.
- Escala provisional configurable: `60 game seconds / real second`; `Time.deltaTime` conserva pausa futura por `timeScale == 0`, mientras rest/sleep avanza directamente sin loops.
- Las tasas serializadas legacy se preservan y se interpretan como `1.8 Hunger` y `3.0 Thirst` por game hour. Sleep 8h consume `14.4/24` respectivamente.
- Sólo el player posee `ActorNeedsComponent` en el Current Slice real. No se agregaron needs ficticios a NPCs/runtime actors ni se amplió `ActorState`.
- Rest/sleep rechaza Dead, no revive y no cura health, heridas, sangrado, dolor ni medicina.
- Fatigue: `DEFERRED — SHOULD, NOT REQUIRED FOR M38.1 FUNCTIONAL CLOSEOUT`; no existe un modelo previo coherente y forzarla ampliaría desproporcionadamente el contrato.

## Persistence Y Compatibilidad

`WorldClockState` es un DTO plano top-level de Current Slice schema V1. Capture, semantic preflight, canonical comparison, apply silencioso y rollback usan la transacción M37/M38 existente. Un save V1 que omite `worldClock` carga con `Day 1 00:00`; un campo presente null, no finito, negativo o fuera de rango se rechaza antes de mutar. Player Hunger/Thirst conserva su DTO y restore atómico existente.

## Deuda Y Fuera De Alcance

Quedan fuera fatigue, UI final, beds/camping/shelters, health/medicine M39, heridas, combate, IA, clima, schedules, streaming, autosave y playable exploration prototype. La deuda Content ID authored preexistente permanece sin cambios.

## Próximo Trabajo

- M38.1 requiere validación manual de Mauro en Play Mode y fresh-session antes de `DONE`.
- M39.0 queda `PLANNED — BLOCKED BY M38.1 MANUAL CLOSEOUT`.
- No iniciar M39.0 ni el playable exploration prototype en este commit.
