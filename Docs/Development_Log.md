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
- Estado: validated; validado manualmente en Unity por confirmacion del usuario.

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
- Estado: validated; validado manualmente en Unity por confirmacion del usuario.

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
- Compilacion estatica de `Assembly-CSharp`: 0 errores.
- Estado: validated; validado manualmente en Unity por confirmacion del usuario, incluidos menu personal/external/equipment, cantidades, transferencias, drop, consumo y Data Load 0 errors / 0 warnings.

### M34.1.3: Inventory Context QoL & Atomic Equipment Replacement

- Se agregaron `EquipmentFailureCode`, `EquipmentReplacementPlan` y `EquipmentDisplacementPlan`: source, alternativa solicitada, desplazados unicos, todos sus slots, placements reservados y cuatro versiones esperadas.
- `PreviewEquipReplacing` valida equipabilidad/ownership/slots y usa una simulacion pura del layout personal que omite source antes de reservar todos los desplazados con el first-fit existente.
- La deduplicacion por `InstanceId` hace que un rifle referenciado por ambas manos se desplace una sola vez; cada desplazado conserva instancia, entry, cantidad y condiciones.
- `EquipReplacing` vuelve a ejecutar preview, compara plan y reservations, captura snapshots y realiza transfers cerrados; ante excepcion restaura personal/equipment storage, layout, mapas, versiones y secuencia de IDs.
- El reemplazo no consulta hard limit porque el ownership agregado y el peso total del actor no cambian; no usa external storage, auto-drop ni nuevos destinos.
- `EquipmentFailureMessageFormatter` traduce failure codes a mensajes de UI; el resolver ofrece `Equipar` para slots libres y `Equipar y reemplazar` con resumen de desplazados cuando el preview es viable.
- El modal Amount suma slider horizontal con redondeo entero, campo sincronizado, clamp `1..maximum` y saltos `-/+` de 1 o 10 con Shift.
- Para stacks de cantidad 1 se muestran solo `Tomar`, `Depositar` o `Soltar`; `Ver detalles` se oculta hasta M34.1.4.
- `ActorNeedsDebugPanel` cachea `InventoryUISessionController` y deja de dibujarse durante cualquier sesion. El footer externo elimina los botones Take/Deposit fijos; clic derecho, Shift+click, drag, merge y rotacion no se cambiaron.
- No se modificaron `ItemStorage`, `GridInventoryLayout`, transfer service, ownership/peso, containers, cadaveres, pickup/drop/use, escena, JSON, arte, prefabs ni loot tables. `GridInventoryBackend` solo agrega la simulacion interna reusable y sin mutaciones.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; solo cuatro warnings preexistentes de `BuildingVisibilityManager`.
- Estado: validated; validado manualmente en Unity por confirmacion del usuario, con Data Load 0 errors / 0 warnings y regresiones de equipment, dual-grid, containers/cadaveres, drag, merge, Shift+clic, pickup/drop y tags de loot confirmadas.

### M33.3.1: Weight-Limited Partial Transfers

- Se agrego `GridStorageTransferQuantityPolicy` con `Exact` y `ClampIncomingToActorHardLimit`; los overloads legacy de `TransferQuantityAuto`/`TransferStackAuto` siguen usando `Exact`.
- `CarryWeightQuantityLimit` y `ActorCarryWeightComponent.EvaluateIncomingQuantityLimit` calculan el maximo entero entrante contra el hard limit y el ownership agregado personal + equipment, sin sumar referencias de slots.
- El calculo usa `floor((remaining + epsilon) / unitWeight)`, valida el resultado contra el limite y trata peso unitario cero como no limitante.
- `PreviewTransferQuantityAuto` no muta y valida endpoints, source, peso y el plan espacial existente. El commit vuelve a ejecutar el preview y luego usa `GridInventoryBackend.TransferTo`, con su revalidacion, snapshots, reservations e invariantes.
- `InventoryMutationResult` y `GridStorageTransferReceipt` exponen requested, actual, source remaining, limit quantity y `WasLimitedByWeight`, conservando IDs y campos anteriores.
- Take Stack/Tomar todo y Shift+clic external -> player eligen clamp explicitamente. Take 1, Take Amount, drag exacto, merge dirigido, equipamiento, replacement, outgoing y storages no actor permanecen exactos.
- El parcial por peso sigue siendo `Success`; source conserva `InstanceId`, placement y seleccion External si queda remainder. El destino conserva su ID real al fusionar.
- Los hooks se notifican solo tras commit exitoso; containers y cadaveres siguen determinando loot/vacio desde su storage real, por lo que un remainder no los marca vacios.
- Se agregaron toasts humanos en espanol para parcial y bloqueo total, sin cambiar el toast absoluto ni crear UI nueva.
- No se modificaron escena, JSON, arte, sprites, prefabs, loot tables, tags, equipment, pickup/drop, necesidades, puertas, visibilidad, movimiento ni combate.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; solo cuatro warnings preexistentes de `BuildingVisibilityManager`.
- Estado: validated; validado manualmente en Unity por confirmacion del usuario.

### M34.2: Item-Owned Storage / Backpack Foundation

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- Se agrego `ItemStorageProfileDefinition` y el pipeline `item_storage_profiles.json` -> loader -> database -> validator/stats, con IDs unicos, dimensiones `1..64`, referencias validas y `max_stack = 1` obligatorio.
- `ItemInstance` posee opcionalmente un `ItemOwnedStorageRuntime`; storage, layout `8x10`, backend, versiones y contenido pertenecen al `InstanceId`, por lo que dos mochilas iguales no comparten estado.
- `ItemOwnedStorageRegistry` resuelve storage y owner raiz por identidad runtime, protege ciclos y solo reconcilia ownership despues de un commit exitoso.
- `GridStorageTransferService` reutiliza los backends existentes, aplica no-nesting antes de mutar, conserva receipts/rollback/hooks y omite preflight de peso solamente cuando source y target comparten owner raiz.
- `ItemWeightResolver` suma peso propio y subtree una sola vez. Pickup de mochila usa peso completo; entrada external -> mochila delega al hard limit del actor; movimientos Personal <-> Mochila mantienen delta cero.
- `small_backpack_01` pesa `1.50 kg`, ocupa `4x4`, usa `backpack_small_01` (`8x10`) y se agrega al inventario inicial debug mediante actor profile por tag `player`, sin editar escena.
- La UI OnGUI agrega selector de compartimentos, apertura desde Equipment `back`, transferencias contextuales, grilla izquierda activa frente a external, celda visual configurable y scroll horizontal/vertical.
- Nesting, pockets, multiples compartimentos, save/load, UI final y arte permanecen fuera de scope. `SampleScene.unity`, loot tables e iconos no se modificaron.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; permanecen cuatro warnings preexistentes de `BuildingVisibilityManager`.

### M34.2.1: Inventory Interaction Unification & Backpack Access

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- `InventoryContextActionResolver` ya no reduce capacidades por estar dentro de una mochila: use, equip/replacement y drop se resuelven por instancia, definicion y owner raiz.
- Se agrego una transaccion atomica para equipar desde item-owned storage reutilizando el backend actual, con snapshots de source, personal, equipment, slots e IDs; las alternativas de dos manos siguen siendo los slots reales declarados.
- Las filas de equipment aceptan drag: primero equip/replacement compatible y, si no aplica, transferencia first-fit al storage del ocupante con no-nesting.
- Shift+clic y doble clic usan la misma ruta de stack. La politica automatica entrante se obtiene del owner raiz del destino y aplica clamp por hard limit; Take 1/cantidad, drag exacto y merge dirigido permanecen exactos.
- El selector personal enumera solo storages equipados. `Revisar contenedor` abre un overlay OnGUI por `InstanceId` para una mochila guardada y `Escape` lo cierra antes que la sesion.
- M34.2, M34.2.1 y M34.2.1a fueron validados posteriormente por confirmacion manual del usuario.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; solo los cuatro warnings preexistentes de `BuildingVisibilityManager`.

### M34.2.1a: Fix Equipment From Item-Owned Storage

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- La causa del falso stale era `TryReserveIncomingAfterRemoving(null, ...)`: esa API exige un source existente en la grilla personal y devolvia `SourceNotFound` para cualquier source real dentro de mochila.
- `GridInventoryBackend.TryReserveIncoming` simula ahora la entrada de equipment desplazado sin remover un source personal inexistente; la variante historica con remocion conserva sus validaciones.
- Los previews item-owned capturan container, versiones de storage/layout y placement. Equip/replacement re-resuelven el runtime por identidad, revalidan personal/equipment/source y capturan snapshots antes del commit.
- Menu contextual y drag siguen usando `ActorEquipmentComponent` como unica entrada a la misma transaccion.
- El drop sobre storage equipado conserva el compartimento visible y mantiene el toast existente. El cambio por hover de `0.30 s` queda diferido.
- M33.3.1, M34.2, M34.2.1 y M34.2.1a quedan `validated` por confirmacion manual del usuario. El hover temporizado para abrir mochila sigue diferido.

### M34.2.1b: Unified Context Actions for Equipped Items

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- `InventoryContextActionResolver.ResolveEquipment` reutiliza el menu universal y deriva acciones desde la instancia equipada, sus slots ocupados, destinos personales accesibles y external storage.
- El request de contexto conserva `source kind = Equipment`, el `InstanceId`, el slot clicado y una copia del set completo de slots para rechazar estado stale sin resolver otra instancia.
- `EquipmentTransactionService` agrega preview/commit atomico para recolocar una instancia ya equipada y para transferirla a otro storage. Replacement reserva placements, conserva IDs y restaura backends, slots, versiones e ID sequence ante fallo.
- Rifle y cualquier item multi-slot se resuelven una vez por `InstanceId`; el slot actual y los no-op no generan acciones. Las mochilas conservan `ReviewOwnedStorage` y el guard generico impide introducir un item-owned storage dentro de otro.
- Transferencias hacia external notifican el hook de destino despues del commit; drop usa el mismo storage runtime del world item y conserva contenido item-owned.
- Las rutas exitosas llaman una sola vez a `CommitVisualState`; preview, stale state, fallo y rollback no publican cambios visuales.
- Compilacion estatica de `Assembly-CSharp` y `Assembly-CSharp-Editor`: 0 errores. El usuario confirmo posteriormente la validacion manual de M34.2.1b; M35.0 conserva estado `implemented`, pendiente de validacion manual.

### M34.2.1c: World Item Quick Actions

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- `WorldInteractionDebugTester` y `ContextualActionDebugPanel` reutilizan el menu mundial existente. Las quick actions se recalculan desde `InteractionSystem`, la misma instancia mundial, Equipment y los item-owned storages accesibles.
- El progreso usa la `ActionDefinition` `pick_up_item` y su costo de `0.5 s`; al terminar revalida referencia, `InstanceId`, `DefinitionId`, cantidad, version, alcance y destino antes de ejecutar.
- `WorldItemPickup` conserva su backend lineal existente como fuente cerrada. Preview/progreso no alteran presentacion; equip/replacement/storage finalizan tags, renderers, colliders y fisica solo despues del commit completo.
- `WorldItemEquipmentTransactionService` mueve la misma instancia directamente a Equipment, reserva placements para desplazados y restaura source, personal, equipment y slots ante cualquier fallo. No captura una secuencia global de IDs ni crea instancias nuevas.
- Equip y replacement emiten exactamente un `CommitVisualState` post-exito. Guardar en storage emite cero eventos de Equipment y usa cantidad exacta completa: no-stackables conservan `InstanceId`, mientras stacks mantienen merge canonico sin clamp ni parcial.
- Los slot sets se resuelven como unidad; Lee-Enfield produce una sola accion 2H y un solo visual por `InstanceId`. Una mochila soltada conserva instancia, contenido y ownership, y los guards vigentes rechazan nesting.
- `Recoger y consumir` se omite deliberadamente porque `InventoryItemUseService` no ofrece una transaccion mundo -> consumo con snapshot/rollback conjunto de source y estado del actor.
- Compilacion estatica de `Assembly-CSharp` y `Assembly-CSharp-Editor`: 0 errores; permanecen cuatro warnings preexistentes de `BuildingVisibilityManager`. Validado manualmente en Unity por confirmacion del usuario.

### M35.0: Universal Visual Rig & Attachment Framework

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- Se agregaron pipelines JSON para capabilities, rig profiles, visual assets, item visual profiles y attachment poses, con registro, stats y validacion de referencias, ciclos, duplicados y politicas cerradas.
- `EquipmentVisualStateSnapshot` copia solamente revision confirmada, versiones, layout e items equipados con `InstanceId`, `DefinitionId` y slots read-only.
- `ActorEquipmentComponent.CommitVisualState` publica el evento tipado una sola vez al final de commits exitosos. Preview, mutaciones intermedias, fallo, rollback y migracion legacy sin cambio no publican.
- `EntityVisualRigRuntime` cachea parts, sockets, capabilities y dependencias; `EntityEquipmentVisualSynchronizer` es reactivo, no hace polling y mantiene un visual por `InstanceId`.
- Humano y Debug Cargo usan el mismo runtime/synchronizer. El cargo resuelve la mochila por capability `mount_storage` hacia `cargo_mount` sin inventario, ownership ni gameplay.
- Mochila, palanca y Lee-Enfield tienen perfiles y poses data-driven. Rifle 2H ocupa dos slots gameplay pero produce un solo visual primario.
- `WorldItemVisualResolver` intenta profile/provider, conserva el sistema legacy y deja el fallback debug como ultima ruta.
- La herramienta Editor genera visuales derivados y el prefab cargo, configura sockets con Undo sobre instancias reemplazables y copia poses locales como JSON; no edita el FBX ni guarda escenas automaticamente.
- Los meshes `Backpack` y `Backpack.001` ya estaban en Survival PSX, por lo que no se extrajo ni duplico el ZIP. Se preparan como variante equipada y de mundo respectivamente.
- Compilacion estatica de `Assembly-CSharp` y `Assembly-CSharp-Editor`: 0 errores; validado manualmente en Unity por confirmacion del usuario.

### M35.1: Lootable Actor Real Equipment Bootstrap

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- Se agrego `initial_equipment` opcional a Actor Profiles. Cada entrada referencia un `item_id`; `slot_ids` sólo selecciona una alternativa completa cuando el servicio no puede elegir una unica opcion.
- `ActorProfileComponent` aplica layout antes del contenido, crea instancias reales en el inventario y delega el equip a una operacion atomica acotada de `EquipmentTransactionService`.
- La operacion captura personal storage, Equipment, slots y secuencia de IDs; ante cualquier fallo restaura todo el lote y reestablece bindings de item-owned storage.
- `debug_npc_capsule_01` usa mochila + palanca derecha. `debug_npc_capsule_rifle_test_01` usa mochila + Lee-Enfield 2H como variante sin conflicto de manos.
- `Debug NPC Capsule` incorpora `ActorEquipmentComponent` y `ActorItemOwnershipComponent`; el synchronizer visual escucha Equipment real y ya no usa el snapshot source debug del NPC.
- No se implementaron UI de loot, transferencias, muerte, persistencia, IA, combate ni contenido anidado inicial.

### M35.2: Lootable Entity Inventory UI V1

Estado: `in progress`; M35.2.1, M35.2.2 y M35.2.3 estan validadas. M35.2.3.1, M35.2.4 y M35.2.5 permanecen como trabajos futuros.

- `LootableActorInventoryComponent` representa inventario, Equipment y storages directos de items equipados como pertenencias reales del actor.
- `ItemStorageDebugPanel` conserva el lado del jugador y agrega en el mismo panel derecho las vistas `Equipamiento`, `Inventario` y `Contenedores`.
- La vista Equipment deduplica por `InstanceId`, muestra todos los slots ocupados y retira hacia el inventario personal mediante `EquipmentTransactionService.TransferEquippedToStorage`.
- Inventario y storages item-owned reutilizan `InventoryGridDebugView` y `GridStorageTransferService`; no existe external storage temporal ni copia del cadaver.
- La subvista de contenedor se reconcilia por `ContainerInstanceId` y vuelve a la lista si el item deja de estar equipado.
- `lootable_actor` usa las tres fuentes reales y se remueve solo cuando todas quedan vacias.
- No se agregaron JSON, escena, servicios globales, drag nuevo entre Equipment y jugador, ventanas multiples, nesting recursivo, persistencia, IA ni combate.

### Serie M35.2: fases de rediseño de ventana

- M35.2.1 Inventory Window Redesign Phase A (Equipment ocupado y acciones contextuales unificadas) esta `validated`.
- M35.2.2 Inventory Window Redesign Phase B (ventana flotante de storage item-owned) esta `validated`.
- M35.2.3 Unified Corpse Belongings Surface esta `implemented`, pendiente de validacion manual en Unity. Se eliminaron las pestañas tecnicas y se presentaron Equipment ocupado e inventario raiz en una unica superficie, conservando sus backends separados.
- La mochila equipada conserva `Revisar`, `Tomar` y `Examinar`; `Revisar` reutiliza la ventana flotante de M35.2.2. El inventario raiz del cadaver usa el resolver contextual existente para `Tomar` y `Examinar`, con revalidacion antes de mutar.
- Compilacion estatica mediante Mono/Roslyn con response files Bee de `Assembly-CSharp` y `Assembly-CSharp-Editor`: 0 errores. La validacion manual de flujos, seleccion, scroll y ventana flotante queda pendiente.
- M35.2.4 Persistent Body Review queda `planned` y M35.2.5 Multiple Floating Storage Windows queda `planned / deferred`.

- M35.2.3 Validation Correction Pass 1 compacta Equipment, elimina controles Legacy y scroll horizontal visible; las acciones cross-actor se difieren a M35.2.3.1, pendiente de auditoria arquitectonica y validacion manual.
- M35.2.3 Validation Correction Pass 2 amplía la altura mínima de Equipment, elimina la ayuda redundante y reserva el footer de ContextualActionDebugPanel; M35.2.3.1 permanece diferido y la revalidación manual sigue pendiente.
- M35.2.3 Validation Correction Pass 3 dimensiona el panel contextual por contenido, reserva el footer de resultado, alinea EQUIPADO/INVENTARIO y habilita scrollbar de Equipment solo por overflow. El drag explícito cadáver raíz ↔ mochila equipada del mismo cadáver reutiliza `GridStorageTransferService` tras revalidar binding, `InstanceId`, actor y owner raíz; Shift/doble clic siguen yendo entre cadáver y jugador. M35.2.3.1 y M35.2.5 permanecen fuera de alcance y la revalidación manual sigue pendiente.
- M35.2.3 Validation Correction Pass 4 — Final Stabilization usa cinco capturas reales de la revalidación fallida como evidencia: reemplaza el layout contextual ambiguo por header/body/footer explícitos sin scroll para una a tres acciones, restaura el body oscuro de Equipment con overflow real y corrige el routing explícito cadáver raíz ↔ mochila mediante el endpoint canónico del `InventoryComponent`. No modifica quick-transfer ni servicios transaccionales; el pulido visual adicional se detiene aquí, M35.2.3.1/M35.2.4/M35.2.5 siguen fuera de alcance. La revalidación manual final confirmó el cierre.

## Cierre M35.2.3

- Commit funcional validado: `27bf438637b621141ca553a39579349a12ff8700`.
- La validacion manual confirmo panel contextual y de resultado completos, EQUIPADO e INVENTARIO coherentes, rifle y mochila deduplicados, drag cadaver raiz hacia mochila equipada, ausencia del rechazo contradictorio, Data Load 0/0 y muerte/revision del cadaver sin excepciones relacionadas.
- Contratos confirmados: ownership unico, `InstanceId`, cantidades, placements, peso, no-nesting, rollback, quick-transfer existente y una sola ventana flotante.
- Deuda no bloqueante: el scrollbar vertical de EQUIPADO no fue probado con overflow real; el contenido actual entra completo sin clipping. La comprobacion o mejora queda diferida hasta contar con una cantidad realista de Equipment o un rework posterior de UI; no bloquea la validacion.
- Antes de iniciar otro sistema funcional se realizara una auditoria y rebaseline del roadmap de Old Scars.

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

## M36.0 — Old Scars Strategic Production Roadmap Rebaseline

### Checkpoint A — Milestone Ledger And Roadmap Authority

Estado anterior:

`PLAN REVIEWED — AUTHORIZED WITH CORRECTIONS`

Estado posterior del milestone:

`IN PROGRESS — CHECKPOINT A IMPLEMENTED; CHECKPOINT B PENDING`

Objetivo:

Reconciliar la autoridad documental, la numeracion historica, los aliases, los estados canonicos, las dependencias, los gates y la cola inmediata antes de alinear diseño y reglas de produccion en Checkpoint B.

Documentos del checkpoint:

- `Docs/Project_Roadmap.md`;
- `Docs/Current_Milestone.md`;
- `Docs/Development_Log.md`;
- `Docs/Next_Sprints.md`.

Decisiones aplicadas:

- `Project_Roadmap.md` pasa a ser la unica autoridad de IDs, estados, dependencias y gates.
- `Current_Milestone.md` se reduce a un snapshot operativo breve.
- `Development_Log.md` conserva su historia append-only; esta entrada reconcilia estados anteriores sin reescribirlos.
- `Next_Sprints.md` elimina milestones ya cerrados y conserva solo M36.1, M37.0 y M37.1 como proximos trabajos reales.
- M36.0 queda reservado como `Old Scars Strategic Production Roadmap Rebaseline`.
- Los IDs y commits historicos no se renombran.
- La colision de M28 queda explicita: el ID historico pertenece a `Add ground item drop pickup and restore container visuals` y el cleanup de containers pasa a `ID TBD`.
- M32.3 se conserva como alias historico del commit de house containers; el milestone vivo continua como M32.
- M35.2.3 usa `Unified Corpse Belongings Surface` como nombre canonico y `Inventory Window Redesign Phase C1` como alias.
- M35.2 queda `DONE — FUNCTIONAL SCOPE CLOSED AFTER M35.2.3`.
- M35.2.1, M35.2.2 y M35.2.3 conservan `VALIDATED`.
- M35.2.3.1, M35.2.4 y M35.2.5 quedan `DEFERRED — RECLASSIFIED` con triggers explicitos.
- M32 y M32.2 quedan reconciliados como `VALIDATED` desde la confirmacion manual ya registrada en este log.
- M32.4, M32.4.1 y Grid Inventory Backend v0 no se elevan sin evidencia de cierre adicional.
- La secuencia inmediata queda M36.1 → M37.0 → M37.1.
- M36.1 se limita a freeze, identidad, invariantes, test seams y baseline; no implementa save, condition, repair ni actor lifecycle.
- M37 persiste primero el slice actual y no diseña serializacion para sistemas hipoteticos.

Trabajo diferido:

- Checkpoint B de M36.0: mirror resumido del GDD Maestro v3.1, arquitectura, reglas JSON, reglas de desarrollo, template, gates y riesgos.
- La serie M35.2 no se reactiva durante M36/M37.
- El cleanup de containers no recibe un nuevo ID hasta ser priorizado.

Verificacion documental del checkpoint:

- `git diff --check` sin errores;
- diff limitado a los cuatro documentos autorizados;
- links Markdown relativos al repositorio;
- ausencia de rutas locales absolutas;
- IDs y estados canonicos reconciliados entre Roadmap, Current y la secuencia de Next;
- no se modificaron C#, JSON gameplay, escenas, prefabs, assets, Packages o ProjectSettings;
- Unity, batchmode y compilaciones no ejecutados.

M36.0 no queda `DONE`. Checkpoint B debe completar la implementacion documental y dejar el milestone como `IMPLEMENTED — PENDING DOCUMENT REVIEW`.

### Checkpoint B — Design Baseline, Gates And Production Rules

Estado anterior:

`IN PROGRESS — CHECKPOINT A IMPLEMENTED; CHECKPOINT B PENDING`

Estado posterior del milestone:

`IMPLEMENTED — PENDING DOCUMENT REVIEW`

Objetivo:

Auditar el GDD Maestro v3.1 contra decisiones recientes, milestones validados, documentos vivos, historial Git y contratos tecnicos reales; separar diseño, estado tecnico, propuestas y decisiones pendientes; y alinear reglas, gates y riesgos sin modificar gameplay.

Documentos del checkpoint:

- `AGENTS.md`;
- `Docs/Current_Milestone.md`;
- `Docs/DataDriven_JSON_Rules.md`;
- `Docs/Development_Log.md`;
- `Docs/Game_Design_Document.md`;
- `Docs/Milestone_Template.md`;
- `Docs/OldScars_Development_Rules.md`;
- `Docs/Production_Gates_and_Risks.md`;
- `Docs/Project_Roadmap.md`;
- `Docs/Technical_Architecture.md`.

Auditoria del GDD:

- El texto y la estructura de `Old_Scars_GDD_Maestro_v3.1.docx`, SHA-256 `919966D0BFCDE1FD77C6D7765EE087B4D04211FBDEAAD06B4AAFCCFEE7308AF4`, fueron leidos y auditados como fuente historica/de diseño.
- Se contrastaron sus 968 parrafos y 190 tablas con decisiones recientes, Roadmap, Current, Next, Development Log, arquitectura, reglas JSON, gates, codigo e historial cuando correspondia. Las 61 imagenes embebidas fueron inventariadas y se revisaron captions/ledger; no hubo revision visual pagina por pagina.
- Se corrigieron estados tecnicos objetivamente desactualizados, el roadmap G0–G5, los riesgos duplicados, el contrato actual de mods, el flujo de carga/validacion, la separacion `ItemInstance`/`ItemStorageEntry`/`GridInventoryLayout` y los limites de persistencia.
- Se separaron foundations/prototipos actuales del diseño final: OnGUI, health escalar, rifle debug, camera actual y condition inicial no se convierten automaticamente en UX, combate, camara o desgaste definitivos.
- Las propuestas y afirmaciones sin evidencia suficiente quedaron etiquetadas como objetivo, propuesta o decision pendiente; no se completo canon por inferencia.
- La estacion de bombeo se conserva como `CANDIDATE, NOT NARRATIVE CANON`; la casa abandonada queda como escenario tecnico de integracion.
- El GDD v3.1 no fue sobrescrito. No se genero un DOCX v3.2 porque no habia PDF independiente ni Word/LibreOffice para verificar con seguridad estilos, tablas, imagenes, pies, numeracion, indices y layout.

Autoridad documental:

- Mauro conserva autoridad creativa y decision final de producto.
- Las decisiones explicitas recientes y milestones aprobados/validados prevalecen sobre fuentes anteriores.
- `Project_Roadmap.md` define IDs, estados, dependencias, secuencia y gates.
- `Technical_Architecture.md`, despues de contraste con codigo, define contratos tecnicos vigentes.
- `Game_Design_Document.md` contiene el baseline de diseño revisado y mantenible.
- El repositorio y commits prueban estado tecnico, pero no convierten una implementacion provisional en diseño final.
- GDD Maestro v3.1 se conserva intacto como fuente historica y de diseño auditada.

Resultado:

- El nuevo baseline Markdown incluye etiquetas de direccion confirmada, design target, technical state, proposal, deferred y pending decision.
- Su apendice `Reconciliacion y correcciones del GDD Maestro v3.1` registra problema, correccion, evidencia, estado y decision pendiente.
- Arquitectura y JSON Rules documentan mods aditivos sin overrides, duplicados por tipo/registro, quantity en `ItemStorageEntry`, placement en `GridInventoryLayout`, rig parts/sockets anidados y actor bootstrap real.
- M36.1 debe congelar identidad durable, granularidad de stacks y tratamiento del `ItemInstance.Condition` get-only antes de M37, sin implementar save, condition mutable, repair ni actor lifecycle.
- M37 persiste primero jugador y slice actual; no pre-serializa actores, clima, factions, quests, reputacion o proceduralidad hipoteticos.
- Gates conservan sueño/descanso `MUST`, fatiga `SHOULD`, facciones minimas, variacion procedural secundaria y slice local acotado sin adelantar sistemas generales de quests/facciones.
- El registro vivo queda en R01–R23 e incorpora derechos de assets, revision de trabajo asistido por IA y claims comerciales ligados a evidencia.

Decisiones pendientes de Mauro:

- genero exacto, combate por turnos/AP o tiempo real, `PENSAR`, camara final y tono;
- causas del colapso, Vandor/Velgrad, industria persistente, protagonista y semilla del abuelo;
- campaña, mapa/regiones, facciones modernas, finales y canon de la estacion de bombeo;
- detalle de daño localizado, armor, penetration, medicina, muerte, incapacidad, save UX y recuperacion;
- companions, alcance de shelter, vehiculos y profundidad procedural;
- plataforma/store/modelo comercial, rating, idiomas, dispositivos, resoluciones y accesibilidad;
- direccion visual/audio, derechos de referencias y alcance de localization;
- granularidad durable de items fungibles por stack y politica futura de mods/overrides.

Trabajo diferido y limites:

- M36.0 no implementa codigo, JSON gameplay, escenas, prefabs, assets, Packages ni ProjectSettings.
- Unity, batchmode y compilaciones no aplican a este checkpoint documental.
- La revision creativa/documental de Mauro sigue pendiente; M36.0 no queda `DONE`.
- Una futura `v3.2_CANDIDATE` exige copia separada, decisiones resueltas, render PDF completo y revision visual pagina por pagina.

Verificacion documental del checkpoint:

- `git diff --cached --check` sin errores;
- diff staged limitado a los diez documentos autorizados y lista exacta confirmada;
- enlaces Markdown relativos resueltos dentro del repositorio;
- ausencia de rutas locales absolutas en los documentos modificados;
- estados de M36.0, IDs de riesgos R01-R23 y gates reconciliados entre documentos;
- SHA-256 de la fuente GDD v3.1 reconfirmado y archivo fuente no sobrescrito;
- no se modificaron C#, JSON gameplay, escenas, prefabs, assets, Packages o ProjectSettings;
- Unity, batchmode y compilaciones no ejecutados;
- no existe PDF independiente y no hubo QA visual pagina por pagina; por eso no se genero una candidata DOCX revisada.

### Documentation Review Correction Pass 1

Estado anterior:

`IMPLEMENTED — PENDING DOCUMENT REVIEW`

Estado posterior:

`IMPLEMENTED — PENDING FINAL DOCUMENT REVIEW`

Inicio, objetivo y alcance:

- Pase correctivo localizado sobre la revision humana de los Checkpoints A y B; no repite la auditoria integral del GDD ni reescribe el roadmap completo.
- Corrige clasificaciones concretas de diseño, formaliza workflow proporcional de Codex y Git, ajusta R03 y reconcilia puntualmente M29.
- Documentos modificados: `AGENTS.md`, `Docs/Current_Milestone.md`, `Docs/Development_Log.md`, `Docs/Game_Design_Document.md`, `Docs/Milestone_Template.md`, `Docs/OldScars_Development_Rules.md`, `Docs/Production_Gates_and_Risks.md` y `Docs/Project_Roadmap.md`.
- `Docs/Next_Sprints.md`, `Docs/Technical_Architecture.md` y `Docs/DataDriven_JSON_Rules.md` fueron leidos y no requieren cambios: la cola inmediata y los contratos tecnicos/JSON permanecen iguales.

Correcciones aplicadas:

- El bucle inmediato usa exposicion/riesgo y deja de presuponer una mecanica de ruido. Audio como feedback sigue siendo legitimo; no se agrega barra, stat, schema, tag ni contrato de ruido. Una percepcion auditiva futura necesita diseño y milestone explicitos.
- PSX, low-poly y legibilidad retro coherente con Old Scars quedan `CONFIRMED — RECENT DECISION` como direccion visual general. Art bible, texturas, paleta, iluminacion, shaders, jitter, filtros, camara, budgets, pipeline y consistencia exacta permanecen pendientes.
- La existencia y los nombres Vandor/Velgrad quedan confirmados. Historia, cronologia, guerra, fronteras, geografia, pueblos, lenguas, culturas, doctrinas, colores, simbolos, tecnologia, colapso y herederos/facciones modernas permanecen pendientes; los experimentos visuales anteriores no se convierten en canon.
- AGENTS conserva el resumen operativo; Development Rules contiene la politica durable detallada y Milestone Template la aplica sin duplicar toda la explicacion.
- Todo prompt declara el nucleo proporcional de milestone/alcance/validacion/Git y la configuracion recomendada de modelo, esfuerzo, velocidad y modo.
- Todo trabajo mutante autorizado que supera verificaciones termina en commit con cuerpo, inspeccion, push a `origin/dev` y arbol limpio/sincronizado, salvo las excepciones explicitamente documentadas.
- Se formalizan evidencia visual y capturas, uso justificado de subagentes y milestones revisables que entregan una unidad funcional util sin microfragmentacion.
- Milestone Template queda dividido en Nivel A obligatorio, Nivel B condicional y variante compacta para trabajo localizado.
- R03 permanece `MITIGATING` como riesgo estructural permanente. Foundation Freeze comprueba la mitigacion local de M36.1 y la consumibilidad por M37, pero ya no exige cerrarlo globalmente.

Reconciliacion puntual de M29:

- Commit de implementacion auditado: `6c4d6eca7ebf9234db24fbaa0c33f4242e6a965f`, `M29 - Add Lee-Enfield firearm prototype`, del 9 de junio de 2026.
- El cuerpo del commit describe rifle/ammo data-driven, pickup/drop, aim, raycast, consumo de municion, cooldown y feedback, pero no registra validacion manual ni confirmacion de Mauro.
- Las versiones historicas auditadas de `Current_Milestone.md` y `Development_Log.md` no contienen una entrada de cierre o prueba manual de M29; la busqueda historica de `M29` en esos documentos aparece recien en Checkpoint A.
- El commit posterior `b86a616c814b062393794b9f80adc43167fc5050` migra prefabs/visuales de items, pero tampoco documenta una validacion manual de M29.
- Resultado: M29 conserva `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED`. Para elevarlo falta evidencia explicita del escenario manual ejecutado y su resultado, o una confirmacion trazable de Mauro; la existencia del codigo no alcanza.

Alcance Git real de Checkpoint B:

- Comparacion autoritativa: `eaf2eb98ced4b3b705a68bbce540dd883a157210..9bd5283f0760ae82a845189384ca59d41ee4d624`.
- Resultado de Git: 10 archivos modificados, 1.345 adiciones y 497 eliminaciones.
- Archivos: `AGENTS.md`, `Docs/Current_Milestone.md`, `Docs/DataDriven_JSON_Rules.md`, `Docs/Development_Log.md`, `Docs/Game_Design_Document.md`, `Docs/Milestone_Template.md`, `Docs/OldScars_Development_Rules.md`, `Docs/Production_Gates_and_Risks.md`, `Docs/Project_Roadmap.md` y `Docs/Technical_Architecture.md`.
- El comparativo `--name-status` contiene solamente esos diez documentos; ningun script temporal de auditoria formo parte del repositorio o del commit.

Limites y validacion manual:

- No se modifican codigo, JSON gameplay, escenas, prefabs, assets, imagenes, GDD DOCX v3.1, Packages ni ProjectSettings.
- Unity validation: `NOT APPLICABLE`; Unity, batchmode y compilaciones no ejecutados.
- La revision documental final de Mauro queda pendiente. M36.0 no queda `DONE`, M36.1 no se inicia y M37 sigue bloqueado por Foundation Freeze.

Verificacion documental del pass:

- rama inicial `dev`; HEAD y `origin/dev` alineados en `9bd5283f0760ae82a845189384ca59d41ee4d624`; ambos checkpoints publicados y arbol inicial limpio;
- `git diff --check` sin errores;
- diff limitado a los ocho documentos autorizados y lista exacta revisada;
- enlaces Markdown relativos resueltos y ausencia de rutas locales absolutas;
- estados M29, M35.2, M35.2.3, M36.0, M36.1, M37.0 y M37.1 coherentes con el Roadmap;
- busquedas cruzadas de ruido/noise, PSX, Vandor, Velgrad, R03, push, modelo, esfuerzo, velocidad y modo revisadas;
- Checkpoint B recomputado directamente con Git: 10 archivos, 1.345 adiciones y 497 eliminaciones;
- solo documentacion autorizada; Unity validation `NOT APPLICABLE` y revision documental final de Mauro pendiente.

### M36.0 — Documentation Review Closeout

Versión:

`Documentation Review Closeout`

Estado anterior:

`IMPLEMENTED — PENDING FINAL DOCUMENT REVIEW`

Estado final:

`DONE — DOCUMENTATION REVIEWED`

`UNITY VALIDATION NOT APPLICABLE`

Aprobación y commits integrados:

- Mauro aprobó la revisión documental final de M36.0.
- Checkpoint A: `eaf2eb98ced4b3b705a68bbce540dd883a157210`.
- Checkpoint B: `9bd5283f0760ae82a845189384ca59d41ee4d624`.
- Documentation Review Correction Pass 1: `428716f7c6a22a53e134459ecbdb2d636f00c9b5`.

Resultado aprobado:

- la jerarquía de autoridad documental;
- el GDD Markdown como baseline revisado y mantenible;
- el GDD Maestro v3.1 como fuente histórica auditable e intacta;
- el roadmap estratégico M36–M55, los trece gates y el registro de riesgos R01–R23;
- la secuencia M36.1 → M37.0 → M37.1 y el cierre funcional de M35.2 después de M35.2.3;
- el freeze de ampliaciones OnGUI, la política proporcional de milestones y prompts, la configuración obligatoria de Codex y las reglas de capturas, subagentes, commits y pushes;
- PSX/low-poly como dirección visual general y la existencia y nombres de Vandor y Velgrad;
- la ausencia de una mecánica de ruido confirmada, R03 como riesgo estructural permanente y M29 como implementación histórica sin validación reconciliada.

Contratos y límites preservados:

- Mauro conserva autoridad final y el Roadmap conserva autoridad sobre IDs, estados, dependencias y gates.
- Las decisiones creativas etiquetadas como pendientes permanecen pendientes; este cierre no las convierte en canon.
- El GDD Maestro v3.1 permanece intacto.
- No se modificaron C#, JSON gameplay, escenas, prefabs, assets, Packages ni ProjectSettings.
- Unity, batchmode, compilaciones y tests de gameplay no se ejecutaron porque la validación Unity no aplica a este cierre documental.
- M36.1 es el siguiente milestone planificado, permanece `PLANNED — PENDING AUTHORIZATION`, no fue iniciado y requiere autorización independiente.
- M37 permanece bloqueado por Foundation Freeze.

### M36.1 — Checkpoint A: Durable Item Identity and Stack Contracts

Version:

`Checkpoint A — Durable Item Identity and Stack Contracts`

Estado inicial:

`PLANNED — REVISED ARCHITECTURE PLAN READY FOR IMPLEMENTATION AUTHORIZATION`

Estado posterior:

`IN PROGRESS — CHECKPOINT A IMPLEMENTED; CHECKPOINT B PENDING`

Objetivo y decisiones implementadas:

- `ItemInstance.InstanceId` permanece `string` get-only, ahora con formato durable `item_<GUID N lowercase>` y semantica opaca para consumidores.
- `CreateNew` y el constructor publico legacy representan new runtime item; `Rehydrate` reserva el ID y `Condition` exactos de un item cargado y lo devuelve detached.
- `ItemInstanceIdRegistry` conserva solamente IDs activos y se reinicia en `SubsystemRegistration` junto con `ItemOwnedStorageRegistry`.
- El constructor de `ItemOwnedStorageRuntime` deja de registrarse por side effect; registros y bindings duplicados se rechazan, mientras el mismo ID/owner es idempotente.
- `CanStackWith` centraliza compatibilidad por `DefinitionId`, `Condition`, `MaxStack` y ausencia de owned storage.
- Split conserva el ID fuente y crea un sibling; merge conserva el destino y retira una fuente totalmente consumida despues del commit.
- Los reservation scopes ambient/nested, limitados al hilo de sesion y LIFO, transfieren reservas al padre; este contexto localizado captura IDs creados por constructors/split profundos sin cambiar contratos publicos. Rollback restaura storage/layout/Equipment y libera solamente IDs nuevos.
- Los call sites terminales de `Remove` corresponden a uso/consumo. Transfer, drop, equip y unequip conservan identidad.
- Ownership se reconcilia para todas las entries afectadas sin rediseñar `InventoryMutationResult`.

Archivos de implementacion:

- `Assets/_OldScars/Scripts/Core/Items/ItemInstance.cs`;
- `Assets/_OldScars/Scripts/Core/Items/ItemInstanceIdRegistry.cs`;
- `Assets/_OldScars/Scripts/Core/Items/ItemOwnedStorageRuntime.cs`;
- `Assets/_OldScars/Scripts/Core/Items/ItemOwnedStorageRegistry.cs`;
- `Assets/_OldScars/Scripts/Core/Items/ItemStorage.cs`;
- `Assets/_OldScars/Scripts/Core/Items/InventoryComponent.cs`;
- `Assets/_OldScars/Scripts/Core/Items/Grid/GridInventoryBackend.cs`;
- `Assets/_OldScars/Scripts/Core/Items/EquipmentTransactionService.cs`;
- `Assets/_OldScars/Scripts/Core/Items/EquipmentOwnedStorageTransactionService.cs`;
- `Assets/_OldScars/Scripts/Core/Items/M36ItemIdentityDiagnostics.cs`;
- `Assets/_OldScars/Editor/M36PersistentIdentityTools.cs`.

Call sites adicionales localizados:

- `WorldItemPickup`, `WorldItemEquipmentTransactionService` y `WorldInteractionDebugTester` necesitaban transiciones explicitas de owner/rollback porque el binding estricto ya no puede sobrescribir silenciosamente un owner anterior.
- Esos cambios no agregan gameplay ni backends: sólo preservan la identidad existente durante drop, world-to-equipment y restauracion de snapshots.

Contratos preservados:

- `DefinitionId` sigue identificando definiciones JSON; `InstanceId` identifica instancias runtime.
- `ItemStorageEntry` conserva una `ItemInstance` representativa y `Quantity`; no hay IDs individuales por unidad fungible.
- Equipment, placements, visuals e item-owned storage siguen referenciando `InstanceId`.
- El storage propio usa exactamente el ID del item propietario y no recibe un segundo ID.
- JSON gameplay, `SampleScene`, prefabs, Packages y ProjectSettings permanecen intactos.
- No se implementan save/load, authored scene IDs, actor lifecycle, condition mutable, repair ni Checkpoint B.

Compilacion y diagnostico:

- Unity 6.4.6f1 compilo `Assembly-CSharp` y `Assembly-CSharp-Editor` sin errores.
- Persisten seis warnings preexistentes fuera del alcance: cuatro `CS0618` en `BuildingVisibilityManager` y dos `CS0414` en `ItemStorageDebugPanel`.
- `Old Scars > Diagnostics > M36.1 > Run Checkpoint A Item Identity` (`Ctrl+Shift+I`) ejecuto el diagnostico determinista con resultado `PASS`.
- El diagnostico comprobo CreateNew unico/formato, Rehydrate exacto/duplicado, cleanup de creacion fallida y scope nested, split, merge, rechazo por `Condition`, owned storage, registro/binding estricto y reset coordinado.
- El cleanup final confirmo cero IDs activos, storages registrados y owners registrados por el diagnostico.
- Un smoke de Play Mode, sin guardar ni modificar `SampleScene`, completo bootstrap con `[OldScars/Data] Load OK — 0 errors, 0 warnings` y sin excepciones relacionadas con M36.1.
- Al salir del smoke, `RelayService` emitio un `TaskCanceledException` de infraestructura Unity; no afecto datos, gameplay, compilacion ni el resultado del diagnostico.

Validacion, deuda y trabajo siguiente:

- Se revisaron estaticamente los flujos existentes de add/remove, pickup/drop, split, transfer, directed merge, Equipment, item-owned storage y ownership.
- La validacion manual final del slice por Mauro permanece pendiente y no se declara completada.
- No hay NUnit mientras Core permanezca en `Assembly-CSharp`; el constructor publico legacy permanece como ruta compatible de new item.
- Checkpoint B debe agregar identidad authored y evidencia de Foundation Freeze; el gate permanece abierto, R03 sigue `MITIGATING` y M37 no comenzo.
- Trabajo siguiente autorizado por separado: `M36.1 Checkpoint B — Authored Slice Identity and Foundation Evidence`.

### M36.1 — Checkpoint A Correction Pass 1

Version:

`Checkpoint A — Correction Pass 1`

Estado anterior:

`IN PROGRESS — CHECKPOINT A IMPLEMENTED; CHECKPOINT B PENDING`

Estado posterior:

`IN PROGRESS — CHECKPOINT A CORRECTED;`

`MANUAL VALIDATION PENDING;`

`CHECKPOINT B NOT STARTED`

Base y objetivo:

- Base auditada: `51aec69301ed3277f61fb9b796cd57f0678578ee`, alineada con `origin/dev` al iniciar.
- Pase correctivo localizado sobre Checkpoint A; no rehace la implementacion ni inicia Checkpoint B.
- Cierra los huecos de hydration detached, bootstrap directo de containers, cleanup de IDs candidatos durante merges totales y removal terminal de owners con storage propio no vacio.

Correcciones implementadas:

- `ItemInstance` separa attachment, validacion y registro de item-owned storage; `CreateNew` conserva su comportamiento funcional y `Rehydrate` permanece detached.
- `ItemOwnedStorageRuntime` admite resolver de definiciones y layout inicial pendiente, expone su backend interno para bootstrap y exige completar la carga inicial antes de publicar.
- `ContainerLootComponent` puebla mediante `GridInventoryBackend.Add`, verifica cantidades, bindea owners y revierte snapshot y reservas nuevas ante un fallo del lote.
- Un merge total repetido por `Add` conserva el destino sin dejar activo el ID candidato consumido.
- Un `Remove` terminal rechaza antes de mutar un item-owned storage no vacio mediante `OwnedStorageNotEmpty`; despues de vaciarlo, el retiro y cleanup existentes completan correctamente.
- El diagnostico cubre hydration detached exitosa, fallo con rollback, merge total, rechazo atomico y retiro posterior al vaciado, con cleanup final de registries.
- La auditoria de creacion directa conserva `WorldItemPickup` porque crea solamente sobre storage vacio y bindea de inmediato, y `DebugInventory` porque representa cada instancia directamente; los demas flujos mueven identidades existentes o usan el backend transaccional.

Archivos de implementacion corregidos:

- `Assets/_OldScars/Scripts/Core/Interactions/ContainerLootComponent.cs`;
- `Assets/_OldScars/Scripts/Core/Items/Grid/GridInventoryBackend.cs`;
- `Assets/_OldScars/Scripts/Core/Items/Grid/InventoryMutationResult.cs`;
- `Assets/_OldScars/Scripts/Core/Items/ItemInstance.cs`;
- `Assets/_OldScars/Scripts/Core/Items/ItemOwnedStorageRuntime.cs`;
- `Assets/_OldScars/Scripts/Core/Items/M36ItemIdentityDiagnostics.cs`.

Compilacion, diagnostico y smoke:

- Unity 6.4.6f1 recompilo Runtime y Editor sin errores; la compilacion de los seis scripts termino con `Tundra build success` y el refresh final completo una recarga de dominio limpia.
- Persisten seis warnings C# preexistentes fuera del alcance: cuatro `CS0618` en `BuildingVisibilityManager` y dos `CS0414` en `ItemStorageDebugPanel`.
- `Old Scars > Diagnostics > M36.1 > Run Checkpoint A Item Identity` produjo `M36.1 Checkpoint A Item Identity Diagnostics: PASS`.
- El diagnostico termino con cero IDs activos, storages registrados y owners registrados.
- El smoke breve entro en Play Mode, cargo `[OldScars/Data] Load OK — 0 errors, 0 warnings`, salio correctamente y no dejo errores o excepciones relacionados con M36.1 en Console.
- Se separo un `RelayService` `TaskCanceledException` externo y preexistente, junto con mensajes de paquetes/licensing de Unity; no afectaron compilacion, datos, gameplay ni diagnostico.

Limites y trabajo siguiente:

- `SampleScene`, prefabs, JSON, Packages, ProjectSettings y asmdefs permanecen intactos; no se implemento save/load ni se inicio Checkpoint B.
- `Foundation Freeze` permanece abierto, R03 sigue `MITIGATING` y M37 no comenzo.
- La validacion manual final por Mauro permanece pendiente; el siguiente trabajo es esa validacion, no Checkpoint B.

### M36.1 — Checkpoint A Correction Pass 2: Committed Ownership Transitions

Version:

`Checkpoint A — Correction Pass 2`

Estado anterior:

`IN PROGRESS — CHECKPOINT A CORRECTED;`

`MANUAL VALIDATION FAILED — OWNERSHIP TRANSITION REGRESSION;`

`CHECKPOINT B NOT STARTED`

Estado posterior:

`IN PROGRESS — CHECKPOINT A CORRECTION PASS 2 IMPLEMENTED;`

`MANUAL REVALIDATION PENDING;`

`CHECKPOINT B NOT STARTED`

Validacion manual fallida y causa raiz:

- Mauro reprodujo que recoger un item lo agregaba al inventario pero dejaba la representacion mundial; equipar desde inventario fallaba; equipar desde el suelo podia funcionar; y transfers entre inventario, containers e item-owned storage mutaban antes de arrojar una excepcion.
- La excepcion repetida fue `InvalidOperationException: Item instance 'item_...' is already bound to a different owner.` en rutas que incluian `WorldItemPickup.PickUp`, `GridStorageTransferService.NotifyCommitted`, `ItemOwnedStorageRegistry.BindItem`/`BindEntries`/`ReconcileCommittedTransfer`, `ActorEquipmentComponent.RebindActorOwnedItems` y `EquipmentTransactionService.Equip`.
- La causa raiz era temporalmente posterior al commit de storage: el backend confirmaba la mutacion y luego la reconciliacion intentaba registrar nuevamente la entry sin retirar o transferir el binding anterior. El `BindItem` estricto rechazaba correctamente el owner stale, pero el fallo impedia finalizar la presentacion mundial y podia dejar el mismo `InstanceId` representado en dos superficies.

Correccion implementada:

- `ItemOwnedStorageRegistry` separa registro inicial, transferencia comprometida y reconstruccion de rollback. `TransferBinding` exige `InstanceId`, source esperado y target; es idempotente sólo cuando ya apunta al target correcto, rechaza terceros y permite retirar explicitamente identidades consumidas.
- La reconciliacion de transfers valida source y target antes de mutar bindings, usa `SourceInstanceId`, `DestinationInstanceId`, `SourceWasRemoved`, `RemovedInstanceIds` y el contenido final, y cubre full move, split, merge y cleanup sin overwrite ciego.
- `GridStorageTransferService` mantiene el scope de identidad alrededor de la mutacion backend, reconcilia ownership antes de notificar hooks y, ante un fallo posterior al commit, restaura snapshots y owners.
- `WorldItemPickup.PickUp` usa el owner mundial como source real y el inventario como target; sólo ejecuta `FinalizeCommittedPickup` despues del commit completo. Un fallo conserva storage, tags y presentacion mundial.
- Equipment conserva el `InventoryComponent` como direct owner canonico para inventario personal↔Equipment y transfiere explicitamente ownership en las rutas world/item-owned; storage, slots, direct owner y root owner se restauran en rollback.
- `LootableActorInventoryComponent` expone el inventario canonico como direct owner para que las superficies proxy no creen ownership paralelo.
- Los diagnosticos agregan expected-source incorrecto, world→inventory, Equipment ida/vuelta, container→inventory→item-owned storage→inventory, merge/split entre owners y rollback forzado.

Archivos de implementacion corregidos:

- `Assets/_OldScars/Scripts/Core/Actors/ActorEquipmentComponent.cs`;
- `Assets/_OldScars/Scripts/Core/Actors/LootableActorInventoryComponent.cs`;
- `Assets/_OldScars/Scripts/Core/Items/EquipmentOwnedStorageTransactionService.cs`;
- `Assets/_OldScars/Scripts/Core/Items/EquipmentTransactionService.cs`;
- `Assets/_OldScars/Scripts/Core/Items/GridStorageTransferService.cs`;
- `Assets/_OldScars/Scripts/Core/Items/ItemOwnedStorageRegistry.cs`;
- `Assets/_OldScars/Scripts/Core/Items/M36ItemIdentityDiagnostics.cs`;
- `Assets/_OldScars/Scripts/Core/Items/WorldItemEquipmentTransactionService.cs`;
- `Assets/_OldScars/Scripts/Core/Items/WorldItemPickup.cs`.

Compilacion, diagnostico y smoke real:

- Unity 6.4.6f1 recompilo `Assembly-CSharp` y `Assembly-CSharp-Editor` sin errores; la compilacion final, ya sin el runner temporal, termino con `Tundra build success` y recarga de dominio.
- `Old Scars > Diagnostics > M36.1 > Run Checkpoint A Item Identity` produjo `M36.1 Checkpoint A Item Identity Diagnostics: PASS` sin estado residual.
- El smoke focalizado uso objetos reales de `SampleScene` y produjo `M36.1 Checkpoint A Real Scene Ownership Smoke: PASS`: crowbar world→inventory→Equipment→inventory→small backpack→inventory→world→inventory; Lee-Enfield world→inventory→ambas manos→inventory; y stack `ammo_303_british_01` x20 desde `Misc Debug Crate` al inventario.
- Cada etapa confirmo el mismo `InstanceId`, direct/root owner esperado, una sola representacion y validacion unica del actor; pickup deshabilito colliders/renderers y finalizo tags solamente despues del commit.
- Play Mode se cerro correctamente. Console no mostro `InvalidOperationException`, `already bound to a different owner` ni errores de M36.1.
- Persisten seis warnings C# preexistentes fuera del alcance: cuatro `CS0618` en `BuildingVisibilityManager` y dos `CS0414` en `ItemStorageDebugPanel`. Mensajes de Relay/licensing de infraestructura Unity se separaron del resultado funcional.
- `SampleScene` no fue guardada ni modificada; su SHA-256 antes y despues del smoke fue `7EBB6605CBFE564F17CA5CAC7BA46348A1CDE887CC3462086DAE1D2B602A1AFB`.

Limites y trabajo siguiente:

- No se modificaron prefabs, JSON, Packages, ProjectSettings, GDD ni asmdefs; no se implementaron save/load, identidad authored, Checkpoint B o M37.
- `Foundation Freeze` permanece abierto, R03 sigue `MITIGATING` y Checkpoint B permanece `NOT STARTED`.
- La revalidacion manual de Checkpoint A por Mauro sigue pendiente; ése es el trabajo siguiente.

### M36.1 — Checkpoint A Manual Validation Closeout

Fecha: 2026-07-22

Version:

`Checkpoint A — Manual Validation Closeout`

Estado anterior:

`IN PROGRESS — CHECKPOINT A CORRECTION PASS 2 IMPLEMENTED;`

`MANUAL REVALIDATION PENDING;`

`CHECKPOINT B NOT STARTED`

Estado posterior:

`IN PROGRESS — CHECKPOINT A VALIDATED AND CLOSED;`

`CHECKPOINT B READY FOR IMPLEMENTATION AUTHORIZATION`

Evidencia funcional validada:

- Commit funcional validado: `8ace5209bd3b48f291314e5298485cc4f630ba1f`.
- Mauro confirmo que el pickup desde el mundo funciona, agrega el item una sola vez y elimina su representacion mundial.
- Equip desde inventario y equip directo desde el mundo funcionan; equip/unequip preservan la misma identidad.
- Transfers entre inventario, mochila, containers y cuerpos funcionan sin duplicaciones y preservan ownership e identidad.
- Drop y re-pickup de una mochila no vacia preservan su contenido; rifle, crowbar y mochila conservan su `InstanceId`.
- No aparecieron `InvalidOperationException`, `already bound to a different owner` ni errores funcionales relacionados con M36.1.
- Los errores observados de Unity Relay pertenecen a `com.unity.ai.assistant`; son evidencia externa separada y no pertenecen al runtime de Old Scars.

Cierre y limites:

- Checkpoint A queda validado y cerrado. Este cierre modifica solamente documentacion y no reejecuta Unity ni altera codigo, escenas, prefabs, JSON, Packages, ProjectSettings o el GDD.
- Checkpoint B no fue iniciado. La siguiente unidad autorizable es `M36.1 Checkpoint B — Authored Slice Identity and Foundation Evidence`.
- M36.1 permanece `IN PROGRESS`; `Foundation Freeze` continua abierto hasta completar y revisar Checkpoint B, R03 sigue `MITIGATING` y M37 permanece bloqueado.

### M36.1 — Checkpoint B Authored Identity Recovery / Completion Pass 1

Fecha: 2026-08-08

Version:

`Checkpoint B — Authored Slice Identity and Foundation Evidence`

`Recovery / Completion Pass 1`

Estado anterior:

`IN PROGRESS — CHECKPOINT A VALIDATED AND CLOSED;`

`CHECKPOINT B READY FOR IMPLEMENTATION AUTHORIZATION`

Estado posterior:

`IN PROGRESS — CHECKPOINT B IMPLEMENTED;`

`AUTOMATED FOUNDATION VALIDATION PASSED;`

`MANUAL UNITY VALIDATION PENDING;`

`FOUNDATION FREEZE REVIEW BLOCKED`

Recuperacion y causa:

- Se encontro y preservo trabajo local parcial de Checkpoint B: `PersistentSceneObjectId`, `ItemInstance.CreateAuthored`, authored identity en `WorldItemPickup` y el apply/validator de `M36PersistentIdentityTools`.
- `WorldItemPickup` ya exigia `authoredItemInstanceId`, pero `SampleScene` no tenia serializados los IDs de `Debug World Crowbar` y `Debug World Lee-Enfield Rifle`.
- El log manual confirmaba que `GameDatabase` cargaba 8 items y que Core terminaba con 0 errors y 0 warnings; la data no era la causa del fallo.
- El warning posterior `Item definition '...' was not found or data is not ready` era secundario y engañoso porque el fallo primario ya era la ausencia de authored identity.

Correccion implementada:

- `Debug World Crowbar` conserva `rusted_crowbar_01` y serializa `item_4c1952809f1a4968ac86384b5a331201`.
- `Debug World Lee-Enfield Rifle` conserva `lee_enfield_rifle_01` y serializa `item_c0f66d58249e4892aa4632028975816e`.
- `CreateAuthored` reserva el ID exacto con el formato durable; no existe fallback a `CreateNew`. Los drops runtime conservan su `ItemInstance` y no reciben authored IDs nuevos.
- `WorldItemPickup` distingue database no disponible, definition inexistente e identidad authored faltante/invalida sin emitir el warning secundario falso.
- `M36PersistentIdentityTools` aplica y valida una tabla aprobada de 3 actores, 3 puertas, 8 contenedores y 2 world items; valida antes de guardar, revierte la escena en memoria ante fallo y es idempotente.
- `Debug Strange Machine`, visuales y children permanecen excluidos de `PersistentSceneObjectId`.

Validacion automatizada:

- Unity 6.4.6f1 compilo Runtime y Editor con `Tundra build success`; todas las corridas validas terminaron con codigo 0 y sin errores C#.
- La primera aplicacion produjo `M36.1 Foundation Identity Validation: PASS` y guardo `SampleScene` con `changed: true`.
- Una apertura independiente de la escena produjo nuevamente `M36.1 Foundation Identity Validation: PASS`.
- Resultado Foundation: actors 3, doors 3, containers 8, authored roots 14, authored world item IDs 2, IDs duplicados 0 e IDs invalidos 0.
- La reaplicacion produjo `changed: false`; el SHA-256 de `SampleScene` permanecio `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335` antes y despues.
- `M36.1 Checkpoint A Item Identity Diagnostics: PASS`.
- `git diff --check` termino sin errores despues de retirar cuatro campos nulos que Unity serializo incidentalmente y normalizar whitespace de los componentes nuevos.
- El diff final de `SampleScene` contiene solamente 14 componentes/referencias `PersistentSceneObjectId` y dos overrides `authoredItemInstanceId`; no modifica transforms, jerarquia, posiciones, rotaciones, escalas, colliders, renderers, materiales, camara, iluminacion, loot o UI.

Limites y gate:

- No se modificaron JSON gameplay, prefabs, Packages, ProjectSettings, GDD, save/load, condition, repair, actor lifecycle, gameplay nuevo o UI final.
- La validacion manual de Checkpoint B por Mauro permanece pendiente; no se declara Play Mode manual validado.
- M36.1 no queda `DONE`, `Foundation Freeze` no se aprueba, R03 permanece `MITIGATING` y M37 no comenzo.

### M36.1 — Diagnostic Console Observability Pass 1

Fecha: 2026-08-08

Estado anterior:

`IN PROGRESS — CHECKPOINT B IMPLEMENTED; AUTOMATED FOUNDATION VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING; FOUNDATION FREEZE REVIEW BLOCKED`

Estado posterior:

`IN PROGRESS — CHECKPOINT B IMPLEMENTED; AUTOMATED FOUNDATION VALIDATION PASSED; DIAGNOSTIC CONSOLE OBSERVABILITY PASS COMPLETE; FOUNDATION FREEZE REVIEW BLOCKED`

Cambios localizados:

- `InteractionSystem` deja silenciosas las consultas puras usadas por refresh/revalidation; `No equipped item`, el resumen de acciones y el detalle por accion quedan limitados a `LogAvailabilityDetails` en la ruta debug explicita.
- `WorldItemPickup` diferencia failures de database, definition e identidad authored con escena, GameObject, IDs, readiness, resultado y accion tomada; pickup y Equipment conservan commits breves correlacionables.
- `GridStorageTransferService` registra solamente failures de commit/rollback con definition/instance, source/target owners y storages, destination, IDs creados/retirados, `SourceWasRemoved` y resultado del rollback.
- `ItemOwnedStorageRegistry` incorpora owner real, esperado y target en mismatches/transiciones sin cambiar reglas de ownership.
- `InventoryComponent` identifica owner en creacion y failures de add; `ContainerLootComponent` identifica container, root, `PersistentSceneObjectId`, loot table, entries y cantidad total sin dump exitoso de contenido.
- Los failure paths de equip/unequip y equip/replacement desde mundo incluyen actor, instance, slots, owners y rollback; no se agrego framework global, telemetry, analytics ni cache de logging.

Validacion automatizada:

- Unity 6.4.6f1 recompilo Runtime y Editor dos veces con `Tundra build success`, exit code 0, sin `error CS` ni `warning CS` en los logs batchmode del pase.
- `M36.1 Checkpoint A Item Identity Diagnostics: PASS`.
- Los dos failures intencionales del diagnostico produjeron un bloque cada uno: owner tercero y owner faltante, ambos con `CommitAttempted: True`, `MutationCommitted: false`, `RollbackAttempted: True` y `RollbackSucceeded: True`.
- `M36.1 Foundation Identity Validation: PASS`: actors 3, doors 3, containers 8, authored roots 14, authored world item IDs 2, duplicados 0 e invalidos 0.
- Ninguna corrida emitio el spam `[InteractionSystem] No equipped item.` o `[InteractionSystem] Available actions:` desde consultas puras.

Validacion manual informada por Mauro:

- crowbar authored y Lee-Enfield authored funcionaron;
- pickup, equip directo desde el mundo, inventario y drop funcionaron;
- no se observaron errores funcionales nuevos de Old Scars.

Limites y gate:

- Se preservaron gameplay, identidad, ownership, Equipment, transacciones y `SampleScene`; no se modificaron JSON, prefabs, Packages o ProjectSettings y no se implemento save/load.
- M36.1 no queda `DONE`; `Foundation Freeze` permanece abierto hasta su revision final y M37 no comenzo.

### M36.1 — Foundation Freeze Documentary Closeout

Fecha: 2026-08-08

Milestone:

`M36.1 — Foundation Freeze & Persistent Identity Contract`

Version:

`Foundation Freeze Documentary Closeout`

Commit base:

`fed00faf9c1d8ea72520653d5326b4c21ca097e4`

Estado anterior:

`IN PROGRESS — CHECKPOINT B IMPLEMENTED; AUTOMATED FOUNDATION VALIDATION PASSED; DIAGNOSTIC CONSOLE OBSERVABILITY PASS COMPLETE; FOUNDATION FREEZE REVIEW BLOCKED`

Estado posterior:

`DONE — FOUNDATION FREEZE APPROVED`

Evidencia aceptada:

- Checkpoint A congelo `ItemInstance` durable, `CreateNew`, `Rehydrate` detached, ownership estricto, item-owned storage, split/merge, Equipment, world pickup/drop y rollback; su diagnostico y validacion manual pasaron.
- Checkpoint B congelo `PersistentSceneObjectId` para 3 actores, 3 puertas y 8 contenedores, 14 authored roots, dos authored world items y `CreateAuthored`; Foundation Identity, reapertura/reaplicacion idempotente y validacion manual de Mauro pasaron.
- Mauro valido crowbar y Lee-Enfield authored, pickup, equip directo desde mundo, inventario y drop sin errores funcionales nuevos observados.
- `Diagnostic Console Observability Pass 1` dejo failures accionables, rollback diagnosticable y consultas puras de `InteractionSystem` sin spam de refresh; Runtime/Editor compilaron y ambos diagnosticos permanecieron en `PASS`.

Decisiones congeladas para M37:

- `DefinitionId` y `InstanceId` son autoridades distintas; runtime, authored y load usan respectivamente `CreateNew`, `CreateAuthored` y `Rehydrate`.
- `Rehydrate` conserva exactamente el `InstanceId` y el `Condition` persistidos; `Condition` sigue get-only y no se vuelve mutable en M37.
- cada stack visible conserva una `ItemInstance` representativa; las unidades fungibles internas no poseen IDs individuales; split crea sibling y merge conserva destino.
- item-owned storage deriva de su item owner y ownership se reconstruye explicitamente; las referencias futuras usan identidad durable.

Deuda aceptada:

- save/load;
- condition mutable;
- repair/disassembly;
- actor lifecycle;
- gameplay nuevo;
- UI final.

Decision:

- `Foundation Freeze — APPROVED`.
- M36.1 queda `DONE — FOUNDATION FREEZE APPROVED`.
- R03 permanece `MITIGATING` y no bloquea el inicio autorizado de M37.0.
- M37.0 queda `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`, pero no fue iniciado por este cierre documental.

Alcance del cierre:

- Solo documentacion; no se modificaron C#, Assets, `SampleScene`, prefabs, JSON, Packages o ProjectSettings.
- No se ejecuto Unity ni se repitieron diagnosticos; el cierre consume la evidencia publicada en el commit base.

### M37.0 — Persistence Core V1 Functional Implementation Pass 1

Fecha: 2026-08-08

Milestone:

`M37.0 — Save Format & Persistence Core`

Version:

`Persistence Core V1 — Functional Implementation Pass 1`

Commit base:

`6eb1568b314445190d79b3c5ad20ba04fddac2b8`

Estado anterior:

`PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`

Estado posterior:

`DONE — PERSISTENCE CORE VALIDATED`

Implementacion:

- `PersistenceSerializer.CurrentFormatVersion = 1` y un envelope estable con `formatVersion`, `writtenUtc` y `payload` no-null.
- El payload usa `JToken` y mantiene el core desacoplado de gameplay, componentes Unity, identidad, ownership y estado de escena; M37.1 sera el consumidor de snapshots/DTOs.
- La configuracion Newtonsoft exclusiva de saves valida JSON, envelope y nombres duplicados, conserva cultura estable y rechaza loops/type metadata.
- El reader acepta V1, rechaza future versions mediante `FutureVersionUnsupported` y exige un paso `ISaveMigration` consecutivo explicito para versiones anteriores; V1 no registra migrations ficticias.
- `PersistenceFileStore` usa `Application.persistentDataPath/Saves` en produccion y root inyectable para diagnostics. Los slots aceptan solamente snake_case cerrado de hasta 64 caracteres.
- Cada slot queda limitado a primary, backup y temp; el documento se serializa/valida en memoria y el temp recibe flush forzado en el mismo directorio.
- First write usa same-directory rename. El overwrite validado uso `File.Replace(temp, primary, backup)`; el fallback por plataforma preserva primero primary como backup y restaura si falla la promocion.
- Read prioriza primary, recupera backup sin borrar evidencia corrupta, distingue ausencia de IO/corrupcion/versionado y no usa un backup viejo para ocultar future versions o migrations ausentes.
- Los resultados distinguen `Success`, `SaveNotFound`, `InvalidSlotId`, `IoFailure`, `MalformedJson`, `InvalidEnvelope`, `FutureVersionUnsupported`, `MigrationUnavailable`, `RecoveryFailed` y `SerializationFailure`.
- Los failure logs incluyen operacion, slot, paths, versiones, existencia, recovery, codigo/causa y accion sin volcar payload; successes permanecen compactos.

Diagnostico automatizado:

- `M37PersistenceCoreDiagnostics` trabajo exclusivamente bajo un root unico de `Path.GetTempPath()` y lo retiro en `finally`; no toco `Application.persistentDataPath` ni saves reales.
- V1 envelope serialize/deserialize preservo version y payload.
- First write/read y el round-trip exacto de un payload nested pasaron.
- Overwrite creo backup con el payload anterior y uso `Strategy: File.Replace`.
- Primary corrupto con backup valido produjo recovery exitoso desde backup sin reescribir el primary.
- Primary y backup corruptos produjeron `RecoveryFailed` sin payload parcial.
- Future format produjo `FutureVersionUnsupported`; version 0 sin migration produjo `MigrationUnavailable`.
- Slot `../escape` produjo `InvalidSlotId`; el temp stale fue limpiado y no quedaron `.tmp` ni directorios de test.
- Resultado final: `M37.0 Persistence Core Diagnostics: PASS` y batchmode retorno 0.

Compilacion y warnings:

- Unity 6.4.6f1 compilo Runtime y Editor con `Tundra build success` y retorno 0.
- Persisten solamente seis warnings preexistentes: cuatro `CS0618` en `BuildingVisibilityManager` y dos `CS0414` en `ItemStorageDebugPanel`; M37.0 no agrego warnings.
- Manual Unity validation: `NOT APPLICABLE`; no existe integracion gameplay en M37.0.

Alcance y secuencia:

- La implementacion uso tres archivos C# y 641 lineas nuevas, dentro del techo autorizado; Unity genero sus cuatro `.meta` correspondientes.
- No se modificaron gameplay, `SampleScene`, prefabs, JSON, Packages, ProjectSettings, asmdefs ni los contratos de M36.1.
- `Persistence Ready` permanece `NOT YET APPROVED` hasta M37.1.
- M37.1 queda `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`, pero no fue iniciado.

### M37.1 — Snapshot Contract & Semantic Preflight Pass 1

Fecha: 2026-08-08

Milestone:

`M37.1 — Current Slice Persistent Round-Trip`

Versión:

`Snapshot Contract & Semantic Preflight Pass 1`

Commit base:

`ca5c27184ce7dbdf864dbd5223ee84b6d667775d`

Estado anterior:

`PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`

Estado posterior:

`IN PROGRESS — SNAPSHOT CONTRACT & SEMANTIC PREFLIGHT COMPLETE; TRANSACTIONAL REHYDRATION PENDING`

Implementación no destructiva:

- `CurrentSliceSaveData` y sus DTOs explícitos representan player, tabla única de items, storages, Equipment, containers, corpses, doors y world items sin referencias a componentes/objetos Unity.
- player captura `PersistentSceneObjectId`, pose mundial, health escalar, hunger/thirst, Inventory, Equipment y owned storages. Estado estático o derivado como profiles, carry weight, visuales y health tags no se duplica.
- `ItemState` conserva exactamente `InstanceId`, `DefinitionId` y `Condition`; entries conservan `Quantity`, placement, orientación y storage owner durable.
- inventory/equipment de player y corpses, item-owned storage y containers authored comparten una representación referencial. Un storage de container inicializado se incluye explícitamente aunque esté vacío.
- sólo roots actualmente muertos entran como corpses; no se capturan transform/lifecycle/AI de NPCs vivos.
- authored world items usan markers present/absent por su item ID. El estado lazy se proyecta desde authored ID + definition sin `CreateAuthored`, `CreateNew`, `Rehydrate` ni nuevas reservas. Drops runtime guardan quantity y pose.
- puertas guardan sólo su estado lógico canónico; containers guardan únicamente los seis tags runtime mutables allowlisted. Tags estáticos/derivados y ángulos visuales quedan fuera.
- semantic preflight valida schema, player/scene IDs, item IDs y definitions, Condition, location única, quantities/max stack, placements/overlap, Equipment multi-slot, required owned storage sin nesting, containers/corpses/doors y authored/runtime world representations.
- el comparador canónico ignora orden incidental y formatting, usa tolerancia `0.0001` para poses y devuelve la primera diferencia accionable.
- `Old Scars > Persistence > M37.1 > Save Debug Slot` está habilitado sólo en Play Mode y llama al capture/preflight/write real con slot `m37_current_slice_debug`.

Diagnóstico y validación:

- `M37.1 Snapshot & Semantic Preflight Diagnostics` abre el slice real en Play Mode mediante un runner Editor persistente a domain reload y usa exclusivamente un root temporal.
- probó capture, semantic preflight, M37.0 write/read, deserialize, post-read preflight, canonical compare, duplicate InstanceId, dangling item, invalid quantity, invalid placement, Equipment inválido, owned-storage ilegal, world representation duplicada y container vacío explícito.
- resultado final: `M37.1 Snapshot & Semantic Preflight Diagnostics: PASS`, retorno 0 y cleanup completo.
- Runtime/Editor compilaron con `Tundra build success`; `M37.0 Persistence Core Diagnostics: PASS` y `M36.1 Foundation Identity Validation: PASS` permanecieron verdes.
- `SampleScene` no fue guardada y conservó SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`.
- no hubo warnings nuevos; permanecen los seis warnings preexistentes documentados en M37.0.

Alcance y secuencia:

- se tocaron/crearon tres archivos C# y dos `.meta`; no se modificaron `SampleScene`, prefabs, JSON, Packages, ProjectSettings o asmdefs.
- no se implementaron apply/load destructivo, rehydration, rollback, authoritative container restore, rehydrated world spawn ni `Load Debug Slot`.
- `Persistence Ready` permanece `NOT YET APPROVED` y M38.0 no comenzó.
- el próximo trabajo es la versión `Transactional Rehydration & Real-Scene Round-Trip Pass 2` dentro del mismo milestone canónico M37.1; no se reservaron IDs adicionales de milestone.

### M37.1 — Transactional Rehydration & Real-Scene Round-Trip Pass 2

Fecha: 2026-08-08

Milestone: `M37.1 — Current Slice Persistent Round-Trip`.

Estado anterior: `IN PROGRESS — SNAPSHOT CONTRACT & SEMANTIC PREFLIGHT COMPLETE; TRANSACTIONAL REHYDRATION PENDING`.

Estado posterior: `IMPLEMENTED — AUTOMATED ROUND-TRIP VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Implementación:

- `CurrentSliceLoadService` ejecuta read, semantic preflight, resolución de escena, snapshot pre-load, teardown selectivo, apply, recapture/compare y rollback mediante el mismo `ApplyCore`.
- no usa reset global de identity/ownership; retira solamente IDs de las superficies seleccionadas y valida que NPCs vivos fuera del slice conserven IDs, direct owners y owned storages.
- rehidrata `InstanceId`, `DefinitionId` y `Condition` exactos; reconstruye item-owned storage detached con contenido/layout exactos antes del registro, y restaura Inventory/grid, Equipment multi-slot y ownership.
- containers reciben contenido autoritativo incluso vacío y no reseedean loot. Corpses ya muertos restauran sólo health, Inventory, Equipment y owned storage; roots vivos son rechazados antes de mutar porque lifecycle general queda fuera de M37.1.
- authored world items restauran present/absent sin lazy respawn; runtime drops reutilizan la instancia ya rehidratada y conservan quantity/pose sin split ni ID nuevo.
- doors restauran el tag lógico y sincronizan visual cuando existe controlador; player health/needs y transform se restauran al final con seguridad de movimiento/controller.
- `Load Debug Slot` quedó disponible sólo en Play Mode sobre `m37_current_slice_debug`; no se agregó ventana, autosave ni UI final.
- un único fault point `UNITY_EDITOR` posterior a storage restore demuestra el failure path. `ApplyFailed` sólo se devuelve como rollback seguro si el snapshot pre-load vuelve a capturarse equivalente; de lo contrario el resultado es `RollbackFailed` con ambas causas.

Validación automatizada:

- Unity 6.4.6f1 Runtime/Editor: `Tundra build success`, retorno 0;
- `M37.0 Persistence Core Diagnostics: PASS`;
- `M36.1 Checkpoint A Item Identity Diagnostics: PASS`;
- `M36.1 Foundation Identity Validation: PASS`;
- `M37.1 Snapshot & Semantic Preflight Diagnostics: PASS`;
- `M37.1 Current Slice Persistent Round-Trip Diagnostics: PASS`;
- State A cubrió player pose/health/needs, dos pickups authored, Lee-Enfield equipada, stack transfer a backpack, container, NPC temporalmente muerto con Inventory/Equipment, door y runtime drop. State B fue distinto; load restauró A y A/C fueron equivalentes.
- el fault produjo `ApplyFailed`, `RollbackAttempted: true`, `RollbackSucceeded: true`; el runtime post-rollback fue equivalente al pre-load.
- el root temporal fue eliminado, la escena no quedó dirty y `SampleScene` conservó SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`.
- no hubo warnings nuevos; permanecen cuatro `CS0618` preexistentes en `BuildingVisibilityManager` y dos `CS0414` en `ItemStorageDebugPanel`.

Alcance y secuencia:

- se modificaron diez archivos C# y Unity generó el `.meta` del nuevo load service; quedaron fuera `SampleScene`, prefabs, JSON, Packages, ProjectSettings y asmdefs.
- Snapshot V1 no cambió. No se implementaron NPC lifecycle/transform, AI, autosave, UI final, M38 ni sistemas futuros.
- `Persistence Ready` permanece `NOT YET APPROVED`.
- trabajo siguiente: `M37.1 — Manual Unity Validation & Persistence Ready Closeout` mediante fresh-session Save/Load Debug Slot por Mauro; M38.0 no comenzó.

### ID TBD — Global Content ID Namespace Foundation

Fecha: 2026-08-09

Milestone: `ID TBD — Global Content ID Namespace Foundation`.

Commit base: `a3659df8aa469cad3db1759efdb87f6eb555e5fe`.

Motivo de identificación:

- el Roadmap no reservaba un número libre para esta unidad y prohíbe inventarlo o reutilizar uno histórico;
- M37.1 permanece abierto por validación manual y M50.0 sigue futuro con dependencias posteriores;
- por autorización explícita se interpone esta cimentación estrecha como `ID TBD`, sin llamarla M37.2/M38.x ni marcar M50.0 iniciado.

Estado posterior: `IMPLEMENTED — STATIC/DATA VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Implementación:

- `ContentId` centraliza parseo, validación y resolución de `namespace:local_id`, con razones precisas y segmentos limitados a minúsculas ASCII, dígitos y `_`; no corrige input inválido silenciosamente.
- `DefinitionContentIdNormalizer` conserva source mod/file en la frontera de carga, canonicaliza IDs y referencias de las 16 Definition families de `GameDatabase`, reserva `core` para contenido oficial y exige namespace explícito a fuentes externas.
- la compatibilidad legacy sólo cualifica referencias sin namespace cuando el contexto es Core. Agrega warnings resumidos y es temporal/removible; `right_hand` → `core:hand_right` queda como excepción histórica estrecha de Equipment.
- `GameDatabase` registra exclusivamente keys canónicas y resuelve lookups authored/schema-v1 contra Core sin registrar aliases. Por tanto una forma legacy y `core:*` no pueden convertirse en dos Definitions distintas dentro del mismo registry.
- `DataValidator` separa `RequireGlobalContentId` de `RequireLocalId`; tags siguen sin namespace, asset keys conservan su dominio secundario y las regex de `PersistentSceneObjectId`/save slot ID permanecen independientes.
- `Mods/Core` migró explícitamente Definition IDs y referencias globales en items, actions, loot, actor/world profiles, weapons/ammo, storage/equipment y visuals/poses. Tags, local group/part/socket/role IDs, icon/provider keys y effect types no fueron migrados.
- componentes runtime que retenían un lookup legacy ahora guardan el ID de la Definition resuelta; Equipment y visual rig canonicalizan layouts/slots/profile IDs antes de comparaciones o snapshots.
- saves schema v1 normalizan en memoria `ItemState.definitionId`, `EquipmentState.layoutId` y `EquippedItemState.slots` antes del semantic preflight. `schemaVersion` permanece en 1; instance IDs, persistent IDs, storage IDs compuestos y tags no se modifican.
- el diagnostic M37.1 ahora construye un payload legacy Core real y exige equivalencia canónica después de leerlo.
- `ContentIdNamespaceDiagnostics` crea bajo `Path.GetTempPath()` una fixture `Mods/Core` + `Mods/TestNamespaceMod`, demuestra coexistencia `core:test_item`/`test_namespace:test_item`, lookup alias único, rechazo legacy externo y referencia cross-namespace, y elimina el root en `finally`. No se agregó un mod distribuido a `StreamingAssets`.

Validación disponible en este entorno:

- los 21 JSON del repositorio parsearon con `jq`;
- auditoría tipada Core: `PASS` para 81 Definitions y 103 referencias globales; auditoría separada de 49 tags y 6 asset keys: `PASS`;
- auditoría sistemática de hardcodes encontró como remanentes intencionales únicamente scene/prefab compatibility, fixtures negativas/legacy y tokens locales de Action effects;
- los 24 C# modificados o nuevos parsearon sin nodos de error con grammar C#: `PASS`;
- `git diff --check`: `PASS`;
- no existen workflows bajo `.github/workflows` ni tests no-Unity aplicables.

Limitación de validación:

- no hay ejecutable Unity ni toolchain C#/.NET disponible en el entorno; no se afirma compilación Unity, ejecución de diagnostics ni compatibilidad de saves validada;
- `Manual Unity validation pending` para compilación Runtime/Editor, carga de Core, nuevo diagnostic de Content IDs, suite M36.1/M37.0/M37.1 y fresh-session Save/Load Debug Slot.

Alcance:

- no se modificaron `SampleScene`, prefabs, tags JSON, Packages, ProjectSettings ni asmdefs;
- no se implementaron manifests, provenance persistida completa, dependencies, overrides/patches, Workshop, SDK, scripting, DLL mods, hot reload, AssetBundles ni namespace de tags;
- seam posterior documentado: `manifest → provenance → dependencies → patches`;
- `Persistence Ready` permanece `NOT YET APPROVED`, M37.1 permanece abierto y M38.0 no comenzó.

### ID TBD — Inventory Interaction UX Correction

Fecha: 2026-08-10

Parent funcional: `M35.2 — Lootable Entity Inventory UI V1`.

Versión: `Context Actions & Quick Transfer Correction Pass 1`.

Estado anterior: `PLANNED — MANUAL UX ISSUES CONFIRMED`.

Estado posterior: `IMPLEMENTED — AUTOMATED VALIDATION PASSED; USER UNITY RECHECK PENDING`.

Implementación:

- `ResolveExternal` expone `Usar / Consumir` solamente para entries consumibles y también expone `Examinar`; lootable inventory reutiliza el mismo resolver sin duplicar acciones.
- `InventoryItemUseService.TryUseExternalItem` consume una unidad directamente desde un endpoint externo accesible, aplica los efectos existentes al actor jugador y ejecuta el callback de salida normal del storage; la ruta personal continúa exigiendo ownership compartido.
- doble click usa `TransferQuantityAuto` con cantidad exacta uno; Shift+click conserva `TransferStackAuto` y drag/single click no cambian.
- `ShowDetails` ahora fija un estado visible y muestra nombre, cantidad, Condition, peso existente, DefinitionId y descripción disponible en el panel debug; no crea una ventana de producción.
- se agregó `Old Scars > Diagnostics > Inventory > Run Interaction UX Correction`, con runner batch que carga `SampleScene` temporalmente y cubre resolver, consumo externo, remoción de última unidad, transferencia exacta y quick-transfer de stack.

Validación automatizada:

- Unity 6.4.6f1 compiló Runtime y Editor sin errores.
- `Inventory Interaction UX Correction Diagnostics: PASS`.
- `Global Content ID Namespace Foundation: PASS`.
- `M36.1 Foundation Identity Validation: PASS`.
- `M37.0 Persistence Core Diagnostics: PASS`.
- `M37.1 Snapshot & Semantic Preflight Diagnostics: PASS`.
- `M37.1 Current Slice Persistent Round-Trip Diagnostics: PASS`; su `ApplyFailed` con rollback exitoso es el fault-injection esperado.
- `SampleScene` no fue modificado ni quedó dirty; `git diff --check` pasó.
- no hubo errores ni warnings nuevos atribuidos a este pass. Permanecen los warnings preexistentes de `BuildingVisibilityManager` y los campos legacy de `ItemStorageDebugPanel`; los mensajes de compatibilidad legacy, licensing y casos negativos pertenecen a fixtures/entorno de los diagnósticos.

Alcance y secuencia:

- no se modificaron escenas, prefabs, JSON, Packages, ProjectSettings, Content IDs, save schema ni contratos M36/M37.
- el recheck manual de Mauro sigue pendiente para contexto externo, doble click, Shift+click, scrap, details de container/cadáver y Console.
- `M37.1` permanece `IMPLEMENTED — AUTOMATED ROUND-TRIP VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`; `Persistence Ready` permanece `NOT YET APPROVED` y M38.0 no comenzó.

### M37.1 — Manual Unity Validation & Persistence Ready Closeout

Fecha: 2026-08-10

Milestone: `M37.1 — Current Slice Persistent Round-Trip`.

Estado posterior: `DONE — CURRENT SLICE PERSISTENCE VALIDATED`.

Gate: `Persistence Ready — APPROVED` para el Current Slice.

Evidencia manual confirmada por Mauro:

- Runtime/Editor compilation: `PASS`.
- `Global Content ID Namespace Foundation Diagnostics`, M36.1, M37.0 y ambos diagnostics M37.1: `PASS`.
- En Play Mode fresh se modificó estado real, se recogió authored crowbar, se equipó Lee-Enfield, se usó backpack/item-owned storage y se modificaron containers.
- `Old Scars > Persistence > M37.1 > Save Debug Slot`: `Success`; 23 items, 11 storages, 3 world items, 8 containers, 0 corpses y 3 doors.
- Tras salir completamente de Play Mode y entrar a un bootstrap fresh-session, `Load Debug Slot` terminó `Success`; 23 items, 11 storages, 3 world items, 8 containers, 0 corpses y 3 doors; `MutationStarted: True`, `RollbackAttempted: False` y `RollbackSucceeded: False`.
- La verificación visual confirmó Inventory, Equipment, item-owned storage, containers y estado esperado; no se reportaron duplicados, pérdidas de items, ownership failures, rehydration failures ni errores de persistence.

Content ID Namespace Foundation:

- `VALIDATED — FOUNDATION COMPLETE`: `namespace:local_id`, namespace `core`, identidad canónica de `GameDatabase`, migración Core, compatibilidad legacy temporal, normalización schema-v1 y diagnostics.
- Los warnings `Legacy unqualified Global Content ID lookup ...` corresponden a referencias authored legacy conocidas de scene/prefab y siguen resueltos por compatibilidad Core-only temporal; no son un bug nuevo.
- Deuda aceptada: migrar las referencias Global Content ID authored restantes en escenas/prefabs a `core:*` canónico y retirar luego la compatibilidad temporal Core legacy cuando ningún path authored o schema-v1 soportado la requiera.
- No existen aún mod manifests, provenance completo, dependency resolution ni patch/load-order system.

Inventory Interaction UX Correction:

- `VALIDATED — AUTOMATED + MANUAL RECHECK PASSED`.
- Mauro confirmó consumo externo de una unidad, double-click de una unidad, Shift+click de stack, ausencia de Use en non-consumables, details visibles, interacciones de corpse/container y ausencia de errores runtime atribuibles.

Alcance aprobado de Persistence Ready:

- Current Slice: player pose, health/needs representados, `ItemInstance` identity, `DefinitionId`, `Condition`, stacks/quantities, grid placements, Inventory, Equipment, ownership, item-owned storage, containers, corpse surfaces actuales, doors, authored world items, runtime dropped world items y runtime mutable state de M37.1.
- Fuera: lifecycle general de actores vivos, posición durable general de NPCs, alive/dead transition entre fresh sessions, runtime NPC spawn/despawn y AI; pertenecen a M38.0.

Secuencia:

- M38.0 queda `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`; no está iniciado.
- M38.1 queda siguiente después de M38.0.
- Durante o después de M38.x se reutilizará la infraestructura en un pequeño playable exploration prototype para evaluar gameplay y presentación, sin declarar vertical slice final ni crear un milestone nuevo.

### M38.0 — Actor Identity, Lifecycle & Persistence V1 — Functional Implementation Pass 1

Fecha: 2026-08-10

Milestone: `M38.0 — Actor Runtime & Lifecycle V1`.

Estado anterior: `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`.

Estado posterior: `IMPLEMENTED — AUTOMATED ACTOR LIFECYCLE VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Implementación:

- `ActorRuntimeIdentity` separa ActorInstanceId opaco/inmutable, ActorProfileId canónico y PersistentSceneObjectId authored. Runtime usa `actor_<32 hex>` generado una vez; authored acepta override serializado y, sin modificar `SampleScene`, deriva fallback estable SHA-256 versionado desde el locator congelado.
- `ActorRuntimeRegistry` exige unicidad activa; `ActorHealthComponent` sincroniza `Alive/Dead` sin cambiar identidad y conserva el mismo actor como corpse lootable.
- `ActorProfileComponent` separa bootstrap normal de preparación de persistence restore; Inventory/Equipment/health seeds no pisan el snapshot cargado.
- `ActorSpawnService` crea una cápsula lógica visible con identity/profile/tags, health, Inventory, Equipment, ownership y lootable. New spawn bootstrappea una vez; restore usa el ID existente; representation removal libera registries/items sin significar muerte ni world streaming.
- `CurrentSliceSaveData` agrega `ActorState[]` referencial para NPCs authored/runtime y deja `PlayerState` como autoridad única del jugador. `CorpseState[]` queda sólo como compatibilidad de lectura V1 pre-M38.
- Preflight valida IDs/profile/origin/lifecycle, cobertura authored, capacidad de spawn, health y referencias storage/equipment. Apply reconcilia representaciones, reindexa escena y reutiliza rehydration/ownership/rollback M37.1; un segundo fault one-shot prueba rollback después de actor reconciliation.

Validación automatizada:

- Runtime compile: `PASS`.
- Editor compile: `PASS`.
- `M36.1 Foundation Identity Validation: PASS`.
- `Global Content ID Namespace Foundation: PASS`.
- `M37.0 Persistence Core Diagnostics: PASS`.
- `M37.1 Snapshot & Semantic Preflight Diagnostics: PASS`.
- `M37.1 Current Slice Persistent Round-Trip Diagnostics: PASS`.
- `M38.0 Actor Runtime & Lifecycle Diagnostics: PASS` en dos Play sessions: authored Alive, authored Dead contra bootstrap fresh Alive, misma identity/profile/pose/storages/Equipment, runtime spawn/restore con mismo ID, duplicate rejection, selective lifecycle y rollback post-reconciliation equivalente.
- `SampleScene` no se guardó y conservó SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`.
- No hubo warnings nuevos atribuibles a M38.0; permanecen cuatro `CS0618` en `BuildingVisibilityManager` y dos `CS0414` en `ItemStorageDebugPanel`.

Alcance y secuencia:

- siete archivos C# tocados/creados y 1336 líneas C# agregadas, dentro del presupuesto; Unity generó los dos `.meta` nuevos.
- no se modificaron `SampleScene`, prefabs, JSON, Packages, ProjectSettings ni asmdefs;
- M36.1, M37.1 y `Persistence Ready — APPROVED` se preservaron; no se implementaron M38.1, needs/world clock, AI, combat, world-scale spawn, UI final ni playable exploration prototype;
- M38.1 queda `PLANNED — BLOCKED BY M38.0 MANUAL CLOSEOUT`;
- siguiente trabajo: `M38.0 — Manual Unity Validation & Closeout` por Mauro sobre Alive, Dead/corpse y runtime actor fresh-session.

### M38.0 — Actor Runtime & Lifecycle V1 — Manual Fresh-Session Closeout

Fecha: 2026-08-12

Milestone: `M38.0 — Actor Runtime & Lifecycle V1`.

Estado anterior: `IMPLEMENTED — AUTOMATED ACTOR LIFECYCLE VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Estado posterior: `DONE — ACTOR RUNTIME & LIFECYCLE VALIDATED`.

Validation: `AUTOMATED + MANUAL FRESH-SESSION PASSED`.

Evidencia manual confirmada por Mauro:

- authored actor Alive → Save → fresh Play session → Load → Alive restaurado correctamente;
- actor muerto → corpse lootable; Save del estado Dead; fresh Play session con bootstrap Alive antes de Load; Load reemplazó correctamente ese bootstrap por el estado Dead persistido;
- corpse conserva Inventory y Equipment;
- no se observó actor vivo + corpse duplicado;
- no se observaron errores de lifecycle, ownership o persistence;
- los warnings visibles son los legacy Content ID ya conocidos y aceptados.

Automated validation: Runtime/Editor compile `PASS`; M36.1 `PASS`; Content ID Foundation `PASS`; M37.0 `PASS`; ambos M37.1 `PASS`; `M38.0 Actor Runtime & Lifecycle Diagnostics: PASS`; rollback post-reconciliation `PASS`; `SampleScene unchanged`.

Persistence Ready: `APPROVED`.

M38.1: `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`. No se inicia M38.1 en este commit.

Fuera de M38.0: AI, combat, needs/world clock, world-scale spawning, streaming y playable exploration prototype.

### M38.1 — Needs, World Clock & Recovery V1 — Functional Implementation Pass 1

Fecha: 2026-08-12

Milestone: `M38.1 — Needs, World Clock & Recovery V1`.

Versión: `World Time, Needs Progression & Rest Integration — Functional Implementation Pass 1`.

Estado anterior: `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`.

Estado posterior: `IMPLEMENTED — AUTOMATED WORLD TIME / NEEDS / RECOVERY VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Objetivo: establecer una autoridad temporal única y persistente, conectar Hunger/Thirst al tiempo de juego, integrar Rest/Sleep sin recovery médico implícito y demostrar fresh-session/rollback sin reabrir M37 ni romper M38.0.

Implementación:

- `WorldClock` es la única autoridad runtime: bootstrap Day 1 00:00, segundos de juego acumulados, Day/HH:MM derivados y escala explícita de 60 segundos de juego por segundo real. Gameplay normal usa `Time.deltaTime`; saltos controlados usan el mismo delta temporal; restore/rollback usa un setter absoluto silencioso.
- `ActorNeedsComponent` deja de drenar por `Update` propio, consume exactamente una vez cada delta del reloj y omite actores Dead. La configuración serializada existente se conserva y se expresa como 1.8 Hunger y 3.0 Thirst por hora de juego.
- `ActorRestService` implementa Rest/Sleep como avance del mismo reloj. Rechaza duración no finita/no positiva, actor disabled, health ausente, lifecycle Dead o reloj ausente sin mutar. No cura health, wounds ni revive.
- `ActorNeedsDebugPanel` expone Day/HH:MM, Rest 1h y Sleep 8h como superficie debug acotada.
- Fatigue queda `SHOULD — DEFERRED`: no existe definición ni consumidor jugable aprobado; agregar una barra aislada no cumpliría el filtro de sistemas conectados.
- `CurrentSliceSaveData` agrega `worldClock` top-level sin schema bump. Saves schema-v1 que omiten el miembro normalizan a Day 1 00:00; `worldClock: null`, no finito, negativo o fuera de cota falla preflight. Capture/compare/apply/rollback incluyen reloj y Player needs; M38 ActorState no se amplía porque los NPC actuales no tienen needs.
- El fault one-shot posterior a restaurar reloj/needs demuestra que el snapshot pre-load recupera exactamente ambos estados mediante la transacción M37.1 existente.

Validación automatizada:

- Runtime compile: `PASS`.
- Editor compile: `PASS`.
- `Global Content ID Namespace Foundation: PASS`.
- `M36.1 Foundation Identity Validation: PASS`.
- `M37.0 Persistence Core Diagnostics: PASS`.
- `M37.1 Snapshot & Semantic Preflight Diagnostics: PASS`.
- `M37.1 Current Slice Persistent Round-Trip Diagnostics: PASS` con rollback transaccional preservado.
- `M38.0 Actor Runtime & Lifecycle Diagnostics: PASS`.
- `Inventory Interaction UX Correction Diagnostics: PASS`.
- `M38.1 Needs, World Clock & Recovery Diagnostics: PASS` en dos Play sessions: bootstrap y derivación temporal, avance único/reconexión de needs, consumibles reales, Rest/Sleep, rechazos inválidos/disabled/Dead, round-trip fresh-session, legacy sin clock, preflight sin mutación y rollback post-runtime-state canónicamente equivalente.
- Recompilación final posterior a revisión: `PASS`; no hubo warnings nuevos. Permanecen cuatro `CS0618` en `BuildingVisibilityManager` y dos `CS0414` en `ItemStorageDebugPanel`.
- `SampleScene` no se guardó y conservó SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`.

Alcance y secuencia:

- ocho archivos C# tocados/creados y 884 líneas C# agregadas, dentro del presupuesto; Unity generó tres `.meta` nuevos;
- no se modificaron `SampleScene`, prefabs, JSON, Packages, ProjectSettings ni asmdefs;
- M36.1, M37.1, `Persistence Ready — APPROVED` y M38.0 se preservaron;
- fuera: health/wound recovery, Fatigue implementada, AI, combat, weather/exposure, world-scale spawn/streaming, UI final y playable exploration prototype;
- M39 queda bloqueado hasta `M38.1 — Manual Unity Validation & Closeout` y autorización separada.

### M38.1 — Manual Fresh-Session Closeout

Fecha: 2026-08-12

Milestone: `M38.1 — Needs, World Clock & Recovery V1`.

Estado anterior: `IMPLEMENTED — AUTOMATED WORLD TIME / NEEDS / RECOVERY VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Estado posterior: `DONE — WORLD TIME / NEEDS / RECOVERY VALIDATED`.

Validation: `AUTOMATED + MANUAL FRESH-SESSION PASSED`.

Evidencia manual confirmada por Mauro:

- World Clock visible y progresando normalmente; bootstrap en Day 1; Day/HH:MM avanzaron durante gameplay.
- Hunger/Thirst progresaron con el mismo tiempo del mundo; Rest 1h y Sleep 8h funcionaron y afectaron las necesidades coherentemente.
- Food/Water restauraron Hunger/Thirst mediante las rutas existentes y el consumo se mantuvo correcto.
- Save Current Slice terminó `Success`: 27 items, 15 storages, 2 world items, 8 containers, 2 actors, 0 legacy corpses, 3 doors y `ElapsedGameSeconds: 100052.668084139`.
- Mauro salió completamente de Play Mode, ejecutó fresh Play y confirmó bootstrap inicial; Load Current Slice terminó `Success` con `MutationStarted: True`, sin rollback requerido, restaurando World Clock y needs.
- Después del load, el reloj y las necesidades continuaron progresando normalmente; no se observaron errores runtime atribuibles a M38.1.
- Los warnings `Legacy unqualified Global Content ID lookup ...` y `Legacy EquipmentSlot reference ...` permanecen como deuda Core-only conocida y aceptada. `Use failed: Matching needs or health are already full.` fue comportamiento esperado con el efecto ya lleno.

Contratos cerrados: `WorldClock` como autoridad temporal única; `elapsedGameSeconds` durable; Day/HH:MM derivados; escala provisional de 60 game seconds por real second; Hunger/Thirst gobernados por game time sin autoridad temporal independiente; Rest/Sleep usando el mismo reloj sin curar ni revivir; actores Dead sin progresión; consumibles preservados; World Clock integrado al Current Slice; legacy schema-v1 sin `worldClock` normalizado a Day 1 00:00; clock inválido rechazado en semantic preflight sin mutación; World Clock y needs incluidos en rollback; fresh-session persistence validada manualmente.

Fatigue: `DEFERRED — SHOULD`.

Fuera de alcance: localized wounds, bleeding, pain, medicine, fatigue completa, combat, AI, weather, temperature, world streaming, beds/camping/shelters completos, UI final y playable exploration prototype.

M39.0 queda `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`. No se inicia M39.0 ni el playable exploration prototype en este commit.

### ID TBD — Player Controls & Health Window Foundation

Fecha: 2026-08-12

Parent: `Post-M38.1 / Pre-M39.0 UX & Controls Foundation`.

