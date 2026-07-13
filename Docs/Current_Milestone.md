# Old Scars - Current Milestone

## Estado Actual

M33.1 y M33.1.1 estan validados manualmente en Unity. M33.2 Universal Grid Storage + Dual Grid Inventory UI v0 y M33.2.1 Partial Directed Merge + Stable Dual Grid UI estan implementados en el checkout y pendientes de validacion manual en Unity. Milestone 32, Milestone 32.2, Milestone 32.4, Milestone 32.4.1 y Grid Inventory Backend v0 mantienen su estado previo `implemented`.

Los bloques listados como `implemented` no estan cerrados como `validated` hasta que Play Mode confirme su flujo completo.

Los ultimos milestones cerrados y validados en Unity son:

- Milestone 25: World Object Profile v0.
- Milestone 26: Storage Transfer v0 / Bidirectional Item Transfer.
- Milestone 26.0.1: Storage Panel Layout Swap.
- Milestone 27: Search vs Open Storage v0.
- M33.1: Visual Grid Inventory UI v0.
- M33.1.1: Inventory Footprint Rebalance + Universal Rotation.

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
- El auto-placement es determinista: top-left, row-major, orientacion original primero y rotacion despues.
- Los siete items Core tienen metadata explicita; items externos sin metadata conservan fallback `1x1` con warning.
- `right_hand` sigue dentro de `InventoryComponent` por compatibilidad transitoria; no es todavia un EquipmentSlot separado.
- `Take All` y `Deposit All` globales quedan deshabilitados cuando participa el inventario espacial; las transferencias individuales y por stack siguen habilitadas.
- No se agregaron UI visual de grilla, drag-and-drop, peso, nesting, mochilas, equipamiento corporal ni save/load.
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

Estado: `implemented`.

- `ItemStorage` sigue siendo la fuente de contenido y stacks; `GridStorageRuntime` compone opcionalmente `GridInventoryLayout`/`GridInventoryBackend` para cualquier owner compatible.
- `IGridStorageOwner` expone lectura por `InstanceId` y operaciones cerradas; containers y cadaveres no tienen backends especializados.
- Los containers inicializan primero su loot lineal y solo activan la grilla si todas las entries reciben placement; un fallo conserva contenido y habilita fallback lineal con error.
- Los actores aplican primero su inventario inicial y completan la inicializacion espacial sin cambiar `InstanceId`; el cadaver delega al `InventoryComponent` original.
- `ItemStorageDebugPanel` muestra Player Grid, centro provisional y External Storage Grid con una sola sesion de drag, selecciones independientes y Legacy List por lado.
- Drag interno solo recoloca layout. La base M33.2 mueve stack completo entre grillas y mantiene Shift/clic o botones con transferencia atomica y auto-placement; M33.2.1 define por separado placement exacto y merge dirigido.
- `InventoryUISessionController` es la unica autoridad para `I`/`Escape`, cierre, cancelacion de drag y bloqueo de movimiento, disparo, interaccion y camara mientras la sesion esta abierta.
- `SampleScene` configura crates, cocina y ambos NPC/cadaveres con dimensiones serializadas; no se cambiaron JSON, loot tables, puertas ni visibilidad.
- Pendiente de validacion manual en Unity; no marcar `validated` hasta completar Play Mode y Console.

### M33.2.1: Partial Directed Merge + Stable Dual Grid UI

Estado: `implemented`.

- El drag entre owners distingue placement exacto en celda vacia de merge dirigido sobre un `destinationInstanceId` concreto; el merge interno no existe.
- La deteccion del receptor usa la celda ocupada por `GridPlacement`, independiente del sprite, fondo o margenes visuales.
- Placement exacto usa insercion sin auto-merge, conserva el source `InstanceId` y respeta X/Y/orientacion aunque exista otro stack compatible.
- Merge dirigido usa capacidad real del receptor, permite transferencia parcial y conserva ambos placements/IDs mientras el source tenga remanente.
- Snapshots restauran contenido y versiones de ambos storages/layouts; hooks, tags y `right_hand` solo reaccionan despues de `Success`.
- El receipt expone cantidad real, IDs origen/destino y eliminacion del source; containers y cuerpos resincronizan contenido despues del commit.
- Seleccion/reconciliacion siguen usando `InstanceId`; el receptor queda activo despues de merge exitoso.
- Los mensajes usan toast absoluto con tiempo no escalado y las tres columnas se centran con un rect congelado por sesion.
- Compilacion estatica de `Assembly-CSharp`: 0 errores; permanecen warnings preexistentes de `BuildingVisibilityManager`.
- Estado de validacion: pendiente de Play Mode/Console; no validated.

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

Validar M33.2/M33.2.1, Milestone 32, Milestone 32.2, Milestone 32.4, Milestone 32.4.1 y Grid Inventory Backend v0 en Unity antes de cerrar esos bloques como `validated`.

Alcance recomendado:

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
- combate o IA.
