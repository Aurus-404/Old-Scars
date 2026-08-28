# Old Scars - Project Roadmap

## Autoridad Del Documento

Este archivo es la autoridad canonica para:

- IDs reservados y aliases historicos;
- estado actual de cada milestone;
- dependencias y orden de ejecucion;
- horizontes de produccion;
- nombres y ubicacion de los gates.

`Current_Milestone.md` resume este estado, `Next_Sprints.md` deriva la cola inmediata y `Development_Log.md` conserva la cronologia append-only. Ninguno de esos documentos puede reasignar IDs ni contradecir el estado publicado aqui.

Mauro conserva la autoridad creativa y de producto. [Game_Design_Document.md](Game_Design_Document.md) contiene el baseline de diseño revisado; el GDD Maestro v3.1 externo se conserva como fuente historica y de diseño auditada. [Technical_Architecture.md](Technical_Architecture.md) mantiene la autoridad sobre contratos tecnicos vigentes despues de contrastarlos con el codigo real. [Open_World_Architecture.md](Open_World_Architecture.md) define la dirección futura aprobada del mundo abierto sin presentarla como implementada. [Deformable_Terrain_Foundation.md](Deformable_Terrain_Foundation.md) fija la decisión de producto, evidencia y límites de terrain volumétrico alterable. [NPC_Sandbox_and_Equipment_Sequence.md](NPC_Sandbox_and_Equipment_Sequence.md) define la secuencia M41.2–M41.4 autorizada después del closeout volumétrico. Este roadmap no sustituye esas fuentes ni convierte implementaciones en diseño final.

## Estado De Produccion

| Campo | Estado canonico |
| --- | --- |
| Milestone/coding unit cerrado mas reciente | ID TBD — Deformable Volumetric Terrain Foundation / Technical Spike |
| Estado M37.0 | `DONE — PERSISTENCE CORE VALIDATED` |
| Ultimo milestone/coding unit funcional cerrado | ID TBD — Deformable Volumetric Terrain Foundation / Technical Spike |
| Estado M35.2 | `DONE — FUNCTIONAL SCOPE CLOSED AFTER M35.2.3` |
| Ultimo submilestone validado | M35.2.3 — Unified Corpse Belongings Surface |
| Commit funcional validado de M41.1 | `1c843961ed72b554f485b86105c443669337e8c0` |
| Commit documental de validacion de M41.1 | `2956bcae19719a5f9073e24d58da4705742732fa` |
| Commit validado de Integrated Gameplay Runtime / SampleScene Convergence | `8c485c78b4ab294de9d983f70ebadfba634ab3e1` |
| Commit validado de Macro Climate Baseline V1 | `457836e7f10a9b2ddbc08cc1db05ca38cd3f7108` |
| Commit final validado de Player Traversal / Camera & Runtime Debug Ergonomics Pass | `ab78da4fbb1af9189d6a5c178515fafdb56f368e` |
| Commit validado de Macro Environment / Biome Regions V1 | `55bcb0db479af43351f28908dfe05125dd9d62e1` |
| Commit técnico validado de Deformable Volumetric Terrain Foundation / Technical Spike | `d0309cf053be220a22151cae2dae9aca6f988e6f` |
| Commit de integración terrain + documentación en dev | `1b41ead829cd566c55df5adfc0522e33e1dffb96` |
| Milestone activo | M41.2 — Basic Equipment & Weapon Coverage V1 |
| Estado M41.2 | `AUTHORIZED — IMMEDIATE PRIORITY` |
| Estado M41.3 | `PLANNED — AFTER M41.2` |
| Estado M41.4 | `PLANNED — AFTER M41.3` |
| Estado ID TBD — Deformable Volumetric Terrain Foundation / Technical Spike | `VALIDATED — TECHNICAL SPIKE COMPLETE` |
| Estado ID TBD — Global Content ID Namespace Foundation | `VALIDATED — FOUNDATION COMPLETE` |
| Estado ID TBD — Minimum Content Source Identity & Provenance Foundation | `VALIDATED — FOUNDATION COMPLETE` |
| Estado ID TBD — World Identity, Topology & Determinism Foundation | `VALIDATED — FOUNDATION COMPLETE` |
| Estado ID TBD — World Session + Persistence V1 / New Game Save-Load Application Shell | `VALIDATED — APPLICATION SHELL COMPLETE` |
| Estado ID TBD — Macro World Plan V1 | `VALIDATED — FOUNDATION COMPLETE` |
| Estado ID TBD — Macro Elevation / Landforms V1 | `VALIDATED — FOUNDATION COMPLETE` |
| Estado ID TBD — Worldgen Gameplay Quality + Macro Water V1 | `VALIDATED — FOUNDATION COMPLETE` |
| Estado ID TBD — Worldgen Pass Isolation Correction | `VALIDATED — SYSTEMIC CORRECTION COMPLETE` |
| Estado ID TBD — Worldgen / World Session Observability Correction | `VALIDATED — OBSERVABILITY CORRECTION COMPLETE` |
| Estado ID TBD — Macro Human Geography / Road Network V1 | `VALIDATED — FOUNDATION COMPLETE` |
| Estado ID TBD — Terrain Materialization Technical Spike | `VALIDATED — TECHNICAL SPIKE COMPLETE` |
| Estado ID TBD — Integrated Gameplay Runtime / SampleScene Convergence | `VALIDATED — RUNTIME CONVERGENCE COMPLETE` |
| Estado ID TBD — Macro Climate Baseline V1 | `VALIDATED — FOUNDATION COMPLETE` |
| Estado ID TBD — Player Traversal / Camera & Runtime Debug Ergonomics Pass | `VALIDATED — RUNTIME ERGONOMICS COMPLETE` |
| Estado ID TBD — Macro Environment / Biome Regions V1 | `VALIDATED — FOUNDATION COMPLETE` |
| Estado M37.1 | `DONE — CURRENT SLICE PERSISTENCE VALIDATED` |
| Persistence Ready | `APPROVED` |
| Estado M40.1 | `DONE — ARMOR / PENETRATION V1 VALIDATED` |
| Combat Ready | `APPROVED` |
| AI Ready | `APPROVED` |
| Open World Rebaseline | `APPROVED DESIGN DIRECTION — PARTIALLY IMPLEMENTED FOUNDATIONS` |
| Siguientes | M41.2 → M41.3 → M41.4 → playtest/review |

M37.0 queda `DONE — PERSISTENCE CORE VALIDATED` y M37.1 queda `DONE — CURRENT SLICE PERSISTENCE VALIDATED`; `Persistence Ready` está `APPROVED`. Global Content ID Namespace, Minimum Content Source Identity/Provenance, World Identity/Topology/Determinism, Macro World Plan V1, Macro Elevation/Landforms V1, Worldgen Gameplay Quality/Macro Water V1, Macro Human Geography/Road Network V1, Macro Climate Baseline V1 y Macro Environment / Biome Regions V1 quedan `VALIDATED — FOUNDATION COMPLETE`; Worldgen Pass Isolation queda `VALIDATED — SYSTEMIC CORRECTION COMPLETE`; Worldgen/World Session Observability queda `VALIDATED — OBSERVABILITY CORRECTION COMPLETE`; World Session/New Game Save-Load queda `VALIDATED — APPLICATION SHELL COMPLETE`; Terrain Materialization Technical Spike queda `VALIDATED — TECHNICAL SPIKE COMPLETE`; Integrated Gameplay Runtime / SampleScene Convergence queda `VALIDATED — RUNTIME CONVERGENCE COMPLETE` en `8c485c78b4ab294de9d983f70ebadfba634ab3e1`; Macro Climate Baseline V1 queda validado y publicado en `457836e7f10a9b2ddbc08cc1db05ca38cd3f7108`; Player Traversal / Camera & Runtime Debug Ergonomics Pass queda `VALIDATED — RUNTIME ERGONOMICS COMPLETE` y publicado en `ab78da4fbb1af9189d6a5c178515fafdb56f368e`; Macro Environment / Biome Regions V1 queda validado y publicado en `55bcb0db479af43351f28908dfe05125dd9d62e1`. M38.0 queda `DONE — ACTOR RUNTIME & LIFECYCLE VALIDATED`, M38.1 queda `DONE — WORLD TIME / NEEDS / RECOVERY VALIDATED`, M39.0 queda `DONE — LOCALIZED HEALTH / MEDICINE VALIDATED`, M40.0 queda `DONE — COMBAT RESOLUTION & WEAPONS V1 VALIDATED`, M40.1 queda `DONE — ARMOR / PENETRATION V1 VALIDATED`, M41.0 queda `DONE — NAVIGATION / PERCEPTION FOUNDATION VALIDATED` y M41.1 queda `DONE — HUMAN ENCOUNTER AI V1 VALIDATED`. M41.1 tiene validation `AUTOMATED + MANUAL UNITY PASSED`; `Combat Ready` y `AI Ready` están `APPROVED`.

La revisión post-Environment queda resuelta: el requirement volumétrico se validó con un bounded shared density field chunked, Marching Tetrahedra, mesh/collider, cráter, túnel con techo, cross-chunk mutation, dirty rebuild localizado, persistencia/replay `SPIKE_NON_PRODUCTION`, player traversal y NavMesh local. El commit técnico es `d0309cf053be220a22151cae2dae9aca6f988e6f`, integrado en `dev` por `1b41ead829cd566c55df5adfc0522e33e1dffb96`. Unity Terrain conserva su evidencia como benchmark heightmap y futuras features no deben asumir que sea la representación productiva definitiva.

