# Old Scars - Current Milestone

## Estado Actual Del Prototipo

El prototipo actual tiene una base debug validada para interacciones contextuales data-driven, acciones con duracion debug, movimiento point-and-click, camara debug, limpieza tecnica de escena, evaluacion auditable de requisitos de herramienta equipada, inventario jugable v0 con pickup loop, container loot v0 y primer POI jugable compacto.

Milestone 16 esta validado en Unity.

No hay milestone implementado pendiente de validacion.

Ultimo milestone validado:

- Milestone 16: Primer POI jugable completo.

Proximo recomendado:

- Preparar el siguiente sprint sobre la base validada de Milestone 16.


## Milestones Validados

### Milestone 9: Point-and-Click Debug Movement + Camera Rig

Estado: `validated`.

Validado en Unity:

- point-and-click debug movement;
- UI click blocking;
- separacion entre click derecho corto y click derecho drag;
- CameraRig con WASD pan y right-drag rotation.

### Milestone 9.1: Movement / Interaction / Camera Polish

Estado: `validated`.

Validado en Unity:

- Debug Player con CharacterController;
- CharacterController gravity;
- interaction range / proximity gating;
- mouse wheel camera zoom.

### Milestone 10: Stateful Contextual Actions Hardening

Estado: `validated`.

Validado en Unity:

- `WorldObjectTags` separa initial tags y runtime tags;
- initial tags quedan como configuracion del Inspector;
- runtime tags mutan durante Play;
- `add_tag` y `remove_tag` afectan al target en runtime;
- los cambios no son save system ni world state persistente;
- no se tocaron JSON ni `InteractionSystem`.

### Milestone 11: Action Duration / Action In Progress

Estado: `validated`.

Validado en Unity:

- `ActionDefinition.cost.time` se usa como duracion debug;
- `DebugActionProgressController` maneja accion activa, duracion, elapsed/progress y finalizacion;
- `ContextualActionDebugPanel` ya no ejecuta actions directamente, inicia progreso;
- `DebugActionExecutor` sigue siendo sincronico y aplica effects solo al terminar la duracion;
- durante accion activa no se puede iniciar otra accion;
- durante accion activa no se abre otro menu contextual;
- durante accion activa no se aceptan nuevos clicks de movimiento;
- la camara sigue libre;
- `force_door` dura 3s y aplica effects al terminar;
- `pry_open_container` dura 2s y aplica effects al terminar;
- `examine_object` dura 1s y muestra info al terminar;
- no se toco JSON;
- no se creo inventario, loot, save system, combate, IA, animaciones finales ni UI final.

### Milestone 12: Item Instances + Debug Inventory

Estado: `validated`.

Validado en Unity:

- `ItemInstance` runtime-only funciona.
- `DebugInventory` crea instancias runtime desde `ItemDefinition`.
- `DebugInventory` permite caso sin item equipado mediante `none`, vacio o indice invalido.
- `ActorInteractionContext` consulta `DebugInventory` si esta asignado.
- `equippedItemDefinitionId` legacy queda como fallback solo cuando no hay `DebugInventory` asignado.
- `WorldInteractionDebugTester` usa el metodo actual de `ActorInteractionContext` para obtener el item equipado.
- Con `rusted_crowbar_01` equipado aparecen `force_door` y `pry_open_container`.
- Con `equippedItemIndex = -1` aparece `Equipped item: (none)` y no se muestran acciones de herramienta.
- `DebugInventory`, si esta asignado, manda sobre el fallback legacy.
- `InteractionSystem` sigue recibiendo solo definition_id y no depende de `DebugInventory`, `ItemInstance` ni MonoBehaviour.
- Milestone 11 sigue funcionando: duracion de acciones y runtime tags siguen correctos.
- No se tocaron JSON, runtime tags, `DebugActionExecutor` ni `ActionDefinition.cost.time`.
- No se creo inventario final, UI de inventario, loot, pickup/drop, save system, equipment system final, slots reales ni durabilidad funcional.

### Milestone 12.1: Technical Cleanup

Estado: `validated`.

Validado en Unity:

- `DebugInventory` verificado/configurado en `Debug Player` con `initialItemDefinitionIds = ["rusted_crowbar_01"]` y `equippedItemIndex = 0`.
- `ActorInteractionContext.debugInventory` apunta al `DebugInventory` del `Debug Player`.
- `GameDataManager` quedo como root GameObject.
- El warning de `DontDestroyOnLoad` ya no aparece.
- `CoreDataSystem` carga correctamente.
- `Core` se mantiene aunque quede vacio.
- El `ActorInteractionContext` duplicado bajo `Debug_Actor` fue renombrado a `Deprecated_ActorInteractionContext_Legacy`.
- `Deprecated_ActorInteractionContext_Legacy` quedo desactivado y aislado.
- `Debug_Actor` se mantiene.
- `DebugPlayerContext.cs`, `GameDataDebugTester.cs`, `ActionAvailabilityDebugTester.cs` y `ActorInteractionContext.EquippedItemDefinitionId` quedan documentados como deprecated/legacy.
- `_Recovery` se mantiene.
- No se borraron scripts legacy.
- No se borro `_Recovery`.
- No se tocaron codigo ni JSON.
- Movimiento, camara, UI blocker, action duration, runtime tags, DebugInventory e InteractionSystem siguen funcionando.

### Milestone 13: Tool Requirement Hardening

Estado: `validated`.

Validado en Unity:

- `requirements.weapon_tags` se mantiene como campo activo.
- `weapon_tags` queda documentado como nombre legacy compatible para required equipped item tags.
- No se agrego `required_item_tags`.
- No se migro schema.
- No se tocaron `actions.json` ni `items.json`.
- No se toco JSON.
- No se cambio la semantica OR de `weapon_tags`.
- `ActionAvailabilityResult` agrega resultado explicable de disponibilidad.
- `ActionAvailabilityEvaluator.Evaluate()` calcula disponibilidad, razones de bloqueo, razones de exito, tags requeridos, tags faltantes y tags del item que hicieron match.
- `ActionAvailabilityEvaluator.IsAvailable()` se mantiene como wrapper compatible de `Evaluate(...).IsAvailable`.
- `InteractionSystem` usa `Evaluate()` internamente y sigue devolviendo solo acciones disponibles.
- `InteractionQuery.LogAvailabilityDetails` permite activar logs detallados opcionales.
- `WorldInteractionDebugTester.logAvailabilityDetails` queda desactivado por defecto.
- `logAvailabilityDetails` permite ver por que una accion aparece o se bloquea.
- `DataValidator` emite warning no destructivo si un `requirements.weapon_tags` valido no aparece en ningun item cargado.
- `DataValidator` no bloquea la carga.
- Con palanca equipada, `force_door` y `pry_open_container` aparecen correctamente.
- Sin item equipado, las acciones de herramienta se bloquean correctamente.
- Action duration y runtime tags siguen funcionando.
- No se tocaron `DebugInventory`, `ItemInstance`, `DebugActionProgressController`, `ActionDefinition.cost.time` ni runtime tags.
- No se creo inventario final, equipment final, slots reales, UI de inventario, loot, pickup/drop ni save system.

### Milestone 14: Playable Inventory + Pickup Loop

Estado: `validated`.

Validado en Unity:

- `InventoryComponent` v0 runtime-only funciona.
- El jugador inicia sin item equipado, con `InventoryComponent` vacio y `equippedItemIndex = -1`.
- `ActorInteractionContext` prioriza item equipado desde `InventoryComponent`.
- Si `InventoryComponent` existe y no tiene item equipado, se considera sin item y no cae a `DebugInventory`.
- `DebugInventory` se mantiene como legacy y solo se usa si no existe `InventoryComponent`.
- `equippedItemDefinitionId` legacy solo se usa si no existe `InventoryComponent` ni `DebugInventory`.
- `InventoryDebugPanel` OnGUI abre/cierra con `I`, cierra con Escape, lista items y permite `Equip` / `Unequip`.
- `DebugWorldUiInputBlocker` consume clicks cuando el panel de inventario esta abierto.
- `WorldItemPickup` funciona con `rusted_crowbar_01`.
- `pick_up_item` dura 0.5s.
- Al recoger, el item se agrega al `InventoryComponent`.
- La palanca del mundo queda oculta/no interactuable.
- `DebugActionExecutionContext` pasa `ActorInteractionContext`, target y item equipado al executor.
- `DebugActionExecutor` soporta el effect cerrado `pick_up_item` sin romper `add_tag`, `remove_tag` ni `show_target_info`.
- `actions.json` agrega `pick_up_item` con `cost.time = 0.5`.
- `tags.json` agrega `world_item`, `pickupable` y `picked_up`.
- `items.json` no fue modificado.
- `DataValidator` permite `pick_up_item` como effect cerrado sin `tag`.
- `SampleScene` agrega `Debug World Crowbar` interactuable con `world_item`, `pickupable`, `inspectable` y `WorldItemPickup.itemDefinitionId = rusted_crowbar_01`.
- Al equipar la palanca, `force_door` y `pry_open_container` aparecen correctamente.
- `InteractionSystem` sigue sin depender de `InventoryComponent`, `DebugInventory`, UI, `WorldItemPickup`, `ItemInstance` ni MonoBehaviour.
- Action duration y runtime tags siguen funcionando.
- No se creo inventario final, drag/drop, grid, peso/capacidad real, save system, loot aleatorio, contenedores reales, pickup/drop generico completo, slots reales, UI final, combate ni IA.

### Milestone 15: Container Loot v0

Estado: `validated`.

Validado en Unity:

- `LootTableDefinition` v0 funciona.
- `GameDataLoader` carga `loot_tables/*.json`.
- `GameDatabase` registra y expone loot tables.
- `DataValidator` valida loot tables sin errores.
- `DataValidator` permite effect cerrado `search_container`.
- `container_loot.json` carga `debug_sealed_container_loot_01`.
- `ContainerLootComponent` saquea el contenedor usando `DebugActionExecutionContext`.
- `ContainerLootComponent` obtiene `InventoryComponent` desde `ActorInteractionContext`, sin buscarlo globalmente.
- `search_container` requiere `opened_container` + `lootable_container`.
- `search_container` aparece solo cuando el contenedor tiene `opened_container` + `lootable_container`.
- `search_container` dura 1.5s.
- `scrap_metal_01` se agrega como item simple de prueba.
- `debug_sealed_container_loot_01` entrega `scrap_metal_01 x1`.
- `search_container` agrega `scrap_metal_01` al `InventoryComponent`.
- `InventoryDebugPanel` muestra `Scrap Metal`.
- Al saquear, se remueve `lootable_container` y se agrega `looted_container`.
- `search_container` no vuelve a aparecer despues de saquear.
- `InteractionSystem` sigue sin depender de inventario, loot ni MonoBehaviour.
- No se creo loot avanzado, UI final, save system, stacks, economia, crafting, combate ni IA.

### Milestone 16: Primer POI jugable completo

Estado: `validated`.

Validado en Unity:

- `SampleScene` funciona como primer POI jugable compacto tipo pequeno taller / bahia de mantenimiento industrial.
- El POI usa sistemas existentes, sin sistemas nuevos.
- `Debug Player` inicia dentro del POI con `InventoryComponent` vacio y sin item equipado.
- `Debug World Crowbar` funciona como herramienta inicial recogible.
- `Debug Locked Door` funciona como obstaculo forzable con palanca.
- `Debug Sealed Container` funciona como contenedor sellado, abrible y saqueable.
- `Debug Strange Machine` funciona como objeto ambiental examinable.
- Loop completo validado: recoger palanca -> equipar -> abrir/forzar obstaculo -> abrir contenedor -> buscar loot -> obtener Scrap Metal -> dejar estados runtime correctos.
- Palanca: `picked_up` agregado y `pickupable` removido.
- Puerta: `forced_open` agregado y `locked_door` removido.
- Contenedor abierto: `opened_container` agregado y `sealed_container` removido.
- Contenedor saqueado: `looted_container` agregado y `lootable_container` removido.
- Data load sigue OK con 0 errors y 0 warnings.
- `InteractionSystem` sigue desacoplado.
- No se toco codigo.
- No se toco JSON.
- No se crearon sistemas nuevos.
- No se rompieron `InventoryComponent`, `WorldItemPickup`, `ContainerLootComponent`, action duration, runtime tags ni loot tables.

