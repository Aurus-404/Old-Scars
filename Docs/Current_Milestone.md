# Old Scars - Current Milestone

## Estado Actual

M33.1, M33.1.1, M33.2, M33.2.1, M33.2.2, M33.3, M33.3.1, M34.1, M34.1.1, M34.1.2, M34.1.3, M34.2, M34.2.1, M34.2.1a y M34.2.1b estan validados manualmente en Unity. M34.2.1c World Item Quick Actions y M35.0 Universal Visual Rig & Attachment Framework estan `implemented` en el checkout y pendientes de validacion manual en Unity. Milestone 32, Milestone 32.2, Milestone 32.4, Milestone 32.4.1 y Grid Inventory Backend v0 mantienen su estado previo `implemented`.

Los bloques listados como `implemented` no estan cerrados como `validated` hasta que Play Mode confirme su flujo completo.

Los ultimos milestones cerrados y validados en Unity son:

- Milestone 25: World Object Profile v0.
- Milestone 26: Storage Transfer v0 / Bidirectional Item Transfer.
- Milestone 26.0.1: Storage Panel Layout Swap.
- Milestone 27: Search vs Open Storage v0.
- M33.1: Visual Grid Inventory UI v0.
- M33.1.1: Inventory Footprint Rebalance + Universal Rotation.
- M33.2: Universal Grid Storage + Dual Grid Inventory UI v0.
- M33.2.1: Partial Directed Merge + Stable Dual Grid UI.
- M33.2.2: Data-Driven Initial Item Orientation + Footprint Polish.
- M33.3: Basic Carry Weight System v0.
- M33.3.1: Weight-Limited Partial Transfers.
- M34.1: Equipment Ownership & Slots Foundation.
- M34.1.1: Inventory & Equipment UI Cleanup.
- M34.1.2: Inventory Context Menu v0.
- M34.1.3: Inventory Context QoL & Atomic Equipment Replacement.
- M34.2: Item-Owned Storage / Backpack Foundation.
- M34.2.1: Inventory Interaction Unification & Backpack Access.
- M34.2.1a: Fix Equipment From Item-Owned Storage.
- M34.2.1b: Unified Context Actions for Equipped Items.

La validacion confirmada de M33.1 incluye Data Load OK con 0 errors y 0 warnings y las regresiones principales funcionando.

## Milestones Recientes Y Pendientes De Validacion

### Milestone 32: Debug Test House Kitchen Containers v0

Estado: `implemented`.

- `M32_DebugTestHouse/Containers/Fridge`, `Oven`, `Countertop`, `Cupboard` y `Upper countertop` fueron configurados como containers funcionales en `SampleScene`.
- Todos reutilizan `WorldObjectTags`, `WorldObjectDebugInfo`, `ContainerLootComponent`, `WorldObjectStateView`, `search_container`, `open_storage` e `ItemStorageDebugPanel`.
- Cada container tiene display name, inspect text, tags y loot table propios.
- Loot tables nuevas: `house_fridge_loot_01`, `house_oven_loot_01`, `house_countertop_loot_01`, `house_cupboard_loot_01` y `house_upper_cupboard_loot_01`.
- Item IDs existentes usados: `food_ration_01`, `water_bottle_01`, `bandage_01`, `ammo_303_british_01` y `scrap_metal_01`.
- `Oven` queda solo preparado semanticamente para una workstation futura mediante tags; no se implementaron crafting, recetas, WorkstationComponent ni UI nueva.
- No se tocaron player, movimiento, puertas, armas, animaciones, crates existentes, scripts C# ni prefabs.
- Pendiente de validar en Play Mode: search inicial con barra, apertura de storage, transferencias bidireccionales, reapertura con `open_storage` sin regenerar loot y regresion de containers debug viejos.

### Milestone 32.2: Real Door System v0

Estado: `implemented`.

- `locked_door`, `closed_door` y `opened_door` son los estados canonicos de puerta para M32.2.
- `force_door` ahora remueve `locked_door` y agrega `opened_door`.
- `open_door` remueve `closed_door` y agrega `opened_door`.
- `close_door` remueve `opened_door` y agrega `closed_door`.
- `DoorSwingController` rota solo `DoorVisualPivot` leyendo `WorldObjectTags`; no lee input, no muta tags, no ejecuta acciones y no toca inventario.
- `M32_DebugTestHouse/Doors/Debug Locked House Door Entrance` inicia como `locked_door`.
- `M32_DebugTestHouse/Doors/Debug Locked House Door Bedroom` inicia como `closed_door`.
- Ambas puertas M32 conservan root gameplay, `WorldObjectDebugInfo`, `WorldObjectStateView` y un collider interactuable/bloqueador bajo `DoorVisual`.
- `forced_open` queda como tag legacy de compatibilidad visual/textual; no es el flujo principal nuevo.
- No se implementaron broken doors, llaves, barricadas, HingeJoint, Rigidbody, fisica real, UI nueva, ruido, IA, pathfinding ni animaciones finales.
- Pendiente de validar en Play Mode: acciones disponibles por estado, swing fisico, bloqueo de paso cerrado, paso libre abierto y regresion de la puerta debug vieja.

