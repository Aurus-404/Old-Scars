# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximo Trabajo

### 1. Sin milestone de implementación activo

Estado: `MACRO ENVIRONMENT / BIOME REGIONS V1 CLOSED`.

M41.1 está `DONE — HUMAN ENCOUNTER AI V1 VALIDATED`, con validation `AUTOMATED + MANUAL UNITY PASSED`; `AI Ready` está `APPROVED`. Player Traversal / Camera & Runtime Debug Ergonomics Pass también está cerrado y validado. El workflow vigente trabaja directamente sobre el checkout original; no usa worktrees para desarrollo normal de Old Scars.

La dirección [Open World Architecture](Open_World_Architecture.md) está `APPROVED DESIGN DIRECTION — NOT IMPLEMENTED` para sus capacidades futuras restantes. Sus foundations y application shell cerradas incluyen:

`ID TBD — Minimum Content Source Identity & Provenance Foundation`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — World Identity, Topology & Determinism Foundation`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — World Session + Persistence V1 / New Game Save-Load Application Shell`

Estado: `VALIDATED — APPLICATION SHELL COMPLETE`.

`ID TBD — Macro World Plan V1`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — Macro Elevation / Landforms V1`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — Worldgen Gameplay Quality + Macro Water V1`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — Worldgen Pass Isolation Correction`

Estado: `VALIDATED — SYSTEMIC CORRECTION COMPLETE`.

`ID TBD — Worldgen / World Session Observability Correction`

Estado: `VALIDATED — OBSERVABILITY CORRECTION COMPLETE`.

`ID TBD — Macro Human Geography / Road Network V1`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — Terrain Materialization Technical Spike`

Estado: `VALIDATED — TECHNICAL SPIKE COMPLETE`.

`ID TBD — Integrated Gameplay Runtime / SampleScene Convergence`

Estado: `VALIDATED — RUNTIME CONVERGENCE COMPLETE`.

Commit de cierre: `8c485c78b4ab294de9d983f70ebadfba634ab3e1`.

`ID TBD — Macro Climate Baseline V1`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

Commit de cierre: `457836e7f10a9b2ddbc08cc1db05ca38cd3f7108`.

Climate aporta `ThermalIndex` y `MoistureIndex` mundiales bajo `macro_climate_v1`, con norte-frío/sur-cálido, anomalía regional, enfriamiento por elevación, influencia oceánica gradual, orografía acotada y una dirección dominante persistida entre ocho direcciones canónicas. Small→Huge aumenta resolución/frecuencia regional. `world_session_v1` schema `6` conserva esa truth como legacy exacta.

`ID TBD — Player Traversal / Camera & Runtime Debug Ergonomics Pass`

Estado: `VALIDATED — RUNTIME ERGONOMICS COMPLETE`.

Commit final validado: `ab78da4fbb1af9189d6a5c178515fafdb56f368e`.

La cámara continúa centrada en el player y sin pan independiente; RMB aporta yaw/pitch, el zoom pedido se conserva separado de la retracción por `SphereCast` y la cámara vuelve suavemente tras una obstrucción. Shift sprint reutiliza el único `PlayerMovementController`; `ActorStaminaComponent` añade stamina real con recovery/lockout, coste adicional de Hunger/Thirst sólo durante sprint y Current Slice persistence dentro de la transacción existente. El panel F3 development-only unifica ScrollView, movement multiplier, stamina/Needs, presets de WorldClock `1x/2x/3x/5x/10x/20x/50x/100x`, reset de cámara y teleport acotado a suelo materializado.

`ID TBD — Macro Environment / Biome Regions V1`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

Commit de cierre: `55bcb0db479af43351f28908dfe05125dd9d62e1`.

Environment añade `MacroEnvironmentPlan` bajo `macro_environment_v1`, con 14 familias terrestres + `None`, clasificación determinista `PrimaryBiome` / `SecondaryBiome` / `TransitionQ16`, océano `None/None/0`, sin biome noise, sin region IDs persistentes y sin GameObject/runtime authority. `world_session_v1` schema `7` persiste Environment; schemas `1`–`6` conservan su truth legacy exacta sin Environment fabricado. El golden Environment validado es `f8081c040da64ccce5e5eb5ffed941c2c2c44cd7ac5442582ee5d331c3abd1c5`.

