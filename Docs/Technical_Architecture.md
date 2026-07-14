# Old Scars - Technical Architecture

## Data y runtime

- JSON contiene definiciones moddables; `GameDataLoader` las carga una vez, `GameDatabase` las registra por ID y `DataValidator` rechaza contratos/referencias invalidos.
- El Debug Player sin `ActorProfileComponent` puede recibir `initial_inventory` desde el unico actor profile cuyo `inventory_seed_actor_tag` coincide con sus tags; esta ruta no aplica health, display ni tags del profile.
- `ItemDefinition` describe un tipo. `ItemInstance` posee identidad y estado runtime; nunca se usa `DefinitionId` como identidad de ownership.
- `ItemStorage` sigue siendo la autoridad de entries, cantidades y stacks. `GridStorageRuntime` + `GridInventoryBackend` agregan layout, placements y transacciones espaciales.

## Ownership del actor y equipment

- `ActorItemOwnershipComponent` agrega inventario personal, equipment storage y contenido item-owned para validar ownership unico.
- `ActorEquipmentComponent` guarda una sola entry por item equipado; los slots solo referencian su `InstanceId`.
- `back` es un slot generico. Equipar una mochila no crea ni copia storage y no cambia el peso si el owner raiz sigue siendo el actor.

## Item-owned storage M34.2

- `ItemDefinition.owned_storage_profile_id` referencia `ItemStorageProfileDefinition`.
- Cada `ItemInstance` con perfil crea como maximo un `ItemOwnedStorageRuntime` propio, con `ItemStorage`, `GridStorageRuntime`, dimensiones, versiones y `ContainerInstanceId`.
- `ItemOwnedStorageRegistry` implementa resolucion por `InstanceId`, direct owner y owner raiz con deteccion de ciclos. Ownership cambia despues de commit; un rollback no lo modifica.
- `ItemOwnedStorageRuntime` implementa `IGridStorageOwner` y reutiliza `GridStorageTransferService`; no existe backend especial de mochila.
- Nesting item-owned queda prohibido en M34.2 v0 mediante un guard transaccional generico basado en capacidad de poseer storage.

## Peso

- `ActorCarryWeightComponent` sigue siendo la unica autoridad de capacidad.
- `ItemWeightResolver` suma el peso de la entry y su contenido item-owned exactamente una vez, con proteccion contra ciclos y duplicados.
- Transfers con el mismo owner raiz tienen delta cero. Entradas externas a un item-owned storage del actor usan la misma politica `Exact` o `ClampIncomingToActorHardLimit` de M33.3.1.

## UI debug

- `InventoryUISessionController` conserva autoridad unica sobre sesion, input, modal y drag.
- `PersonalStorageNavigator` solo selecciona owners accesibles; no posee datos ni placements.
- `InventoryGridDebugView` separa dimensiones logicas de tamano visual de celda. Los paneles OnGUI dibujan scroll y ejecutan mutaciones exclusivamente mediante APIs cerradas.

Estado de M34.2: `implemented`; pendiente de validacion manual en Unity.
