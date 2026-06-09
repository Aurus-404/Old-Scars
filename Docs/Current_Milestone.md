# Old Scars - Current Milestone

## Estado Actual Del Prototipo

El prototipo actual tiene una base debug validada para interacciones contextuales data-driven, acciones con duracion debug, movimiento point-and-click, camara debug, limpieza tecnica de escena, evaluacion auditable de requisitos de herramienta equipada, inventario de actor v0 con Storage y Equipped separados conceptualmente, pickup loop, container loot v0, primer POI jugable compacto, feedback de gameplay estructurado runtime-only, diagnostico runtime-only de disponibilidad de acciones, lectura visual debug estable de estados runtime por color, storage runtime-only de items con cantidades simples, inspeccion dependiente de `RuntimeTags`, defensas de acceso al storage de contenedores, necesidades runtime genericas de actor, health runtime v0 de actor, consumibles cerrados por JSON, UI debug de survival/health, saqueo manual de contenedores, loot de actor muerto debug, auditoria funcional post-M23.1 y primer pipeline data-driven de Actor Profiles.

Milestone 23, Milestone 23.0.1, Milestone 23.0.2, Milestone 23.0.3, Milestone 23.1, Milestone 23.1.1, Milestone 23.1.2, Functional Audit / Cleanup Pass post-M23.1 y Milestone 24 con sus passes M24.1-M24.4 estan validados en Unity.

No hay milestone implementado pendiente de validacion.

Ultimo milestone validado:

- Milestone 20: Item Storage / Container Foundation v0.
- Milestone 21: Stateful Inspection & Container Access v0.
- Milestone 21.0.1: Hotfix - State-Aware Inspection Selection.
- Milestone 22: Actor Needs & Debug Supply Containers v0.
- Milestone 22.1: Survival UI, Action Feedback & Manual Container Loot v0.
- Milestone 22.1.1: Hotfix - Wire Survival and Storage Debug UI.
- Milestone 22.1.2: Hotfix - Equippable Item Flag.
- Milestone 23: Actor Inventory Foundation v0.
- Milestone 23.0.1: Hotfix - Cleanup legacy equipped index warning.
- Milestone 23.0.2: Hotfix - Revalidate Action Requirements Before Execution.
- Milestone 23.0.3: Hotfix - Refresh Context Menu Availability.
- Milestone 23.1: Lootable Debug Actor + Health v0.
- Milestone 23.1.1: Hotfix - Health Examine Texts + Player Debug Damage.
- Milestone 23.1.2: Hotfix - Debug Player Health Feedback.
- Post-M23.1 Functional Audit / Cleanup Pass.
- Milestone 24: Actor Profile Pipeline v0.
- Milestone 24.1: Actor Profile Data Load.
- Milestone 24.2: Actor Profile Validation.
- Milestone 24.3: Actor Profile Runtime Apply.
- Milestone 24.4: Debug NPC Capsule Actor Profile Migration.

Proximo recomendado:

- Preparar Milestone 25: Storage Transfer v0 / Bidirectional Item Transfer. No hay implementacion definida todavia.


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

### Milestone 17: Gameplay Feedback Log Foundation / POI State Readability v0

Estado: `validated`.

Validado en Unity:

- El proyecto compila en Unity sin errores.
- Se agrego una base runtime-only de feedback de gameplay mediante `GameplayFeedbackEntryType`, `GameplayFeedbackEntry`, `GameplayFeedbackLog` y `DebugFeedbackLogPanel`.
- Los sistemas de gameplay registran entradas estructuradas y el panel debug solo las lee.
- `GameplayFeedbackLog` es append/read, runtime-only, no persistente y limitado por `maxEntries`.
- `GameplayFeedbackLog` no tiene listeners, subscriptions, callbacks, dispatch ni payload generico.
- El panel `Gameplay Feedback Log` aparece en `SampleScene`.
- `ItemPickedUp` se registra al recoger la palanca.
- `ItemEquipped` y `ItemUnequipped` se registran al equipar o desequipar.
- `ActionCompleted` se registra en `examine_object`, `force_door`, `pry_open_container` y `search_container`.
- `TargetStateChanged` debug registra cambios runtime de tags.
- `LootReceived` se registra al obtener `scrap_metal_01`.
- `search_container` deja de aparecer despues de saquear.
- El contenedor queda con `looted_container`.
- La puerta queda con `forced_open`.
- `InteractionSystem` no fue tocado.
- El gameplay no depende del panel de feedback.
- No se creo journal, quest log, UI final, save system, EventBus ni sistemas grandes.

### Milestone 18: Action Availability Diagnostics / Requirement Readability v0

Estado: `validated`.

Validado en Unity:

- El proyecto compila en Unity sin errores.
- Se agrego una capacidad diagnostica opcional para disponibilidad de acciones contextuales.
- El diagnostico usa la misma evaluacion que `GetAvailableActions()` mediante `ActionAvailabilityEvaluator.Evaluate()` y `ActionAvailabilityResult`.
- El diagnostico no duplica logica de disponibilidad y no cambia comportamiento jugable.
- `ActionAvailabilityDiagnosticReport` captura target, actor tags snapshot, target tags snapshot, item equipado, item tags snapshot y entradas por accion.
- `ActionAvailabilityDiagnosticEntry` captura action id/display, disponibilidad, razones de bloqueo, tags requeridos, tags faltantes y tags del item que hicieron match.
- `DebugActionAvailabilityPanel` muestra acciones disponibles y bloqueadas, razones de bloqueo y snapshots de contexto.
- `GetAvailableActions()` sigue devolviendo solo acciones disponibles.
- El menu contextual ejecutable no cambio respecto a Milestone 17.
- Puerta cerrada sin palanca: `force_door` bloqueada por item tags faltantes.
- Puerta cerrada con palanca: `force_door` disponible.
- Puerta forzada: `force_door` bloqueada por falta de `locked_door` y snapshot muestra `forced_open`.
- Contenedor sellado: `pry_open_container` disponible.
- Contenedor abierto: `search_container` disponible.
- Contenedor looteado: `search_container` bloqueada por falta de `lootable_container` y snapshot muestra `looted_container`.
- `GameplayFeedbackLog` sigue separado y funcionando.
- `DebugFeedbackLogPanel` se muestra/oculta con F7.
- `DebugActionAvailabilityPanel` se muestra/oculta con F8.
- `InventoryDebugPanel` sigue funcionando con `I`.
- Los paneles arrancan ocultos por defecto.
- El diagnostico es runtime-only, debug/fundacional, sin EventBus, sin listeners, sin subscriptions, sin callbacks, sin payload generico, sin UI final y sin persistencia.
- No se toco JSON, loaders, database, validator, `GameplayFeedbackLog` base, combate, IA, save system, journal, quest log ni UI final.

