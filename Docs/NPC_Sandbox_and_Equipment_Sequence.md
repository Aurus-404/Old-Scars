# Old Scars — Equipment, NPC Sandbox & Combat Validation Sequence

- Estado de dirección: `APPROVED — ACTIVE SEQUENCE`
- Tipo: gameplay/content validation sequence
- Alcance: cobertura funcional de equipment/weapons, NPC sandbox visible en WorldRuntime y combate AI observable sobre el mapa actual
- Autoridad relacionada: `Project_Roadmap.md`, `Current_Milestone.md`, `Next_Sprints.md`, `Technical_Architecture.md`, `DataDriven_JSON_Rules.md`

## Propósito

Esta secuencia existe para dejar de validar actor/equipment/AI únicamente mediante fixtures cerradas y empezar a probar los sistemas juntos dentro del runtime real de Old Scars.

La meta no es producción masiva de contenido ni IA final. La meta es disponer de un sandbox jugable y observable donde sea posible:

1. tener suficiente equipment y armas para ejercitar los sistemas existentes;
2. spawnear NPCs con loadouts distintos generados desde datos;
3. verlos navegar en el mapa real;
4. dañarlos por regiones corporales, matarlos y lootear exactamente lo que poseían;
5. hacer que actores con relaciones distintas detecten, persigan o ataquen usando las autoridades ya implementadas;
6. observar errores reales de integración, navegación, ownership, persistence, equipment, combat y corpse loot.

`Deformable Volumetric Terrain Foundation / Technical Spike` permanece `VALIDATED — TECHNICAL SPIKE COMPLETE` en `d0309cf053be220a22151cae2dae9aca6f988e6f`, integrado por `1b41ead829cd566c55df5adfc0522e33e1dffb96`.

La secuencia funcional queda:

`M41.2 DONE → M41.3 DONE → M41.4 AUTHORIZED → Playtest / Review`.

---

# M41.2 — Basic Equipment & Weapon Coverage V1

Estado final: `DONE — BASIC EQUIPMENT & WEAPON COVERAGE V1 VALIDATED`

Commit funcional: `4f877da10dee813b0bed816194110b5a27087683`.

## Resultado cerrado

M41.2 dejó:

- los 17 slots reales de `core:human_standard_01` cubiertos desde Definitions;
- 27 items Core nuevos de ropa/equipment sin exigir arte final;
- mochilas pequeña/media/grande con storage real `8×10`, `10×12` y `12×14`;
- casco y chaleco reutilizando M40.1;
- Lee-Enfield `manual_cycle`, range `80`;
- Semi-Automatic Rifle `semi_automatic`, range `75`;
- Automatic Rifle `automatic`, range `60`;
- contrato `fire_mode` data-driven sin branches por DefinitionId;
- manual/semi = un disparo por press;
- automatic = repetición mientras LMB permanezca held, limitada por `cycle_time`, ammo y sin auto-reload;
- `firearm.range` como máximo físico temporal de hitscan;
- `melee_range` como máximo físico melee;
- aim/tracer debug limitado al endpoint alcanzable;
- Current Slice preservando backpack content, ownership/equipment y firearm loaded state sin schema bump.

`debug_accuracy_spread` quedó intencionalmente sin un sistema de precisión completo para ser consumido en M41.4.

---

# M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1

Estado final: `DONE — NPC SANDBOX SPAWN & RANDOMIZED LOADOUTS V1 VALIDATED`

Commit funcional publicado: `a90dc4e1a38bef69e3762e398a378a666a9f993e`.

## Objetivo cumplido

M41.3 convirtió las foundations existentes de actor, navigation, health, equipment, ownership y corpse loot en una prueba visible dentro de `WorldRuntime`.

La implementación no creó una población productiva ni una IA nueva. Construyó tooling development-only sobre autoridades ya existentes.

## Actor Loadout Profile

Se implementó una familia de Definition separada para loadouts de actor.

`ActorLoadoutProfileDefinition` puede expresar:

- grupos de elección ponderados;
- `none` explícito como resultado válido;
- equipment real;
- inventory real;
- cantidades/rangos de cantidad;
- slot ids cuando son necesarios;
- packages weapon/ammo sin branches por DefinitionId.

La semántica de `LootTableDefinition` de contenedores se preservó; no se deformó para resolver actor equipment.

Core incluye el profile:

`core:debug_sandbox_npc_loadout_01`.

También existe un actor profile development-only:

`core:debug_sandbox_npc_01`.

El actor usa `core:human_standard_01`, health/navigation/perception existentes y no incorpora encounter combat AI en M41.3.

## Generación y RNG

El roll ocurre únicamente durante NEW ACTOR BOOTSTRAP.

Cadena validada:

`ActorSpawnService → actor identity/components → actor profile → actor loadout profile → deterministic debug roll → real ItemInstances → Equipment/Inventory/ownership`.

El sandbox utiliza un RNG reproducible basado en base seed + spawn sequence/profile evidence; no usa `UnityEngine.Random` como autoridad oculta.

El resultado concreto se transforma inmediatamente en estado normal del actor.

