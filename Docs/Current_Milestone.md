# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1

Estado actual:

`AUTHORIZED — IMMEDIATE PRIORITY`

Autoridad de alcance detallada: [NPC_Sandbox_and_Equipment_Sequence.md](NPC_Sandbox_and_Equipment_Sequence.md).

Objetivo: convertir las foundations ya existentes de actor, equipment, inventory, ownership, navigation, localized health, combat y corpse continuity en una prueba visible dentro de `WorldRuntime`, usando NPCs reales con loadouts probabilísticos data-driven.

M41.3 debe demostrar:

- control development-only para spawnear NPCs reales en una posición materializada/NavMesh válida;
- uso de las autoridades actuales de actor spawn, identity, Equipment, ownership e ItemInstance; no crear registries o inventarios paralelos;
- loadout generado desde JSON con probabilidades reales y resultado explícito `none`/vacío cuando corresponda;
- auditoría de Loot Tables v0 antes de decidir si se extienden o si corresponde un Actor Loadout Table/Profile separado;
- resultado de cada roll diagnosticable/reproducible mediante seed/evidence de debug;
- equipment, backpack, inventory, weapon y ammo como estado real del actor;
- roaming básico mediante `ActorNavigationController` sobre el mapa/terrain vigente;
- localized health M39, combat/armor M40/M40.1, lifecycle Alive/Dead y corpse continuity reales;
- al morir, el corpse debe conservar exactamente las pertenencias del actor vivo; prohibido rerollear loot al morir o al abrir el cadáver;
- varios NPC simultáneos sin duplicar ownership, items o autoridades.

M41.3 no implementa todavía affiliation Blue/Red, threat acquisition automático, imperfect aim, reaction/focus/spread ni full ballistics; esos alcances pertenecen a M41.4 o milestones posteriores.

## Último Cierre Funcional

### M41.2 — Basic Equipment & Weapon Coverage V1

Estado final:

`DONE — BASIC EQUIPMENT & WEAPON COVERAGE V1 VALIDATED`

Commit funcional publicado: `4f877da10dee813b0bed816194110b5a27087683`.

M41.2 cerró con:

- los 17 slots reales de `core:human_standard_01` detectados desde Definitions y con coverage data-driven;
- `27` items Core nuevos de ropa/equipment para cobertura y variedad funcional sin exigir arte final;
- mochilas pequeña/media/grande con storage real `8×10`, `10×12` y `12×14`;
- casco y chaleco reutilizando los perfiles/autoridad M40.1 existentes;
- Lee-Enfield `manual_cycle`, range `80`;
- Semi-Automatic Rifle `semi_automatic`, range `75`;
- Automatic Rifle `automatic`, range `60`;
- contrato genérico `fire_mode` con `manual_cycle`, `semi_automatic` y `automatic`, validado por datos y sin branches por DefinitionId;
- manual/semi = un disparo por press; automatic = repetición mientras LMB permanezca held, siempre limitada por `cycle_time`, munición disponible y sin auto-reload;
- `firearm.range` como máximo físico temporal del hitscan y `melee_range` como máximo físico melee;
- aim/tracer debug clampado al endpoint alcanzable;
- Current Slice preservando backpack content, ownership/equipment y firearm loaded state sin schema bump.

Validation reportada `PASS`: Runtime/Editor compile, M41.2 coverage/backpacks/fire modes/range/Current Slice D3D11 Play Mode, M37/M37.1, Content Provenance/Namespace, Inventory UX, M40/M40.1, Player Controls/Health y `git diff --check`.

System Harmony: sin autoridad paralela de Equipment/ownership/storage/firearm resolver/persistence; sin worldgen/terrain/schema changes y sin adelantar M41.3/M41.4. `debug_accuracy_spread` permanece deuda intencional para M41.4.

## Último Cierre Técnico De Mundo

### Deformable Volumetric Terrain Foundation / Technical Spike

Estado final:

`VALIDATED — TECHNICAL SPIKE COMPLETE`

Commit técnico: `d0309cf053be220a22151cae2dae9aca6f988e6f`.

Integración publicada en `dev`: `1b41ead829cd566c55df5adfc0522e33e1dffb96`.

Autoridad de evidencia: [Deformable_Terrain_Foundation.md](Deformable_Terrain_Foundation.md).

El spike validó density field chunked, Marching Tetrahedra, mesh/collider, crater, túnel con roof/floor, cross-chunk mutation, dirty rebuild localizado, persistencia/replay `SPIKE_NON_PRODUCTION`, player traversal y NavMesh local sin cambiar `world_session_v1` schema `7` ni los goldens de worldgen.

## Siguiente Milestone Reservado

### M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1

Estado: `PLANNED — AFTER M41.3`.

Debe agregar Blue/Red como presentación debug sobre affiliation/disposition genérica, adquisición automática de amenazas mediante perception/LOS, cierre de distancia según `firearm.range`/`melee_range` y aim físicamente imperfecto con reaction/focus/spread antes de `PhysicalShotPathResolver`.

Después de M41.4 se exige playtest/review antes de autorizar otro sistema grande.

## Contratos Cerrados Relevantes

- M37/M37.1 continúan siendo la autoridad de persistence/Current Slice.
- M38 continúa siendo actor identity/lifecycle/spawn foundation.
- M39 continúa siendo la autoridad de localized health/wounds.
- M40/M40.1 continúan siendo la autoridad de combat, firearms/ammo/reload, armor y penetration.
- M41.0 continúa siendo la autoridad de Navigation/Perception.
- M41.1 continúa siendo el encounter brain existente; M41.3 no debe reemplazarlo.
- `ActorEquipmentComponent`, item-owned storage, ownership e `ItemInstance` continúan siendo las autoridades de equipment/state.
- `fire_mode` es data-driven; no crear sistemas separados por arma.
- JSON declara contenido; C# ejecuta comportamiento genérico.

## No Iniciar Todavía

Durante M41.3 no iniciar:

- M41.4 por anticipado salvo seams estrictamente necesarios;
- affiliation/faction system completo;
- automatic threat acquisition de combate;
- imperfect NPC aim/reaction/focus/spread;
- full ballistics/bullet drop/travel time/wind;
- strategic AI, squads, cover tactics o schedules;
- final NPC population/ecology;
- production UI;
- condition/repair/crafting;
- mining/geology/fluid simulation;
- world streaming/LOD/navigation productivos;
- producción masiva de contenido.

## Próximo Paso Exacto

Iniciar `M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1` en el checkout canónico `D:\Programs\UnityProject\Old Scarss`, sin worktrees, preservando el cambio user-owned `ProjectSettings/ProjectSettings.asset` (`runInBackground: 0 → 1`).
