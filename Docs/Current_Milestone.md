# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronologia y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Milestone Activo

### M36.1 — Foundation Freeze & Persistent Identity Contract

Version actual:

`Diagnostic Console Observability Pass 1`

Estado inicial:

`IN PROGRESS — CHECKPOINT B IMPLEMENTED;`

`AUTOMATED FOUNDATION VALIDATION PASSED;`

`MANUAL UNITY VALIDATION PENDING;`

`FOUNDATION FREEZE REVIEW BLOCKED`

Estado actual:

`IN PROGRESS — CHECKPOINT B IMPLEMENTED;`

`AUTOMATED FOUNDATION VALIDATION PASSED;`

`DIAGNOSTIC CONSOLE OBSERVABILITY PASS COMPLETE;`

`FOUNDATION FREEZE REVIEW BLOCKED`

Objetivo: congelar identidad durable de items y del contenido authored del slice actual, junto con los contratos minimos de creacion, futura rehidratacion, stacking, ownership y rollback que consumira M37, sin implementar save/load.

## Resultado De Checkpoint A

- `InstanceId` sigue siendo `string`, get-only y opaco; los IDs nuevos usan `item_<GUID N lowercase>`.
- `CreateNew` reserva una identidad nueva y conserva la ruta funcional completa: adjunta, inicializa y registra su item-owned storage cuando corresponde.
- `Rehydrate` reserva exactamente la identidad y el `Condition` cargados y permanece detached; su item-owned storage puede adjuntarse sin publicar, poblarse con layout pendiente, validarse y registrarse de forma explicita.
- `ItemInstanceIdRegistry` conserva solamente IDs activos y se reinicia junto con `ItemOwnedStorageRegistry` al comenzar una sesion runtime.
- el constructor de `ItemOwnedStorageRuntime` no registra globalmente por side effect; registros y bindings duplicados ahora fallan de forma explicita.
- el bootstrap inicial de `ContainerLootComponent` usa `GridInventoryBackend.Add`, comprueba la cantidad afectada, registra owners de entries y restaura storage e IDs si falla el lote.
- un stack usa una `ItemInstance` representativa; `Condition` participa en `CanStackWith`.
- split conserva la fuente y crea un sibling; merge conserva el destino y retira la fuente consumida despues del commit; un merge total por `Add` no deja el ID candidato activo.
- un `Remove` terminal rechaza de forma atomica un item-owned storage no vacio con `OwnedStorageNotEmpty`; despues de vaciarlo, retira owner, storage e identidades.
- los scopes de reserva nested transfieren IDs al padre y liberan solamente identidades nuevas durante rollback; transfer, drop y equip/unequip preservan identidad.
- `BindItem` y `BindEntries` continúan estrictos; las transiciones legítimas usan un handoff explícito que exige el owner fuente esperado, acepta idempotencia sólo en el target correcto y rechaza un tercer owner.
- los transfers confirmados reconcilian source y target en dos fases usando el resultado final de storage: full move transfiere el ID, split registra el sibling, full merge conserva el destino y retira la fuente consumida, y rollback reconstruye los bindings restaurados.
- `WorldItemPickup.PickUp` transfiere mediante `GridStorageTransferService` y finaliza tags, colliders, renderers y feedback únicamente después del commit de storage y ownership.
- Equipment usa una regla única de direct owner: inventario personal y Equipment del actor conservan el `InventoryComponent` canónico; las rutas world/item-owned transfieren ownership explícitamente y restauran storage, slots y owner ante fallo.
- las superficies proxy de actor resuelven al inventario canónico; ownership se valida para todas las entries afectadas y `InventoryMutationResult` conserva el fallo localizado `OwnedStorageNotEmpty`.
- las creaciones directas restantes son acotadas: `WorldItemPickup` exige storage vacio antes de bindear entries y `DebugInventory` conserva cada instancia en su lista.

## Evidencia Y Validacion

- `Diagnostic Console Observability Pass 1` deja las consultas puras de availability sin logs rutinarios; el detalle permanece disponible solo cuando `LogAvailabilityDetails` activa la ruta debug explicita.
- Los failures de authored world items, transfers/ownership, Equipment, inventario y containers incluyen IDs, owners, estado de commit/rollback y causa proporcional cuando esos datos existen; los commits importantes permanecen compactos.
- Los containers correlacionan inicializacion con GameObject, root, `PersistentSceneObjectId`, loot table, entries y cantidad total sin volcar el inventario completo.
- El pase no cambia gameplay, identidad, ownership, Equipment, transacciones, `SampleScene`, JSON ni save/load.
- Mauro valido manualmente crowbar y Lee-Enfield authored, pickup, equip directo desde el mundo, inventario y drop; no observo errores funcionales nuevos de Old Scars.
- Unity batchmode recompilo Runtime y Editor con `Tundra build success`, codigo 0 y sin `error CS`/`warning CS` en las dos corridas del pase.
- `M36.1 Checkpoint A Item Identity Diagnostics: PASS`; sus dos rollbacks intencionales registraron contexto accionable y `RollbackSucceeded: True` una sola vez por failure.
- `M36.1 Foundation Identity Validation: PASS`: actors 3, doors 3, containers 8, authored roots 14, authored world item IDs 2, duplicados 0 e invalidos 0.
- Ninguna corrida emitio `[InteractionSystem] No equipped item.` ni `[InteractionSystem] Available actions:` desde consultas puras.

