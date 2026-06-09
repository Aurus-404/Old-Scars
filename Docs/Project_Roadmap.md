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
- base runtime-only de feedback de gameplay estructurado validada en `SampleScene`.
- diagnostico runtime-only de disponibilidad de acciones contextuales validado en `SampleScene`.
- lectura visual debug estable de estados runtime por color validada en `SampleScene`.
- base comun runtime-only de storage de items con cantidades simples validada en `SampleScene`.
- inspeccion dependiente de `RuntimeTags` y reglas defensivas de acceso a storage de contenedores validadas en `SampleScene`.
- necesidades runtime genericas de actor, consumibles cerrados por JSON, UI debug de survival y saqueo manual de contenedores validados en `SampleScene`.
- stackeo simple por `max_stack`, cajas debug de suministros y flag funcional `equippable` validados en Unity.
- base de inventario de actor validada: `InventoryComponent` separa conceptualmente Storage y Equipped, con slot runtime `right_hand` basado en `rightHandItemInstanceId`.
- acciones contextuales revalidadas antes de ejecutar y menu contextual debug refrescado cuando cambia el item equipado.
- base de salud runtime de actor validada con estados `alive_actor`, `damaged_actor`, `low_health_actor`, `dead_actor` y `lootable_actor`, lectura visual por `WorldObjectStateView` y loot de actor muerto mediante `ItemStorageDebugPanel`.
- auditoria funcional post-M23.1 y tres cleanup passes validados antes de M24, sin cambios de comportamiento jugable.
- primer pipeline data-driven de Actor Profiles validado: carga, validacion y aplicacion runtime basica sobre actores colocados en escena.
- primer pipeline data-driven de World Object Profiles validado: carga, validacion y aplicacion runtime basica sobre objetos colocados en escena.
- transferencia bidireccional validada entre inventario de actor y storage abierto, conservando instancias en transferencias completas y dividiendo stacks en transferencias parciales.
- `ItemStorageDebugPanel` validado con Player Inventory a la izquierda y Open Storage a la derecha.
- separacion validada entre primera revision de contenedor natural mediante `search_container` y acceso posterior mediante `open_storage`, incluso cuando el storage esta vacio.

