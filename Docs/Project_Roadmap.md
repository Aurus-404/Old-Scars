# Old Scars - Project Roadmap

Este documento es la fuente principal del roadmap vivo de Old Scars.

## Estado Actual Resumido

Old Scars tiene una base debug/prototipo validada para:

- carga data-driven desde `StreamingAssets/Mods/Core`;
- evaluacion de acciones contextuales desde JSON;
- ejecucion de effects cerrados por C#;
- interacciones contextuales con UI debug;
- tags iniciales y runtime tags en objetos del mundo;
- movimiento point-and-click;
- gravedad debug con CharacterController;
- rango de interaccion;
- CameraRig con WASD, right-drag rotation y zoom;
- acciones contextuales con duracion debug usando `ActionDefinition.cost.time`;
- item instances runtime-only y DebugInventory debug validados en Unity;
- limpieza tecnica de escena validada en Unity;
- evaluacion auditable de requisitos de herramienta equipada validada en Unity.
- inventario jugable v0 y pickup loop validados en Unity.
- container loot v0 validado en Unity.
- primer POI jugable compacto validado en `SampleScene`.

Milestone 16 esta validado en Unity.

## Estados Permitidos

- `planned`
- `in progress`
- `implemented`
- `validated`
- `blocked`

## Tabla De Milestones

| Milestone | Objetivo | Estado | Nota de validacion |
| --- | --- | --- | --- |
| CoreDataSystem | Cargar y validar definiciones JSON desde Mods/Core. | validated | Base usada por todos los milestones posteriores. |
| ActionAvailabilitySystem | Evaluar acciones por actor, target, stats e item equipado. | validated | Funciona con acciones world_interaction y combat debug. |
| Milestone 6: Basic Contextual Interaction UI | Agregar examine_object, show_target_info y resultado debug. | validated | Probado con objetos inspectable sin item equipado. |
| Milestone 7: Extract InteractionSystem | Sacar evaluacion de interacciones de WorldInteractionDebugTester. | validated | InteractionSystem evalua acciones desde InteractionQuery. |
| Milestone 8: Actor Interaction Context | Reemplazar DebugPlayerContext en el flujo principal. | validated | ActorInteractionContext es la base minima de actor interactuante. |
| Milestone 9: Point-and-Click Debug Movement + Camera Rig | Agregar movimiento debug y camara separada. | validated | Validado: point-and-click, UI click blocking, WASD pan y right-drag rotation. |
| Milestone 9.1: Movement / Interaction / Camera Polish | Pulir movimiento, proximidad y zoom. | validated | Validado: CharacterController gravity, interaction range y mouse wheel zoom. |
| Milestone 10: Stateful Contextual Actions Hardening | Formalizar runtime tags sin crear save/world state. | validated | Validado: initial tags quedan como configuracion, runtime tags mutan durante Play. |
| Milestone 11: Action Duration / Action In Progress | Agregar ejecucion temporal debug de acciones. | validated | Validado: force_door 3s, pry_open_container 2s, examine_object 1s; effects se aplican al terminar. |
| Milestone 12: Item Instances + Debug Inventory | Introducir ItemInstance runtime-only y DebugInventory debug. | validated | Validado: ItemInstance runtime-only, DebugInventory crea instancias runtime, herramienta equipada habilita acciones y sin item las bloquea. |
| Milestone 12.1: Technical Cleanup | Limpiar escena debug y documentar legacy sin tocar sistemas validados. | validated | Validado: GameDataManager root, sin warning DontDestroyOnLoad, CoreDataSystem ready y sistemas validados intactos. |
| Milestone 13: Tool Requirement Hardening | Hacer auditable y robusta la evaluacion de requisitos de herramienta equipada. | validated | Validado: Evaluate explicable, logs opcionales, requisitos de herramienta correctos y sistemas validados intactos. |
| Milestone 14: Playable Inventory + Pickup Loop | Crear inventario v0, item pickup de mundo y equip simple por UI debug. | validated | Validado: iniciar sin item, recoger palanca, verla con I, equiparla y habilitar acciones de herramienta. |
| Milestone 15: Container Loot v0 | Saquear contenedores abiertos y agregar loot al InventoryComponent. | validated | Validado: LootTableDefinition v0, carga de loot_tables, search_container, Scrap Metal en inventario y bloqueo de loot infinito. |
| Milestone 16: Primer POI jugable completo | Ordenar `SampleScene` como una bahia de mantenimiento compacta que combine pickup, inventario, herramienta equipada, puerta, contenedor, loot y maquina examinable. | validated | Validado: SampleScene funciona como primer POI jugable compacto con loop completo y estados runtime correctos. |

