# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M41.2 — Basic Equipment & Weapon Coverage V1

Estado actual:

`AUTHORIZED — IMMEDIATE PRIORITY`

Autoridad de alcance detallada: [NPC_Sandbox_and_Equipment_Sequence.md](NPC_Sandbox_and_Equipment_Sequence.md).

Objetivo: agregar coverage funcional suficiente de equipment, storage y firearms para poder probar los sistemas existentes dentro de WorldRuntime antes de abrir el sandbox NPC M41.3.

M41.2 debe inspeccionar las Definitions reales y conservar `core:human_standard_01` como única fuente de verdad. El layout actual contiene 17 slots:

- `core:head`;
- `core:eyes`;
- `core:neck`;
- `core:torso_inner`;
- `core:torso_middle`;
- `core:torso_outer`;
- `core:hands_wear`;
- `core:hand_left`;
- `core:hand_right`;
- `core:back`;
- `core:waist`;
- `core:sling`;
- `core:legs_inner`;
- `core:legs_outer`;
- `core:feet_inner`;
- `core:foot_left`;
- `core:foot_right`.

La implementación no debe copiar esa lista como autoridad paralela: los diagnostics/coverage deben derivarla de las Definitions cargadas.

Aceptación principal:

- al menos un item funcional capaz de ejercitar cada slot relevante del layout;
- ropa/equipment básico sin requerir modelos, iconos, attachment visuals ni arte final;
- propiedades sólo cuando ya exista un consumidor gameplay real;
- varias mochilas con capacidades/peso/footprint distintos usando el backend item-owned storage existente;
- equip/unequip, ownership, transfers, backpack content y Current Slice sin regresiones;
- Lee-Enfield conserva bolt-action coverage;
- agregar al menos una firearm semi-automatic y una automatic;
- cualquier fire/action mode requerido debe ser genérico y data-driven, reutilizando `WeaponCombatService` y el estado `ItemInstance` existente;
- `firearm.range` se conserva como máximo físico temporal del hitscan y `melee_range` como máximo físico melee;
- aim/trace debug debe terminar en el alcance efectivo y no aparentar daño infinito;
- no implementar todavía balística productiva, bullet drop, travel time, viento o un solver de proyectil completo.

No convertir M41.2 en producción masiva de contenido. El contenido nuevo existe para coverage y para alimentar M41.3/M41.4.

## Siguientes Milestones Reservados

### M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1

Estado: `PLANNED — AFTER M41.2`.

Debe permitir spawnear NPCs reales dentro de WorldRuntime, generar equipment/inventory probabilístico desde JSON con posibilidad de `none`, navegar de forma básica sobre el mapa vigente, recibir localized damage, morir y dejar exactamente sus pertenencias reales en el corpse.

### M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1

Estado: `PLANNED — AFTER M41.3`.

Debe agregar Blue/Red como presentación debug sobre una representación genérica mínima de affiliation/disposition, adquisición automática de amenazas mediante perception/LOS, cierre de distancia según arma, melee range real y aim físicamente imperfecto con reaction/focus/spread antes de `PhysicalShotPathResolver`.

Después de M41.4 se exige playtest/review antes de autorizar otro sistema grande.

## Último Cierre Técnico

### Deformable Volumetric Terrain Foundation / Technical Spike

Estado final:

`VALIDATED — TECHNICAL SPIKE COMPLETE`

Commit técnico: `d0309cf053be220a22151cae2dae9aca6f988e6f`.

Integración publicada en `dev`: `1b41ead829cd566c55df5adfc0522e33e1dffb96`.

Autoridad de evidencia: [Deformable_Terrain_Foundation.md](Deformable_Terrain_Foundation.md).

El spike validó:

- bounded shared scalar-density lattice dividida en cuatro chunks técnicos;
- `Marching Tetrahedra` como meshing del spike;
- baseline derivada de Macro Geography sin autoridad paralela;
- mesh + collider;
- crater runtime;
- túnel/cavidad con roof/floor real imposible para una única heightmap;
- cross-chunk mutation y shared-border agreement;
- dirty rebuild `1/2/2/4` según contained/border/corner;
- player traversal sobre chunks, crater y túnel;
- persistencia/replay `deformable_terrain_spike_v1` marcada `SPIKE_NON_PRODUCTION`;
- dos operations persistidas en `1,511 B`;
- local NavMesh probe;
- placeholder materials mate Surface/Soil/Rock;
- goldens Plan/Geography/Water/Climate/Environment/Human Geography preservados;
- `world_session_v1` permanece schema `7`.

Mediciones finales de referencia: density `6–16 ms`, initial mesh `156–160 ms`, collider creation `6 ms`, affected mesh rebuild `149 ms`, collider update `5 ms`, NavMesh baseline `44 ms` y deformed `31 ms`. No son budgets productivos.

## Open World Rebaseline

Estado de dirección:

`APPROVED DESIGN DIRECTION — PARTIALLY IMPLEMENTED FOUNDATIONS`

[Open_World_Architecture.md](Open_World_Architecture.md) mantiene la dirección del mundo lógico persistente y [Deformable_Terrain_Foundation.md](Deformable_Terrain_Foundation.md) fija la requirement volumétrica/deformable. La representación volumétrica quedó técnicamente demostrada, pero streaming sectorial, LOD productivo, world persistence general de mutations/blueprints, final navigation strategy, biome realization local, vegetation, geology, rivers finales y materialización sectorial de producción siguen futuros.

No volver a ampliar worldgen por inercia durante M41.2–M41.4.

## Contratos Cerrados Relevantes

- M37/M37.1 continúan siendo la autoridad de persistence/Current Slice; no crear un save paralelo para equipment.
- M39 continúa siendo la autoridad de localized health/wounds.
- M40/M40.1 continúan siendo la autoridad de combat, firearms/ammo/reload, armor y penetration.
- M41.0 continúa siendo la autoridad de Navigation/Perception.
- M41.1 continúa siendo el encounter brain existente; M41.4 debe extender adquisición/aim sin reemplazarlo.
- `ActorEquipmentComponent`, item-owned storage, ownership y `ItemInstance` siguen siendo las autoridades de equipment/state.
- JSON declara contenido; C# ejecuta comportamiento genérico.

## No Iniciar Todavía

Durante M41.2 no iniciar:

- M41.3 o M41.4 por anticipado salvo seams estrictamente necesarios y sin implementar su feature;
- probabilidades/loadout NPC;
- facciones completas;
- strategic AI, squads, cover tactics;
- full ballistics/bullet drop/travel time/wind;
- modelos, animaciones, audio o UI final;
- condition/repair/crafting;
- mining/geology/fluid simulation;
- world streaming/LOD/navigation productivos;
- producción masiva de contenido.

## Próximo Paso Exacto

Iniciar `M41.2 — Basic Equipment & Weapon Coverage V1` en el checkout canónico `D:\Programs\UnityProject\Old Scarss`, sin worktrees, preservando el cambio user-owned `ProjectSettings/ProjectSettings.asset` (`runInBackground: 0 → 1`).