Milestone 23, Milestone 23.0.1, Milestone 23.0.2, Milestone 23.0.3, Milestone 23.1, Milestone 23.1.1, Milestone 23.1.2, Functional Audit / Cleanup Pass post-M23.1, Milestone 24 con sus passes M24.1-M24.4, Milestone 25, Milestone 26, Milestone 26.0.1 y Milestone 27 estan validados en Unity.

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
| Milestone 17: Gameplay Feedback Log Foundation / POI State Readability v0 | Agregar una base runtime-only de feedback estructurado para acciones relevantes de gameplay y lectura debug del POI. | validated | Validado: GameplayFeedbackLog registra entradas estructuradas y DebugFeedbackLogPanel las lee sin acoplar gameplay a UI. |
| Milestone 18: Action Availability Diagnostics / Requirement Readability v0 | Agregar diagnostico opcional de disponibilidad de acciones contextuales sin duplicar la logica de evaluacion. | validated | Validado: muestra acciones disponibles/bloqueadas, razones de bloqueo y snapshots de contexto sin cambiar GetAvailableActions ni el menu contextual. |
| Milestone 19.1: Debug State Color Readability | Mejorar la lectura visual de estados del POI usando colores debug por regla visual en WorldObjectStateView. | validated | Validado: WorldObjectStateView aplica color debug con MaterialPropertyBlock sin modificar gameplay ni materiales compartidos. |
| Milestone 19.2: Stable Color-Only State Visuals | Estabilizar la lectura visual del POI para que puerta y contenedor comuniquen estado solo por color debug. | validated | Validado: puerta y contenedor cambian color sin rotar, moverse ni cambiar geometria; la palanca sigue ocultandose con picked_up. |
| Milestone 20: Item Storage / Container Foundation v0 | Crear una base comun runtime-only para almacenamiento de items con cantidades simples, usada por inventario y contenedores del mundo. | validated | Validado: ItemStorage e ItemStorageEntry funcionan, InventoryComponent usa storage internamente y ContainerLootComponent transfiere contenido existente sin re-rollear loot. |
| Milestone 21: Stateful Inspection & Container Access v0 | Agregar inspeccion dependiente de RuntimeTags y defensas de acceso al storage de contenedores. | validated | Validado: WorldObjectDebugInfo elige textos condicionales, DebugActionExecutor los usa en examine_object y ContainerLootComponent bloquea accesos invalidos. |
| Milestone 21.0.1: Hotfix - State-Aware Inspection Selection | Corregir seleccion de texto condicional para usar RuntimeTags reales y reglas mutuamente excluyentes. | validated | Validado: la puerta forzada ya no muestra texto de puerta trabada. |
| Milestone 22: Actor Needs & Debug Supply Containers v0 | Agregar necesidades runtime genericas de actor, consumibles cerrados y cajas debug de suministros. | validated | Validado: hunger/thirst decaen, agua/comida restauran necesidades desde JSON y las cajas debug cargan loot tables grandes. |
| Milestone 22.1: Survival UI, Action Feedback & Manual Container Loot v0 | Agregar UI debug de survival, feedback de uso de items, stackeo por max_stack y saqueo manual de contenedores. | validated | Validado: UI Hunger/Thirst visible, ItemUsed en GameplayFeedbackLog, search_container abre ItemStorageDebugPanel y looted_container solo aparece al vaciar storage. |
| Milestone 22.1.1: Hotfix - Wire Survival and Storage Debug UI | Corregir wiring de paneles debug de necesidades/storage y colores de cajas debug. | validated | Validado: ActorNeedsDebugPanel e ItemStorageDebugPanel aparecen y bloquean clicks; crates nuevas reflejan estado por color. |
| Milestone 22.1.2: Hotfix - Equippable Item Flag | Agregar flag booleano equippable para controlar el boton Equip en InventoryDebugPanel. | validated | Validado: solo la palanca muestra Equip; agua/comida no se equipan y siguen pudiendo usarse. |
| Milestone 23: Actor Inventory Foundation v0 | Separar Storage y Equipped en `InventoryComponent` y validar `right_hand` como primer slot runtime de actor. | validated | Validado: crowbar se equipa solo en `right_hand` desde JSON; agua/comida/scrap no se equipan; force_door/pry_open_container siguen funcionando. |
| Milestone 23.0.1: Hotfix - Cleanup legacy equipped index warning | Eliminar el warning CS0414 del indice legacy de equip sin cambiar `rightHandItemInstanceId`. | validated | Validado: `rightHandItemInstanceId` queda como fuente real y `GetEquippedItemDefinitionId()` sigue devolviendo el item de `right_hand`. |
| Milestone 23.0.2: Hotfix - Revalidate Action Requirements Before Execution | Revalidar acciones contextuales antes de iniciar progreso. | validated | Validado: menus viejos no pueden iniciar acciones si la palanca fue desequipada. |
| Milestone 23.0.3: Hotfix - Refresh Context Menu Availability | Refrescar el menu contextual debug cuando cambia el item equipado. | validated | Validado: `Item` pasa a `(none)` y las acciones de palanca desaparecen/reaparecen segun `right_hand`. |
| Milestone 23.1: Lootable Debug Actor + Health v0 | Agregar actor debug looteable con salud basica usando la base de inventario de actor de M23. | validated | Validado: ActorHealthComponent v0, Debug NPC Capsule, search_body, loot de cuerpo por ItemStorageDebugPanel y bandage_01 funcionan sin romper M23. |
| Milestone 23.1.1: Hotfix - Health Examine Texts + Player Debug Damage | Agregar estado damaged_actor, textos de inspeccion por salud y dano debug al Player. | validated | Validado: actor full/damaged/low/dead muestra textos distintos; Player recibe dano debug sin muerte real ni lootable_actor. |
| Milestone 23.1.2: Hotfix - Debug Player Health Feedback | Registrar feedback debug al danar al Player desde ActorNeedsDebugPanel. | validated | Validado: Debug Damage Player registra Info en GameplayFeedbackLog con actor, dano y health antes/despues. |
| Post-M23.1 Functional Audit / Cleanup Pass | Auditar y limpiar deuda funcional menor antes de M24 sin cambiar comportamiento jugable. | validated | Validado: scripts debug/legacy sin referencias eliminados, objeto legacy inactivo removido de SampleScene, `ActionEffectTypes` centraliza effect types y los flujos M23/M23.1 siguen funcionando. |
| Milestone 24: Actor Profile Pipeline v0 | Crear una primera base data-driven de perfiles de actor con carga, validacion y aplicacion runtime. | validated | Validado: Debug NPC Capsule recibe identidad, tags iniciales, health e inventario desde `debug_npc_capsule_01` sin duplicar items. |
| Milestone 24.1: Actor Profile Data Load | Cargar `actor_profiles/*.json` y registrar `ActorProfileDefinition` en `GameDatabase`. | validated | Validado: Data Load OK, 0 errors, 0 warnings y `ActorProfiles: 1`. |
| Milestone 24.2: Actor Profile Validation | Validar schema, IDs, tags, health, inventario y referencias de actor profiles. | validated | Validado: `DataValidator` acepta el perfil actual y rechaza datos invalidos o `equipped` no soportado. |
| Milestone 24.3: Actor Profile Runtime Apply | Aplicar un actor profile una sola vez sobre componentes existentes del actor. | validated | Validado: `ActorProfileComponent` aplica display name, initial tags, health e initial inventory sin auto-crear componentes. |
| Milestone 24.4: Debug NPC Capsule Actor Profile Migration | Migrar Debug NPC Capsule al perfil data-driven y retirar su seeder manual. | validated | Validado: usa `actorProfileId = debug_npc_capsule_01`; recibe Bandage x3 y Scrap Metal x2 sin duplicacion. |
| Milestone 25: World Object Profile v0 | Crear un pipeline data-driven minimo para identidad y tags iniciales reutilizables de objetos del mundo. | validated | Validado: Data Load OK, 0 errors, 0 warnings; Debug Locked Door usa `debug_locked_door_01` y `force_door` sigue funcionando. |
| Milestone 26: Storage Transfer v0 / Bidirectional Item Transfer | Permitir transferencias en ambas direcciones entre inventario del jugador y storage abierto. | validated | Validado con contenedores y cuerpo de Debug NPC Capsule; no duplica items, conserva instancias completas y divide stacks parciales. |
| Milestone 26.0.1: Storage Panel Layout Swap | Reordenar visualmente el panel debug de storage sin cambiar su logica. | validated | Validado visualmente: Player Inventory a la izquierda, Open Storage a la derecha; Deposit y Take mantienen su direccion correcta. |
| Milestone 27: Search vs Open Storage v0 | Separar primera revision de contenedor natural de la apertura posterior de su storage. | validated | Validado: `search_container` descubre el storage una vez y `open_storage` permite reabrirlo incluso vacio sin generar loot nuevo. |
| Milestone 28: Container State / Naming Cleanup v0 | Limpiar naming y deuda de estados legacy de contenedores sin cambiar el comportamiento validado. | planned | Proximo recomendado; alcance todavia no implementado. |

## Milestone Actual

No hay milestone implementado pendiente de validacion.

Los ultimos milestones cerrados como `validated` son:

- Milestone 25: World Object Profile v0.
- Milestone 26: Storage Transfer v0 / Bidirectional Item Transfer.
- Milestone 26.0.1: Storage Panel Layout Swap.
- Milestone 27: Search vs Open Storage v0.

