# Old Scars - Technical Architecture

## Alcance Y Autoridad

Este documento describe contratos tecnicos implementados en el slice actual. No asigna IDs, estados, dependencias ni gates; esa autoridad pertenece a [Project_Roadmap.md](Project_Roadmap.md). Una capacidad futura mencionada aqui es un limite de integracion, no una implementacion existente.

## Datos, Mods Y Runtime

- JSON contiene definiciones moddables; `GameDataLoader` las carga una vez, `GameDatabase` las registra por ID y `DataValidator` rechaza los contratos o referencias que valida explicitamente.
- `Mods/Core` se carga primero. Los directorios externos se cargan despues en orden alfabetico y pueden agregar IDs nuevos.
- No existe todavia politica de override, manifest, dependencia o version de mod. Un ID duplicado dentro de su tipo/registro produce error y la segunda definicion se rechaza.
- El deserializador actual ignora campos desconocidos. Eso no los incorpora al contrato: un campo no documentado puede quedar silenciosamente sin consumidor hasta que loader, validator y runtime lo implementen.
- Las definiciones no contienen estado de partida. Los objetos colocados en escena y las instancias runtime consumen definiciones por ID.
- `ItemDefinition` describe un tipo; `ItemInstance` conserva identidad y estado de una instancia representativa. La cantidad del stack pertenece a `ItemStorageEntry`, no a `ItemInstance`.

## Identidad Durable De Items Y Limite De Persistencia

- `ItemInstance.InstanceId` es un `string` get-only autoritativo. Los IDs nuevos usan `item_<GUID N lowercase>`; son opacos para consumidores y no codifican comportamiento.
- `ItemInstance.CreateNew` valida la definicion, reserva un ID nuevo, usa `condition_max` y registra explicitamente item-owned storage. El constructor publico legacy conserva exactamente esa semantica de new runtime item.
- `ItemInstance.CreateAuthored` valida una definicion y reserva exactamente un `item_<32 hex lowercase>` preasignado para un world item colocado en escena. No genera fallback, no reemplaza `Rehydrate` y no se usa para drops runtime, que conservan su instancia existente.
- `ItemInstance.Rehydrate` valida y reserva exactamente el ID y el `Condition` recibidos, rechaza duplicados y devuelve un item detached. En una futura hidratacion con storage propio, el caller puede adjuntarlo sin publicar, poblar su contenido con layout pendiente, completar la validacion inicial y recien entonces registrarlo de forma explicita. M37 debe usar esta ruta y no el constructor publico.
- `ItemInstanceIdRegistry` mantiene solamente un `HashSet` de IDs activos. Un reset en `SubsystemRegistration` limpia de forma coordinada identidad, storages y ownership runtime; no existen tombstones persistentes, high-water ni historial de retirados.
- Un stack contiene una `ItemInstance` representativa y `ItemStorageEntry.Quantity`; las unidades fungibles internas no poseen IDs individuales. `CanStackWith` exige `DefinitionId`, `Condition`, `MaxStack` y ausencia de owned storage compatibles.
- Split conserva el ID fuente y crea un sibling durable. Merge conserva el ID destino; si consume completamente la fuente, la retira solo despues de validar storage/layout y confirmar la transaccion. Transfer, drop y equip/unequip preservan identidad.
- Un `GridInventoryBackend.Remove` que retiraria la entry completa rechaza antes de mutar si su item-owned storage no esta vacio; devuelve `OwnedStorageNotEmpty`. Una vez vacio, el retiro terminal libera bindings, storage e identidad mediante el contrato existente.
- Los scopes de reserva ambient/nested estan limitados al hilo de sesion, exigen LIFO y transfieren reservas al scope padre. El contexto localizado es necesario porque constructors y split reservan IDs dentro de servicios transaccionales ya existentes; evita cambiar sus contratos publicos. Rollback restaura storage/layout/Equipment y luego libera solamente IDs nuevos con sus registros/bindings.
- `ItemInstance.Condition` permanece get-only. Es estado de instancia representativo, participa en stacking y debe rehidratarse exactamente en M37; no hay mutacion, desgaste ni reparacion.
- M37.0 implementa el envelope, versionado, filesystem y recovery descritos en `Persistence Core V1`; M37.1 implementa snapshot/preflight y apply transaccional del slice real. La validación manual fresh-session permanece pendiente.
- `PersistentSceneObjectId` aporta identidad authored estable a exactamente 14 roots stateful de `SampleScene`: 3 actores, 3 puertas y 8 contenedores. Los dos world items usan identidad de item separada; visuales, children y `Debug Strange Machine` quedan excluidos.
- No existe un lifecycle persistente común. M37.1 restaura sólo el estado explícito del Current Slice; actores vivos fuera del slice, transform/lifecycle de NPCs y sistemas futuros permanecen excluidos.

