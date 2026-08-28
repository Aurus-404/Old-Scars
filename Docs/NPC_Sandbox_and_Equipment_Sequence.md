# Old Scars — Equipment, NPC Sandbox & Combat Validation Sequence

- Estado de dirección: `APPROVED — ACTIVE SEQUENCE`
- Tipo: gameplay/content validation sequence
- Alcance: cobertura funcional de equipment/weapons, NPC sandbox visible en WorldRuntime y combate AI observable sobre el mapa actual
- Autoridad relacionada: `Project_Roadmap.md`, `Current_Milestone.md`, `Next_Sprints.md`, `Technical_Architecture.md`, `DataDriven_JSON_Rules.md`

## Propósito

Esta secuencia existe para dejar de validar los sistemas de actor/equipment/AI solamente en fixtures muy cerradas y empezar a probarlos juntos dentro del runtime real de Old Scars, sobre el mapa/materialización física vigente.

La meta no es producción masiva de contenido ni IA final. La meta es crear un sandbox jugable y observable donde sea posible:

1. tener suficiente equipment y armas para ejercitar los sistemas existentes;
2. spawnear NPCs con loadouts distintos generados desde datos;
3. verlos navegar en el mapa real;
4. dañarlos por regiones corporales, matarlos y lootear exactamente lo que poseían;
5. hacer que NPCs con relaciones distintas detecten, persigan o ataquen a otros actores usando las autoridades ya implementadas;
6. observar errores reales de integración, navegación, ownership, persistence, equipment, combat y corpse loot en lugar de depender únicamente de diagnostics aislados.

`Deformable Volumetric Terrain Foundation / Technical Spike` ya quedó `VALIDATED — TECHNICAL SPIKE COMPLETE` en el commit técnico `d0309cf053be220a22151cae2dae9aca6f988e6f`, integrado en `dev` por `1b41ead829cd566c55df5adfc0522e33e1dffb96`. M41.2 quedó cerrado en `4f877da10dee813b0bed816194110b5a27087683`; la secuencia continúa ahora por M41.3.

---

# M41.2 — Basic Equipment & Weapon Coverage V1

Estado final: `DONE — BASIC EQUIPMENT & WEAPON COVERAGE V1 VALIDATED`

Commit funcional publicado: `4f877da10dee813b0bed816194110b5a27087683`.

## Objetivo

Agregar contenido funcional mínimo suficiente para cubrir los espacios de equipment existentes y varias familias de armas, sin exigir arte final.

El contenido debe vivir en el pipeline data-driven Core/mod existente. JSON declara contenido; C# sólo implementa comportamiento genérico que todavía falte.

## Equipment coverage

El layout humano actual `core:human_standard_01` contiene 17 slots. La implementación debe inspeccionar las Definitions reales y enumerar los slots desde allí; no se debe mantener una lista paralela hardcodeada como autoridad runtime.

Cada slot real debe poder ser ejercitado por al menos una pieza equipable válida.

Las piezas de prueba pueden ser simples, por ejemplo:

- gorros/cascos;
- camisas/chaquetas/abrigos;
- pantalones;
- guantes;
- botas/calzado;
- equipamiento de cintura u otros slots que realmente existan;
- back equipment y mochilas.

No se exige:

- modelo 3D;
- sprite/icono propio;
- textura;
- attachment visual;
- animación;
- arte final.

Un item puede usar el fallback visual actual. Lo importante es que tenga nombre, peso, slots válidos, propiedades realmente consumidas por los sistemas actuales y que equip/unequip/ownership/persistence funcionen correctamente.

No inventar estadísticas sin consumidor. Si una propiedad todavía no afecta gameplay, no crear un sistema nuevo sólo para llenar JSON.

## Mochilas

M41.2 debía dejar varias mochilas funcionalmente distintas para estresar item-owned storage y Equipment, con diferencias reales de capacidad/footprint/peso según los contratos vigentes.

Objetivos de prueba:

- mochila con poco espacio;
- mochila intermedia;
- mochila grande con más espacio y mayor coste físico razonable;
- equip/unequip con contenido;
- ownership correcto;
- storage item-owned preservado;
- persistence/round-trip;
- cadáveres y transferencias sin duplicar ni perder contenido.

No crear un backend nuevo de mochila.

## Weapon coverage

El Lee-Enfield cubre bolt/manual-cycle. M41.2 debía agregar cobertura funcional de:

- bolt/manual-cycle — existente;
- semi-automatic;
- automatic.

El objetivo era cubrir comportamientos, no inflar el catálogo. La extensión debía ser genérica/data-driven y compartida por `WeaponCombatService` y consumidores existentes, sin sistemas separados por arma o branches por DefinitionId.

## Rango físico temporal de armas

La balística física completa permanece futura, pero el hitscan temporal no debe comportarse como un disparo infinito.

Contrato cerrado por M41.2:

- `firearm.range` = distancia física máxima temporal del disparo hitscan;
- `melee_range` = distancia física máxima de un ataque melee;
- el aim/trace/debug visual no debe sugerir que un objetivo más allá del rango es alcanzable;
- el ray de cámara puede seguir buscando el punto bajo el mouse para determinar dirección, pero la solución física y la visualización alcanzable quedan clampadas al rango efectivo;
- gravedad, caída de proyectil, velocidad de bala y solver balístico productivo siguen fuera.

Este rango sigue siendo un contrato temporal reemplazable/extendible por futura balística.

## Closeout M41.2

M41.2 cerró con evidencia reportada `PASS` y dejó:

- los 17 slots reales de `core:human_standard_01` cubiertos desde Definitions;
- `27` items Core nuevos de ropa/equipment para coverage y variedad funcional;
- mochilas pequeña/media/grande con storage real `8×10`, `10×12` y `12×14`;
- casco y chaleco reutilizando M40.1;
- Lee-Enfield `manual_cycle`, range `80`;
- Semi-Automatic Rifle `semi_automatic`, range `75`;
- Automatic Rifle `automatic`, range `60`;
- contrato `fire_mode` canónico con `manual_cycle`, `semi_automatic`, `automatic`;
- manual/semi = un disparo por press; automatic = repetición mientras LMB esté held, limitada por `cycle_time`, munición y sin auto-reload;
- aim/tracer clampado al endpoint alcanzable;
- Current Slice preservando backpack content, ownership/equipment y estado cargado firearm sin schema bump;
- Runtime/Editor compile, M41.2 D3D11 Play Mode, M37/M37.1, Content Provenance/Namespace, Inventory UX, M40/M40.1, Player Controls/Health y `git diff --check` en PASS;
- revisión de System Harmony sin autoridad paralela, worldgen/terrain/schema changes ni adelanto de M41.3/M41.4.

Deuda intencional: `debug_accuracy_spread` sigue sin sistema de precisión; se reserva para M41.4.

---

# M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1

Estado actual: `AUTHORIZED — IMMEDIATE PRIORITY`

## Objetivo

Convertir las foundations existentes de actor, navigation, health, equipment, ownership y corpse loot en una prueba visible dentro de WorldRuntime.

Debe existir una herramienta development-only simple para spawnear NPCs reales en el mapa actual.

## Spawn

Agregar un control debug reproducible, por ejemplo `Spawn NPC`, que:

- encuentre una posición válida/materializada;
- use la autoridad de spawn/identity existente;
- no cree un segundo actor registry;
- configure Navigation/Perception mediante profiles existentes cuando corresponda;
- coloque al actor sobre superficie física/NavMesh válida;
- permita varios NPC simultáneos para pruebas de navegación y ownership.

La UI puede ser OnGUI/debug mientras permanezca development-only y pequeña. No es UI final.

## Randomized actor loadouts

Cada NPC debe poder aparecer con equipment e inventory distintos a partir de datos JSON.