## Proximo Recomendado

Preparar Milestone 28: Container State / Naming Cleanup v0.

Objetivo recomendado: limpiar nombres y estados debug de contenedores despues de M27, reducir la dependencia futura de `lootable_container` / `looted_container` sin romper compatibilidad y corregir titulos debug inconsistentes como `Contenedor saqueado Contents (Debug)`.

M28 no debe redisenar storage, `search_body`, loot, inventario, UI final ni save system.

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

Milestone 17 dejo validado:

- `GameplayFeedbackEntryType`, `GameplayFeedbackEntry`, `GameplayFeedbackLog` y `DebugFeedbackLogPanel` funcionan como base runtime-only de feedback;
- `GameplayFeedbackLog` es append/read, no persistente y limitado por `maxEntries`;
- el log no tiene listeners, subscriptions, callbacks, dispatch ni payload generico;
- los sistemas de gameplay registran entradas estructuradas;
- `DebugFeedbackLogPanel` solo lee `Entries` desde `GameplayFeedbackLog`;
- el panel debug no recibe llamadas directas desde gameplay y no ejecuta logica de gameplay;
- `ItemPickedUp` se registra al recoger la palanca;
- `ItemEquipped` y `ItemUnequipped` se registran al equipar o desequipar;
- `ActionCompleted` se registra en `examine_object`, `force_door`, `pry_open_container` y `search_container`;
- `TargetStateChanged` debug registra cambios runtime de tags;
- `LootReceived` se registra al obtener `scrap_metal_01`;
- `search_container` deja de aparecer despues de saquear;
- el contenedor queda con `looted_container`;
- la puerta queda con `forced_open`;
- `InteractionSystem` no fue tocado;
- el gameplay no depende del panel de feedback;
- no se creo journal, quest log, UI final, save system, EventBus ni sistemas grandes.

Milestone 18 dejo validado:

- `ActionAvailabilityDiagnosticReport` y `ActionAvailabilityDiagnosticEntry` funcionan como diagnostico runtime-only de disponibilidad;
- el diagnostico usa `ActionAvailabilityEvaluator.Evaluate()` y `ActionAvailabilityResult`;
- el diagnostico evalua el mismo conjunto de acciones candidatas que `InteractionSystem` considera antes de filtrar;
- `GetAvailableActions()` sigue devolviendo solo acciones disponibles;
- el menu contextual ejecutable no cambio respecto a Milestone 17;
- `DebugActionAvailabilityPanel` muestra acciones disponibles y bloqueadas, razones de bloqueo y snapshots de contexto;
- puerta cerrada sin palanca: `force_door` queda bloqueada por item tags faltantes;
- puerta cerrada con palanca: `force_door` queda disponible;
- puerta forzada: `force_door` queda bloqueada por falta de `locked_door` y el snapshot muestra `forced_open`;
- contenedor sellado: `pry_open_container` queda disponible;
- contenedor abierto: `search_container` queda disponible;
- contenedor looteado: `search_container` queda bloqueada por falta de `lootable_container` y el snapshot muestra `looted_container`;
- `GameplayFeedbackLog` sigue separado y funcionando;
- `DebugFeedbackLogPanel` se muestra/oculta con F7;
- `DebugActionAvailabilityPanel` se muestra/oculta con F8;
- `InventoryDebugPanel` sigue funcionando con `I`;
- los paneles debug arrancan ocultos por defecto;
- no se toco JSON, loaders, database, validator, `GameplayFeedbackLog` base, combate, IA, save system, journal, quest log ni UI final.

Milestone 19.1 dejo validado:

- `WorldObjectStateView` soporta color debug por regla visual usando `MaterialPropertyBlock`;
- los colores reflejan runtime tags sin modificar gameplay ni materiales compartidos;
- la puerta cambia de rojo oscuro a verde tras `force_door`;
- el contenedor cambia de naranja a cian y luego gris oscuro durante el loop `pry_open_container` -> `search_container`;
- la palanca se oculta tras `pick_up_item`;
- F7, F8 e I siguen funcionando;
- el menu contextual sigue mostrando solo acciones disponibles;
- no se toco JSON, `InteractionSystem`, `ActionAvailabilityEvaluator`, `GameplayFeedbackLog` ni diagnostics.

Milestone 19.2 dejo validado:

- `SampleScene` fue ajustada para que puerta y contenedor mantengan geometria estable;
- se neutralizaron rotaciones, cambios de variante visual y deformaciones raras;
- puerta y contenedor comunican estados solo por color debug;
- la puerta cambia de rojo oscuro a verde tras `force_door` sin rotar ni moverse;
- el contenedor cambia de naranja a cian y luego gris oscuro sin cambiar geometria;
- la palanca sigue ocultandose con `SetActive` cuando tiene `picked_up`;
- data load sigue OK con 0 errors y 0 warnings;
- F7, F8 e I siguen funcionando;
- el menu contextual sigue mostrando solo acciones disponibles;
- no se toco codigo, JSON ni gameplay.

Milestone 20 dejo validado:

- `ItemStorage` funciona como clase C# pura runtime-only, no `MonoBehaviour`;
- `ItemStorageEntry` representa `ItemInstance` + `Quantity`;
- `Quantity` no fue agregado a `ItemInstance`;
- no hay auto-merge por `DefinitionId`, para evitar mezclar objetos unicos con distinta condicion;
- `InventoryComponent` usa `ItemStorage` internamente sin romper `AddItemByDefinitionId`, pickup, equip ni `InventoryDebugPanel`;
- `InventoryDebugPanel` muestra cantidades simples cuando `Quantity > 1`;
- `ContainerLootComponent` inicializa storage interno una sola vez desde su loot table antes de que el contenedor sea accesible;
- `search_container` transfiere contenido existente del contenedor al inventario en vez de generar loot al buscar;
- el contenedor queda con `looted_container` y no vuelve a entregar loot;
- data load sigue OK con 0 errors y 0 warnings;
- F7, F8 e I siguen funcionando;
- el menu contextual sigue mostrando solo acciones disponibles;
- no se toco JSON, schema, `InteractionSystem`, `ActionAvailabilityEvaluator`, diagnostics ni `GameplayFeedbackLog` base;
- no se agrego UI final, peso, slots, grid, save system ni contenedores anidados.