### Milestone 19.1: Debug State Color Readability

Estado: `validated`.

Validado en Unity:

- `WorldObjectStateView` ahora soporta color debug por regla visual usando `MaterialPropertyBlock`.
- Los colores reflejan runtime tags sin modificar gameplay ni materiales compartidos.
- Puerta cambia de rojo oscuro a verde tras `force_door`.
- Contenedor cambia de naranja a cian tras `pry_open_container` y luego a gris oscuro tras `search_container`.
- Palanca se oculta tras `pick_up_item`.
- Data load sigue OK con 0 errors y 0 warnings.
- F7, F8 e I siguen funcionando.
- El menu contextual sigue mostrando solo acciones disponibles.
- No se toco JSON, `InteractionSystem`, `ActionAvailabilityEvaluator`, `GameplayFeedbackLog` ni diagnostics.

### Milestone 19.2: Stable Color-Only State Visuals

Estado: `validated`.

Validado en Unity:

- `SampleScene` fue ajustada para que puerta y contenedor mantengan geometria estable.
- Se neutralizaron rotaciones, cambios de variante visual y deformaciones raras.
- Puerta y contenedor ahora comunican estados solo por color debug.
- Puerta inicia roja y tras `force_door` cambia a verde sin rotar ni moverse.
- Contenedor inicia naranja, tras `pry_open_container` cambia a cian, y tras `search_container` cambia a gris oscuro sin cambiar geometria.
- La palanca sigue ocultandose con `SetActive` cuando tiene `picked_up`.
- Data load sigue OK con 0 errors y 0 warnings.
- F7, F8 e I siguen funcionando.
- El menu contextual sigue mostrando solo acciones disponibles.
- No se toco codigo, JSON ni gameplay.

### Milestone 20: Item Storage / Container Foundation v0

Estado: `validated`.

Validado en Unity:

- El proyecto compila sin errores.
- Se creo una base comun runtime-only para almacenamiento de items mediante `ItemStorage` e `ItemStorageEntry`.
- `ItemStorage` es clase C# pura, no `MonoBehaviour`.
- `ItemStorageEntry` representa `ItemInstance` + `Quantity`.
- `Quantity` no fue agregado a `ItemInstance`.
- En Milestone 20 todavia no habia auto-merge por `DefinitionId`; Milestone 22.1 agrego merge simple controlado por `max_stack`.
- `InventoryComponent` ahora usa `ItemStorage` internamente sin romper el flujo existente.
- `pick_up_item` sigue agregando `rusted_crowbar_01` al inventario.
- `InventoryDebugPanel` con I sigue funcionando.
- `InventoryDebugPanel` muestra cantidades simples cuando `Quantity > 1`.
- Equipar crowbar sigue funcionando.
- `ContainerLootComponent` ahora inicializa storage interno una sola vez desde su loot table antes de que el contenedor sea accesible.
- `sealed_container` no permite `search_container`.
- `pry_open_container` habilita `search_container`.
- `search_container` transfiere contenido existente del contenedor al inventario en lugar de generar loot al buscar.
- El contenedor queda `looted_container` y no vuelve a entregar loot.
- Data load sigue OK con 0 errors y 0 warnings.
- F7, F8 e I siguen funcionando.
- El menu contextual sigue mostrando solo acciones disponibles.
- No se toco JSON, schema, `InteractionSystem`, `ActionAvailabilityEvaluator`, diagnostics ni `GameplayFeedbackLog` base.
- No se agrego UI final, peso, slots, grid, save system ni contenedores anidados.

### Milestone 21: Stateful Inspection & Container Access v0

Estado: `validated`.

Validado en Unity:

- El proyecto compila sin errores.
- Data load sigue OK con 0 errors y 0 warnings.
- Se agrego inspeccion dependiente de `RuntimeTags`.
- `WorldObjectDebugInfo` puede elegir textos condicionales por `requiredTags`, `forbiddenTags` y prioridad.
- `WorldObjectDebugInfo` mantiene `displayName` e `inspectText` como fallback.
- `DebugActionExecutor` usa esos textos al ejecutar `examine_object`.
- Examinar puerta cerrada muestra texto `locked_door`.
- Tras `force_door`, examinar puerta muestra texto `forced_open`.
- Examinar contenedor sellado muestra texto `sealed_container` y `[DEBUG STORAGE]`.
- Tras `pry_open_container`, `search_container` aparece correctamente.
- `search_container` transfiere contenido existente.
- Tras `search_container`, examinar contenedor muestra `looted_container`.
- El contenedor no entrega loot dos veces.
- `InventoryDebugPanel` con I sigue funcionando.
- F7, F8 e I siguen funcionando.
- El menu contextual sigue mostrando solo acciones disponibles.
- No se toco JSON ni schema.
- No se toco `InteractionSystem`.
- No se toco `ActionAvailabilityEvaluator`.
- No se tocaron diagnostics.
- No se toco `GameplayFeedbackLog`.
- No se creo UI final de contenedor, peso, slots, grid, split/merge, save system ni contenedores anidados.

