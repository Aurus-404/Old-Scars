# Old Scars - Development Log

## Hecho

### CoreDataSystem

- Carga JSON desde StreamingAssets/Mods/Core.
- Carga tags, items, weapon_profiles y actions.
- Valida IDs, types, tags y referencias.
- Guarda definiciones en GameDatabase.
- Permite consultar definiciones por ID.

### ActionAvailabilitySystem

- Evalua ActionDefinition contra un ActionAvailabilityContext.
- Usa actor tags, actor stats, target tags e item tags.
- actor_tags, target_tags y actor_min_stats deben cumplirse todos.
- weapon_tags requiere al menos uno.

### Contextual Menu Debug

- WorldInteractionDebugTester usa New Input System.
- Click derecho sobre WorldObjectTags abre menu contextual debug.
- El menu usa OnGUI y aparece cerca del mouse.
- Click izquierdo fuera del menu lo cierra.
- Escape lo cierra.
- Click derecho en vacio no lo cierra.

### Action Effects Desde JSON

- ActionDefinition puede declarar effects opcional.
- Effects soportados por ahora:
  - add_tag;
  - remove_tag;
  - show_target_info;
  - pick_up_item;
  - search_container;
  - apply_damage;
  - kill_actor;
  - search_actor_inventory.
- Target soportado por ahora:
  - target.
- Esto no es scripting libre dentro de JSON.

### Runtime Tag Mutation

- WorldObjectTags tiene tags iniciales configurables en Inspector.
- En runtime usa una lista interna.
- Expone HasTag, AddTag, RemoveTag y Tags/GetTags.

### DebugPlayerContext

- Componente debug configurable en Inspector.
- Define actor tags.
- Define actor stats.
- Define debug equipped item id.
- Desde Milestone 8 queda como legacy debug temporal fuera del flujo principal.
- Defaults:
  - actor tags: player, human;
  - strength = 4;
  - debug equipped item id: rusted_crowbar_01.

### Segundo Caso De Interaccion

- Debug Locked Door:
  - locked_door -> force_door -> forced_open.
- Debug Sealed Container:
  - sealed_container -> pry_open_container -> opened_container.

### No Equipped Item Handling

- debug equipped item id vacio, whitespace o none significa sin item equipado.
- No se busca item en GameDatabase.
- itemTags queda vacio.
- No hay error.
- Si el item id no existe y no es none, se loguea warning y se sigue evaluando.

### Basic Contextual Interaction UI

- Agregada action examine_object para world_interaction.
- examine_object requiere target tag inspectable.
- examine_object no requiere item equipado, weapon_tags ni actor_min_stats.
- Agregado tag inspectable.
- Agregado WorldObjectDebugInfo con displayName e inspectText.
- Agregado ContextualActionDebugResultPanel con OnGUI debug.
- DebugActionExecutor ahora devuelve DebugActionExecutionResult y no depende de UI.
- ContextualActionDebugPanel decide si muestra el resultado.
- Agregado effect show_target_info como effect cerrado permitido por C#.
- show_target_info solo usa target y no muta tags.

### Extract InteractionSystem

- Agregado InteractionSystem como clase C# simple, no MonoBehaviour.
- Agregada InteractionQuery para pasar datos preparados al sistema.
- InteractionSystem no depende directamente de DebugPlayerContext.
- InteractionSystem resuelve item equipado, incluyendo none/vacio como sin item.
- InteractionSystem construye ActionAvailabilityContext, filtra actions por context y usa ActionAvailabilityEvaluator.
- WorldInteractionDebugTester queda como coordinador debug de input, raycast, lectura de DebugPlayerContext y envio de acciones al panel.
- No se modificaron JSON ni ejecucion de effects.
- Probado en Unity: con none aparecen solo acciones sin herramienta; con rusted_crowbar_01 aparecen acciones con herramienta mas examine_object.
- Probado en Unity: force_door y pry_open_container siguen mutando tags, y examine_object muestra informacion sin mutar tags.

### Actor Interaction Context

- Agregado ActorInteractionContext como MonoBehaviour minimo para datos de interaccion de actor.
- ActorInteractionContext expone actor tags, actor stats y equipped item definition id.
- ActorInteractionContext es ahora la base minima para actores que interactuan.
- Defaults: actor tags player/human, strength = 4, equipped item definition id rusted_crowbar_01.
- WorldInteractionDebugTester ahora usa ActorInteractionContext para construir InteractionQuery.
- DebugPlayerContext se mantiene como legacy debug temporal, pero salio del flujo principal.
- InteractionSystem no fue modificado.
- InteractionSystem sigue evaluando acciones desde InteractionQuery.
- No se agregaron inventario, equipment system, entity system, player final, save system ni cambios JSON.
- Probado en Unity: con rusted_crowbar_01, puerta muestra force_door + examine_object; contenedor muestra pry_open_container + examine_object; maquina muestra examine_object.
- Probado en Unity: con none, puerta/contenedor/maquina muestran solamente examine_object.
- Probado en Unity: force_door y pry_open_container siguen mutando tags, y examine_object muestra informacion sin mutar tags.

### Point-and-Click Debug Movement + Camera Rig

- Agregado PointClickMovementController para mover un actor hacia un target en linea recta.
- Agregado PointClickMovementInputController para click izquierdo sobre Ground.
- Agregado DebugWorldUiInputBlocker para consumir click izquierdo cuando hay UI debug abierta.
- Agregado CameraRigController para pan con WASD, rotacion con click derecho drag y recentrado opcional.
- WorldInteractionDebugTester ahora distingue click derecho corto de click derecho drag.
- Click derecho corto sobre Interactable abre menu contextual.
- Click derecho drag rota camara y no abre menu contextual al soltar.
- ContextualActionDebugResultPanel ahora permite detectar si un click cae dentro del panel.
- No se agregaron NavMesh, pathfinding, PlayerController final, auto-interaccion, stamina, combate ni cambios JSON.
- Validado en Unity: point-and-click debug movement funciona.
- Validado en Unity: UI click blocking cierra paneles abiertos con click izquierdo fuera de UI y no mueve al actor ese frame.
- Validado en Unity: click derecho corto y click derecho drag quedan separados correctamente.
- Validado en Unity: CameraRigController funciona con WASD pan y right-drag rotation.
- No se modifico InteractionSystem.

### Movement / Interaction / Camera Polish

- PointClickMovementController ahora usa CharacterController.Move.
- Debug Player usa CharacterController para movimiento debug.
- Agregada gravedad basica con verticalVelocity y gravity configurable.
- WorldInteractionDebugTester ahora usa interactionRange configurable antes de abrir menu contextual.
- La distancia de interaccion usa Collider.ClosestPoint cuando hay collider disponible.
- CameraRigController ahora permite zoom con rueda del mouse.
- El zoom mueve la camara hija en localPosition y respeta minZoomDistance/maxZoomDistance.
- Validado en Unity: CharacterController gravity funciona.
- Validado en Unity: interaction range/proximity gating bloquea menu contextual fuera de rango.
- Validado en Unity: mouse wheel camera zoom funciona.
- No se tocaron JSON ni InteractionSystem.
- Si el Debug Player conserva un Capsule Collider duplicado, conviene quitarlo o desactivarlo para evitar colisiones dobles.

### Stateful Contextual Actions Hardening

- Milestone 10 es hardening del sistema existente de runtime tags.
- No crea Stateful Contextual Actions desde cero: formaliza la base que ya funcionaba.
- WorldObjectTags mantiene el campo serializado tags como configuracion inicial del Inspector.
- WorldObjectTags mantiene runtimeTags como copia mutable durante Play.
- Agregadas propiedades/metodos explicitos: InitialTags, RuntimeTags, Tags, GetRuntimeTags, GetInitialTags y ResetRuntimeTagsFromInitial.
- Tags se mantiene como alias compatible de runtime tags para no romper InteractionSystem ni llamadas existentes.
- Las mutaciones HasTag, AddTag y RemoveTag operan sobre runtimeTags.
- DebugActionExecutor sigue aplicando add_tag/remove_tag solo al target.
- DebugActionExecutor ahora loguea tag agregado, tag ya existente, tag removido y tag inexistente.
- DebugActionExecutor loguea initial tags y runtime tags despues de effects que mutan tags.
- No se tocaron JSON ni InteractionSystem.
- No es save system.
- No es world state persistente.
- Los cambios de tags existen solo durante Play.
- Validado en Unity.

### Documentation Roadmap Reorganization

- Se creo Docs/Project_Roadmap.md como fuente principal del roadmap vivo.
- Se creo Docs/Milestone_Template.md como plantilla estandar para futuros milestones.
- Se ordeno Docs/Current_Milestone.md como snapshot compacto del estado actual.
- Se ordeno Docs/Next_Sprints.md como backlog de sprints recomendados.
- Se actualizo AGENTS.md para exigir lectura del roadmap antes de proponer o implementar sprints.
- Milestone 9, Milestone 9.1 y Milestone 10 quedaron registrados como validated.
- Proximo recomendado registrado: Milestone 11 Action Duration / Action In Progress.
- No se toco codigo.
- No se toco JSON.
- No se cambio gameplay.

### Action Duration / Action In Progress

- Milestone 11 implementado y validado como sistema debug de acciones en progreso.
- Agregado DebugActionProgressController para manejar accion activa, target, item equipado, duracion, elapsed y progreso.
- Agregado ContextualActionDebugProgressPanel con OnGUI debug simple.
- ContextualActionDebugPanel ya no ejecuta acciones directamente; ahora inicia acciones mediante DebugActionProgressController.
- DebugActionExecutor sigue siendo sincronico y solo ejecuta effects al finalizar la duracion.
- WorldInteractionDebugTester bloquea apertura de menu contextual mientras hay accion activa.
- PointClickMovementInputController bloquea nuevos clicks de movimiento mientras hay accion activa.
- La camara queda libre durante acciones activas.
- Se usa ActionDefinition.cost.time como duracion debug.
- Validado en Unity: force_door dura 3s y aplica effects al terminar.
- Validado en Unity: pry_open_container dura 2s y aplica effects al terminar.
- Validado en Unity: examine_object dura 1s y muestra info al terminar.
- Validado en Unity: durante accion activa no se puede iniciar otra accion.
- Validado en Unity: durante accion activa no se abre otro menu contextual.
- Validado en Unity: durante accion activa no se aceptan nuevos clicks de movimiento.
- Validado en Unity: la camara sigue libre.
- No se toco JSON.
- No se toco DataValidator.
- No se reimplementaron runtime tags.
- No se agregaron combate, inventario real, loot, save system, IA, animaciones finales, UI final, cancelacion ni interrupciones.
- Estado: validated.

### Item Instances + Debug Inventory

- Milestone 12 implementado y validado como base debug/runtime para item instances.
- Agregado `ItemInstance` como clase C# simple, runtime-only y no MonoBehaviour.
- `ItemInstance` guarda `InstanceId`, `DefinitionId` y `Condition`.
- `Condition` se inicializa desde `ItemDefinition.physical.condition_max` o usa fallback seguro.
- Validado en Unity: `ItemInstance` runtime-only funciona.
- Agregado `DebugInventory` como MonoBehaviour debug temporal.
- `DebugInventory` crea item instances runtime desde una lista serializada de item definition ids.
- Validado en Unity: `DebugInventory` crea instancias runtime desde `ItemDefinition`.
- `DebugInventory` normaliza `none`, vacio e indice invalido como sin item equipado.
- `ActorInteractionContext` ahora puede usar `DebugInventory` como fuente principal del item equipado.
- Si `DebugInventory` esta asignado y devuelve sin item, se trata como sin item y no se usa fallback legacy.
- `equippedItemDefinitionId` legacy solo se usa si no hay `DebugInventory` asignado.
- `WorldInteractionDebugTester` usa `ActorInteractionContext.GetEquippedItemDefinitionId()`.
- `InteractionSystem` no fue modificado y sigue recibiendo un item definition id.
- Validado en Unity: con `rusted_crowbar_01` equipado aparecen `force_door` y `pry_open_container`.
- Validado en Unity: con `equippedItemIndex = -1` aparece `Equipped item: (none)` y no se muestran acciones de herramienta.
- Validado en Unity: `DebugInventory`, si esta asignado, manda sobre el fallback legacy.
- Validado en Unity: `InteractionSystem` sigue recibiendo solo definition_id y no depende de `DebugInventory` ni `ItemInstance`.
- Validado en Unity: Milestone 11 sigue funcionando; duracion de acciones y runtime tags siguen correctos.
- No se tocaron JSON, runtime tags, `DebugActionExecutor` ni `ActionDefinition.cost.time`.
- No se agregaron inventario final, UI de inventario, loot, pickup/drop, save system, equipment system final, slots reales ni durabilidad funcional.
- Estado: validated.