Estado: `IMPLEMENTED — AUTOMATED VALIDATION PASSED; MANUAL UNITY RECHECK PENDING`.

Objetivo: reemplazar point-and-click por movimiento WASD camera-relative estilo Project Zomboid y separar Health en una ventana debug no modal antes de M39.0, sin iniciar health localizado ni medicina.

Implementación:

- `PlayerMovementController` / `PlayerMovementInputController` reemplazan los scripts `PointClick*` conservando sus GUID. WASD proyecta forward/right de cámara sobre XZ, normaliza diagonales, conserva `CharacterController` + gravity y rota suavemente al player; click izquierdo ya no genera targets ni órdenes de movimiento.
- Un inicio de WASD válido cancela la acción temporal activa una vez; `InventoryUISessionController.BlocksWorldInput` bloquea movimiento. El pan manual de `CameraRigController` pasa a flechas y conserva RMB rotation, wheel zoom, MMB recenter y screen-edge pan.
- `ActorNeedsDebugPanel` conserva Day/HH:MM, Hunger, Thirst, Rest 1h y Sleep 8h. `ActorHealthDebugWindow` abre/cierra con H, X o Escape, expone Health real/cualitativo y Debug Damage Player, no pausa WorldClock ni bloquea WASD.
- `DebugWorldUiInputBlocker` absorbe clicks dentro de Health Window sin activar `BlocksWorldInput` global. No se modificaron JSON/Core data, Packages ni ProjectSettings.

Validación automatizada:

- Runtime/Editor compile: `PASS`.
- `Player Controls & Health Window Diagnostics: PASS` (math camera-relative, diagonal normalizada, ausencia de tipos PointClick, Health real, H-window state y bloqueo local de clicks sin bloqueo global).
- `M37.1 Current Slice Persistent Round-Trip Diagnostics: PASS`.
- `M38.0 Actor Runtime & Lifecycle Diagnostics: PASS`.
- `M38.1 Needs, World Clock & Recovery Diagnostics: PASS`.
- `Inventory Interaction UX Correction Diagnostics: PASS`.
- `SampleScene` unchanged; no se guardó ni cambió en el diff.

Manual: `PENDING — Mauro controls + Health Window recheck` en Play Mode: WASD/diagonales y yaw de cámara, inventario bloqueando movimiento, flechas de cámara, H/X/Escape, drag de ventana, clicks internos, Debug Damage y continuidad de WorldClock/needs.

Fuera de alcance: M39.0, regiones, wounds, bleeding, pain, bandages, medicine, NavMesh, rebinding, input manager, locomotion framework, UI final y cambios de persistencia/JSON.

Siguiente: `M39.0 — Localized Health & Medicine V1` continúa `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`.

### ID TBD — Player Controls & Health Window Foundation — Correction Pass 1

Fecha: 2026-08-12

Pass: `Correction Pass 1 — Follow Camera & Exclusive Windows`.

Manual findings:

- el pan libre por flechas contradecía la cámara follow esperada;
- órbita RMB, zoom wheel y recenter MMB no funcionaban correctamente en el recheck manual;
- Health e Inventory podían quedar abiertas a la vez.

Corrección:

- `CameraRigController` sigue continuamente a `recenterTarget` en `LateUpdate`; se retiraron pan por flechas y screen-edge pan. RMB conserva órbita, wheel conserva zoom limitado y MMB reaplica el pivot sin desacoplar cámara del player.
- Abrir Inventory personal o externo cierra Health; abrir Health cierra la sesión Inventory activa. Health mantiene WASD y bloqueo local de clicks; Inventory mantiene `BlocksWorldInput`.
- El diagnóstico de Player Controls & Health Window cubre follow, ausencia de pan independiente, órbita/zoom/recenter, WASD camera-relative y exclusividad Health–Inventory.

Validación automatizada: `PENDING` hasta runtime/editor compile y diagnóstico actualizado. `Inventory Interaction UX` se repetirá como regresión directa.

Manual recheck: `PENDING — Mauro follow/orbit/zoom/window exclusivity recheck`.

M39.0 continúa `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`.

### ID TBD — Player Controls & Health Window Foundation — Correction Pass 1 Closeout

Fecha: 2026-08-12

Estado final: `VALIDATED — AUTOMATED + MANUAL PASSED`.

Validación automatizada:

- Runtime compile: `PASS`.
- Editor compile: `PASS`.
- `Player Controls & Health Window Diagnostics: PASS`.
- `Inventory Interaction UX Correction Diagnostics: PASS`.
- No se repitieron M37/M38: este pass no modificó persistence, actor lifecycle ni sus seams.
- `SampleScene`, JSON, Packages y ProjectSettings permanecieron sin cambios.

Validación manual confirmada por Mauro:

- WASD camera-relative, diagonales, left-click sin movimiento y cancelación de acción: `PASS`.
- Cámara follow continua, órbita RMB, wheel zoom, MMB recenter y ausencia de free pan por flechas: `PASS`.
- Inventory bloquea WASD; Health permite WASD, bloquea sólo sus clicks y conserva Debug Damage/H/X/Escape: `PASS`.
- Health e Inventory son mutuamente excluyentes en ambas direcciones: `PASS`.
- No se observaron errores runtime atribuibles a la unidad.

Contratos cerrados: WASD camera-relative con `CharacterController`; cámara follow continua del player; RMB orbit, wheel zoom y MMB recenter sin pan libre; Health en H no modal; Inventory modal; ventanas Health/Inventory exclusivas.

Siguiente: cerrar esta unidad sin milestone adicional; `M39.0 — Localized Health & Medicine V1` permanece `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`.

### M39.0 — Localized Health & Medicine V1 — Functional Pass 1

Fecha: 2026-08-12

Versión: `Localized Body Regions, Wounds, Bleeding, Pain & Treatment — Functional Pass 1`.

Estado anterior: `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`.

Estado posterior: `IMPLEMENTED — AUTOMATED LOCALIZED HEALTH / MEDICINE VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Implementación:

- `ActorMedicalStateComponent` agrega el dominio humano V1 `Head/Torso/LeftArm/RightArm/LeftLeg/RightLeg` y heridas durables `Laceration/Puncture/Blunt` con WoundId, severity, bleeding, pain y `Unbandaged/Bandaged`.
- `WorldClock.GameTimeAdvanced` gobierna bleeding normal y de Rest/Sleep mediante el mismo delta directo. `ActorHealthComponent` conserva la reserva vital escalar, tags, muerte y lifecycle M38; Dead deja de progresar y no existe segunda autoridad/lifecycle.
- `core:bandage_01` reemplaza `restore_health` por `consumable.wound_treatment`. El servicio real consume exactamente x1, reduce bleeding de la herida elegida, conserva herida/pain y revierte estado médico si falla el retiro. El contrato es data-driven y compartido por Core/mods.
- La ventana H existente se convirtió en superficie regional cualitativa con cuerpo esquemático, hover/selección, heridas, bleeding/pain/treatment, aplicación de venda y controles DEBUG separados. H/X/Escape, WASD y exclusividad con Inventory se preservaron; input de cámara se ignora sobre panels debug.
- `PlayerState` y `ActorState` guardan DTOs médicos planos en schema/envelope V1. Un save pre-M39 que omite el bloque deriva baseline sin heridas conservando health legacy; null/objeto inválido falla preflight. El apply/rollback M37/M38 existente restaura medical state de player, authored y runtime actors, incluido corpse legacy sin etiología fabricada.

Validación automatizada:

- Runtime compile: `PASS`.
- Editor compile: `PASS`.
- `M36.1 Checkpoint A Item Identity Diagnostics: PASS`.
- `M36.1 Foundation Identity Validation: PASS`.
- `M37.0 Persistence Core Diagnostics: PASS`.
- `M37.1 Snapshot & Semantic Preflight Diagnostics: PASS`.
- `M37.1 Current Slice Persistent Round-Trip Diagnostics: PASS`.
- `M38.0 Actor Runtime & Lifecycle Diagnostics: PASS`.
- `M38.1 Needs, World Clock & Recovery Diagnostics: PASS`.
- `Player Controls & Health Window Diagnostics: PASS`.
- `Inventory Interaction UX Correction Diagnostics: PASS`.
- `M39.0 Localized Health & Medicine Diagnostics: PASS` en dos Play sessions, incluido actor runtime herido/Dead, exact round-trip, legacy omission, null/casing/enum/severity inválidos sin mutación y rollback post-medical-state equivalente.
- M38.0/M38.1 alcanzaron PASS y cleanup, aunque Unity batch quedó detenido después de `Cleanup mono`; se cerraron únicamente esas instancias post-PASS para liberar el project lock.
- `SampleScene` unchanged, SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`; Packages y ProjectSettings intactos. Permanecen seis warnings C# preexistentes y no hay warnings nuevos atribuibles a M39.0.

Manual: `PENDING — Mauro localized health / medicine fresh-session recheck`.

Fuera de alcance: tissue healing, infection, diseases, fractures, organs, surgery, projectiles, ballistics, armor, regional penalties, AI, UI final y M40.

Siguiente: `M39.0 — Manual Unity Validation & Closeout`; M40.0 queda `PLANNED — BLOCKED BY M39.0 MANUAL CLOSEOUT`.

### M39.0 — Localized Health & Medicine V1 — Manual Fresh-Session Closeout

Fecha: 2026-08-12

Estado anterior: `IMPLEMENTED — AUTOMATED LOCALIZED HEALTH / MEDICINE VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Estado posterior: `DONE — LOCALIZED HEALTH / MEDICINE VALIDATED`.

Validation: `AUTOMATED + MANUAL FRESH-SESSION PASSED`.

Evidencia manual confirmada por Mauro:

- H abrió la ventana Salud y mostró Cabeza, Torso, Brazo izq., Brazo der., Pierna izq. y Pierna der.; una región sana mostró `Se ve bien`.
- Una laceración severa debug apareció únicamente en Brazo Izq. con gravedad, pain, bleeding y treatment; el estado general cambió a `Injured` y la reserva vital disminuyó con sangrado activo.
- Rest 1h y Sleep 8h no curaron la herida.
- Aplicar vendaje consumió una unidad, mantuvo la herida durable, cambió el estado a vendada y redujo/controló el sangrado.
- Save Current Slice fue seguido por salida completa de Play Mode, fresh Play y Load Current Slice exitoso. Reaparecieron la misma región, herida durable, estado vendado, pain, bleeding y reserva vital persistida.
- El load final informó: slot `m37_current_slice_debug`; 26 items; 15 storages; 2 world items; 8 containers; 2 actors; 0 legacy corpses; 3 doors; phase `Complete`; failure code y result `Success`; `MutationStarted: True`; `RollbackAttempted: False`; `RollbackSucceeded: False`. No intentar rollback es correcto para un load exitoso.
- No se observaron errores runtime atribuibles a M39.0. Los warnings legacy de Global Content ID sin calificar y EquipmentSlot permanecen como deuda Core-only conocida y aceptada.

Contratos cerrados: seis regiones humanas V1; heridas con WoundId durable; tipos `Laceration/Puncture/Blunt`; severity acotada; bleeding conectado al mismo WorldClock; Rest/Sleep sobre el mismo delta médico; pain derivado; tratamiento localizado; venda data-driven con consumo exactamente x1; vendaje sin eliminar la herida ni ejecutar `Heal(+X)`; `ActorMedicalStateComponent` como autoridad de wounds/bleeding/pain/treatment; `ActorHealthComponent` preservado como bridge de vitalidad/lifecycle; muerte coherente por agotamiento vital; persistencia de player y actors; strict preflight; rollback transaccional; compatibilidad con saves V1 sin `medicalState`; ventana H cualitativa; Health/Inventory exclusivity y contratos WASD/camera preservados.

Deuda de tuning no bloqueante: una laceración severa puede tardar demasiado en producir pérdida vital grave. La relación entre wound severity, bleeding rate y tiempo hasta deterioro crítico/muerte deberá balancearse posteriormente. No es un fallo arquitectónico, no rompe persistence y no se modificaron valores de gameplay en este closeout.

Fuera de alcance: combat resolution, ballistics, armor, penetration, infection, fractures, surgery, organs, blood types, transfusions, antibiotics, complex analgesics, regional movement penalties, limb disability y AI.

M38.0 permanece `DONE — ACTOR RUNTIME & LIFECYCLE VALIDATED`; M38.1 permanece `DONE — WORLD TIME / NEEDS / RECOVERY VALIDATED`; `Persistence Ready` permanece `APPROVED`.

Siguiente: M40.0 — Combat Resolution & Weapons V1 queda `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`. No se inicia en este closeout documental.

### M40.0 — Combat Resolution & Weapons V1 — Functional Pass 1

Fecha: 2026-08-13.

Estado: `IMPLEMENTED — AUTOMATED COMBAT / WEAPONS VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Se graduó el prototipo M29 sin crear una segunda autoridad. `FirearmDebugController` conserva su MonoBehaviour/GUID y el input F/LMB/R, pero ahora delega en `WeaponCombatService` y `CombatResolutionService`; ya no consume ammo suelta ni llama daño escalar. Equipment, ownership, `ItemInstance`, M39 medical y M38 lifecycle conservan sus autoridades existentes.

Contratos implementados:

- resolución única de impactos a seis regiones por bounds/punto real y `ActorMedicalStateComponent.TryApplyWound`;
- melee data-driven mediante `WeaponProfile`, con range/timing e impacto Blunt para la fixture Core;
- firearm state durable por `ItemInstance` (`ammoProfileId + loadedRounds`), capacity derivada del `FirearmProfile`, reload temporizado/cancelable y consumo exacto desde ownership real;
- fire consume un round incluso en miss; dry-fire y full/incompatible/out-of-range rechazan sin mutar; el cycle bloquea el ataque hasta quedar ready;
- drop/pickup/equipment conservan el mismo `InstanceId` y estado; visuales siguen derivados de commits Equipment;
- Current Slice V1 agrega `ItemState.firearmState`, legacy omission normaliza unloaded, null/rounds/ammo/duplicados inválidos fallan preflight y el fault post-firearm restore reutiliza rollback M37–M39.

Evidencia automatizada:

- Runtime/Editor compile `PASS`; permanecen seis warnings C# preexistentes y no hay warnings nuevos atribuibles a M40.0;
- Global Content ID, M36.1, M37.0, Snapshot/Semantic Preflight M37.1, Current Slice Round-Trip M37.1, M38.0, M38.1, M39.0, Player Controls & Health Window e Inventory Interaction UX: `PASS`;
- `M40.0 Combat Resolution & Weapons Diagnostics: PASS` en dos Play sessions; el fault post-firearm-state informó `ApplyFailed`, `RollbackAttempted: True`, `RollbackSucceeded: True` y equivalencia canónica;
- `SampleScene` unchanged, SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`; Packages y ProjectSettings intactos;
- alcance: 13 C# y 1277 líneas C# agregadas, dentro del objetivo autorizado.

Fuera: armor/penetration, proyectiles físicos, critical hits, balance/spread final, animación/audio final, condition/desgaste, AI combat, dual wield, attachments y UI final.

Siguiente: `M40.0 — Manual Unity Validation & Closeout`. M40.1 queda `PLANNED — BLOCKED BY M40.0 MANUAL CLOSEOUT`. M40.0 no está `DONE`.

### M40.0 — Correction Pass 1 — Physical Shot Origin / Near-Cover Blocking

Fecha: 2026-08-13.

Estado: `IMPLEMENTED — AUTOMATED COMBAT / WEAPONS VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

El recheck manual de Mauro confirmó unloaded, reload completo/parcial, consumo exacto de ammo, fire, bolt cycle, heridas regionales `Puncture`, impactos contra mundo y continuidad Dead/corpse. También detectó que el ray origin físico adelantado por `muzzle_offset` podía comenzar detrás de una pared cercana y omitir cobertura.

Corrección acotada: el camera ray sigue determinando el target deseado; el disparo físico ahora parte del centro corporal a muzzle-height con sólo 0.02 m de epsilon hacia el target, y el primer collider real bloquea. `muzzle_offset` queda limitado al origen visual de la línea debug. No se agregaron sockets, hitboxes, visuals M35 ni proyectiles físicos.

El diagnóstico M40 agrega un caso near-cover: una pared a 0.65 m bloquea al actor detrás, consume exactamente un round y no crea wound; al retirar la misma pared, el mismo target recibe la herida localizada correcta. Manual pendiente: Mauro near-cover raycast recheck.

### M40.0 — Combat Resolution & Weapons V1 — Manual Fresh-Session Closeout

Fecha: 2026-08-13.

Estado anterior: `IMPLEMENTED — AUTOMATED COMBAT / WEAPONS VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Estado posterior: `DONE — COMBAT RESOLUTION & WEAPONS V1 VALIDATED`.

Validation: `AUTOMATED + MANUAL FRESH-SESSION PASSED`.

Evidencia manual confirmada por Mauro:

- Lee-Enfield se equipó; F activó combat mode; unloaded rechazó fire; reload completo y parcial funcionaron con capacity 10 y consumo exacto de ammo compatible; LMB consumió exactamente un loaded round; bolt cycle impidió fire inmediato; impactos reales produjeron heridas regionales `Puncture`; world geometry bloqueó disparos; muerte/corpse continuó integrada con M38/M39.
- Correction Pass 1 publicada en `ea6cbcd02c36f8403509ce209967efe29914f2a0` pasó: con el jugador pegado a una pared y un actor detrás, el impacto quedó en world geometry, el actor no recibió wound y no se atravesó cobertura cercana; con línea limpia, el actor volvió a recibir impacto normalmente.
- Crowbar se equipó y usó el mismo combat mode; el ataque melee temporizado produjo heridas `Blunt` observadas en LeftArm, Torso, RightLeg y RightArm; fuera de rango informó `Melee attack missed`; geometría interpuesta bloqueó; WASD canceló una acción en progreso; no apareció una ruta médica paralela.
- Lee-Enfield soltado y recogido conservó `Loaded 8/10` y `InstanceId: item_c0f66d58249e4892aa4632028975816e`: el estado cargado siguió al `ItemInstance`, no al owner ni a la representación world/equipment.
- Current Slice save terminó `Result: Success`; tras salir completamente de Play Mode, iniciar una nueva Play session y cargar el slot, el load terminó `Phase: Complete`, `FailureCode: Success`, `Result: Success`. El Lee-Enfield reapareció equipado en `Loaded 8/10`.
- No aparecieron errores nuevos atribuibles a M40. Los warnings legacy `core:*` conocidos permanecen como deuda aceptada y no bloquean el milestone.

Evidencia automatizada preservada: `M40.0 Combat Resolution & Weapons Diagnostics: PASS`, incluidos seis body regions, melee/range, dry-fire, reload parcial/completo/cancelado, fire/miss/cycle, bleeding-to-Dead, drop/pickup, Equipment, fresh-session round-trip, legacy V1, preflight, rollback transaccional y near-cover Correction Pass 1. El fault esperado informó `ApplyFailed`, `RollbackAttempted: True` y `RollbackSucceeded: True`.

Contratos preservados: M38/M39 continúan como autoridades de medical/death/corpse; el firearm state durable permanece en `ItemInstance`; Current Slice conserva preflight/apply/rollback transaccional M37 sin un sistema paralelo. `Persistence Ready` permanece `APPROVED`.

Fuera y deuda diferida: armor/penetration, proyectiles físicos, critical hits, balance/spread final, animación/audio final, weapon condition/wear, AI combat, dual wield, attachments y UI final. El tuning severity/bleeding de M39 y la compatibilidad legacy Core Content IDs permanecen como deuda no bloqueante.

Siguiente: M40.1 — Armor & Penetration V1 queda `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`. No se diseña ni implementa en este closeout documental.

### M40.1 — Armor & Penetration V1 — Functional Pass 1

Fecha: 2026-08-13.

Estado: `IMPLEMENTED — AUTOMATED ARMOR / PENETRATION VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Se implementó una foundation data-driven de cobertura regional y penetración con una escala interna común, sin fingir unidades físicas. `PenetrationResolutionService` no conoce actores ni M39: procesa capas de cualquier adapter mediante `incomingPower <= resistance → Stopped`, `incomingPower > resistance → Penetrated` y `residual = max(0, incomingPower - resistance)`. El dispatch terminal reconoce hoy únicamente el adapter médico humano; futuros receptores machine/vehicle podrán agregarse sin reescribir el núcleo.

Contratos implementados:

- `ArmorProfileDefinition` declara regiones cubiertas, `penetration_profile_id`, impact resistance, blunt transfer/threshold y `layer_priority`; `PenetrationProfileDefinition` declara resistencia compartida para wearable armor y world surfaces;
- sólo Equipment real protege; inventory, backpack, container y world no lo hacen; las seis `BodyRegion` y múltiples capas usan orden determinista;
- firearm stopped rechaza `Puncture` y produce cero wounds o una única `Blunt`; penetrated produce una sola `Puncture` residual; melee reutiliza impact resistance sólo contra el receptor directo;
- M39 conserva autoridad exclusiva sobre wounds, bleeding, pain y reserva vital; M38 conserva Dead/corpse; armor nunca los muta por una ruta paralela;
- world geometry normal permanece opaca; sólo una surface con `penetration_profile_id` puede gastar budget y continuar, con epsilon `0.001`, deduplicación y máximo cuatro superficies;
- toda munición de proyectil exige `penetration_power > 0`; no existen `IsAP`, `CanPenetrate` ni branches FMJ/AP/HP. Futuras familias variarán por datos y usarán el mismo resolver;
- `EffectiveResistance(ItemInstance, baseResistance)` queda como seam M43; M40.1 no lee, degrada ni muta `Condition`;
- no se agregó estado persistente: Equipment e identidad `ItemInstance` ya son durables, profiles son Definitions, no existen `armorState`/`penetrationState` y schema/envelope V1 permanecen en 1;
- Core y mods atraviesan los mismos loaders, normalización, database, validator y referencias canónicas; el fixture mínimo es una única definición de armor de torso que puede ocupar slots existentes outer/middle.

El fixture final permite el recheck manual sin otra familia de ammo: `.303` usa `penetration_power: 0.65`; cada capa usa `resistance: 0.325`. Una capa produce `Penetrated` con residual `0.325`; dos instancias equipadas consumen exactamente el budget y producen `Stopped`; ambas sólo en inventory producen `Unarmored`. El único menú de preparación/ciclo registra target, IDs y modo sin modificar `SampleScene`.

Evidencia automatizada:

- Runtime/Editor compile: `PASS` (`Tundra build success`, return code 0);
- Global Content ID Namespace Foundation, M36.1 Foundation Identity, M38.0 Actor Runtime & Lifecycle y M39.0 Localized Health & Medicine: `PASS`;
- `M40.0 Combat Resolution & Weapons Diagnostics: PASS` después de los últimos cambios;
- `M40.1 Armor & Penetration Diagnostics: PASS` en dos Play sessions, cubriendo A–W, exact threshold, Equipment authority, seis regiones, stopped con/sin trauma, residual penetration, melee, death/corpse, round-trip exacto, legacy/no-invention, invalid data/no mutation y regresiones de input/combate;
- world coverage pasó thin cover + actor, resistant cover, dos capas sucesivas, budget agotado, límite de cuatro, geometría opaca, clear M40 y el caso combinado world cover → wearable armor → actor;
- la suite M37.1 completa no se repitió porque ningún archivo productivo de persistence/capture cambió; M40.1 probó el round-trip exacto relevante en dos sesiones y schema/envelope V1 intactos;
- alcance final: 13 archivos C# y 1800 líneas C# agregadas, exactamente dentro del techo duro; JSON mínimo, Packages y ProjectSettings intactos;
- `SampleScene` unchanged, SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`;
- no hubo warnings nuevos atribuibles a M40.1; permanecen los seis warnings C# preexistentes y la deuda legacy Core Content ID documentada.

Fuera: proyectiles físicos, ricochet, ángulo/thickness real, spall, fragmentation, destructible meshes, armor degradation/repair, ammo-family expansion, vehicles, machines, AI, balance final y UI final.

Manual: `PENDING — Mauro armor/penetration fresh-session recheck`.

Gate: `Combat Ready — PENDING MANUAL M40.1 CLOSEOUT`.

Siguiente: `M40.1 — Manual Unity Validation & Closeout`. La implementación queda congelada; no iniciar M41.0.

### M40.1 — Armor & Penetration V1 — Manual Fresh-Session Closeout

Fecha: 2026-08-13.

Estado anterior: `IMPLEMENTED — AUTOMATED ARMOR / PENETRATION VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Estado final: `DONE — ARMOR / PENETRATION V1 VALIDATED`.

Validation: `AUTOMATED + MANUAL FRESH-SESSION PASSED`.

Mauro confirmó el cierre manual:

- `StoppedTwoLayers`: `.303` power `0.65` frente a resistencia total `0.65` produjo `Stopped`, exactamente una `Blunt` y ninguna `Puncture`; Head y arms descubiertos conservaron el resultado unarmored/Puncture;
- `PenetratedOneLayer`: resistencia `0.325` dejó residual `0.325` y produjo exactamente una `Puncture`;
- `UnarmoredInventoryOnly`: las dos armor `ItemInstance` en inventory, fuera de Equipment, no protegieron;
- crowbar directo sobre torso protegido produjo `Blunt`; Equipment intervino, inventory-only no y melee no atravesó paredes;
- geometría opaca bloqueó antes de armor/actor; al restaurar una línea limpia el impacto volvió a alcanzar el target;
- Current Slice guardó al actor `actor_677cb4714310457d9e35140b04a199f0`; tras salir completamente de Play, fresh Play lo reconstruyó mediante `Initialization: PersistenceRestore` y el load informó `FailureCode: Success`, `Result: Success`;
- `item_65e023d5f6a1478c8384a2f39be86630` y `item_71d498f132b9435c9e85caf1be6a5de4` conservaron identidad y Equipment; otro impacto de torso `.65 <= .65` dio `Stopped`, una `Blunt` y ninguna torso `Puncture`.

La evidencia automatizada publicada permanece vigente: Runtime/Editor compile, regresión M40.0 y `M40.1 Armor & Penetration Diagnostics` dieron `PASS`; `SampleScene`, Packages, ProjectSettings y persistence schema/envelope V1 permanecieron intactos. No se repitió Unity durante este closeout documental.

Los warnings legacy Global Content ID continúan como deuda conocida no bloqueante, no como fallos M40.1. Permanecen diferidos: familias FMJ/AP/HP/tracer/anti-material como variaciones exclusivamente data-driven sobre el mismo resolver; receivers machine/vehicle; integración M43 de `Condition`; proyectiles físicos, ricochet, ángulo, espesor real, spall, fragmentación, vehículos y máquinas.

Gate: `Combat Ready — APPROVED`.

Siguiente: M41.0 — Navigation & Perception Foundation permanece `PLANNED`, disponible para autorización y no iniciado.

### M41.0 — Navigation & Perception Foundation — Functional Pass 1

Fecha: 2026-08-13.

Base: `6c376a88234edc0d4fb2a046d8ae2a17a115efad` en `dev`, sincronizada `0/0` y con working tree limpio al comenzar.

Estado anterior: `PLANNED`.

Estado actual: `IMPLEMENTED — AUTOMATED NAVIGATION / PERCEPTION VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Implementación:

- `ActorNavigationController` agrega órdenes NPC data-driven mediante `NavMeshAgent`, estados explícitos `Idle/Moving/Reached/Failed`, path completo prevalidado, llegada sin loop, stop, fallos estables y autoridad lifecycle M38;
- destinos fuera del NavMesh se rechazan con un epsilon exclusivamente numérico: no existe teleport, snap funcional, retry automático ni spam por frame;
- `ActorVisualPerceptionService` permanece separado de Navigation y Combat y devuelve un resultado explicable después de identidad, lifecycle, range, FOV horizontal y LOS físico, incluido blocker y child collider del target;
- `ActorProfileDefinition` incorpora bloques opcionales `navigation` y `visual_perception`; `DataValidator` exige valores finitos/positivos y FOV dentro de `(0, 360]`;
- `ActorProfileComponent` configura capacidades declaradas durante bootstrap y restore, mientras el player conserva sus controladores propios y no recibe navegación NPC;
- `CurrentSliceLoadService` aplica la pose durable mediante el seam de Navigation, limpia órdenes efímeras y deja el actor `Idle`; no cambian snapshot, schema ni envelope V1;
- `SampleScene` queda preparada mediante Editor API con una fixture aislada, `NavMeshSurface`, asset bakeado, barrera `Not Walkable` y markers reproducibles;
- un único `M41.0 Navigation & Perception Diagnostics` cubre spawn/registry, datos aplicados, desplazamiento/Reached, destinos inválidos y cercanos fuera del NavMesh, estabilidad sin retry, muerte/inmovilidad, player authority, range/FOV/LOS, blocker, self, child collider y restore;
- el mismo tooling ofrece un setup Play temporal y toggle de barrera para la validación manual, sin UI final ni control gráfico automatizado.

Validación automatizada:

- Runtime/Editor compile: `PASS` (`Tundra build success`, return code 0);
- Data validation: `PASS` dentro del diagnóstico (`GameDataManager.Report.HasErrors == false`);
- `M41.0 Navigation & Perception Diagnostics: PASS`;
- regresión directa `M38.0 Actor Runtime & Lifecycle Diagnostics: PASS`;
- no hubo warnings nuevos atribuibles a M41.0; permanecen los seis warnings C# preexistentes.

Persistencia: `NONE`. Orden, path, estado operativo y resultado de percepción son efímeros; sólo se reutiliza la pose/lifecycle durable ya existente.

Manual: `PENDING — Mauro M41.0 navigation/perception fresh-session recheck`.

Fuera: hostility, factions, alert states, investigation/search, chase, flee, combat decisions, weapon selection, shooting/melee AI, cover, flanking, squads, patrol schedules, Behavior Trees, GOAP, Utility AI, strategic/vehicle navigation y animal behavior.

Siguiente: `M41.0 — Manual Unity Validation & Closeout`. M41.1 permanece `PLANNED`, no iniciado y no autorizado por este pass.

### M41.0 — Navigation & Perception Foundation — Manual Unity Closeout

Fecha: 2026-08-13.

Estado anterior: `IMPLEMENTED — AUTOMATED NAVIGATION / PERCEPTION VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Estado final: `DONE — NAVIGATION / PERCEPTION FOUNDATION VALIDATED`.

Validation: `AUTOMATED + MANUAL UNITY PASSED`.

Mauro confirmó manualmente:

- el runtime actor recibió destination y comenzó `Moving`;
- el navigator se desplazó físicamente, rodeó la barrera sin atravesar geometría bloqueante y terminó `Reached`;
- con observer/target deterministas y barrera activa, Perception informó `Occluded` y blocker exacto `Navigation Perception Barrier`;
- al retirar la barrera, Perception informó `Perceived: True`, `Reason: Perceived` y `Blocker: <NONE>`.

El primer intento manual detectó que el helper reutilizaba transforms vivos en vez de restablecer la geometría canónica usada por el diagnóstico. El commit `b4345890d9185d439d408cdece211424c88b8b21` corrigió exclusivamente el tooling Editor: `Prepare Manual Validation`, `Toggle Manual Perception Blocker` y el diagnóstico automático comparten restauración segura de poses, sincronización física y assertions exactos para `Occluded`/`Perceived`. Runtime Navigation/Perception, datos, persistencia y contratos M38–M40 permanecieron intactos.

La evidencia automatizada publicada permanece vigente: Runtime/Editor compile, Data validation, `M41.0 Navigation & Perception Diagnostics` y regresión directa M38.0 dieron `PASS`; no se repitió Unity durante este closeout documental.

Persistencia: `NONE`. Orden, path, estado operativo y resultados de percepción siguen siendo efímeros; M41.0 no agrega estado durable ni cambia schema/envelope V1.

El cambio local de `SampleScene.unity` generado durante la prueba manual se auditó antes de restaurarlo: contenía únicamente el desplazamiento accidental de `M41_NavigationFixture` y normalización de campos vacíos producida por Unity, sin contenido funcional nuevo ni cambios intencionales. La escena se restauró desde `b4345890` y el checkout quedó limpio antes de editar documentación.

Gate: `AI Ready — PENDING M41.1`. M41.0 no aprueba ese gate por sí solo.

Siguiente: M41.1 — Human Encounter AI V1 permanece `PLANNED`, disponible para autorización y no iniciado.

### M41.1 — Human Encounter AI V1 — Final Closeout

Fecha: 2026-08-20.