Piezas validadas:

- StateAwareInspectionText v1.
- ContainerInspectionDebugStorageReadability v0.
- ContainerAccessRules v0.

### Milestone 21.0.1: Hotfix - State-Aware Inspection Selection

Estado: `validated`.

Validado en Unity:

- La seleccion de texto condicional usa `RuntimeTags` reales.
- La puerta forzada ya no muestra el texto de puerta trabada.
- Puerta `locked_door` requiere `locked_door` y bloquea `forced_open`.
- Puerta `forced_open` requiere `forced_open`, bloquea `locked_door` y tiene mayor prioridad.
- El contenedor mantiene `looted_container` con prioridad mas alta.
- `opened_container` + `lootable_container` mantiene `forbiddenTags: looted_container`.
- `sealed_container` requiere `sealed_container`.
- No se toco JSON, `InteractionSystem`, `ActionAvailabilityEvaluator`, diagnostics, `GameplayFeedbackLog`, `ItemStorage`, `ItemStorageEntry` ni `InventoryComponent`.

### Milestone 22: Actor Needs & Debug Supply Containers v0

Estado: `validated`.

Validado en Unity:

- `ActorNeedsComponent` generico para actores con hunger/thirst runtime.
- Solo actores con `ActorNeedsComponent` tienen necesidades.
- Hunger/Thirst decaen en Play Mode.
- `water_bottle_01` restaura `thirst`.
- `food_ration_01` restaura `hunger`.
- Consumibles definidos por JSON cerrado con `consumable.restore_needs`.
- `InventoryDebugPanel` permite usar consumibles mediante `InventoryItemUseService`.
- El uso consume cantidad solo si se aplica un efecto valido.
- Food/Water Debug Crate y Misc Debug Crate usan loot tables y storage interno.
- Data load sigue OK con 0 errors y 0 warnings.
- F7, F8, I, palanca, puerta y caja original siguen funcionando.
- No se creo UI final, cocina, heridas, enfermedad, temperatura, descanso, IA, combate ni save system.

### Milestone 22.1: Survival UI, Action Feedback & Manual Container Loot v0

Estado: `validated`.

Validado en Unity:

- UI debug fija arriba a la izquierda muestra Hunger/Thirst.
- Consumir agua/comida registra `ItemUsed` en `GameplayFeedbackLog`.
- `max_stack` se agrego a `ItemDefinition`/JSON.
- `max_stack = 1` significa no stackeable.
- `max_stack > 1` permite merge simple en `ItemStorage`.
- `Scrap Metal x1 + Scrap Metal x500` queda como `Scrap Metal x501`.
- Cajas debug nuevas usan cantidades x500.
- `search_container` abre `ItemStorageDebugPanel` y no transfiere todo automaticamente.
- `ItemStorageDebugPanel` muestra `Take 1`, `Take Stack`, `Take All` y `Close`.
- `looted_container` solo se aplica cuando el storage queda vacio.
- Cajas debug nuevas cambian color con `WorldObjectStateView`: cian con loot, gris/negro vacias.
- No se creo inventario final, drag/drop, peso, save system, comercio, IA ni combate.

### Milestone 22.1.1: Hotfix - Wire Survival and Storage Debug UI

Estado: `validated`.

Validado en Unity:

- `ActorNeedsDebugPanel` esta presente en `SampleScene`, visible arriba a la izquierda y conectado al Debug Player.
- `ActorNeedsDebugPanel` autoresuelve `ActorNeedsComponent` de forma segura.
- `ItemStorageDebugPanel` existe en `SampleScene`.
- `search_container` encuentra o crea `ItemStorageDebugPanel` por fallback seguro.
- El panel de storage abre contenido de contenedores validos.
- Aparecen `Take 1`, `Take Stack`, `Take All` y `Close`.
- `DebugWorldUiInputBlocker` bloquea clicks sobre paneles debug para no mover ni disparar acciones detras.
- Food/Water Debug Crate y Misc Debug Crate reflejan estados con `WorldObjectStateView`.

### Milestone 22.1.2: Hotfix - Equippable Item Flag

Estado: `validated`.

Validado en Unity:

- `equippable` se agrego como boolean en `ItemDefinition`/JSON.
- `equippable` no es tag y no usa strings `yes/no`.
- `rusted_crowbar_01` tiene `equippable: true`.
- `scrap_metal_01`, `water_bottle_01` y `food_ration_01` tienen `equippable: false`.
- `InventoryDebugPanel` solo muestra `Equip` si `itemDefinition.equippable == true`.
- La palanca sigue equipable.
- Agua/comida no se pueden equipar, pero si usar.
- Cantidades, stacking, `Use` e `ItemStorageDebugPanel` siguen funcionando.
- Data load sigue OK con 0 errors y 0 warnings.

### Milestone 23: Actor Inventory Foundation v0

Estado: `validated`.

Validado en Unity:

- `InventoryComponent` separa conceptualmente Storage y Equipped.
- `right_hand` es el primer slot runtime funcional.
- `right_hand` usa `rightHandItemInstanceId`, no indice.
- El item equipado sigue existiendo dentro de `ItemStorage`.
- `rusted_crowbar_01` se equipa solo en `right_hand` segun JSON.
- `equip.equippable`, `equip.allowed_slots` y `equip.occupied_slots` estan soportados.
- `equippable` plano queda como compatibilidad temporal.
- `DataValidator` detecta contradicciones entre `equippable` plano y `equip.equippable`.
- `InventoryComponent` valida internamente si un item puede equiparse.
- `InventoryDebugPanel` muestra Equipped separado de Storage.
- Agua/comida/scrap no se pueden equipar.
- Agua/comida siguen mostrando `Use`.
- `InteractionSystem` sigue detectando la palanca equipada y habilita `force_door` / `pry_open_container`.
- Loot final validado: Scrap x501, Water x500, Food x500, Crowbar x1.
- Data load sigue OK con 0 errors y 0 warnings.
- F7, F8, I, Hunger/Thirst, `GameplayFeedbackLog`, `ItemStorageDebugPanel` y loot de cajas siguen funcionando.

