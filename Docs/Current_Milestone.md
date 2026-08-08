# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Milestone Activo

### M37.1 — Current Slice Persistent Round-Trip

Versión implementada:

`Transactional Rehydration & Real-Scene Round-Trip Pass 2`

Estado actual:

`IMPLEMENTED — AUTOMATED ROUND-TRIP VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`

## Contrato Implementado

- `CurrentSliceSaveData` define DTOs planos y explícitos para player, items, storages, Equipment, containers, corpses, doors y world items; pose usa floats propios y no serializa componentes ni objetos Unity.
- la tabla única de items conserva `InstanceId`, `DefinitionId` y `Condition`; stacks conservan una identidad representativa, `Quantity` y placement/orientación exactos.
- storages usan referencias durables por actor/container/item owner y cubren inventory, Equipment, item-owned storage, containers authored y cuerpos actualmente muertos.
- player conserva identidad authored, pose mundial, health escalar, hunger/thirst, Inventory, Equipment y owned storages. Tags de health, peso y visuales permanecen derivados.
- containers se capturan siempre con storage autoritativo explícito, incluso vacío, y sólo conservan los tags mutables de apertura, descubrimiento y contenido.
- cada authored world item conserva un marker present/absent por su item ID; los drops runtime conservan identidad, cantidad y pose. Un authored item lazy se proyecta sin crear una `ItemInstance` ni reservar IDs.
- puertas conservan sólo `opened_door`, `closed_door` o `locked_door`; el ángulo visual no forma parte del snapshot.
- semantic preflight valida schema, referencias de escena y definiciones, unicidad/localización, cantidades, placements, Equipment multi-slot, item-owned storage sin nesting, containers, corpses, doors y world representations.
- el comparador canónico ignora orden incidental y tolera `0.0001` en poses, pero reporta la primera diferencia semántica accionable.
- `Save Debug Slot` está disponible sólo en Play Mode y usa capture + preflight + `PersistenceFileStore.Write` sobre `m37_current_slice_debug`.
- `Load Debug Slot` usa el mismo pipeline real que diagnostics: read, semantic preflight, resolución de escena, snapshot de rollback, teardown selectivo, apply, recapture y comparación canónica.
- apply rehidrata cada item una vez mediante `ItemInstance.Rehydrate`, adjunta y valida item-owned storage antes de registrarlo, restaura storages/placements, Equipment y ownership, y reconcilia authored world items y runtime drops sin generar IDs sustitutos.
- containers restauran contenido autoritativo incluso vacío y quedan marcados inicializados para impedir reseed. Corpses restauran solamente health, Inventory, Equipment y owned storage cuando el root ya está muerto; NPCs vivos y lifecycle general permanecen fuera del slice.
- doors restauran el tag lógico y sincronizan el controlador visual cuando existe. Health/needs del player y su pose se aplican al final con cancelación de movimiento y `CharacterController` temporalmente deshabilitado.
- un fallo posterior a la primera mutación ejecuta `ApplyCore` con el snapshot pre-load, sin rollback recursivo. `RollbackFailed` conserva ambas causas y nunca se presenta como un load seguro.

## Evidencia Automatizada

- Unity 6.4.6f1 compiló Runtime y Editor con `Tundra build success` y retorno 0.
- `M37.0 Persistence Core Diagnostics: PASS`.
- `M36.1 Foundation Identity Validation: PASS` después del seam authored de sólo lectura.
- `M37.1 Snapshot & Semantic Preflight Diagnostics: PASS` sobre el slice real en Play Mode, con save/read temporal, preflight post-read, comparación canónica y casos negativos requeridos.
- `M36.1 Checkpoint A Item Identity Diagnostics: PASS`.
- `M37.1 Current Slice Persistent Round-Trip Diagnostics: PASS`: preparó un State A real con player pose/health/needs, pickups authored, Lee-Enfield equipada, stack dentro de backpack, container, corpse equipado, door y runtime drop; mutó State B, cargó A y obtuvo equivalencia canónica.
- el fault Editor-only posterior a storage restore produjo `ApplyFailed`, `RollbackAttempted: true`, `RollbackSucceeded: true` y runtime final equivalente al snapshot pre-load.
- el diagnóstico representó y round-trippeó un container vacío sin mutar gameplay; limpió su root temporal y `SampleScene` conservó SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`.
- persisten sólo los seis warnings preexistentes: cuatro `CS0618` en `BuildingVisibilityManager` y dos `CS0414` en `ItemStorageDebugPanel`.

## Estado De Gates Y Próximo Trabajo

- `Foundation Freeze`: `APPROVED`.
- `Persistence Ready`: `NOT YET APPROVED`.
- el próximo trabajo inmediato es `M37.1 — Manual Unity Validation & Persistence Ready Closeout` mediante Save/Load Debug Slot en una sesión fresca.
- la implementación automatizada no sustituye la validación manual ni autoriza cerrar el gate.
- M38.0 permanece bloqueado y no fue iniciado.
