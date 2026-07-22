# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronologia y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Milestone Activo

### M36.1 — Foundation Freeze & Persistent Identity Contract

Version actual:

`Checkpoint A — Correction Pass 2: Committed Ownership Transitions`

Estado inicial:

`PLANNED — REVISED ARCHITECTURE PLAN READY FOR IMPLEMENTATION AUTHORIZATION`

Estado actual:

`IN PROGRESS — CHECKPOINT A CORRECTION PASS 2 IMPLEMENTED;`

`MANUAL REVALIDATION PENDING;`

`CHECKPOINT B NOT STARTED`

Objetivo: reemplazar la identidad temporal de `ItemInstance` por IDs durables y congelar los contratos minimos de creacion, futura rehidratacion, stacking, split, merge, item-owned storage, ownership y rollback que consumira M37.

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

- Runtime y Editor compilaron sin errores en Unity 6.4.6f1; la recompilacion final sin el runner temporal termino con `Tundra build success` y recarga de dominio.
- Diagnostico `Old Scars > Diagnostics > M36.1 > Run Checkpoint A Item Identity` (`Ctrl+Shift+I`): `M36.1 Checkpoint A Item Identity Diagnostics: PASS`.
- El diagnostico cubre source esperado incorrecto, world→inventory, Equipment, container/item-owned storage, full move, split, full merge y rollback de ownership; termina sin estado residual.
- El smoke real de Play Mode produjo `M36.1 Checkpoint A Real Scene Ownership Smoke: PASS` sobre crowbar, rifle, mochila y crate: pickup, equip/unequip, storage round-trip, drop/re-pick y stack transfer conservaron identidad y una sola representación.
- Play Mode se cerró correctamente; Console no registró `InvalidOperationException`, `already bound to a different owner` ni errores relacionados con M36.1.
- Persisten seis warnings preexistentes: cuatro de API obsolete en `BuildingVisibilityManager` y dos campos no usados en `ItemStorageDebugPanel`.
- `SampleScene` permanece intacta con SHA-256 `7EBB6605CBFE564F17CA5CAC7BA46348A1CDE887CC3462086DAE1D2B602A1AFB`.
- La revalidacion manual del slice por Mauro permanece pendiente; no se afirma validacion funcional final.

## Checkpoint B

`NOT STARTED`

Debe completar identidad authored y evidencia del slice para actores/objetos mundiales sin convertir Checkpoint A en save/load. `SampleScene`, prefabs, JSON, Packages y ProjectSettings permanecen intactos.

## Estado De Gates Y Secuencia

- `Foundation Freeze`: no aprobado; evidencia parcial de Checkpoint A solamente.
- R03 permanece `MITIGATING`.
- M37.0 no comenzo y sigue bloqueado hasta completar/revisar M36.1.
- milestone anterior: M36.0 — `DONE — DOCUMENTATION REVIEWED`, commit de cierre `461b1b6508ef234777b82ccea97624b5b94b428c`.

No iniciar Checkpoint B, M37, condition mutable, repair, actor lifecycle, save/load, gameplay nuevo ni UI final sin una autorizacion posterior.