No se rerollea en:

- muerte;
- apertura de cadáver;
- cierre/reapertura;
- Current Slice restore.

## Evidencia de variación

El gate integrado demostró simultáneamente:

- `12` NPCs activos;
- `12` `ActorInstanceId` únicos;
- `131` `ItemInstance` únicos;
- `12` firmas de loadout distintas.

El profile Core incluye probabilidades reales de no portar algunas piezas, backpack o weapon.

## Spawn y navegación

Los actores se spawnean cerca del player sobre una posición NavMesh válida y usan `ActorSpawnService` como autoridad.

El roaming development-only delega órdenes exclusivamente a `ActorNavigationController`.

Durante M41.3 se descubrió un defecto genérico previo de composición:

`ActorProfileComponent` consultaba `Collider.bounds` inmediatamente después de crear/mover la representación mientras transforms físicos todavía no estaban sincronizados. Esto podía producir `NavMeshAgent.baseOffset` cercano a `79` en lugar de aproximadamente la altura normal del actor.

La corrección usa `Physics.SyncTransforms()` antes de derivar la geometría/configuración del agent.

Después de corregirlo, los NPCs recibieron destinos válidos y se desplazaron mediante la autoridad existente.

Esta corrección no crea pathfinding paralelo ni navegación alternativa.

## Damage, death y corpse continuity

Los NPCs utilizan la ruta real:

- M39 localized health/wounds/bleeding;
- M40 combat/weapons;
- M40.1 armor/penetration;
- M38 lifecycle Alive/Dead.

Se verificó daño localizado en múltiples regiones.

La muerte se produjo por heridas M40 y sangrado M39 impulsado por `WorldClock`, no por una resta debug directa de HP como ruta productiva.

Se capturó evidencia canónica de pertenencias y se validó:

`PRE-DEATH BELONGINGS == CORPSE BELONGINGS == REOPENED CORPSE BELONGINGS`.

La apertura/cierre/reapertura se ejercitó mediante la sesión real de inventario.

No hubo:

- reroll al morir;
- reroll al abrir;
- pérdida del arma;
- pérdida del backpack;
- duplicación de ItemInstances;
- ruptura de ownership.

## Persistence

Current Slice snapshot/teardown/restore preservó:

- Actor/Item IDs;
- Definitions;
- cantidades;
- Equipment;
- ownership;
- item-owned storages.

En restore, `LoadoutProfileId` y `LoadoutSignature` aparecen ausentes como metadata de bootstrap; el generador no vuelve a ejecutarse. Se restaura el estado real ya generado.

No hubo schema bump de Current Slice ni `world_session_v1`.

## Validación M41.3

Reportado `PASS`:

- Runtime compile;
- Editor compile;
- WorldRuntime D3D11 integrated gate;
- GameDataLoader/DataValidator;
- Content Namespace/Provenance;
- Equipment/ownership/item-owned storage;
- M37 Persistence Core;
- M37.1 Current Slice;
- M38 Actor Lifecycle;
- M39 localized health;
- M40 weapons/combat;
- M40.1 armor/penetration;
- M41.0 Navigation/Perception;
- M41.1 Human Encounter AI regression;
- M41.2 Equipment/Weapon Coverage;
- player controls/runtime;
- `git diff --check`.

La revisión automática encontró únicamente dos findings pequeños de observabilidad y ambos fueron corregidos antes del cierre: identificación de firearms en el resumen del sandbox y ubicación del feedback de seed inválida.

## System Harmony M41.3

Se preservan como autoridades únicas:

- `ActorSpawnService`;
- actor runtime registry/identity;
- `ItemInstance`/ItemInstanceId;
- Equipment;
- Inventory/item-owned storage;
- ownership;
- corpse continuity;
- `ActorNavigationController`;
- M39 health;
- M40/M40.1 combat;
- M37/M37.1 persistence.

No se introdujo:

- segundo registry;
- fake corpse loot;
- reroll-on-death/open/restore;
- pathfinding paralelo;
- nueva save schema;
- worldgen change;
- M41.4 adelantado.

## Deuda intencional después de M41.3

El roaming y la metadata de sandbox son tooling efímero. No constituyen población persistente, schedules, ecological simulation ni AI productiva.

---

# M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1

Estado: `AUTHORIZED — IMMEDIATE PRIORITY`

## Objetivo

Permitir un sandbox de combate observable NPC↔NPC y NPC↔Player usando las autoridades existentes, sin aimbot perfecto y sin reescribir M41.1.

## Blue / Red debug actors

Agregar controles development-only equivalentes a:

- `Spawn Blue NPC`;
- `Spawn Red NPC`.

Los colores son únicamente presentación/debug. La lógica no debe depender de `if color == red`.

Debe existir una representación mínima de affiliation/team/disposition genérica suficiente para expresar relaciones del sandbox.

Baseline:

- Blue no hostil al Player;
- Red considera hostiles a Blue y Player;
- Red vs Red no hostil por defecto;
- Blue vs Blue no hostil por defecto.

La arquitectura debe poder extenderse posteriormente sin convertirse en el sistema completo de facciones/reputación.