Tras ese cierre, la prioridad explícita pasa a la secuencia jugable `M41.2 → M41.3 → M41.4 → playtest/review`. M42.0–M47.1 conservan sus IDs y alcances planificados; su orden anterior sigue requiriendo reconciliación posterior sin renumeración ni reutilización silenciosa.

## Estados Canonicos

- `PLANNED`: alcance, dependencias y validacion propuestos; trabajo no iniciado.
- `IN PROGRESS`: trabajo autorizado y activo.
- `IMPLEMENTED`: alcance escrito o implementado y verificado estaticamente.
- `PENDING UNITY VALIDATION`: implementacion funcional terminada que requiere prueba manual en Unity.
- `VALIDATED`: prueba de aceptacion requerida ejecutada con evidencia explicita.
- `DONE`: milestone validado o documentalmente aceptado, con cierre y documentos coherentes.
- `DEFERRED`: retirado de la cola activa con motivo y trigger de retorno.
- `BLOCKED`: dependencia o decision externa impide continuar.
- `REJECTED`: excluido deliberadamente del producto o del alcance.

Los calificadores explican el estado sin crear una segunda taxonomia. `VALIDATED` nunca significa solamente que el proyecto compila.

## Reglas De Numeracion

1. Los commits, tags e IDs historicos no se renombran retrospectivamente.
2. Todo ID nuevo se reserva primero en este documento.
3. Una colision se registra en el ledger con nombre canonico, alias, evidencia y disposicion.
4. Un trabajo sin ID libre usa `ID TBD`; no reutiliza un numero historico.
5. Los sufijos legacy existentes se conservan como parte de la historia, pero no fijan la convención futura.
6. Los estados historicos inciertos no se elevan a `VALIDATED` por inferencia.

M41.2, M41.3 y M41.4 quedan reservados canónicamente por este rebaseline inmediato y no pueden reutilizarse para otro alcance. El terrain volumétrico cerrado conserva `ID TBD` como coding unit histórico interpuesto: no se renombra retrospectivamente ni consume un ID reservado posterior.

## Ledger Historico Y De Aliases

### Foundations Y Milestones Validados Antes De M28

Los detalles y la evidencia permanecen en `Development_Log.md`. Estos IDs y nombres quedan reservados y no pueden reutilizarse:

- `CoreDataSystem`, `ActionAvailabilitySystem`;
- M6, M7, M8, M9, M9.1, M10, M11, M12, M12.1, M13, M14, M15, M16, M17, M18;
- M19.1, M19.2, M20, M21, M21.0.1, M22, M22.1, M22.1.1, M22.1.2;
- M23, M23.0.1, M23.0.2, M23.0.3, M23.1, M23.1.1, M23.1.2;
- M24, M24.1, M24.2, M24.3, M24.4, M25, M26, M26.0.1 y M27.

Estado canonico del conjunto: `VALIDATED`, dentro del alcance debug/fundacional documentado para cada milestone.

### IDs Historicos Detectados En Git

| ID historico | Nombre/evidencia Git | Estado o relacion canonica | Disposicion |
| --- | --- | --- | --- |
| M28 | Add ground item drop pickup and restore container visuals — commit `7fb2671030d34fd69f79f7960adeddc65e6caf71` | `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED` | Se conserva el asunto historico del commit y el ID queda reservado. |
| M29 | Lee-Enfield firearm prototype — commit `6c4d6eca7ebf9234db24fbaa0c33f4242e6a965f` | `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED` | El commit demuestra implementacion, pero su cuerpo, el Development Log y las versiones historicas de Current no registran una prueba manual explicita ni una confirmacion de Mauro; el ID queda reservado sin elevar estado. |
| M30 | PSX Style implementation — commit `f756029` | `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED` | ID historico reservado. |
| M30.3 | Usable item prefab migration — commit `b86a616` | `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED` | ID historico reservado. |
| M30.4 | Safe crate editor visuals — commit `6ea3114` | `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED` | ID historico reservado. |
| M31.0 | Player visual and basic animator — commit `254392d` | `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED` | ID historico reservado. |
| M32.2 | Real Door System v0 — commit `a3ba9e5` | `VALIDATED` | `Development_Log.md` registra validacion manual confirmada; el snapshot anterior estaba stale. |
| M32.3 | House container variants — commit `92f57b3` | Alias historico de M32 | No se crea un segundo milestone funcional. |

### Colisiones Y Aliases Reconciliados

| Referencia anterior | Referencia canonica | Decision |
| --- | --- | --- |
| `Milestone 28: Container State / Naming Cleanup v0` planificado | `ID TBD — Container State / Naming Cleanup v0` | M28 ya pertenece al commit historico de ground item drop/pickup. El cleanup queda diferido y sin ID hasta una priorizacion futura. |
| `M32.3 — House Container Variants` en Git | `M32 — Debug Test House Kitchen Containers v0` en el ledger vivo | Se conserva M32.3 como alias de commit; no se renombra Git. |
| `M35.2.3 — Inventory Window Redesign Phase C1` | `M35.2.3 — Unified Corpse Belongings Surface` | `Inventory Window Redesign Phase C1` queda como alias funcional. |
| `M35.2.3.1` como continuacion de UI | `M35.2.3.1 — Universal Corpse Item Actions` | ID conservado, trabajo `DEFERRED — RECLASSIFIED` como futura necesidad transaccional cross-actor. |

## Ledger Funcional Reciente

