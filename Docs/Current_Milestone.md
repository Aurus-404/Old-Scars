# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronologia y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Milestone Activo

### M36.1 — Foundation Freeze & Persistent Identity Contract

Version actual:

`Checkpoint A — Durable Item Identity and Stack Contracts`

Estado inicial:

`PLANNED — REVISED ARCHITECTURE PLAN READY FOR IMPLEMENTATION AUTHORIZATION`

Estado actual:

`IN PROGRESS — CHECKPOINT A IMPLEMENTED; CHECKPOINT B PENDING`

Objetivo: reemplazar la identidad temporal de `ItemInstance` por IDs durables y congelar los contratos minimos de creacion, futura rehidratacion, stacking, split, merge, item-owned storage, ownership y rollback que consumira M37.

## Resultado De Checkpoint A

- `InstanceId` sigue siendo `string`, get-only y opaco; los IDs nuevos usan `item_<GUID N lowercase>`.
- `CreateNew` reserva una identidad nueva; `Rehydrate` reserva exactamente la identidad y el `Condition` cargados y devuelve un item detached.
- `ItemInstanceIdRegistry` conserva solamente IDs activos y se reinicia junto con `ItemOwnedStorageRegistry` al comenzar una sesion runtime.
- el constructor de `ItemOwnedStorageRuntime` no registra globalmente por side effect; registros y bindings duplicados ahora fallan de forma explicita.
- un stack usa una `ItemInstance` representativa; `Condition` participa en `CanStackWith`.
- split conserva la fuente y crea un sibling; merge conserva el destino y retira la fuente consumida despues del commit.
- los scopes de reserva nested transfieren IDs al padre y liberan solamente identidades nuevas durante rollback; transfer, drop y equip/unequip preservan identidad.
- ownership se reconcilia para todas las entries afectadas sin cambiar `InventoryMutationResult`.

## Evidencia Y Validacion

- Runtime y Editor compilaron sin errores en Unity 6.4.6f1.
- Diagnostico `Old Scars > Diagnostics > M36.1 > Run Checkpoint A Item Identity` (`Ctrl+Shift+I`): `PASS`.
- El diagnostico termina con cero IDs, storages y owners registrados.
- Un smoke de Play Mode cargo los datos con `0 errors, 0 warnings` y no produjo excepciones relacionadas con M36.1; al salir, el servicio Unity Relay registro un `TaskCanceledException` externo al gameplay.
- Se revisaron estaticamente add/remove, split, transfer, directed merge, Equipment, owned storage y ownership.
- Persisten seis warnings preexistentes: cuatro de API obsolete en `BuildingVisibilityManager` y dos campos no usados en `ItemStorageDebugPanel`.
- La validacion manual del slice por Mauro permanece pendiente; no se afirma validacion funcional final.

## Checkpoint B

`NOT STARTED`

Debe completar identidad authored y evidencia del slice para actores/objetos mundiales sin convertir Checkpoint A en save/load. `SampleScene`, prefabs, JSON, Packages y ProjectSettings permanecen intactos.

## Estado De Gates Y Secuencia

- `Foundation Freeze`: no aprobado; evidencia parcial de Checkpoint A solamente.
- R03 permanece `MITIGATING`.
- M37.0 no comenzo y sigue bloqueado hasta completar/revisar M36.1.
- milestone anterior: M36.0 — `DONE — DOCUMENTATION REVIEWED`, commit de cierre `461b1b6508ef234777b82ccea97624b5b94b428c`.

No iniciar Checkpoint B, M37, condition mutable, repair, actor lifecycle, save/load, gameplay nuevo ni UI final sin una autorizacion posterior.
