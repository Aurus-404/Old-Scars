# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximo Trabajo

### 1. Deformable Volumetric Terrain Foundation / Technical Spike

Estado: `AUTHORIZED — IMMEDIATE PRIORITY`.

Documento de decisión y alcance: [Deformable_Terrain_Foundation.md](Deformable_Terrain_Foundation.md).

La decisión de producto queda aprobada: el terreno productivo de Old Scars debe admitir deformación volumétrica localizada y persistente para soportar, cuando existan sus consumidores de gameplay, cavado, pozos, zanjas/trincheras, cráteres, explosiones, excavación lateral, túneles y cuevas. Una representación heightmap-only no satisface ese requisito.

`Terrain Materialization Technical Spike` permanece `VALIDATED — TECHNICAL SPIKE COMPLETE`, pero Unity `Terrain/TerrainCollider` pasa a considerarse benchmark/prototipo heightmap y no una representación productiva definitiva que futuros sistemas puedan asumir.

El primer coding unit autorizado es:

`ID TBD — Deformable Volumetric Terrain Foundation / Technical Spike`

Objetivo inmediato: demostrar una representación local chunked volumétrica —preferentemente smooth voxel/density field o equivalente demostrado— que consuma la macro truth existente, genere mesh+collision, permita una deformación subtract real y persista al menos una modificación de prueba.

Aceptación mínima:

- área local volumétrica derivada de la truth existente sin reemplazar Macro Geography/Water/Climate/Environment;
- mesh y collision funcionales;
- deformación runtime localizada con cráter/cavidad real;
- al menos una forma imposible de representar correctamente con una única heightmap, por ejemplo túnel corto, cavidad con techo, overhang o excavación lateral;
- rebuild limitado a chunks/regiones afectadas;
- medición de generación, mesh rebuild, collider update y memoria aproximada;
- prueba de persistencia o round-trip equivalente de la deformación;
- player capaz de recorrer la superficie y la zona deformada;
- estrategia explícita para dirty chunks/mutation state y navegación local posterior;
- ningún whole-world voxel allocation ni autoridad mundial paralela.

El spike también debe servir como baseline visual cercano. El terreno actual funciona razonablemente de lejos pero se percibe plástico/reflejante y tosco de cerca. No se exige arte final: Codex está autorizado a crear texturas placeholder task-owned simples —por ejemplo PNGs generados por script para surface/topsoil, soil y rock— y usarlas en la prueba. Deben ser mates, legibles de cerca, reemplazables y claramente no finales.

No hace falta usar generación de imagen por IA para estos placeholders; una textura procedural simple es válida. Si el entorno de ejecución dispone de una herramienta apropiada para producir un placeholder de imagen task-owned, también puede usarse, pero el objetivo es validar rendering/texel density/material response, no producir arte definitivo.

### 2. Secuencia M41.2–M41.4 — Equipment + NPC Sandbox

Después de cerrar y reconciliar el terrain deformable, la prioridad pasa a una secuencia jugable concreta y observable antes de volver a ampliar worldgen.

Documento de diseño y alcance completo: [NPC_Sandbox_and_Equipment_Sequence.md](NPC_Sandbox_and_Equipment_Sequence.md).

#### M41.2 — Basic Equipment & Weapon Coverage V1

Estado: `PLANNED — NEXT AFTER TERRAIN CLOSEOUT`.

Objetivo:

- inspeccionar los 17 slots reales de `core:human_standard_01` y cubrir cada slot relevante con al menos una pieza equipable funcional;
- agregar ropa/equipment básico sin exigir modelos, iconos o arte final;
- agregar varias mochilas con capacidades distintas para estresar item-owned storage, ownership y persistence;
- mantener Lee-Enfield como cobertura bolt-action y agregar al menos un arma semi-automatic y una automatic;
- cualquier fire/action mode nuevo debe ser genérico/data-driven y reutilizar `WeaponCombatService`, no ramas hardcodeadas por arma;
- hacer explícito que `firearm.range` es el máximo físico temporal del hitscan y `melee_range` el máximo melee;
- clamp visual/debug de aim al rango efectivo para no sugerir disparos infinitos;
- no implementar todavía bullet drop, velocidad de bala ni balística productiva.

#### M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1

Estado: `PLANNED — AFTER M41.2`.

Objetivo:

- agregar un botón/control development-only para spawnear NPCs reales en WorldRuntime;
- spawn sólo sobre posición/materialización/NavMesh válida y mediante las autoridades existentes de actor/identity;
- loadout aleatorio desde JSON con probabilidades reales y posibilidad explícita de `none`;
- equipment, backpack, inventory, weapon y ammo deben convertirse en estado real del actor, no loot decorativo;
- las loot tables v0 actuales son determinísticas y no soportan chance/weights; antes de implementar se debe auditar si corresponde extenderlas o crear un Actor Loadout Table/Profile data-driven separado;
- cada spawn puede ser distinto, pero el roll concreto debe quedar diagnosticable/reproducible;
- roaming básico mediante `ActorNavigationController` para probar navegación real sobre el mapa/terrain vigente;
- el actor debe usar M39/M40/M40.1, poder recibir daño localizado, morir y dejar exactamente su equipment/inventory real en el cadáver;
- prohibido rerollear loot al morir o abrir el cadáver.