| Milestone | Estado canonico | Nota |
| --- | --- | --- |
| M32 — Debug Test House Kitchen Containers v0 | `VALIDATED` | `Development_Log.md` registra validacion manual por confirmacion del usuario. |
| M32.2 — Real Door System v0 | `VALIDATED` | `Development_Log.md` registra validacion manual por confirmacion del usuario. |
| M32.4 — Interior Visibility Raycast v0 | `IMPLEMENTED — PENDING UNITY VALIDATION` | Incluido en el batch `420e087d512a24c50b1b8849205b818928649ff8`; no se eleva sin evidencia explicita reconciliada. |
| M32.4.1 — Door Pivot Repair + Interior Visibility Cast Debug/Stability | `IMPLEMENTED — PENDING UNITY VALIDATION` | Incluido en el batch `420e087d512a24c50b1b8849205b818928649ff8`; el cierre manual canonico sigue pendiente. |
| Grid Inventory Backend v0 | `IMPLEMENTED — PENDING UNITY VALIDATION` | ID legacy no numerado; fue incluido en el batch `420e087d512a24c50b1b8849205b818928649ff8`, pero no tiene cierre manual aislado. |
| M33.1, M33.1.1, M33.2, M33.2.1, M33.2.2, M33.3 y M33.3.1 | `VALIDATED` | Base espacial, UI debug y peso. |
| M34.1, M34.1.1, M34.1.2, M34.1.3, M34.2, M34.2.1, M34.2.1a, M34.2.1b y M34.2.1c | `VALIDATED` | Ownership, Equipment, storage item-owned y acciones unificadas. |
| M34.1.4 — Item Inspection Panel | `DEFERRED — RECLASSIFIED` | Retomar dentro de UI/UX de produccion si aporta informacion real. |
| M35.0 — Universal Visual Rig & Attachment Framework | `VALIDATED` | Pipeline visual data-driven y sincronizacion por commit. |
| M35.1 — Lootable Actor Real Equipment Bootstrap | `VALIDATED` | Equipment real inicializado desde Actor Profiles. |
| M35.2 — Lootable Entity Inventory UI V1 | `DONE — FUNCTIONAL SCOPE CLOSED AFTER M35.2.3` | El objetivo funcional se considera satisfecho por M35.2.1–M35.2.3. |
| M35.2.1 — Inventory Window Redesign Phase A | `VALIDATED` | Equipment ocupado y contextuales existentes. |
| M35.2.2 — Inventory Window Redesign Phase B | `VALIDATED` | Ventana flotante unica para item-owned storage. |
| M35.2.3 — Unified Corpse Belongings Surface | `VALIDATED` | Commit funcional `27bf438637b621141ca553a39579349a12ff8700`; cierre documental `2956bcae19719a5f9073e24d58da4705742732fa`. |
| M35.2.3.1 — Universal Corpse Item Actions | `DEFERRED — RECLASSIFIED` | Retomar solo si un loop posterior necesita transaccion cross-actor y despues de contratos de identidad/persistencia. |
| M35.2.4 — Persistent Body Review | `DEFERRED — RECLASSIFIED` | Significa reabrir un cuerpo vacio; no equivale a save/load. Retomar por necesidad jugable comprobada. |
| M35.2.5 — Multiple Floating Storage Windows | `DEFERRED — RECLASSIFIED` | Retomar en UI/UX de produccion si la investigacion de uso justifica multiples ventanas. |
| ID TBD — Container State / Naming Cleanup v0 | `DEFERRED — ID REQUIRED ON REACTIVATION` | Deuda de naming/tags legacy; no bloquea M36/M37. |
| ID TBD — Global Content ID Namespace Foundation | `VALIDATED — FOUNDATION COMPLETE` | Foundation actual: `namespace:local_id`, namespace `core`, identidad canónica de `GameDatabase`, migración Core, compatibilidad legacy temporal, normalización schema-v1 y diagnósticos. No implementa manifests, provenance completa, dependencies ni patches/load-order. El nombre queda reservado con `ID TBD`; no consume un número histórico. |
| ID TBD — Minimum Content Source Identity & Provenance Foundation | `VALIDATED — FOUNDATION COMPLETE` | Manifest mínimo por source, source ID/namespace/version, ownership de declaraciones, Core común, orden determinista, recognized inputs y SHA-256 de provenance por source/set. Excluye generation compatibility, dependencies, patches, world/save y version negotiation. |
| ID TBD — World Identity, Topology & Determinism Foundation | `VALIDATED — FOUNDATION COMPLETE` | `WorldId`, exact `WorldSeed`, generator context/version, SHA-256 scope/pass domains, `SectorId` y topology multiconexión conectada/canónica. Excluye session, persistence payload, menu, geography, history, materialización y compatibility. |
| ID TBD — World Session + Persistence V1 / New Game Save-Load Application Shell | `VALIDATED — APPLICATION SHELL COMPLETE` | Session lifecycle único, bootstrap determinista mínimo, `world_session_v1` hermano sobre M37, save catalog y flujo Main Menu/World Runtime validados hasta fresh process. No completa macro world plan, gameplay world persistence general, materialización ni compatibility. |
| ID TBD — Macro World Plan V1 | `VALIDATED — FOUNDATION COMPLETE` | Mundo finito con size preset/resolved settings durables, bounds macro, placements deterministas, topology derivada y evidencia canónica; integrado en New Game y `world_session_v1` schema 2 con compatibilidad legacy schema 1. Excluye elevation, landforms, geography, history, terrain y materialización. |
| ID TBD — Macro Elevation / Landforms V1 | `VALIDATED — FOUNDATION COMPLETE` | Campo mundial fixed-point compacto de elevation normalizada y regiones Plains/RollingHills/Highlands/Mountains; sampling global continuo, evidencia canónica, preview y persistencia `world_session_v1` schema 3. Schemas 1/2 permanecen legacy sin geography fabricada. Excluye terrain, hydrology/coastlines, climate, geology, biomes, history y materialización. |
| ID TBD — Worldgen Gameplay Quality + Macro Water V1 | `VALIDATED — FOUNDATION COMPLETE` | Water global committed con Land Coverage separado, ocean/coastline, conditioned drainage/basins, quality hard/soft y starter suitable; persistencia `world_session_v1` schema 4, schemas 1/2/3 legacy sin truth fabricada. Excluye climate, final rivers, terrain/materialización y runtime water simulation. |
| ID TBD — Worldgen Pass Isolation Correction | `VALIDATED — SYSTEMIC CORRECTION COMPLETE` | `GeneratorVersion` global queda como metadata y los passes poseen contratos deterministas separados. Restauró los outputs V1 de Plan/Geography y evita que evolución downstream re-seedee upstream truth. |
| ID TBD — Macro Human Geography / Road Network V1 | `VALIDATED — FOUNDATION COMPLETE` | Hubs Regional/Local y roads Primary/Secondary globales, routeadas por coste entero y separadas del MST/topology, committed en `world_session_v1` schema 5. Excluye settlements detallados, bridges, terrain/road materialization, navigation física, climate/history y runtime routing. |
| ID TBD — Terrain Materialization Technical Spike | `VALIDATED — TECHNICAL SPIKE COMPLETE` | Proyección local transient a Unity Terrain/Collider, water/road diagnostics, player existente y NavMesh terrestre local. Preserva sector≠tile, M37/hashes y escala unfrozen; queda como benchmark heightmap, no representación productiva definitiva. |
| ID TBD — Integrated Gameplay Runtime / SampleScene Convergence | `VALIDATED — RUNTIME CONVERGENCE COMPLETE` | Commit `8c485c78b4ab294de9d983f70ebadfba634ab3e1`: WorldRuntime canónico y SampleScene laboratorio reutilizan la misma composición gameplay; worldgen aporta terrain/world truth sin reemplazar Inventory, Health, Needs, Interaction, Combat ni Persistence. Fixture M32 sólo development; identidad/persistencia/current-slice y regresiones M37–M41/worldgen validadas. |
| ID TBD — Macro Climate Baseline V1 | `VALIDATED — FOUNDATION COMPLETE` | Commit `457836e7f10a9b2ddbc08cc1db05ca38cd3f7108`: truth mundial `ThermalIndex`/`MoistureIndex` bajo `macro_climate_v1`, dirección dominante persistida, schema 6 exacto y schemas 1–5 legacy sin Climate fabricado. Excluye weather runtime, vegetation, geology, final rivers y materialización local. |
| ID TBD — Player Traversal / Camera & Runtime Debug Ergonomics Pass | `VALIDATED — RUNTIME ERGONOMICS COMPLETE` | Commit final `ab78da4fbb1af9189d6a5c178515fafdb56f368e`: cámara follow-only con yaw/pitch/zoom/collision, sprint + stamina integrada con Needs y Current Slice, panel F3 development-only con ScrollView/time presets/teleport; Player Controls D3D11, M38 y WorldRuntime/session Play Mode `PASS`. |
| ID TBD — Macro Environment / Biome Regions V1 | `VALIDATED — FOUNDATION COMPLETE` | Commit `55bcb0db479af43351f28908dfe05125dd9d62e1`: `macro_environment_v1`, 14 familias terrestres + None, Primary/Secondary/TransitionQ16, océano None/None/0, schema 7 exacto y schemas 1–6 legacy sin Environment fabricado. Excluye vegetation, fauna, geology, terrain materials, weather, region IDs y materialización local. |
| ID TBD — Deformable Volumetric Terrain Foundation / Technical Spike | `VALIDATED — TECHNICAL SPIKE COMPLETE` | Commit técnico `d0309cf053be220a22151cae2dae9aca6f988e6f`, integrado en `dev` por `1b41ead829cd566c55df5adfc0522e33e1dffb96`: density field chunked, Marching Tetrahedra, mesh/collider, crater, roofed tunnel, cross-chunk mutation, dirty rebuild, persistence/replay spike, player traversal y NavMesh local; world_session schema 7 y goldens preservados. |
| M41.2 — Basic Equipment & Weapon Coverage V1 | `AUTHORIZED — IMMEDIATE PRIORITY` | Coverage funcional de los 17 slots reales, varias mochilas y bolt/semi/automatic usando autoridades existentes; rangos hitscan/melee explícitos y debug clampado. No arte final ni NPC sandbox todavía. |
| M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1 | `PLANNED — AFTER M41.2` | Spawn development-only en WorldRuntime, loadouts probabilísticos JSON con `none`, roaming básico, localized damage/death y corpse con pertenencias reales sin reroll. |
| M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1 | `PLANNED — AFTER M41.3` | Blue/Red debug sobre affiliation genérica, automatic threat acquisition por perception/LOS, cierre de distancia por arma y aim físicamente imperfecto sin aimbot. |

## Open World Rebaseline — Dirección Futura Aprobada

[Open_World_Architecture.md](Open_World_Architecture.md) congela la dirección de producto/arquitectura futura con estado `APPROVED DESIGN DIRECTION — NOT IMPLEMENTED` para las capas que aún no existen. [Deformable_Terrain_Foundation.md](Deformable_Terrain_Foundation.md) añade el requisito aprobado y la evidencia validada de terrain productivo volumétrico/deformable.

Camino conceptual propuesto:

| Orden | Unidad | Estado | Dependencia conceptual | Resultado esperado |
| --- | --- | --- | --- | --- |
| 1 | ID TBD — Minimum Content Source Identity & Provenance Foundation | `VALIDATED — FOUNDATION COMPLETE` | Global Content ID Foundation validada | Fuentes/versiones/inputs identificables mediante el pipeline Core/mod existente, sin implementar el alcance completo M50.0. |
| 2 | ID TBD — World Identity, Topology & Determinism | `VALIDATED — FOUNDATION COMPLETE` | Unidad 1 | Contratos lógicos mínimos de mundo/sector/topología y determinismo validados sin GameObjects, pose ni materialización. |
| Bridge | ID TBD — World Session + Persistence V1 / New Game Save-Load Application Shell | `VALIDATED — APPLICATION SHELL COMPLETE` | Unidad 2; M37 validado | Lifecycle y persistencia mínima de identity/topology/provenance evidence, sin completar la futura World Persistence general. |
| 3 | ID TBD — Macro World Plan V1 | `VALIDATED — FOUNDATION COMPLETE` | Unidad 2; application shell validada | Settings/tamaños durables, bounds finitos, placements, topology derivada y persistence schema 2 sin geography/materialización. |
| 4 | ID TBD — Macro Elevation / Landforms V1 | `VALIDATED — FOUNDATION COMPLETE` | Unidad 3 | Campo de elevation y landforms regionales globales, committed y consultable sin terrain/materialización local. |
| 5 | ID TBD — Worldgen Gameplay Quality + Macro Water V1 | `VALIDATED — FOUNDATION COMPLETE` | Unidades 3–4 | Water/coastline truth global committed, quality hard/soft y starter suitable sin terrain, rivers ni runtime simulation. |
| Correction | ID TBD — Worldgen Pass Isolation Correction | `VALIDATED — SYSTEMIC CORRECTION COMPLETE` | Unidad 5 | Versión global separada de contratos deterministas por pass antes de sumar Climate/Environment. |
| Correction | ID TBD — Worldgen / World Session Observability Correction | `VALIDATED — OBSERVABILITY CORRECTION COMPLETE` | Pass Isolation y application shell | Eventos únicos Create/Load/Runtime Ready/Save con evidence ya existente; sin cambio de generation, schema o runtime simulation. |
| Infrastructure | ID TBD — Macro Human Geography / Road Network V1 | `VALIDATED — FOUNDATION COMPLETE` | Unidades 3–5 | Red mundial lógica committed que consume truth macro sin convertir el MST scaffold en physical adjacency/travel graph ni materializar roads. |
| Spike | ID TBD — Terrain Materialization Technical Spike | `VALIDATED — TECHNICAL SPIKE COMPLETE` | Macro Geography/Water/Human Geography validadas | Conversión local medida a Unity Terrain/Collider, water/roads diagnósticos y NavMesh local; benchmark heightmap que no congela representación productiva. |
| Integration | ID TBD — Integrated Gameplay Runtime / SampleScene Convergence | `VALIDATED — RUNTIME CONVERGENCE COMPLETE` | Terrain spike; M32–M41.1; World Session/Current Slice | WorldRuntime canónico ejecuta el gameplay compartido sobre worldgen; SampleScene queda laboratorio y no existe una autoridad gameplay paralela. |
| 6 | ID TBD — Macro Climate Baseline V1 | `VALIDATED — FOUNDATION COMPLETE` | Unidades 4–5; Pass Isolation; integración runtime cerrada | Truth mundial determinista de baseline térmica y humedad climática de largo plazo, persistida en schema 6; no weather runtime, vegetation ni materialización local. |
| 6b | ID TBD — Macro Environment / Biome Regions V1 | `VALIDATED — FOUNDATION COMPLETE` | Unidad 6; macro truth validada | Regiones environment/biome globales derivadas de Climate + Water, con Landform separado, persistidas en schema 7; sin vegetation/materiales finales ni region IDs. |
| Spike volumétrico | ID TBD — Deformable Volumetric Terrain Foundation / Technical Spike | `VALIDATED — TECHNICAL SPIKE COMPLETE` | Macro truth + WorldRuntime + Terrain spike existentes | Density field local chunked, Marching Tetrahedra, mesh/collider, mutación 3D, replay persistente spike, player traversal y NavMesh local demostrados. |
| Tramo jugable activo | M41.2 — Basic Equipment & Weapon Coverage V1 | `AUTHORIZED — IMMEDIATE PRIORITY` | M34/M35 equipment; M37 Current Slice; M40 combat; terrain volumétrico cerrado | Coverage funcional de equipment/storage/firearms para alimentar pruebas reales dentro de WorldRuntime. |
| Después | M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1 | `PLANNED — AFTER M41.2` | M41.2; M38/M39/M41.0 | NPCs spawneables con loadout probabilístico real, roaming, damage/death/corpse loot. |
| Después | M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1 | `PLANNED — AFTER M41.3` | M41.3; M41.1; M40 | Hostility debug genérica, automatic threat acquisition, engagement por rango y aim imperfecto observable. |
| Review | Playtest / Review M41.2–M41.4 | `PLANNED — REQUIRED BEFORE NEXT LARGE SYSTEM` | M41.4 | Jugar/observar varios NPC simultáneos y rebaselinear desde bugs/game feel reales. |
| 7 | ID TBD — Bounded History & Present-Day Resolution | `PLANNED — NOT AUTHORIZED` | Macro truth y geography/features requeridos | Historia estructurada acotada que produce/explica estado presente real sin event-sourced persistence. |
| 8 | ID TBD — World Persistence | `PLANNED — NOT AUTHORIZED` | Unidades 1–7; M37 validado | Persistencia de geography, history, sector blueprints y gameplay world state general que reutiliza garantías M37 y deja `current_slice_v1` intacto. |
| 9 | ID TBD — Sector Blueprint & Authored Composition | `PLANNED — NOT AUTHORIZED` | Unidades 5–8 | Blueprint local validable y composición de estructuras/sitios autorados mediante autoridades existentes. |
| 10 | ID TBD — Large-Sector Navigation & Performance Gate | `PLANNED — NOT AUTHORIZED` | Unidad 2; antes de materialización productiva | Spike medido de navegación/particiones/lifecycle sin reemplazar M41.0; debe incorporar evidencia de terrain deformable. |
| 11 | ID TBD — Sector Materialization & Transition | `PLANNED — NOT AUTHORIZED` | Unidades 8–10 + terrain representation validada | Un sector autoritativo activo, staging inerte y transición runtime recuperable sin imponer autosave por frontera ni depender de una heightmap incapaz de mutaciones volumétricas. |
| 12 | ID TBD — Connected First Playable | `PLANNED — NOT AUTHORIZED` | Unidades 1–11; M32–M41.4 según alcance vigente | Prueba integrada A→B→A, mutaciones, save, full exit y fresh load; no vertical slice audiovisual final. |
| 13 | ID TBD — Open World Playtest & Roadmap Rebaseline | `PLANNED — NOT AUTHORIZED` | Unidad 12 | Evidencia para reordenar sistemas posteriores y fijar gates/budgets reales. |

Esta tabla no autoriza sistemas posteriores por inercia. La dirección aprobada de terrain tampoco obliga todavía a un algoritmo productivo final, resolución, chunk size, LOD, geología, mining loop o dinámica de fluidos. Marching Tetrahedra queda como método validado del spike, no como dogma permanente.

## Roadmap Estrategico Desde M36

Los IDs siguientes quedan reservados por M36.0 o por rebaseline posterior explícito. No expresan fechas ni autorizan implementacion por si solos.