## Milestone Actual

No hay milestone implementado pendiente de validacion.

El ultimo milestone cerrado como `validated` es:

- Milestone 16: Primer POI jugable completo (`validated`).

## Proximo Recomendado

Preparar el siguiente sprint sobre la base validada de Milestone 16.

Milestone 11 dejo validado:

- representar una accion en progreso;
- usar `cost.time` como duracion debug;
- bloquear doble ejecucion mientras una accion esta activa;
- mantener UI debug y alcance chico;
- no crear combate, inventario real, save system ni sistema final de animaciones.

Milestone 12 dejo validado:

- `ItemInstance` runtime-only funciona;
- `DebugInventory` crea instancias runtime desde `ItemDefinition`;
- `ActorInteractionContext` usa `DebugInventory` como fuente principal cuando esta asignado;
- `equippedItemDefinitionId` legacy solo se usa si no hay `DebugInventory` asignado;
- con `rusted_crowbar_01` equipado aparecen `force_door` y `pry_open_container`;
- con `equippedItemIndex = -1` aparece `Equipped item: (none)` y no se muestran acciones de herramienta;
- `InteractionSystem` sigue recibiendo solo definition_id y no depende de `DebugInventory`, `ItemInstance` ni MonoBehaviour;
- Milestone 11 sigue funcionando: duracion de acciones y runtime tags siguen correctos.

Milestone 12.1 dejo validado:

- `GameDataManager` quedo como root GameObject;
- el warning de `DontDestroyOnLoad` ya no aparece;
- `CoreDataSystem` carga correctamente;
- `DebugInventory` quedo verificado en `Debug Player`;
- `Deprecated_ActorInteractionContext_Legacy` quedo desactivado y aislado;
- `DebugPlayerContext`, `GameDataDebugTester`, `ActionAvailabilityDebugTester` y `ActorInteractionContext.EquippedItemDefinitionId` quedan documentados como legacy/deprecated;
- `_Recovery` se mantiene;
- no se borraron scripts legacy;
- no se tocaron codigo ni JSON;
- movimiento, camara, UI blocker, action duration, runtime tags, DebugInventory e InteractionSystem siguen funcionando.

Milestone 13 dejo validado:

- `requirements.weapon_tags` se mantiene como campo activo y legacy compatible;
- `weapon_tags` queda documentado como required equipped item tags;
- no se agrego `required_item_tags` ni se migro schema;
- no se toco JSON;
- no se cambio la semantica OR de `weapon_tags`;
- `ActionAvailabilityEvaluator.IsAvailable()` se mantiene compatible;
- `ActionAvailabilityEvaluator.Evaluate()` devuelve una evaluacion explicable;
- `InteractionSystem` sigue devolviendo solo acciones disponibles;
- logs detallados de disponibilidad son opcionales y controlados desde `WorldInteractionDebugTester.logAvailabilityDetails`;
- `DataValidator` agrega warning no destructivo si un `weapon_tags` valido no aparece en ningun item cargado;
- `DataValidator` no bloquea la carga;
- con palanca equipada, `force_door` y `pry_open_container` aparecen correctamente;
- sin item equipado, las acciones de herramienta se bloquean correctamente;
- action duration y runtime tags siguen funcionando;
- no se tocaron DebugInventory, ItemInstance, action duration ni runtime tags.

Milestone 14 dejo validado:

- `InventoryComponent` v0 runtime-only funciona;
- el jugador inicia sin item equipado;
- `InventoryComponent` usa una lista plana de `ItemInstance` y equip simple por indice;
- `ActorInteractionContext` prioriza `InventoryComponent`, luego `DebugInventory`, luego `equippedItemDefinitionId` legacy;
- si `InventoryComponent` existe y no tiene item equipado, se considera sin item y no se usa fallback;
- `InventoryDebugPanel` OnGUI abre/cierra con `I`, muestra items, permite equipar y unequip;
- `WorldItemPickup` funciona con `rusted_crowbar_01`;
- `pick_up_item` dura 0.5s;
- al recoger, el item se agrega al `InventoryComponent`;
- la palanca del mundo queda oculta/no interactuable;
- al equipar la palanca, `force_door` y `pry_open_container` aparecen correctamente;
- `pick_up_item` es un effect cerrado de C# permitido por JSON;
- tags nuevos: `world_item`, `pickupable`, `picked_up`;
- `DebugActionExecutionContext` pasa actor, target y item equipado hacia el executor;
- `InteractionSystem` sigue sin depender de `InventoryComponent`, UI, `WorldItemPickup`, `DebugInventory`, `ItemInstance` ni MonoBehaviour;
- action duration y runtime tags siguen funcionando;
- no se toco `items.json`;
- no se creo inventario final, drag/drop, grid, peso/capacidad real, save system, loot aleatorio, contenedores reales, equipment slots reales, UI final, combate ni IA.