### Technical Cleanup 12.1

- Milestone 12.1 implementado y validado como limpieza tecnica controlada de escena y documentacion.
- `DebugInventory` verificado/configurado en `Debug Player` con `initialItemDefinitionIds = ["rusted_crowbar_01"]` y `equippedItemIndex = 0`.
- `ActorInteractionContext.debugInventory` apunta al `DebugInventory` del `Debug Player`.
- `GameDataManager` fue movido a root para corregir el warning de `DontDestroyOnLoad`.
- Validado en Unity: `GameDataManager` quedo como root GameObject.
- Validado en Unity: el warning de `DontDestroyOnLoad` ya no aparece.
- Validado en Unity: `CoreDataSystem` carga correctamente.
- `Core` se mantiene aunque quede vacio.
- El `ActorInteractionContext` duplicado bajo `Debug_Actor` fue renombrado a `Deprecated_ActorInteractionContext_Legacy`.
- `Deprecated_ActorInteractionContext_Legacy` fue desactivado para evitar seleccion accidental por fallback.
- Validado en Unity: `Deprecated_ActorInteractionContext_Legacy` quedo desactivado y aislado.
- `Debug_Actor` se mantiene.
- `DebugPlayerContext.cs`, `GameDataDebugTester.cs`, `ActionAvailabilityDebugTester.cs` y `ActorInteractionContext.EquippedItemDefinitionId` quedan documentados como deprecated/legacy.
- `_Recovery` se mantiene.
- No se borraron scripts legacy.
- No se borro `_Recovery`.
- No se tocaron codigo ni JSON.
- Validado en Unity: movimiento, camara, UI blocker, action duration, runtime tags, DebugInventory e InteractionSystem siguen funcionando.
- Estado: validated.

### Tool Requirement Hardening

- Milestone 13 implementado y validado como hardening auditable de requisitos de herramienta equipada.
- `requirements.weapon_tags` se mantiene como campo activo.
- `weapon_tags` queda documentado como nombre legacy compatible para required equipped item tags.
- No se agrego `required_item_tags`.
- No se migro schema.
- No se tocaron `actions.json` ni `items.json`.
- No se toco JSON.
- No se cambio la semantica OR de `weapon_tags`.
- Agregado `ActionAvailabilityResult` en `OldScars.Core.Actions`, junto a `ActionAvailabilityEvaluator`.
- `ActionAvailabilityResult` registra disponibilidad, razones de bloqueo, razones de exito, tags requeridos, tags faltantes y tags del item que hicieron match.
- `ActionAvailabilityEvaluator.Evaluate()` calcula una evaluacion explicable sin usar `Debug.Log`.
- `ActionAvailabilityEvaluator.IsAvailable()` se mantiene como wrapper compatible de `Evaluate(...).IsAvailable`.
- `ActionAvailabilityContext` acepta metadata opcional de item equipado: `EquippedItemId` y `HasEquippedItem`.
- `InteractionSystem` usa `Evaluate()` internamente y sigue devolviendo solo acciones disponibles.
- `InteractionQuery.LogAvailabilityDetails` permite activar logs detallados opcionales.
- Validado en Unity: `logAvailabilityDetails` permite ver por que una accion aparece o se bloquea.
- `WorldInteractionDebugTester.logAvailabilityDetails` queda desactivado por defecto para evitar spam.
- `DataValidator` agrega warning no destructivo si un tag valido usado por `requirements.weapon_tags` no aparece en ningun item cargado.
- El warning de `DataValidator` se calcula contra todos los items disponibles en `GameDatabase`.
- Validado en Unity: `DataValidator` no bloquea la carga.
- Validado en Unity: con palanca equipada, `force_door` y `pry_open_container` aparecen correctamente.
- Validado en Unity: sin item equipado, las acciones de herramienta se bloquean correctamente.
- Validado en Unity: action duration y runtime tags siguen funcionando.
- No se tocaron `DebugInventory`, `ItemInstance`, `DebugActionProgressController`, `ActionDefinition.cost.time` ni runtime tags.
- No se agregaron inventario final, equipment final, slots reales, UI de inventario, loot, pickup/drop ni save system.
- Estado: validated.

### Playable Inventory + Pickup Loop

- Milestone 14 implementado y validado como primer loop jugable v0.
- Agregado `InventoryComponent` como MonoBehaviour runtime-only para el `Debug Player`.
- `InventoryComponent` guarda una lista runtime plana de `ItemInstance`.
- `InventoryComponent` expone `AddItemByDefinitionId`, `GetItems`, `GetEquippedItemInstance`, `GetEquippedItemDefinitionId`, `EquipIndex` y `Unequip`.
- `InventoryComponent` inicia con `equippedItemIndex = -1`.
- Validado en Unity: `InventoryComponent` v0 funciona.
- Validado en Unity: el jugador inicia sin item equipado.
- `InventoryComponent` no autoequipa items al recoger.
- `InventoryComponent` no implementa save, peso, capacidad, stacks, slots reales ni UI interna.
- `DebugInventory` se mantiene como legacy.
- `ActorInteractionContext` ahora resuelve item equipado con prioridad `InventoryComponent` -> `DebugInventory` -> `equippedItemDefinitionId` legacy.
- Si `InventoryComponent` existe y no tiene item equipado, se considera sin item y no cae a `DebugInventory`.
- Agregado `InventoryDebugPanel` OnGUI.
- `InventoryDebugPanel` abre/cierra con `I`, cierra con Escape, muestra item equipado, lista items y permite `Equip` / `Unequip`.
- `DebugWorldUiInputBlocker` ahora bloquea clicks de movimiento cuando el panel de inventario esta abierto.
- Agregado `WorldItemPickup` como componente minimo para un item tirado en el mundo.
- `WorldItemPickup` valida y agrega una `ItemInstance` al `InventoryComponent` del actor al ejecutar pickup.
- Al recoger, `WorldItemPickup` agrega runtime tag `picked_up`, remueve `pickupable` y desactiva colliders/renderers para evitar doble pickup.
- Validado en Unity: `WorldItemPickup` funciona con `rusted_crowbar_01`.
- Validado en Unity: `pick_up_item` dura 0.5s.
- Validado en Unity: al recoger, el item se agrega al `InventoryComponent`.
- Validado en Unity: la palanca del mundo queda oculta/no interactuable.
- Agregado `DebugActionExecutionContext` para pasar `ActorInteractionContext`, target y item equipado al executor.
- `ContextualActionDebugPanel` pasa el actor context al iniciar una accion.
- `DebugActionProgressController` conserva duracion y progreso, pero ahora ejecuta con `DebugActionExecutionContext`.
- `DebugActionExecutor` soporta el effect cerrado `pick_up_item`.
- Los effects existentes `add_tag`, `remove_tag` y `show_target_info` siguen funcionando.
- `DataValidator` permite `pick_up_item` como effect cerrado con `target = target` y sin `tag`.
- `actions.json` agrega la action `pick_up_item` para `world_interaction`, con `target_tags` `world_item` y `pickupable`, y `cost.time = 0.5`.
- `tags.json` agrega `world_item`, `pickupable` y `picked_up`.
- `items.json` no fue modificado.
- `SampleScene` configura `Debug Player` con `InventoryComponent` vacio y `equippedItemIndex = -1`.
- `SampleScene` conserva `DebugInventory` como legacy, sin mandar cuando existe `InventoryComponent`.
- `SampleScene` agrega `InventoryDebugPanel` bajo `Debug_UI`.
- `SampleScene` agrega `Debug World Crowbar` en layer `Interactable`, con `WorldObjectTags`, `WorldObjectDebugInfo` y `WorldItemPickup.itemDefinitionId = rusted_crowbar_01`.
- Validado en Unity: `InventoryDebugPanel` abre con `I` y permite equipar/unequip.
- Validado en Unity: al equipar la palanca, `force_door` y `pry_open_container` aparecen correctamente.
- `InteractionSystem` no fue modificado para depender de inventario, UI, `WorldItemPickup`, `DebugInventory`, `ItemInstance` ni MonoBehaviour.
- Validado en Unity: `InteractionSystem` sigue desacoplado del inventario, UI y pickup.
- Validado en Unity: action duration y runtime tags siguen funcionando.
- No se cambio `weapon_tags`, action duration, runtime tags ni `ActionAvailabilityEvaluator.Evaluate`.
- No se crearon inventario final, drag/drop, grid, peso/capacidad real, save system, loot aleatorio, contenedores reales, pickup/drop generico completo, slots reales, UI final, combate ni IA.
- Estado: validated.

### Container Loot v0

- Milestone 15 implementado y validado como primer sistema jugable v0 para saquear contenedores abiertos.
- Agregado `LootTableDefinition` como definition JSON runtime-readable.
- `LootTableDefinition` v0 usa `type`, `id` y entries con `item_id` + `count`.
- No se agregaron chance, pesos, rarezas, condiciones, economia ni random avanzado.
- `GameDataLoader` ahora carga archivos desde `loot_tables`.
- Validado en Unity: `GameDataLoader` carga `loot_tables/*.json`.
- `GameDatabase` registra y expone loot tables con `RegisterLootTable`, `GetLootTable`, `GetAllLootTables` y `LootTableCount`.
- `GameDatabase.LogStats()` incluye cantidad de loot tables.
- `DataValidator` valida `loot_table`, entries, referencias a items y `count > 0`.
- Validado en Unity: `DataValidator` valida loot tables sin errores.
- `DataValidator` permite el effect cerrado `search_container`.
- Agregado `ContainerLootComponent` como MonoBehaviour de target para saquear contenedores.
- `ContainerLootComponent` usa `DebugActionExecutionContext` y obtiene inventario desde `ActorInteractionContext`.
- `ContainerLootComponent` no usa busqueda global para resolver inventario.
- `DebugActionExecutor` soporta el effect cerrado `search_container`.
- Los effects existentes `add_tag`, `remove_tag`, `show_target_info` y `pick_up_item` se mantienen.
- `actions.json` agrega `search_container` para `world_interaction`, con target tags `opened_container` y `lootable_container`, y `cost.time = 1.5`.
- `tags.json` agrega `material`, `lootable_container` y `looted_container`.
- `items.json` agrega `scrap_metal_01` como item simple de prueba.
- `container_loot.json` agrega la tabla `debug_sealed_container_loot_01` con `scrap_metal_01 x1`.
- Validado en Unity: `container_loot.json` carga `debug_sealed_container_loot_01`.
- `SampleScene` agrega `lootable_container` al `Debug Sealed Container`.
- `SampleScene` agrega `ContainerLootComponent` al `Debug Sealed Container` con `lootTableId = debug_sealed_container_loot_01`.
- El loot infinito se evita removiendo `lootable_container` y agregando `looted_container` al saquear.
- Validado en Unity: `search_container` aparece solo con `opened_container` + `lootable_container`.
- Validado en Unity: `search_container` dura 1.5s.
- Validado en Unity: `search_container` agrega `scrap_metal_01` al `InventoryComponent`.
- Validado en Unity: `InventoryDebugPanel` muestra `Scrap Metal`.
- Validado en Unity: el contenedor remueve `lootable_container` y agrega `looted_container`.
- Validado en Unity: `search_container` ya no aparece despues de saquear.
- `InteractionSystem` no fue modificado para depender de inventario, loot component ni MonoBehaviour.
- Validado en Unity: `InteractionSystem` sigue desacoplado de inventario, loot y MonoBehaviour.
- No se crearon loot avanzado, UI final, save system, stacks, economia, crafting, combate ni IA.
- Estado: validated.

### Primer POI Jugable Completo