| Horizonte | Milestone | Tipo | Estado | Dependencias | Resultado / gate |
| --- | --- | --- | --- | --- | --- |
| CERRADO | M36.0 — Old Scars Strategic Production Roadmap Rebaseline | Gobernanza | `DONE — DOCUMENTATION REVIEWED` | M35.2 cerrado | Checkpoints A/B y Documentation Review Correction Pass 1 revisados y aprobados por Mauro; Unity validation `NOT APPLICABLE`. |
| CERRADO | M36.1 — Foundation Freeze & Persistent Identity Contract | Arquitectura | `DONE — FOUNDATION FREEZE APPROVED` | M36.0 revisado | Identidad durable/authored, ownership, rollback, stack granularity y politica exacta de `Condition` congelados con evidencia automatizada y manual; no implemento save/load. |
| CERRADO | M37.0 — Save Format & Persistence Core | Arquitectura | `DONE — PERSISTENCE CORE VALIDATED` | M36.1 | Envelope V1, serializacion, safe write, backup/recovery, version policy y migration seam validados sin integrar estado gameplay. |
| CERRADO | ID TBD — Global Content ID Namespace Foundation | Arquitectura/datos | `VALIDATED — FOUNDATION COMPLETE` | M37.0 | `ContentId`, namespace `core`, referencias globales canónicas, compatibilidad legacy acotada, normalización schema-v1 y diagnósticos. Excluye manifests, provenance completa, dependencies ni patches/load-order. |
| CERRADO | M37.1 — Current Slice Persistent Round-Trip | Arquitectura/jugable | `DONE — CURRENT SLICE PERSISTENCE VALIDATED` | M37.0; compatibilidad schema-v1 Core validada | Snapshot, apply transaccional, rollback, diagnóstico y round-trip fresh-session manual validados; `Persistence Ready` aprobado para el Current Slice. |
| CERRADO | M38.0 — Actor Runtime & Lifecycle V1 | Arquitectura/jugable | `DONE — ACTOR RUNTIME & LIFECYCLE VALIDATED` | ID TBD y M37.1 cerrados | Identidad durable, Alive/Dead, corpse continuity, spawn/restore runtime y persistencia fresh-session validados con automatización y evidencia manual. |
| CERRADO | M38.1 — Needs, World Clock & Recovery V1 | Jugable | `DONE — WORLD TIME / NEEDS / RECOVERY VALIDATED` | M38.0 | Autoridad temporal, Hunger/Thirst por game time, rest/sleep, persistence/rollback y diagnostics validados; fatigue queda deferred SHOULD. |
| CERRADO | M39.0 — Localized Health & Medicine V1 | Jugable | `DONE — LOCALIZED HEALTH / MEDICINE VALIDATED` | M38.1 | Seis regiones, heridas durables, bleeding por WorldClock, pain, venda localizada, UI H y persistence V1 validados con automatización y fresh-session manual. |
| CERRADO | M40.0 — Combat Resolution & Weapons V1 | Jugable | `DONE — COMBAT RESOLUTION & WEAPONS V1 VALIDATED` | M39.0 | Resolver único hacia M39, melee/firearms, estado cargado por instancia, ammo/reload, near-cover blocking y persistence validados con automatización y fresh-session manual. |
| CERRADO | M40.1 — Armor & Penetration V1 | Jugable | `DONE — ARMOR / PENETRATION V1 VALIDATED` | M40.0 | Cobertura regional equipped-only, núcleo común de penetración para armor/world, trauma residual y round-trip fresh-session validados; gate `Combat Ready` aprobado. |
| CERRADO | M41.0 — Navigation & Perception Foundation | Arquitectura/jugable | `DONE — NAVIGATION / PERCEPTION FOUNDATION VALIDATED` | M38.0 | Navigation NPC y perception visual separadas, data-driven y validadas con automatización y prueba manual Unity. |
| CERRADO | M41.1 — Human Encounter AI V1 | Jugable | `DONE — HUMAN ENCOUNTER AI V1 VALIDATED` | M40.0, M41.0 | Avoid, Alerted, Flee, Fight y LostContact acotados, data-driven y validados; gate `AI Ready` aprobado. |
| CERRADO | ID TBD — Minimum Content Source Identity & Provenance Foundation | Arquitectura/datos | `VALIDATED — FOUNDATION COMPLETE` | Global Content ID Foundation; Open World Rebaseline | Manifest compartido Core/mod, ownership/orden estable y provenance SHA-256 validados sin generation compatibility, world code ni persistencia. |
| CERRADO | ID TBD — World Identity, Topology & Determinism Foundation | Arquitectura/datos | `VALIDATED — FOUNDATION COMPLETE` | Content Source Identity/Provenance; Open World Rebaseline | IDs mundo/sector separados, seed/version context, domain derivation SHA-256 y topology conectada multiconexión validados sin Unity world, save ni geometry. |
| CERRADO | ID TBD — World Session + Persistence V1 / New Game Save-Load Application Shell | Arquitectura/aplicación | `VALIDATED — APPLICATION SHELL COMPLETE` | World Identity/Topology/Determinism; Content Provenance; M37 | Session lifecycle, `world_session_v1`, bootstrap mínimo, catalog, Main Menu/World Runtime y fresh-process load validados; no macro worldgen ni gameplay world state general. |
| CERRADO | ID TBD — Macro World Plan V1 | Arquitectura/worldgen | `VALIDATED — FOUNDATION COMPLETE` | World Identity/Topology/Determinism; application shell | Mundo finito completo con size/settings resueltos, bounds, placements y topology persistidos; sin geography local/materialización. |
| CERRADO | ID TBD — Macro Elevation / Landforms V1 | Arquitectura/worldgen | `VALIDATED — FOUNDATION COMPLETE` | Macro World Plan V1; application shell | Campo global fixed-point de elevation/landforms committed, consultable y persistido schema 3; sin terrain, hydrology, climate ni materialización. |
| CERRADO | ID TBD — Worldgen Gameplay Quality + Macro Water V1 | Arquitectura/worldgen | `VALIDATED — FOUNDATION COMPLETE` | Macro Elevation/Landforms V1; application shell | Water/coastline/drainage truth global, analysis quality y starter suitable persistidos schema 4; sin climate, rivers, terrain ni runtime simulation. |
| CERRADO | ID TBD — Worldgen Pass Isolation Correction | Arquitectura/worldgen | `VALIDATED — SYSTEMIC CORRECTION COMPLETE` | Gameplay Quality + Macro Water V1 | Contratos deterministas separados por pass y pipeline global sólo metadata; Plan/Geography goldens V1 restaurados sin regeneración legacy. |
| CERRADO | ID TBD — Worldgen / World Session Observability Correction | Arquitectura/diagnóstico | `VALIDATED — OBSERVABILITY CORRECTION COMPLETE` | Pass Isolation; World Session shell | Lifecycle Create/Load/Runtime Ready/Save observable con cardinalidad estable, truth legacy explícita y `WRITE_COMMIT` preservado. |
| CERRADO | ID TBD — Macro Human Geography / Road Network V1 | Arquitectura/worldgen | `VALIDATED — FOUNDATION COMPLETE` | Macro Geography; Macro Water/quality; Pass Isolation | Hubs y red vial macro committed/global, multi-landmass, routeada por coste, persistida schema 5 y observable; sin settlements, roads físicas, terrain ni routing runtime. |
| CERRADO | ID TBD — Terrain Materialization Technical Spike | Arquitectura/worldgen | `VALIDATED — TECHNICAL SPIKE COMPLETE` | Macro Geography/Water/Human Geography | Ventana local Unity Terrain/Collider + NavMesh y overlays diagnósticos; benchmark heightmap, no representación productiva definitiva. |
| CERRADO | ID TBD — Integrated Gameplay Runtime / SampleScene Convergence | Arquitectura/jugable | `VALIDATED — RUNTIME CONVERGENCE COMPLETE` | Terrain spike; M32–M41.1; World Session/Current Slice | WorldRuntime canónico y SampleScene laboratorio comparten gameplay real sobre worldgen; commit `8c485c78b4ab294de9d983f70ebadfba634ab3e1`. |
| CERRADO | ID TBD — Macro Climate Baseline V1 | Arquitectura/worldgen | `VALIDATED — FOUNDATION COMPLETE` | Macro Geography/Water; Pass Isolation; runtime convergence | Baseline térmica/humedad climática committed bajo `macro_climate_v1`, schema 6 y legacy 1–5 exactos; commit `457836e7f10a9b2ddbc08cc1db05ca38cd3f7108`. |
| CERRADO | ID TBD — Player Traversal / Camera & Runtime Debug Ergonomics Pass | Jugable/herramientas | `VALIDATED — RUNTIME ERGONOMICS COMPLETE` | Runtime convergence; M38.1; Current Slice | Cámara player-centric con pitch/collision, sprint+stamina integrada con Needs/persistence y Runtime Debug Tools development-only; commit final `ab78da4fbb1af9189d6a5c178515fafdb56f368e`. |
| CERRADO | ID TBD — Macro Environment / Biome Regions V1 | Arquitectura/worldgen | `VALIDATED — FOUNDATION COMPLETE` | Macro Climate Baseline V1; macro truth validada | `macro_environment_v1`, 14 familias + None, ecotone Primary/Secondary/Transition, schema 7 y legacy exacto; commit `55bcb0db479af43351f28908dfe05125dd9d62e1`. |
| CERRADO | ID TBD — Deformable Volumetric Terrain Foundation / Technical Spike | Arquitectura/world/terrain | `VALIDATED — TECHNICAL SPIKE COMPLETE` | Macro Environment; Terrain spike; WorldRuntime | Density field chunked + Marching Tetrahedra, mesh/collider, crater/tunnel, dirty rebuild, persistence spike, player traversal y NavMesh local; commit `d0309cf053be220a22151cae2dae9aca6f988e6f`. |
| AHORA | M41.2 — Basic Equipment & Weapon Coverage V1 | Jugable/contenido | `AUTHORIZED — IMMEDIATE PRIORITY` | M34/M35; M37.1; M40/M40.1; terrain volumétrico cerrado | Cubrir 17 slots reales, varias mochilas, bolt/semi/automatic y range debug coherente sin arte final ni sistemas paralelos. |
| DESPUÉS | M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1 | Jugable/AI sandbox | `PLANNED — AFTER M41.2` | M41.2; M38/M39/M41.0 | Spawn NPC real, loadouts probabilísticos JSON, roaming, localized health/death y corpse loot exacto. |
| DESPUÉS | M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1 | Jugable/AI combat | `PLANNED — AFTER M41.3` | M41.3; M41.1; M40/M40.1 | Blue/Red debug sobre affiliation genérica, threat acquisition, engagement por rango y aim imperfecto físico/observable. |
| REVIEW | Playtest / Review M41.2–M41.4 | Jugable/QA | `PLANNED — REQUIRED` | M41.4 | No autorizar automáticamente otro sistema grande; evaluar bugs, navegación, ownership, combat y game feel reales. |
| RESECUENCIAR | M42.0 — Weather, Exposure & Environment V1 | Jugable | `PLANNED — SEQUENCE REBASELINE REQUIRED` | M38.1; futura world/sector foundation aplicable | Clima dinámico, forecast, exposicion y proteccion permanecen planificados; no equivalen al Macro Climate Baseline y ya no son el siguiente trabajo automático. |
| RESECUENCIAR | M42.1 — Food, Water, Animals & Ecology V1 | Jugable | `PLANNED — SEQUENCE REBASELINE REQUIRED` | M42.0; M41.0 para animales moviles; world/sector context aplicable | Calidad, purificacion, deterioro y animales acotados; gate `World Systems Ready` pendiente de reubicación revisada. |
| RESECUENCIAR | M43.0 — Condition, Repair & Disassembly V1 | Jugable | `PLANNED — SEQUENCE REBASELINE REQUIRED` | M37.1 | Condition mutable, reparacion y desmontaje preservando identidad; prioridad final posterior al playtest open-world. |
| RESECUENCIAR | M43.1 — Bounded Crafting & Workstations V1 | Jugable | `PLANNED — SEQUENCE REBASELINE REQUIRED` | M43.0 | Recetas cerradas y estaciones limitadas; prioridad final posterior al playtest open-world. |
| RESECUENCIAR | M44.0 — Skills & Long-Term Progression V1 | Jugable | `PLANNED — SEQUENCE REBASELINE REQUIRED` | M39.0, M40.1, M41.1, M42.1, M43.1 | Competencias que habilitan opciones, sin grind. |
| RESECUENCIAR | M44.1 — Shelter & Recovery Progression V1 | Jugable | `PLANNED — SEQUENCE REBASELINE REQUIRED` | M38.1, M39.0, M42.1, M43.1, M44.0 | Refugio funcional y recuperacion; gate `Survival Systems Ready` pendiente de reubicación revisada. |
| RESECUENCIAR | M45.0 — Content Tools & World Sectorization | Herramientas/arquitectura | `PLANNED — SCOPE REBASELINE REQUIRED` | M37.1; open-world foundations ID TBD | El ID y scope histórico se conservan; la sectorización fundamental migra al nuevo camino ID TBD y el tooling/gate restante debe reconciliarse antes de autorización. |
| RESECUENCIAR | M45.1 — Old Scars Vertical Slice Candidate: La estacion de bombeo | Contenido/jugable | `PLANNED — CANDIDATE, NOT NARRATIVE CANON; SEQUENCE REBASELINE REQUIRED` | Connected First Playable y systems/content foundations revisados | Vertical slice audiovisual posterior; no sustituye el Connected First Playable. |
| FUTURO / RESECUENCIAR | M46.0 — Settlements, Trade & Patrimonial Value | Jugable/contenido | `PLANNED — SEQUENCE REBASELINE REQUIRED` | M45.1 o dependencia posterior revisada | Asentamientos y economia material acotada. |
| FUTURO / RESECUENCIAR | M46.1 — Faction Identity, Disposition & Memory V1 | Jugable/contenido | `PLANNED — SEQUENCE REBASELINE REQUIRED` | M41.4, M46.0 o dependencia posterior revisada | MUST limitado a identidad, disposicion y memoria minima; M41.4 sólo provee una affiliation/disposition sandbox mínima y no sustituye este milestone. |
| FUTURO / RESECUENCIAR | M47.0 — Controlled Secondary World Variation V1 | Arquitectura/herramientas | `PLANNED — SCOPE REBASELINE REQUIRED` | Open-world foundations; dependencies posteriores revisadas | Variación secundaria posterior permanece distinta del worldgen macro fundamental ahora requerido. |
| FUTURO / RESECUENCIAR | M47.1 — Narrative, Events & Objectives V1 | Contenido/jugable | `PLANNED — SCOPE REBASELINE REQUIRED` | M46.1, M47.0 o dependencies posteriores revisadas | Eventos/objetivos autorales permanecen distintos de la historia causal de generación. |
| FUTURO | M48.0 — Production UI/UX & Accessibility | Produccion | `PLANNED` | M45.1, M47.1 | UI de produccion sin reescribir backends. |
| FUTURO | M48.1 — Art, Animation & Audio Production Pipeline | Produccion/herramientas | `PLANNED` | M45.1 | Pipeline repetible con budgets. |
| FUTURO | M49.0 — Content Production & Optimization | Contenido/produccion | `PLANNED` | M47.1, M48.0, M48.1 | Contenido a escala sin sistemas nuevos. |
| FUTURO | M50.0 — Modding & Data Compatibility V1 | Arquitectura/produccion | `PLANNED` | M37.1, M45.0, M49.0 | Manifests, versiones, dependencias, overrides y compatibilidad; gate `Production Ready`. |
| FUTURO | M51.0 — Alpha | Produccion | `PLANNED` | Production Ready | Feature complete y recorrido de inicio a fin; gate `Alpha`. |
| POSTERIOR AL ALPHA | M52.0 — Content Complete | Contenido/produccion | `PLANNED` | Alpha | Contenido de lanzamiento integrado; gate `Content Complete`. |
| POSTERIOR AL ALPHA | M53.0 — Beta | Produccion | `PLANNED` | Content Complete | Feature/content lock, estabilidad y balance; gate `Beta`. |
| POSTERIOR AL ALPHA | M54.0 — Release Candidate | Produccion | `PLANNED` | Beta | Build publicable y recuperable; gate `Release Candidate`. |
| POSTERIOR AL ALPHA | M55.0 — Launch | Produccion | `PLANNED` | Release Candidate | Lanzamiento, soporte inicial y rollback operativo. |

