# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M40.1 — Armor & Penetration V1

Estado final:

`DONE — ARMOR / PENETRATION V1 VALIDATED`

Validation: `AUTOMATED + MANUAL FRESH-SESSION PASSED`.

`Combat Ready — APPROVED`.

M40.0 permanece `DONE — COMBAT RESOLUTION & WEAPONS V1 VALIDATED`. M40.1 validó cobertura regional equipped-only, penetración determinista común para wearable armor y world surfaces, trauma residual y dispatch explícito de consecuencias. M39/M38 conservan autoridad sobre wounds, bleeding, pain, vitalidad, muerte y corpse. `Persistence Ready` continúa `APPROVED`.

## Evidencia De Cierre

- Automatización: Runtime/Editor compile y `M40.1 Armor & Penetration Diagnostics: PASS` en dos Play sessions, incluida la regresión M40.0 requerida, seis regiones, Equipment authority, stop/penetration/trauma residual, melee, superficies world acotadas, muerte/corpse, persistencia V1 y datos inválidos sin mutación.
- Dos capas equipped sumaron resistencia `0.65`: la `.303` con `penetration_power: 0.65` produjo `Stopped`, exactamente una `Blunt` y ninguna `Puncture`. Una capa de `0.325` produjo residual `0.325` y exactamente una `Puncture`.
- Head/arms descubiertos se comportaron unarmored. Las mismas piezas sólo en inventory no protegieron. Crowbar sobre torso protegido produjo `Blunt`; inventory-only no intervino y melee no atravesó paredes.
- Geometría opaca bloqueó antes de armor/actor; restaurar una línea limpia permitió nuevamente el impacto.
- Current Slice guardó y, tras fresh Play, reconstruyó `actor_677cb4714310457d9e35140b04a199f0` mediante `Initialization: PersistenceRestore`; el load informó `FailureCode: Success` y `Result: Success`.
- Las armor `item_65e023d5f6a1478c8384a2f39be86630` y `item_71d498f132b9435c9e85caf1be6a5de4` conservaron identidad y Equipment; el torso volvió a dar `Stopped` con `Blunt` y sin `Puncture`.
- Los warnings legacy Global Content ID permanecen como deuda conocida, no como fallos M40.1.

## Contratos Congelados

- `PenetrationResolutionService` sigue receiver-independent: `incomingPower <= resistance` produce `Stopped`; sólo `incomingPower > resistance` produce `Penetrated`; residual = `max(0, incomingPower - resistance)`.
- Armor protege exclusivamente desde Equipment y por región. World geometry permanece opaca salvo profile penetrable explícito; la continuación del ray usa budget y límites acotados.
- Toda munición de proyectil usa `penetration_power > 0`; futuras FMJ/AP/HP, tracer y anti-material se diferencian por datos y reutilizan el mismo resolver, sin branches binarios por tipo.
- M40.1 no agrega estado durable. `EffectiveResistance(ItemInstance, baseResistance)` queda reservado para M43; machines, vehicles y sus receivers permanecen futuros.
- Proyectiles físicos, ricochet, ángulo, espesor real, spall, fragmentación y sistemas completos de vehículos/máquinas permanecen fuera de alcance.

## Próximo Trabajo

M41.0 — Navigation & Perception Foundation permanece `PLANNED`, disponible para autorización y no iniciado. No existe milestone activo hasta una autorización explícita de Mauro.