### Milestone 23.0.1: Hotfix - Cleanup legacy equipped index warning

Estado: `validated`.

Validado en Unity:

- Se elimino el warning CS0414 del indice legacy de equip en `InventoryComponent`.
- `rightHandItemInstanceId` sigue siendo la fuente real del equipamiento.
- `GetEquippedItemDefinitionId()` sigue devolviendo el item equipado en `right_hand`.

### Milestone 23.0.2: Hotfix - Revalidate Action Requirements Before Execution

Estado: `validated`.

Validado en Unity:

- `ContextualActionDebugPanel` revalida la accion antes de iniciar `DebugActionProgressController`.
- La revalidacion usa el flujo existente de `InteractionSystem` y disponibilidad de acciones.
- Si la accion ya no esta disponible, no inicia progreso y muestra feedback debug.
- Cubre `force_door` y `pry_open_container` si la palanca fue desequipada despues de abrir el menu.

### Milestone 23.0.3: Hotfix - Refresh Context Menu Availability

Estado: `validated`.

Validado en Unity:

- Mientras `ContextualActionDebugPanel` esta abierto, compara el item equipado actual con el ultimo observado.
- Si el item equipado cambia, refresca acciones disponibles con `InteractionSystem.GetAvailableActions`.
- La linea `Item` cambia entre `rusted_crowbar_01` y `(none)`.
- `force_door` / `pry_open_container` desaparecen al desequipar y vuelven al reequipar si el target sigue valido.
- La revalidacion de M23.0.2 sigue protegiendo antes de ejecutar.

### Milestone 23.1: Lootable Debug Actor + Health v0

Estado: `validated`.

Validado en Unity:

- `ActorHealthComponent` v0 funciona para Player y Debug NPC Capsule.
- Health usa max/current health, low health threshold y estados runtime.
- Estados validados: `alive_actor`, `damaged_actor`, `low_health_actor`, `dead_actor` y `lootable_actor`.
- Health no pinta colores directamente; actualiza tags runtime y `WorldObjectStateView` representa estados.
- Player y NPC se ven verdes vivos; low health se ve rojo; actor muerto se ve negro.
- Debug NPC Capsule puede recibir dano por accion debug contextual.
- Debug NPC muerto agrega `dead_actor + lootable_actor` si tiene inventario.
- `search_body` aparece solo con `dead_actor + lootable_actor`.
- `search_body` abre `ItemStorageDebugPanel` reutilizado mediante fuente reusable de storage.
- El cadaver no usa `ContainerLootComponent`.
- Loot del cuerpo transfiere item instances al inventario del player.
- Al vaciar el cuerpo, se remueve `lootable_actor` y `search_body` desaparece, manteniendo `dead_actor`.
- `DebugActorInventorySeeder` existe solo como componente debug, no como sistema de perfiles de NPC.
- `bandage_01` es consumible medico simple, no equipable, con `restore_health.amount = 25`.
- Bandage cura al Player y consume 1 solo si restaura health; full health no consume.
- Survival Supply Debug Crate mantiene el loot table ID existente y contiene Water Bottle x500, Food Ration x500 y Bandage x500.
- Agua/comida siguen restaurando Hunger/Thirst.
- Cajas normales siguen usando `ContainerLootComponent`.
- `ItemStorageDebugPanel` sigue funcionando con cajas y actor muerto.
- M23 sigue funcionando: `right_hand`, crowbar, `force_door`, `pry_open_container`, revalidacion de acciones y refresh del menu contextual.
- Data load sigue OK con 0 errors y 0 warnings.
- F7, F8, I, Hunger/Thirst, `GameplayFeedbackLog`, `ItemStorageDebugPanel` y loot manual siguen funcionando.

### Milestone 23.1.1: Hotfix - Health Examine Texts + Player Debug Damage

Estado: `validated`.

Validado en Unity:

- `damaged_actor` se agrega cuando currentHealth < maxHealth y el actor sigue vivo.
- Full health vivo usa `alive_actor`.
- Danado vivo usa `alive_actor + damaged_actor`.
- Baja salud vivo usa `alive_actor + damaged_actor + low_health_actor`.
- Muerto NPC usa `dead_actor + lootable_actor` si tiene loot, o `dead_actor` si fue vaciado.
- Player puede recibir dano por boton debug en `ActorNeedsDebugPanel`.
- Player en 0 health es solo estado debug visual/numerico: sin muerte real, game over, bloqueo de movimiento/acciones ni `lootable_actor`.
- `ActorNeedsDebugPanel` muestra Hunger, Thirst, Health y boton `Debug Damage Player`.
- Debug NPC Capsule muestra textos de examinar distintos segun estado: full health, danado, low health y muerto.

### Milestone 23.1.2: Hotfix - Debug Player Health Feedback

Estado: `validated`.

Validado en Unity:

- `Debug Damage Player` registra una entrada `Info` en `GameplayFeedbackLog`.
- La entrada incluye actor/player, dano aplicado y health antes/despues.
- El boton sigue danando al Player.
- Player sigue cambiando de color por estado de salud.
- Bandage sigue curando.
- NPC damage y `search_body` siguen funcionando igual.

### Post-M23.1 Functional Audit / Cleanup Pass

Estado: `validated`.

Validado en Unity:

- Cleanup Pass 1 elimino scripts debug/legacy no usados y sus `.meta`: `GameDataDebugTester`, `ActionAvailabilityDebugTester` y `DebugPlayerContext`.
- Cleanup Pass 2 elimino de `SampleScene` el GameObject inactivo `Deprecated_ActorInteractionContext_Legacy`.
- Debug Player conserva el `ActorInteractionContext` activo usado por `WorldInteractionDebugTester`.
- Cleanup Pass 3 agrego `ActionEffectTypes` y centralizo `add_tag`, `remove_tag`, `show_target_info`, `pick_up_item`, `search_container`, `apply_damage`, `kill_actor` y `search_actor_inventory`.
- `DataValidator` y `DebugActionExecutor` usan las mismas constantes de effect type.
- No se cambio JSON, actions, effects ni semantica de ejecucion.
- Data Load OK con 0 errors y 0 warnings.
- Siguen funcionando: Crowbar pickup, `right_hand`, `force_door`, `pry_open_container`, `search_container`, `debug_damage_actor`, `low_health_actor`, `dead_actor + lootable_actor`, `search_body` y bandage.
- El warning de Unity.AI.Toolkit Account API no pertenece a Old Scars.

### Milestone 24: Actor Profile Pipeline v0

Estado: `validated`.

Validado en Unity:

- M24.1 agrego `ActorProfileDefinition`, `actor_profiles.json`, carga desde `actor_profiles/*.json`, registro y consulta en `GameDatabase`.
- M24.2 agrego validacion fuerte de type, id, display name, initial tags, health e initial inventory, y rechaza `equipped` no soportado.
- M24.3 agrego `ActorProfileComponent` para aplicar una sola vez display name, initial tags, health e initial inventory sobre componentes existentes.
- M24.4 migro Debug NPC Capsule a `actorProfileId = debug_npc_capsule_01` y retiro `DebugActorInventorySeeder` de ese actor.
- Debug NPC Capsule recibe `bandage_01 x3` y `scrap_metal_01 x2` desde `actor_profiles.json`.
- No se duplico inventario.
- `DebugActorInventorySeeder.cs` sigue existiendo como candidato legacy/debug para futura limpieza controlada.
- Data Load OK con 0 errors, 0 warnings y `ActorProfiles: 1`.
- Siguen funcionando `pick_up_item`, `right_hand`, `force_door`, `pry_open_container`, `search_container`, `debug_damage_actor`, `low_health_actor`, `dead_actor`, `lootable_actor` y `search_body`.

## Sistemas Activos

### Data Layer

- JSON define contenido.
- C# ejecuta logica.
- `GameDataLoader` carga `StreamingAssets/Mods/Core`, incluyendo `actor_profiles/*.json`.
- `GameDatabase` expone definiciones cargadas, incluyendo Actor Profiles por ID.
- `DataValidator` valida IDs, types, tags, referencias, effects, loot tables, Actor Profiles, `max_stack`, consumibles, datos `equip` y warnings no destructivos de `weapon_tags`.
- `ActionEffectTypes` centraliza constantes de effect types cerrados compartidas por `DataValidator` y `DebugActionExecutor`.

### Actor Profile Runtime

- `ActorProfileDefinition` define display name, initial tags, health e initial inventory.
- `ActorProfileComponent` consulta el perfil por ID y lo aplica una sola vez sobre componentes existentes.
- El componente no auto-crea componentes faltantes; registra warning/error y omite esa parte del perfil.
- Los health runtime tags siguen siendo responsabilidad de `ActorHealthComponent` y no se declaran en Actor Profiles.
- `equipped` / `right_hand` no forman parte del schema validado de M24.
- Debug NPC Capsule usa `actorProfileId = debug_npc_capsule_01`.
- `DebugActorInventorySeeder` ya no esta conectado a Debug NPC Capsule; el script queda como candidato legacy/debug.

### Interaction System

- `ActorInteractionContext` aporta actor tags, actor stats y equipped item definition id actual.
- Si `InventoryComponent` existe, `ActorInteractionContext` usa exclusivamente esa fuente aunque no haya item equipado.
- Si no hay `InventoryComponent` y `DebugInventory` esta asignado, `ActorInteractionContext` usa `DebugInventory` aunque no haya item equipado.
- Si no hay `InventoryComponent` ni `DebugInventory`, `ActorInteractionContext` usa `equippedItemDefinitionId` legacy.
- `InteractionSystem` recibe `InteractionQuery`.
- `InteractionSystem` resuelve item equipado, maneja `none`/vacio y evalua acciones por contexto.
- `InteractionSystem` puede loguear detalles de disponibilidad si `InteractionQuery.LogAvailabilityDetails` esta activo.
- `InteractionSystem` puede producir un reporte diagnostico estructurado de disponibilidad sin conocer UI.
- `ActionAvailabilityEvaluator` evalua actor tags, target tags, item tags y actor_min_stats.
- `ActionAvailabilityEvaluator.Evaluate()` devuelve `ActionAvailabilityResult` explicable.
- `ActionAvailabilityDiagnosticReport` y `ActionAvailabilityDiagnosticEntry` copian snapshots/datos derivados de la misma evaluacion.
- `requirements.weapon_tags` sigue siendo el campo activo para required equipped item tags.

### Debug Item Runtime