### Milestone 32.4: Interior Visibility Raycast v0

Estado: `implemented`.

- Se agregaron `BuildingInteriorVolume`, `BuildingOccluderTarget` y `BuildingVisibilityManager` como sistema chico de visibilidad interior para la casa debug.
- `M32_DebugTestHouse` tiene un manager con referencias serializadas a `Main Camera`, `Debug Player`, `HouseInteriorVolume` y targets de estructura.
- `M32_DebugTestHouse/InteriorZones/HouseInteriorVolume` usa `BoxCollider` trigger y fallback por bounds/local-space para detectar al player.
- Las paredes estructurales de `M32_DebugTestHouse/Structure` usan `renderersToHide` para ocultarse por raycast camara-jugador.
- Las paredes mantienen `collidersToDisableWhileHidden` vacio para no desactivar los `BoxCollider` que bloquean fisicamente al jugador.
- `CasaPrimerPiso` queda como pieza superior `hideAlwaysWhenInside` y usa ambas listas, restaurando su estado inicial aunque ya arranque con renderer/collider deshabilitados.
- El manager corre en `LateUpdate`, usa `restoreDelay` contra parpadeo y no llama `Camera.main` cada frame.
- No se uso `GameObject.SetActive(false)`; se usan `renderer.enabled` y `collider.enabled`.
- No se tocaron puertas, containers, loot, inventario, player movement, armas, animaciones, JSON ni `DataDriven_JSON_Rules.md`.
- Pendiente de validar en Play Mode: entrada/salida de la casa, ocultamiento/restauracion de paredes, pieza superior y colliders opt-in, interacciones M32/M32.2 y Console sin errores rojos.

### Milestone 32.4.1: Door Pivot Repair + Interior Visibility Cast Debug/Stability

Estado: `implemented`.

- Se agrego una herramienta editor-only para puertas M32 con menus `Old Scars/Debug/Validate M32 Door Pivots` y `Old Scars/Debug/Repair M32 Door Pivots`.
- La herramienta opera solo bajo `M32_DebugTestHouse/Doors`, no se ejecuta automaticamente, no recrea puertas, no reemplaza roots funcionales y no toca JSON.
- `Repair M32 Door Pivots` normaliza solamente transforms de root, `DoorVisualPivot` y `DoorVisual`, transfiriendo la escala del root al visual cuando corresponde.
- `Validate M32 Door Pivots` reporta escalas no normalizadas, referencias/pivots faltantes, visuales faltantes, escalas invalidas y posiciones locales absurdas.
- `BuildingVisibilityManager` ahora usa casts desde player hacia camara con `SphereCastAll` por defecto, `RaycastAll` solo como fallback y `OverlapSphere` alrededor de la camara para casos cercanos.
- Defaults nuevos: `sphereCastRadius = 0.35`, `cameraOverlapRadius = 0.45`, `drawDebugCasts = true`, `logHitChanges = false` y `debugDrawDuration = 0.05`; `restoreDelay` se mantiene en `0.15`.
- Se agrego debug visual con `Debug.DrawRay`/lineas runtime y gizmos seleccionados del manager, sin mover camara ni tocar player movement.
- Se mantiene la politica de colliders: paredes estructurales ocultan solo renderer; techo/pisos superiores/piezas visuales usan colliders opt-in.
- No se usa `GameObject.SetActive(false)` y M32.4.1 no depende de cambios nuevos en `TagManager.asset`.
- Pendiente de validar en Unity: ejecutar validate/repair desde menu, probar puertas M32, entrar a la casa, mover/acercar/alejar camara y revisar debug casts/Console.

### Grid Inventory Backend v0

Estado: `implemented`.

- `ItemStorage` conserva contenido, cantidades y merge/split de stacks; `GridInventoryLayout` agrega capacidad espacial y placements por `ItemInstance.InstanceId`.
- El `InventoryComponent` del Debug Player usa grilla debug `6x8`; los inventarios de NPC/cadaver y los storages de containers/world items siguen lineales.
- Add, Remove y Transfer hacen preflight, reservan placements, aplican un commit y restauran snapshots completos si falla una invariante.
- El auto-placement es determinista: top-left, row-major, orientacion inicial data-driven primero y alternativa despues.
- Los siete items Core tienen metadata explicita; items externos sin metadata conservan fallback `1x1` con warning.
- En el bloque historico del backend, `right_hand` seguia dentro de `InventoryComponent`; M34.1 conserva ese campo solo como fallback/migracion y delega en `hand_right` cuando existe `ActorEquipmentComponent`.
- `Take All` y `Deposit All` globales quedan deshabilitados cuando participa el inventario espacial; las transferencias individuales y por stack siguen habilitadas.
- Ese bloque no agrego UI visual, peso ni equipamiento; M33/M34 los incorporan por capas sin cambiar el estado de validacion pendiente del backend original.
- Estado de validacion: solo checks estaticos; pendiente de Play Mode y Console.

