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
  - search_container.
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

## Decisiones De Scope

- No hay inventario final.
- No hay equipamiento runtime real.
- No hay entidades runtime complejas.
- No hay combate real.
- No hay save system.
- No hay IA.
- No hay UI final.
- No hay journal.
- No hay quest log.
- No hay EventBus de gameplay.
- Los sistemas actuales son prototipos debug para probar flujo data-driven.