- `ItemInstance` representa una instancia runtime minima de un item definition.
- `ItemInstance` guarda `InstanceId`, `DefinitionId` y `Condition`.
- `ItemStorage` es una clase C# pura runtime-only para almacenar entries de items con cantidades simples.
- `ItemStorage` permite resolver entries por `ItemInstance.InstanceId` para referencias seguras desde Equipped.
- `ItemStorage` mergea stacks por mismo `definitionId` hasta `max_stack` cuando `max_stack > 1`.
- `ItemStorageEntry` representa un `ItemInstance` y una `Quantity >= 1`.
- La cantidad pertenece al storage; no se agrego `Quantity` a `ItemInstance`.
- `max_stack` en `ItemDefinition` define el stackeo simple; `max_stack = 1` significa no stackeable.
- `equip.equippable` es la fuente actual de equipabilidad por slot cuando existe el bloque `equip`.
- `equippable` plano en `ItemDefinition` queda como compatibilidad temporal y no debe contradecir `equip.equippable`.
- `equip.allowed_slots` y `equip.occupied_slots` usan IDs tecnicos; en M23 solo `right_hand` esta validado.
- `consumable.restore_needs` define efectos cerrados de consumibles por `need_id` y `amount`.
- `consumable.restore_health.amount` define restauracion cerrada de health.
- `LootTableDefinition` representa una tabla v0 deterministica de loot.
- `InventoryComponent` v0 usa `ItemStorage` internamente como Storage.
- `InventoryComponent` expone Equipped separado conceptualmente, con `right_hand` como primer slot runtime.
- `InventoryComponent` usa `rightHandItemInstanceId` para referenciar el item equipado.
- `InventoryComponent` permite `AddItemByDefinitionId`, `AddItemByDefinitionId` con cantidad simple, equipar/unequipar `right_hand` y compatibilidad con metodos legacy de equip.
- `InventoryComponent` no autoequipa al recoger.
- `InventoryDebugPanel` muestra Equipped separado de Storage, lee entries de storage, muestra cantidades, permite `Use` en consumibles y muestra `Equip` solo si `InventoryComponent` confirma que el item puede equiparse en `right_hand`.
- `InventoryItemUseService` aplica consumibles a `ActorNeedsComponent` / `ActorHealthComponent` y consume cantidad solo si hubo efecto valido.
- `ItemStorageDebugPanel` es panel debug reusable para ver y tomar contenido de storages accesibles.
- `LootableActorInventoryComponent` expone inventario de actor muerto como fuente reusable para `ItemStorageDebugPanel`.
- `DebugActorInventorySeeder` es candidato legacy/debug para limpieza controlada y ya no se usa en Debug NPC Capsule.
- `DebugInventory` crea instancias runtime al iniciar cuando `GameDataManager` esta listo.
- `DebugInventory` no es inventario final, no guarda save data y no implementa equipamiento real.

### World Object Runtime Tags

- `WorldObjectTags` mantiene `tags` serializados como initial tags.
- `RuntimeTags` es la copia mutable durante Play.
- `Tags` sigue siendo alias compatible de runtime tags.
- `HasTag`, `AddTag` y `RemoveTag` operan sobre runtime tags.
- `ResetRuntimeTagsFromInitial` permite reset debug del estado runtime.

### World Object State View

- `WorldObjectStateView` lee `WorldObjectTags.RuntimeTags` y aplica reglas visuales debug.
- `WorldObjectStateView` no modifica tags, gameplay ni disponibilidad de acciones.
- Las reglas pueden activar/desactivar hijos, rotar transforms locales o aplicar color debug.
- El color debug usa `MaterialPropertyBlock`, sin modificar materiales compartidos.
- En `SampleScene`, puerta y contenedor usan geometria estable y comunican estado solo por color.
- La palanca puede seguir ocultando su visual con `SetActive` cuando tiene `picked_up`.

### World Object Debug Info

- `WorldObjectDebugInfo` guarda texto debug de inspeccion.
- `WorldObjectDebugInfo` puede seleccionar texto por `RuntimeTags`.
- Las reglas condicionales usan `requiredTags`, `forbiddenTags` y `priority`.
- Si no hay regla valida, o si un campo condicional esta vacio, se usa el fallback `displayName` / `inspectText`.

### Actor Needs Runtime

- `ActorNeedsComponent` es generico para actores y no exclusivo del jugador.
- Solo los actores con `ActorNeedsComponent` tienen necesidades.
- `ActorNeedProfile` define configuracion serializable de necesidades.
- `ActorNeedState` guarda valores runtime visibles para debug.
- Hunger/Thirst decaen en Play Mode mediante `Time.deltaTime`.
- Las necesidades se restauran con API cerrada como `RestoreNeed`, `TryRestoreNeed`, `GetNeedValue` y `HasNeed`.
- `ActorNeedsDebugPanel` muestra Hunger/Thirst/Health y boton `Debug Damage Player` como UI debug temporal arriba a la izquierda.

### Actor Health Runtime

- `ActorHealthComponent` es generico para Player y NPCs debug.
- Health usa max/current health y `lowHealthThreshold`.
- Estados runtime actuales: `alive_actor`, `damaged_actor`, `low_health_actor`, `dead_actor` y `lootable_actor`.
- `ActorHealthComponent` no pinta colores directamente; `WorldObjectStateView` lee tags y representa estados.
- Player en 0 health es solo debug visual/numerico: sin muerte real, game over, bloqueo de movimiento/acciones ni `lootable_actor`.

### Debug Action Execution

- `DebugActionProgressController` controla acciones debug en progreso.
- Usa `ActionDefinition.cost.time` como duracion debug.
- `DebugActionExecutor` ejecuta effects definidos por JSON.
- Effects soportados:
  - `add_tag`;
  - `remove_tag`;
  - `show_target_info`;
  - `pick_up_item`;
  - `search_container`;
  - `apply_damage`;
  - `kill_actor`;
  - `search_actor_inventory`.