## Persistence Core V1

- `PersistenceSerializer.CurrentFormatVersion` vale `1`. El envelope JSON conserva los nombres estables `formatVersion`, `writtenUtc` y `payload`; `payload` debe estar presente y no ser null.
- El payload es un `JToken` desacoplado. M37.0 no serializa directamente `MonoBehaviour`, `GameObject`, `Transform`, `UnityEngine.Object`, `ItemInstance`, registries ni componentes runtime; M37.1 construye y consume sus DTOs sobre este limite.
- La configuracion Newtonsoft.Json de saves es independiente del loader de definiciones: cultura invariante, `TypeNameHandling.None`, parseo de fechas desactivado, nulls explicitos, loops rechazados, nombres duplicados rechazados y JSON indentado UTF-8 sin BOM.
- El reader valida JSON, root object, `formatVersion` entero y payload. Version actual se entrega; version futura produce `FutureVersionUnsupported`; version anterior usa solamente pasos `ISaveMigration` consecutivos registrados explicitamente o produce `MigrationUnavailable`. No existe migration historica ficticia en V1.
- `PersistenceFileStore` resuelve produccion bajo `Application.persistentDataPath/Saves`; un root base inyectado queda reservado para diagnostics. Slot IDs usan snake_case cerrado, maximo 64 caracteres, y nunca aceptan separadores, extensiones o rutas arbitrarias.
- Cada slot usa `<slot>.json`, `<slot>.json.tmp` y `<slot>.json.bak`. El documento se serializa y valida completamente en memoria; el temp se escribe en el mismo directorio con flush forzado antes de su promocion.
- El primer write promueve temp por rename en el mismo filesystem. Overwrite usa `File.Replace(temp, primary, backup)` cuando esta soportado; el fallback por `PlatformNotSupportedException`/`NotSupportedException` mueve primero primary a backup y luego temp a primary, restaurando primary si la segunda promocion falla. No se afirma atomicidad universal fuera de la operacion soportada por plataforma/filesystem.
- Read prefiere primary valido. Primary ausente/corrupto puede entregar backup valido con recovery explicito sin borrar o reescribir evidencia; primary y backup invalidos producen `RecoveryFailed`. Versiones futuras o migrations ausentes son rechazos de politica y no hacen rollback silencioso a backup.
- Los resultados distinguen `Success`, `SaveNotFound`, `InvalidSlotId`, `IoFailure`, `MalformedJson`, `InvalidEnvelope`, `FutureVersionUnsupported`, `MigrationUnavailable`, `RecoveryFailed` y `SerializationFailure`.
- Los failures registran operacion, slot, paths, versiones, existencia, recovery, failure code/causa y accion sin imprimir payload. Los commits y recoveries exitosos usan logs breves.
- `M37PersistenceCoreDiagnostics` ejecuta once escenarios sobre un subdirectorio unico del temp del sistema, nunca sobre saves reales, y elimina ese root en `finally`.

## Current Slice Snapshot V1

