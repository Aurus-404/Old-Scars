# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronologia y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Milestone Cerrado Y Siguiente Trabajo

### M37.0 — Save Format & Persistence Core

Version:

`Persistence Core V1 — Functional Implementation Pass 1`

Estado inicial:

`PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`

Estado actual:

`DONE — PERSISTENCE CORE VALIDATED`

## Contratos Implementados

- `CurrentFormatVersion = 1`; el envelope usa nombres JSON estables `formatVersion`, `writtenUtc` y `payload`.
- `payload` es un `JToken` desacoplado; M37.0 no conoce ni serializa `ItemInstance`, actores, componentes, objetos Unity o estado de escena.
- `PersistenceSerializer` usa Newtonsoft.Json con configuracion exclusiva de saves, valida JSON, envelope, version y payload, rechaza versiones futuras y expone un seam explicito de migrations consecutivas.
- `PersistenceFileStore` usa por defecto `Application.persistentDataPath/Saves` y admite un root configurado para diagnostics; los slots aceptan solamente IDs cerrados snake_case de hasta 64 caracteres.
- cada slot conserva como maximo primary `<slot>.json`, backup `<slot>.json.bak` y temp `<slot>.json.tmp`.
- el primer write serializa y valida en memoria, escribe y fuerza flush del temp en el mismo directorio y lo promueve por rename; overwrite usa `File.Replace(temp, primary, backup)` cuando la plataforma lo soporta.
- el fallback por falta real de soporte conserva primero el primary como backup antes de promover el temp; no se afirma atomicidad universal.
- load usa primary valido, recupera desde backup ante primary ausente/corrupto, preserva evidencia invalida y distingue causas mediante failure codes. Versiones futuras o antiguas sin migration se rechazan sin rollback silencioso a un backup viejo.

## Evidencia Y Validacion

- Unity 6.4.6f1 compilo Runtime y Editor con `Tundra build success` y retorno 0.
- Persisten seis warnings preexistentes y fuera del alcance: cuatro `CS0618` en `BuildingVisibilityManager` y dos `CS0414` en `ItemStorageDebugPanel`; M37.0 no agrego warnings.
- `M37.0 Persistence Core Diagnostics: PASS` con retorno 0.
- El diagnostico cubrio envelope V1, first write/read, overwrite mediante `File.Replace`, backup anterior, recovery, doble corrupcion, future version, migration ausente, slot invalido, temp cleanup y payload exacto.
- El diagnostico uso exclusivamente un root unico bajo el directorio temporal del sistema y termino sin directorios o archivos temporales de test.
- No se modificaron gameplay, `SampleScene`, prefabs, JSON, Packages, ProjectSettings ni los contratos congelados de M36.1.
- Manual Unity validation: `NOT APPLICABLE`; M37.0 no integra gameplay y su contrato filesystem/serialization/recovery quedo cubierto por batchmode.

## Estado De Gates Y Secuencia

- `Foundation Freeze`: `APPROVED`.
- `Persistence Ready`: `NOT YET APPROVED`; pertenece al cierre de M37.1.
- M37.1 — Current Slice Persistent Round-Trip queda `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`, pero no comenzo.
- M37.1 debe construir snapshots del slice real y usar `Rehydrate`/identidad authored/ownership sin reescribir el envelope o filesystem base de M37.0.

No iniciar autosave, UI save/load, cloud, profiles, snapshots hipoteticos, actor lifecycle o gameplay nuevo dentro del cierre de M37.0.
