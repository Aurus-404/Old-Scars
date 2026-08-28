# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximo Trabajo

### 1. M41.2 — Basic Equipment & Weapon Coverage V1

Estado: `AUTHORIZED — IMMEDIATE PRIORITY`.

Documento de diseño y secuencia: [NPC_Sandbox_and_Equipment_Sequence.md](NPC_Sandbox_and_Equipment_Sequence.md).

El Deformable Volumetric Terrain Foundation / Technical Spike ya está `VALIDATED — TECHNICAL SPIKE COMPLETE` en el commit técnico `d0309cf053be220a22151cae2dae9aca6f988e6f`, integrado en `dev` por `1b41ead829cd566c55df5adfc0522e33e1dffb96`. Su evidencia y límites quedan registrados en [Deformable_Terrain_Foundation.md](Deformable_Terrain_Foundation.md).

M41.2 cambia deliberadamente el foco desde foundations grandes hacia coverage funcional que permita probar los sistemas existentes dentro de WorldRuntime.

Objetivos inmediatos:

- inspeccionar y usar los 17 slots reales de `core:human_standard_01` como única fuente de verdad;
- cubrir cada slot ejercitable con al menos un item equipable funcional, sin exigir modelos, iconos, attachments ni arte final;
- agregar ropa/equipment básico con sólo las propiedades que los sistemas actuales consumen;
- agregar varias mochilas con capacidades realmente distintas para estresar item-owned storage, ownership, transfers y Current Slice;
- preservar Lee-Enfield como bolt-action y agregar al menos una firearm semi-automatic y una automatic;
- si el backend necesita representar fire/action modes, agregar únicamente la extensión genérica/data-driven mínima y reutilizar `WeaponCombatService`;
- hacer explícito y observable que `firearm.range` es el máximo físico temporal del hitscan y `melee_range` el máximo melee;
- clamp visual/debug del aim al alcance efectivo para no sugerir daño a distancia infinita;
- validar equip/unequip, ownership, grid/storage, backpack content y persistence/round-trip sin crear autoridades paralelas.

Fuera de M41.2:

- modelos/animaciones/audio/iconos finales;
- producción masiva de ropa o armas;
- loot probabilístico de NPCs;
- NPC spawn sandbox;
- affiliation/hostility;
- imperfect AI aim;
- full ballistics/bullet drop/travel time/wind;
- condition/repair/crafting;
- terrain productivo adicional por inercia.

### 2. M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1

Estado: `PLANNED — AFTER M41.2`.

Objetivo:

- control development-only para spawnear NPCs reales en WorldRuntime;
- spawn sobre posición/materialización/NavMesh válida mediante las autoridades existentes de actor/identity;
- loadout aleatorio desde JSON con probabilidades reales y posibilidad explícita de `none`;
- auditar Loot Tables v0 antes de decidir si se extienden o si corresponde un Actor Loadout Table/Profile separado;
- equipment, backpack, inventory, weapon y ammo se convierten en estado real del actor;
- seed/roll evidence diagnosticable para reproducir un spawn concreto;
- roaming básico con `ActorNavigationController` sobre el terrain vigente;
- localized health, armor, death y corpse continuity reales;
- el cadáver conserva exactamente el equipment/inventory del actor vivo; nunca rerollear loot al morir o abrirlo.

### 3. M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1

Estado: `PLANNED — AFTER M41.3`.

Objetivo:

- controles debug equivalentes a `Spawn Blue NPC` / `Spawn Red NPC`;
- colores sólo como presentación debug, con affiliation/disposition genérica por debajo;
- baseline: Blue no hostil al Player; Red hostil a Blue y Player; same-team no hostil por defecto;
- adquisición automática mínima de amenazas mediante candidatos cercanos + `ActorVisualPerceptionService`/LOS antes de `HumanEncounterAIController`;
- firearm AI cierra distancia hasta un engagement válido y nunca resuelve daño más allá de `firearm.range`;
- melee AI se acerca hasta `melee_range` antes de golpear;
- aim NPC físicamente imperfecto: target aproximado + error angular + `PhysicalShotPathResolver`;
- reaction/acquisition delay, focus time y spread afectado por distancia/movimiento/arma sin llegar a aimbot perfecto;
- misses físicos y posibles impactos en otras regiones/obstáculos;
- observabilidad development-only de target, distancia, weapon range, state, perception, focus, spread y navigation.

Las referencias investigadas registradas en [NPC_Sandbox_and_Equipment_Sequence.md](NPC_Sandbox_and_Equipment_Sequence.md) —Source/Half-Life 2, Arma 3 y Halo— son referencias conceptuales, no código ni valores a copiar.

### 4. Playtest / Review Después De M41.4

No encadenar automáticamente otro sistema grande.

Prueba objetivo:

`WorldRuntime → varios NPCs con loadouts distintos → navegación real → Blue/Red detectan hostiles → cierran distancia según arma → disparan/golpean con precisión imperfecta → localized health/armor/death → corpse loot exacto`

Después se revisarán bugs reales, navegación, ownership/equipment, combate y game feel antes de decidir la siguiente mecánica o volver a worldgen/materialización.

## Estado Del Mundo Abierto Cerrado Hasta Aquí