- `CurrentSliceSaveData` es el payload explícito de M37.1. Contiene player, tabla única de items, storages, Equipment, containers, corpses, doors y world items; no serializa `MonoBehaviour`, `Transform`, referencias runtime ni definiciones estáticas.
- `ItemState` define cada identidad una sola vez mediante `InstanceId`, `DefinitionId` y `Condition`. `StorageEntryState`, Equipment y world representations referencian ese ID; quantity permanece en la entry/representación y no crea IDs por unidad fungible.
- `StorageState` usa claves derivadas de `kind + ownerId`: actor/container por `PersistentSceneObjectId` e item-owned storage por el `InstanceId` del item owner. Entries grid conservan x/y, rotación y footprint efectivo exactos.
- player captura pose mundial, health escalar, hunger/thirst, Inventory, Equipment y owned storages. Health tags, carry weight, stats y visuales son derivados y no se duplican en el save.
- containers se incluyen aunque su storage autoritativo esté vacío. Un container runtime no inicializado aborta capture; Pass 1 no ejecuta loot tables ni agrega un restore seam anticipado.
- corpses representan sólo roots authored actualmente muertos. Los NPC vivos, su transform y su lifecycle permanecen fuera de M37.1 y pertenecen a M38.0.
- cada world item authored posee un marker `present/absent` por su item ID. Un authored lazy se proyecta desde authored ID + definition sin reservar identidad; uno recogido queda absent y su item debe resolver en otro owner. Drops runtime presentes conservan quantity y pose.
- puertas guardan sólo un estado lógico entre `opened_door`, `closed_door` y `locked_door`. Containers guardan únicamente los tags runtime mutables allowlisted; tags estáticos, health/world tags derivados y ángulos visuales quedan excluidos.
- semantic preflight valida schema, scene identities, definitions, condición, localización única, quantity/max stack, placements/overlap, Equipment multi-slot completo, item-owned storage requerido sin nesting, referencias de containers/corpses/doors y authored/runtime world state. No muta gameplay.
- el comparador canónico ordena colecciones incidentales y usa tolerancia `0.0001` sólo para pose; cualquier otra diferencia produce un path accionable.
- `Save Debug Slot` llama al capture/preflight/write real y `Load Debug Slot` llama al pipeline transaccional real sobre `m37_current_slice_debug`; ambos están disponibles sólo en Play Mode.
- `M37.1 Snapshot & Semantic Preflight Diagnostics` entra Play Mode sobre `SampleScene`, usa un root temporal, prueba casos válidos/negativos y sale sin guardar la escena.

## Transactional Current Slice Load V1

- `CurrentSliceLoadService` distingue `Success`, `ReadFailed`, `SemanticPreflightFailed`, `SceneResolutionFailed`, `ApplyFailed` y `RollbackFailed`, con fase, causa y resultado de rollback localizados.
- La secuencia es read/preflight, resolución de referencias, capture del snapshot pre-load, teardown selectivo, `ItemInstance.Rehydrate`, owned storages, root storages/Equipment/ownership, world reconciliation, runtime state, pose del player, recapture y comparación canónica.
- El teardown no usa resets globales. Limpia solamente las superficies del player, containers, corpses y world representations seleccionadas; recoge también los IDs bootstrap realmente contenidos y preserva bindings de NPCs vivos fuera del slice.
- Cada item se rehidrata una vez con `InstanceId`, `DefinitionId` y `Condition` exactos. Los owned storages se adjuntan detached, reciben entries/placements exactos, completan layout y sólo entonces se registran.
- Root storages se reemplazan atómicamente después de validar quantities, footprints, bounds y overlap. Equipment restaura layout, una entry por item y slot IDs sin duplicar items multi-slot; ownership se reconstruye después de publicar storages válidos.
- Authored world markers restauran present/absent de forma autoritativa; absent marca la fuente inicializada y evita lazy respawn. Runtime drops se crean desde la `ItemInstance` ya rehidratada, conservando quantity y pose, sin split, transfer ni ID nuevo.
- Containers quedan inicializados con contenido autoritativo incluso vacío, por lo que load nunca ejecuta loot tables. Corpses deben estar ya muertos y restauran sólo health/Inventory/Equipment/owned storage; un root vivo produce `SceneResolutionFailed` porque lifecycle general pertenece a M38.0.
- Doors restauran el tag lógico y sincronizan visual si exponen `DoorSwingController`. Player health/needs se aplican exactamente y la pose se restaura al final, cancelando movimiento y deshabilitando temporalmente `CharacterController`.
- Ante fallo posterior a mutación, el mismo `ApplyCore` recibe el snapshot pre-load sin recursión. Sólo un rollback recapturado y equivalente produce `ApplyFailed` seguro; si falla, `RollbackFailed` conserva causa de apply y rollback.
- `M37.1 Current Slice Persistent Round-Trip Diagnostics` usa `SampleScene` y root temporal, prepara State A mediante rutas runtime, muta State B, carga A y compara A/C. Un único fault point `UNITY_EDITOR` posterior a storages demuestra rollback equivalente; el diagnóstico sale sin guardar la escena ni dejar archivos.

## Inventory, Grid, Ownership Y Equipment

