# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximo Trabajo

### 1. M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1

Estado: `AUTHORIZED — IMMEDIATE PRIORITY`.

Documento de diseño y secuencia: [NPC_Sandbox_and_Equipment_Sequence.md](NPC_Sandbox_and_Equipment_Sequence.md).

M41.2 ya quedó `DONE — BASIC EQUIPMENT & WEAPON COVERAGE V1 VALIDATED` en `4f877da10dee813b0bed816194110b5a27087683`. El layout humano de 17 slots tiene coverage data-driven, existen 27 items Core nuevos de ropa/equipment, tres tiers reales de mochila `8×10` / `10×12` / `12×14`, y las firearms cubren `manual_cycle` / `semi_automatic` / `automatic` con ranges `80` / `75` / `60` sin branches por DefinitionId. `firearm.range` y `melee_range` limitan el alcance físico temporal; Current Slice, ownership, storage y M40/M40.1 permanecieron verdes.

Objetivo inmediato de M41.3:

- agregar un control development-only para spawnear NPCs reales dentro de `WorldRuntime`;
- usar las autoridades existentes de actor spawn/identity, Equipment, ItemInstance, ownership, inventory y corpse continuity;
- ubicar el spawn sobre posición materializada y NavMesh válida;
- generar loadouts distintos desde JSON con probabilidades reales y posibilidad explícita de `none`;
- auditar las Loot Tables v0 determinísticas antes de decidir si conviene extenderlas o crear un `Actor Loadout Table/Profile` separado y semánticamente correcto;
- mantener el roll concreto diagnosticable/reproducible mediante seed/evidence de debug;
- convertir equipment, backpack, inventory, weapon y ammo seleccionados en estado real del actor;
- roaming básico mediante `ActorNavigationController` para estresar navegación sobre el terrain vigente;
- reutilizar M39/M40/M40.1 para localized health, armor, damage y muerte;
- al morir, el corpse conserva exactamente las pertenencias del actor vivo; no rerollear loot al morir ni al abrir el cadáver;
- permitir varios NPC simultáneos sin autoridades paralelas ni duplicación de ownership/items.

Fuera de M41.3:

- affiliation Blue/Red;
- hostile acquisition automático;
- reaction/focus/spread o imperfect AI aim;
- full ballistics/bullet drop/travel time/wind;
- strategic AI, squads, cover tactics, jobs o schedules;
- final NPC ecology/population;
- production UI;
- world streaming/LOD/navigation productivos.

### 2. M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1

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

### 3. Playtest / Review Después De M41.4

No encadenar automáticamente otro sistema grande.

Prueba objetivo:

`WorldRuntime → varios NPCs con loadouts distintos → navegación real → Blue/Red detectan hostiles → cierran distancia según arma → disparan/golpean con precisión imperfecta → localized health/armor/death → corpse loot exacto`

Después se revisarán bugs reales, navegación, ownership/equipment, combate y game feel antes de decidir la siguiente mecánica o volver a worldgen/materialización.

## Estado Cerrado Relevante

- `M41.2 — Basic Equipment & Weapon Coverage V1` — `DONE — VALIDATED`, commit `4f877da10dee813b0bed816194110b5a27087683`;
- `Deformable Volumetric Terrain Foundation / Technical Spike` — `VALIDATED — TECHNICAL SPIKE COMPLETE`, commit técnico `d0309cf053be220a22151cae2dae9aca6f988e6f`, integrado en `dev` por `1b41ead829cd566c55df5adfc0522e33e1dffb96`;
- `M41.1 — Human Encounter AI V1` — `DONE — HUMAN ENCOUNTER AI V1 VALIDATED`;
- M37/M37.1 persistence, M39 localized health, M40/M40.1 combat/armor, M41.0 navigation/perception y M34/M35 equipment/ownership permanecen autoridades reutilizables.

## No Iniciar Todavía

Durante M41.3–M41.4 no iniciar por inercia:

- minería/geología/fluid simulation;
- whole-world voxels, streaming/sector transition o LOD productivo;
- weather/seasons/final rivers;
- Bounded History / World Persistence general / Sector Blueprint;
- production UI;
- facciones completas/reputación/memoria regional;
- squads/tactics/cover AI sofisticada;
- strategic/off-sector AI;
- full ballistic simulation/bullet drop/wind;
- final loot economy/balance;
- final NPC population/ecology;
- condition/repair/crafting;
- producción masiva de contenido.

El próximo paso inmediato es `M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1`. Después: `M41.4 → playtest/review`.