Milestone 15 dejo validado:

- `LootTableDefinition` v0 funciona;
- `GameDataLoader` carga `loot_tables/*.json`;
- `GameDatabase` registra y expone loot tables;
- `DataValidator` valida loot tables sin errores y permite el effect cerrado `search_container`;
- `container_loot.json` carga `debug_sealed_container_loot_01`;
- `ContainerLootComponent` ejecuta el saqueo usando `DebugActionExecutionContext` e `InventoryComponent`;
- `search_container` aparece solo con `opened_container` + `lootable_container`;
- `search_container` dura 1.5s;
- `search_container` agrega `scrap_metal_01` al `InventoryComponent`;
- `InventoryDebugPanel` muestra `Scrap Metal`;
- al saquear, se remueve `lootable_container` y se agrega `looted_container`;
- `search_container` ya no aparece despues de saquear;
- `InteractionSystem` sigue sin depender de inventario, loot ni MonoBehaviour;
- no se creo loot avanzado, UI final, save system, stacks, economia, crafting, combate ni IA.

Milestone 16 dejo validado:

- `SampleScene` funciona como primer POI jugable compacto tipo bahia de mantenimiento industrial;
- el POI usa solo sistemas existentes: movimiento point-and-click, camara, inventario v0, pickup, herramienta equipada, acciones con duracion, runtime tags, loot tables v0 y container loot v0;
- `Debug Player` inicia dentro del POI con `InventoryComponent` vacio y sin item equipado;
- `Debug World Crowbar` funciona como herramienta inicial recogible;
- `Debug Locked Door` funciona como obstaculo forzable con palanca;
- `Debug Sealed Container` funciona como contenedor sellado, abrible y saqueable;
- `Debug Strange Machine` funciona como objeto ambiental examinable;
- el loop completo funciona: recoger palanca -> equipar -> abrir/forzar obstaculo -> abrir contenedor -> buscar loot -> obtener Scrap Metal -> dejar estados runtime correctos;
- runtime validado de palanca: `picked_up` agregado y `pickupable` removido;
- runtime validado de puerta: `forced_open` agregado y `locked_door` removido;
- runtime validado de contenedor abierto: `opened_container` agregado y `sealed_container` removido;
- runtime validado de contenedor saqueado: `looted_container` agregado y `lootable_container` removido;
- data load sigue OK con 0 errors y 0 warnings;
- `InteractionSystem` sigue desacoplado;
- no se toco codigo;
- no se toco JSON;
- no se crearon sistemas nuevos;
- no se rompieron `InventoryComponent`, `WorldItemPickup`, `ContainerLootComponent`, action duration, runtime tags ni loot tables.

## Milestones Pospuestos / No Tocar Todavia

- combate real;
- IA;
- facciones;
- mapa grande;
- vehiculos;
- crafting completo;
- UI final;
- dialogos complejos;
- procedural world;
- save system avanzado.

## Reglas De Cierre De Milestone

Un milestone solo puede pasar a `validated` cuando:

- compila en Unity;
- fue probado manualmente en Unity;
- el usuario confirmo que funciona;
- la documentacion quedo actualizada.

Si el codigo fue implementado pero falta confirmacion del usuario en Unity, el estado correcto es `implemented`.

## Decisiones Tecnicas Vigentes