- `ItemStorage` es la autoridad de entries, cantidades y stacks. `GridStorageRuntime` y `GridInventoryBackend` agregan layout, placements y transacciones espaciales sin crear otra lista de items.
- `ActorItemOwnershipComponent` agrega inventario personal, equipment storage y contenido item-owned para exigir ownership unico.
- `ActorEquipmentComponent` guarda una sola entry por item equipado; los slots referencian su `InstanceId`. Un item multi-slot no duplica storage, detalle ni peso.
- `back` es un slot generico. Equipar una mochila no crea ni copia su storage y no cambia el peso si el owner raiz sigue siendo el actor.
- Preview, commit, stale checks, guards y rollback viven en servicios transaccionales existentes; UI y visuales no mutan storages directamente.
- Un commit exitoso publica un unico snapshot visual final. Preview, no-op, fallo y rollback no publican estado visual confirmado.
- Hooks y observers posteriores al commit son notificaciones best-effort, no parte del rollback transaccional: una excepcion se diagnostica, pero no revierte gameplay ya confirmado.

## Item-Owned Storage Y Peso

- `ItemDefinition.owned_storage_profile_id` referencia un `ItemStorageProfileDefinition`.
- Cada `ItemInstance` nuevo con perfil crea como maximo un `ItemOwnedStorageRuntime` propio, con `ItemStorage`, grid, versiones y `ContainerInstanceId` exactamente igual al ID del item. `CreateNew` completa layout y registro; `Rehydrate` permanece detached y no adjunta ni publica storage automaticamente.
- El constructor de `ItemOwnedStorageRuntime` no registra por side effect. El attachment detached admite un resolver de definiciones y layout inicial pendiente; `CompleteInitialContentLoad` debe validar el layout antes del registro. `ItemOwnedStorageRegistry` registra de forma explicita, rechaza duplicados, resuelve por `InstanceId`, direct owner y root owner, y detecta ciclos. Repetir el mismo binding es idempotente; cambiar owner exige una transicion explicita despues del commit.
- `ItemOwnedStorageRuntime` implementa `IGridStorageOwner` y reutiliza `GridStorageTransferService`; no existe un backend especial de mochila.
- `ContainerLootComponent` puebla contenido inicial mediante `GridInventoryBackend.Add`, verifica cada cantidad afectada, bindea los owners resultantes y restaura el snapshot y las reservas nuevas si falla el lote.
- Nesting de item-owned storage permanece prohibido en el contrato v0 mediante un guard transaccional generico.
- `ActorCarryWeightComponent` es la autoridad de capacidad. `ItemWeightResolver` suma cada entry y su subtree item-owned exactamente una vez, con proteccion contra ciclos y duplicados.
- Transfers dentro del mismo root owner tienen delta cero. Entradas externas usan las politicas de peso existentes del actor.

## Actor Profiles Y Bootstrap Inicial

- `ActorProfileComponent` espera a que `GameDataManager` este listo y aplica display, tags iniciales, health escalar, equipment layout, inventario inicial, Equipment inicial y visual rig profile.
- `initial_inventory` crea instancias reales en el inventario del actor. Sus entradas se aplican una por una: un fallo registra warning y no revierte entradas anteriores. El selector debug `inventory_seed_actor_tag` puede aplicar solamente ese inventario a un actor sin `ActorProfileComponent`; no aplica display, health, Equipment ni visual rig.
- `initial_equipment` requiere `equipment_layout_id` y crea instancias reales de cantidad uno. Cada entrada puede seleccionar una alternativa completa mediante `slot_ids`; si se omite, debe quedar una unica alternativa libre.
- `EquipmentTransactionService.TryEquipInitialItems` captura inventario, Equipment y slots y abre un scope de reservas justo antes del lote `initial_equipment`. Un error restaura esos snapshots, limpia IDs/storages nuevos y no deja Equipment parcial.
- El bootstrap valida ownership unico antes de publicar el snapshot visual confirmado.
- El profile completo no es una transaccion: un rollback de `initial_equipment` conserva display, tags, health, layout e `initial_inventory` ya aplicados.

## Interaccion Y Estado Del Mundo

- `InteractionSystem`, `ActionAvailabilityEvaluator` y effects C# cerrados mantienen la logica separada de panels y objetos concretos.
- `WorldObjectTags` conserva estado runtime por tags; `WorldObjectStateView` lo presenta sin decidir acciones ni mutarlo.
- Containers, corpses y world items reutilizan `ItemStorage`, grid, ownership y servicios de transfer. No se convierten en un backend paralelo por su presentacion.
- Doors usan tags canónicos de estado; M37.1 persiste el tag lógico y sincroniza el controlador visual cuando existe.

## Observabilidad Diagnostica