## Gates Canonicos

Este archivo es autoridad sobre los nombres y la ubicacion de los gates. Sus criterios detallados se desarrollan en [Production_Gates_and_Risks.md](Production_Gates_and_Risks.md).

Estado vigente: `Foundation Freeze — APPROVED` en M36.1; `Persistence Ready — APPROVED` para el Current Slice validado en M37.1; `Combat Ready — APPROVED` en M40.1; `AI Ready — APPROVED` en M41.1.

Los gates ya aprobados no se reabren. Las ubicaciones M42.1 en adelante conservan la reserva histórica del roadmap, pero su orden y milestone de cierre requieren reconciliación después del Connected First Playable; no autorizan iniciar esos milestones en la secuencia anterior.

| Gate | Cierre previsto |
| --- | --- |
| Foundation Freeze | M36.1 |
| Persistence Ready | M37.1 |
| Combat Ready | M40.1 |
| AI Ready | M41.1 |
| World Systems Ready | M42.1 |
| Survival Systems Ready | M44.1 |
| Content Pipeline Ready | M45.0 |
| Vertical Slice Approved | M45.1 |
| Production Ready | M50.0 |
| Alpha | M51.0 |
| Content Complete | M52.0 |
| Beta | M53.0 |
| Release Candidate | M54.0 |

## Dependencias Y Camino Critico

Camino cerrado vigente:

`M36.0 → M36.1 → M37.0 → (ID TBD validado + M37.1 cerrado) → Persistence Ready APPROVED → M38.0 DONE → M38.1 DONE → M39.0 DONE → M40.0 DONE → M40.1 DONE → Combat Ready APPROVED → M41.0 DONE → M41.1 DONE → AI Ready APPROVED → open-world foundations validadas → Terrain Materialization Technical Spike VALIDATED → Integrated Gameplay Runtime / SampleScene Convergence VALIDATED → Macro Climate Baseline V1 VALIDATED → Macro Environment / Biome Regions V1 VALIDATED → Deformable Volumetric Terrain Foundation / Technical Spike VALIDATED`

Cierre de soporte runtime posterior/intercalado: `Player Traversal / Camera & Runtime Debug Ergonomics Pass VALIDATED`.

Camino crítico inmediato autorizado:

`M41.2 Basic Equipment & Weapon Coverage V1 → M41.3 NPC Sandbox Spawn & Randomized Loadouts V1 → M41.4 Affiliation, Range-Aware Combat & Imperfect Aim V1 → Playtest / Review`

Después de ese tramo, el camino open-world grande vuelve a revisión explícita antes de Bounded History / World Persistence / Sector Blueprint / Large-Sector Navigation / Sector Materialization. No se autoriza continuar esas foundations por inercia.

Dependencias de produccion:

- persistencia antes de escalar actores, NPCs o mundo;
- actor lifecycle antes de heridas, necesidades complejas e IA;
- world clock antes de clima dinámico, deterioro y reconciliación temporal;
- condition antes de reparacion; reparacion/desmontaje antes de crafting;
- source provenance y generation compatibility antes de comprometer compatibilidad de mundos persistentes;
- macro truth y cross-sector networks antes de resolver blueprints vecinos;
- terrain volumétrico/deformable validado antes de materialización sectorial productiva que deba soportar cavado/explosiones/túneles;
- world persistence general antes de comprometer mutaciones sectoriales de producción;
- navegación/rendimiento deben medirse contra la representación deformable real, no solamente contra Unity Terrain;
- M41.2 provee coverage de equipment/firearms antes de generar NPC loadouts M41.3;
- M41.3 provee actores/loadouts reales antes de affiliation y combat sandbox M41.4;
- M41.4 reutiliza M41.1/M40/M41.0 y no crea un faction/AI stack paralelo;
- sector materialization/transition antes del Connected First Playable;
- tools y validators antes de contenido masivo;
- economia material antes de comercio;
- save, actores y comercio antes de consecuencias regionales;
- toda Definition global nueva debe usar `ContentId` canónico; no se permite volver a IDs globales simples durante los milestones intermedios.

## Alcance Inmediato

### M41.2 — Basic Equipment & Weapon Coverage V1

Estado: `AUTHORIZED — IMMEDIATE PRIORITY`.

Autoridad de alcance: [NPC_Sandbox_and_Equipment_Sequence.md](NPC_Sandbox_and_Equipment_Sequence.md).

M41.2 debe usar el layout `core:human_standard_01` cargado como autoridad y cubrir sus 17 slots reales con contenido funcional suficiente para ejercitar Equipment. Debe agregar varias mochilas con diferencias reales usando el backend item-owned storage existente y ampliar la cobertura firearm desde el Lee-Enfield bolt-action hacia al menos un semi-automatic y un automatic mediante comportamiento genérico/data-driven si el backend actual lo requiere.

El alcance temporal de armas debe quedar coherente: `firearm.range` limita físicamente el hitscan actual, `melee_range` limita melee y el aim/trace debug no debe sugerir que un collider más allá de ese alcance puede recibir daño. La cámara puede seguir encontrando una dirección lejana bajo el mouse, pero la solución física/visual alcanzable se clampa al arma.

M41.2 no implementa NPC spawn/loadouts probabilísticos, affiliation, imperfect AI aim, full ballistics, modelos/iconos/animaciones finales, condition/repair/crafting ni producción masiva de contenido. Debe preservar `WeaponCombatService`, `ActorEquipmentComponent`, item-owned storage, ownership, `ItemInstance` y Current Slice como autoridades existentes.

### Después — M41.3 Y M41.4

M41.3 convierte ese coverage en NPCs spawneables con loadouts probabilísticos reales, roaming, localized damage/death y corpse loot exacto. M41.4 agrega la capa mínima de affiliation/disposition, automatic threat acquisition, range-aware combat e imperfect physical aim sobre M41.1/M41.0/M40 existentes. Después se exige un playtest/review antes de autorizar otro sistema grande.

### M36.0 — Checkpoint A

Reconciliar autoridad documental, ledger historico, estados, dependencias, gates y cola inmediata. No cambia codigo ni contenido gameplay.

### M36.0 — Checkpoint B

Alinear el baseline de diseño revisado, arquitectura, JSON rules, reglas de desarrollo, template, gates y riesgos. El GDD Maestro v3.1 se conserva intacto como fuente historica auditada; las decisiones ambiguas no se resuelven por inferencia.

