# Old Scars - Next Sprints

Este documento funciona como backlog ordenado de sprints recomendados. La fuente principal del roadmap vivo es `Docs/Project_Roadmap.md`.

## Proximo Recomendado

### Preparar Siguiente Sprint

Estado actual:

- Milestone 19.2 esta `validated`.
- `SampleScene` funciona como primer POI jugable compacto tipo pequeno taller / bahia de mantenimiento industrial.
- El POI usa sistemas validados: movimiento point-and-click, camara debug, inventario v0, pickup, equip simple, acciones con duracion, runtime tags, loot tables v0, container loot v0 y feedback runtime-only.
- El loop completo funciona: recoger palanca -> equipar -> abrir/forzar obstaculo -> abrir contenedor -> buscar loot -> obtener Scrap Metal -> dejar estados runtime correctos.
- El POI ahora tiene una base runtime-only de feedback estructurado mediante `GameplayFeedbackLog` y `DebugFeedbackLogPanel`.
- El feedback registra `ItemPickedUp`, `ItemEquipped`, `ItemUnequipped`, `ActionCompleted`, `LootReceived` y `TargetStateChanged`.
- El POI ahora tiene diagnostico runtime-only de disponibilidad mediante `ActionAvailabilityDiagnosticReport`, `ActionAvailabilityDiagnosticEntry` y `DebugActionAvailabilityPanel`.
- El diagnostico muestra acciones disponibles/bloqueadas, razones de bloqueo y snapshots de contexto sin cambiar `GetAvailableActions()`.
- `DebugFeedbackLogPanel` se alterna con F7, `DebugActionAvailabilityPanel` con F8 e `InventoryDebugPanel` con `I`.
- Los paneles debug arrancan ocultos por defecto.
- `WorldObjectStateView` soporta color debug por regla visual usando `MaterialPropertyBlock`.
- Puerta y contenedor comunican estados runtime por color debug estable, sin rotaciones ni cambios de geometria.
- La palanca sigue ocultandose con `SetActive` cuando tiene `picked_up`.
- Data load sigue OK con 0 errors y 0 warnings.
- `InteractionSystem` sigue desacoplado.
- No se toco JSON.
- No se crearon actions nuevas ni effects nuevos.
- No se creo journal, quest log, UI final, save system, EventBus, listeners/subscriptions/callbacks ni sistemas grandes.
- No se rompieron `InventoryComponent`, `WorldItemPickup`, `ContainerLootComponent`, action duration, runtime tags ni loot tables.

Proxima accion recomendada:

- Milestone 20 queda como proximo milestone pendiente, sin definir todavia.

Base validada:

- Milestone 11: Action Duration / Action In Progress esta `validated`.
- `ActionDefinition.cost.time` se usa como duracion debug.
- `DebugActionProgressController` maneja accion activa, duracion, elapsed/progress y finalizacion.
- `ContextualActionDebugPanel` inicia progreso en vez de ejecutar actions directamente.
- `DebugActionExecutor` sigue siendo sincronico y aplica effects al terminar.
- Durante accion activa no se puede iniciar otra accion, no se abre otro menu y no se aceptan nuevos clicks de movimiento.
- La camara sigue libre.
- No se toco JSON ni se crearon inventario, loot, save system, combate, IA, animaciones finales ni UI final.

Milestone 12 validado:

- Milestone 12: Item Instances + Debug Inventory esta `validated`.
- `ItemInstance` runtime-only funciona.
- `DebugInventory` crea instancias runtime desde `ItemDefinition`.
- Con `rusted_crowbar_01` equipado aparecen `force_door` y `pry_open_container`.
- Con `equippedItemIndex = -1` aparece `Equipped item: (none)` y no se muestran acciones de herramienta.
- `DebugInventory`, si esta asignado, manda sobre el fallback legacy.
- `equippedItemDefinitionId` legacy solo se usa si no hay `DebugInventory`.
- `InteractionSystem` sigue recibiendo solo definition_id y no depende de `DebugInventory` ni `ItemInstance`.
- Milestone 11 sigue funcionando: duracion de acciones y runtime tags siguen correctos.
- No se creo inventario final, UI, loot, pickup/drop, save system ni equipment system final.

Milestone 12.1 validado:

- Milestone 12.1: Technical Cleanup esta `validated`.
- `GameDataManager` quedo como root GameObject.
- El warning de `DontDestroyOnLoad` ya no aparece.
- `CoreDataSystem` carga correctamente.
- `DebugInventory` quedo verificado en `Debug Player`.
- `Deprecated_ActorInteractionContext_Legacy` quedo desactivado y aislado.
- Scripts legacy documentados como deprecated/legacy; no fueron borrados.
- `_Recovery` se mantiene y no fue borrado.
- No se tocaron codigo ni JSON.
- Movimiento, camara, UI blocker, action duration, runtime tags, DebugInventory e InteractionSystem siguen funcionando.

Milestone 13 validado:

- Milestone 13: Tool Requirement Hardening esta `validated`.
- `requirements.weapon_tags` se mantiene como campo activo.
- `weapon_tags` queda documentado como nombre legacy compatible para required equipped item tags.
- No se agrego `required_item_tags` ni se migro schema.
- No se tocaron `actions.json` ni `items.json`.
- No se toco JSON.
- No se cambio la semantica OR de `weapon_tags`.
- `ActionAvailabilityEvaluator.Evaluate()` agrega evaluacion explicable.
- `ActionAvailabilityEvaluator.IsAvailable()` se mantiene compatible.
- `InteractionSystem` usa `Evaluate()` internamente y sigue devolviendo solo acciones disponibles.
- Logs detallados son opcionales mediante `WorldInteractionDebugTester.logAvailabilityDetails`.
- `DataValidator` agrega warning no destructivo si un `weapon_tags` valido no aparece en ningun item cargado.
- `DataValidator` no bloquea la carga.
- Con palanca equipada, `force_door` y `pry_open_container` aparecen correctamente.
- Sin item equipado, las acciones de herramienta se bloquean correctamente.
- Action duration y runtime tags siguen funcionando.
- No se tocaron DebugInventory, ItemInstance, action duration ni runtime tags.

Milestone 14 validado:

- `InventoryComponent` v0 runtime-only en `Debug Player`.
- El jugador arranca sin item equipado.
- `ActorInteractionContext` prioriza `InventoryComponent` sobre `DebugInventory` y legacy string.
- Si `InventoryComponent` existe y no tiene item equipado, se considera sin item.
- `InventoryDebugPanel` abre con `I`, muestra items y permite equipar/unequip.
- `WorldItemPickup` permite recoger una palanca del mundo.
- `pick_up_item` dura 0.5s y crea una `ItemInstance` runtime.
- El objeto recogido agrega `picked_up`, pierde `pickupable` y deja de ser interactuable.
- Al recoger, el item se agrega al `InventoryComponent`.
- Al equipar la palanca, `force_door` y `pry_open_container` aparecen correctamente.
- `InteractionSystem` sigue sin depender de inventario, UI, pickup, `DebugInventory`, `ItemInstance` ni MonoBehaviour.
- Action duration y runtime tags siguen funcionando.
- `items.json` no fue modificado.
- No se creo inventario final, drag/drop, grid, peso/capacidad, save system, loot aleatorio, contenedores reales, pickup/drop generico, slots reales, UI final, combate ni IA.

Milestone 15 validado:

- Milestone 15: Container Loot v0 esta `validated`.
- `LootTableDefinition` v0 funciona.
- `GameDataLoader` carga `loot_tables/*.json`.
- `GameDatabase` registra y expone loot tables.
- `DataValidator` valida loot tables sin errores.
- `container_loot.json` carga `debug_sealed_container_loot_01`.
- `ContainerLootComponent` saquea contenedores usando `DebugActionExecutionContext`.
- `search_container` aparece solo con `opened_container` + `lootable_container`.
- `search_container` dura 1.5s.
- `debug_sealed_container_loot_01` entrega `scrap_metal_01 x1`.
- Al saquear, `scrap_metal_01` se agrega al `InventoryComponent`.
- `InventoryDebugPanel` muestra `Scrap Metal`.
- Al saquear, `lootable_container` se remueve y `looted_container` se agrega.
- `search_container` ya no aparece despues de saquear.
- `InteractionSystem` sigue sin depender de inventario, loot ni MonoBehaviour.
- No se creo loot avanzado, UI final, save system, stacks, economia, crafting, combate ni IA.

Milestone 17 validado:

- Milestone 17: Gameplay Feedback Log Foundation / POI State Readability v0 esta `validated`.
- `GameplayFeedbackEntryType`, `GameplayFeedbackEntry`, `GameplayFeedbackLog` y `DebugFeedbackLogPanel` funcionan como base runtime-only de feedback.
- `GameplayFeedbackLog` es append/read, no persistente y limitado por `maxEntries`.
- `GameplayFeedbackLog` no tiene listeners, subscriptions, callbacks, dispatch ni payload generico.
- `DebugFeedbackLogPanel` solo lee `Entries` desde `GameplayFeedbackLog`.
- Los sistemas de gameplay registran entradas estructuradas; el panel debug no recibe llamadas directas desde gameplay.
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