- Milestone 16 implementado y validado como composicion de escena sobre sistemas ya validados.
- `SampleScene` fue ordenada como un POI compacto tipo pequeno taller / bahia de mantenimiento industrial.
- El flujo implementado usa movimiento point-and-click, camara debug, inventario v0, pickup, equip simple, acciones con duracion, runtime tags, loot table v0 y container loot v0.
- `Debug Player` mantiene `InventoryComponent` vacio y `equippedItemIndex = -1`.
- `Debug World Crowbar` mantiene layer `Interactable`, tags `world_item`, `pickupable`, `inspectable` y `WorldItemPickup.itemDefinitionId = rusted_crowbar_01`.
- `Debug Locked Door` mantiene layer `Interactable` y tags `locked_door`, `inspectable`.
- `Debug Sealed Container` mantiene layer `Interactable`, tags `sealed_container`, `lootable_container`, `inspectable` y `ContainerLootComponent.lootTableId = debug_sealed_container_loot_01`.
- `Debug Strange Machine` mantiene layer `Interactable` y tag `inspectable`.
- Se actualizaron textos de `WorldObjectDebugInfo` para palanca, puerta, contenedor y maquina.
- Validado en Unity: `SampleScene` funciona como primer POI jugable compacto.
- Validado en Unity: el POI usa sistemas existentes y no crea sistemas nuevos.
- Validado en Unity: `Debug Player` inicia dentro del POI con `InventoryComponent` vacio y sin item equipado.
- Validado en Unity: `Debug World Crowbar` funciona como herramienta inicial recogible.
- Validado en Unity: `Debug Locked Door` funciona como obstaculo forzable con palanca.
- Validado en Unity: `Debug Sealed Container` funciona como contenedor sellado, abrible y saqueable.
- Validado en Unity: `Debug Strange Machine` funciona como objeto ambiental examinable.
- Validado en Unity: el loop completo funciona, recoger palanca -> equipar -> abrir/forzar obstaculo -> abrir contenedor -> buscar loot -> obtener Scrap Metal -> dejar estados runtime correctos.
- Validado en Unity: la palanca agrega `picked_up` y remueve `pickupable`.
- Validado en Unity: la puerta agrega `forced_open` y remueve `locked_door`.
- Validado en Unity: el contenedor agrega `opened_container` y remueve `sealed_container` al abrirse.
- Validado en Unity: el contenedor agrega `looted_container` y remueve `lootable_container` despues de saquear.
- Validado en Unity: data load sigue OK con 0 errors y 0 warnings.
- Validado en Unity: `InteractionSystem` sigue desacoplado.
- No se modificaron scripts C#.
- No se modificaron JSON.
- No se modificaron `InteractionSystem`, `InventoryComponent`, `DebugActionExecutor`, `DebugActionProgressController`, `GameDataLoader`, `GameDatabase` ni `DataValidator`.
- No se agregaron actions nuevas ni effects nuevos.
- No se crearon combate, IA, facciones, save system, UI final, inventario final, loot avanzado, crafting, puerta fisica real ni contenedor final.
- No se rompieron `InventoryComponent`, `WorldItemPickup`, `ContainerLootComponent`, action duration, runtime tags ni loot tables.
- Estado: validated.

### Gameplay Feedback Log Foundation / POI State Readability v0

- Milestone 17 implementado y validado como base runtime-only de feedback estructurado para el POI.
- Agregado `GameplayFeedbackEntryType` con categorias cerradas: `ItemPickedUp`, `ItemEquipped`, `ItemUnequipped`, `ActionCompleted`, `LootReceived`, `TargetStateChanged`, `Info` y `Warning`.
- Agregado `GameplayFeedbackEntry` para guardar datos estructurados: tipo, mensaje fallback, tiempo, actor, target, item, action, quantity, tags agregados/removidos y `debugOnly`.
- Agregado `GameplayFeedbackLog` como log runtime-only append/read con `maxEntries`, `Record`, `Clear` y `Entries`.
- `GameplayFeedbackLog` no es persistente.
- `GameplayFeedbackLog` no tiene listeners, subscriptions, callbacks, dispatch ni payload generico.
- Agregado `DebugFeedbackLogPanel` como UI debug OnGUI que solo lee `Entries`.
- `DebugFeedbackLogPanel` no recibe llamadas desde gameplay y no ejecuta logica de gameplay.
- `SampleScene` agrega `GameplayFeedbackDebug` bajo `Debug_UI` con `GameplayFeedbackLog` y `DebugFeedbackLogPanel`.
- `WorldItemPickup` registra `ItemPickedUp` al recoger la palanca.
- `InventoryComponent` registra `ItemEquipped` y `ItemUnequipped` al equipar o desequipar.
- `DebugActionExecutor` registra `ActionCompleted` al completar acciones.
- `DebugActionExecutor` registra `TargetStateChanged` solo cuando el mismo aplica effects `add_tag` o `remove_tag`.
- `WorldItemPickup` registra `TargetStateChanged` solo por los tags que el mismo cambia: `picked_up` y `pickupable`.
- `ContainerLootComponent` registra `TargetStateChanged` solo por los tags que el mismo cambia: `looted_container` y `lootable_container`.
- `ContainerLootComponent` registra `LootReceived` al obtener `scrap_metal_01`.
- `InventoryComponent.AddItemByDefinitionId` no registra `ItemPickedUp` ni `LootReceived`; pickup y loot registran desde sus sistemas de origen.
- Validado en Unity: el proyecto compila sin errores.
- Validado en Unity: el panel `Gameplay Feedback Log` aparece en `SampleScene`.
- Validado en Unity: `ItemPickedUp` se registra al recoger la palanca.
- Validado en Unity: `ItemEquipped` / `ItemUnequipped` se registran al equipar o desequipar.
- Validado en Unity: `ActionCompleted` se registra en `examine_object`, `force_door`, `pry_open_container` y `search_container`.
- Validado en Unity: `TargetStateChanged` debug registra cambios runtime de tags.
- Validado en Unity: `LootReceived` se registra al obtener `scrap_metal_01`.
- Validado en Unity: `search_container` deja de aparecer despues de saquear.
- Validado en Unity: el contenedor queda con `looted_container`.
- Validado en Unity: la puerta queda con `forced_open`.
- Validado en Unity: `InteractionSystem` no fue tocado.
- Validado en Unity: el gameplay no depende del panel de feedback.
- No se creo journal, quest log, UI final, save system, EventBus ni sistemas grandes.
- Estado: validated.

### Action Availability Diagnostics / Requirement Readability v0

- Milestone 18 implementado y validado como capacidad diagnostica opcional para disponibilidad de acciones contextuales.
- Agregado `ActionAvailabilityDiagnosticReport` como reporte runtime-only con target display/name, contexto requerido, actor tags snapshot, target tags snapshot, equipped item id, equipped item tags snapshot y entradas por accion.
- Agregado `ActionAvailabilityDiagnosticEntry` como entrada estructurada por accion candidata.
- Cada entrada guarda action id/display, disponibilidad, razones de exito/bloqueo, tags requeridos, tags faltantes y matched item tags.
- El diagnostico usa `ActionAvailabilityEvaluator.Evaluate()` y `ActionAvailabilityResult`.
- El diagnostico evalua el mismo conjunto de acciones candidatas que `InteractionSystem` considera antes de filtrar por disponibilidad.
- `GetAvailableActions()` sigue devolviendo solo acciones disponibles.
- El comportamiento jugable y el menu contextual ejecutable no cambiaron respecto a Milestone 17.
- Agregado `DebugActionAvailabilityPanel` como UI debug OnGUI para visualizar acciones disponibles/bloqueadas, razones de bloqueo y snapshots de contexto.
- `DebugActionAvailabilityPanel` solo lee el reporte y no ejecuta acciones ni modifica disponibilidad.
- `DebugActionAvailabilityPanel` se muestra/oculta con F8 y arranca oculto por defecto.
- `DebugFeedbackLogPanel` se ajusto para mostrarse/ocultarse con F7 y arrancar oculto por defecto.
- `InventoryDebugPanel` sigue funcionando con `I`.
- Validado en Unity: el proyecto compila sin errores.
- Validado en Unity: `GetAvailableActions()` sigue devolviendo solo acciones disponibles.
- Validado en Unity: el diagnostico muestra acciones disponibles y bloqueadas.
- Validado en Unity: puerta cerrada sin palanca bloquea `force_door` por item tags faltantes.
- Validado en Unity: puerta cerrada con palanca muestra `force_door` disponible.
- Validado en Unity: puerta forzada bloquea `force_door` por falta de `locked_door` y el snapshot muestra `forced_open`.
- Validado en Unity: contenedor sellado muestra `pry_open_container` disponible.
- Validado en Unity: contenedor abierto muestra `search_container` disponible.
- Validado en Unity: contenedor looteado bloquea `search_container` por falta de `lootable_container` y el snapshot muestra `looted_container`.
- Validado en Unity: `GameplayFeedbackLog` sigue separado y funcionando.
- Validado en Unity: los paneles debug arrancan ocultos por defecto.
- El diagnostico es runtime-only, debug/fundacional, sin EventBus, sin listeners, sin subscriptions, sin callbacks, sin payload generico, sin UI final y sin persistencia.
- No se toco JSON, loaders, database, validator, `GameplayFeedbackLog` base, combate, IA, save system, journal, quest log ni UI final.
- Estado: validated.

### Debug State Color Readability

- Milestone 19.1 implementado y validado como mejora de lectura visual debug de estados runtime del POI.
- `WorldObjectStateView` ahora soporta color debug por regla visual.
- El color debug se aplica usando `MaterialPropertyBlock`.
- Los colores reflejan runtime tags sin modificar gameplay.
- Los colores no modifican materiales compartidos.
- Puerta `locked_door`: rojo oscuro.
- Puerta `forced_open`: verde.
- Contenedor `sealed_container`: naranja.
- Contenedor `opened_container` + `lootable_container`: cian.
- Contenedor `looted_container`: gris oscuro.
- Palanca `pickupable`: color claro/visible.
- Palanca `picked_up`: sigue ocultandose.
- Validado en Unity: data load sigue OK con 0 errors y 0 warnings.
- Validado en Unity: puerta cambia de rojo oscuro a verde tras `force_door`.
- Validado en Unity: contenedor cambia de naranja a cian tras `pry_open_container` y a gris oscuro tras `search_container`.
- Validado en Unity: palanca se oculta tras `pick_up_item`.
- Validado en Unity: F7, F8 e I siguen funcionando.
- Validado en Unity: el menu contextual sigue mostrando solo acciones disponibles.
- No se toco JSON.
- No se toco `InteractionSystem`.
- No se toco `ActionAvailabilityEvaluator`.
- No se toco `GameplayFeedbackLog`.
- No se tocaron diagnostics.
- No se creo UI final, arte final, VFX, sonido ni animaciones.
- Estado: validated.

### Stable Color-Only State Visuals

- Milestone 19.2 implementado y validado como estabilizacion visual color-only para estados del POI.
- `SampleScene` fue ajustada para que puerta y contenedor mantengan geometria estable entre estados.
- Se neutralizaron rotaciones de puerta.
- Se neutralizaron cambios de variante visual del contenedor.
- Se neutralizaron deformaciones raras o cambios de forma/posicion/escala entre estados del contenedor.
- Puerta y contenedor ahora comunican estados solo por color debug.
- La puerta inicia roja y tras `force_door` cambia a verde sin rotar ni moverse.
- El contenedor inicia naranja, tras `pry_open_container` cambia a cian y tras `search_container` cambia a gris oscuro sin cambiar geometria.
- La palanca sigue ocultandose con `SetActive` cuando tiene `picked_up`.
- Validado en Unity: data load sigue OK con 0 errors y 0 warnings.
- Validado en Unity: F7, F8 e I siguen funcionando.
- Validado en Unity: el menu contextual sigue mostrando solo acciones disponibles.
- No se toco codigo.
- No se toco JSON.
- No se toco gameplay.
- Estado: validated.

### Item Storage / Container Foundation v0

- Milestone 20 implementado y validado como base comun runtime-only para almacenamiento de items con cantidades simples.
- Se creo `ItemStorage` como clase C# pura, no `MonoBehaviour`.
- Se creo `ItemStorageEntry` para representar `ItemInstance` + `Quantity`.
- `Quantity` debe ser mayor o igual a 1.
- `Quantity` no fue agregado a `ItemInstance`.
- En Milestone 20 todavia no habia auto-merge por `DefinitionId`; Milestone 22.1 agrego merge simple controlado por `max_stack`.
- `InventoryComponent` ahora usa `ItemStorage` internamente.
- `AddItemByDefinitionId(string)` sigue funcionando como antes.
- `InventoryComponent` soporta `AddItemByDefinitionId(string, int quantity)`.
- `InventoryDebugPanel` sigue funcionando con `I`.
- `InventoryDebugPanel` muestra cantidades simples cuando `Quantity > 1`.
- `ContainerLootComponent` mantiene su componente y `lootTableId` serializado.
- `ContainerLootComponent` inicializa storage interno una sola vez desde su loot table.
- El contenido del contenedor existe aunque el contenedor este `sealed_container`.
- `search_container` transfiere contenido existente al `InventoryComponent` en lugar de generar loot al buscar.
- Al quedar vacio, el contenedor queda `looted_container` como antes.
- Validado en Unity: compila sin errores.
- Validado en Unity: data load 0 errors / 0 warnings.
- Validado en Unity: `pick_up_item` sigue agregando `rusted_crowbar_01` al inventario.
- Validado en Unity: equipar crowbar sigue funcionando.
- Validado en Unity: `sealed_container` no permite `search_container`.
- Validado en Unity: `pry_open_container` habilita `search_container`.
- Validado en Unity: `search_container` transfiere contenido existente del contenedor al inventario.
- Validado en Unity: las cantidades simples se muestran en `InventoryDebugPanel` cuando `Quantity > 1`.
- Validado en Unity: el contenedor queda `looted_container` y no vuelve a entregar loot.
- Validado en Unity: F7, F8 e I siguen funcionando.
- Validado en Unity: el menu contextual sigue mostrando solo acciones disponibles.
- No se toco JSON.
- No se toco schema.
- No se toco `InteractionSystem`.
- No se toco `ActionAvailabilityEvaluator`.
- No se tocaron diagnostics.
- No se toco `GameplayFeedbackLog` base.
- No se agrego UI final, peso, slots, grid, save system ni contenedores anidados.
- Estado: validated.