Milestone 21 dejo validado:

- inspeccion dependiente de `RuntimeTags` mediante `WorldObjectDebugInfo`;
- `WorldObjectDebugInfo` puede elegir textos condicionales por `requiredTags`, `forbiddenTags` y `priority`;
- los campos `displayName` e `inspectText` existentes se mantienen como fallback;
- `DebugActionExecutor` usa textos condicionales al ejecutar `examine_object`;
- `ContainerLootComponent` expone resumen debug de storage para inspeccion;
- el bloque `[DEBUG STORAGE]` muestra estado runtime del storage sin ser UI final;
- `ContainerLootComponent` separa tener contenido interno de poder acceder al storage;
- `ContainerLootComponent` valida acceso antes de transferir loot;
- `sealed_container` puede tener storage inicializado pero no permite `search_container`;
- `search_container` transfiere contenido existente y no entrega loot dos veces;
- data load sigue OK con 0 errors y 0 warnings;
- F7, F8 e I siguen funcionando;
- el menu contextual sigue mostrando solo acciones disponibles;
- no se toco JSON ni schema;
- no se toco `InteractionSystem`;
- no se toco `ActionAvailabilityEvaluator`;
- no se tocaron diagnostics;
- no se toco `GameplayFeedbackLog`;
- no se creo UI final de contenedor, peso, slots, grid, split/merge, save system ni contenedores anidados.

Milestone 21.0.1 dejo validado:

- la seleccion condicional de inspeccion usa `RuntimeTags` reales;
- las reglas de puerta son mutuamente excluyentes;
- puerta `locked_door` requiere `locked_door` y bloquea `forced_open`;
- puerta `forced_open` requiere `forced_open`, bloquea `locked_door` y tiene mayor prioridad;
- tras `force_door`, examinar la puerta muestra texto `forced_open` y no el texto de puerta trabada;
- el contenedor mantiene `looted_container` con prioridad mas alta;
- `opened_container` + `lootable_container` mantiene `forbiddenTags: looted_container`;
- `sealed_container` requiere `sealed_container`;
- no se toco JSON, `InteractionSystem`, `ActionAvailabilityEvaluator`, diagnostics, `GameplayFeedbackLog`, `ItemStorage`, `ItemStorageEntry` ni `InventoryComponent`.

Milestone 22 dejo validado:

- `ActorNeedsComponent` funciona como sistema generico de necesidades runtime para actores, no exclusivo del jugador;
- solo los actores con `ActorNeedsComponent` tienen necesidades;
- hunger/thirst existen como estado runtime y decaen durante Play Mode;
- el perfil/configuracion de necesidades queda separado del estado runtime;
- `water_bottle_01` restaura `thirst`;
- `food_ration_01` restaura `hunger`;
- los consumibles usan el bloque cerrado `consumable.restore_needs` en JSON;
- `InventoryDebugPanel` permite usar consumibles sin mover logica de hambre/sed a UI;
- `InventoryItemUseService` aplica efectos a `ActorNeedsComponent` y consume cantidad solo si hubo efecto valido;
- se agregaron cajas debug de suministros usando `ContainerLootComponent`, `lootTableId` e `ItemStorage`;
- data load sigue OK con 0 errors y 0 warnings;
- F7, F8, I, palanca, puerta y caja original siguen funcionando;
- no se creo cocina, heridas, enfermedad, temperatura, descanso, IA, combate, save system ni UI final.

Milestone 22.1 dejo validado:

- `ActorNeedsDebugPanel` muestra Hunger/Thirst arriba a la izquierda como UI debug temporal;
- consumir agua/comida se registra en `GameplayFeedbackLog` como `ItemUsed`;
- `max_stack` se agrego a `ItemDefinition` y JSON como fuente de stackeo simple;
- `max_stack = 1` significa no stackeable;
- `max_stack > 1` permite merge simple en `ItemStorage`;
- `ItemStorage` mergea por mismo `definitionId` hasta `max_stack`;
- `Scrap Metal x1 + Scrap Metal x500` queda como `Scrap Metal x501`;
- las cajas debug nuevas usan cantidades x500;
- `search_container` abre `ItemStorageDebugPanel` y ya no transfiere todo automaticamente;
- `ItemStorageDebugPanel` ofrece `Take 1`, `Take Stack`, `Take All` y `Close`;
- `looted_container` solo se aplica cuando el storage queda vacio;
- las cajas debug nuevas cambian color con `WorldObjectStateView`: cian con loot y gris/negro vacias;
- `ItemStorageDebugPanel` es debug reusable y no UI final;
- no se creo inventario final, drag/drop, peso, save system, comercio, IA ni combate.

Milestone 22.1.1 dejo validado:

- `ActorNeedsDebugPanel` esta presente en `SampleScene`, visible arriba a la izquierda y lee el `ActorNeedsComponent` del Debug Player;
- `ActorNeedsDebugPanel` puede autoresolver referencia de forma segura;
- `ItemStorageDebugPanel` existe en `SampleScene`;
- `DebugActionExecutor`/`search_container` pueden encontrar o crear el panel por fallback seguro;
- al buscar un contenedor valido se abre el panel de contenido;
- aparecen `Take 1`, `Take Stack`, `Take All` y `Close`;
- `DebugWorldUiInputBlocker` evita que clicks sobre paneles debug muevan al jugador o disparen acciones detras;
- Food/Water Debug Crate y Misc Debug Crate reflejan estado por color con `WorldObjectStateView`.

