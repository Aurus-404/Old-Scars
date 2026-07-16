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

Estado de M34.2, M34.2.1 y M34.2.1a: `validated` por confirmacion manual del usuario.

## Context actions desde Equipment M34.2.1b

- `InventoryContextMenuRequest` representa Equipment mediante `InstanceId`, slot clicado y snapshot read-only de todos los slots ocupados; los paneles revalidan ese contexto antes de ejecutar.
- `InventoryContextActionResolver` sigue siendo la unica tabla de acciones. Omite el slot actual, no-op e incompatibilidades y resuelve relocation, replacement, unequip, storage transfer, drop y `ReviewOwnedStorage` por contratos existentes.
- `EquipmentTransactionService` mantiene la instancia dentro de equipment durante una recolocacion. Si hay replacement, desplaza las instancias ocupantes a placements reservados del inventario personal dentro del mismo snapshot/rollback.
- Sacar una instancia equipada hacia item-owned, external o world storage usa preview y commit cerrado, conserva ownership por `InstanceId`, ejecuta guards/peso/hooks y libera todos sus slots atomicamente.
- Un commit exitoso publica exactamente un snapshot visual final. Preview, no-op, stale state, fallo y rollback publican cero eventos.

Estado de M34.2.1b: `implemented`; pendiente de validacion manual en Unity.

## Universal Visual Rig M35.0

- Equipment conserva autoridad exclusiva sobre storage, ownership, slots e `InstanceId`. El visual consume `EquipmentVisualStateSnapshot`, una copia read-only que contiene solamente revision confirmada, versiones, layout e items equipados con sus slots.
- `ActorEquipmentComponent.CommitVisualState` es el unico punto de publicacion de `VisualStateCommitted`. Los servicios lo invocan una vez despues de equip, unequip, replacement, equip/replacement desde item-owned storage o migracion legacy exitosa; preview, fallo y rollback no publican.
- `EntityEquipmentVisualSynchronizer` combina el snapshot con `EntityVisualRigRuntime` y perfiles del `GameDatabase`. No hace polling permanente y mantiene como maximo un visual por `InstanceId`, incluso para equipment multi-slot.
- `VisualRigProfileDefinition` describe partes, sockets, mappings y familia; capabilities resuelven compatibilidad estructural sin asumir Player, humano, bipedo o cantidad de manos.
- `IVisualAssetProvider` separa asset keys data-driven de `Resources`; M35.0 implementa solamente el provider `builtin` y deja AssetBundles/Mod Kit fuera de alcance.
- `AttachmentPoseDefinition` conserva offsets por visual + rig/familia + socket. La resolucion usa exacto, familia, base e identidad; los offsets no viven en el synchronizer.
- Visuales equipados son hijos reemplazables sin gameplay, storage, ownership, colliders ni rigidbodies. `WorldItemVisualResolver` intenta perfil/provider, luego el sistema world legacy y finalmente el fallback debug existente.
- La indisponibilidad de partes/sockets es una API visual cerrada y publica invalidacion; no decide que debe hacer gameplay con el equipment afectado.

Estado de M35.0: `implemented`; pendiente de validacion manual en Unity.