### M33.1: Visual Grid Inventory UI v0

Estado: `validated`.

- `InventoryDebugPanel` y la columna izquierda de `ItemStorageDebugPanel` muestran la grilla OnGUI `6x8` del jugador con footprints reales.
- Seleccion, preview, drag-and-drop y rotacion `R` usan exclusivamente `ItemInstance.InstanceId` y placements del backend.
- Recolocar un item actualiza solo `GridInventoryLayout`; no cambia storage, cantidad, instancia ni `right_hand`.
- Los siete items Core declaran `inventory.icon_id` y tienen placeholders Sprite bajo `Resources/OldScars/InventoryIcons/`.
- `InventoryIconResolver` resuelve por `icon_id`; si falta un sprite, la grilla conserva color, abreviatura y cantidad como fallback.
- Containers y cadaveres siguen lineales en la columna derecha; batch global permanece deshabilitado.
- Legacy List sigue disponible manualmente y una entry sin placement muestra `MISSING PLACEMENT` sin inventar posicion.
- Validado manualmente en Unity: grilla, iconos, drag-and-drop, rotacion, seleccion, equipamiento y transferencias con containers/cadaveres funcionan.
- Data Load OK con 0 errors y 0 warnings.

### M33.1.1: Inventory Footprint Rebalance + Universal Rotation

Estado: `validated`.

- Footprints Core: rifle `6x1`, palanca `5x1`, botella `2x1`, scrap `2x2`, municion `1x1`, venda `1x1` y comida `1x1`.
- Todos los footprints no cuadrados pueden intercambiar ancho/alto mediante `IsRotated`; no existe un flag de opt-in por item.
- La rotacion de footprints cuadrados es exito no-op y no cambia placement ni `GridInventoryLayout.Version`.
- Storage, transfers, `right_hand`, containers, cadaveres, pickup, drop, firearm, escena e iconos permanecieron sin cambios en ese bloque.
- Validado manualmente en Unity junto con M33.1: rotacion universal, no-op de cuadrados, pickup/drop, equipamiento, transfers y Data Load 0 errors / 0 warnings.

### M33.2: Universal Grid Storage + Dual Grid Inventory UI v0

Estado: `validated`.

- `ItemStorage` sigue siendo la fuente de contenido y stacks; `GridStorageRuntime` compone opcionalmente `GridInventoryLayout`/`GridInventoryBackend` para cualquier owner compatible.
- `IGridStorageOwner` expone lectura por `InstanceId` y operaciones cerradas; containers y cadaveres no tienen backends especializados.
- Los containers inicializan primero su loot lineal y solo activan la grilla si todas las entries reciben placement; un fallo conserva contenido y habilita fallback lineal con error.
- Los actores aplican primero su inventario inicial y completan la inicializacion espacial sin cambiar `InstanceId`; el cadaver delega al `InventoryComponent` original.
- `ItemStorageDebugPanel` muestra Player Grid, centro provisional y External Storage Grid con una sola sesion de drag, selecciones independientes y Legacy List por lado.
- Drag interno solo recoloca layout. La base M33.2 mueve stack completo entre grillas y mantiene Shift/clic o botones con transferencia atomica y auto-placement; M33.2.1 define por separado placement exacto y merge dirigido.
- `InventoryUISessionController` es la unica autoridad para `I`/`Escape`, cierre, cancelacion de drag y bloqueo de movimiento, disparo, interaccion y camara mientras la sesion esta abierta.
- `SampleScene` configura crates, cocina y ambos NPC/cadaveres con dimensiones serializadas; no se cambiaron JSON, loot tables, puertas ni visibilidad.
- Validado manualmente en Unity junto con M33.2.1.

### M33.2.1: Partial Directed Merge + Stable Dual Grid UI

Estado: `validated`.