Milestone 22.1.2 dejo validado:

- `equippable` se agrego a `ItemDefinition` y JSON como boolean funcional;
- `equippable` no es tag y no usa strings `yes/no`;
- `rusted_crowbar_01` tiene `equippable: true`;
- `scrap_metal_01`, `water_bottle_01` y `food_ration_01` tienen `equippable: false`;
- `InventoryDebugPanel` muestra `Equip` solo si `itemDefinition.equippable == true`;
- la palanca sigue equipable;
- agua y comida no se pueden equipar, pero si usar;
- cantidades, stackeo, `Use` e `ItemStorageDebugPanel` siguen funcionando;
- data load sigue OK con 0 errors y 0 warnings.

Milestone 23 dejo validado:

- `InventoryComponent` separa conceptualmente Storage y Equipped;
- `right_hand` es el primer slot runtime funcional;
- `right_hand` usa `rightHandItemInstanceId`, no indice;
- el item equipado sigue existiendo dentro de `ItemStorage`;
- `rusted_crowbar_01` se equipa solo en `right_hand` segun JSON;
- `equip.equippable`, `equip.allowed_slots` y `equip.occupied_slots` estan soportados;
- `equippable` plano queda como compatibilidad temporal;
- `DataValidator` detecta contradicciones entre `equippable` plano y `equip.equippable`;
- `InventoryComponent` valida internamente si un item puede equiparse;
- `InventoryDebugPanel` muestra Equipped separado de Storage;
- agua, comida y scrap no se pueden equipar;
- agua/comida siguen usando `Use`;
- `InteractionSystem` sigue detectando la palanca equipada y habilita `force_door` / `pry_open_container`;
- loot final validado: Scrap x501, Water x500, Food x500, Crowbar x1;
- data load sigue OK con 0 errors y 0 warnings;
- F7, F8, I, Hunger/Thirst, `GameplayFeedbackLog`, `ItemStorageDebugPanel` y loot de cajas siguen funcionando.

Milestone 23.0.1 dejo validado:

- se elimino el warning CS0414 por el indice legacy de equip;
- `rightHandItemInstanceId` sigue siendo la fuente real del equipamiento;
- `GetEquippedItemDefinitionId()` sigue devolviendo el item equipado en `right_hand`.

Milestone 23.0.2 dejo validado:

- `ContextualActionDebugPanel` revalida requisitos antes de iniciar `DebugActionProgressController`;
- la revalidacion usa el flujo existente de `InteractionSystem` / disponibilidad de acciones;
- si la accion ya no esta disponible, no inicia progreso y muestra feedback debug;
- desequipar la palanca con un menu viejo abierto bloquea `force_door` y `pry_open_container`.

Milestone 23.0.3 dejo validado:

- mientras `ContextualActionDebugPanel` esta abierto, detecta cambios del item equipado actual;
- si cambia el item equipado, refresca acciones disponibles con `InteractionSystem.GetAvailableActions`;
- la linea `Item` cambia entre `rusted_crowbar_01` y `(none)`;
- `force_door` / `pry_open_container` desaparecen al desequipar y vuelven al reequipar si el target sigue valido;
- la revalidacion de M23.0.2 sigue siendo la proteccion real antes de ejecutar.

Milestone 23.1 dejo validado:

- `ActorHealthComponent` v0 funciona para Player y Debug NPC Capsule;
- health usa max/current health, `lowHealthThreshold` y estados runtime;
- estados runtime validados: `alive_actor`, `damaged_actor`, `low_health_actor`, `dead_actor` y `lootable_actor`;
- health no pinta colores directamente: actualiza tags runtime y `WorldObjectStateView` representa estados;
- Player y NPC vivos se ven verdes, low health se ve rojo y muerto se ve negro;
- Debug NPC Capsule puede recibir dano por accion debug contextual;
- Debug NPC muerto agrega `dead_actor + lootable_actor` si tiene inventario;
- `search_body` aparece solo con `dead_actor + lootable_actor`;
- `search_body` abre `ItemStorageDebugPanel` reutilizado mediante fuente reusable de storage;
- el cadaver no usa `ContainerLootComponent`;
- loot del cuerpo transfiere item instances al inventario del player;
- al vaciar el cuerpo, se remueve `lootable_actor` y `search_body` desaparece, manteniendo `dead_actor`;
- `DebugActorInventorySeeder` existe solo como componente debug, no como sistema de perfiles de NPC;
- `bandage_01` es consumible medico simple, no equipable, con `consumable.restore_health.amount = 25`;
- Bandage cura al Player y consume 1 solo si restaura health;
- si el Player esta full health, Bandage no se consume;
- Survival Supply Debug Crate mantiene el loot table ID existente y contiene Water Bottle x500, Food Ration x500 y Bandage x500;
- agua/comida siguen restaurando Hunger/Thirst;
- cajas normales siguen usando `ContainerLootComponent`;
- `ItemStorageDebugPanel` sigue funcionando con cajas y actor muerto;
- M23 sigue funcionando: `right_hand`, crowbar, `force_door`, `pry_open_container`, revalidacion de acciones y refresh del menu contextual;
- data load sigue OK con 0 errors y 0 warnings;
- F7, F8, I, Hunger/Thirst, `GameplayFeedbackLog`, `ItemStorageDebugPanel` y loot manual siguen funcionando.

Milestone 23.1.1 dejo validado:

- `damaged_actor` representa actor vivo con health por debajo del maximo;
- full health vivo: `alive_actor`;
- danado vivo: `alive_actor + damaged_actor`;
- baja salud vivo: `alive_actor + damaged_actor + low_health_actor`;
- muerto NPC: `dead_actor + lootable_actor` si tiene loot, o `dead_actor` si fue vaciado;
- Player puede recibir dano por boton debug en `ActorNeedsDebugPanel`;
- Player en 0 health es solo estado debug visual/numerico: sin muerte real, sin game over, sin bloqueo de movimiento/acciones y sin `lootable_actor`;
- `ActorNeedsDebugPanel` muestra Hunger, Thirst, Health y boton `Debug Damage Player`;
- Debug NPC Capsule muestra textos de examinar distintos para vivo full health, danado, low health y muerto usando `WorldObjectDebugInfo`.

Milestone 23.1.2 dejo validado:

- `Debug Damage Player` registra una entrada `Info` en `GameplayFeedbackLog`;
- la entrada incluye actor/player, dano aplicado y health antes/despues;
- el boton sigue danando al Player;
- Player sigue cambiando visualmente por estado de salud;
- Bandage sigue curando;
- NPC damage y `search_body` siguen funcionando igual.

Milestone 24 dejo validado:

- M24.1 agrego `ActorProfileDefinition`, carga de `actor_profiles/*.json`, registro y consulta en `GameDatabase`;
- M24.2 agrego validacion fuerte de type, id, display name, initial tags, health e initial inventory, y rechaza `equipped` mientras no esta soportado;
- M24.3 agrego `ActorProfileComponent`, que aplica una sola vez display name, initial tags, health e initial inventory sobre componentes existentes;
- M24.4 migro Debug NPC Capsule a `actorProfileId = debug_npc_capsule_01` y retiro `DebugActorInventorySeeder` de ese actor;
- Debug NPC Capsule recibe `bandage_01 x3` y `scrap_metal_01 x2` desde `actor_profiles.json` sin duplicar inventario;
- `DebugActorInventorySeeder.cs` no fue eliminado y queda como candidato legacy/debug para una futura limpieza controlada;
- Data Load validado: 0 errors, 0 warnings y `ActorProfiles: 1`;
- siguen funcionando `pick_up_item`, `right_hand`, `force_door`, `pry_open_container`, `search_container`, `debug_damage_actor`, `low_health_actor`, `dead_actor`, `lootable_actor` y `search_body`.

Milestone 25 dejo validado:

- `WorldObjectProfileDefinition`, `world_object_profiles.json`, carga en `GameDataLoader`, registro en `GameDatabase` y validacion en `DataValidator`;
- `WorldObjectProfileComponent` aplica una sola vez `display_name` e `initial_tags` sobre componentes existentes;
- Debug Locked Door usa `worldObjectProfileId = debug_locked_door_01`;
- Data Load OK con 0 errors, 0 warnings y `WorldObjectProfiles: 1`;
- `force_door` sigue funcionando con la puerta cargada desde profile.

Milestone 26 y Milestone 26.0.1 dejaron validado:

- transferencia bidireccional entre `InventoryComponent` y storages abiertos;
- `Take 1`, `Take Stack`, `Take All`, `Deposit 1` y `Deposit All`;
- transferencias completas conservan la instancia; transferencias parciales dividen stacks sin duplicar ni destruir items;
- al depositar completamente un item equipado se limpia `right_hand`;
- contenedores y cuerpos restauran estado de contenido al depositar cuando corresponde;
- `ItemStorageDebugPanel` muestra Player Inventory a la izquierda y Open Storage a la derecha sin cambiar la logica de transferencia.

Milestone 27 dejo validado:

- `search_container` representa solo la primera revision de un contenedor natural abierto y no revisado;
- `search_container` requiere `opened_container + unsearched_container`, conserva barra de carga, remueve `unsearched_container`, agrega `storage_accessible` y abre el panel;
- `open_storage` es una accion/effect cerrado separado, requiere `storage_accessible`, dura 0 y abre el mismo panel incluso vacio;
- Debug Sealed Container, Survival Supply Debug Crate y Misc Debug Crate usan el nuevo modelo;
- `search_body` no fue redisenado;
- `lootable_container` y `looted_container` siguen existiendo por compatibilidad;
- Data Load OK con 0 errors y 0 warnings; M26 sigue funcionando.

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
- `ItemStorage` es runtime-only y no es save data.
- `ItemStorageEntry` guarda `ItemInstance` + `Quantity`; la cantidad pertenece al storage, no a `ItemInstance`.
- `max_stack` en `ItemDefinition` es la fuente de stackeo simple.
- `max_stack = 1` significa no stackeable.
- `max_stack > 1` permite merge simple en `ItemStorage` por mismo `definitionId` hasta el limite del stack.
- `equip.equippable` es la fuente actual de equipabilidad por slot cuando existe el bloque `equip`.
- `equippable` plano en `ItemDefinition` queda como compatibilidad temporal y no debe contradecir `equip.equippable`.
- `equip.allowed_slots` y `equip.occupied_slots` declaran slots tecnicos; en M23 solo `right_hand` esta validado.
- El item equipado en `right_hand` se referencia por `rightHandItemInstanceId`, no por indice de storage.
- `InventoryDebugPanel` solo muestra `Equip` cuando `InventoryComponent` confirma que el item puede equiparse en `right_hand`.
- `consumable.restore_needs` define efectos cerrados de consumibles por `need_id` y `amount`.
- `consumable.restore_health.amount` define restauracion cerrada de health para consumibles medicos simples.
- `ActorNeedsComponent` es generico para actores y no exclusivo del jugador.
- `ActorNeedsComponent` mantiene configuracion/perfil separado de estado runtime.
- `ActorHealthComponent` es health runtime v0 para actores y no debe pintar visuales directamente.
- Health runtime actualiza tags como `alive_actor`, `damaged_actor`, `low_health_actor`, `dead_actor` y `lootable_actor`.
- `ActorProfileDefinition` define identidad y estado inicial data-driven de actores; el estado mutable sigue viviendo en componentes runtime.
- `ActorProfileComponent` aplica un perfil una sola vez sobre componentes existentes y no auto-crea componentes faltantes.
- Los Actor Profiles no declaran health runtime tags ni `equipped`; esos datos siguen fuera del schema validado de M24.
- `WorldObjectProfileDefinition` define identidad y tags iniciales reutilizables de objetos del mundo; no guarda estado runtime.
- `WorldObjectProfileComponent` aplica una sola vez `display_name` e `initial_tags` sobre componentes existentes y no lee JSON directamente.
- La muerte real del Player no existe todavia: 0 health del Player es solo estado debug visual/numerico, sin game over, bloqueo de movimiento/acciones ni `lootable_actor`.
- `DebugInventory` es debug temporal y no es inventario final.
- `InventoryComponent` es inventario de actor v0, usa `ItemStorage` para Storage y expone Equipped con `right_hand`; no es inventario final.
- `ActorInteractionContext` resuelve item equipado con prioridad `InventoryComponent` -> `DebugInventory` -> `equippedItemDefinitionId` legacy.
- Si `InventoryComponent` esta asignado al actor, define exclusivamente el item equipado; si devuelve sin item, no se usa fallback.
- Si no hay `InventoryComponent` y `DebugInventory` esta asignado, `DebugInventory` define el item equipado; si devuelve sin item, no se usa fallback legacy.
- `requirements.weapon_tags` es el campo activo para requisitos de tags del item equipado.
- `weapon_tags` es un nombre legacy compatible; una migracion futura podria introducir `required_item_tags`, pero no existe todavia.
- Tags iniciales de `WorldObjectTags` son configuracion del Inspector.
- Runtime tags de `WorldObjectTags` son estado mutable solo durante Play.
- Los effect types cerrados se centralizan en `ActionEffectTypes` para que `DataValidator` y `DebugActionExecutor` usen las mismas constantes.
- `add_tag` y `remove_tag` afectan al target en runtime.
- `show_target_info` es un effect cerrado que lee `WorldObjectDebugInfo`.
- `pick_up_item` es un effect cerrado que ejecuta `WorldItemPickup` y agrega una `ItemInstance` al `InventoryComponent` del actor.
- `search_container` es un effect cerrado para la primera revision de un contenedor natural con `opened_container + unsearched_container`; al completarse habilita `storage_accessible` y abre `ItemStorageDebugPanel`.
- `open_storage` es un effect cerrado separado para reabrir un storage ya descubierto, incluso vacio, sin generar loot nuevo ni repetir la revision inicial.
- `search_body` es la accion contextual debug para revisar actor muerto looteable mediante effect cerrado `search_actor_inventory`.
- Los cadaveres/actores muertos looteables no deben reutilizar `ContainerLootComponent`; exponen su inventario mediante una fuente reusable de storage.
- `ContainerLootComponent` valida acceso al storage antes de transferir loot y expone resumen debug de storage para inspeccion.
- `ItemStorageDebugPanel` es debug reusable para storage, permite transferencias bidireccionales y no es UI final.
- `LootTableDefinition` v0 es deterministica: solo `item_id` y `count`.
- `GameplayFeedbackLog` es runtime-only, append/read y no persistente.
- `GameplayFeedbackLog` no es EventBus: no tiene listeners, subscriptions, callbacks, dispatch ni payload generico.
- `DebugFeedbackLogPanel` es UI debug y solo lee entradas del log.
- El feedback de gameplay puede servir como base futura para HUD, journal, notificaciones o UI final, pero esos sistemas no existen todavia.
- `ActionAvailabilityDiagnosticReport` es runtime-only, debug/fundacional y no persistente.
- El diagnostico de disponibilidad no es EventBus: no tiene listeners, subscriptions, callbacks ni payload generico.
- El diagnostico de disponibilidad explica estado actual antes de ejecutar acciones; no registra hechos ocurridos y no se mezcla con `GameplayFeedbackLog`.
- `DebugActionAvailabilityPanel` es UI debug y solo muestra el reporte diagnostico.
- `DebugFeedbackLogPanel` arranca oculto por defecto y se alterna con F7.
- `DebugActionAvailabilityPanel` arranca oculto por defecto y se alterna con F8.
- `WorldObjectStateView` lee runtime tags y aplica reglas visuales debug sin modificar tags ni gameplay.
- `WorldObjectStateView` puede aplicar color debug por regla visual usando `MaterialPropertyBlock`.
- En `SampleScene`, puerta y contenedor comunican estados por color debug estable, sin rotacion ni cambio de geometria.
- `WorldObjectDebugInfo` puede seleccionar texto de inspeccion por `RuntimeTags`, `requiredTags`, `forbiddenTags` y prioridad.
- No hay scripting libre dentro de JSON.
- No hay inventario final todavia.
- No hay loot final ni avanzado todavia.
- No hay save system todavia.
- No hay journal ni quest log todavia.
- No hay combate real todavia.
- Movimiento validado: point-and-click sobre Ground.
- Debug Player usa CharacterController.
- Interacciones contextuales requieren proximidad.
- Camara usa CameraRig con pan, rotacion y zoom.
- `ActionDefinition.cost.time` se usa como duracion debug de acciones contextuales.
- `DebugActionExecutor` sigue siendo sincronico y aplica effects solo al terminar la duracion.

## Sistemas Existentes