- Los failure boundaries del slice escriben contexto accionable y proporcional en Unity Console / `Editor.log`: operacion, objeto o actor, IDs, owners, storage, resultado y estado de rollback cuando esos datos existen.
- Los valores ausentes relevantes usan `<NONE>`, `<EMPTY>` o `<UNKNOWN>`. Los commits importantes son breves y correlacionables por `InstanceId`; no se vuelcan inventories completos en operaciones rutinarias.
- `InteractionSystem` mantiene silenciosas las consultas puras usadas por refresh/revalidation. El detalle de availability se emite solamente por la ruta debug explicita `LogAvailabilityDetails`; los fallos de construccion del contexto siguen visibles.
- Este contrato no introduce telemetry, analytics, file logger, cache global ni framework universal. Modificar un failure boundary exige revisar tambien la calidad y frecuencia de sus mensajes.

## Visual Rig Y Attachments

- Equipment conserva autoridad exclusiva sobre storage, ownership, slots e `InstanceId`. El visual consume `EquipmentVisualStateSnapshot`, una copia read-only de una revision confirmada.
- `EntityEquipmentVisualSynchronizer` combina ese snapshot con `EntityVisualRigRuntime` y perfiles del `GameDatabase`; mantiene como maximo un visual por `InstanceId`.
- Parts y sockets son miembros declarados dentro de `VisualRigProfileDefinition`. Capabilities, visual assets, item visual profiles y attachment poses pertenecen a familias de definicion separadas. Offsets y compatibilidad no se hardcodean en el synchronizer.
- `IVisualAssetProvider` separa asset keys data-driven de la carga. El slice actual implementa solamente el provider `builtin`; AssetBundles y Mod Kit no forman parte del contrato actual.
- Visuales equipados son presentacion reemplazable: no contienen gameplay, storage, ownership, colliders ni rigidbodies.

## UI Y Superficie Funcional Actual

- `InventoryUISessionController` conserva autoridad unica sobre sesion, input, modales, menu contextual y drag.
- `PersonalStorageNavigator` selecciona owners accesibles; no posee datos ni placements.
- Los panels actuales usan `OnGUI` y son UI debug, no framework ni UI de produccion.
- La superficie funcional de M35.2 termina despues de Unified Corpse Belongings Surface: Equipment e inventario raiz se presentan juntos sin fusionar sus backends.
- Universal Corpse Item Actions, revisita de cuerpos vacios y multiples ventanas flotantes no son contratos actuales; quedaron reclassified/deferred en el Roadmap.

## Limites Tecnicos Del Slice

- Actor needs avanza con `Time.deltaTime`; no existe world clock persistente ni offline progression.
- Health es un valor escalar con tags derivados; no existe daño localizado, heridas, sangrado, dolor, armadura ni penetracion integrados.
- `FirearmDebugController` es un prototipo debug de arma de fuego, no el contrato final de combate.
- No existe actor runtime registry, spawn/lifecycle durable, IA, navegacion de NPCs, clima, ecologia, facciones, sectorizacion ni proceduralidad runtime.
- La UI es debug OnGUI y no debe expandirse como si fuera la UI final.
- Los mods son aditivos y sin overrides/versiones; el soporte de compatibilidad de produccion pertenece a un milestone posterior.

## Frontera M36.1 / M37

- M36.1 Checkpoint A validado y cerrado implementa identidad durable de items, invariantes, hydration detached, cleanup terminal, transiciones comprometidas de ownership y diagnostico determinista. Mauro confirmo manualmente los flujos del slice sin duplicaciones ni ownership exceptions.
- Checkpoint B implementa identidad authored para 14 roots stateful y 2 world items, con apply/validator Editor idempotente. Runtime/Editor compilaron, Foundation Identity y Checkpoint A dieron `PASS`; Mauro validó manualmente los flujos authored principales y `Foundation Freeze` está `APPROVED`. M37.0 quedó validado y M37.1 consume y rehidrata esas identidades sin reinterpretarlas.
- M37 debe persistir y rehidratar el `Condition` get-only exacto, sin implementar condition mutable.
- Items no stackeables y stacks visibles poseen identidad durable; las unidades fungibles internas conservan cantidad sin identidad individual.
- M36.1 no implementa save/load, condition mutable, repair/disassembly, actor lifecycle, gameplay nuevo ni UI final.
- M37 persiste primero el slice real: jugador, items, inventory/grid, Equipment, ownership, item-owned storages, containers, cuerpos, puertas, world items y runtime tags existentes.
- M37 no pre-serializa actores futuros, clima, facciones, economia regional o mundo procedural hipotetico.