### Stateful Inspection & Container Access v0

- Milestone 21 implementado y validado como mejora de inspeccion por estado runtime y reglas defensivas de acceso a storage.
- `WorldObjectDebugInfo` ahora soporta textos condicionales por `requiredTags`, `forbiddenTags` y `priority`.
- `WorldObjectDebugInfo` mantiene `displayName` e `inspectText` como fallback.
- `DebugActionExecutor` usa esos textos condicionales al ejecutar `examine_object`.
- `ContainerLootComponent` expone resumen debug de storage interno.
- El resultado de inspeccion de contenedores agrega `[DEBUG STORAGE]` como debug/readability.
- `[DEBUG STORAGE]` no es UI final de contenedor.
- `ContainerLootComponent` valida acceso antes de transferir loot.
- Tener storage interno queda separado de poder acceder al storage.
- Un contenedor `sealed_container` puede tener contenido inicializado pero no permite `search_container`.
- Validado en Unity: compila sin errores.
- Validado en Unity: data load 0 errors / 0 warnings.
- Validado en Unity: examinar puerta cerrada muestra texto `locked_door`.
- Validado en Unity: tras `force_door`, examinar puerta muestra texto `forced_open`.
- Validado en Unity: examinar contenedor sellado muestra `sealed_container` + `[DEBUG STORAGE]`.
- Validado en Unity: tras `pry_open_container`, `search_container` aparece correctamente.
- Validado en Unity: `search_container` transfiere contenido existente.
- Validado en Unity: tras `search_container`, examinar contenedor muestra `looted_container`.
- Validado en Unity: el contenedor no entrega loot dos veces.
- Validado en Unity: `InventoryDebugPanel` con I sigue funcionando.
- Validado en Unity: F7, F8 e I siguen funcionando.
- Validado en Unity: el menu contextual sigue mostrando solo acciones disponibles.
- No se toco JSON.
- No se toco schema.
- No se toco `InteractionSystem`.
- No se toco `ActionAvailabilityEvaluator`.
- No se tocaron diagnostics.
- No se toco `GameplayFeedbackLog`.
- No se creo UI final de contenedor, peso, slots, grid, split/merge, save system ni contenedores anidados.
- Estado: validated.

### Hotfix - State-Aware Inspection Selection

- Milestone 21.0.1 implementado y validado como hotfix de seleccion condicional de inspeccion.
- La seleccion de texto condicional usa `RuntimeTags` reales.
- Las reglas de puerta son mutuamente excluyentes.
- Puerta `locked_door` requiere `locked_door` y bloquea `forced_open`.
- Puerta `forced_open` requiere `forced_open`, bloquea `locked_door` y tiene mayor prioridad.
- Validado en Unity: la puerta forzada ya no muestra texto de puerta trabada.
- El contenedor mantiene `looted_container` con prioridad mas alta.
- `opened_container` + `lootable_container` mantiene `forbiddenTags: looted_container`.
- `sealed_container` requiere `sealed_container`.
- No se toco JSON.
- No se toco `InteractionSystem`.
- No se toco `ActionAvailabilityEvaluator`.
- No se tocaron diagnostics.
- No se toco `GameplayFeedbackLog`.
- No se tocaron `ItemStorage`, `ItemStorageEntry` ni `InventoryComponent`.
- Estado: validated.

### Actor Needs & Debug Supply Containers v0

- Milestone 22 implementado y validado como primera base survival runtime de actor.
- Agregado `ActorNeedsComponent` generico, no exclusivo del jugador.
- Agregados `ActorNeedProfile` y `ActorNeedState` para separar configuracion/perfil y estado runtime.
- Hunger/Thirst decaen en Play Mode.
- Agregados consumibles cerrados por JSON mediante `consumable.restore_needs`.
- Agregado `water_bottle_01`, que restaura `thirst`.
- Agregado `food_ration_01`, que restaura `hunger`.
- Agregado `InventoryItemUseService` para aplicar consumibles a `ActorNeedsComponent` sin mover logica de necesidades a UI.
- Agregado `InventoryItemUseResult` como resultado simple de uso.
- `InventoryDebugPanel` permite usar consumibles.
- El uso de consumibles consume cantidad solo cuando hubo efecto valido.
- Agregadas cajas debug de suministros usando loot tables, `ContainerLootComponent` e `ItemStorage`.
- Validado en Unity: data load 0 errors / 0 warnings.
- Validado en Unity: F7, F8, I, palanca, puerta y caja original siguen funcionando.
- No se creo UI final, cocina, heridas, enfermedad, temperatura, descanso, IA, combate ni save system.
- Estado: validated.

### Survival UI, Action Feedback & Manual Container Loot v0

- Milestone 22.1 implementado y validado como refuerzo de survival debug, feedback de uso y saqueo manual.
- Agregado `ActorNeedsDebugPanel` como UI debug fija arriba a la izquierda para Hunger/Thirst.
- El consumo de agua/comida se registra en `GameplayFeedbackLog` como `ItemUsed`.
- Agregado `max_stack` a `ItemDefinition`/JSON.
- `max_stack = 1` significa no stackeable.
- `max_stack > 1` permite merge simple en `ItemStorage`.
- `ItemStorage` mergea por mismo `definitionId` hasta `max_stack`.
- Validado en Unity: `Scrap Metal x1 + Scrap Metal x500` queda como `Scrap Metal x501`.
- Las cajas debug nuevas usan cantidades x500.
- `search_container` abre `ItemStorageDebugPanel` en vez de transferir todo automaticamente.
- `ItemStorageDebugPanel` muestra `Take 1`, `Take Stack`, `Take All` y `Close`.
- `looted_container` se aplica solo cuando el storage queda vacio.
- Food/Water Debug Crate y Misc Debug Crate cambian color con `WorldObjectStateView`: cian con loot, gris/negro vacias.
- `ItemStorageDebugPanel` queda como debug reusable, no UI final.
- No se creo inventario final, drag/drop, peso, save system, comercio, IA ni combate.
- Estado: validated.

### Hotfix - Wire Survival and Storage Debug UI

- Milestone 22.1.1 implementado y validado como hotfix de wiring/configuracion debug.
- `ActorNeedsDebugPanel` quedo presente en `SampleScene`.
- `ActorNeedsDebugPanel` muestra Hunger/Thirst arriba a la izquierda y lee el `ActorNeedsComponent` del Debug Player.
- Si la referencia esta vacia, `ActorNeedsDebugPanel` autoresuelve de forma segura.
- `ItemStorageDebugPanel` quedo presente en `SampleScene`.
- `DebugActionExecutor`/`search_container` pueden encontrar o crear `ItemStorageDebugPanel` por fallback seguro.
- Al buscar un contenedor valido, se abre el panel con contenido.
- Validado en Unity: aparecen `Take 1`, `Take Stack`, `Take All` y `Close`.
- `DebugWorldUiInputBlocker` bloquea clicks sobre `ItemStorageDebugPanel` y `ActorNeedsDebugPanel`.
- Food/Water Debug Crate y Misc Debug Crate reflejan estados con `WorldObjectStateView`.
- No se toco `InteractionSystem`, `ActionAvailabilityEvaluator`, JSON de items/loot, sistemas M20/M21/M22 mas de lo necesario, documentacion, commit ni push durante el hotfix.
- Estado: validated.

### Hotfix - Equippable Item Flag

- Milestone 22.1.2 implementado y validado como hotfix de elegibilidad de equip.
- Agregado `equippable` boolean a `ItemDefinition`/JSON.
- `equippable` tiene fallback seguro `false` si falta.
- `equippable` es dato funcional, no tag.
- `rusted_crowbar_01` queda con `equippable: true`.
- `scrap_metal_01`, `water_bottle_01` y `food_ration_01` quedan con `equippable: false`.
- `InventoryDebugPanel` muestra `Equip` solo si `itemDefinition.equippable == true`.
- Validado en Unity: la palanca sigue equipable.
- Validado en Unity: agua y comida no se pueden equipar, pero si usar.
- Validado en Unity: cantidades, stacking, `Use` e `ItemStorageDebugPanel` siguen funcionando.
- Validado en Unity: data load 0 errors / 0 warnings.
- No se creo equipment system avanzado, slots de manos, ActorInventoryComponent, UI final, documentacion, commit ni push durante el hotfix.
- Estado: validated.

### Actor Inventory Foundation v0

- Milestone 23 implementado y validado como base de inventario de actor.
- `InventoryComponent` separa conceptualmente Storage y Equipped.
- `ItemStorage` sigue siendo la base principal para guardar items.
- `right_hand` queda como primer slot runtime funcional.
- `right_hand` usa `rightHandItemInstanceId`, no indice.
- El item equipado sigue existiendo dentro de `ItemStorage`.
- `ItemStorage` puede resolver entries por `ItemInstance.InstanceId`.
- `rusted_crowbar_01` se equipa solo en `right_hand` segun JSON.
- `ItemDefinition` soporta `equip.equippable`, `equip.allowed_slots` y `equip.occupied_slots`.
- `equippable` plano queda como compatibilidad temporal.
- `DataValidator` detecta contradicciones entre `equippable` plano y `equip.equippable`.
- En M23 el unico slot valido es `right_hand`.
- `InventoryComponent` valida internamente si un item puede equiparse antes de aceptar la operacion.
- Agua, comida y scrap no se pueden equipar ni por UI ni por llamada interna.
- Agua y comida siguen usando `Use`.
- `InventoryDebugPanel` muestra Equipped separado de Storage.
- `InventoryDebugPanel` muestra `Equip` solo si `InventoryComponent` confirma que el item puede equiparse en `right_hand`.
- `ActorInteractionContext` / `InteractionSystem` siguen obteniendo el item equipado mediante `GetEquippedItemDefinitionId()`.
- Validado en Unity: con crowbar equipada, `force_door` y `pry_open_container` siguen disponibles.
- Validado en Unity: loot final queda Scrap x501, Water x500, Food x500, Crowbar x1.
- Validado en Unity: data load 0 errors / 0 warnings.
- Validado en Unity: F7, F8, I, Hunger/Thirst, `GameplayFeedbackLog`, `ItemStorageDebugPanel` y loot de cajas siguen funcionando.
- No se implemento `equip_visual`.
- No se implementaron `left_hand`, `both_hands`, ropa/armadura, NPCs, cadaveres, salud, muerte, IA, combate real, save system ni UI final.
- Estado: validated.

### Hotfix - Cleanup legacy equipped index warning

- Milestone 23.0.1 implementado y validado como hotfix de warning en `InventoryComponent`.
- Se elimino el warning CS0414 del campo legacy de indice equipado.
- `rightHandItemInstanceId` sigue siendo la fuente real del equipamiento.
- `GetEquippedItemDefinitionId()` sigue devolviendo el item equipado en `right_hand`.
- No se cambio el flujo de `right_hand`.
- No se toco `InteractionSystem`, docs, escena, Unity/batchmode, commit ni push durante el hotfix.
- Estado: validated.

### Hotfix - Revalidate Action Requirements Before Execution

- Milestone 23.0.2 implementado y validado como proteccion contra menus contextuales viejos.
- `ContextualActionDebugPanel` revalida la accion antes de iniciar `DebugActionProgressController`.
- La revalidacion reconstruye el contexto actual del actor, target, item equipado y action context.
- La revalidacion usa el flujo existente de `InteractionSystem` / disponibilidad de acciones.
- Si la accion ya no esta disponible, no inicia progreso.
- Si la accion ya no esta disponible, muestra feedback/log debug indicando que la accion ya no esta disponible o que los requisitos cambiaron.
- Validado en Unity: desequipar la palanca despues de abrir un menu viejo bloquea `force_door` y `pry_open_container`.
- No se hizo refactor grande de `InteractionSystem`.
- No se implementaron acciones instantaneas/repetidas, NPCs, salud, cadaveres, IA ni combate.
- Estado: validated.