#### M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1

Estado: `PLANNED — AFTER M41.3`.

Objetivo:

- controles debug `Spawn Blue NPC` y `Spawn Red NPC` o equivalente;
- los colores son representación debug, no reglas hardcodeadas de lógica;
- baseline: Blue no hostil al Player; Red hostil a Blue y Player; same-team no hostil por defecto;
- agregar la capa mínima de affiliation/disposition y adquisición automática de amenaza usando candidatos cercanos + `ActorVisualPerceptionService`/LOS antes de `HumanEncounterAIController`;
- firearm AI debe cerrar distancia si el target está fuera de `firearm.range` o del preferred engagement range;
- melee AI debe acercarse hasta `melee_range` antes de golpear;
- ningún daño físico puede resolverse fuera del alcance del arma;
- aim NPC no debe ser aimbot: target aproximado + error angular físico + `PhysicalShotPathResolver`;
- reaction/acquisition delay y focus time deben hacer que la precisión mejore progresivamente sin llegar a perfección normal;
- distancia, movimiento y arma/fire mode pueden modificar spread;
- el sistema debe permitir misses físicos y golpes en regiones distintas, no un porcentaje abstracto que decida hit/miss antes del ray/path físico;
- mantener observabilidad development-only de target, distancia, weapon range, state, perception, focus, spread y navigation para entender el comportamiento mirando la partida.

Referencias de diseño ya investigadas y registradas en el documento de secuencia: Source/Half-Life 2 para reaction/focus/spread, Arma 3 para engagement/fire-mode ranges y Bungie Halo para importancia de engagement distance. Son referencias conceptuales, no implementaciones a copiar.

### 3. Playtest / Review Después De M41.4

No encadenar automáticamente otro sistema grande.

La prueba objetivo es:

`WorldRuntime → spawnear varios NPCs con loadouts distintos → navegación real → Blue/Red detectan hostiles → se acercan según alcance → disparan/golpean con precisión imperfecta → localized health/armor/death → corpse loot exacto`.

Después de esa prueba se revisarán bugs reales, navegación, ownership/equipment, combate y game feel antes de decidir la siguiente mecánica o volver a worldgen/materialización.

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
- `Macro Environment / Biome Regions V1` — `VALIDATED — FOUNDATION COMPLETE`, commit `55bcb0db479af43351f28908dfe05125dd9d62e1`.

Environment añade `MacroEnvironmentPlan` bajo `macro_environment_v1`, con 14 familias terrestres + `None`, `PrimaryBiome` / `SecondaryBiome` / `TransitionQ16`, océano `None/None/0`, sin biome noise ni region IDs persistentes. `world_session_v1` schema `7` persiste esa truth y schemas `1`–`6` continúan legacy exactos sin Environment fabricado.

New Game actual genera plan finito → elevation/landforms → Macro Water → Macro Climate → Macro Environment → quality/starter → Macro Human Geography. Ese worldgen macro queda suficientemente avanzado por ahora y no debe seguir creciendo por inercia mientras gameplay y representación física cercana todavía necesitan trabajo.

## Connected First Playable

El Connected First Playable sigue siendo el objetivo integrado posterior a las foundations/materialización/continuidad necesarias. Debe demostrar A→B→A, continuidad cross-sector, mutaciones persistentes, save, full process exit y fresh load usando M32–M41.1 dentro del runtime canónico.

La nueva decisión de terrain deformable añade una condición importante: Sector Materialization productiva no debe quedar atada a una heightmap incapaz de representar las mutaciones físicas requeridas.

La secuencia M41.2–M41.4 se considera un sandbox de integración previo que estresa equipment, actors, navigation, perception, combat, localized health, corpse loot y ownership dentro del runtime real; no reemplaza el Connected First Playable.

## Modding Y Provenance

La Global Content ID Foundation y la Minimum Content Source Identity & Provenance Foundation están validadas. Cada source requiere manifest `source_id`/`namespace`/`version`; ownership de declaraciones, orden estable y SHA-256 de recognized inputs están implementados sobre el pipeline Core/mod existente.

`Provenance` prueba qué fuentes/inputs estuvieron presentes. `Generation compatibility` continúa no implementada y será responsable de decidir compatibilidad semántica de mundos; no se infiere solamente desde igualdad/diferencia del fingerprint.

Dependencies, overrides/patches y compatibilidad de producción permanecen en alcance posterior M50.0.

## No Iniciar Todavía

Durante el Deformable Terrain spike y la secuencia M41.2–M41.4 no iniciar por inercia:

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

El próximo paso inmediato continúa siendo terminar y cerrar el coding unit autorizado de terrain deformable en el checkout canónico. Cuando quede cerrado, la secuencia prevista es M41.2 → M41.3 → M41.4 → playtest/review.
