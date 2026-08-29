# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1

Estado actual:

`AUTHORIZED — IMMEDIATE PRIORITY`

Autoridad de alcance detallada: [NPC_Sandbox_and_Equipment_Sequence.md](NPC_Sandbox_and_Equipment_Sequence.md).

Objetivo: convertir el sandbox visible ya validado en M41.3 en una prueba observable de combate NPC↔NPC y NPC↔Player, reutilizando M41.0/M41.1/M40/M40.1 sin crear un segundo stack de AI, percepción, navegación o combate.

M41.4 debe demostrar:

- controles development-only equivalentes a `Spawn Blue NPC` / `Spawn Red NPC`;
- Blue/Red únicamente como presentación debug sobre una affiliation/disposition genérica;
- baseline de prueba: Blue no hostil al Player; Red hostil a Blue y Player; same-team no hostil por defecto;
- adquisición automática mínima de amenazas mediante candidatos cercanos + `ActorVisualPerceptionService`/LOS antes de asignar la amenaza a `HumanEncounterAIController`;
- firearms que cierran distancia hasta un engagement válido y nunca resuelven daño más allá de `firearm.range`;
- melee que se acerca hasta `melee_range` antes de atacar;
- aim NPC físicamente imperfecto mediante punto objetivo aproximado + error angular + ruta física existente;
- reaction/acquisition delay, focus time y spread afectados por distancia/movimiento/arma sin aimbot perfecto;
- misses físicos reales y posible impacto en otras regiones/obstáculos;
- observabilidad development-only de target, distancia, weapon range, state, perception, focus, spread y navigation.

M41.4 no implementa todavía full ballistics, sistema completo de facciones/reputación, squads, cover tactics sofisticadas, strategic/off-sector AI, final NPC population/ecology ni production UI.

Después de M41.4 se exige un playtest/review antes de autorizar otro sistema grande.

## Último Cierre Funcional

### M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1

Estado final:

`DONE — NPC SANDBOX SPAWN & RANDOMIZED LOADOUTS V1 VALIDATED`

Commit funcional publicado: `a90dc4e1a38bef69e3762e398a378a666a9f993e`.

M41.3 cerró con:

- `ActorLoadoutProfileDefinition` separado de las Loot Tables de contenedores, con grupos ponderados, `none` explícito, equipment/inventory, cantidades y packages weapon/ammo validados;
- RNG reproducible mediante base seed + secuencia + profiles, sin `UnityEngine.Random` como autoridad oculta;
- 12 NPC simultáneos con 12 `ActorInstanceId` únicos, 131 `ItemInstance` únicos y 12 firmas de loadout distintas;
- spawn cercano sobre NavMesh y roaming delegado exclusivamente a `ActorNavigationController`;
- corrección genérica del `NavMeshAgent.baseOffset`: `Collider.bounds` se estaba leyendo antes de sincronizar transforms; `Physics.SyncTransforms()` permite derivar la geometría correcta;
- localized damage real mediante M39/M40 en múltiples regiones;
- muerte real por heridas M40 + sangrado/`WorldClock` M39;
- pertenencias canónicas pre-muerte == cadáver == cadáver reabierto;
- apertura/cierre/reapertura mediante la sesión real de inventario, sin reroll;
- Current Slice snapshot/teardown/restore preservando IDs, Definitions, cantidades, Equipment, ownership y storages;
- restore con `LoadoutProfileId` / `LoadoutSignature` ausentes, demostrando que el generador no vuelve a ejecutarse al cargar;
- gate integrado WorldRuntime D3D11, Runtime/Editor compile y regresiones relevantes M37–M41.2 en `PASS`;
- System Harmony sin autoridades paralelas, sin schema/world-session/worldgen changes y sin adelantar M41.4.

Deuda no bloqueante: roaming y metadata del sandbox siguen siendo tooling efímero; no son población persistente ni AI productiva.

## Cierres Relevantes Previos

### M41.2 — Basic Equipment & Weapon Coverage V1

Estado final:

`DONE — BASIC EQUIPMENT & WEAPON COVERAGE V1 VALIDATED`

Commit funcional publicado: `4f877da10dee813b0bed816194110b5a27087683`.

M41.2 dejó los 17 slots reales con coverage data-driven, 27 items Core de ropa/equipment, tres tiers de backpack `8×10` / `10×12` / `12×14`, protección M40.1 y firearms `manual_cycle` / `semi_automatic` / `automatic` con ranges `80` / `75` / `60`.

### Deformable Volumetric Terrain Foundation / Technical Spike

Estado final:

`VALIDATED — TECHNICAL SPIKE COMPLETE`

Commit técnico: `d0309cf053be220a22151cae2dae9aca6f988e6f`.

Integración publicada en `dev`: `1b41ead829cd566c55df5adfc0522e33e1dffb96`.

Autoridad de evidencia: [Deformable_Terrain_Foundation.md](Deformable_Terrain_Foundation.md).

## Contratos Cerrados Relevantes

- M37/M37.1 continúan siendo la autoridad de persistence/Current Slice.
- M38 continúa siendo actor identity/lifecycle/spawn foundation.
- M39 continúa siendo la autoridad de localized health/wounds.
- M40/M40.1 continúan siendo la autoridad de combat, firearms/ammo/reload, armor y penetration.
- M41.0 continúa siendo la autoridad de Navigation/Perception.
- M41.1 continúa siendo el encounter brain; M41.4 debe reutilizarlo y no reemplazarlo.
- M41.3 deja `ActorLoadoutProfileDefinition` y el sandbox runtime como tooling validado, no como población productiva.
- `ActorEquipmentComponent`, item-owned storage, ownership e `ItemInstance` continúan siendo las autoridades de equipment/state.
- `fire_mode` es data-driven; no crear sistemas separados por arma.
- JSON declara contenido; C# ejecuta comportamiento genérico.

## No Iniciar Todavía

Durante M41.4 no iniciar por inercia:

- full faction/reputation system;
- squads/cover tactics/strategic AI;
- full ballistics/bullet drop/travel time/wind;
- final NPC population/ecology;
- production UI;
- condition/repair/crafting;
- mining/geology/fluid simulation;
- world streaming/LOD/navigation productivos;
- producción masiva de contenido.

## Próximo Paso Exacto

Cuando exista cuota Codex suficiente, iniciar `M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1` en el checkout canónico `D:\Programs\UnityProject\Old Scarss`, sin worktrees y preservando el cambio user-owned `ProjectSettings/ProjectSettings.asset` (`runInBackground: 0 → 1`). Antes de implementar, auditar las autoridades reales de affiliation/target assignment/perception/encounter AI y evitar cualquier stack paralelo.