Milestone 18 validado:

- Milestone 18: Action Availability Diagnostics / Requirement Readability v0 esta `validated`.
- `ActionAvailabilityDiagnosticReport` y `ActionAvailabilityDiagnosticEntry` funcionan como base runtime-only de diagnostico.
- El diagnostico usa `ActionAvailabilityEvaluator.Evaluate()` y `ActionAvailabilityResult`.
- El diagnostico evalua el mismo conjunto de acciones candidatas que `InteractionSystem` considera antes de filtrar disponibilidad.
- `GetAvailableActions()` sigue devolviendo solo acciones disponibles.
- El menu contextual ejecutable no cambio respecto a Milestone 17.
- `DebugActionAvailabilityPanel` muestra acciones disponibles y bloqueadas, razones de bloqueo y snapshots de contexto.
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

Milestone 19.1 validado:

- Milestone 19.1: Debug State Color Readability esta `validated`.
- `WorldObjectStateView` ahora soporta color debug por regla visual usando `MaterialPropertyBlock`.
- Los colores reflejan runtime tags sin modificar gameplay ni materiales compartidos.
- Puerta cambia de rojo oscuro a verde tras `force_door`.
- Contenedor cambia de naranja a cian tras `pry_open_container` y a gris oscuro tras `search_container`.
- Palanca se oculta tras `pick_up_item`.
- F7, F8 e I siguen funcionando.
- El menu contextual sigue mostrando solo acciones disponibles.
- No se toco JSON, `InteractionSystem`, `ActionAvailabilityEvaluator`, `GameplayFeedbackLog` ni diagnostics.

Milestone 19.2 validado:

- Milestone 19.2: Stable Color-Only State Visuals esta `validated`.
- `SampleScene` fue ajustada para que puerta y contenedor mantengan geometria estable.
- Se neutralizaron rotaciones, cambios de variante visual y deformaciones raras.
- Puerta y contenedor ahora comunican estados solo por color debug.
- La puerta cambia de rojo oscuro a verde tras `force_door` sin rotar ni moverse.
- El contenedor cambia de naranja a cian y luego gris oscuro sin cambiar geometria.
- La palanca sigue ocultandose con `SetActive` cuando tiene `picked_up`.
- Data load sigue OK con 0 errors y 0 warnings.
- F7, F8 e I siguen funcionando.
- El menu contextual sigue mostrando solo acciones disponibles.
- No se toco codigo, JSON ni gameplay.

Pruebas validadas:

- Con `DebugInventory.equippedItemIndex = 0`, puerta muestra `force_door` + `examine_object`, contenedor muestra `pry_open_container` + `examine_object`, maquina muestra `examine_object`.
- Con `DebugInventory.equippedItemIndex = -1`, puerta y contenedor muestran solo `examine_object`, maquina muestra `examine_object`.
- Con `logAvailabilityDetails` activado, la consola explica matches y bloqueos por tags de item equipado.
- Confirmar Milestone 11: duraciones de 3s, 2s y 1s, y runtime tags mutando al finalizar.

Pruebas validadas de Milestone 14:

- Al iniciar, `InventoryComponent` esta vacio y `equippedItemIndex = -1`.
- Puerta solo muestra `examine_object`.
- Contenedor solo muestra `examine_object`.
- Maquina muestra `examine_object`.
- Palanca del mundo muestra `pick_up_item` + `examine_object`.
- `pick_up_item` dura 0.5s.
- Al terminar, la palanca desaparece o queda no interactuable.
- `InventoryComponent` recibe una `ItemInstance` de `rusted_crowbar_01`.
- Con `I`, la palanca aparece en el inventario.
- La palanca no se equipa automaticamente.
- El boton `Equip` equipa la palanca.
- Puerta muestra `force_door` + `examine_object`.
- Contenedor muestra `pry_open_container` + `examine_object`.
- `force_door` y `pry_open_container` siguen respetando duracion.
- Runtime tags siguen funcionando.
- `InteractionSystem` sigue desacoplado del inventario y del pickup.

Pruebas validadas de Milestone 15:

- Iniciar sin item equipado.
- Recoger `rusted_crowbar_01`, abrir inventario con `I` y equiparla.
- Interactuar con `Debug Sealed Container`: debe mostrar `pry_open_container + examine_object`.
- Ejecutar `pry_open_container`: debe durar 2s.
- Confirmar tags: `sealed_container` se remueve, `opened_container` se agrega y `lootable_container` sigue.
- Interactuar de nuevo: debe mostrar `search_container + examine_object`.
- Ejecutar `search_container`: debe durar 1.5s.
- Confirmar feedback textual de `Scrap Metal x1`.
- Confirmar que `scrap_metal_01` aparece en `InventoryComponent` / `InventoryDebugPanel`.
- Confirmar tags: `lootable_container` se remueve y `looted_container` se agrega.
- Interactuar otra vez: `search_container` ya no debe aparecer.
- Confirmar que pickup, action duration, runtime tags, `force_door`, `pry_open_container` e `InteractionSystem` siguen funcionando.

Pruebas validadas de Milestone 17:

- El proyecto compila en Unity sin errores.
- El panel `Gameplay Feedback Log` aparece en `SampleScene`.
- `ItemPickedUp` se registra al recoger la palanca.
- `ItemEquipped` / `ItemUnequipped` se registran al equipar o desequipar.
- `ActionCompleted` se registra en `examine_object`, `force_door`, `pry_open_container` y `search_container`.
- `TargetStateChanged` debug registra cambios runtime de tags.
- `LootReceived` se registra al obtener `scrap_metal_01`.
- `search_container` deja de aparecer despues de saquear.
- El contenedor queda con `looted_container`.
- La puerta queda con `forced_open`.
- `InteractionSystem` no fue tocado.
- El gameplay no depende del panel de feedback.
- No se creo journal, quest log, UI final, save system, EventBus ni sistemas grandes.

Pruebas validadas de Milestone 18:

- El proyecto compila en Unity sin errores.
- `GetAvailableActions()` sigue devolviendo solo acciones disponibles.
- El menu contextual ejecutable no cambio respecto a Milestone 17.
- El diagnostico muestra acciones disponibles y bloqueadas.
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
- No se toco JSON, loaders, database, validator, `GameplayFeedbackLog` base, combate, IA, save system, journal, quest log ni UI final.

## Sprints Posteriores Recomendados

### Tool Requirement Schema Cleanup Futuro

- Evaluar si conviene migrar de `weapon_tags` a `required_item_tags`.
- No hacerlo hasta que haya necesidad clara de schema.
- Mantener compatibilidad con contenido existente.

### Container Loot Debug Hardening Futuro

- Endurecer contenedores debug y loot tables v0 si aparece una necesidad concreta.
- No convertirlo en economia o loot final.

### Knowledge / Learned Tags

- Explorar tags aprendidos/conocidos por actor.
- Mantenerlo chico y compatible con InteractionSystem.
- No crear sistema narrativo complejo todavia.

### Save System Minimo

- Guardar estado minimo cuando existan instancias/runtime state suficientes.
- No empezar con save system avanzado.

### Primer POI Jugable Completo

- Ya fue validado como Milestone 16 en `SampleScene`.
- Mantenerlo como base jugable compacta para pruebas futuras.

### POI Follow-up Futuro

- Milestone 17 ya valido la primera base runtime-only de feedback estructurado para legibilidad del POI.
- Milestone 18 ya valido diagnostico runtime-only de disponibilidad de acciones y lectura de requisitos/bloqueos.
- Milestone 19.1 ya valido color debug por regla visual en `WorldObjectStateView`.
- Milestone 19.2 ya valido estados visuales estables color-only para puerta y contenedor en `SampleScene`.
- Evaluar solo ajustes chicos de legibilidad o feedback debug si una proxima prueba los justifica.
- No convertir el POI en mapa grande ni crear sistemas nuevos para decoracion.

### Milestone 20

- Pendiente.
- Sin definir todavia.

## Pospuestos / No Tocar Todavia

- combate;
- IA;
- facciones;
- journal;
- quest log;
- EventBus de gameplay;
- mapa grande;
- vehiculos;
- crafting completo;
- UI final;
- dialogos complejos;
- procedural world;
- save system avanzado.

## Reglas Para Reordenar Este Backlog

- No adelantar sistemas grandes si contradicen `Docs/Project_Roadmap.md`.
- No reimplementar sistemas ya validados.
- No tocar JSON sin necesidad concreta.
- No convertir JSON en scripting libre.
- Mantener C# como lugar de ejecucion de logica.