### Hotfix - Refresh Context Menu Availability

- Milestone 23.0.3 implementado y validado como refresco visual del menu contextual debug.
- Mientras `ContextualActionDebugPanel` esta abierto, compara el equipped item actual con el ultimo observado.
- Si cambia el item equipado, refresca acciones disponibles usando `InteractionSystem.GetAvailableActions()`.
- La linea `Item` del menu cambia entre `rusted_crowbar_01` y `(none)`.
- `force_door` y `pry_open_container` desaparecen al desequipar la palanca.
- `force_door` y `pry_open_container` vuelven a aparecer al reequipar la palanca si el target sigue valido.
- La revalidacion de M23.0.2 sigue siendo la proteccion real antes de ejecutar.
- No se duplicaron reglas de tags/items/stats en el panel.
- No se agrego sistema de eventos grande ni refactor grande de `InteractionSystem`.
- Estado: validated.

### Deuda Tecnica Menor Detectada

- GameDataManager mostraba warning de DontDestroyOnLoad porque no estaba en un root GameObject.
- Milestone 12.1 movio GameDataManager a root.
- Validado en Unity: el warning ya no aparece.

## Comportamiento Actual Importante

Milestone 12.1 validado:

- el warning de `DontDestroyOnLoad` ya no aparece;
- `CoreDataSystem` carga correctamente;
- Milestone 12 y Milestone 11 siguen funcionando sin cambios.

Milestone 13 validado:

- `weapon_tags` sigue activo y significa required equipped item tags;
- `ActionAvailabilityEvaluator.Evaluate()` permite explicar disponibilidad y bloqueos;
- `InteractionSystem` mantiene el contrato de devolver solo acciones disponibles;
- logs detallados se activan con `WorldInteractionDebugTester.logAvailabilityDetails`;
- `DataValidator` puede emitir warnings no destructivos para `weapon_tags` sin item cargado;
- `DataValidator` no bloquea la carga;
- con palanca equipada aparecen correctamente `force_door` y `pry_open_container`;
- sin item equipado se bloquean correctamente las acciones de herramienta;
- action duration y runtime tags siguen funcionando;
- no se tocaron JSON ni sistemas validados.

Milestone 14 validado:

- el flujo esperado es empezar sin item equipado, recoger una palanca del mundo, verla con `I`, equiparla y habilitar `force_door` / `pry_open_container`;
- `InventoryComponent` v0 funciona;
- el jugador inicia sin item equipado;
- `WorldItemPickup` funciona con `rusted_crowbar_01`;
- `InventoryComponent` manda sobre `DebugInventory` si existe;
- el caso sin item equipado desde `InventoryComponent` no cae a fallback;
- `pick_up_item` dura 0.5s y crea una `ItemInstance` runtime desde `rusted_crowbar_01`;
- `pick_up_item` marca el target como `picked_up`, remueve `pickupable` y desactiva colliders/renderers;
- al recoger, el item se agrega al `InventoryComponent`;
- la palanca del mundo queda oculta/no interactuable;
- `InventoryDebugPanel` abre con `I` y permite equipar/unequip;
- al equipar la palanca, `force_door` y `pry_open_container` aparecen correctamente;
- `InteractionSystem` sigue evaluando solo por definition id recibido en `InteractionQuery`.
- action duration y runtime tags siguen funcionando.

Milestone 15 validado:

- `pry_open_container` abre el contenedor como antes;
- `search_container` aparece solo con `opened_container` y `lootable_container`;
- `search_container` dura 1.5s;
- al saquear, se agrega `scrap_metal_01` al `InventoryComponent`;
- `InventoryDebugPanel` muestra `Scrap Metal`;
- el feedback textual usa `DebugActionExecutionResult` y `ContextualActionDebugResultPanel`;
- al saquear, `lootable_container` se remueve y `looted_container` se agrega;
- `search_container` ya no aparece despues de saquear;
- `InteractionSystem` sigue desacoplado del inventario y del loot.

Milestone 16 validado:

- `SampleScene` funciona como primer POI jugable compacto;
- el loop completo de POI funciona desde recoger/equipar palanca hasta obtener Scrap Metal;
- runtime tags quedan correctos: `picked_up`, `forced_open`, `opened_container` y `looted_container`;
- `pickupable`, `locked_door`, `sealed_container` y `lootable_container` se remueven cuando corresponde;
- data load sigue OK con 0 errors y 0 warnings;
- `InteractionSystem` sigue desacoplado;
- no se tocaron codigo ni JSON.

Milestone 17 validado:

- `GameplayFeedbackLog` registra entradas estructuradas runtime-only;
- `DebugFeedbackLogPanel` solo lee `Entries`;
- el feedback es append/read, sin listeners, subscriptions, callbacks, dispatch ni payload generico;
- `ItemPickedUp`, `ItemEquipped`, `ItemUnequipped`, `ActionCompleted`, `LootReceived` y `TargetStateChanged` se registran en el loop del POI;
- el gameplay no depende del panel de feedback;
- `InteractionSystem` no fue tocado;
- no se creo journal, quest log, UI final, save system, EventBus ni sistemas grandes.

Milestone 18 validado:

- el diagnostico de disponibilidad es runtime-only, debug/fundacional y no persistente;
- el diagnostico usa `ActionAvailabilityEvaluator.Evaluate()` y `ActionAvailabilityResult`;
- `GetAvailableActions()` sigue devolviendo solo acciones disponibles;
- el menu contextual ejecutable no cambio respecto a Milestone 17;
- `DebugActionAvailabilityPanel` muestra acciones disponibles/bloqueadas, razones de bloqueo y snapshots de contexto;
- `DebugActionAvailabilityPanel` se alterna con F8 y arranca oculto;
- `DebugFeedbackLogPanel` se alterna con F7 y arranca oculto;
- `InventoryDebugPanel` sigue funcionando con `I`;
- `GameplayFeedbackLog` sigue separado y funcionando;
- no se toco JSON, loaders, database, validator, `GameplayFeedbackLog` base, combate, IA, save system, journal, quest log ni UI final.

Milestone 19.1 validado:

- `WorldObjectStateView` soporta color debug por regla visual usando `MaterialPropertyBlock`;
- los colores reflejan runtime tags sin modificar gameplay ni materiales compartidos;
- puerta cambia de rojo oscuro a verde tras `force_door`;
- contenedor cambia de naranja a cian y luego gris oscuro;
- palanca se oculta tras `pick_up_item`;
- F7, F8 e I siguen funcionando;
- el menu contextual sigue mostrando solo acciones disponibles;
- no se toco JSON, `InteractionSystem`, `ActionAvailabilityEvaluator`, `GameplayFeedbackLog` ni diagnostics.

Milestone 19.2 validado:

- `SampleScene` mantiene geometria estable para puerta y contenedor;
- puerta y contenedor comunican estados solo por color debug;
- se neutralizaron rotaciones, variantes visuales y deformaciones raras;
- la palanca sigue ocultandose con `SetActive` cuando tiene `picked_up`;
- data load sigue OK con 0 errors y 0 warnings;
- F7, F8 e I siguen funcionando;
- el menu contextual sigue mostrando solo acciones disponibles;
- no se toco codigo, JSON ni gameplay.

Milestone 20 validado:

- `ItemStorage` e `ItemStorageEntry` forman la base comun runtime-only de almacenamiento de items con cantidades simples;
- `InventoryComponent` usa `ItemStorage` internamente sin romper `AddItemByDefinitionId`, pickup, equip ni `InventoryDebugPanel`;
- `ContainerLootComponent` inicializa storage interno una sola vez desde su loot table;
- el contenido del contenedor existe antes de ser accesible;
- `search_container` transfiere contenido existente al inventario y no re-rollea loot;
- las cantidades simples se muestran en `InventoryDebugPanel` cuando `Quantity > 1`;
- el contenedor queda `looted_container` y no vuelve a entregar loot;
- data load sigue OK con 0 errors y 0 warnings;
- F7, F8 e I siguen funcionando;
- el menu contextual sigue mostrando solo acciones disponibles;
- no se toco JSON, schema, `InteractionSystem`, `ActionAvailabilityEvaluator`, diagnostics ni `GameplayFeedbackLog` base;
- no se agrego UI final, peso, slots, grid, save system ni contenedores anidados.

Milestone 21 validado:

- `WorldObjectDebugInfo` selecciona textos de inspeccion por `RuntimeTags`;
- las reglas usan `requiredTags`, `forbiddenTags` y prioridad, con fallback a campos existentes;
- `DebugActionExecutor` usa esos textos en `examine_object`;
- `ContainerLootComponent` expone `[DEBUG STORAGE]` para inspeccion debug de storage;
- `ContainerLootComponent` valida acceso antes de transferir loot;
- examinar puerta cerrada muestra texto `locked_door`;
- tras `force_door`, examinar puerta muestra texto `forced_open`;
- examinar contenedor sellado muestra texto `sealed_container` + `[DEBUG STORAGE]`;
- tras `search_container`, examinar contenedor muestra `looted_container`;
- el contenedor no entrega loot dos veces;
- F7, F8 e I siguen funcionando;
- el menu contextual sigue mostrando solo acciones disponibles;
- no se toco JSON, schema, `InteractionSystem`, `ActionAvailabilityEvaluator`, diagnostics ni `GameplayFeedbackLog`.

Milestone 21.0.1 validado:

- la seleccion condicional usa `RuntimeTags` reales;
- la puerta forzada ya no muestra el texto de puerta trabada;
- las reglas de puerta son mutuamente excluyentes;
- `forced_open` tiene mayor prioridad que `locked_door`;
- no se tocaron JSON ni sistemas prohibidos.

Milestone 22 validado:

- `ActorNeedsComponent` agrega hunger/thirst runtime de forma generica para actores;
- Hunger/Thirst decaen en Play Mode;
- consumibles por JSON cerrado usan `consumable.restore_needs`;
- `water_bottle_01` restaura `thirst`;
- `food_ration_01` restaura `hunger`;
- cajas debug de suministros usan loot tables y storage interno;
- data load sigue OK con 0 errors y 0 warnings.

Milestone 22.1 validado:

- `ActorNeedsDebugPanel` muestra Hunger/Thirst arriba a la izquierda;
- consumir agua/comida registra `ItemUsed` en `GameplayFeedbackLog`;
- `max_stack` en `ItemDefinition` controla stackeo simple;
- `ItemStorage` mergea stacks por mismo `definitionId` hasta `max_stack`;
- `Scrap Metal x1 + Scrap Metal x500` queda como `Scrap Metal x501`;
- `search_container` abre `ItemStorageDebugPanel`;
- `ItemStorageDebugPanel` permite `Take 1`, `Take Stack`, `Take All` y `Close`;
- `looted_container` se aplica solo cuando el storage queda vacio;
- cajas debug nuevas quedan cian con loot y gris/negro vacias mediante `WorldObjectStateView`.

Milestone 22.1.1 validado:

- `ActorNeedsDebugPanel` e `ItemStorageDebugPanel` estan correctamente presentes/conectados en `SampleScene`;
- `search_container` encuentra o crea el panel de storage;
- los clicks sobre paneles debug no mueven ni disparan acciones detras;
- Food/Water Debug Crate y Misc Debug Crate reflejan colores de estado correctamente.

Milestone 22.1.2 validado:

- `equippable` es boolean funcional de `ItemDefinition`, no tag;
- `InventoryDebugPanel` muestra `Equip` solo si `equippable == true`;
- la palanca sigue equipable;
- agua, comida y scrap no muestran `Equip`;
- agua/comida siguen mostrando `Use`.

Milestone 23 validado:

- `InventoryComponent` separa Storage y Equipped como base de inventario de actor;
- `right_hand` es el primer slot runtime funcional;
- `rightHandItemInstanceId` es la fuente real del item equipado;
- el item equipado sigue dentro de `ItemStorage`;
- la palanca se equipa solo en `right_hand` desde JSON;
- `equip.equippable`, `equip.allowed_slots` y `equip.occupied_slots` estan soportados;
- `equippable` plano queda como compatibilidad temporal;
- `DataValidator` detecta contradicciones entre `equippable` plano y `equip.equippable`;
- `InventoryComponent` rechaza internamente items invalidos;
- `InventoryDebugPanel` muestra Equipped separado de Storage;
- agua/comida/scrap no se equipan;
- agua/comida siguen usando `Use`;
- `InteractionSystem` sigue habilitando `force_door` y `pry_open_container` con la palanca equipada;
- las acciones se revalidan antes de ejecutarse;
- el menu contextual se refresca si cambia el item equipado;
- data load 0 errors / 0 warnings;
- F7, F8, I, Hunger/Thirst, `GameplayFeedbackLog`, `ItemStorageDebugPanel` y loot de cajas siguen funcionando;
- loot final validado: Scrap x501, Water x500, Food x500, Crowbar x1.