El sistema debe admitir probabilidades reales, incluyendo explícitamente la posibilidad de `none`/vacío.

Ejemplo conceptual, no schema congelado:

```text
Head:
  40% wool_cap
  15% helmet
  45% none

Back:
  20% small_backpack
  10% large_backpack
  70% none

Primary weapon:
  55% bolt_action
  20% semi_auto
  10% automatic
  15% none
```

Las loot tables v0 existentes son determinísticas y hoy sólo modelan `item_id + count`; no tienen chance/weights/rarity. Antes de implementar M41.3 se debe auditar el contrato real y elegir la extensión mínima correcta.

Dirección preferida: un `Actor Loadout Table/Profile` o equivalente data-driven que pueda expresar equipment, inventory, probabilidades y `none`, en vez de deformar las loot tables de contenedores si sus semánticas no coinciden.

No congelar el nombre/schema exacto antes de auditar consumidores.

## Reglas del random loadout

El resultado concreto de un spawn debe convertirse en estado real del actor:

- equipment real en `ActorEquipmentComponent`;
- items reales con `ItemInstanceId`;
- ownership real;
- backpack/item-owned storage real;
- inventory real;
- ammo compatible con el arma seleccionada cuando corresponda;
- cadáver debe conservar exactamente las pertenencias del actor, no regenerar loot al morir.

Una vez generado el actor, su contenido no debe volver a rerollearse al abrir el cadáver o consultar inventory.

La semántica final de determinismo de spawns productivos queda futura. Para el sandbox es válido un RNG de prueba controlable/reproducible, pero debe ser posible registrar la seed/roll evidence para diagnosticar un spawn concreto.

## Roaming básico

El NPC del sandbox debe desplazarse de manera simple para probar navegación real. No se requiere comportamiento complejo.

Puede usar destinos locales elegidos dentro de una región válida y navegar mediante `ActorNavigationController`.

Objetivos:

- comprobar NavMesh en la representación física nueva;
- comprobar slopes/borders/colliders;
- observar múltiples actores moviéndose;
- detectar rápidamente zonas sin navegación o spawn inválido.

No implementar strategic AI, schedules, needs AI ni pathfinding mundial.

## Damage, death y loot

El NPC debe reutilizar exactamente los sistemas existentes:

- seis regiones corporales;
- wounds/bleeding/pain de M39;
- M40/M40.1 para impactos/armor;
- lifecycle Alive/Dead;
- corpse continuity;
- equipment/inventory/ownership ya reales;
- corpse belongings/loot existente.

Debe ser posible:

`Spawn NPC → inspeccionar loadout → dañarlo en regiones distintas → matarlo → abrir cadáver → encontrar el mismo equipment/inventory que poseía`.

No crear health simplificado especial para NPC sandbox.

## Aceptación M41.3

- varios spawns producen loadouts distintos de acuerdo con datos/probabilidades;
- `none` es un resultado válido en categorías configuradas;
- no hay reroll al morir/lootear;
- equipment e inventory del cadáver son exactamente los del actor vivo;
- navegación básica funciona sobre el mapa real;
- actor recibe daño localizado y puede morir;
- corpse loot/ownership no duplica ni pierde items;
- spawn inválido/NavMesh failure queda diagnosticable;
- no existe una autoridad paralela de actor, equipment, health, navigation o loot.

---

# M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1

Estado planificado: `PLANNED — AFTER M41.3`

## Objetivo

Permitir un sandbox de combate observable NPC↔NPC y NPC↔Player usando las autoridades ya implementadas, sin aimbot perfecto y sin reescribir M41.1.

## Blue / Red debug actors

Agregar controles development-only equivalentes a:

- `Spawn Blue NPC`;
- `Spawn Red NPC`.

Los colores son únicamente presentación/debug. La lógica no debe depender de `if color == red`.

Debe existir una representación mínima de affiliation/team/disposition genérica suficiente para expresar relaciones del sandbox.

Baseline de prueba:

- Blue no es hostil al Player;
- Red considera hostiles a Blue y Player;
- Red vs Red no debe atacarse por defecto;
- Blue vs Blue no debe atacarse por defecto.

La arquitectura debe poder extenderse a relaciones futuras sin convertir esta prueba en el sistema completo de facciones M46.1.

## Threat acquisition

M41.1 actualmente puede combatir una amenaza explícitamente asignada. M41.4 debe agregar la capa mínima para que un actor encuentre candidatos hostiles cercanos y use `ActorVisualPerceptionService` antes de asignar/actualizar amenaza.

No permitir omnisciencia.

Cadena deseada:

`affiliation/disposition → nearby candidate → perception/LOS → threat assignment → existing HumanEncounterAIController → Navigation/WeaponCombatService`.

No crear otro combat brain ni otro perception service.

## Range-aware combat

La IA debe entender el alcance del arma actualmente equipada.

Para firearm:

- nunca intentar resolver daño más allá de `firearm.range`;
- usar un `preferred engagement distance` coherente y nunca mayor al rango físico;
- si la amenaza está demasiado lejos, navegar para cerrar distancia;
- entrar en Fighting sólo cuando exista una condición de engagement válida;
- si el target sale del rango/LOS, responder mediante navegación/lost-contact existentes.

Para melee:

- usar `melee_range`;
- si el enemigo está lejos, acercarse;
- sólo golpear cuando está físicamente a distancia válida;
- no proyectar un melee ray kilométrico.

El comportamiento exacto de mantener distancia, retroceder o buscar cover queda fuera salvo que sea necesario para corregir una falla evidente del sandbox.

## Imperfect aim — no aimbot

Los NPC no deben disparar siempre al punto exacto de la cabeza ni convertir una probabilidad abstracta directamente en `hit/miss`.

Dirección preferida:

`target observado → punto objetivo aproximado → error angular físico → dirección de disparo real → PhysicalShotPathResolver → lo que golpea físicamente recibe el impacto`.

Esto permite misses reales, impactos accidentales en otras regiones, paredes u otros actores.

Baseline V1 propuesto:

- apuntar principalmente a center mass;
- selección/variación limitada de región objetivo para aprovechar localized health;
- head no debe ser objetivo dominante;
- reaction/acquisition delay antes de precisión útil;
- focus time: el spread disminuye mientras conserva una observación válida;
- nunca llegar a spread perfecto cero para un NPC normal;
- mayor distancia aumenta error;
- movimiento/cambio brusco de target puede aumentar error;
- arma/fire mode puede aportar accuracy/spread data;
- automatic fire puede aumentar spread durante una ráfaga y recuperarlo al pausar si es proporcional al scope.

No implementar una simulación balística completa todavía. El error angular debe poder alimentar una futura trayectoria física sin tirar este trabajo.

## Referencias de diseño investigadas

Estas referencias sirven para entender patrones, no para copiar implementaciones ni valores:

- Valve Source SDK / Half-Life 2 AI: reaction delays, focus time y spread defocused/focused. `ai_basenpc.cpp` en Source SDK 2013.
  - https://github.com/ValveSoftware/source-sdk-2013/blob/master/src/game/server/ai_basenpc.cpp
- Valve Developer Community: weapon bullet spread cones y comportamiento de Combine Soldier según arma/rango.
  - https://developer.valvesoftware.com/wiki/Authoring_a_weapon_entity
  - https://developer.valvesoftware.com/wiki/AI_Learning:_CombineSoldier
- Bohemia Interactive / Arma 3 AI Config Reference: fire modes con `minRange`, `midRange`, `maxRange` y probabilidades asociadas.
  - https://community.bohemia.net/wiki/Arma_3:_AI_Config_Reference
- Bungie GDC 2002 Halo AI: engagement distance como parte crítica de la lectura/calidad del comportamiento.
  - https://halo.bungie.org/misc/gdc.2002.haloai/talk.html?page=19