Estado anterior: `IMPLEMENTED — AUTOMATED VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Estado final: `DONE — HUMAN ENCOUNTER AI V1 VALIDATED`.

Validation: `AUTOMATED + MANUAL UNITY PASSED`.

Implementación cerrada:

- `ActorProfileDefinition.encounter_ai` declara response y tuning finito/validado; `ActorProfileComponent` sólo añade/configura `HumanEncounterAIController` para actores NPC que ya tienen Navigation y Perception, y rechaza una autoridad de movimiento del player;
- `HumanEncounterAIController` decide solamente target explícito, state/timers y respuesta `avoid`/`flee`/`fight`; Navigation conserva path, Perception conserva LOS y `WeaponCombatService` conserva ammo, reload, impacto y consecuencias;
- player y NPC reutilizan `PhysicalShotPathResolver`; M41.1 no creó una autoridad paralela de Combat, Navigation o Perception;
- `LostContact` congela la última posición de una percepción positiva, cancela acción activa y nunca copia el transform oculto del target; timeout limpia el encounter y reacquisition exige asignación explícita;
- lifecycle `Dead` deja IA inactiva, detiene navegación y cancela reload/ataque; encounter state, target, timers y path son efímeros y schema/envelope de save permanecen sin cambios;
- el diagnóstico automático cubre avoid/flee, navegación inválida estable, fight con reload/disparo/armor, LostContact/no omniscience, reacquisition y lifecycle;
- la limpieza Editor-only preserva diagnostics históricos para automatización y deja una fixture manual explícita `Old Scars/Diagnostics/AI/M41.1` con Avoid, Flee, Fight, LOS y Toggle LOS Barrier; el panel `M41.1 Manual Status` muestra escenario, estado, percepción/LOS y weapon/ammo/reload para Fight.

Validación automatizada:

- Runtime/Editor compile: `PASS`;
- `M41.1 Human Encounter AI Diagnostics: PASS`;
- `git diff --check`: `PASS`.

Mauro confirmó manualmente:

- Avoid: `Idle → Alerted → Avoiding`, navegación física y `NAVIGATION_MOVING`;
- Flee: `Idle → Alerted → Fleeing`, navegación física y `NAVIGATION_MOVING`;
- Fight: `Idle → Alerted → Fighting`, Perceived/LOS correcto, Lee-Enfield equipada y reload/disparo real. El estado final `0 loaded / 0 reserve` fue el agotamiento deliberado de toda la munición durante la prueba;
- LOS: target inicialmente percibido; barrera activa produjo `Perceived = False`, `Occluded` y `LostContact`; tras timeout volvió a `Idle`; al retirar barrera y reasignar threat volvió a `Alerted`, sin omnisciencia.

Gate: `AI Ready — APPROVED`.

Fuera: hostility universal, facciones, investigación, cover/flanking, behavior trees, población/streaming, AI animal, UI final, cambios de persistencia y cualquier milestone posterior.

Siguiente: hardening de workflow de desarrollo; no iniciar M42.0 ni otro milestone jugable por este cierre.

### Workflow Hardening — Closeout

Fecha: 2026-08-20.

Se cerró el pass de workflow fuera de milestone: `AGENTS.md`, Development Rules, Milestone Template y cuatro repo-local skills quedaron compactos, con política explícita de System Harmony, uso proporcional de modelos/subagentes y logs accionables. El worktree aislado confirmó una única consulta Unity MCP real y read-only: `editor_status` devolvió Editor `ready`, sin compilación, para Unity `6000.4.6f1` en el proyecto del worktree.

`com.unity.pipeline` queda solamente como requisito técnico del bridge MCP configurado; Unity CLI global es opcional y no se convierte en requisito de Old Scars. No hubo cambios de gameplay, datos, persistencia, escenas ni milestones. M42.0 y Open World Rebaseline permanecen no iniciados.

### Open World Rebaseline — Phase 2A Architecture Freeze Draft

Fecha: 2026-08-23.

Estado de dirección: `APPROVED DESIGN DIRECTION — NOT IMPLEMENTED`.

Estado documental: `DRAFT — PENDING MAURO DIFF REVIEW; NO COMMIT / NO PUSH`.

Después de la auditoría arquitectónica Phase 1 y su revisión, se preparó una propuesta de baseline canónico para el futuro mundo abierto. La dirección define un único mundo lógico persistente, sectores grandes interconectados, macro planning anterior a la realización local, un solo sector con simulación pesada autoritativa, coordenadas lógicas separadas de Unity, worldgen determinista, historia causal acotada, blueprints validados antes de materializar y mutaciones gobernadas por persistencia.

Se creó `Open_World_Architecture.md` como autoridad de diseño futuro y se reconciliaron Roadmap, Current Milestone, Next Sprints, GDD, Technical Architecture y Production Gates/Risks. `Technical_Architecture.md` continúa describiendo sólo realidad implementada; `DataDriven_JSON_Rules.md` permanece unchanged porque no existe manifest/world schema implementado.

Correcciones congeladas en el draft:

- un sector inactivo puede permanecer localmente no resuelto, pero nunca usar silenciosamente un generador/contenido incompatible cuando se visite;
- world history registra causalidad, pero el world state presente es autoridad y no se reconstruye por event replay;
- runtime transition y autosave/checkpoint policy son contratos separados;
- staging inerte de destino no crea una segunda simulación autoritativa;
- world persistence debe garantizar unicidad durable de actores/items entre estado activo e inactivo sin otra autoridad gameplay;
- logical world persistence no congela un único JSON monolítico ni diseña todavía transacciones multi-file;
- provenance y generation compatibility son problemas relacionados pero distintos;
- mundo finito muy grande es recomendación inicial; finite vs future-expandable permanece pendiente de aprobación final de Mauro.

El camino crítico conceptual pasa a foundations `ID TBD`: content source identity/provenance; world identity/topology/determinism; macro geography/cross-sector networks; bounded history; world persistence; sector blueprint/authored composition; navigation/performance gate; materialization/transition; Connected First Playable; playtest/rebaseline. M42.0–M47.1 conservan sus IDs y sistemas, pero requieren sequence/scope rebaseline y no quedan autorizados.

No se modificaron C#, JSON, scenes, prefabs, packages ni gameplay. No se ejecutó Unity porque la tarea es exclusivamente documental. El draft permanece en un worktree/branch aislado y debe detenerse antes de commit, push o integración hasta revisión explícita de Mauro.

### ID TBD — Minimum Content Source Identity & Provenance Foundation

Fecha: 2026-08-23.

Estado: `VALIDATED — FOUNDATION COMPLETE`.

Se implementó identidad/provenance mínima de content sources sobre el pipeline existente. Todo root inmediato de `StreamingAssets/Mods` requiere `manifest.json` estricto con `source_id`, `namespace` y `version`; `old_scars_core` posee el namespace reservado `core`. `GameDataLoader` descubre y valida todos los manifests, duplicados y reservas antes de registrar Definitions, carga Core primero y ordena externos por `source_id` estable. Folder name y paths absolutos permanecen sólo operacionales.

`ContentLoadContext` conserva source identity/version/namespace/root IO y recognized inputs. `DefinitionContentIdNormalizer` exige que cada declaración pertenezca al namespace owner sin aplicar esa regla a referencias; cross-namespace references explícitas continúan bajo resolución/semántica de `DataValidator`. La compatibilidad legacy sin namespace permanece exclusiva de Core.

`LoadedContentSource` y `LoadedContentSet` exponen metadata inmutable y SHA-256 canónico. El hash usa campos manifest relevantes, paths relativos normalizados y bytes exactos consumidos de los JSON que recorre el propio loader; no usa folder/root absoluto ni archivos no reconocidos. `GameDataManager` publica el set sólo después de loader + validator exitosos. Provenance registra evidencia de inputs y no se interpreta como generation/save compatibility.

Validación autónoma en Unity `6000.4.6f1`, Editor batchmode aislado:

- Runtime compile: `PASS`;
- Editor compile: `PASS`;
- `Minimum Content Source Identity & Provenance Foundation`: `PASS` para Core real y escenarios A–L de manifests, rename, duplicates, Core reservation, ownership, cross-namespace, bytes, order, archivos ignorados y failures;
- real Core + `DataValidator`: `PASS`;
- `Global Content ID Namespace Foundation`: `PASS`;
- temp fixture cleanup: `PASS`;
- no Play Mode/manual Mauro gate requerido: el cambio es loader/data contract con diagnostics deterministas y sin interacción visual.

No se implementaron generation compatibility, WorldId/WorldSeed, topology, sectors, world persistence, saves, menus, dependencies, patches, overrides, load order, Workshop ni scripting/DLL mods. `ID TBD — World Identity, Topology & Determinism Foundation` queda como candidato `PLANNED — NOT AUTHORIZED`.

### ID TBD — World Identity, Topology & Determinism Foundation

Fecha: 2026-08-23.

Estado: `VALIDATED — FOUNDATION COMPLETE`.

Se implementó el mínimo mundo lógico puro bajo `OldScars.Core.World`. `WorldId` usa `world_<32 hex lowercase>` y permanece independiente de `WorldSeed`; `WorldSeed` conserva signed 64-bit exacto. `GeneratorVersion` y `WorldGenerationContext` forman el contexto mínimo actual sin convertir `LoadedContentSet` provenance en generation input o compatibility policy.

`WorldDeterminism` deriva SHA-256 mediante encoding length-prefixed estable de `WorldSeed + GeneratorVersion + ScopeStableKey + PassKey`. No usa `WorldId`, `UnityEngine.Random`, `GetHashCode`, filesystem ni execution order. No se agregó PRNG porque todavía no existe un pass que consuma sampling.

`SectorId` usa `sector_<32 hex lowercase>` derivable de un domain key. `WorldTopology` ordena nodos/conexiones canónicamente, admite múltiples connection keys para el mismo par, normaliza endpoints no dirigidos y rechaza duplicates, targets ausentes, self-loops y topologías desconectadas. `CanonicalDescription`/`CanonicalHash` son evidencia lógica SHA-256, no save compatibility.

Validación autónoma en Unity `6000.4.6f1`, Editor batchmode aislado:

- Runtime compile: `PASS`;
- Editor compile: `PASS`;
- `World Identity / Topology / Determinism Foundation`: `PASS` en dos procesos frescos y una pasada final posterior a review;
- golden domain key `9e328386ee1245517f38557b3de565fb5afe7944fbd3e5dbca57659bd9116c0c`: estable;
- golden topology hash `faf467c0c3f29921a67a39e7e938e9d1d6bd319b9e7a085edebfbb938d507cd9`: estable;
- `Global Content ID Namespace Foundation`, `Minimum Content Source Identity & Provenance Foundation`, `M36.1 Foundation Identity Validation` y `M37.0 Persistence Core Diagnostics`: `PASS`;
- no se requirió gate manual/visual; no existen GameObjects, scenes ni comportamiento jugable en este alcance.

No se implementaron world session/manager, save payload, `current_slice_v1` changes, Main Menu, New Game/Load Game, logical pose type, finite/expandable decision, geography, roads/rivers, history, terrain, materialización, active-sector lifecycle, NavMesh ni procedural buildings. `ID TBD — World Session + Persistence V1 / New Game Save-Load Path` queda candidato `PLANNED — NOT AUTHORIZED`.

### ID TBD — World Session + Persistence V1 / New Game Save-Load Application Shell

Fecha: 2026-08-23.

Estado: `VALIDATED — APPLICATION SHELL COMPLETE`.

Se implementó `WorldSession` como autoridad lógica inmutable publicada por un único `WorldSessionService` con lifecycle Create/Load/Save/Close. La session conserva `WorldId`, display name, seed/generator version, topology, active sector y evidencia del `LoadedContentSet` presente al crear el mundo. El display name nunca determina identidad ni archivo; dos mundos con nombre y seed iguales conservan WorldIds/slots distintos y topología inicial equivalente.

`WorldSessionBootstrap` usa `bootstrap_v1` y el contrato `WorldDeterminism` existente para derivar un único starter sector. Este bootstrap es mínimo y reemplazable: no se presenta como macro worldgen. Seed vacío usa randomness criptográfica sólo para elegir el entero signed 64-bit; no se agregó autoridad `UnityEngine.Random`.

`world_session_v1`, schema `1`, quedó como payload hermano dentro del envelope/store M37. Usa `WorldId.Canonical` como slot y persiste generation context, topología completa/hash canónico, active sector y provenance evidence por source/set. Read aplica deserialización estricta, parseo de IDs, `WorldTopology.TryCreate`, hash/membership/provenance preflight y recién después permite publicar la session. `current_slice_v1` y `CurrentSliceLoadService` no se modificaron ni reinterpretaron; provenance permanece evidencia y no compatibility policy.

La application route ahora es `MainMenu → WorldRuntime`, con `SampleScene` retenida como laboratorio en Build Settings. Main Menu ofrece New Game, Load Game y Exit; New Game persiste antes de entrar. World Runtime es un placeholder sin materialización y su menú Escape ofrece Continue, Save, Return to Main Menu y Exit. Return cierra la session sin autosave implícito; Exit dentro de Editor no termina el proceso de Mauro.

Validación autónoma en Unity `6000.4.6f1`, usando roots temporales y procesos Editor aislados:

- Runtime compile: `PASS`;
- Editor compile: `PASS`;
- `World Session / Persistence V1 Application Shell Diagnostics`: `PASS` para create, same-seed/different-world, exact round-trip, duplicate display names, catalog filtering, corrupt/semantic-invalid saves, lifecycle y contracts de payload/scenes;
- Play Mode real: `MainMenu.TryCreateWorld → WorldRuntime → Continue/Save/Return → MainMenu.TryLoadWorld → WorldRuntime`: `PASS`, con session eliminada en cada retorno;
- fresh Process A/B final posterior a review: `PASS`; A creó y cerró, B descubrió desde disco y cargó `world_ebffdfb63bac4e959ff4327da3406bf9`, seed `-3141592653589793`, topology hash `2ebdb9e76e28fe6131e883ee483b7ce2772cc913ae1bc1c19a411fb2bf61ce74` y active sector `sector_d8e0351713ca8a236e4d1a90bcb05267`, luego limpió el root temporal;
- `M37.0 Persistence Core Diagnostics`, `M37.1 Snapshot & Semantic Preflight Diagnostics`, `World Identity / Topology / Determinism Foundation` y `Minimum Content Source Identity & Provenance Foundation`: `PASS`;
- scene/build contract: `MainMenu` startup, `WorldRuntime` segundo, `SampleScene` conservada: `PASS`.

No se implementaron macro world plan/geography, history, terrain, authored composition, sector materialization/streaming/transitions, gameplay world-state persistence, Main Menu art final ni generation compatibility. El siguiente candidato queda `ID TBD — Macro World Plan V1`, `PLANNED — NOT AUTHORIZED`.

La revisión scoped detectó y corrigió un finding material antes del cierre: `GameDataManager` compartía root con `MainMenuSceneController`, por lo que `DontDestroyOnLoad` podía conservar UI de menú dentro de World Runtime. Ambos componentes quedaron en roots separados y el diagnóstico post-review exige que ningún `MainMenuSceneController` sobreviva en runtime; static contract y Play flow volvieron a `PASS`.

`codex review --uncommitted` no pudo iniciarse por el issue conocido de Windows `codex.exe: Acceso denegado`. Se realizó revisión manual scoped de authorities, publication transaction, slot/path identity, provenance/compatibility boundary, hashing, scene lifecycle y scope. El arranque Play Mode batch aislado emitió además un `ArgumentOutOfRangeException` del indexador `UnityEditor.Search`; el stack fue íntegramente UnityEditor, sin frame de Old Scars, y el flujo post-review completó `PASS`. No se observaron errores/exceptions relevantes del producto.

### ID TBD — Macro World Plan V1

Fecha: 2026-08-24.

Estado: `VALIDATED — FOUNDATION COMPLETE`.

Se reemplazó el starter sector de New Game por `MacroWorldPlan V1`, dato lógico inmutable generado antes de runtime. `WorldGenerationSettings` posee únicamente `WorldSizePreset`; `Small`, `Medium`, `Large` y `Huge` resuelven counts, world extents y separación mínima distintos, y esos valores resueltos se persisten para impedir reinterpretación silenciosa si cambia el tuning futuro. El mundo de producto queda aprobado como finito pero muy grande, con bounds físicos lógicos y distribución macro completa al crear la partida; el detalle sectorial continúa lazy.

`MacroWorldPlanGenerator` usa los domains SHA-256 existentes de `WorldDeterminism` para IDs y sampling maximin de posiciones dentro de `FiniteMacroWorldBounds`. La topology se deriva como árbol espacial mínimo conectado y mantiene las conexiones explícitas/multi-edge del contrato existente. No participan `WorldId`, `UnityEngine.Random`, `System.Random`, `GetHashCode`, Unity coordinates, filesystem ni insertion order. El único hash nuevo es la evidencia canónica del plan completo.

`WorldSession` contiene el plan actual y deriva de él su topology. `world_session_v1` conserva su snapshot type y evoluciona a schema `2` para nuevos mundos: persiste generation context, settings resueltos, bounds, placements, topology/hash, plan hash, active sector y provenance evidence. Schema `1` conserva un path legacy explícito, sin preset/plan fabricado y sin reinterpretación al volver a guardar. M37 permanece como envelope/store/recovery authority y `current_slice_v1` no cambió.

Main Menu agrega selección funcional de tamaño con default `Large`; Create genera y persiste el plan antes de entrar a `WorldRuntime`. No se agregó selector ni soporte artificial de workers. El futuro worker budget queda aprobado como setting de ejecución/rendimiento: `1 worker` y `N workers` deberán producir exactamente el mismo resultado lógico y nunca participarán en IDs, topology, plan hash o geography.

Validación autónoma en Unity `6000.4.6f1`, worktree y roots temporales aislados:

- Runtime compile: `PASS`;
- Editor compile: `PASS`;
- `Macro World Plan V1 Diagnostics`: `PASS` para same input, independencia de WorldId, escalas `Small < Medium < Large < Huge`, different-size, bounds, unicidad, spacing, connectivity, order independence y golden seed;
- golden MacroWorldPlan SHA-256: `3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a`;
- fuzz: `12 seeds × 4 presets`, todos los invariants `PASS`;
- tiempos diagnósticos aproximados: Small `10 ms`, Medium `43 ms`, Large `206 ms`, Huge `1156 ms`; no son budgets de producción;
- schema `2` save/read logical round-trip y compatibilidad schema `1`: `PASS`;
- `World Session / Persistence V1 Application Shell Diagnostics` y Play Mode real Main Menu(size)→Runtime→Save/Return→Load: `PASS`;
- fresh Process A/B: `PASS`; B reconstruyó desde disco `world_9600e0703d0f400790e46617959730da`, seed `-3141592653589793`, size `Huge`, plan hash `93cf399513b34c706088b1f0eee520e456695a415224bb91f10c08cca67e888d`, topology hash `347a831419d012d70501282dddb1b4a0e67ba81fa04d9fff141bb52d2c79b982` y active sector `sector_5ef1235b03d85764319ce1ec1d22f4fa`; el root temporal fue eliminado;
- `World Identity / Topology / Determinism Foundation`, `Minimum Content Source Identity & Provenance Foundation`, `M37.0 Persistence Core`, `M37.1 Snapshot & Semantic Preflight` y `M37.1 Current Slice Persistent Round-Trip`: `PASS`.

La revisión scoped confirmó una sola autoridad de topology/session/persistence, ausencia de grid/Unity/WorldId/provenance/worker leakage y compatibilidad schema 1 delimitada. Detectó y revirtió antes del cierre un drift incidental de `ProjectSettings.runInBackground` producido durante validación; el diff final no contiene `ProjectSettings`. `codex review --uncommitted` volvió a quedar bloqueado por `codex.exe: Acceso denegado`, por lo que se completó revisión manual del diff. No se observaron errores/exceptions relevantes de Old Scars; permanecieron warnings preexistentes de paquetes/compilación y mensajes de licensing/shutdown del Editor aislado.

No se implementaron elevation/noise, landforms, climate, hydrology/oceans, geology, vegetation, roads/rail, settlements/sites, history, sector geometry, terrain, materialization, transitions, NavMesh ni multithread generation. El siguiente candidato queda `ID TBD — Macro Elevation / Landforms V1`, `PLANNED — NOT AUTHORIZED`.

### ID TBD — Macro Elevation / Landforms V1

Fecha: 2026-08-24.

Estado: `VALIDATED — FOUNDATION COMPLETE`.

Se agregó `MacroGeographyPlan` como verdad lógica mundial separada de `MacroWorldPlan`, `WorldTopology`, sectores y Unity. Cubre exactamente los bounds finitos con un raster fixed-point compacto: elevation `ushort` normalizada y landform `byte` con `Plains`, `RollingHills`, `Highlands` y `Mountains`. `ElevationAt(MacroPoint2D)` interpola enteros de forma determinista/boundary-safe y `LandformAt` consulta la región; ninguna API de geografía recibe `SectorId`, por lo que futuros sectores consumen el mismo field global en lugar de intentar reconciliar relieve local.

`MacroGeographyGenerator` separa domains de landform regions, regional upheaval, base elevation, relief detail, mountain ridges y surface roughness. SHA-256 se usa sólo para derivar esos domains una vez; el inner loop usa value noise/fBm fixed-point y mixer explícito, sin `Mathf.PerlinNoise`, `UnityEngine.Random`, `System.Random`, `GetHashCode` ni SHA por sample. La clasificación global por percentile evita worlds casi enteramente planos/montañosos y un análisis committed exige distribución, coherencia regional, connected plains/mountains, rango vertical y mayor roughness montañosa. Vintage Story fue referencia conceptual para múltiples escalas, passes separados y landforms regionales; no se copió su algoritmo.

El tuning resuelto/persistido usa grids Small `49×49`, Medium `65×65`, Large `81×81` y Huge `113×113`; elevation+landform ocupan `3 bytes` raw por sample, aproximadamente `7.2–38.3 KB` antes del envelope/Base64. No se fija equivalencia a metros, sea level, sector shape ni terrain tiles. Un único `MacroGeographyPlan.CanonicalHash` prueba igualdad lógica para golden, persistence preflight y fresh process; no es compatibility policy.

`WorldSessionBootstrap` usa `macro_geography_v1` y genera plan → geografía → session. `world_session_v1` evoluciona a schema `3`: persiste settings resueltos, elevation/landform samples, geography hash y el MacroWorldPlan/provenance/active sector existentes sobre el mismo envelope/store M37. Read reconstruye la truth committed por validators y rechaza bytes/length/hash semánticamente inválidos antes de publicar; no regenera desde seed. Schema `1` permanece topology-only legacy y schema `2` MacroWorldPlan-only legacy; ambos vuelven a guardarse en su schema sin geografía fabricada. `current_slice_v1` no cambió.

Se agregó `MacroGeographyPreviewExporter` como herramienta Editor/diagnóstica sin autoridad. La preview golden inspeccionada mostró grandes lowlands continuos, macizos/ridges altos y regiones amplias de los cuatro landforms; los sector placements aparecen sólo como overlay y no crean seams ni estructuran el relief. El PNG de validación fue temporal y se eliminó.

Validación autónoma en Unity `6000.4.6f1`, worktree y roots temporales aislados:

- Runtime compile: `PASS`;
- Editor compile: `PASS`;
- `Macro Elevation / Landforms V1 Diagnostics`: `PASS` para same input, WorldId independence, different seed, exact bounds, interpolation/boundaries, global cross-sector query, variety/coherence, plains/mountains, elevation range, order independence y preview export;
- golden MacroGeography SHA-256: `c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e`;
- fuzz: `8 seeds × 4 presets`, todos los invariants `PASS`;
- tiempos diagnósticos aproximados plan+geography: Small `14 ms`, Medium `49 ms`, Large `199 ms`, Huge `910 ms`; se reportan como evidencia, no budgets de producción;
- schema `3` save/read exact round-trip y compatibilidad explícita schemas `1`/`2`: `PASS`;
- `Macro World Plan V1 Diagnostics`, `World Session / Persistence V1 Application Shell Diagnostics` y Play Mode real Main Menu→Runtime→Save/Return→Load: `PASS`;
- fresh Process A/B: `PASS`; el segundo proceso reconstruyó desde disco el mismo WorldId, seed, size, MacroWorldPlan hash, MacroGeography hash, topology hash y active sector, y eliminó el root temporal;
- `World Identity / Topology / Determinism Foundation`, `Minimum Content Source Identity & Provenance Foundation`, `M37.0 Persistence Core` y `M37.1 Current Slice Persistent Round-Trip`: `PASS`.

No se implementaron Unity Terrain, sector meshes, hydrology/coastlines, sea level final, climate/moisture, geology, vegetation/biomes, caves, roads/rail, settlements, history, sector polygons, materialization, transitions, NavMesh ni threading. No existe selector fake de workers; el futuro worker budget continúa performance-only y deberá conservar evidencia idéntica con `1` o `N` workers. El siguiente candidato queda `ID TBD — Macro Hydrology / Coastlines V1`, `PLANNED — NOT AUTHORIZED`.

La revisión scoped confirmó que geography no consume WorldId, SectorId, topology edges ni Unity coordinates; no hay SHA inner-loop, global random, one-noise authority, parallel loader/save engine, hash proliferation, worker option falsa ni scope creep hacia terrain. Schemas `1`/`2` preservan su truth original y schema `3` rehidrata samples committed antes de publicar la session. Se detectó y revirtió el drift incidental `ProjectSettings.runInBackground` producido por Play Mode aislado; el diff final no incluye ProjectSettings. `codex review --uncommitted` continuó bloqueado por `codex.exe: Acceso denegado`, por lo que se completó revisión manual del diff. Los logs conservaron warnings preexistentes de packages/licensing y failures intencionales de fixtures; los diagnostics terminaron `PASS` sin errores/exceptions relevantes del producto.

### ID TBD — Worldgen Gameplay Quality + Macro Water V1

Fecha: 2026-08-24.

Estado: `VALIDATED — FOUNDATION COMPLETE`.

Se preservó `MacroWorldPlan` y el tuning de `MacroGeographyPlan`. La topology MST existente queda reconocida como scaffold de conectividad lógico V1, no physical adjacency, road graph ni travel graph. Antes de fijar criterios de quality se midieron `192` mundos (`48 seeds × Small/Medium/Large/Huge`): gradient/local-relief y regiones conectadas demostraron que la geografía existente ya ofrece plains/corridors amplios junto con rugged terrain, por lo que no se retuneó.

Se implementó `MacroWaterPlan` global, inmutable y durable. `LandCoveragePreset` (`Low`, `Medium`, `High`, default `High`) vive en `MacroWaterGenerationSettings` separado y sólo cambia Water; `WorldGenerationSettings.DeterministicKey`, SectorIds, placements, topology y MacroGeography permanecen iguales. El pass resuelve sea level contra océano boundary-connected, bodies, coastline, conditioned drainage D8 terminante y basin candidates. Es precomputed generation-time data, no hydrology simulation runtime ni rivers finales. Un único Water SHA-256 canónico sirve golden/round-trip/fresh-process.

`WorldGameplayQualityAnalyzer` deriva gradient, roughness/local relief, low-relief traversal/site potential, connected corridors y starter-anchor suitability en fixed-point sin afirmar metros, grados, Walkable, NavMesh o Buildable. Hard failures se separan de soft findings. El starter de nuevos worlds se selecciona después de Water entre anchors terrestres suitable, con scoring determinista/canonical y preferencia central; no queda en océano ni mountain extrema. Un corpus stress de `384` variantes (`32 seeds × 4 sizes × 3 coverages`) terminó `0` hard rejections / `0` generation failures tras ajustar conservadoramente el piso de land-corridor de `2500` a `2000` Q16 y evaluar el neighborhood inmediato del anchor, sin aplanar ni retunear geography.

`WorldSessionBootstrap` cambia el contrato de nuevos mundos a `macro_water_quality_v1` y ejecuta plan → geography → Water → quality → starter → session. `world_session_v1` evoluciona a schema `4` sobre el mismo M37 envelope/store: persiste settings/sea/masks/body labels/coastline/conditioned surface/drainage/basins/hash de Water y la truth previa. Read reconstruye mediante validators reales, ahora incluyendo flood exacto sea-level/boundary, drainage/basins y quality hard preflight, antes de publicar. Schemas `1`/`2`/`3` cargan y vuelven a guardar su shape original sin fabricar plan/geography/Water/quality posteriores. `current_slice_v1` permanece intacto.

Main Menu agrega Land Coverage funcional; save catalog y World Runtime distinguen schema `4` de legacy `1`/`2`/`3`. El `WorldgenInspectorWindow` permanente permite seed/size/coverage y preview de seis paneles: elevation, landforms, gradient/suitability, Water/coastline, drainage/basins y sector anchors/MST. Tooling/export PNG no es authority. La preview golden inspeccionada mostró regiones de relief distinguibles, mar/costas significativos, corridors/site potential y rugged barriers sin seams de sector.

Validación autónoma en Unity `6000.4.6f1`, worktree, persistence roots y procesos Editor aislados:

- Runtime compile y Editor compile: `PASS`;
- `Worldgen Gameplay Quality + Macro Water V1 Diagnostics`: `PASS`; golden Water hash `c4563b2469d9315fb6c966b3b5bf7297d1ebca2de48e253df4c01abce0c8b727`; routine fuzz `4 × 4 × 3`;
- stress `32 × 4 × 3`: `384/384` generados, `0` rechazos y `0` failures; land/ocean/coastline, drainage, broad corridors, ruggedness y múltiples starter candidates válidos en todos los grupos;
- schema `4` exact round-trip + schemas `1`/`2`/`3` legacy: `PASS`;
- World Session edit-mode y Play Mode Main Menu(coverage)→Runtime→Save/Return→Load: `PASS`;
- fresh Process A/B: `PASS`; B reconstruyó mismo WorldId, seed `-3141592653589793`, size `Huge`, coverage `High`, MacroWater hash `67b7051ea09caadfa7aad1ff0d29caac714e3515ebff7344e537c11e9a6d868b`, topology hash `7090f6a82f7fb5936e79dcd11b19fcccecace14e9532083edd216d2865a50102` y active sector, luego eliminó el root temporal;
- `Macro World Plan V1`, `Macro Elevation/Landforms V1`, `World Identity/Topology/Determinism`, Content Source Provenance, Global Content ID Namespace, M37.0, M37.1 snapshot/semantic preflight, M37.1 transactional round-trip y M41.0 Navigation/Perception: `PASS`;
- timings finales aproximados plan+geography+Water+quality: Small `16 ms`, Medium `53 ms`, Large `209 ms`, Huge `907 ms`; payload schema `4` serializado `45,027/83,645/134,052/250,567 bytes` y Water raw estimado `16.9/29.8/46.3/90.0 KB`. No son budgets productivos.

La revisión scoped confirmó: sin authority paralela, topology física falsa, `WorldId`/path/provenance leakage, setting-domain leakage, runtime simulation, SHA inner-loop, fake workers, whole-world NavMesh, silent legacy upgrade ni scope creep a climate/rivers/terrain. Detectó y corrigió un refuerzo semantic-preflight: el ocean mask ahora debe coincidir exactamente con sea level + finite-boundary flood, además de sus validaciones internas. `codex review --uncommitted` volvió a no poder iniciarse por `codex.exe: Acceso denegado`; se completó revisión manual del diff. Los warnings de packages/licensing y failures de fixtures negativos permanecieron diferenciados de errores del producto. El drift automático `ProjectSettings.runInBackground` de batch Play Mode fue revertido y no forma parte del cambio.

No se implementaron climate/moisture, final rivers, terrain/materialization, sector transitions, roads/rail, vehicles/boats, buildings, history, whole-world NavMesh, threading ni continuous world simulation. El siguiente candidato queda `ID TBD — Macro Climate / Moisture V1`, `PLANNED — NOT AUTHORIZED`.

### ID TBD — Worldgen Pass Isolation Correction

Fecha: 2026-08-24.

Estado: `VALIDATED — SYSTEMIC CORRECTION COMPLETE`.

Se corrigió la dependencia sistémica por la que `WorldDeterminism.DeriveDomainKey` incorporaba el `GeneratorVersion` global y cada milestone downstream re-seedeaba Plan/Geography aunque sus contratos no hubieran cambiado. La API anterior fue reemplazada por `DerivePassDomainKey(WorldSeed, passGenerationContract, scope, pass)`, que no acepta `WorldGenerationContext`. `GeneratorVersion` queda como metadata global de creación; New Game usa `world_pipeline_v2`. `MacroWorldPlanGenerator` posee `macro_plan_v1`, `MacroGeographyGenerator` posee `macro_geography_v1` y Water conserva `macro_water_v1` sobre sus inputs committed.

La separación recuperó naturalmente —sin hardcodes de output— los goldens originales: MacroWorldPlan `3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a` y MacroGeography `c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e`. Water cambió legítimamente al volver a consumir esa Geography original y su nuevo golden es `ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0`. No se agregó schema `5`: schemas `1`/`2`/`3`/`4` rehidratan truth committed y nunca regeneran ni se actualizan silenciosamente.

Validación autónoma en Unity `6000.4.6f1`, worktree y persistence roots temporales aislados:

- Runtime/Editor compile: `PASS` con sólo los seis warnings preexistentes de `BuildingVisibilityManager`/`ItemStorageDebugPanel`;
- `Worldgen Pass Isolation Correction Diagnostics`: `PASS`; versiones globales histórica/actual/Climate sintética produjeron exactamente el mismo Plan/Geography/Water, un contrato Geography sintético conservó Plan y cambió Geography/Water, Land Coverage continuó Water-only, WorldId independence permaneció y el fuzz `2 seeds × 4 sizes` pasó;
- `Macro World Plan V1`, `Macro Elevation/Landforms V1`, `Worldgen Gameplay Quality + Macro Water V1` y `World Identity/Topology/Determinism`: `PASS` con los goldens corregidos;
- fresh Process A/B: `PASS`; el segundo proceso descubrió y reconstruyó desde schema `4` el mismo WorldId, seed, size, Plan/Geography/Water/topology hashes y active sector, y eliminó su root temporal;
- M37.0 Persistence Core, Content Source Identity/Provenance y Global Content ID Namespace: `PASS`;
- los diagnostics de Plan/Geography/Water cubrieron round-trip schema `4` y paths legacy schemas `1`/`2`/`3` sin fabricar truth posterior; `current_slice_v1` no cambió.

Limitación de evidencia: el primer import limpio de este worktree quedó bloqueado en el prompt Windows firewall antes de lanzar Asset Import Workers. Un `Library` copiado del worktree Water permitió compile y diagnostics lógicos, pero Unity no resolvió los MonoBehaviours de escenas al abrirlas desde ese cache trasladado. Por eso el aggregate edit-mode de Application Shell terminó sólo con los checks de scene wiring `I/J` no observables, y Play Mode/M37.1 Current Slice/M41.0 no se declararon reejecutados. Los YAML/GUIDs de Main Menu/World Runtime permanecieron intactos, fresh Process A/B y la persistencia no-escena pasaron, y el diff no toca scenes, prefabs, navigation, gameplay ni `current_slice_v1`; no se fingió evidencia de esos gates.

La revisión scoped confirmó que no queda ningún consumidor de `DeriveDomainKey`, la versión global no entra en SectorId/placements/topology/Geography/Water/starter, Water cambia sólo por dependencia real de Geography, no hay framework determinista paralelo, SHA inner-loop, schema nuevo, regeneración legacy, runtime simulation, GameObjects ni scope creep a Climate. Se revirtió el drift automático `ProjectSettings.runInBackground`; `DataDriven_JSON_Rules.md` no cambió porque no existe contrato JSON nuevo.

No se implementaron Climate/Moisture, rivers, terrain/materialization, sectors/transitions, GameObjects ni runtime world-scale processing. El siguiente candidato continúa `ID TBD — Macro Climate / Moisture V1`, `PLANNED — NOT AUTHORIZED`.

### ID TBD — Worldgen / World Session Observability Correction

Fecha: 2026-08-24.

Estado: `VALIDATED — OBSERVABILITY CORRECTION COMPLETE`.

Se agregaron eventos lifecycle estructurados en los límites existentes, sin introducir un logger o manager paralelo. Después de que Create genera, guarda y publica correctamente, `[Worldgen][WORLD_CREATED]` resume `WorldId`, seed, pipeline, size/coverage, contratos y hashes de Plan/Geography/Water, sector count, sea level, starter, muestra landform/elevation/surface, candidates adecuados y tiempo de generación. Load publicado emite `[WorldSession][LOAD_OK]` con schema/truth presente o ausente; `WorldRuntimeSceneController.Start` emite `[WorldRuntime][SESSION_READY]` una sola vez por entrada; Save manual agrega `[WorldSession][SAVE_OK]` sin sustituir `[Persistence][WRITE_COMMIT]` como autoridad del commit físico.

Los diagnostics de Application Shell se ampliaron con captura scoped y cardinalidad exacta. Edit Mode probó un único Create/Save/Load, campos completos y schemas `1`/`2`/`3` con `PLAN/GEOGRAPHY/WATER=<ABSENT>` donde corresponde. Play Mode Main Menu→Runtime→Save→Return→Load→Runtime terminó `PASS` con `WORLD_CREATED=1`, `LOAD_OK=1`, `SESSION_READY=2`, `SAVE_OK=1` y `WRITE_COMMIT=2`. No existe logging en `Update`, `OnGUI`, samples, celdas, sectores ni consultas rutinarias.

Validación autónoma en Unity `6000.4.6f1`, worktree y persistence roots aislados:

- Runtime compile y Editor compile: `PASS`;
- `World Session / Persistence V1 Application Shell Diagnostics`: `PASS`;
- Play Mode lifecycle/cardinality: `PASS`;
- `Worldgen Pass Isolation Correction`, `Macro World Plan V1`, `Macro Elevation/Landforms V1`, `Worldgen Gameplay Quality + Macro Water V1` y M37.0 Persistence Core: `PASS`;
- goldens sin drift: Plan `3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a`, Geography `c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e`, Water `ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0`;
- `git diff --check`: `PASS`.

La revisión scoped confirmó que los logs consumen truth ya calculada, no alteran seed/contracts/hashes, no fabrican evidence legacy, no duplican filesystem/session authorities y no agregan schema, GameObjects, runtime simulation ni scope creep. Corrigió un detalle antes del cierre: una muestra starter inesperadamente no disponible ahora declara `<UNAVAILABLE>` en lugar de mostrar el valor default de la estructura. `codex review --uncommitted` continuó bloqueado por `codex.exe: Acceso denegado`, por lo que se completó revisión manual del diff. El Play Mode aislado produjo de nuevo una excepción interna de `UnityEditor.Search.SearchDatabase.IndexationOnStartup`; el stack permaneció fuera de Old Scars y el flujo terminó `PASS`. El drift automático `ProjectSettings.runInBackground` fue revertido y no forma parte del cambio.

Unity MCP se conserva como vía preferida de inspección del Editor normal cuando está disponible y es seguro; los procesos aislados quedan para imports/fresh-process/diagnostics que lo necesitan. Ninguna validación cerró o perturbó el Editor de Mauro. No se implementaron Climate, roads, settlements, worldgen adicional ni cambios de persistencia. El siguiente candidato queda `ID TBD — Macro Human Geography / Road Network V1`, `PLANNED — NOT AUTHORIZED`.

### ID TBD — Macro Human Geography / Road Network V1

Fecha: 2026-08-24.

Estado: `VALIDATED — FOUNDATION COMPLETE`.

Se agregó `MacroHumanGeographyPlan` como primera truth humana mundial committed, separada de `WorldTopology`, sectores y GameObjects. El pass `macro_human_roads_v1` selecciona hubs `RegionalHub`/`LocalHub` sobre tierra usando site/traversal potential, relief, spacing, coast útil y acceso del starter. Los IDs `human_site_<32 hex>` y `macro_road_<32 hex>` derivan sólo de `WorldSeed` + contrato/settings del pass; `WorldId`, versión global, insertion order, paths y random global no participan.

La red crea un backbone Primary espacial conectado por landmass, agrega enlaces no-tree para ciclos/redundancia y une cada LocalHub mediante una rama Secondary. No reutiliza el MST de `WorldTopology`. Un A* entero con tie-break canónico consume un cost field global donde low relief es barato, highlands/rugged caro, extreme terrain fuertemente penalizado y ocean impassable; diagonal corner-cutting oceánico se rechaza. Las rutas se simplifican a polylines macro colineales sin seams sectoriales ni routing runtime.

`WorldSessionBootstrap` usa metadata global `world_pipeline_v3` y ejecuta Plan → Geography → Water → quality/starter → Human Geography → session. `world_session_v1` evoluciona a schema `5` sobre M37 y persiste settings resueltos, sites, road class/endpoints, polylines, cost metadata y un único Human Geography canonical hash. Schemas `1`–`4` cargan y vuelven a guardar sólo su truth legacy, sin fabricar infraestructura ni silent upgrade; `current_slice_v1` permanece intacto.

La observabilidad `[Worldgen][WORLD_CREATED]` agrega contrato/hash, hubs, road counts, geometry points y starter-to-network; `[WorldSession][LOAD_OK]` reporta el hash o `<ABSENT>` en schemas legacy. El Worldgen Inspector/preview conserva seis paneles y reemplaza el panel de MST por Human Infrastructure sobre Water/coast, con roads Primary/Secondary, hubs y sector markers opcionales. No existe logging por road/cell/frame.

Validación autónoma en Unity `6000.4.6f1`, worktree y persistence roots aislados:

- Runtime/Editor compile: `PASS`; Tundra compiló Assembly-CSharp y Assembly-CSharp-Editor sin errores, con sólo warnings preexistentes de proyecto/paquetes;
- `Macro Human Geography / Road Network V1 Diagnostics`: `PASS`; golden `a786f018ce3bdea44aeb066c80e38cb1f5dc8e114c65bd7eb352489628245ba6`, determinism/WorldId/pass isolation/order independence, land/endpoints/ocean, branches/cycles, terrain-cost preference, starter access, corruption preflight y schema `5` round-trip;
- goldens upstream intactos: Plan `3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a`, Geography `c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e`, Water `ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0`;
- routine corpus `36/36` y stress `144/144` (`12 seeds × 4 sizes × 3 coverages`): `0` rechazos duros; `126` worlds con findings blandos de cobertura/gap, conservados como tuning y no rechazo;
- tiempos/payload aproximados: Small `28 ms/52,442 B`, Medium `79 ms/98,139 B`, Large `295 ms/160,050 B`, Huge `1,203 ms/288,407 B`; no son budgets productivos;
- preview PNG temporal exportada e inspeccionada: backbone, branches y links redundantes visibles sobre tierra, sin ocean crossing ni spaghetti; el artefacto no se incluyó;
- fresh Process A/B: `PASS`; ambos observaron Human Geography hash `7099469990ae9cfd21e4c5b27a233f5aff5a46f4f908b2ef62b5be0556260d18` junto con WorldId/seed/size/Water/topology/active sector iguales;
- World Session edit-mode, Play Mode Main Menu→Create→Runtime→Save→Return→Load, M37 Persistence Core, World Identity/Topology/Determinism, Macro Plan, Macro Geography, Water/Quality, Pass Isolation, Content Source Provenance, Global Content Namespace y M41 Navigation/Perception: `PASS`;
- Play flow observability: `WORLD_CREATED=1`, `LOAD_OK=1`, `SESSION_READY=2`, `SAVE_OK=1`, `WRITE_COMMIT=2`.

La revisión scoped detectó y corrigió un finding material: el generador producía backbones conectados, pero el semantic validator no demostraba explícitamente esa propiedad frente a un payload manipulado. El preflight ahora exige Primary conectado por landmass, semántica Regional↔Regional para Primary, Local↔Regional para Secondary y al menos una rama Secondary por LocalHub; una prueba negativa desconecta un hub y confirma failure accionable antes de publicación. La revisión también confirmó ausencia de sector-local roads, ocean crossings, topology-as-roads, random/hash inestable, SHA inner-loop, runtime routing, legacy upgrade y scope creep.

`codex review --uncommitted` no pudo iniciarse por el issue conocido de Windows `codex.exe: Acceso denegado`; se completó revisión manual scoped y se reejecutó el diagnóstico posterior al fix. Los mensajes de licensing/duplicate package assemblies y failures intencionales de fixtures quedaron separados de errores del producto. El drift automático `ProjectSettings.runInBackground` del Editor aislado fue revertido y no forma parte del diff; el cambio local preexistente de Mauro en el checkout principal permaneció intacto.

No se implementaron settlements detallados, bridges, streets, rail, terrain/road materialization, climate, final rivers, history, sector transitions, whole-world NavMesh ni simulación vial runtime. El siguiente candidato queda `ID TBD — Terrain Materialization Technical Spike`, `PLANNED — NOT AUTHORIZED`.

### ID TBD — Terrain Materialization Technical Spike

Fecha: 2026-08-25.

Estado: `VALIDATED — TECHNICAL SPIKE COMPLETE`.

Se implementó el primer consumer físico de `WorldSession` schema `5` sin modificar worldgen ni persistencia. `TerrainMaterializationPlanner` toma active `SectorId`, su placement committed, MacroGeography, Macro Water y polylines de Macro Human Geography; recorta una ventana lógica boundary-safe y proyecta height/landform/ocean/roads a un frame Unity local cerca del origen. `TerrainMaterializationPlan` es derivado, inmutable y transient; no posee hash propio ni usa `WorldId`, paths, random o GameObjects como authority. Schemas `1`–`4` sin truth suficiente fallan explícitamente y no fabrican terrain.

`WorldTerrainMaterializationController` crea una sola representación local: Unity Terrain/TerrainCollider, tints diagnósticos de landform, ocean mesh mask-clipped al sea level committed, `LineRenderer` para fragments de roads persisted, player técnico con `PlayerMovementController`/`PlayerMovementInputController` y una NavMesh terrestre local. La surface usa un proxy interno derivado del ocean mask para excluir seabed; un actor Core se genera mediante `ActorSpawnService` y navega a través del `ActorNavigationController` existente. Product sector no equivale a Terrain GameObject/NavMesh partition, no hay world-scale/inactive NavMesh y nada se materializa en `Update`.

La configuración Inspector/diagnóstico separa escala física de unidades macro. Se midieron:

- `512×512` Unity units, relief `180`, logical `1400×1400`, heightmap `129`: projection `4 ms`, Terrain `59 ms`, NavMesh `425 ms`, total `500 ms`, memoria estimada `164,192 B`, `11` objetos;
- baseline provisional `768×768`, relief `240`, logical `1800×1800`, heightmap `257`: projection `13 ms`, Terrain `12 ms`, NavMesh `796 ms`, total `823 ms`, memoria estimada `463,392 B`, `11` objetos;
- `1024×1024`, relief `320`, logical `2400×2400`, heightmap `257`: projection `14 ms`, Terrain `15 ms`, NavMesh `1,264 ms`, total `1,295 ms`, memoria estimada `724,608 B`, `11` objetos;
- probe rugged `512×512`, relief `1200`, logical `1800×1800`, heightmap `257`: NavMesh `634 ms`, total `660 ms`; pendiente máxima observada `51.52°` frente al contract de agente `45°`, con `142/142` samples steep rechazados por la NavMesh.

Estos números son mediciones del hardware/Editor actual, no budgets ni equivalencia macro-units→metros. La baseline `768/240/1800/h257` queda recomendada sólo como punto de comparación porque conserva detalle suficiente con coste total sub-segundo aproximado en el diagnostic; NavMesh domina el tiempo y requiere particionado/rebuild profiling antes de producción. La escala física final, tamaño de ventana, vertical exaggeration, tile/surface counts, mutation resolution y travel pacing permanecen `UNFROZEN`.

Validación autónoma en Unity `6000.4.6f1`, worktree, scenes y persistence roots aislados:

- Runtime compile y Editor compile: `PASS`;
- `Terrain Materialization Technical Spike Diagnostics`: `PASS`; comprobó equivalencia determinista, scale isolation, Terrain/TerrainCollider, samples físicos contra MacroGeography, sea/coast, roads en el mismo frame, safe land spawn, un solo Terrain/NavMesh local, paths completos, slope exclusion, schema `5` round-trip y goldens intactos. La reejecución posterior al hardening del tooling midió totals `481/895/1,317 ms` para los tres candidates y volvió a terminar `PASS`;
- Play Mode Main Menu→Create→WorldRuntime→Save→Return→Load: `PASS`; cardinalidad `WORLD_CREATED=1`, `LOAD_OK=1`, `SESSION_READY=2`, `MATERIALIZATION_READY=2`, `SAVE_OK=1`, `WRITE_COMMIT=2`, con materialización y navegación actor real repetidas al crear/cargar;
- fresh Process A/B: `PASS`; reconstruyó mismo `WorldId`, seed, size, active sector, topology y hashes de Plan/Geography/Water/Human Geography desde disco;
- World Session edit-mode schemas `1`–`5`, M37 Persistence Core, World Identity/Topology/Determinism, Content Source Provenance, Macro Human Geography y M41 Navigation/Perception: `PASS`;
- goldens sin drift: Plan `3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a`, Geography `c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e`, Water `ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0`, Human Geography `a786f018ce3bdea44aeb066c80e38cb1f5dc8e114c65bd7eb352489628245ba6`;
- `git diff --check`: `PASS` antes del cierre Git; el diff final no incluye ProjectSettings.

Tres PNG temporales fueron exportadas e inspeccionadas fuera del repo: inland/plain mostró relief bajo, costa y roads globales continuas; rugged mostró highlands/mountains coherentes; coastal mostró ocean mask/sea level alineados con la costa. Los tints blocky y lines doradas son visualización técnica, no biome, texturing o road surface final. Unity MCP confirmó en preflight que el Editor normal estaba ready, sin compile/Play ni Console entries; la validación reproducible posterior usó procesos aislados. Cuando el GUI normal dejó de estar disponible no se intentó controlarlo o cerrarlo.

La revisión scoped detectó y corrigió antes del cierre: alias mutable de configuración dentro del plan; TerrainCollider/seabed incluido como source NavMesh; restauración incompleta de `RenderSettings.sun`; y el diagnostic permanente que no restauraba el scene setup previo. También confirmó ausencia de sector=Terrain, coordenadas Unity gigantes, segundo movement/navigation authority, roads regeneradas localmente, terrain noise paralelo, TerrainData durable, schema nuevo, per-frame materialization, voxel/deformation prematura y streaming scope creep. `codex review --uncommitted` volvió a no iniciar por `codex.exe: Acceso denegado`; se completó revisión manual del diff y las suites finales posteriores a los fixes. El Play Mode aislado conservó una excepción interna conocida de `UnityEditor.Search.SearchDatabase.IndexationOnStartup`; quedó fuera de Old Scars y el flujo terminó `PASS` sin errores/exceptions relevantes del producto.

Unity Terrain continúa recomendado como backend provisional después del spike: heightmap/TerrainCollider/local NavMesh funcionan con la truth actual y dejan un seam natural para futura mutación local. No prueba todavía final tiling/streaming, road/water surfaces, interiors/links, rendering/vegetation, persistence de mutations, caves/overhangs ni production performance. La composición futura queda `committed base terrain truth + durable local terrain mutations → materialized physical terrain`, sin implicar voxels.

No se implementaron Biomes/Environment, settlements/streets, final sector streaming, terrain mutations, vegetation, final roads/water, climate, rivers ni gameplay world persistence. El siguiente candidato queda `ID TBD — Macro Environment / Biome Regions V1`, `PLANNED — NOT AUTHORIZED`; un Terrain Materialization V1 estrecho sólo deberá adelantarse si evidencia futura descubre un blocker fundacional real.

### ID TBD — World Runtime / Player / Save Continuity System Harmony Correction

Fecha: 2026-08-25.

Estado: `VALIDATED — SYSTEM HARMONY CORRECTION COMPLETE`.

El product `WorldRuntime` dejó de usar la cápsula/cámara técnica del spike y ahora instancia la misma composición authored de player/camera que consume `SampleScene`. `PFB_PlayerGameplayComposition` conserva el player real con profile Core, `PersistentSceneObjectId`, `ActorRuntimeIdentity`, Inventory, Equipment/ownership, health/medical/needs, visual rig/animación, `PlayerMovementController`/input y el `CameraRigController` existente. El materializador vuelve a ser un consumer físico acotado: terrain, water/roads proyectadas y NavMesh local, sin ownership de player/camera.

Se agregó `world_gameplay_v1` schema `1` como payload hermano sobre M37, enlazado por `WorldId` + `ActiveSectorId` y con `current_slice_v1` sin cambios como truth gameplay. Load sigue el orden terrain → composición/profile → semantic binding preflight → Current Slice transactional apply/compare-or-rollback → camera bind. Worlds schema `5` anteriores sin sidecar usan bootstrap legacy explícito; un sidecar de otro world/sector falla antes de aplicar. Save preflighta gameplay antes de escribir y sólo declara éxito cuando terminan world session + gameplay; un fallo del segundo commit queda parcial, failed y accionable, sin fingir transacción multi-file.

La identidad authored legacy `scene_sample_scene_actor_player_primary` se preservó intencionalmente como valor opaco para mantener continuidad. M36 validó formato/unicidad en `SampleScene`; M37/M38 y los ciclos WorldRuntime demostraron un único `ActorInstanceId`/player role/registry representation, sin duplicar autoridad durable. Return to Main Menu libera representaciones Current Slice antes del unload, limpia la session y resetea `WorldClock` sin destruir `GameDataManager`.

Validación autónoma en Unity `6000.4.6f1`, worktree y roots temporales aislados:

- Runtime compile y Editor compile: `PASS`;
- static/semantic shared-composition y WorldSession application contracts: `PASS`;
- Play Mode New Game → materialization → real player movement/camera → health mutation → Save → Menu → Load, dos ciclos consecutivos: `PASS`; pose, `PersistentSceneObjectId`, `ActorInstanceId` y health restaurados con cardinalidad 1;
- world legacy sin gameplay sidecar: `PASS` mediante `LegacySafeSpawn`; copia deliberada A→B rechazada en `SemanticPreflight` sin publicar gameplay ready;
- fresh Process A/B en dos Unity separados: `PASS`; mismo WorldId, SectorId, topology, seed, actor identity, pose y health desde disco;
- M36.1 Foundation Identity, M37.0 Persistence Core, M37.1 Snapshot/Semantic Preflight y Current Slice persistent round-trip, M38 Actor Lifecycle, M38 Needs/WorldClock, M39 Health, Player Controls/Camera, M41 Navigation/Perception, Terrain Materialization, World Identity/Topology, Content Provenance, Macro Plan, Pass Isolation, Geography, Water y Human Geography: `PASS`;
- tres capturas Terrain inland/rugged/coastal fueron exportadas fuera del repo e inspeccionadas con render activo; no mostraron cápsula/camera fixture del materializador y luego se eliminaron;
- el intento de Terrain con `-nographics` se descartó por `RenderTexture.Create failed`; la repetición batch con render terminó `PASS`. Los mensajes del cliente de licensing fueron infraestructura y los procesos devolvieron exit `0` en las pruebas aceptadas.

La revisión System Harmony confirmó que no existe segundo persistence engine, Current Slice loader, player/camera authority, WorldManager, schema `6`, upgrade legacy silencioso ni scope creep. `codex review` continuó bloqueado por `codex.exe: Acceso denegado`, por lo que se completó una revisión manual scoped. El cambio automático `ProjectSettings.runInBackground` del Editor aislado se revirtió y no forma parte del diff; los scripts temporales de authoring, saves y capturas fueron eliminados.

Regla permanente: reutilizar la misma clase no constituye integración suficiente si ya existe una composición/autoridad gameplay. Product runtime debe consumir las autoridades establecidas de player, camera, identity, persistence y gameplay en vez de construir fixtures técnicos paralelos.

No se implementaron Biomes, terrain scale tuning, streaming, settlements ni optimización de NavMesh. El siguiente candidato permanece `ID TBD — Macro Environment / Biome Regions V1`, `PLANNED — NOT AUTHORIZED`.

### ID TBD — Integrated Gameplay Runtime / SampleScene Convergence

Fecha: 2026-08-26.

Estado: `VALIDATED — CONVERGENCE COMPLETE`.

`WorldRuntime` ahora consume una única composición gameplay compartida en vez de ser un terrain viewer con componentes seleccionados. `GameplayRuntimeComposition` enlaza el `PFB_PlayerGameplayComposition` real con las superficies ya existentes de Inventory/Storage, Needs/Health, interacción contextual, progress/result, feedback e input blocking; `SampleScene` consume exactamente el mismo wiring mediante un bootstrap delgado y conserva únicamente sus fixtures diagnósticas estrechas como laboratorio.

La fixture authored de integración se extrajo a `Resources/Development/PFB_IntegratedGameplayFixture`: casa M32, dos puertas, cinco contenedores, un actor y los world items authored crowbar/rifle. `DevelopmentGameplayIntegrationFixture` la coloca sólo en Editor/development builds sobre tierra generated con slope/height/spacing/NavMesh preflight, falla explícitamente si no encuentra placement válido y no participa de worldgen truth. La extracción preservó exactamente `14` `PersistentSceneObjectId` authored y `2` `ItemInstanceId` authored, sin regeneraciones, duplicados ni una segunda autoridad durable de player.

El orden de producto queda `WorldSession → terrain materialization → shared gameplay runtime → development fixture → shared player/profile → world_gameplay_v1/CurrentSlice transactional apply → camera/input → gameplay ready`. La espera controlada de un frame ocurre con input deshabilitado para permitir que representaciones authored existentes inicialicen sus profiles/storages antes del semantic preflight. `[WorldRuntime][GAMEPLAY_RUNTIME_READY]` registra una sola vez cardinalidad de player, camera, WorldClock, Inventory session, interacción, Needs/Health y fixture.

Validación autónoma en Unity `6000.4.6f1`, Library aislada y roots temporales:

- Runtime compile y Editor compile posteriores a retirar authoring/capture temporal: `PASS`;
- M36 authored identity: `14` scene roots + `2` world item IDs, `0` missing/duplicate/invalid: `PASS`;
- Play Mode MainMenu → New Game → generated WorldRuntime: cardinalidad exacta `1` para player, Main Camera, WorldClock, Inventory session, interacción, Needs y Health; fixture validada sobre land: `PASS`;
- Inventory open/close, Health arbitration, Needs visible/ticking, container search/transfer, authored crowbar pickup/equip y contextual `core:force_door` mediante authorities existentes: `PASS`;
- Save → Menu → Load repetido restauró pose, `ActorInstanceId`, `PersistentSceneObjectId`, health, container quantity, equipped crowbar/world-item absence y door tags: `PASS`;
- world legacy sin sidecar conservó safe bootstrap explícito; sidecar World A aplicado a World B fue rechazado en semantic preflight sin publicar gameplay ready: `PASS`;
- fresh Process A/B en dos Unity separados preservó WorldId, SectorId, topology, seed, actor identity, pose y health: `PASS`;
- M37 CurrentSlice/Persistence, M38 actor lifecycle/Needs/WorldClock, M39 Health, Inventory/Storage, Player Controls/Camera, M40 Combat/Armor, M41 Navigation/Perception/Encounter AI, Terrain Materialization, WorldSession, Content Provenance/Namespaces y worldgen pass isolation/Human Geography: `PASS`;
- goldens sin drift: Plan `3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a`, Geography `c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e`, Water `ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0` y Human Geography upstream unchanged: `PASS`.

La revisión System Harmony confirmó ausencia de player/camera/session/WorldClock duplicados, segundo Inventory/Health/Interaction/Persistence, bootstrap tardío dependiente del scene-load order, fixture filtrada a worldgen, identidad inestable o manager agregado. El visual capture temporal confirmó el gameplay camera con player/fixture sobre terrain generated; sigue siendo presentación gris diagnóstica y no aceptación audiovisual final. Los scripts temporales fueron retirados. El runner Terrain `-nographics` no dispone de graphics device para RenderTexture/URP; la repetición aislada D3D11 pasó. Ningún proceso o configuración del Editor de Mauro fue modificado.

Regla permanente: `WorldRuntime` es el runtime gameplay integrado canónico; `SampleScene` es un laboratorio. Una feature sólo cuenta como integrada cuando sus autoridades, UI/input, consumers del mundo e interacciones de persistencia coexisten y pueden probarse en ese runtime.

No se implementaron Biomes, climate, settlements, terrain-scale tuning, final roads, streaming ni optimización de NavMesh. El siguiente candidato no se inicia en esta tarea.

### Runtime Playtest Ergonomics Cleanup

Fecha: 2026-08-28.

Se corrigió la separación de input del panel F3: el puntero sobre Runtime Debug Tools ya no suprime WASD/Shift, pero clicks, cámara e interacciones siguen bloqueados por las superficies existentes; Inventory conserva su modalidad y el filtro Item Debug captura teclado sólo mientras está enfocado. Item Debug enumera las `ItemDefinition` reales de `GameDatabase` y usa el camino normal `InventoryComponent.AddItemByDefinitionId` para probar equipment, storage y armas sin una autoridad paralela. El resolver de teleport mantiene la validación de solape de `PlayerMovementController` y permite suelo materializado o superficies authored visibles con normal de suelo, rechazando actores, triggers y geometría no caminable.

Validación autónoma: Runtime compile y Editor compile `PASS`; Play Mode D3D11 de `MainMenu → New Game → WorldRuntime → Save → Return → Load` con `[WorldRuntime][GAMEPLAY_RUNTIME_READY]` y `World Session Application Play Flow: PASS`. No se modificaron schemas, worldgen, terrain authority ni gameplay persistence.

### Runtime Playtest Hotfix A — WorldClock / Item Debug / Sandbox Health Observability

Fecha: 2026-08-29.

Se corrigió el consumo del multiplicador debug de `WorldClock`: `Update` ahora avanza mediante `GameSecondsPerRealSecond`, conservando `Time.deltaTime`, el baseline persistido y el carácter efímero/reset a 1x del multiplicador. Item Debug mantiene `GameDatabase` + `InventoryComponent.AddItemByDefinitionId` como authorities, elimina scroll horizontal, muestra nombres display cortos, deja `ContentId` en detalles seleccionados y agrega cantidad entera `1..1000` para `Give X`. El último NPC sandbox ahora expone health/max, vital fraction, wounds, bleeding efectivo por game hour y lifecycle usando `ActorHealthComponent`, `ActorMedicalStateComponent` y `ActorRuntimeIdentity`; no se modificó la letalidad de firearms.

Validación autónoma en Unity `6000.4.6f1`, batchmode D3D11:

- Runtime/Editor compile: `PASS`; sólo warnings preexistentes del proyecto y mensajes de assemblies/licensing separados;
- prueba real de `WorldClock.Update`: `1x=1.2949677184224129`, `2x=2.5899354368448257`, `10x=12.949677184224129`, `100x=129.49677184224129` game seconds por frame de referencia; M38.1 `PASS`;
- M39 localized health/medicine: `PASS`; M40 combat/firearms: `PASS`; M41.3 WorldRuntime sandbox D3D11: `PASS`; Inventory Interaction UX/stack ownership: `PASS`;
- `git diff --check`: `PASS`; no schema, JSON, scene, ProjectSettings, M41.4 ni combat-lethality changes.

La confirmación visual manual de la ausencia del scrollbar, el click `Give X` sobre `.303` y la lectura del panel F3 queda pendiente de Mauro; la validación automatizada no se presenta como sustituto de esa aceptación manual.

### Health & Damage Consolidation — Pass A

Fecha: 2026-08-31.

Estado: `VALIDATED — CONDITION / CONSCIOUSNESS AUTHORITIES STABILIZED`.

La escritura histórica `transientTrauma 0 → ~0.97` quedó aislada como restore legítimo: el diagnóstico de consciencia guardaba un NPC inconsciente, demostraba recuperación y después cargaba deliberadamente el snapshot traumático anterior. Las únicas rutas productivas permanecen consecuencia aguda de una wound nueva, recuperación temporal, restore exacto de `ActorConditionStateData` y reset de inicialización. Restaurar wounds no reaplica trauma.

`ActorMedicalStateComponent` conserva wounds/bleeding/pain/treatment y entrega una sola consecuencia inmediata interna a `ActorConditionComponent`. Condition conserva Blood/trauma/estabilidad/`FunctionalState`; bleeding y recuperación de trauma progresan independientemente en el mismo avance de `WorldClock`, Blood se recupera lentamente sólo después de estabilizar bleeding y nunca desde/de vuelta a Dead. La recuperación funcional usa hysteresis data-driven y puede cruzar varios estados sin flapping. La pérdida fatal de Blood llama `ActorHealthComponent.Kill`, manteniendo Health/Lifecycle como única autoridad de muerte.

Validación focalizada en Unity `6000.4.6f1`, batchmode `-nographics`:

- Runtime compile y Editor compile: `PASS`;
- `Actor Consciousness & Incapacitation Diagnostics: PASS`: healthy baseline; una sola contribución inmediata; wound restore sin duplicación; trauma/bleeding independientes y combinados; hysteresis y recuperación multiestado; Blood recovery acotada; snapshot traumático restaurado intencionalmente; muerte única vía Health/Lifecycle;
- `M41 Sandbox Preparation Diagnostics: PASS`: Blue↔Red hostile, Red→Player hostile, Blue→Player neutral; roaming Blue/Red/White con home anchor, cancelación por threat y reanudación Idle; incapacidad estable en `Inactive` con acquisition habilitado y sin ping-pong, threat, navegación ni ataques;
- `git diff --check`: `PASS`.

La revisión independiente detectó y corrigió antes del cierre una restricción excesiva que habría rechazado profiles externos legacy con thresholds más cercanos que el default de hysteresis; el dead-band se acota a estabilidad 1 sin imponer separación retroactiva. También separó la recuperación runtime con hysteresis del restore/configuración determinista, evitando que cargar un snapshot válido heredase el peor estado vivo anterior.

No se modificaron balance de armas/letalidad, familias de wounds, órganos, fracturas, infección, cirugía, alimentación/hidratación/rest modifiers, UI final ni schema de Current Slice. `ProjectSettings.runInBackground` permanece como cambio local ajeno y fuera del commit.

### Health & Damage Consolidation — Pass B

Fecha: 2026-08-31.

Estado: `VALIDATED — IMMEDIATE VITAL CONSEQUENCE INTEGRATED`.

`CombatResolutionService` ahora conserva una sola cadena de consecuencia: resuelve armor, crea como máximo una wound final en `ActorMedicalStateComponent` y calcula una única consecuencia vital inmediata con severidad, tipo y región finales. `ActorConditionComponent` sigue recibiendo exclusivamente la consecuencia aguda de la wound; `ActorHealthComponent` sigue siendo la autoridad final de Vital Integrity, muerte, lifecycle y corpse. No se agregó daño paralelo a `WeaponCombatService`, un manager global ni un segundo pool vital.

`health.vital_integrity` es configuración data-driven de actor profile: escala y factores positivos para tipo de wound y región. Core usa blunt `0.35`, puncture `1.0`, laceration `0.60`, head `1.80`, torso `1.0` y limb `0.25`; los profiles legacy sin el bloque conservan defaults compatibles. Current Slice ya persiste el scalar Health existente, por lo que no cambian schema/envelope ni se inventa estado adicional.

Validación focalizada en Unity `6000.4.6f1`, batchmode `-nographics`:

- Runtime compile y Editor compile: `PASS` (warnings preexistentes separados);
- `M40.1 Armor & Penetration Diagnostics: PASS`: blunt limb `5.69`, blunt head `50.40`, `.303` limb `16.25`, torso `65.00`, head `117.00`, dos limb `.303` acumulados `32.50`; cubre armor stop/penetration, una wound/una consecuencia vital, muerte inmediata vital y Current Slice fresh-session;
- `Actor Consciousness & Incapacitation Diagnostics: PASS`: bleeding fatal continúa cerrando mediante Health/Lifecycle y restore de Condition conserva su contrato;
- `M41 Sandbox Preparation Diagnostics: PASS`: regresión AI P0, afiliaciones, roaming y `Inactive` estable con acquisition habilitado;
- `git diff --check`: `PASS`.

No se modificaron la recuperación fisiológica, WorldClock, balance adicional, trauma persistence, familias de wound, órganos, fracturas, infección, UI final ni el schema de Current Slice. `ProjectSettings.runInBackground` permanece como cambio local ajeno y fuera del commit.