Con `InventoryComponent` asignado:

- el item equipado sale exclusivamente de `InventoryComponent`;
- si `InventoryComponent` no tiene item equipado, se considera sin item;
- no se usa `DebugInventory` ni `equippedItemDefinitionId` legacy como fallback.

Sin `InventoryComponent` y con `DebugInventory` asignado:

- el item equipado sale de `DebugInventory`;
- si `DebugInventory` no tiene item equipado, se considera sin item;
- no se usa `equippedItemDefinitionId` legacy como fallback.

Sin `InventoryComponent` ni `DebugInventory` asignados:

- `ActorInteractionContext` conserva `equippedItemDefinitionId` como fallback legacy.

Con equipped item definition id rusted_crowbar_01:

- en una puerta con locked_door aparece force_door;
- en una puerta con locked_door e inspectable aparecen force_door y examine_object;
- en un contenedor con sealed_container aparece pry_open_container;
- en un contenedor con sealed_container e inspectable aparecen pry_open_container y examine_object;
- en un objeto solo inspectable aparece examine_object.

Con equipped item definition id none o vacio:

- no hay error;
- se considera que no hay item equipado;
- las acciones que requieren weapon_tags no aparecen;
- examine_object puede aparecer si el target tiene inspectable.

### Milestone 23.1: Lootable Debug Actor + Health v0

- Milestone 23.1 implementado y validado como base debug de health y actor muerto looteable.
- `ActorHealthComponent` v0 funciona para Player y Debug NPC Capsule.
- Health usa max/current health, low health threshold y estados runtime.
- Estados runtime validados: `alive_actor`, `damaged_actor`, `low_health_actor`, `dead_actor` y `lootable_actor`.
- Health no pinta colores directamente; actualiza tags runtime y `WorldObjectStateView` representa estados.
- Player y NPC se ven verdes vivos.
- Actor con low health se ve rojo.
- Actor muerto se ve negro.
- Debug NPC Capsule puede recibir dano por accion debug contextual.
- Debug NPC muerto agrega `dead_actor + lootable_actor` si tiene inventario.
- `search_body` aparece solo con `dead_actor + lootable_actor`.
- `search_body` abre `ItemStorageDebugPanel` reutilizado mediante fuente reusable de storage.
- El cadaver no usa `ContainerLootComponent`.
- Loot del cuerpo transfiere item instances al inventario del player.
- Al vaciar el cuerpo, se remueve `lootable_actor` y `search_body` desaparece, manteniendo `dead_actor`.
- `DebugActorInventorySeeder` existe solo como componente debug, no como sistema de perfiles de NPC.
- `bandage_01` fue agregado como consumible medico simple.
- `bandage_01` usa `consumable.restore_health.amount = 25`.
- Bandage no se equipa.
- Bandage cura al Player y consume 1 solo si restaura health.
- Si Player esta full health, Bandage no se consume.
- Survival Supply Debug Crate mantiene el loot table ID existente y contiene Water Bottle x500, Food Ration x500 y Bandage x500.
- Agua/comida siguen restaurando Hunger/Thirst.
- Cajas normales siguen usando `ContainerLootComponent`.
- `ItemStorageDebugPanel` sigue funcionando con cajas y actor muerto.
- M23 sigue funcionando: `right_hand`, crowbar, `force_door`, `pry_open_container`, revalidacion de acciones y refresh del menu contextual.
- Data Load validado: 0 errors / 0 warnings.
- F7, F8, I, Hunger/Thirst, `GameplayFeedbackLog`, `ItemStorageDebugPanel` y loot manual siguen funcionando.
- No se creo IA, combate real, enemigos reales, companeros reales, muerte real del jugador, game over, heridas complejas, save system, UI final ni perfiles JSON complejos de NPC.
- Estado: validated.

### Milestone 23.1.1: Hotfix - Health Examine Texts + Player Debug Damage

- Milestone 23.1.1 implementado y validado como hotfix de lectura/validacion de health.
- `damaged_actor` se agrega cuando currentHealth < maxHealth y el actor sigue vivo.
- Full health vivo queda como `alive_actor`.
- Danado vivo queda como `alive_actor + damaged_actor`.
- Baja salud viva queda como `alive_actor + damaged_actor + low_health_actor`.
- Muerto NPC queda como `dead_actor + lootable_actor` si tiene loot, o `dead_actor` si fue vaciado.
- Player puede recibir dano por boton debug en `ActorNeedsDebugPanel`.
- Player en 0 health sigue siendo solo debug visual/numerico: sin muerte real, game over, bloqueo de movimiento/acciones ni `lootable_actor`.
- `ActorNeedsDebugPanel` muestra Hunger, Thirst, Health y boton `Debug Damage Player`.
- Debug NPC Capsule muestra textos de examinar distintos segun estado: vivo full health, danado, low health y muerto.
- Estado: validated.

### Milestone 23.1.2: Hotfix - Debug Player Health Feedback

- Milestone 23.1.2 implementado y validado como hotfix de trazabilidad debug.
- `Debug Damage Player` registra una entrada `Info` en `GameplayFeedbackLog`.
- La entrada incluye actor/player, dano aplicado y health antes/despues.
- No se convirtio en `ActionDefinition`.
- No pasa por `DebugActionProgressController`.
- El boton sigue danando al Player.
- Player sigue cambiando de color por estado de salud.
- Bandage sigue curando.
- NPC damage y `search_body` siguen funcionando igual.
- Estado: validated.

### Post-M23.1 Functional Audit / Cleanup Pass

- Functional Audit / Cleanup Pass post-M23.1 cerrada y validada antes de M24.
- Cleanup Pass 1 elimino scripts debug/legacy confirmados como no referenciados: `GameDataDebugTester`, `ActionAvailabilityDebugTester` y `DebugPlayerContext`, junto con sus `.meta`.
- Cleanup Pass 2 elimino de `SampleScene` el GameObject inactivo `Deprecated_ActorInteractionContext_Legacy`.
- Debug Player conserva el `ActorInteractionContext` activo usado por `WorldInteractionDebugTester`.
- Cleanup Pass 3 agrego `ActionEffectTypes` en `Assets/_OldScars/Scripts/Core/Actions/`.
- `DataValidator` y `DebugActionExecutor` usan las mismas constantes para `add_tag`, `remove_tag`, `show_target_info`, `pick_up_item`, `search_container`, `apply_damage`, `kill_actor` y `search_actor_inventory`.
- No se cambio JSON, no se cambiaron actions/effects y no se cambio semantica de ejecucion.
- Validado en Unity: Data Load OK con 0 errors y 0 warnings.
- Validado en Unity: Crowbar pickup, `right_hand`, `force_door`, `pry_open_container`, `search_container`, `debug_damage_actor`, `low_health_actor`, `dead_actor + lootable_actor`, `search_body` y bandage siguen funcionando.
- El warning de Unity.AI.Toolkit Account API no pertenece a Old Scars.
- Estado: validated.

### Milestone 24: Actor Profile Pipeline v0

- M24.1 agrego `ActorProfileDefinition`, `actor_profiles/actor_profiles.json`, carga en `GameDataLoader` y registro/consulta en `GameDatabase`.
- M24.2 agrego validacion fuerte en `DataValidator` para type, id, display name, initial tags, health e initial inventory; `equipped` se rechaza porque todavia no esta soportado.
- M24.3 agrego `ActorProfileComponent` para aplicar una sola vez display name, initial tags, health e initial inventory sobre componentes existentes.
- M24.4 migro Debug NPC Capsule en `SampleScene` a `actorProfileId = debug_npc_capsule_01` y retiro `DebugActorInventorySeeder` de ese actor.
- Debug NPC Capsule recibe `bandage_01 x3` y `scrap_metal_01 x2` desde `actor_profiles.json` sin duplicar inventario.
- `DebugActorInventorySeeder.cs` no fue eliminado y queda como candidato legacy/debug para una futura limpieza controlada.
- Validado en Unity: Data Load OK con 0 errors, 0 warnings y `ActorProfiles: 1`.
- Validado en Unity: `pick_up_item`, `right_hand`, `force_door`, `pry_open_container`, `search_container`, `debug_damage_actor`, `low_health_actor`, `dead_actor`, `lootable_actor` y `search_body` siguen funcionando.
- Estado: validated.

### Milestone 25: World Object Profile v0

- Se agrego el pipeline minimo data-driven de World Object Profiles.
- `WorldObjectProfileDefinition` representa `display_name` e `initial_tags` reutilizables.
- `world_object_profiles.json` agrega `debug_locked_door_01`.
- `GameDataLoader` carga World Object Profiles.
- `GameDatabase` registra, consulta y reporta World Object Profiles.
- `DataValidator` valida type, id, snake_case, unicidad, display name, initial tags y referencias de tags.
- `WorldObjectProfileComponent` espera a que `GameDataManager` este ready y aplica el profile una sola vez sobre componentes existentes.
- Debug Locked Door fue migrado a `worldObjectProfileId = debug_locked_door_01`.
- Validado en Unity: Data Load OK con 0 errors, 0 warnings y `WorldObjectProfiles: 1`.
- Validado en Unity: Debug Locked Door recibe nombre/tags desde profile y `force_door` sigue funcionando.
- Estado: validated.

### Milestone 26: Storage Transfer v0 / Bidirectional Item Transfer

- `ItemStorageDebugPanel` permite mover items desde storage hacia Player Inventory y desde Player Inventory hacia storage.
- Se agregaron `Take 1`, `Take Stack`, `Take All`, `Deposit 1` y `Deposit All`.
- Transferencias completas conservan la `ItemInstance`.
- Transferencias parciales dividen stacks correctamente.
- La transferencia evita duplicar o destruir items.
- Si se deposita completamente el item equipado, se limpia `right_hand`.
- Contenedores y cuerpos restauran estado de contenido al depositar cuando corresponde.
- Validado en Unity con Debug Sealed Container, Survival Supply Debug Crate, Misc Debug Crate y cuerpo de Debug NPC Capsule.
- Estado: validated.

### Milestone 26.0.1: Storage Panel Layout Swap

- Se cambio solo el layout visual de `ItemStorageDebugPanel`.
- Player Inventory queda a la izquierda.
- Open Storage queda a la derecha.
- Deposit mueve izquierda -> derecha.
- Take mueve derecha -> izquierda.
- No cambio la logica de transferencia.
- Validado visualmente en Unity.
- Estado: validated.

### Milestone 27: Search vs Open Storage v0

- Se separo la primera revision de un contenedor natural del acceso posterior a su storage.
- Se agregaron los tags `unsearched_container` y `storage_accessible`.
- `search_container` requiere `opened_container + unsearched_container`, conserva barra de carga, remueve `unsearched_container`, agrega `storage_accessible` y abre `ItemStorageDebugPanel`.
- Se agrego la accion/effect cerrado `open_storage`.
- `open_storage` requiere `storage_accessible`, dura 0, abre el mismo panel aunque el storage este vacio y no genera loot nuevo.
- Vaciar un contenedor no elimina `storage_accessible`.
- Debug Sealed Container, Survival Supply Debug Crate y Misc Debug Crate fueron migrados al nuevo modelo.
- `search_body` y `LootableActorInventoryComponent` no fueron redisenados.
- `lootable_container` y `looted_container` siguen existiendo por compatibilidad.
- Validado en Unity: Data Load OK con 0 errors y 0 warnings.
- Validado en Unity: pry -> search inicial -> open posterior funciona; `open_storage` abre storage vacio y M26 sigue funcionando.
- Estado: validated.

### Milestone 32: Debug Test House Kitchen Containers v0