La suite Macro Environment, corpus `72/72`, Fresh Process A/B, pass isolation, goldens upstream, World Session, Main Menu→WorldRuntime→Save→Return→Load y Terrain Materialization D3D11 quedaron `PASS`. Las previews Small/Medium/Large/Huge mostraron regiones amplias y coherentes sin seams ni ruido tipo checkerboard.

WorldRuntime sigue siendo el runtime canónico de gameplay y SampleScene el laboratorio/regresión. New Game actual genera plan finito → elevation/landforms → Macro Water → Macro Climate → Macro Environment → quality/starter → Macro Human Geography. Environment no entra todavía en Gameplay Quality, Starter ni Human Geography V1 y no materializa vegetación, fauna, geology, ground materials o weather.

### 2. Revisión de secuencia post-Environment

No hay un nuevo coding unit autorizado automáticamente. Antes de mandar otra implementación a Codex, hay que decidir el siguiente paso hacia un mundo físicamente jugable usando la truth ya cerrada de Geography + Water + Climate + Environment + Human Roads.

La revisión debe decidir cómo encadenar las unidades futuras ya previstas —Bounded History / Present-Day Resolution, World Persistence, Sector Blueprint / Authored Composition, Large-Sector Navigation / Performance y Sector Materialization / Transition— sin abrir otra serie larga de foundations abstractas ni saltarse dependencias reales.

Fuera de alcance mientras no exista autorización específica:

- reabrir o retunear Macro Environment / Macro Climate sin un defecto real o unit explícito;
- vegetation/biomes locales, terrain materials, fauna, geology, final rivers o weather runtime;
- Bounded History, World Persistence, Sector Blueprint o materialización productiva antes de decidir su secuencia;
- ampliar nuevamente traversal/camera/debug ergonomics sin alcance explícito;
- M42.0 u otro milestone jugable por inercia;
- UI final, condition, repair, crafting, factions amplias o producción masiva de contenido.

M42.0 permanece planificado, pero su secuencia requiere rebaseline y no constituye el siguiente trabajo automático. Todo trabajo nuevo requiere autorización explícita.

## Connected First Playable

El Connected First Playable es la prueba integrada objetivo después de las foundations open-world y la materialización/continuidad necesaria. Debe demostrar A→B→A, continuidad cross-sector, mutaciones persistentes, save, full process exit y fresh load usando M32–M41.1 dentro del runtime canónico. No está iniciado, no es la vertical slice audiovisual final y no adelanta M45.1.

## Modding Y Provenance

La Global Content ID Foundation y la Minimum Content Source Identity & Provenance Foundation están validadas. Cada source requiere manifest `source_id`/`namespace`/`version`; ownership de declaraciones, orden estable y SHA-256 de recognized inputs están implementados sobre el pipeline Core/mod existente.

`Provenance` prueba qué fuentes/inputs estuvieron presentes. `Generation compatibility` continúa no implementada y será responsable de decidir si inputs semánticos siguen siendo compatibles con un mundo; no se infiere desde igualdad/diferencia del fingerprint.

Dependencies, overrides/patches y compatibilidad de producción permanecen en alcance posterior M50.0. Las foundations actuales no sustituyen M50.0 ni lo marcan iniciado.

## No Iniciar Todavía

- otro coding unit open-world sin la revisión de secuencia post-Environment;
- Terrain Materialization V1 productiva sin su contrato previo;
- weather runtime, seasons, final rivers, geology o ampliaciones de Climate/Environment fuera de un unit autorizado;
- nuevas ampliaciones OnGUI sin milestone autorizado;
- UI final;
- condition, repair o crafting;
- actores o mundo a escala fuera de las foundations implementadas;
- facciones amplias;
- vegetation/biomes locales, sectors jugables, transición, world history o gameplay world persistence general antes de sus units autorizadas;
- producción masiva de contenido.