- El drag entre owners distingue placement exacto en celda vacia de merge dirigido sobre un `destinationInstanceId` concreto; el merge interno no existe.
- La deteccion del receptor usa la celda ocupada por `GridPlacement`, independiente del sprite, fondo o margenes visuales.
- Placement exacto usa insercion sin auto-merge, conserva el source `InstanceId` y respeta X/Y/orientacion aunque exista otro stack compatible.
- Merge dirigido usa capacidad real del receptor, permite transferencia parcial y conserva ambos placements/IDs mientras el source tenga remanente.
- Snapshots restauran contenido y versiones de ambos storages/layouts; hooks, tags y `right_hand` solo reaccionan despues de `Success`.
- El receipt expone cantidad real, IDs origen/destino y eliminacion del source; containers y cuerpos resincronizan contenido despues del commit.
- Seleccion/reconciliacion siguen usando `InstanceId`; el receptor queda activo despues de merge exitoso.
- Los mensajes usan toast absoluto con tiempo no escalado y las tres columnas se centran con un rect congelado por sesion.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; permanecen warnings preexistentes de `BuildingVisibilityManager`.
- Validado manualmente en Unity junto con M33.2.

### M33.2.2: Data-Driven Initial Item Orientation + Footprint Polish

Estado: `validated`.

- `inventory.initial_orientation` es opcional, admite solo `original`/`rotated` y usa `original` si falta.
- El first-fit prueba la orientacion inicial antes de la alternativa sin crear estados redundantes para footprints cuadrados.
- Footprints Core: rifle `7x2` con inicio rotado efectivo `2x7`; botella `2x1` con inicio rotado efectivo `1x2`; palanca `5x1`, scrap `2x2`, municion, venda y comida `1x1` con inicio original.
- La metadata solo afecta nuevas colocaciones y reconstrucciones; drag exacto conserva la orientacion solicitada y merge dirigido no altera placement.
- No se modificaron storage, transfer service, merge, `right_hand`, sesion, UI general, sprites, metas ni escena.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; permanecen cuatro warnings preexistentes de `BuildingVisibilityManager`.
- Validado manualmente en Unity por confirmacion del usuario.

### M33.3: Basic Carry Weight System v0

Estado: `validated`.

- `physical.weight_kg` es obligatorio, finito y no negativo para todo item Core; los siete items actuales declaran peso explicito.
- `ActorCarryWeightComponent` calcula el peso on demand desde `InventoryComponent`, usa capacidad base `30 kg` y hard limit `39 kg`, y expone estados `Normal`, `Encumbered` y `HardBlocked`.
- La politica opcional del owner bloquea incoming externo que exceda el hard limit; containers, cuerpos y NPCs sin el componente siguen sin limite de peso.
- La carga inicial de perfiles es el unico bypass controlado; pickup, Take/Deposit, Shift+click, drag exacto y merge parcial revalidan antes de mutar.
- La UI debug muestra snapshot del jugador y peso unitario/stack sin decidir permisos ni redimensionar paneles por mensajes.
- `SampleScene` solo agrega `ActorCarryWeightComponent` al Debug Player con capacidad `30` y multiplicador `1.3`.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; permanecen cuatro warnings preexistentes de `BuildingVisibilityManager`.
- Validado manualmente en Unity por confirmacion del usuario.

### M34.1: Equipment Ownership & Slots Foundation

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- `ActorItemOwnershipComponent` agrega las entries directas del inventario personal y del equipment storage y valida que cada `ItemInstance.InstanceId` pertenezca a un solo nodo.
- `ActorEquipmentComponent` usa un `ItemStorage` lineal separado; los slots solo referencian la misma instancia mediante mapas `slot -> InstanceId` e `InstanceId -> slots`.
- Los datos agregan 17 slots exactos y el layout `human_standard_01`, agrupado y ordenado para UI debug. `back` es un slot generico.
- `equip.slot_sets` declara alternativas completas: la palanca admite `hand_right` o `hand_left`; el rifle ocupa atomicamente ambas manos. No existe `both_hands`.
- `EquipmentTransactionService` separa preview y commit, revalida versiones, conserva `InstanceId` y revierte storages, layout, mapas, versiones y secuencia de IDs ante fallo.
- `right_hand` queda como compatibilidad temporal: cuando existe `ActorEquipmentComponent`, delega en `hand_right`; la interaccion prueba `hand_right` y luego `hand_left`.
- `ActorCarryWeightComponent` suma ownership agregado una sola vez por entry; mover un item entre inventario y equipment mantiene delta de peso cero.
- `InventoryDebugPanel` e `ItemStorageDebugPanel` muestran la lista central de 17 slots, scroll persistente, seleccion canonica por `InstanceId` y acciones debug de equipar/desequipar. La grilla externa permanece intacta.
- M34.2 queda reservado para item-owned storage/mochilas; no se implementaron nesting, pockets ni peso de subtrees.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; permanecen cuatro warnings preexistentes de `BuildingVisibilityManager`.