- M32 implementado en `SampleScene` y pendiente de validacion manual en Unity.
- `M32_DebugTestHouse/Containers/Fridge` fue configurado como container funcional con `house_fridge_loot_01`.
- `M32_DebugTestHouse/Containers/Oven` fue configurado como container funcional con `house_oven_loot_01`.
- `M32_DebugTestHouse/Containers/Countertop` fue configurado como container funcional con `house_countertop_loot_01`.
- `M32_DebugTestHouse/Containers/Cupboard` fue configurado como container funcional con `house_cupboard_loot_01`.
- `M32_DebugTestHouse/Containers/Upper countertop` fue configurado como container funcional con `house_upper_cupboard_loot_01`.
- Los cinco objetos reutilizan `WorldObjectTags`, `WorldObjectDebugInfo`, `ContainerLootComponent`, `WorldObjectStateView`, `search_container`, `open_storage` e `ItemStorageDebugPanel`.
- Cada container quedo en layer `Interactable`, con tags iniciales `opened_container + unsearched_container + inspectable` y tags semanticos propios.
- Loot tables nuevas agregadas a `container_loot.json`: `house_fridge_loot_01`, `house_oven_loot_01`, `house_countertop_loot_01`, `house_cupboard_loot_01` y `house_upper_cupboard_loot_01`.
- Item IDs existentes usados: `food_ration_01`, `water_bottle_01`, `bandage_01`, `ammo_303_british_01` y `scrap_metal_01`.
- Tags nuevos agregados a `tags.json`: `kitchen`, `food_storage`, `oven`, `cooking_station`, `workstation_candidate`, `countertop`, `food_prep_surface`, `cupboard`, `storage` y `upper_cupboard`.
- `Oven` queda preparado semanticamente como posible workstation futura solo mediante tags.
- No se implementaron crafting, recetas, WorkstationComponent, UI nueva, puertas, player, movimiento, armas ni animaciones.
- No se tocaron scripts C#, prefabs ni crates debug existentes.
- Estado: implemented; pendiente de validacion manual en Unity.

### Milestone 32.2: Real Door System v0

- M32.2 implementado en `SampleScene` y pendiente de validacion manual en Unity.
- Se agrego `DoorSwingController` como componente chico en `Core/Interactions`.
- `DoorSwingController` solo lee `WorldObjectTags` y rota `DoorVisualPivot`; no lee input, no muta tags, no ejecuta acciones y no toca inventario.
- Tags nuevos agregados a `tags.json`: `closed_door` y `opened_door`.
- `forced_open` queda registrado como tag legacy; no es estado principal nuevo.
- `actions.json` agrega `open_door` y `close_door` usando solo `remove_tag` y `add_tag`.
- `force_door` migro de `locked_door -> forced_open` a `locked_door -> opened_door`.
- `world_object_profiles.json` agrega `debug_closed_door_01` con `closed_door + inspectable`.
- `M32_DebugTestHouse/Doors/Debug Locked House Door Entrance` conserva `debug_locked_door_01` e inicia como `locked_door`.
- `M32_DebugTestHouse/Doors/Debug Locked House Door Bedroom` usa `debug_closed_door_01` e inicia como `closed_door`.
- Ambas puertas M32 recibieron `DoorSwingController` apuntando a su `DoorVisualPivot`.
- En ambas puertas M32, el `BoxCollider` solido del root quedo deshabilitado para no bloquear siempre.
- En ambas puertas M32, `DoorVisual` queda en layer `Interactable` con `BoxCollider` solido hijo para bloquear cerrado, rotar abierto y seguir resolviendo `WorldObjectTags` desde el root.
- `WorldObjectStateView` y `WorldObjectDebugInfo` de las puertas M32 reconocen `locked_door`, `closed_door`, `opened_door` y `forced_open` legacy.
- La puerta debug vieja fuera de M32 fue actualizada para que `opened_door` tenga reglas/textos coherentes despues del cambio global de `force_door`.
- No se tocaron containers, loot, inventario, player, movimiento, armas, animaciones del player, UI nueva, crafting, HingeJoint, Rigidbody ni fisica real.
- Estado: implemented; pendiente de validacion manual en Unity.

### Milestone 32.4: Interior Visibility Raycast v0

- M32.4 implementado en `SampleScene` y pendiente de validacion manual en Unity.
- Se agregaron `BuildingInteriorVolume`, `BuildingOccluderTarget` y `BuildingVisibilityManager` en `Core/Interactions`.
- `BuildingVisibilityManager` usa referencias serializadas a `Main Camera`, `Debug Player`, `HouseInteriorVolume` y targets, con fallback de inicializacion solamente.
- El sistema corre raycasts en `LateUpdate` solo cuando hay building actual detectado.
- `BuildingInteriorVolume` usa `OnTriggerEnter/Exit` con `ActorInteractionContext` player y fallback `ContainsPlayer` por bounds/local-space.
- `BuildingOccluderTarget` separa `renderersToHide` de `collidersToDisableWhileHidden` y guarda estados iniciales para restaurarlos exactamente.
- Las paredes actuales de `M32_DebugTestHouse/Structure` ocultan solo renderers; sus `BoxCollider` estructurales no se desactivan.
- `CasaPrimerPiso` queda configurado como `hideAlwaysWhenInside` con renderer y collider opt-in, preservando que ambos arrancan deshabilitados.
- No se uso `GameObject.SetActive(false)`.
- No se agrego layer `InteriorOccluder` ni se modifico `TagManager.asset`.
- No se tocaron puertas, containers, loot, inventario, player movement, armas, animaciones, JSON ni `DataDriven_JSON_Rules.md`.
- Estado: implemented; pendiente de validacion manual en Unity.

### Milestone 32.4.1: Door Pivot Repair + Interior Visibility Cast Debug/Stability

- M32.4.1 implementado en el checkout y pendiente de validacion manual en Unity.
- Se agrego `RepairM32DoorPivotsTool` como herramienta editor-only bajo `Assets/_OldScars/Editor/Debug`.
- La herramienta agrega los menus `Old Scars/Debug/Validate M32 Door Pivots` y `Old Scars/Debug/Repair M32 Door Pivots`.
- La herramienta opera solo bajo `M32_DebugTestHouse/Doors` y busca puertas con `DoorSwingController`.
- `Validate M32 Door Pivots` reporta root scale no normalizada, `DoorSwingController` sin `doorPivot`, `DoorVisualPivot` sin `DoorVisual`, visual scale invalida o near zero y posiciones locales absurdas.
- `Repair M32 Door Pivots` no se ejecuta automaticamente, no recrea puertas, no reemplaza roots funcionales, no borra componentes y no toca JSON.
- La reparacion normaliza solamente transforms de root, `DoorVisualPivot` y `DoorVisual`.
- Si root scale no es uno y `DoorVisual.localScale` esta cerca de uno, la herramienta transfiere la escala del root al visual y luego deja root/pivot en scale `1,1,1`.
- La herramienta deriva `halfWidth` desde `DoorVisual.localScale.x * 0.5`, con fallback `0.575`, y ubica pivot/visual a lados opuestos de la bisagra.
- `BuildingVisibilityManager` ahora castea desde player hacia camara usando los offsets `1.6`, `0.9` y `0.25`.
- `SphereCastAll` queda como cast principal con `sphereCastRadius = 0.35`.
- `RaycastAll` queda como fallback cuando `useSphereCasts` esta desactivado o el radio no es valido.
- Se agrego `OverlapSphere` alrededor de la camara con `cameraOverlapRadius = 0.45` para casos de camara pegada o dentro de una pared.
- Los hits se filtran por `BuildingOccluderTarget`, mismo `buildingId`, `hideByCameraRaycast` y ausencia de `WorldObjectTags` en padres.
- Se agregaron `drawDebugCasts = true`, `logHitChanges = false` y `debugDrawDuration = 0.05`.
- El debug visual usa lineas/rayos verdes sin hit valido, rojos con hit valido y azul/cyan para overlap de camara; tambien agrega gizmos al seleccionar el manager.
- `restoreDelay` se mantiene en `0.15`.
- La politica de colliders no cambio: paredes estructurales ocultan solo renderers; techo/pisos superiores/piezas visuales usan colliders opt-in.
- No se uso `GameObject.SetActive(false)`.
- No se movio camara, no se toco player movement y M32.4.1 no depende de cambios nuevos en `TagManager.asset`.
- No se tocaron containers, loot, inventario, JSON ni actions/tags/profiles; M32.4.1 no agrego cambios nuevos ni depende de `ProjectSettings/TagManager.asset`.
- Estado: implemented; pendiente de validacion manual en Unity.

### Grid Inventory Backend v0

- Implementado como bloque tecnico pendiente de validacion manual en Unity.
- `ItemStorage` sigue siendo la fuente de contenido/stacks; `GridInventoryLayout` mantiene placements por `ItemInstance.InstanceId`.
- Se agregaron preflight, reserva determinista, commit y rollback por snapshots para Add, Remove y Transfer.
- El Debug Player queda configurado con grilla `6x8`; inventarios NPC/cadaver y storages de container/world item conservan layout lineal.
- Los siete items Core recibieron `inventory.footprint` explicito; la ausencia de metadata usa fallback `1x1` y warning.
- `InventoryDebugPanel` muestra InstanceId, footprint, placement, orientacion y fallback en modo diagnostico read-only.
- `Take All`/`Deposit All` globales quedan deshabilitados si participa la grilla; operaciones individuales y por stack conservan sus APIs existentes.
- `right_hand` se conserva como compatibilidad temporal y solo se limpia despues de una mutacion exitosa que remueve la instancia.
- Los `max_stack = 999` existentes quedan registrados como deuda de balance y no se modificaron.
- No se agregaron tests EditMode, asmdef, UI final, drag-and-drop, peso, nesting, save/load ni equipamiento corporal.
- Estado: implemented; verificacion estatica solamente, no validated.

### M33.1: Visual Grid Inventory UI v0

- Implementada grilla OnGUI `6x8` reusable en `InventoryDebugPanel` y en la columna del jugador de `ItemStorageDebugPanel`.
- Los items se dibujan desde `GridPlacement` y `EffectiveWidth/EffectiveHeight`; la UI no ejecuta first-fit ni muta layout/storage directamente.
- Seleccion y drag usan `InstanceId`; preview/commit de recolocacion viven en `GridInventoryBackend` y solo actualizan `GridInventoryLayout`.
- Se agregaron ghost, destino valido/invalido, rotacion con `R`, no-op estable y cancelacion sin mutaciones.
- Una entry sin placement muestra `MISSING PLACEMENT`, loguea una vez por instancia y mantiene Legacy List manual.
- `inventory.icon_id` es opcional; los siete Core lo declaran y usan siete placeholders Sprite `512x512` importados desde los adjuntos aprobados.
- El resolver usa `Resources/OldScars/InventoryIcons/`, cachea aciertos/ausencias y conserva fallback determinista.
- Containers/cadaveres siguen como lista; Take/Deposit individual y por stack conservan backend existente; batch global sigue deshabilitado.
- Validado manualmente en Unity: grilla `6x8`, iconos, drag-and-drop, rotacion, seleccion, equipamiento y transferencias con containers/cadaveres funcionan.
- Data Load OK con 0 errors y 0 warnings.
- Estado: validated.

### M33.1.1: Inventory Footprint Rebalance + Universal Rotation

- Rebalanceados los footprints Core: rifle `6x1`, palanca `5x1`, botella `2x1`, scrap `2x2`, municion `1x1`, venda `1x1` y comida `1x1`.
- Se elimino el flag de rotacion del contrato JSON/C#; todos los footprints no cuadrados admiten orientacion alternativa.
- Los footprints cuadrados tratan la orden de rotacion como exito no-op, sin estado redundante ni incremento de `GridInventoryLayout.Version`.
- No se modificaron storage, transfers, `right_hand`, containers, cadaveres, pickup, drop, firearm, escena ni iconos.
- Validado manualmente en Unity: rotacion universal, no-op de cuadrados, pickup/drop, equipamiento, transfers y Data Load 0 errors / 0 warnings.
- Estado: validated.

### M33.2: Universal Grid Storage + Dual Grid Inventory UI v0

- Se agregaron `IGridStorageOwner`, `GridStorageRuntime` y `GridStorageTransferService` para reutilizar `ItemStorage`, `GridInventoryLayout` y `GridInventoryBackend` sin backends por tipo de owner.
- `InventoryComponent`, `ContainerLootComponent` y `LootableActorInventoryComponent` exponen el mismo contrato espacial; el cadaver delega al inventario real del actor.
- Containers y actores inicializan contenido primero y layout despues. Si el first-fit completo falla, el storage queda intacto y pasa a `LinearFallback` sin placements parciales.
- `InventoryGridDebugView` quedo desacoplado de `InventoryComponent` y dibuja cualquier `IGridStorageOwner` sin crear ni administrar placements.
- `ItemStorageDebugPanel` usa tres columnas OnGUI: Player Grid, informacion/acciones provisionales y External Storage Grid.
- Una sola `InventoryGridDragController` mantiene drag entre ambos lados por `InstanceId`, recolocacion interna, rotacion, preview, cancelacion y transferencia exacta de stack completo.
- El drag exacto rechaza merges ambiguos; Shift/clic y botones Take/Deposit 1/Stack usan auto-placement atomico y nunca ejecutan batch global.
- `InventoryUISessionController` centraliza `I`, `Escape`, cierre, cancelacion y bloqueo de input del mundo/camara; los paneles ya no escuchan esas teclas por separado.
- Los hooks de owner preservan acceso/tags de containers y cuerpos fuera de `GridInventoryBackend`; `right_hand` solo se limpia despues de una salida exitosa de esa misma instancia.
- `SampleScene` configura sealed crate `4x4`, supply/misc crates `6x5`, fridge `5x8`, oven `4x4`, countertop `5x3`, cupboards `4x5` y ambos NPC/cadaveres `6x8`.
- No se modificaron JSON, loot tables, puertas, visibilidad, arte, tests ni asmdef; no se agregaron Canvas, EventSystem ni UI Toolkit.
- Validado manualmente en Unity junto con M33.2.1.
- Estado: validated.

