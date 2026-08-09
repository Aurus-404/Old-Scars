# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximos Tres Milestones

### 1. ID TBD — Global Content ID Namespace Foundation

Estado: `IMPLEMENTED — STATIC/DATA VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Objetivo inmediato:

- compilar Runtime y Editor en Unity;
- ejecutar `Old Scars > Diagnostics > Content IDs > Run Namespace Foundation` y exigir `PASS`;
- cargar `Mods/Core` en `SampleScene`, revisar errores/warnings y verificar interactions, Inventory, Equipment y visuals con IDs `core:*`;
- ejecutar la cobertura legacy schema-v1 agregada al diagnóstico M37.1 y no afirmar compatibilidad de saves antes de ese resultado;
- cerrar la unidad sin asignarle retrospectivamente un número no reservado.

Fuera de alcance:

- manifests, dependency resolver, load-order definitivo, overrides/patches, Workshop, SDK, scripting, DLL mods, hot reload o AssetBundles;
- namespace masivo de tags;
- cambios de escenas/prefabs únicamente para silenciar la compatibilidad temporal.

Salida: contrato de Global Content IDs validado en Unity, o bloqueo documentado con el error real.

### 2. M37.1 — Current Slice Persistent Round-Trip

Estado: `IMPLEMENTED — AUTOMATED ROUND-TRIP VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Versión siguiente del mismo milestone: `Manual Unity Validation & Persistence Ready Closeout`.

Objetivo:

- guardar mediante `Save Debug Slot`, salir completamente de Play Mode y entrar a una sesión fresca;
- esperar bootstrap normal, ejecutar `Load Debug Slot` y verificar identidades, quantities/placements, Equipment, owned storage, world state, doors, health/needs y player pose;
- confirmar que un payload schema v1 con Definition IDs/layout/slots Core sin namespace se normaliza y carga sin subir versión;
- cerrar `Persistence Ready` solamente si Mauro confirma el resultado sin errores funcionales relacionados.

Fuera de alcance:

- autosave, UI final de save/load, cloud y profiles;
- actor lifecycle futuro, clima, facciones o proceduralidad;
- gameplay nuevo fuera del round-trip del slice existente.

Salida: decisión documentada del gate `Persistence Ready` y cierre de M37.1 si la validación manual es exitosa.

### 3. M38.0 — Actor Runtime & Lifecycle V1

Estado: `PLANNED — BLOCKED BY ID TBD AND M37.1 CLOSEOUT`.

Objetivo:

- definir IDs, spawn y lifecycle durable de actores sobre `Persistence Ready` y Global Content IDs canónicos;
- integrar muerte y cuerpos persistibles sin reabrir los contratos congelados de M36.1;
- evitar nuevas referencias de contenido global sin namespace.

Fuera de alcance:

- necesidades/world clock de M38.1;
- combate, IA o UI final;
- mundo o contenido a escala.

## Secuencia De Modding Posterior

La cimentación actual no implementa el sistema completo. Las próximas piezas se planifican como unidades separadas en este orden conceptual:

`manifest → provenance → dependencies → patches`

M50.0 conserva el alcance de compatibilidad de producción; esta unidad `ID TBD` no lo sustituye ni lo marca iniciado.

## No Iniciar Todavía

- nuevas ampliaciones OnGUI;
- UI final;
- combate o IA;
- condition, repair o crafting;
- actores o mundo a escala;
- facciones amplias;
- generación procedural;
- producción masiva de contenido.
