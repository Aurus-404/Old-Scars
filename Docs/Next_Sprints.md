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

### 2. Después Del Spike: Core Gameplay / Mechanics Polish

Tras cerrar la foundation de terrain deformable, la prioridad vuelve deliberadamente a las mecánicas base durante un tramo corto antes de profundizar otra vez el mundo abierto.

Orden orientativo, sujeto a inspección del estado real al terminar el spike:

- movement/camera/stamina feel;
- interaction y pickup/drop/equip;
- inventory/containers usability;
- combat/firearms/melee feedback y tuning;
- Needs/Rest tuning para que generen decisiones y no busywork;
- AI/perception/navigation usability y regresiones reales;
- revisión de mecánicas base faltantes antes de autorizar otra foundation macro.

No se autoriza convertir este tramo en UI final, content production masiva ni refactors preventivos.

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

## Modding Y Provenance

La Global Content ID Foundation y la Minimum Content Source Identity & Provenance Foundation están validadas. Cada source requiere manifest `source_id`/`namespace`/`version`; ownership de declaraciones, orden estable y SHA-256 de recognized inputs están implementados sobre el pipeline Core/mod existente.

`Provenance` prueba qué fuentes/inputs estuvieron presentes. `Generation compatibility` continúa no implementada y será responsable de decidir compatibilidad semántica de mundos; no se infiere solamente desde igualdad/diferencia del fingerprint.

Dependencies, overrides/patches y compatibilidad de producción permanecen en alcance posterior M50.0.

## No Iniciar Todavía

Durante el Deformable Terrain spike no iniciar:

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
- nuevas ampliaciones OnGUI sin milestone autorizado;
- UI final;
- condition, repair o crafting;
- facciones amplias;
- producción masiva de contenido.

El próximo paso después de actualizar la documentación es iniciar el coding unit autorizado de terrain deformable directamente en el checkout canónico, sin worktrees.