### M33.2.1: Partial Directed Merge + Stable Dual Grid UI

- Se separo el drop entre owners en placement exacto sobre celda vacia y merge dirigido sobre un `destinationInstanceId` concreto.
- La resolucion del receptor convierte cursor a celda y consulta footprints de `GridPlacement`; cualquier celda de un item multicelda resuelve la misma instancia.
- `ItemStorage.AddItemAsSeparateEntry` permite transferir un stack completo sin auto-merge, conservando su `InstanceId` y evitando presencia simultanea en ambos storages.
- `GridStorageTransferService` agrega preview/commit de merge dirigido con owners/IDs distintos, capacidad real, `IncompatibleStack` y `StackFull`.
- El merge parcial incrementa solo el receptor, conserva source ID/placement si queda cantidad y elimina source entry/placement solo al consumirlo completo.
- Los snapshots de `ItemStorage` y `GridInventoryLayout` restauran tambien sus versiones; fallos restauran ambos lados y la secuencia de IDs.
- Receipts exponen cantidad efectiva, IDs y `SourceWasRemoved`; hooks corren solo despues de `Success`, resincronizan containers/cuerpos y limpian `right_hand` solo si desaparecio la instancia equipada del jugador.
- Seleccion y lado activo se reconcilian por `InstanceId`; un merge exitoso selecciona el receptor.
- Mensajes de ambos paneles usan toast absoluto de 1.75 segundos con `Time.unscaledTime`, severidad y deduplicacion, sin filas `GUILayout`.
- El panel dual congela su rect al abrir la sesion y centra columnas calculadas desde las dimensiones de ambas grillas, sin depender del contenido ni mensajes.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; solo warnings preexistentes de `BuildingVisibilityManager`.
- No se modificaron JSON, escena, sprites, metas, puertas, visibilidad, transforms ni colliders; no se agregaron tests, asmdef, Canvas ni UI Toolkit.
- Validado manualmente en Unity junto con M33.2.
- Estado: validated.

### M33.2.2: Data-Driven Initial Item Orientation + Footprint Polish

- Se agrego `inventory.initial_orientation` opcional con valores cerrados `original`/`rotated` y fallback `original`.
- `DataValidator` rechaza valores desconocidos; la orientacion no se deriva de IDs, categoria, tipo, icono ni footprint.
- El first-fit prueba la orientacion inicial antes de la alternativa y normaliza footprints cuadrados sin estados/versiones redundantes.
- El rifle queda `7x2` original con inicio rotado efectivo `2x7`; la botella queda `2x1` original con inicio rotado efectivo `1x2`.
- Palanca `5x1`, scrap `2x2`, municion, venda y comida `1x1` declaran inicio original.
- Placements existentes, recolocacion manual, transferencia exacta y merge dirigido conservan sus contratos previos.
- No se modificaron `ItemStorage`, `GridStorageTransferService`, `right_hand`, tags, containers, cadaveres, sesion, toast, input, sprites, metas, icon resolver, escena, puertas ni visibilidad.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; solo cuatro warnings preexistentes de `BuildingVisibilityManager`.
- Estado: validated; validado manualmente en Unity por confirmacion del usuario.

### M33.3: Basic Carry Weight System v0

- `ItemPhysical.weight_kg` pasa a nullable para distinguir ausencia de cero; `DataValidator` exige presencia, valor finito y valor no negativo con diagnostico por item.
- Los siete items Core declaran pesos explicitos: rifle `4.2`, crowbar `2.3`, water `1.2`, scrap `0.10`, ammo `0.025`, bandage `0.08` y food `0.50` kg.
- `ActorCarryWeightComponent` suma on demand las entries de `InventoryComponent` usando `double`; capacidad blanda `30 kg`, hard limit `39 kg` y epsilon solamente para comparaciones.
- `ICarryWeightLimitedOwner` mantiene la politica opcional por owner; storages sin componente no adquieren limite ni conocen actores concretos.
- La carga inicial de actor profile usa el unico bypass controlado; altas runtime, pickup y transferencias incoming al jugador aplican la politica antes de mutar.
- Preview y commit de drag exacto/merge dirigido aplican la misma politica; merge pesa solo `TransferQuantity` y commit vuelve a obtener el preview actual.
- Rechazos no ejecutan hooks de transferencia ni efectos de pickup; outgoing, consumo, municion y drop no reciben un bloqueo nuevo.
- `InventoryDebugPanel` e `ItemStorageDebugPanel` muestran peso actual/capacidades/estado y pesos unitario/stack; los rechazos reutilizan el toast absoluto existente.
- `SampleScene` solo agrega `ActorCarryWeightComponent` al Debug Player con `baseCarryCapacityKg = 30` y `hardLimitMultiplier = 1.3`.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; solo cuatro warnings preexistentes de `BuildingVisibilityManager`.
- Estado: validated; validado manualmente en Unity por confirmacion del usuario.

### M34.1: Equipment Ownership & Slots Foundation

- Se agregaron definiciones data-driven `EquipmentSlotDefinition` y `EquipmentLayoutDefinition`, carga/registro/validacion y los archivos Core `equipment_slots.json` y `equipment_layouts.json`.
- `human_standard_01` contiene exactamente 17 slots agrupados y ordenados; `back` es generico y no existe `both_hands`.
- `ActorProfileDefinition` acepta `equipment_layout_id`; `debug_npc_capsule_01` referencia `human_standard_01` sin agregar componentes de equipment al NPC.
- `ItemEquip.slot_sets` es el schema definitivo de alternativas completas. La palanca declara mano derecha o izquierda; el rifle declara ambas manos en un solo set atomico. El schema legacy solo queda como compatibilidad, mapeando `right_hand` a `hand_right`.
- Se agrego `ActorItemOwnershipComponent` como vista agregada de inventario personal + equipment storage, con localizacion por `InstanceId` y validacion de ownership unico.
- Se agrego `ActorEquipmentComponent` con `ItemStorage` lineal separado y mapas de referencias de slots; el item multi-slot existe una sola vez en storage y peso.
- `EquipmentTransactionService` implementa preview/commit de equipar y desequipar, revalidacion por versiones, first-fit data-driven al volver al inventario y rollback completo de storages, layout, mapas, versiones y secuencia de IDs.
- `InventoryComponent` conserva sus APIs legacy, pero delega `right_hand` a `hand_right` cuando existe equipment; `ActorInteractionContext` usa mano derecha y luego izquierda.
- `ActorCarryWeightComponent` consulta ownership agregado cuando esta disponible; equipar/desequipar dentro del mismo actor no ejecuta preflight incoming y mantiene delta de peso cero, incluso en `HardBlocked`.
- `InventoryDebugPanel` e `ItemStorageDebugPanel` agregan una lista central fija de 17 slots, scroll persistente, filas vacias, indicador `2H`, seleccion canonica por `InstanceId`, auto-scroll y acciones debug. La grilla externa se conserva a la derecha.
- `InventoryUISessionController` sigue siendo la autoridad unica de la sesion; el estado de seleccion distingue personal/equipment/external sin agregar listeners independientes de `I` o `Escape`.
- No se modifico `SampleScene.unity` como parte de M34.1 y no se agregaron NPC/corpse equipment, item-owned storage, backpack, pockets, nesting, peso de subtrees, equip desde mundo, drop equipado, armor, save/load, modelos ni UI final.
- M34.2 queda diferido para item-owned storage/backpack foundation.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; solo cuatro warnings preexistentes de `BuildingVisibilityManager`.
- Estado: validated; validado manualmente en Unity por confirmacion del usuario.

### M34.1.1: Inventory & Equipment UI Cleanup

- Cleanup exclusivo de la UI debug OnGUI; no se modificaron backend, JSON, escena, ownership, peso, transferencias, placements, tags ni rollback.
- `InventoryDebugPanel` elimina visualmente el header legacy de `Right Hand`/`Unequip`, mantiene Close en la cabecera general y organiza Player Grid, Equipment y Selected Item con una altura comun.
- `ItemStorageDebugPanel` organiza Player Grid, Equipment + footer y External Storage Grid con el mismo body height; las grillas conservan dimensiones reales y el sobrante queda vacio dentro del fondo.
- La columna central divide Equipment viewport y Session/Actions footer mediante alturas calculadas y estables.
- Take 1/Stack y Deposit 1/Stack se dibujan dentro del footer visible; los detalles resumidos usan scroll vertical interno y no desplazan los botones fuera de la ventana.
- `EquipmentDebugListView` usa solo scrollbar vertical, fija el ancho de filas y separa slot alineado a izquierda de item/Vacio alineado a derecha, con clipping de nombres e indicador `2H`.
- El footer externo resuelve personal/equipment/external desde `InventoryUISessionSelection`, y los clicks de Legacy List actualizan la misma autoridad para evitar reaparicion de selecciones viejas.
- El toast absoluto permanece fuera de GUILayout y conserva su duracion/politica.
- Inventory Context Menu v0, weight-limited partial transfers y M34.2 item-owned storage siguen pendientes.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; solo cuatro warnings preexistentes de `BuildingVisibilityManager`.
- Estado: validated; validado manualmente en Unity por confirmacion del usuario.

### M34.1.2: Inventory Context Menu v0

- Se agrego un unico estado de menu contextual/dialogo de cantidad poseido por `InventoryUISessionController`, sin estado estatico ni menus independientes por panel.
- Se agregaron `InventoryContextActionKind`, contratos cerrados y `InventoryContextActionResolver`; el resolver es read-only, resuelve por `InstanceId`/owner y consulta consumibles y previews de Equipment.
- Clic derecho sobre Player Grid, External Grid y filas ocupadas de Equipment abre el menu absoluto sin participar en `GUILayout`; clic derecho vacio cierra y clic derecho durante drag solo cancela ese drag.
- Equipment reporta `slotId`, `InstanceId`, rect y boton; ambas filas de una instancia `2H` producen una sola autoridad y las mismas acciones.
- Equip/Unequip usan `PreviewEquip`/`PreviewUnequip` y `EquipmentTransactionService`; Use usa `InventoryItemUseService`; Drop usa `DroppedWorldItemSpawner`; Take/Deposit usan `GridStorageTransferService`.
- Las acciones Amount usan un modal absoluto con entero `1..quantity`, botones `-/+`, Enter/Confirmar, Escape/Cancelar, bloqueo del contenido y revalidacion completa antes del commit.
- La seleccion posterior permanece por `InstanceId`: equip pasa a Equipment, unequip a Personal, transferencias usan el destination/remainder del receipt y consumo/drop completo limpian la seleccion desaparecida.
- Se retiraron los botones fijos de Use/Equip/Unequip/Drop; Take/Deposit 1/Stack permanecen temporalmente como fallback de validacion.
- Use desde equipment se omite porque el servicio actual consume por indice del inventario personal; no se agrego una ruta improvisada.
- No se implementaron auto-swap, equip desde external, drop equipado, transferencias parciales automaticas por peso, item-owned storage ni UI final.
- No se modificaron `SampleScene.unity`, JSON Core, sprites, arte, `ItemStorage`, backends espaciales, ownership, peso, Equipment/transfer services, containers, cadaveres ni pickup.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; estado `implemented`, pendiente de validacion manual en Unity.

## Decisiones De Scope

- No hay inventario final.
- No hay equipment system final.
- No hay entidades runtime complejas.
- No hay combate real.
- No hay save system.
- No hay IA.
- No hay UI final.
- No hay journal.
- No hay quest log.
- No hay EventBus de gameplay.
- Los tags legacy `lootable_container` y `looted_container` se mantienen temporalmente por compatibilidad.
- Los titulos debug de storage necesitan una limpieza de naming futura.
- Los sistemas actuales son prototipos debug para probar flujo data-driven.
