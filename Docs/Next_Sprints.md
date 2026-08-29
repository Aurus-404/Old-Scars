# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximo Trabajo

### 1. M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1

Estado: `AUTHORIZED — IMMEDIATE PRIORITY`.

Documento de diseño y secuencia: [NPC_Sandbox_and_Equipment_Sequence.md](NPC_Sandbox_and_Equipment_Sequence.md).

M41.3 ya quedó `DONE — NPC SANDBOX SPAWN & RANDOMIZED LOADOUTS V1 VALIDATED` en `a90dc4e1a38bef69e3762e398a378a666a9f993e`. El sandbox puede spawnear múltiples NPCs con loadouts ponderados/reproducibles, equipment/inventory/ownership reales, roaming mediante `ActorNavigationController`, localized damage M39/M40, muerte real y corpse continuity exacta sin reroll al morir, abrir, reabrir o restaurar Current Slice.

Objetivo inmediato de M41.4:

- agregar controles debug equivalentes a `Spawn Blue NPC` / `Spawn Red NPC`;
- usar colores sólo como presentación debug sobre affiliation/disposition genérica;
- baseline de prueba: Blue no hostil al Player; Red hostil a Blue y Player; same-team no hostil por defecto;
- adquirir amenazas automáticamente mediante candidatos cercanos + `ActorVisualPerceptionService`/LOS antes de asignarlas a `HumanEncounterAIController`;
- preservar M41.1 como encounter brain, M41.0 como Navigation/Perception y M40/M40.1 como combat/armor authorities;
- firearm AI debe cerrar distancia hasta un engagement válido y nunca resolver daño más allá de `firearm.range`;
- melee AI debe acercarse hasta `melee_range` antes de atacar;
- aim NPC físicamente imperfecto: target aproximado + error angular + ruta física existente;
- reaction/acquisition delay y focus time antes de precisión útil;
- spread afectado por distancia, movimiento y arma, sin llegar a aimbot perfecto;
- misses físicos y posibles impactos en otras regiones u obstáculos;
- observabilidad development-only de affiliation, target, distancia, weapon range, state, perception, focus, spread y navigation.

Las referencias investigadas registradas en [NPC_Sandbox_and_Equipment_Sequence.md](NPC_Sandbox_and_Equipment_Sequence.md) —Source/Half-Life 2, Arma 3 y Halo— son referencias conceptuales, no código ni valores a copiar.

Fuera de M41.4:

- sistema completo de facciones/reputación;
- squads, cover tactics sofisticadas o strategic/off-sector AI;
- full ballistics, bullet drop, travel time, wind o solver físico productivo;
- final NPC population/ecology;
- production UI;
- world streaming/LOD/navigation productivos;
- minería/geología/fluid simulation;
- condition/repair/crafting;
- producción masiva de contenido.

### 2. Playtest / Review Después De M41.4

No encadenar automáticamente otro sistema grande.

Prueba objetivo:

`WorldRuntime → varios NPCs con loadouts distintos → navegación real → Blue/Red detectan hostiles → cierran distancia según arma → disparan/golpean con precisión imperfecta → localized health/armor/death → corpse loot exacto`

Después se revisarán bugs reales, navegación, ownership/equipment, combate, comportamiento emergente y game feel antes de decidir la siguiente mecánica o volver a worldgen/materialización.

### 3. Decisión Posterior

Después del playtest/review se elegirá explícitamente entre:

- correcciones/polish de gameplay si el sandbox expone problemas reales;
- una mecánica base adicional;
- o retorno a world realization/materialización si la base jugable queda suficientemente estable.

No autorizar por inercia Bounded History, World Persistence general, Sector Blueprint, Large-Sector Navigation ni otro bloque open-world grande.

## Estado Cerrado Relevante

- `M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1` — `DONE — VALIDATED`, commit `a90dc4e1a38bef69e3762e398a378a666a9f993e`;
- `M41.2 — Basic Equipment & Weapon Coverage V1` — `DONE — VALIDATED`, commit `4f877da10dee813b0bed816194110b5a27087683`;
- `Deformable Volumetric Terrain Foundation / Technical Spike` — `VALIDATED — TECHNICAL SPIKE COMPLETE`, commit técnico `d0309cf053be220a22151cae2dae9aca6f988e6f`, integrado en `dev` por `1b41ead829cd566c55df5adfc0522e33e1dffb96`;
- `M41.1 — Human Encounter AI V1` — `DONE — HUMAN ENCOUNTER AI V1 VALIDATED`;
- M37/M37.1 persistence, M39 localized health, M40/M40.1 combat/armor, M41.0 navigation/perception y M34/M35 equipment/ownership permanecen autoridades reutilizables.

## No Iniciar Todavía

Durante M41.4 no iniciar por inercia:

- Bounded History / World Persistence general / Sector Blueprint;
- mining/geology/fluid simulation;
- whole-world voxels, streaming/sector transition o LOD productivo;
- weather/seasons/final rivers;
- production UI;
- facciones completas/reputación/memoria regional;
- squads/tactics/cover AI sofisticada;
- strategic/off-sector AI;
- full ballistic simulation/bullet drop/wind;
- final loot economy/balance;
- final NPC population/ecology;
- condition/repair/crafting;
- producción masiva de contenido.

El próximo paso inmediato es `M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1`. Después: `playtest/review → decisión explícita`.