### M34.1.1: Inventory & Equipment UI Cleanup

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- Inventario personal y storage externo usan un body comun: Player Grid, Equipment y Details/External Grid comparten Y, altura, padding y separacion.
- La columna central separa un viewport de equipment con altura calculada y un footer fijo para Carry, instrucciones, seleccion y acciones.
- `EquipmentDebugListView` elimina el scrollbar horizontal, conserva scroll vertical y dibuja slot a la izquierda e item a la derecha con clipping e indicador `2H`.
- El encabezado legacy `Right Hand` y su boton fijo `Unequip` dejaron de dibujarse; las APIs legacy no cambiaron.
- Take/Deposit 1/Stack quedan dentro del footer externo; los detalles resumidos usan un viewport vertical interno y la grilla externa permanece en la derecha.
- La seleccion del footer consulta `InventoryUISessionSelection` como autoridad para evitar que un owner viejo del drag recupere foco.
- Toast, backend, JSON, escena, ownership, peso, transferencias, placements y rollback permanecen sin cambios.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; permanecen cuatro warnings preexistentes de `BuildingVisibilityManager`.

### M34.1.2: Inventory Context Menu v0

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- `InventoryUISessionController` posee un unico estado runtime de menu contextual y dialogo de cantidad; los paneles no mantienen menus paralelos ni estado estatico global.
- Acciones cerradas C# resuelven personal, external y equipment por `InstanceId`, owner actual y previews existentes; no usan nombres arbitrarios, reflexion ni decisiones por `DefinitionId`.
- Clic derecho en ambas grillas selecciona canonicamente la instancia, no inicia drag y abre acciones relevantes; clic derecho vacio cierra el menu y un drag activo se cancela sin abrirlo en el mismo evento.
- Las filas de Equipment reportan slot, instancia, rect y boton. Ambas filas de un rifle `2H` resuelven la misma entry y la misma accion de desequipar.
- Equip/Unequip delegan en `EquipmentTransactionService`; Use delega en `InventoryItemUseService`; Drop delega en `DroppedWorldItemSpawner`; Take/Deposit delegan en `GridStorageTransferService`.
- El modal absoluto de cantidad acepta enteros entre `1` y la cantidad disponible, bloquea el contenido de las grillas y revalida owner, instancia, cantidad y storage externo antes de ejecutar.
- `Escape` prioriza modal, menu, drag y sesion; `I` cierra menu, modal y sesion. El toast sigue absoluto y Take/Deposit 1/Stack permanecen como fallback temporal.
- Use desde equipment, auto-swap, equip desde external y drop equipado no se implementaron; el servicio de uso actual solo admite inventario personal.
- No se modificaron backend protegido, JSON, escena, sprites ni arte.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; validado manualmente con Data Load 0 errors / 0 warnings.

### M34.1.3: Inventory Context QoL & Atomic Equipment Replacement

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- `EquipmentReplacementPlan` registra source, slot set solicitado, desplazados unicos con todos sus slots, placements reservados, versiones esperadas y un `EquipmentFailureCode` cerrado.
- `PreviewEquipReplacing` simula sin mutar: copia el layout personal sin la instancia source y reserva por first-fit todos los desplazados, por `InstanceId` unico, usando el espacio liberado por source.
- `EquipReplacing` revalida versiones, ownership, slots y reservations; usa snapshots completos de ambos storages, layout, mapas de slots, versiones y secuencia de IDs para rollback exacto.
- Un item `2H` desplazado vuelve una sola vez al inventario y libera todos sus slots; dos ocupantes distintos se reservan y desplazan como dos instancias. No hay auto-drop, external fallback ni chequeo de peso nuevo.
- El menu diferencia `Equipar` de `Equipar y reemplazar`, muestra los objetos desplazados y usa mensajes humanizados sin exponer strings internos de storage.
- El modal de cantidad agrega slider entero sincronizado con campo numerico y botones `-/+`; Shift aplica saltos de 10 y el rango sigue cerrado en `1..maximum`.
- Para `quantity == 1`, Take/Deposit/Drop muestran una sola accion. `Ver detalles` queda oculto hasta M34.1.4 porque no agregaba informacion a la seleccion existente.
- `Needs (Debug)` no se dibuja mientras `InventoryUISessionController` tiene una sesion abierta; los botones fijos Take/Deposit del footer fueron retirados sin tocar clic derecho, Shift+click ni drag.
- No se modificaron escena, JSON, arte, prefabs, containers, cadaveres, pickup, drop, uso, peso, salud, movimiento, combate ni loot tables.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; permanecen cuatro warnings preexistentes de `BuildingVisibilityManager`.