## Sistemas Activos

### Data Layer

- JSON define contenido.
- C# ejecuta logica.
- `GameDataLoader` carga `StreamingAssets/Mods/Core`.
- `GameDatabase` expone definiciones cargadas.
- `DataValidator` valida IDs, types, tags, referencias, effects, loot tables y warnings no destructivos de `weapon_tags`.

### Interaction System

- `ActorInteractionContext` aporta actor tags, actor stats y equipped item definition id actual.
- Si `InventoryComponent` existe, `ActorInteractionContext` usa exclusivamente esa fuente aunque no haya item equipado.
- Si no hay `InventoryComponent` y `DebugInventory` esta asignado, `ActorInteractionContext` usa `DebugInventory` aunque no haya item equipado.
- Si no hay `InventoryComponent` ni `DebugInventory`, `ActorInteractionContext` usa `equippedItemDefinitionId` legacy.
- `InteractionSystem` recibe `InteractionQuery`.
- `InteractionSystem` resuelve item equipado, maneja `none`/vacio y evalua acciones por contexto.
- `InteractionSystem` puede loguear detalles de disponibilidad si `InteractionQuery.LogAvailabilityDetails` esta activo.
- `ActionAvailabilityEvaluator` evalua actor tags, target tags, item tags y actor_min_stats.
- `ActionAvailabilityEvaluator.Evaluate()` devuelve `ActionAvailabilityResult` explicable.
- `requirements.weapon_tags` sigue siendo el campo activo para required equipped item tags.

### Debug Item Runtime

- `ItemInstance` representa una instancia runtime minima de un item definition.
- `ItemInstance` guarda `InstanceId`, `DefinitionId` y `Condition`.
- `LootTableDefinition` representa una tabla v0 deterministica de loot.
- `InventoryComponent` v0 guarda una lista runtime plana de `ItemInstance`.
- `InventoryComponent` permite `AddItemByDefinitionId`, `EquipIndex` y `Unequip`.
- `InventoryComponent` no autoequipa al recoger.
- `DebugInventory` crea instancias runtime al iniciar cuando `GameDataManager` esta listo.
- `DebugInventory` no es inventario final, no guarda save data y no implementa equipamiento real.

### World Object Runtime Tags

- `WorldObjectTags` mantiene `tags` serializados como initial tags.
- `RuntimeTags` es la copia mutable durante Play.
- `Tags` sigue siendo alias compatible de runtime tags.
- `HasTag`, `AddTag` y `RemoveTag` operan sobre runtime tags.
- `ResetRuntimeTagsFromInitial` permite reset debug del estado runtime.

### Debug Action Execution

- `DebugActionProgressController` controla acciones debug en progreso.
- Usa `ActionDefinition.cost.time` como duracion debug.
- `DebugActionExecutor` ejecuta effects definidos por JSON.
- Effects soportados:
  - `add_tag`;
  - `remove_tag`;
  - `show_target_info`;
  - `pick_up_item`;
  - `search_container`.
- `add_tag` y `remove_tag` afectan solo al target.
- `show_target_info` lee `WorldObjectDebugInfo`.
- `pick_up_item` ejecuta `WorldItemPickup` sobre el target.
- `search_container` ejecuta `ContainerLootComponent` sobre el target.

### UI Debug Contextual

- `WorldInteractionDebugTester` coordina input de interaccion, raycast y bridge hacia UI.
- `WorldInteractionDebugTester.logAvailabilityDetails` activa logs detallados opcionales de disponibilidad.
- `ContextualActionDebugPanel` muestra acciones disponibles con OnGUI.
- `ContextualActionDebugProgressPanel` muestra progreso debug de accion activa.
- `ContextualActionDebugResultPanel` muestra resultados debug.
- `InventoryDebugPanel` muestra inventario v0 con OnGUI y se abre con `I`.
- `DebugWorldUiInputBlocker` consume click izquierdo cuando hay UI abierta.

