# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M40.1 — Armor & Penetration V1

Estado actual:

`IMPLEMENTED — AUTOMATED ARMOR / PENETRATION VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`

`Combat Ready — PENDING MANUAL M40.1 CLOSEOUT`.

M40.0 permanece `DONE — COMBAT RESOLUTION & WEAPONS V1 VALIDATED`. M40.1 agrega cobertura regional equipped-only, penetración determinista común para wearable armor y world surfaces, trauma residual y un dispatch explícito de consecuencias; M39/M38 conservan autoridad sobre wounds, bleeding, pain, vitalidad, muerte y corpse. `Persistence Ready` continúa `APPROVED`.

## Evidencia Automatizada

- Runtime/Editor compile, Global Content ID Namespace Foundation, M36.1 Foundation Identity, M38.0 lifecycle, M39.0 Health/Medicine y M40.0 Combat Resolution & Weapons: `PASS`.
- `M40.1 Armor & Penetration Diagnostics: PASS` en dos Play sessions: contratos A–W, seis regiones, Equipment authority, stopped con/sin trauma, exact threshold, residual penetration, melee, death/corpse, save/load exacto, legacy V1, invalid data sin mutación y regresiones M40.
- World coverage: thin penetrable cover + actor, resistant cover, dos superficies sucesivas, budget agotado, límite de cuatro, geometría opaca y caso combinado world cover → wearable armor → actor: `PASS`.
- La `.303` Core declara `penetration_power: 0.65`; una capa del fixture de torso (`resistance: 0.325`) produce residual `0.325` y dos instancias equipadas consumen exactamente el budget hasta `Stopped`.
- 13 archivos C# y 1800 líneas C# agregadas, exactamente dentro del techo duro; JSON/docs/.meta no cuentan.
- `SampleScene` unchanged, SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`; Packages y ProjectSettings intactos.
- No aparecieron warnings nuevos atribuibles a M40.1; permanecen los seis warnings C# preexistentes documentados y la deuda legacy Core Content ID.

## Contrato Funcional

- `PenetrationResolutionService` es receiver-independent: `incomingPower <= resistance` produce `Stopped`; `incomingPower > resistance` produce `Penetrated`; residual = `max(0, incomingPower - resistance)`.
- `ArmorProfileDefinition` declara seis regiones posibles, profile de penetración, resistencia de impacto, blunt transfer/threshold y `layer_priority`. `PenetrationProfileDefinition` declara la resistencia en una escala interna compartida, no en unidades físicas fingidas.
- Sólo `ActorEquipmentComponent.Entries` protege; inventory, backpacks, containers y world items no lo hacen. Capas aplicables se ordenan determinísticamente.
- World geometry es opaca salvo `penetration_profile_id` explícito. La continuación usa epsilon `0.001`, deduplicación de collider/owner y máximo cuatro superficies por attack.
- Un stop nunca produce `Puncture` y puede producir cero heridas o una única `Blunt`; una penetración produce como máximo una única consecuencia residual. El adapter humano llama M39 sólo cuando el collider terminal realmente es un actor médico.
- Toda `AmmoProfileDefinition` de proyectil exige `penetration_power > 0`; no existen branches `IsAP`, `CanPenetrate`, FMJ/AP/HP ni por IDs concretos.
- Melee no atraviesa paredes y reutiliza el mismo núcleo contra impact resistance de la armor que cubre al receptor directo.

## Persistence, Condition Y Compatibilidad

M40.1 agrega cero estado durable: profiles son Definitions y Equipment/`ItemInstance` ya hacen round-trip exacto. No existen `armorState` ni `penetrationState`; schema/envelope V1 siguen en versión 1 y un save anterior no inventa armor. El diagnostic confirmó misma armor/`InstanceId` equipada y protección post-load. `EffectiveResistance(ItemInstance, baseResistance)` queda como seam M43; M40.1 no lee, degrada ni muta `Condition`.

## Próximo Trabajo

- Ejecutar únicamente `M40.1 — Manual Unity Validation & Closeout` con el checklist de [Next_Sprints.md](Next_Sprints.md).
- El menú de Play Mode `Old Scars > Diagnostics > Combat > M40.1 Prepare or Cycle Manual Armor Target` prepara y cicla los modos `StoppedTwoLayers`, `PenetratedOneLayer` y `UnarmoredInventoryOnly` sin modificar `SampleScene`.
- No iniciar M41.0 hasta completar el recheck manual y decidir `Combat Ready`.