### M33.3.1: Weight-Limited Partial Transfers

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- `GridStorageTransferQuantityPolicy` separa `Exact` de `ClampIncomingToActorHardLimit`; las APIs existentes conservan `Exact` como default compatible.
- Take Stack/Tomar todo y Shift+clic entrante hacia un actor con autoridad de peso solicitan clamp explicito. Take 1, Take Amount, drag exacto, merge dirigido, equipamiento, outgoing y storages no actor permanecen exactos.
- `ActorCarryWeightComponent` calcula una cantidad entera maxima con el hard limit, ownership agregado y `floor` con epsilon seguro; el peso unitario cero no limita.
- El preview es puro y combina el maximo por peso con el plan espacial existente. El commit recalcula y delega en la transaccion actual con reservations, snapshots, invariantes y rollback.
- El resultado/receipt expone cantidad solicitada, efectiva, remanente, limite por peso e IDs reales. Un parcial por peso es `Success`; maximo cero rechaza sin mutaciones.
- Si queda remanente, conserva source `InstanceId`, placement y seleccion External. Si desaparece, se selecciona el destination `InstanceId`, incluido un merge existente.
- Los hooks siguen corriendo solo tras `Success`; containers y cadaveres conservan sus decisiones de loot/vacio basadas en el contenido real posterior.
- No se modificaron escena, JSON, arte, prefabs, tags, loot tables, equipo, pickup/drop, necesidades, puertas, visibilidad, movimiento ni combate.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; permanecen cuatro warnings preexistentes de `BuildingVisibilityManager`.

### M34.2: Item-Owned Storage / Backpack Foundation

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- `owned_storage_profile_id` vincula una definicion de item con un perfil cargado desde `item_storage_profiles.json`; cada `ItemInstance` crea su propio `ItemOwnedStorageRuntime` y conserva contenido/layout por `InstanceId`.
- `small_backpack_01` es una mochila no stackable de `1.50 kg`, footprint `4x4`, equipable en el slot generico `back` y con storage espacial `8x10` definido por `backpack_small_01`.
- El owner raiz se resuelve transitivamente por identidad runtime. Transfers exitosos actualizan ownership despues del commit; previews y rechazos no lo mutan.
- El peso agrega exactamente una vez item contenedor y contenido. Transferencias entre inventario personal y mochila del mismo actor tienen delta cero; entradas externas y pickup de mochila usan el hard limit sobre el peso completo.
- Item-owned storage dentro de otro item-owned storage queda rechazado en v0 con rollback/no mutacion; no hay nesting, pockets, save/load ni multiples compartimentos.
- La UI OnGUI conserva tres columnas: la izquierda selecciona Inventario personal o una mochila accesible/equipada, el centro mantiene Equipment/detalles y la derecha conserva external storage. Celdas visuales configurables de `32 px` y scroll no cambian dimensiones logicas.
- La mochila inicial del Debug Player se resuelve desde un actor profile data-driven por tag `player`; `SampleScene.unity` no fue modificada.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; permanecen cuatro warnings preexistentes de `BuildingVisibilityManager`.

### M34.2.1: Inventory Interaction Unification & Backpack Access

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- Las acciones de usar, equipar, reemplazar y soltar se resuelven para cualquier compartimento personal por instancia, owner raiz y servicios existentes; la ubicacion solo cambia destinos de transferencia.
- El drag hacia equipment acepta inventario personal e item-owned storage del actor. Un slot compatible usa equip/replacement atomico; un slot ocupado por storage recibe items no equipables mediante `GridStorageTransferService` y mantiene no-nesting.
- Shift+clic y doble clic comparten una unica transferencia rapida de stack. Entradas automaticas resuelven la autoridad de peso desde el owner raiz y usan `ClampIncomingToActorHardLimit`; acciones de cantidad y drag exacto siguen siendo exactos.
- El selector muestra solamente storages equipados. Una mochila guardada ofrece `Revisar contenedor`, abre su runtime exacto por `InstanceId` en un overlay OnGUI y vuelve a cerrarse si pierde acceso.
- Equipment vuelve a vincular inventario y equipment al mismo owner raiz del actor despues de commit o rollback; no crea `both_hands`, no copia storage y conserva IDs/contenido.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; permanecen cuatro warnings preexistentes de `BuildingVisibilityManager`.

### M34.2.1a: Fix Equipment From Item-Owned Storage

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- Se corrigio el falso stale de replacement: la simulacion anterior llamaba una API que exigia remover un source de la grilla personal aunque la instancia viviera en la mochila.
- El preview item-owned captura `ContainerInstanceId`, versiones de storage/layout y placement del source. El commit re-resuelve el runtime por `InstanceId`, compara versiones reales y conserva el rechazo de stale legitimo.
- Menu contextual y drag delegan en la misma transaccion atomica; snapshots de source, personal, equipment, slots e IDs se capturan solo despues de revalidar y antes de mutar.
- Soltar un item sobre una mochila equipada conserva el compartimento visible. El cambio automatico por hover de `0.30 s` queda diferido como mejora UX.
- M34.2, M34.2.1 y M34.2.1a quedaron validados manualmente por confirmacion del usuario. El hover temporizado para abrir mochila sigue diferido y no bloquea.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; permanecen cuatro warnings preexistentes de `BuildingVisibilityManager`.