- `GameDataLoader`: carga JSON desde mods, incluyendo `actor_profiles/*.json` y `world_object_profiles/*.json`.
- `GameDatabase`: guarda definiciones cargadas y expone Actor Profiles y World Object Profiles por ID.
- `TagRegistry`: registra tags validos.
- `DataValidator`: valida IDs, types, tags, referencias, effects, loot tables, Actor Profiles, World Object Profiles, `max_stack`, consumibles, datos `equip` y warnings no destructivos de `weapon_tags`.
- `ActionEffectTypes`: centraliza constantes de effect types cerrados compartidas por `DataValidator` y `DebugActionExecutor`.
- `ActionAvailabilityEvaluator`: evalua requirements y puede devolver resultado explicable.
- `InteractionSystem`: arma contexto y devuelve acciones disponibles.
- `ActorInteractionContext`: datos minimos del actor para interactuar.
- `ItemInstance`: instancia runtime-only minima de un item.
- `ItemStorage`: storage runtime-only de items con cantidades simples y merge basico por `definitionId` + `max_stack`.
- `ItemStorageEntry`: entrada runtime de storage con `ItemInstance` representativo y `Quantity`.
- `ActorNeedsComponent`: necesidades runtime genericas de actor con hunger/thirst debug.
- `ActorHealthComponent`: health runtime v0 de actores con max/current health, low health threshold y tags de estado.
- `ActorProfileDefinition`: definicion data-driven v0 de identidad, initial tags, health e initial inventory de actor.
- `ActorProfileComponent`: aplica un Actor Profile una sola vez sobre componentes runtime existentes.
- `WorldObjectProfileDefinition`: definicion data-driven v0 de display name e initial tags reutilizables para objetos del mundo.
- `WorldObjectProfileComponent`: aplica un World Object Profile una sola vez sobre `WorldObjectDebugInfo` y `WorldObjectTags` existentes.
- `ActorNeedProfile`: configuracion serializable de necesidades.
- `ActorNeedState`: estado runtime visible para debug.
- `LootableActorInventoryComponent`: expone el inventario de un actor muerto looteable como fuente reusable para `ItemStorageDebugPanel`.
- `DebugActorInventorySeeder`: componente legacy/debug candidato a limpieza controlada; ya no se usa en Debug NPC Capsule.
- `InventoryItemUseService`: aplica consumibles cerrados a `ActorNeedsComponent` / `ActorHealthComponent` y consume cantidad si hubo efecto valido.
- `InventoryItemUseResult`: resultado simple de uso de item para UI/debug.
- `LootTableDefinition`: definicion v0 de loot deterministico.
- `InventoryComponent`: inventario de actor v0 runtime-only apoyado en `ItemStorage`, con Storage y Equipped separados conceptualmente y slot `right_hand` por `rightHandItemInstanceId`.
- `DebugInventory`: inventario debug temporal para crear item instances y exponer item equipado.
- `InventoryDebugPanel`: UI debug OnGUI de inventario v0, muestra Equipped separado de Storage, uso de consumibles y equip validado por `InventoryComponent`.
- `ActorNeedsDebugPanel`: UI debug fija para Hunger/Thirst/Health y dano debug del Player.
- `ItemStorageDebugPanel`: UI debug reusable para inspeccionar y transferir contenido en ambas direcciones entre Player Inventory y Open Storage.
- `WorldItemPickup`: componente debug para recoger un item de mundo configurado.
- `ContainerLootComponent`: componente debug para inicializar storage desde loot table, reportar storage debug y transferir contenido solo cuando el contenedor es accesible.
- `WorldObjectTags`: initial tags y runtime tags.
- `WorldObjectStateView`: componente visual debug que lee runtime tags y aplica SetActive, rotacion local o color debug por regla visual.
- `WorldObjectDebugInfo`: texto debug para examinar objetos, con fallback y textos condicionales por runtime tags.
- `ActionAvailabilityResult`: resultado explicable de disponibilidad de acciones.
- `DebugActionProgressController`: controla acciones debug en progreso.
- `DebugActionExecutor`: ejecuta effects debug cerrados.
- `DebugActionExecutionContext`: contexto minimo para pasar actor, target e item equipado al executor.
- `ContextualActionDebugPanel`: menu contextual debug OnGUI; revalida acciones antes de ejecutar y refresca disponibilidad si cambia el item equipado.
- `ContextualActionDebugProgressPanel`: feedback debug de accion en progreso.
- `ContextualActionDebugResultPanel`: resultado debug OnGUI.
- `GameplayFeedbackEntryType`: categorias cerradas de feedback runtime, incluyendo `ItemUsed`.
- `GameplayFeedbackEntry`: entrada estructurada de feedback runtime.
- `GameplayFeedbackLog`: log runtime-only append/read de feedback de gameplay.
- `DebugFeedbackLogPanel`: panel debug OnGUI que lee entradas del log.
- `ActionAvailabilityDiagnosticReport`: reporte runtime-only de disponibilidad actual de acciones contextuales.
- `ActionAvailabilityDiagnosticEntry`: entrada estructurada por accion candidata con disponibilidad, razones y tags requeridos/faltantes.
- `DebugActionAvailabilityPanel`: panel debug OnGUI que muestra el reporte de disponibilidad con F8.
- `PointClickMovementController`: movimiento debug con CharacterController.
- `PointClickMovementInputController`: input de movimiento por click izquierdo.
- `DebugWorldUiInputBlocker`: bloqueo debug de clicks cuando hay UI abierta.
- `CameraRigController`: pan, rotacion y zoom de camara.

## Deuda Tecnica Menor

- Los tags legacy `lootable_container` y `looted_container` siguen existiendo por compatibilidad despues de M27; conviene limpiarlos gradualmente sin romper contenido ni estados visuales existentes.
- Algunos titulos debug combinan nombres de estado con el sufijo ingles `Contents (Debug)`, por ejemplo `Contenedor saqueado Contents (Debug)`; conviene normalizarlos en M28.
- Esta deuda no bloquea el loop validado de M27.

## Sistemas Que Todavia NO Existen

- inventario final;
- loot final o avanzado;
- contenedores reales;
- save system;
- world state persistente;
- journal;
- quest log;
- EventBus de gameplay;
- combate real;
- IA;
- pathfinding/NavMesh;
- sistema de dialogos;
- POIs multiples o de produccion;
- equipment system completo;
- actor inventory final;
- cadaveres lootables finales;
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