- JSON define contenido.
- C# ejecuta logica.
- IDs conectan archivos.
- Tags conectan sistemas.
- `Mods/Core` representa el contenido base oficial del juego y carga primero.
- Core debe funcionar como mod interno de ejemplo.
- Las definitions viven en JSON.
- Las instancias viven en runtime o en un futuro sistema de guardado.
- `ItemInstance` es runtime-only y no es save data.
- `DebugInventory` es debug temporal y no es inventario final.
- `InventoryComponent` es inventario jugable v0, no inventario final.
- `ActorInteractionContext` resuelve item equipado con prioridad `InventoryComponent` -> `DebugInventory` -> `equippedItemDefinitionId` legacy.
- Si `InventoryComponent` esta asignado al actor, define exclusivamente el item equipado; si devuelve sin item, no se usa fallback.
- Si no hay `InventoryComponent` y `DebugInventory` esta asignado, `DebugInventory` define el item equipado; si devuelve sin item, no se usa fallback legacy.
- `requirements.weapon_tags` es el campo activo para requisitos de tags del item equipado.
- `weapon_tags` es un nombre legacy compatible; una migracion futura podria introducir `required_item_tags`, pero no existe todavia.
- Tags iniciales de `WorldObjectTags` son configuracion del Inspector.
- Runtime tags de `WorldObjectTags` son estado mutable solo durante Play.
- `add_tag` y `remove_tag` afectan al target en runtime.
- `show_target_info` es un effect cerrado que lee `WorldObjectDebugInfo`.
- `pick_up_item` es un effect cerrado que ejecuta `WorldItemPickup` y agrega una `ItemInstance` al `InventoryComponent` del actor.
- `search_container` es un effect cerrado que ejecuta `ContainerLootComponent` y agrega loot v0 al `InventoryComponent` del actor.
- `LootTableDefinition` v0 es deterministica: solo `item_id` y `count`.
- No hay scripting libre dentro de JSON.
- No hay inventario final todavia.
- No hay loot final ni avanzado todavia.
- No hay save system todavia.
- No hay combate real todavia.
- Movimiento validado: point-and-click sobre Ground.
- Debug Player usa CharacterController.
- Interacciones contextuales requieren proximidad.
- Camara usa CameraRig con pan, rotacion y zoom.
- `ActionDefinition.cost.time` se usa como duracion debug de acciones contextuales.
- `DebugActionExecutor` sigue siendo sincronico y aplica effects solo al terminar la duracion.

## Sistemas Existentes

- `GameDataLoader`: carga JSON desde mods.
- `GameDatabase`: guarda definiciones cargadas.
- `TagRegistry`: registra tags validos.
- `DataValidator`: valida IDs, types, tags, referencias, effects, loot tables y warnings no destructivos de `weapon_tags`.
- `ActionAvailabilityEvaluator`: evalua requirements y puede devolver resultado explicable.
- `InteractionSystem`: arma contexto y devuelve acciones disponibles.
- `ActorInteractionContext`: datos minimos del actor para interactuar.
- `ItemInstance`: instancia runtime-only minima de un item.
- `LootTableDefinition`: definicion v0 de loot deterministico.
- `InventoryComponent`: inventario v0 runtime-only con lista plana de item instances y equip por indice.
- `DebugInventory`: inventario debug temporal para crear item instances y exponer item equipado.
- `InventoryDebugPanel`: UI debug OnGUI de inventario v0.
- `WorldItemPickup`: componente debug para recoger un item de mundo configurado.
- `ContainerLootComponent`: componente debug para saquear contenedores abiertos.
- `WorldObjectTags`: initial tags y runtime tags.
- `WorldObjectDebugInfo`: texto debug para examinar objetos.
- `ActionAvailabilityResult`: resultado explicable de disponibilidad de acciones.
- `DebugActionProgressController`: controla acciones debug en progreso.
- `DebugActionExecutor`: ejecuta effects debug cerrados.
- `DebugActionExecutionContext`: contexto minimo para pasar actor, target e item equipado al executor.
- `ContextualActionDebugPanel`: menu contextual debug OnGUI.
- `ContextualActionDebugProgressPanel`: feedback debug de accion en progreso.
- `ContextualActionDebugResultPanel`: resultado debug OnGUI.
- `PointClickMovementController`: movimiento debug con CharacterController.
- `PointClickMovementInputController`: input de movimiento por click izquierdo.
- `DebugWorldUiInputBlocker`: bloqueo debug de clicks cuando hay UI abierta.
- `CameraRigController`: pan, rotacion y zoom de camara.

## Deuda Tecnica Menor

- No hay deuda tecnica menor bloqueante registrada.
- No hay deuda tecnica menor bloqueante registrada despues de Milestone 13.

## Sistemas Que Todavia NO Existen

- inventario real;
- loot final o avanzado;
- contenedores reales;
- save system;
- world state persistente;
- combate real;
- IA;
- pathfinding/NavMesh;
- sistema de dialogos;
- POIs multiples o de produccion;
- equipment system real;
- UI final;
- crafting completo;
- facciones;
- vehiculos.

## Checklist De Incongruencias Para Propuestas Futuras

Antes de proponer o implementar algo, verificar:

- si reimplementa algo ya validado;
- si contradice restricciones actuales;
- si toca JSON sin necesidad;
- si crea sistemas grandes prematuros;
- si invade inventario/save/loot/combate/IA antes de tiempo;
- si respeta initial tags vs runtime tags;
- si respeta point-and-click movement;
- si respeta interaction range;
- si respeta CameraRig;
- si mantiene JSON como datos y C# como logica cerrada.