### M34.2.1b: Unified Context Actions for Equipped Items

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

- Los slots ocupados abren el mismo menu contextual y resolver de inventario, con origen `Equipment`, `InstanceId` y copia de todos los slots ocupados.
- El resolver omite el slot actual, alternativas incompatibles y no-op; una instancia multi-slot produce un solo conjunto de acciones desde cualquiera de sus filas.
- Mover entre alternativas de equipment y reemplazar usa una transaccion atomica que conserva la misma instancia, reserva placements para desplazados y restaura storage, slots, versiones e IDs ante fallo.
- Desequipar, mover a storage personal accesible, depositar en external y soltar pasan por operaciones cerradas; no-nesting, peso, ownership y hooks del destino se revalidan antes del commit.
- Cada commit exitoso publica una sola vez el snapshot visual final; preview, stale state, fallo y rollback no publican.
- Compilacion estatica de `Assembly-CSharp` y `Assembly-CSharp-Editor`: 0 errores. El usuario confirmo posteriormente la validacion manual de M34.2.1b.

### M34.2.1c: World Item Quick Actions

Estado: `implemented`; pendiente de validacion manual en Unity.

- El menu contextual mundial existente conserva `Examinar` y `Recoger` y agrega acciones rapidas de equip, replacement y storage desde estado real, sin tabla por item ni renderer paralelo.
- `WorldItemPickup` aporta referencia, `InstanceId`, definicion, cantidad y version de contenido estables. Preview y progreso no cambian tags, renderers, colliders ni fisica; la presentacion se finaliza solo despues del commit total.
- `WorldItemEquipmentTransactionService` coordina world -> equipment directamente, reserva destinos de desplazados y revierte source, personal, equipment y slots ante fallo, sin crear IDs ni usar inventario personal como staging.
- Los slot sets se resuelven completos: un rifle 2H ofrece una sola accion y produce un solo `CommitVisualState` despues de equip/replacement exitoso; preview, stale, fallo y rollback producen cero.
- Guardar en item-owned storage reutiliza transferencia exacta completa y su semantica canonica de stacks. No produce eventos visuales de Equipment y conserva exactamente `InstanceId` para no-stackables.
- `Recoger y consumir` queda omitido: falta un contrato autoritativo mundo -> consumo que revierta conjuntamente cantidad fuente y cambios de necesidades/salud.
- Compilacion estatica de `Assembly-CSharp` y `Assembly-CSharp-Editor`: 0 errores; validacion manual pendiente.

### M35.0: Universal Visual Rig & Attachment Framework

Estado: `implemented`; pendiente de validacion manual en Unity.

- Se agregaron contratos data-driven para capabilities, rig profiles, partes, sockets, visual assets, item visual profiles y attachment poses.
- Equipment publica snapshots inmutables exclusivamente gameplay mediante un evento central post-commit; no publica en preview, fallo o rollback.
- `EntityEquipmentVisualSynchronizer` es comun para actor y Debug Cargo, no hace polling permanente y deduplica visuals por `InstanceId`.
- El rig humano usa bindings sobre una instancia visual reemplazable; la herramienta Editor crea sockets bajo `spine_02`, `hand_l` y `hand_r` con Undo y nunca modifica el FBX fuente ni guarda la escena automaticamente.
- Mochila, palanca y Lee-Enfield tienen perfiles visuales. La mochila reutiliza `Backpack` equipada y `Backpack.001` de mundo mediante una generacion Editor manual; no se duplico el ZIP ni se importo arte nuevo.
- Debug Cargo usa `mount_storage -> cargo_mount` con la misma fuente/synchronizer universal y sin inventario, ownership ni gameplay.
- `WorldItemVisualResolver` conserva migracion progresiva: profile/provider, sistema world existente y fallback debug.
- Compilacion estatica de runtime y Editor: 0 errores; validacion visual y offsets permanecen pendientes de Unity.

## Ultimo Estado Validado

### World Object Profiles

- `WorldObjectProfileDefinition` y `world_object_profiles.json` forman el pipeline minimo de perfiles reutilizables para objetos del mundo.
- `GameDataLoader`, `GameDatabase` y `DataValidator` cargan, registran y validan World Object Profiles.
- `WorldObjectProfileComponent` aplica una sola vez `display_name` e `initial_tags` sobre componentes existentes.
- Debug Locked Door usa `worldObjectProfileId = debug_locked_door_01`.
- En el estado validado de M25, `force_door` seguia funcionando con la puerta cargada desde profile.
- El flujo actual de `force_door` fue migrado en M32.2 a `locked_door -> opened_door` y queda pendiente de validacion manual.