## Threat acquisition

M41.1 sabe combatir una amenaza explícitamente asignada. M41.4 debe agregar la capa mínima para encontrar candidatos hostiles cercanos y comprobar percepción antes de asignarlos.

Cadena deseada:

`affiliation/disposition → nearby candidate → ActorVisualPerceptionService/LOS → threat assignment → HumanEncounterAIController → ActorNavigationController / WeaponCombatService`.

No permitir omnisciencia.

No crear otro perception service ni otro combat brain.

## Range-aware combat

La IA debe entender el alcance de su arma equipada.

Para firearm:

- jamás resolver daño más allá de `firearm.range`;
- usar una distancia de engagement válida nunca mayor al alcance físico;
- si el target está demasiado lejos, cerrar distancia mediante Navigation;
- combatir sólo cuando exista LOS/engagement válido;
- si se pierde rango/LOS, reutilizar navegación y lost-contact existentes.

Para melee:

- usar `melee_range`;
- acercarse cuando el target está lejos;
- golpear únicamente a distancia válida;
- nunca proyectar melee a distancia absurda.

Mantener distancia compleja, retreat táctico y cover quedan fuera salvo que aparezca un blocker real.

## Imperfect aim — no aimbot

No usar:

`posición exacta de Head → ray perfecto → hit garantizado`.

Tampoco usar una probabilidad abstracta que salte directamente a hit/miss.

Dirección:

`target observado → punto objetivo aproximado → error angular físico → dirección de disparo → PhysicalShotPathResolver → impacto real`.

Esto debe permitir:

- misses reales;
- impactos en otra región;
- impactos en paredes/obstáculos;
- eventual impacto accidental en otro actor si la ruta física lo determina.

Baseline V1:

- center mass como objetivo predominante;
- head no dominante;
- reaction/acquisition delay;
- focus time que reduce gradualmente spread mientras la percepción válida se mantiene;
- spread nunca perfecto cero para un NPC normal;
- mayor distancia = mayor error;
- movimiento/cambio brusco de target puede aumentar error;
- arma/fire mode puede aportar datos de precisión existentes;
- automatic fire puede incrementar spread durante ráfaga y recuperarlo tras pausa si resulta proporcional al scope.

No implementar todavía bullet travel, gravity, wind, drag o solver balístico productivo.

## Referencias conceptuales

Usar como referencias de patrones, no como código ni valores a copiar:

- Valve Source SDK / Half-Life 2 AI: reaction/focus y spread defocused/focused;
- Valve Developer Community: weapon spread y engagement según arma;
- Arma 3 AI config: min/mid/max ranges y probabilidades por fire mode;
- Bungie/Halo AI talks: engagement distance como parte crítica de comportamiento legible.

## Observabilidad M41.4

El sandbox debe poder explicar decisiones sin depender únicamente de logs crípticos.

Ejemplo development-only:

```text
NPC actor_x
Affiliation: Red
Target: actor_y / Player
Perception: Perceived
Distance: 27.8
Weapon: Automatic Rifle
Weapon range: 60
State: Fighting
Focus: 0.71
Spread: 3.4 deg
Navigation: Idle/Moving
```

Fuera de range:

```text
Distance: 72
Weapon range: 60
State: Closing Distance
```

## Aceptación M41.4

Debe demostrarse como mínimo:

- Blue/Red spawn usando el sandbox real de M41.3;
- affiliation/disposition genérica y no branches por color;
- same-team no hostil por defecto;
- Red detecta Blue/Player únicamente mediante percepción/LOS;
- target automático llega al existing encounter AI;
- firearm cierra distancia si está fuera del engagement/range;
- melee cierra distancia hasta `melee_range`;
- no damage fuera de los ranges físicos existentes;
- reaction/focus/spread producen disparos físicamente imperfectos;
- no existe head aimbot;
- NPC↔NPC y NPC↔Player usan M39/M40/M40.1 existentes;
- death/corpse continuity de M41.3 permanece intacta;
- no stack paralelo de faction/AI/navigation/combat.

---

# Playtest / Review Después De M41.4

No encadenar automáticamente otro sistema grande.

Prueba objetivo:

`WorldRuntime → varios NPCs con loadouts distintos → navegación real → Blue/Red detectan hostiles → cierran distancia según arma → disparan/golpean con precisión imperfecta → localized health/armor/death → corpse loot exacto`.

Después del playtest se revisarán bugs reales, performance, navegación, ownership/equipment, combat readability y game feel. Recién entonces se decidirá si conviene:

- corregir/pulir gameplay;
- sumar otra mecánica base;
- o volver a world realization/materialización.

## Límites generales de la secuencia

No iniciar por inercia durante M41.4:

- sistema completo de facciones/reputación/memoria regional;
- squads, cover tactics sofisticadas o strategic/off-sector AI;
- final NPC population/ecology;
- full ballistic simulation;
- production UI;
- Bounded History / World Persistence general / Sector Blueprint;
- world streaming/LOD/navigation productivos;
- mining/geology/fluid simulation;
- condition/repair/crafting;
- producción masiva de contenido.