### M36.0 — Documentation Review Correction Pass 1

Corregir clasificaciones de diseño revisadas por Mauro, formalizar el workflow proporcional de Codex, commits, publicacion, evidencia visual y subagentes, ajustar la semantica estructural de R03 y reconciliar puntualmente M29 sin iniciar M36.1 ni cambiar contenido jugable.

### M36.0 — Documentation Review Closeout

Mauro aprobo la jerarquia documental, el GDD Markdown como baseline revisado, el roadmap M36–M55, los trece gates, R01–R23, el workflow de Codex/Git y las clasificaciones corregidas. M36.0 queda `DONE — DOCUMENTATION REVIEWED`; las decisiones creativas etiquetadas siguen abiertas y M36.1 requiere autorizacion independiente.

### M36.1 — Foundation Freeze Aprobado / Limite Obligatorio

Checkpoint A corregido implementa IDs `item_<GUID N lowercase>` opacos e inmutables, rutas separadas `CreateNew`/`Rehydrate`, unicidad de IDs activos, ownership estricto, item-owned storage explicito y reglas de stack/split/merge/rollback consumibles por M37. Un stack conserva una `ItemInstance` representativa y cantidad fungible; `Condition` get-only forma parte del estado de instancia y de la compatibilidad de stack.

Los pases correctivos agregan attachment detached y registro explicito de item-owned storage, bootstrap transaccional de containers, cleanup de IDs en merges totales, rechazo atomico de removal terminal cuando el storage propio no esta vacio y transiciones comprometidas de ownership. La validacion automatizada esta verde y Mauro confirmo manualmente pickup, drop, equip/unequip, equip directo desde el mundo, item-owned storage, mochila no vacia, containers y transfers con cuerpos sin duplicaciones ni ownership exceptions. Checkpoint A queda validado y cerrado.

Checkpoint B recupera la implementacion local parcial y congela identidad authored para el slice actual. `PersistentSceneObjectId` identifica exactamente 14 roots stateful de `SampleScene`: 3 actores, 3 puertas y 8 contenedores. Los dos world items authored usan `ItemInstance.CreateAuthored` con IDs `item_<32 hex lowercase>` exactos y separados de `DefinitionId`; los drops runtime conservan su instancia existente y no reciben un authored ID nuevo. `Debug Strange Machine` permanece excluida.

Runtime y Editor compilaron en Unity 6.4.6f1. `M36.1 Foundation Identity Validation` paso despues de aplicar la tabla, despues de reabrir la escena y en una reaplicacion idempotente; Checkpoint A volvio a dar `PASS`. Mauro valido manualmente crowbar y Lee-Enfield authored, pickup, equip directo desde el mundo, inventario y drop sin errores funcionales nuevos. M36.1 debe seguir siendo corto y no implementa:

- save/load;
- condition;
- repair/disassembly;
- actor lifecycle;
- gameplay nuevo;
- UI final.

Decisiones congeladas para M37:

- M37 debe rehidratar el `InstanceId` exacto y el `Condition` exacto validado; no debe crear otro ID durante carga;
- items no stackeables y cada stack visible conservan identidad durable; las unidades fungibles internas de un stack no poseen IDs individuales;
- `Foundation Freeze` queda `APPROVED`; estas decisiones son el contrato de entrada de M37 y no autorizan reinterpretarlas durante el closeout.

### ID TBD — Global Content ID Namespace Foundation / Límite Obligatorio

Unidad técnica interpuesta por autorización explícita para evitar que nuevos sistemas acumulen supuestos de ID global simple y Core implícito. Su estado es `VALIDATED — FOUNDATION COMPLETE`.

Implementa solamente:

- contrato central `ContentId` para `namespace:local_id` y namespace oficial reservado `core`;
- canonicalización en la frontera de carga de las Definition families registradas globalmente y de sus referencias reales;
- registries de `GameDatabase` con una sola clave canónica y compatibilidad temporal Core sin alias registrados;
- distinción de Global Content ID, Local ID, runtime/instance ID, persistent scene ID, tags y asset keys;
- migración explícita de `Mods/Core`, seam de source context y compatibilidad schema v1 para Definition ID/layout/equipment slots sin subir versión;
- fixture Editor temporal para coexistencia entre namespaces y referencias cross-namespace.

Esta foundation no implementó manifest, provenance, dependencies, overrides/patches, Workshop, SDK, scripting, DLL mods, hot reload, AssetBundles ni namespace de tags. La extensión posterior `ID TBD — Minimum Content Source Identity & Provenance Foundation` ya validó el manifest mínimo y provenance de inputs actuales; dependencies, patches y compatibilidad productiva permanecen en M50.0, que continúa futuro y no se considera iniciado.

### M37 — Limite Obligatorio

M37 persiste primero el slice actual: jugador, items, inventory/grid, Equipment, ownership, item-owned storages, containers, cuerpos, puertas, world items y runtime tags existentes. No serializa sistemas hipoteticos para actores, clima, facciones o mundo procedural.

M37.0 está `DONE — PERSISTENCE CORE VALIDATED`. M37.1 queda `DONE — CURRENT SLICE PERSISTENCE VALIDATED`: los diagnostics automatizados y la validación fresh-session manual, incluida la compatibilidad schema-v1 de Global Content IDs Core, pasaron. `Persistence Ready` permanece aprobado para el Current Slice y no se reabre.

### M38.0 — Límite Obligatorio

M38.0 queda `DONE — ACTOR RUNTIME & LIFECYCLE VALIDATED`. Extiende el Current Slice con identidad durable distinta de profile/locator, lifecycle `Alive/Dead`, pose/health y referencias a storages existentes; reconcilia authored bootstrap y representaciones runtime mediante el apply/rollback de M37.1. La validación automatizada y la evidencia manual fresh-session pasaron. AI, combat, world streaming, spawn a escala y el playable exploration prototype permanecen fuera.

### M38.1 — Límite Obligatorio

M38.1 queda `DONE — WORLD TIME / NEEDS / RECOVERY VALIDATED`. Implementa una autoridad única de segundos absolutos de game time, escala provisional configurable, derivación `Day N / HH:MM`, progreso de Hunger/Thirst mediante el mismo delta normal o explícito, rest/sleep real sin loops y persistencia/rollback aditivos dentro del Current Slice schema V1.

El slice real mantiene needs sólo en el player; no agrega componentes ficticios a NPCs ni amplía `ActorState`. Rest/sleep rechaza actores Dead, no revive y no aplica recuperación de health/heridas. Fatigue queda `DEFERRED — SHOULD, NOT REQUIRED FOR M38.1 FUNCTIONAL CLOSEOUT` porque no existe un modelo previo coherente y su incorporación correcta exigiría una expansión desproporcionada.

El diagnóstico automatizado fresh-session, la suite M36–M38 y el diagnóstico UX de inventario pasaron. Mauro confirmó visualmente World Clock, progresión de Hunger/Thirst, Rest 1h, Sleep 8h, consumibles, save/load fresh-session y continuidad posterior al load, sin errores runtime atribuibles a M38.1. `SampleScene` permaneció unchanged y no hubo warnings nuevos atribuibles.

### M39.0 — Límite Obligatorio

M39.0 queda `DONE — LOCALIZED HEALTH / MEDICINE VALIDATED`, con validation `AUTOMATED + MANUAL FRESH-SESSION PASSED`. Implementa únicamente `Head/Torso/LeftArm/RightArm/LeftLeg/RightLeg`, heridas durables `Laceration/Puncture/Blunt`, severity acotada, bleeding por el mismo `WorldClock`, pain derivado, tratamiento localizado `Bandaged` data-driven y la ventana H cualitativa existente.

`ActorMedicalStateComponent` es autoridad de wounds/bleeding/pain/treatment. `ActorHealthComponent` conserva la reserva vital escalar como bridge de vitalidad, tags, death y lifecycle M38; su agotamiento produce muerte coherente y una venda nunca llama `Heal(+X)`. El tratamiento consume exactamente x1 venda, no elimina la herida y preserva el WoundId durable. Rest/Sleep procesa el mismo delta médico del `WorldClock`.

Player y actores usan DTOs médicos planos aditivos en Current Slice schema V1. La omisión legacy de `medicalState` deriva baseline sin heridas ni etiología; null o datos inválidos fallan strict preflight sin mutar; el mismo apply transaccional cubre rollback post-medical-state. La ventana H cualitativa, la exclusividad Health/Inventory y los contratos WASD/camera quedaron preservados y validados.

Mauro confirmó manualmente las seis regiones, baseline `Se ve bien`, laceración severa aislada en LeftArm, estado `Injured`, bleeding con pérdida vital, Rest/Sleep sin healing, venda x1 con herida durable y sangrado controlado, y save/load fresh-session con el mismo estado médico y reserva vital. El load terminó `Success`, sin rollback requerido ni errores runtime atribuibles a M39.0.

Deuda de tuning no bloqueante: revisar posteriormente la relación severity/bleeding rate/tiempo hasta deterioro crítico o muerte, porque una laceración severa puede tardar demasiado en producir pérdida vital grave. No es un fallo arquitectónico ni de persistence.

Fuera del cierre M39: combat resolution, ballistics, armor, penetration, infection, fractures, surgery, organs, blood types, transfusions, antibiotics, complex analgesics, regional movement penalties, limb disability y AI. En ese closeout M40.0 quedó listo para autorización; su estado vigente se controla a continuación.

### M40.0 — Closeout Validado