### Movimiento Y Camara

- `PointClickMovementInputController` usa click izquierdo sobre `Ground`.
- `PointClickMovementController` mueve al Debug Player en X/Z.
- Debug Player usa `CharacterController`.
- La gravedad debug usa `verticalVelocity`.
- `WorldInteractionDebugTester` usa `interactionRange` antes de abrir menu contextual.
- `CameraRigController` controla:
  - WASD pan;
  - right-drag rotation;
  - mouse wheel zoom.

## Setup Debug Esperado

- Suelo en layer `Ground`.
- Objetos interactuables en layer `Interactable`.
- Objetos interactuables con `WorldObjectTags`.
- Objetos inspeccionables con tag `inspectable` y `WorldObjectDebugInfo`.
- Debug Player con:
  - `ActorInteractionContext`;
  - `InventoryComponent` v0 vacio para Milestone 14;
  - `DebugInventory` opcional para Milestone 12;
  - `PointClickMovementController`;
  - `CharacterController`.
- `ActorInteractionContext.inventoryComponent` apuntando al `InventoryComponent` del `Debug Player`.
- `InventoryDebugPanel.inventory` apuntando al `InventoryComponent`.
- Item de mundo debug con:
  - layer `Interactable`;
  - `WorldObjectTags` con `world_item`, `pickupable`, `inspectable`;
  - `WorldItemPickup.itemDefinitionId = rusted_crowbar_01`;
  - collider y renderer visibles.
- Contenedor debug saqueable con:
  - `sealed_container`;
  - `lootable_container`;
  - `inspectable`;
  - `ContainerLootComponent.lootTableId = debug_sealed_container_loot_01`.
- `WorldInteractionDebugTester.logAvailabilityDetails` desactivado por defecto; activarlo solo para auditar requisitos de acciones.
- Camara principal como hija del CameraRig.

## Proximo Recomendado

Preparar el siguiente sprint sobre la base validada de Milestone 16.

Milestone 16 fue validado con:

- inicio dentro del POI con `InventoryComponent` vacio y sin item equipado;
- palanca recogible y equipable desde `InventoryDebugPanel`;
- puerta forzable con palanca;
- contenedor sellado abrible y saqueable;
- maquina ambiental examinable;
- obtencion de Scrap Metal;
- estados runtime correctos para palanca, puerta y contenedor;
- data load OK con 0 errors y 0 warnings;
- `InteractionSystem` desacoplado;
- sin cambios de codigo ni JSON.

Milestone 14 fue validado con:

- al iniciar, `InventoryComponent` vacio y sin item equipado;
- puerta y contenedor muestran solo `examine_object`;
- palanca de mundo muestra `pick_up_item` y `examine_object`;
- `pick_up_item` dura 0.5s;
- al terminar, se crea una `ItemInstance`, el objeto deja de ser interactuable y aparece feedback;
- con `I`, la palanca aparece en inventario;
- la palanca no se autoequipa;
- al equiparla desde el panel, puerta muestra `force_door` y contenedor muestra `pry_open_container`;
- action duration y runtime tags siguen funcionando.

Milestone 15 fue validado con:

- abrir el contenedor con `pry_open_container`;
- confirmar que `sealed_container` se remueve, `opened_container` se agrega y `lootable_container` queda;
- volver a interactuar y ver `search_container`;
- ejecutar `search_container` durante 1.5s;
- recibir feedback de `Scrap Metal x1`;
- confirmar `scrap_metal_01` en `InventoryComponent` / `InventoryDebugPanel`;
- confirmar que `lootable_container` se remueve y `looted_container` se agrega;
- confirmar que `search_container` ya no aparece;
- confirmar que pickup, action duration, runtime tags e `InteractionSystem` siguen funcionando.

No debe crear todavia:

- inventario real;
- loot final o avanzado;
- save system;
- combate real;
- IA;
- dialogos complejos;
- UI final;
- sistema de animaciones final.

## Deuda Tecnica Menor

- No hay deuda tecnica menor bloqueante registrada.
- No hay deuda tecnica menor bloqueante registrada despues de Milestone 13.