### Storage Bidireccional

- `ItemStorageDebugPanel` permite transferir items entre Player Inventory y Open Storage.
- Acciones disponibles: `Take 1`, `Take Stack`, `Take All`, `Deposit 1` y `Deposit All`.
- Transferencias completas conservan la instancia.
- Transferencias parciales dividen stacks correctamente.
- No se duplican ni destruyen items.
- Si se deposita completamente un item equipado, se limpia `right_hand`.
- Contenedores y cuerpos restauran estado de contenido al depositar cuando corresponde.
- Layout validado: Player Inventory a la izquierda y Open Storage a la derecha.

### Search Vs Open Storage

- `search_container` representa la primera revision de un contenedor natural.
- Requiere `opened_container + unsearched_container`, conserva barra de carga, remueve `unsearched_container`, agrega `storage_accessible` y abre `ItemStorageDebugPanel`.
- `open_storage` es una accion y effect cerrado separado.
- Requiere `storage_accessible`, dura 0 y abre el mismo panel aunque el storage este vacio.
- `open_storage` no genera loot nuevo ni repite la revision inicial.
- Vaciar un contenedor no elimina `storage_accessible`.
- Debug Sealed Container, Survival Supply Debug Crate y Misc Debug Crate usan el nuevo modelo.
- `search_body` no fue redisenado.

## Decisiones Vigentes

- JSON define datos; C# ejecuta logica.
- Perfiles data-driven definen configuracion inicial reutilizable, no estado runtime.
- Las escenas referencian profile IDs cuando corresponde.
- `ItemStorage` sigue siendo la base runtime comun de inventarios y storages.
- El acceso a un storage y la existencia de su contenido son conceptos separados.
- `ItemStorageDebugPanel` sigue siendo UI debug reusable, no UI final.
- `lootable_container` y `looted_container` se mantienen temporalmente por compatibilidad.

## Deuda Registrada

- Limpiar gradualmente la dependencia de tags legacy `lootable_container` / `looted_container` sin romper compatibilidad.
- Normalizar titulos debug inconsistentes como `Contenedor saqueado Contents (Debug)`.
- Revisar como deuda de balance los `max_stack = 999` actuales antes de una fase posterior de inventario; Grid Inventory Backend v0 no cambia esos valores.

## Proximo Recomendado

Validar M34.2.1c y M35.0 en Unity: probar quick actions sobre items mundiales y luego revisar mochila/palanca/rifle en player y NPC humano junto con el Debug Cargo Rig. M33.3.1, M34.2, M34.2.1, M34.2.1a y M34.2.1b ya estan `validated`; el hover temporizado de mochila sigue diferido.

Alcance recomendado:

- agregar ownership/equipment solamente al Debug Player y comprobar `human_standard_01` con 17 slots;
- validar reemplazo palanca -> rifle `2H`, dos items de una mano -> rifle y rifle `2H` -> palanca, incluido rechazo sin espacio y rollback;
- confirmar preservacion de `InstanceId`, ownership unico, peso sin duplicados/delta cero y compatibilidad de interaccion desde mano izquierda;
- validar lista central, scroll persistente, seleccion multi-slot y grilla externa preservada;
- validar containers de cocina M32;
- validar puertas M32.2 con `force_door`, `open_door`, `close_door` y `examine_object`;
- ejecutar `Validate M32 Door Pivots` y `Repair M32 Door Pivots` antes de probar puertas si las jerarquias visuales siguen corruptas;
- validar visibilidad interior M32.4 con camara libre, paredes estructurales y salida de la casa;
- validar los debug casts/overlap de M32.4.1 con camara cercana y lejana;
- validar first-fit, rotacion, grilla llena, merge sin celda nueva, split con placement nuevo, rollback y preservacion de `right_hand` ante fallo;
- validar pickup, drop, consumo, containers y cadaveres con el Debug Player en grilla `6x8`;
- validar ambas grillas, fallback lineal, drag exacto, Shift/clic, botones 1/Stack, tags de containers/cadaveres y autoridad unica de `I`/`Escape` de M33.2;
- confirmar Console sin errores rojos;
- despues de validar, retomar Milestone 28: Container State / Naming Cleanup v0.

No implementar todavia:

- UI final;
- save system;
- storage de refugio/base;
- contenedores creados por jugador;
- rediseno de cuerpos;
- loot avanzado;
- pockets, nesting general y nuevos compartimentos item-owned;
- armor, equipment corporal final y animacion/IK de attachments;
- combate o IA.