- Runtime y Editor compilaron sin errores en Unity 6.4.6f1; la recompilacion final sin el runner temporal termino con `Tundra build success` y recarga de dominio.
- Diagnostico `Old Scars > Diagnostics > M36.1 > Run Checkpoint A Item Identity` (`Ctrl+Shift+I`): `M36.1 Checkpoint A Item Identity Diagnostics: PASS`.
- El diagnostico cubre source esperado incorrecto, world→inventory, Equipment, container/item-owned storage, full move, split, full merge y rollback de ownership; termina sin estado residual.
- El smoke real de Play Mode produjo `M36.1 Checkpoint A Real Scene Ownership Smoke: PASS` sobre crowbar, rifle, mochila y crate: pickup, equip/unequip, storage round-trip, drop/re-pick y stack transfer conservaron identidad y una sola representación.
- Play Mode se cerró correctamente; Console no registró `InvalidOperationException`, `already bound to a different owner` ni errores relacionados con M36.1.
- Persisten seis warnings preexistentes: cuatro de API obsolete en `BuildingVisibilityManager` y dos campos no usados en `ItemStorageDebugPanel`.
- `SampleScene` permanecio intacta durante Checkpoint A; su identidad authored se aplica exclusivamente en Checkpoint B.
- Mauro confirmo manualmente pickup desde el mundo con desaparicion de la representacion mundial y sin duplicados.
- Equip desde inventario y equip directo desde el mundo funcionaron; equip/unequip preservaron la misma identidad.
- Transfers entre inventario, mochila, containers y cuerpos preservaron ownership e identidad sin duplicaciones.
- Drop y re-pickup de una mochila no vacia preservaron su contenido; rifle, crowbar y mochila conservaron su `InstanceId`.
- No aparecieron `InvalidOperationException`, `already bound to a different owner` ni errores funcionales relacionados con M36.1.
- Los errores observados de Unity Relay pertenecen a `com.unity.ai.assistant` y se separan de la validacion del runtime de Old Scars.

## Checkpoint B

`IMPLEMENTED — AUTOMATED FOUNDATION VALIDATION PASSED`

- `PersistentSceneObjectId` identifica exactamente 14 roots stateful de `SampleScene`: 3 actores, 3 puertas y 8 contenedores.
- `Debug Strange Machine`, visuales y children quedan excluidos.
- `Debug World Crowbar` conserva `rusted_crowbar_01` y usa `item_4c1952809f1a4968ac86384b5a331201`.
- `Debug World Lee-Enfield Rifle` conserva `lee_enfield_rifle_01` y usa `item_c0f66d58249e4892aa4632028975816e`.
- `ItemInstance.CreateAuthored` reserva el ID exacto; los drops runtime conservan su `ItemInstance` y no reciben authored IDs nuevos.
- `WorldItemPickup` distingue database no disponible, definition inexistente e identidad authored invalida; un fallo de identidad ya no emite el warning secundario falso de definition/data readiness.
- El tool Editor aplica la tabla aprobada, valida antes de guardar, revierte la escena en memoria ante fallo y es idempotente.

## Evidencia Automatizada De Checkpoint B

- Unity 6.4.6f1 compilo Runtime y Editor con `Tundra build success` y codigo de salida 0.
- `M36.1 Foundation Identity Validation: PASS`: actors 3, doors 3, containers 8, authored roots 14, authored world item IDs 2, duplicados 0 e invalidos 0.
- El validator paso nuevamente despues de reabrir `SampleScene` desde disco.
- La reaplicacion fue idempotente (`changed: false`) y preservo el SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`.
- `M36.1 Checkpoint A Item Identity Diagnostics: PASS`.
- El diff de `SampleScene` contiene solamente los 14 componentes/referencias de identidad y dos overrides `authoredItemInstanceId`; no cambia transforms, jerarquia, colliders, renderers, materiales, camara, iluminacion, loot o UI.
- La evidencia manual que disparo el recovery confirmo que `GameDatabase` cargaba 8 items con 0 errors y 0 warnings; la causa era la ausencia de authored IDs serializados.
- La validacion manual de Checkpoint B por Mauro fue exitosa: ambos authored world items y sus flujos de pickup/equip/inventario/drop funcionaron sin errores nuevos observados.

## Estado De Gates Y Secuencia

- `Foundation Freeze`: no aprobado; Checkpoints A/B, la validacion manual y el pase de observabilidad aportan evidencia, pero la revision final permanece pendiente.
- R03 permanece `MITIGATING`.
- M37.0 no comenzo y sigue bloqueado hasta validar/revisar M36.1.
- milestone anterior: M36.0 — `DONE — DOCUMENTATION REVIEWED`, commit de cierre `461b1b6508ef234777b82ccea97624b5b94b428c`.

No iniciar M37, condition mutable, repair, actor lifecycle, save/load, gameplay nuevo ni UI final antes de la revision final de `Foundation Freeze`.