- `add_tag` y `remove_tag` afectan solo al target.
- `show_target_info` lee `WorldObjectDebugInfo`.
- `pick_up_item` ejecuta `WorldItemPickup` sobre el target.
- `search_container` ejecuta `ContainerLootComponent` sobre el target y transfiere contenido existente del storage del contenedor.
- En el flujo actual, `search_container` abre `ItemStorageDebugPanel` para saqueo manual cuando el storage es accesible.
- `search_actor_inventory` abre `ItemStorageDebugPanel` sobre el inventario de actor muerto looteable.
- `apply_damage` y `kill_actor` son acciones debug de health, no combate real.
- `ContainerLootComponent` valida acceso al storage antes de transferir loot.
- Al examinar un contenedor, `[DEBUG STORAGE]` muestra estado runtime del storage como debug/readability.

### Gameplay Feedback Runtime

- `GameplayFeedbackEntryType` define categorias cerradas: `ItemPickedUp`, `ItemEquipped`, `ItemUnequipped`, `ItemUsed`, `ActionCompleted`, `LootReceived`, `TargetStateChanged`, `Info` y `Warning`.
- `GameplayFeedbackEntry` guarda datos estructurados: tipo, mensaje fallback, tiempo, actor, target, item, action, quantity, tags agregados/removidos y `debugOnly`.
- `GameplayFeedbackLog` guarda entradas runtime-only con `maxEntries`, `Record`, `Clear` y `Entries`.
- `GameplayFeedbackLog` es append/read y no persistente.
- `GameplayFeedbackLog` no es EventBus y no tiene listeners, subscriptions, callbacks, dispatch ni payload generico.
- `DebugFeedbackLogPanel` lee `Entries` y las muestra con OnGUI debug.
- `DebugFeedbackLogPanel` arranca oculto por defecto y se muestra/oculta con F7.
- `DebugFeedbackLogPanel` no recibe llamadas desde gameplay y no ejecuta logica de gameplay.
- Si no existe `GameplayFeedbackLog` en escena, el gameplay sigue funcionando sin depender del feedback visual.

### Action Availability Diagnostics Runtime

- `ActionAvailabilityDiagnosticReport` describe la disponibilidad actual de acciones contextuales antes de ejecutarlas.
- `ActionAvailabilityDiagnosticReport` guarda target, contexto requerido, actor tags snapshot, target tags snapshot, item equipado y equipped item tags snapshot.
- `ActionAvailabilityDiagnosticEntry` guarda una entrada por accion candidata del mismo contexto.
- Cada entrada guarda disponibilidad, razones de exito/bloqueo, tags requeridos/faltantes y matched item tags.
- El diagnostico usa `ActionAvailabilityEvaluator.Evaluate()` y `ActionAvailabilityResult`.
- El diagnostico no registra hechos ocurridos; eso pertenece a `GameplayFeedbackLog`.
- El diagnostico es runtime-only, no persistente y sin EventBus, listeners, subscriptions, callbacks ni payload generico.
- `DebugActionAvailabilityPanel` solo muestra el reporte con OnGUI debug.
- `DebugActionAvailabilityPanel` arranca oculto por defecto y se muestra/oculta con F8.

### UI Debug Contextual

- `WorldInteractionDebugTester` coordina input de interaccion, raycast y bridge hacia UI.
- `WorldInteractionDebugTester.logAvailabilityDetails` activa logs detallados opcionales de disponibilidad.
- `ContextualActionDebugPanel` muestra acciones disponibles con OnGUI, revalida antes de ejecutar y refresca el menu si cambia el item equipado.
- `ContextualActionDebugProgressPanel` muestra progreso debug de accion activa.
- `ContextualActionDebugResultPanel` muestra resultados debug.
- `InventoryDebugPanel` muestra inventario v0 con OnGUI y se abre con `I`.
- `ActorNeedsDebugPanel` muestra necesidades/health debug del actor.
- `ItemStorageDebugPanel` muestra contenido de storage y permite `Take 1`, `Take Stack`, `Take All` y `Close`.
- `DebugFeedbackLogPanel` muestra feedback estructurado runtime-only del POI como UI debug y se alterna con F7.
- `DebugActionAvailabilityPanel` muestra diagnostico de disponibilidad runtime-only como UI debug y se alterna con F8.
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
  - `ActorNeedsComponent` para hunger/thirst debug;
  - `ActorHealthComponent` para health debug;
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
  - `ContainerLootComponent.lootTableId = debug_sealed_container_loot_01`;
  - storage runtime inicializado una vez desde su loot table.
- `WorldInteractionDebugTester.logAvailabilityDetails` desactivado por defecto; activarlo solo para auditar requisitos de acciones.
- Camara principal como hija del CameraRig.
- `GameplayFeedbackDebug` bajo `Debug_UI` con `GameplayFeedbackLog` y `DebugFeedbackLogPanel` para leer feedback runtime del POI.
- `ActionAvailabilityDiagnosticsDebug` bajo `Debug_UI` con `DebugActionAvailabilityPanel` para leer diagnostics runtime del POI.
- `ActorNeedsDebugPanel` visible arriba a la izquierda para Hunger/Thirst/Health y dano debug del Player.
- `ItemStorageDebugPanel` disponible para saqueo manual de contenedores accesibles y actor muerto looteable.
- `DebugFeedbackLogPanel` y `DebugActionAvailabilityPanel` arrancan ocultos por defecto.
- Objetos principales del POI pueden usar `WorldObjectStateView` para reflejar runtime tags visualmente sin controlar gameplay.
- Debug NPC Capsule con `ActorProfileComponent.actorProfileId = debug_npc_capsule_01` y sin `DebugActorInventorySeeder`.

## Proximo Recomendado

Preparar Milestone 25: Storage Transfer v0 / Bidirectional Item Transfer. Objetivo futuro: permitir poner items dentro de contenedores y cuerpos usando `ItemStorage` / `InventoryComponent`, sin romper el saqueo actual. No hay implementacion definida todavia.

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

Milestone 17 fue validado con:

- base runtime-only de feedback estructurado;
- `GameplayFeedbackLog` append/read sin listeners, subscriptions, callbacks, dispatch ni payload generico;
- `DebugFeedbackLogPanel` leyendo entradas del log sin acoplar gameplay a UI;
- entradas `ItemPickedUp`, `ItemEquipped`, `ItemUnequipped`, `ActionCompleted`, `LootReceived` y `TargetStateChanged`;
- POI sigue funcionando: puerta `forced_open`, contenedor `looted_container`, `search_container` desaparece despues de saquear;
- `InteractionSystem` no fue tocado;
- no se creo journal, quest log, UI final, save system, EventBus ni sistemas grandes.

Milestone 18 fue validado con:

- diagnostico opcional de disponibilidad de acciones contextuales;
- `ActionAvailabilityDiagnosticReport` y `ActionAvailabilityDiagnosticEntry`;
- diagnostico basado en `ActionAvailabilityEvaluator.Evaluate()` y `ActionAvailabilityResult`;
- `GetAvailableActions()` sigue devolviendo solo acciones disponibles;
- menu contextual ejecutable sin cambios respecto a Milestone 17;
- acciones disponibles y bloqueadas visibles en `DebugActionAvailabilityPanel`;
- razones de bloqueo y snapshots de contexto visibles;
- `force_door`, `pry_open_container` y `search_container` diagnostican correctamente sus requisitos segun estado runtime;
- `GameplayFeedbackLog` sigue separado y funcionando;
- F7 alterna `DebugFeedbackLogPanel`;
- F8 alterna `DebugActionAvailabilityPanel`;
- `I` sigue alternando `InventoryDebugPanel`;
- paneles debug ocultos por defecto;
- no se toco JSON, loaders, database, validator, `GameplayFeedbackLog` base, combate, IA, save system, journal, quest log ni UI final.

Milestone 19.1 fue validado con:

- soporte de color debug por regla visual en `WorldObjectStateView`;
- uso de `MaterialPropertyBlock`;
- colores reflejando runtime tags sin modificar gameplay ni materiales compartidos;
- puerta rojo oscuro -> verde tras `force_door`;
- contenedor naranja -> cian -> gris oscuro durante el loop;
- palanca oculta tras `pick_up_item`;
- F7, F8 e I funcionando;
- menu contextual mostrando solo acciones disponibles;
- sin tocar JSON, `InteractionSystem`, `ActionAvailabilityEvaluator`, `GameplayFeedbackLog` ni diagnostics.

Milestone 19.2 fue validado con:

- geometria estable para puerta y contenedor en `SampleScene`;
- rotaciones, variantes visuales y deformaciones raras neutralizadas;
- estados de puerta y contenedor comunicados solo por color debug;
- palanca ocultandose con `SetActive` cuando tiene `picked_up`;
- data load OK con 0 errors y 0 warnings;
- F7, F8 e I funcionando;
- menu contextual mostrando solo acciones disponibles;
- sin tocar codigo, JSON ni gameplay.

Milestone 20 fue validado con:

- base comun runtime-only de storage mediante `ItemStorage` e `ItemStorageEntry`;
- `InventoryComponent` apoyado internamente en `ItemStorage` sin romper pickup, equip ni panel debug;
- `ContainerLootComponent` inicializando storage interno una vez desde su loot table;
- `search_container` transfiriendo contenido existente al inventario en vez de generar loot al buscar;
- cantidades simples visibles en `InventoryDebugPanel` cuando `Quantity > 1`;
- contenedor quedando `looted_container` y sin entregar loot dos veces;
- data load OK con 0 errors y 0 warnings;
- F7, F8 e I funcionando;
- menu contextual mostrando solo acciones disponibles;
- sin tocar JSON, schema, `InteractionSystem`, `ActionAvailabilityEvaluator`, diagnostics ni `GameplayFeedbackLog` base;
- sin UI final, peso, slots, grid, save system ni contenedores anidados.

Milestone 21 fue validado con:

- inspeccion dependiente de `RuntimeTags`;
- textos condicionales en `WorldObjectDebugInfo` por `requiredTags`, `forbiddenTags` y prioridad;
- `DebugActionExecutor` usando esos textos al ejecutar `examine_object`;
- resumen debug `[DEBUG STORAGE]` al examinar contenedores;
- `ContainerLootComponent` validando acceso antes de transferir loot;
- puerta cerrada mostrando texto `locked_door`;
- puerta forzada mostrando texto `forced_open`;
- contenedor sellado mostrando texto `sealed_container` + `[DEBUG STORAGE]`;
- contenedor abierto habilitando `search_container`;
- contenedor saqueado mostrando texto `looted_container` y sin entregar loot dos veces;
- F7, F8 e I funcionando;
- menu contextual mostrando solo acciones disponibles;
- sin tocar JSON, schema, `InteractionSystem`, `ActionAvailabilityEvaluator`, diagnostics ni `GameplayFeedbackLog`.

Milestone 21.0.1 fue validado con:

- seleccion condicional usando `RuntimeTags` reales;
- reglas de puerta mutuamente excluyentes;
- puerta `forced_open` con prioridad mayor que `locked_door`;
- la puerta forzada ya no muestra el texto de puerta trabada;
- no se toco JSON, `InteractionSystem`, `ActionAvailabilityEvaluator`, diagnostics, `GameplayFeedbackLog`, `ItemStorage`, `ItemStorageEntry` ni `InventoryComponent`.

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
- actor inventory final;
- cadaveres lootables finales;
- loot final o avanzado;
- save system;
- journal;
- quest log;
- EventBus;
- combate real;
- IA;
- dialogos complejos;
- UI final;
- sistema de animaciones final.

## Deuda Tecnica Menor

- No hay deuda tecnica menor bloqueante registrada.
- No hay deuda tecnica menor bloqueante registrada despues de Milestone 13.