Principio común adoptado para Old Scars:

`weapon capability + physical range + perception + reaction/focus + aim error + navigation` son dimensiones separadas que se integran; ninguna debe convertirse en un aimbot o en un porcentaje mágico de daño.

## Observabilidad obligatoria

Para evitar repetir las fixtures cerradas anteriores, el sandbox debe poder mostrar en development build información explicable de un actor seleccionado o cercano, por ejemplo:

```text
Actor: actor_...
Affiliation: red
Target: player
Distance: 41.3 m
Weapon: ...
Weapon range: 35 m
Preferred range: 28 m
State: Closing Distance
Perception: Perceived/Occluded/LostContact
Aim focus: 0.42
Spread: 5.1 deg
Navigation: Moving
```

No es UI final. La meta es poder mirar la partida y entender por qué una IA se acerca, espera, falla o dispara.

## Aceptación M41.4

Debe demostrarse dentro de WorldRuntime:

- Blue/Red spawneables con loadouts M41.3;
- relaciones debug genéricas, no color hardcodeado;
- adquisición automática de amenazas mediante percepción/LOS;
- Red puede atacar Player y Blue;
- Blue no ataca Player en baseline;
- firearm users cierran distancia cuando están fuera de alcance;
- melee users se acercan hasta rango de golpe;
- no hay disparos físicos más allá del rango del arma;
- aim tiene error físico visible y no aimbot;
- diferentes disparos pueden fallar o golpear distintas regiones;
- existing M39/M40/M40.1 siguen resolviendo heridas/armor/death;
- actor muerto deja su loadout real para loot;
- navigation funciona suficientemente sobre el terrain/materialización vigente;
- comportamiento es observable/debuggable sin depender únicamente de automated PASS.

---

# Secuencia De Ejecución Aprobada

Estado actual de la secuencia:

`M41.2 Basic Equipment & Weapon Coverage V1 — DONE / VALIDATED`

→

`M41.3 NPC Sandbox Spawn & Randomized Loadouts V1 — AUTHORIZED / ACTIVE NEXT`

→

`M41.4 Affiliation, Range-Aware Combat & Imperfect Aim V1 — PLANNED`

→

`PLAYTEST / REVIEW BEFORE AUTHORIZING ANOTHER LARGE SYSTEM`

No encadenar automáticamente otro milestone después de M41.4. El resultado debe evaluarse jugando y observando varios NPC simultáneamente.

## Resultado esperado de la secuencia

Una prueba exitosa debería permitir:

`entrar al WorldRuntime → spawnear varios NPCs con equipment/inventory diferentes → verlos navegar → Blue/Red descubrir enemigos → acercarse según alcance del arma → disparar/golpear con precisión imperfecta → recibir heridas localizadas/armor → morir → lootear exactamente sus pertenencias`.

El objetivo estratégico es comprobar que las foundations de Old Scars forman un pequeño ecosistema jugable integrado y detectar deuda real antes de seguir agregando worldgen o sistemas abstractos.

---

# Fuera De Alcance De M41.2–M41.4

- modelos/animaciones/sonido final de todas las prendas y armas;
- production UI;
- producción masiva de content;
- sistema completo de facciones/reputación/memoria regional;
- cover AI sofisticada;
- squads/tactics;
- strategic AI/off-sector combat;
- full ballistic trajectory, bullet drop o wind;
- ricochet/spall más allá de contratos existentes;
- suppressive fire avanzado;
- morale completo;
- schedules/jobs/needs AI;
- settlement simulation;
- final loot economy/balance;
- final spawn ecology;
- procedural NPC population mundial;
- streaming AI de producción;
- dynamic navigation architecture final para terreno deformado;
- reescritura de `HumanEncounterAIController`, `ActorNavigationController`, `ActorVisualPerceptionService`, `WeaponCombatService`, M39 health o ownership/equipment.

La regla es reutilizar y estresar las autoridades existentes antes de sustituirlas.
