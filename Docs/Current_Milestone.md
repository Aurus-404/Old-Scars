# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Milestone Activo

### ID TBD — Global Content ID Namespace Foundation

No se asigna un número por inferencia: no existe un ID libre reservado para esta unidad y la regla vigente del Roadmap exige `ID TBD`. Es una unidad técnica interpuesta y acotada; no es M37.2 ni adelanta M50.0.

Estado actual:

`IMPLEMENTED — STATIC/DATA VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`

M37.1 permanece abierto con su estado previo `IMPLEMENTED — AUTOMATED ROUND-TRIP VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`. `Persistence Ready` continúa `NOT YET APPROVED`.

## Contrato Implementado

- `ContentId` es el contrato central para Global Content IDs canónicos `namespace:local_id`; ambos segmentos aceptan sólo letras ASCII minúsculas, dígitos y `_`, sin trim ni lowercase silenciosos.
- `core` es el namespace reservado del contenido oficial. `Mods/Core` usa el mismo parser, normalizador, registries y validator que una fuente externa; no existe bypass de validación.
- `DefinitionContentIdNormalizer` recorre en la frontera de carga únicamente IDs y referencias que apuntan a los 16 registries globales actuales. Los mods externos deben declarar IDs canónicos explícitos; el nombre de carpeta aún no prueba ownership del namespace.
- `GameDatabase` registra sólo IDs canónicos y resuelve consultas por una única clave. Un alias legacy no crea una segunda entry ni puede coexistir accidentalmente con su forma `core:*`.
- `DataValidator` distingue `Global Content ID` de `Local ID` y conserva dominios separados para tags, asset keys e identidades runtime/persistentes.
- Los JSON oficiales de `Mods/Core` migraron sus Definition IDs y referencias globales a `core:*`, incluyendo items, actions, loot, actor/world profiles, weapons/ammo, storage/equipment y visuals/poses.
- `ItemInstance.InstanceId`, `PersistentSceneObjectId`, storage IDs compuestos, save slot IDs y tags no fueron namespaced.
- El diagnóstico Editor `Old Scars > Diagnostics > Content IDs > Run Namespace Foundation` usa una fixture temporal, no contamina `StreamingAssets`, y cubre parser, errores, compatibilidad Core, rechazo legacy externo, identidad canónica, coexistencia `core:test_item` / `test_namespace:test_item` y referencia cross-namespace.

## Compatibilidad Legacy Y Saves

- Sólo el contexto explícito de carga Core puede convertir una referencia sin namespace, por ejemplo `bandage_01` → `core:bandage_01`; produce warning agregado y la ruta está documentada como temporal/removible.
- Los lookups legacy de escenas/prefabs y los tres campos Global Content ID de saves schema v1 se resuelven explícitamente contra Core: `ItemState.definitionId`, `EquipmentState.layoutId` y `EquippedItemState.slots`.
- La compatibilidad histórica específica `right_hand` → `core:hand_right` se limita a referencias legacy de Equipment; no define un alias canónico general.
- El snapshot se normaliza en memoria antes de semantic preflight. No se incrementó `schemaVersion`; el siguiente save escribe las referencias canónicas provenientes de definitions/runtime.
- No se afirma compatibilidad de saves como validada hasta ejecutar el diagnóstico y el round-trip manual en Unity.

## Validación Disponible

- todos los JSON del repositorio parsean con `jq`;
- auditoría tipada de Core: todos los Global Content IDs son canónicos y todas las referencias auditadas resuelven en su registry destino;
- auditoría de hardcodes: los valores sin namespace restantes corresponden a fixtures negativos, diagnostics legacy, scene/prefab compatibility o tokens locales de effects;
- parseo sintáctico de todos los C# modificados con grammar C#: `PASS`;
- `git diff --check`: `PASS`;
- no existen workflows de GitHub Actions aplicables en el repositorio.

`Manual Unity validation pending`

Checklist manual obligatorio:

1. Abrir el proyecto en Unity 6.4.6f1 y confirmar compilación Runtime/Editor sin errores nuevos.
2. Abrir `SampleScene` en una sesión fresca, entrar a Play Mode y confirmar `CoreDataSystem ready`, cero errores de data y sólo warnings legacy explicables para campos authored todavía no migrados.
3. Ejecutar `Old Scars > Diagnostics > Content IDs > Run Namespace Foundation` y exigir `PASS`.
4. Ejecutar los diagnostics existentes M36.1/M37.0/M37.1, incluido `Current Slice Persistent Round-Trip`, y exigir `PASS`.
5. Probar `Save Debug Slot`, salir completamente de Play Mode, volver a entrar y ejecutar `Load Debug Slot`; verificar Definition IDs/layout/slots canónicos, inventario, Equipment, owned storage, visuals, world items, containers, puertas, health/needs y ausencia de duplicados.
6. Confirmar que `SampleScene` no queda dirty y revisar Console antes de aprobar esta unidad o `Persistence Ready`.

## Próximo Trabajo

- completar el checklist manual de esta unidad;
- volver al closeout manual fresh-session de M37.1;
- mantener M38.0 bloqueado hasta ambos cierres;
- continuar después, en unidades separadas, con `manifest → provenance → dependencies → patches` sin marcar esas capacidades futuras como implementadas.