M40.0 queda `DONE — COMBAT RESOLUTION & WEAPONS V1 VALIDATED`. Validation: `AUTOMATED + MANUAL FRESH-SESSION PASSED`. Implementa una ruta única de melee/firearm hacia heridas M39, seis regiones por impacto real, estado cargado durable por `ItemInstance`, ammo compatible, reload temporizado/cancelable, cycle, Equipment/ownership y persistence/rollback V1.

La automatización M40 pasó, incluido near-cover Correction Pass 1, preflight estricto y fault post-firearm-state con `ApplyFailed`, `RollbackAttempted: True` y `RollbackSucceeded: True`. Mauro confirmó manualmente firearm unloaded/reload/fire/cycle/regiones/world blocking/Dead-corpse; near-cover sin atravesar geometría; crowbar melee/range/cancelación; estado `Loaded 8/10` e `InstanceId` preservados tras drop/pickup; y Current Slice fresh-session restaurando el rifle equipado en `Loaded 8/10` con resultado `Success`. No aparecieron errores nuevos atribuibles a M40. Los warnings legacy `core:*` permanecen como deuda aceptada.

Fuera de M40.0 quedaron armor/penetration, proyectiles físicos, critical hits, balance/spread final, condition/desgaste, AI combat, dual wield, attachments, animación/audio y UI final. El tuning severity/bleeding de M39 y la compatibilidad legacy Core Content IDs permanecen como deuda no bloqueante. El estado vigente de armor/penetration se controla en M40.1 a continuación.

### M40.1 — Armor & Penetration V1 Validado

M40.1 queda `DONE — ARMOR / PENETRATION V1 VALIDATED`. Validation: `AUTOMATED + MANUAL FRESH-SESSION PASSED`. M40.0 permanece `DONE — COMBAT RESOLUTION & WEAPONS V1 VALIDATED`; `Combat Ready` queda `APPROVED`.

`PenetrationResolutionService` compara una magnitud interna común contra capas independientes del tipo de receptor. `incomingPower <= resistance` produce `Stopped`; sólo `incomingPower > resistance` produce `Penetrated`; el residual es `max(0, incomingPower - resistance)`. Armor wearable y superficies world penetrables usan exactamente ese núcleo, y el dispatch terminal mantiene un adapter médico explícito sin convertir al resolver común en una dependencia de M39.

Armor se descubre exclusivamente desde Equipment y aplica por las seis `BodyRegion`. Las capas se ordenan por `layer_priority` descendente y desempates canónicos, nunca por orden incidental. Un stop rechaza `Puncture` y puede transferir una única consecuencia `Blunt`; una penetración genera como máximo una consecuencia médica residual. M39 continúa siendo la única autoridad de wounds, bleeding, pain, vitalidad y muerte.

Toda munición de proyectil declara `penetration_power > 0`. No existen ramas `IsAP`, `CanPenetrate`, FMJ/AP/HP ni otra taxonomía binaria: futuras familias diferirán por datos y atravesarán el mismo resolver.

World geometry sin `penetration_profile_id` permanece opaca. Las superficies explícitamente penetrables continúan el ray con presupuesto residual, epsilon `0.001`, deduplicación de collider/owner y límite de cuatro superficies. Melee reutiliza impact resistance sólo contra el receptor impactado; no atraviesa paredes.

M40.1 agrega cero estado durable: profiles son Definitions; Equipment, `ItemInstance`, `Condition` e identidad ya realizan round-trip. No existen `armorState` ni `penetrationState`, y schema/envelope V1 permanecen en versión 1. `EffectiveResistance(ItemInstance, baseResistance)` es el seam reservado para M43; M40.1 no lee ni muta Condition.

Mauro confirmó manualmente: dos capas equipped con resistencia total `0.65` detuvieron la `.303` de power `0.65` con exactamente una `Blunt` y ninguna `Puncture`; una capa de `0.325` dejó residual `0.325` y una única `Puncture`; Head/arms descubiertos y armor sólo en inventory conservaron el comportamiento unarmored; crowbar directo reutilizó protección/impact resistance sin atravesar paredes; geometría opaca bloqueó antes del actor y una línea limpia restauró el impacto. El round-trip fresh-session reconstruyó `actor_677cb4714310457d9e35140b04a199f0` por `PersistenceRestore`, preservó equipadas `item_65e023d5f6a1478c8384a2f39be86630` y `item_71d498f132b9435c9e85caf1be6a5de4`, cargó con `FailureCode: Success` / `Result: Success` y volvió a detener el impacto de torso sin `Puncture`.

Las extensiones futuras — FMJ/AP/HP y anti-material exclusivamente data-driven sobre el mismo resolver, receptores machine/vehicle y el seam M43 de `Condition` — permanecen diferidas. Proyectiles físicos, ricochet, ángulo, espesor real, spall, fragmentación, vehículos y máquinas no forman parte de M40.1. Los warnings legacy Global Content ID continúan como deuda conocida no bloqueante.

### M41.0 — Navigation & Perception Foundation Validado

M41.0 queda `DONE — NAVIGATION / PERCEPTION FOUNDATION VALIDATED`. Validation: `AUTOMATED + MANUAL UNITY PASSED`. Runtime/Editor compile, data validation, `M41.0 Navigation & Perception Diagnostics` y la regresión directa M38.0 dieron `PASS`.

Navigation y Perception son capacidades independientes declaradas por bloques opcionales de `ActorProfileDefinition`. `ActorNavigationController` usa `NavMeshAgent` y estados explícitos `Idle`, `Moving`, `Reached` y `Failed`; rechaza destinos fuera del NavMesh, no reintenta y respeta lifecycle `Dead`. `ActorVisualPerceptionService` evalúa identidad, lifecycle, range, FOV horizontal y LOS físico con resultados explicables, sin depender de Combat ni de Navigation.

`ActorProfileComponent` aplica capacidades en bootstrap y restore sin apropiarse de la autoridad del player. `SampleScene` contiene una fixture aislada, NavMesh bakeado, barrera y markers reproducibles para diagnóstico y validación manual. Orden, path y resultados de percepción son efímeros; M41.0 no agrega estado durable ni cambia schema/envelope V1, y Navigation queda `Idle` tras restore.

Mauro confirmó manualmente que el navigator recibió destination, se desplazó físicamente rodeando la barrera y completó `Moving → Reached`. Con poses deterministas, la barrera activa produjo `Occluded` con blocker exacto `Navigation Perception Barrier`; al retirarla produjo `Perceived: True`, `Reason: Perceived` y `Blocker: <NONE>`. El helper usado para esa validación quedó corregido por `b4345890d9185d439d408cdece211424c88b8b21`.

M41.1 permanece fuera de alcance de M41.0. Hostility, alert states, investigation, chase, flee, combat decisions, cover, behavior trees y sistemas AI generales no forman parte de M41.0.

### M41.1 — Human Encounter AI V1 Validado

M41.1 queda `DONE — HUMAN ENCOUNTER AI V1 VALIDATED`. Validation: `AUTOMATED + MANUAL UNITY PASSED`; el gate `AI Ready` queda `APPROVED`.

`HumanEncounterAIController` sólo decide estado de encuentro, target explícitamente asignado, timers y response data-driven `avoid`/`flee`/`fight`. Reutiliza `ActorVisualPerceptionService` para percepción/LOS, `ActorNavigationController` para órdenes y path, y `WeaponCombatService` para arma, ammo, reload, impacto y resolución médica. Player y NPC usan `PhysicalShotPathResolver`; no se agrega una autoridad paralela de combate, navegación o percepción.

`LostContact` conserva exclusivamente la última posición obtenida de una percepción positiva, cancela acción activa y no lee el transform oculto del target. Tras timeout limpia el encounter y exige reasignación explícita para reacquisition. Estados, target, timers, path, percepción y munición de combate conservan sus contratos efímeros/persistentes preexistentes; M41.1 no cambia schema ni envelope de save.

El diagnóstico automático cubre avoid/flee, navegación inválida sin retry, fight con reload/disparo/armor, LostContact, no omniscience, reacquisition y lifecycle Dead. Mauro confirmó manualmente los cuatro escenarios y el tooling Editor quedó reducido a una fixture M41.1 explícita; diagnostics históricos se preservan para automatización sin entradas visibles obsoletas.

## Trabajo Congelado O Diferido

- No ampliar OnGUI por inercia fuera de tooling development-only acotado a un milestone autorizado.
- No iniciar por inercia Bounded History, World Persistence general, Sector Blueprint/Materialization, facciones amplias, UI final o producción masiva de contenido durante M41.2–M41.4.
- No introducir minería completa, geología/minerales de producción, fluid simulation, derrumbes estructurales, whole-world voxels o destructibilidad total como continuación automática del terrain spike.
- No introducir enfermedades generales, agricultura o vehiculos en la version inicial completa sin un nuevo rebaseline aprobado.
- No convertir JSON en scripting libre.
- No crear sistemas universales preventivos sin necesidad actual demostrada.
- M41.2 no adelanta NPC loadouts probabilísticos, affiliation, imperfect AI aim ni full ballistics; esos alcances pertenecen a M41.3/M41.4 o a futuros milestones explícitos.

## Regla Transversal De Integracion

Profundidad mediante sistemas conectados es una regla de aceptacion: todo sistema nuevo debe recibir estado relevante de otro sistema, modificar al menos una decision jugable y ofrecer feedback explicable. Una barra o simulacion aislada no cumple esta regla.