Las foundations y application shell cerradas incluyen:

- `Minimum Content Source Identity & Provenance Foundation` — `VALIDATED — FOUNDATION COMPLETE`;
- `World Identity, Topology & Determinism Foundation` — `VALIDATED — FOUNDATION COMPLETE`;
- `World Session + Persistence V1 / New Game Save-Load Application Shell` — `VALIDATED — APPLICATION SHELL COMPLETE`;
- `Macro World Plan V1` — `VALIDATED — FOUNDATION COMPLETE`;
- `Macro Elevation / Landforms V1` — `VALIDATED — FOUNDATION COMPLETE`;
- `Worldgen Gameplay Quality + Macro Water V1` — `VALIDATED — FOUNDATION COMPLETE`;
- `Worldgen Pass Isolation Correction` — `VALIDATED — SYSTEMIC CORRECTION COMPLETE`;
- `Worldgen / World Session Observability Correction` — `VALIDATED — OBSERVABILITY CORRECTION COMPLETE`;
- `Macro Human Geography / Road Network V1` — `VALIDATED — FOUNDATION COMPLETE`;
- `Terrain Materialization Technical Spike` — `VALIDATED — TECHNICAL SPIKE COMPLETE`;
- `Integrated Gameplay Runtime / SampleScene Convergence` — `VALIDATED — RUNTIME CONVERGENCE COMPLETE`, commit `8c485c78b4ab294de9d983f70ebadfba634ab3e1`;
- `Macro Climate Baseline V1` — `VALIDATED — FOUNDATION COMPLETE`, commit `457836e7f10a9b2ddbc08cc1db05ca38cd3f7108`;
- `Player Traversal / Camera & Runtime Debug Ergonomics Pass` — `VALIDATED — RUNTIME ERGONOMICS COMPLETE`, commit final `ab78da4fbb1af9189d6a5c178515fafdb56f368e`;
- `Macro Environment / Biome Regions V1` — `VALIDATED — FOUNDATION COMPLETE`, commit `55bcb0db479af43351f28908dfe05125dd9d62e1`;
- `Deformable Volumetric Terrain Foundation / Technical Spike` — `VALIDATED — TECHNICAL SPIKE COMPLETE`, commit técnico `d0309cf053be220a22151cae2dae9aca6f988e6f`, integrado en `dev` por `1b41ead829cd566c55df5adfc0522e33e1dffb96`.

El terrain volumétrico validó una lattice de density compartida y chunked, Marching Tetrahedra, mesh/collider, crater, túnel con techo, cross-chunk mutation, dirty rebuild, persistencia/replay `SPIKE_NON_PRODUCTION`, player traversal y NavMesh local sin cambiar `world_session_v1` schema `7` ni los goldens de worldgen.

New Game actual genera plan finito → elevation/landforms → Macro Water → Macro Climate → Macro Environment → quality/starter → Macro Human Geography. Ese worldgen macro queda suficientemente avanzado por ahora y no debe seguir creciendo por inercia mientras gameplay integrado necesita más cobertura real.

## Connected First Playable

El Connected First Playable sigue siendo el objetivo integrado posterior a las foundations/materialización/continuidad necesarias. La secuencia M41.2–M41.4 es un sandbox de integración previo que estresa equipment, actors, navigation, perception, combat, localized health, corpse loot y ownership dentro del runtime real; no reemplaza el Connected First Playable.

## Modding Y Provenance

La Global Content ID Foundation y la Minimum Content Source Identity & Provenance Foundation están validadas. Cada source requiere manifest `source_id`/`namespace`/`version`; ownership de declaraciones, orden estable y SHA-256 de recognized inputs están implementados sobre el pipeline Core/mod existente.

`Provenance` prueba qué fuentes/inputs estuvieron presentes. `Generation compatibility` continúa no implementada y será responsable de decidir compatibilidad semántica de mundos; no se infiere solamente desde igualdad/diferencia del fingerprint.

Dependencies, overrides/patches y compatibilidad de producción permanecen en alcance posterior M50.0.

## No Iniciar Todavía

Durante M41.2–M41.4 no iniciar por inercia:

- minería como loop completo;
- geología/minerales de producción;
- fluid simulation o agua entrando físicamente en túneles;
- derrumbes/soil structural simulation;
- destrucción completa de edificios/cimientos;
- whole-world voxel allocation;
- caves mundiales masivas;
- streaming/sector transition productivo;
- LOD productivo definitivo;
- dynamic NavMesh de producción a gran escala;
- vegetation/biomes locales finales;
- terrain art/material pipeline final;
- weather runtime, seasons o final rivers;
- Bounded History / Present-Day Resolution;
- World Persistence general o Sector Blueprint productivos por inercia;
- production UI;
- facciones completas/reputación/memoria regional;
- squads/tactics/cover AI sofisticada;
- strategic/off-sector AI;
- full ballistic simulation/bullet drop/wind;
- final loot economy/balance;
- final NPC population/ecology;
- condition, repair o crafting;
- producción masiva de contenido.

El próximo paso inmediato es `M41.2 — Basic Equipment & Weapon Coverage V1`. Después: `M41.3 → M41.4 → playtest/review`.
