# Old Scars - Project Roadmap

## Autoridad Del Documento

Este archivo es la autoridad canonica para:

- IDs reservados y aliases historicos;
- estado actual de cada milestone;
- dependencias y orden de ejecucion;
- horizontes de produccion;
- nombres y ubicacion de los gates.

`Current_Milestone.md` resume este estado, `Next_Sprints.md` deriva la cola inmediata y `Development_Log.md` conserva la cronologia append-only. Ninguno de esos documentos puede reasignar IDs ni contradecir el estado publicado aqui.

Mauro conserva la autoridad creativa y de producto. [Game_Design_Document.md](Game_Design_Document.md) contiene el baseline de diseño revisado; el GDD Maestro v3.1 externo se conserva como fuente historica y de diseño auditada. [Technical_Architecture.md](Technical_Architecture.md) mantiene la autoridad sobre contratos tecnicos vigentes despues de contrastarlos con el codigo real. Este roadmap no sustituye esas fuentes ni convierte implementaciones en diseño final.

## Estado De Produccion

| Campo | Estado canonico |
| --- | --- |
| Milestone cerrado mas reciente | M38.0 — Actor Runtime & Lifecycle V1 |
| Estado M37.0 | `DONE — PERSISTENCE CORE VALIDATED` |
| Ultimo milestone funcional cerrado | M35.2 — Lootable Entity Inventory UI V1 |
| Estado M35.2 | `DONE — FUNCTIONAL SCOPE CLOSED AFTER M35.2.3` |
| Ultimo submilestone validado | M35.2.3 — Unified Corpse Belongings Surface |
| Commit funcional validado | `27bf438637b621141ca553a39579349a12ff8700` |
| Commit documental de validacion | `2956bcae19719a5f9073e24d58da4705742732fa` |
| Milestone activo | M38.1 — Needs, World Clock & Recovery V1 |
| Estado ID TBD — Global Content ID Namespace Foundation | `VALIDATED — FOUNDATION COMPLETE` |
| Estado M37.1 | `DONE — CURRENT SLICE PERSISTENCE VALIDATED` |
| Persistence Ready | `APPROVED` |
| Siguientes | M38.1 listo para autorizacion de implementacion |

M37.0 queda `DONE — PERSISTENCE CORE VALIDATED` y M37.1 queda `DONE — CURRENT SLICE PERSISTENCE VALIDATED` tras diagnóstico automatizado y round-trip fresh-session manual. `Persistence Ready` está `APPROVED`. ID TBD — Global Content ID Namespace Foundation queda `VALIDATED — FOUNDATION COMPLETE`; no implementa manifests, provenance completa, dependency resolution ni patches/load-order. M38.0 queda `DONE — ACTOR RUNTIME & LIFECYCLE VALIDATED` con automatización y validación manual fresh-session aprobadas; M38.1 queda planificado y listo para autorización de implementación.

## Estados Canonicos

- `PLANNED`: alcance, dependencias y validacion propuestos; trabajo no iniciado.
- `IN PROGRESS`: trabajo autorizado y activo.
- `IMPLEMENTED`: alcance escrito o implementado y verificado estaticamente.
- `PENDING UNITY VALIDATION`: implementacion funcional terminada que requiere prueba manual en Unity.
- `VALIDATED`: prueba de aceptacion requerida ejecutada con evidencia explicita.
- `DONE`: milestone validado o documentalmente aceptado, con cierre y documentos coherentes.
- `DEFERRED`: retirado de la cola activa con motivo y trigger de retorno.
- `BLOCKED`: dependencia o decision externa impide continuar.
- `REJECTED`: excluido deliberadamente del producto o del alcance.

Los calificadores explican el estado sin crear una segunda taxonomia. `VALIDATED` nunca significa solamente que el proyecto compila.

## Reglas De Numeracion

1. Los commits, tags e IDs historicos no se renombran retrospectivamente.
2. Todo ID nuevo se reserva primero en este documento.
3. Una colision se registra en el ledger con nombre canonico, alias, evidencia y disposicion.
4. Un trabajo sin ID libre usa `ID TBD`; no reutiliza un numero historico.
5. Los sufijos legacy existentes se conservan como parte de la historia, pero no fijan la convención futura.
6. Los estados historicos inciertos no se elevan a `VALIDATED` por inferencia.

## Ledger Historico Y De Aliases

### Foundations Y Milestones Validados Antes De M28

Los detalles y la evidencia permanecen en `Development_Log.md`. Estos IDs y nombres quedan reservados y no pueden reutilizarse:

- `CoreDataSystem`, `ActionAvailabilitySystem`;
- M6, M7, M8, M9, M9.1, M10, M11, M12, M12.1, M13, M14, M15, M16, M17, M18;
- M19.1, M19.2, M20, M21, M21.0.1, M22, M22.1, M22.1.1, M22.1.2;
- M23, M23.0.1, M23.0.2, M23.0.3, M23.1, M23.1.1, M23.1.2;
- M24, M24.1, M24.2, M24.3, M24.4, M25, M26, M26.0.1 y M27.

Estado canonico del conjunto: `VALIDATED`, dentro del alcance debug/fundacional documentado para cada milestone.

### IDs Historicos Detectados En Git

| ID historico | Nombre/evidencia Git | Estado o relacion canonica | Disposicion |
| --- | --- | --- | --- |
| M28 | Add ground item drop pickup and restore container visuals — commit `7fb2671030d34fd69f79f7960adeddc65e6caf71` | `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED` | Se conserva el asunto historico del commit y el ID queda reservado. |
| M29 | Lee-Enfield firearm prototype — commit `6c4d6eca7ebf9234db24fbaa0c33f4242e6a965f` | `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED` | El commit demuestra implementacion, pero su cuerpo, el Development Log y las versiones historicas de Current no registran una prueba manual explicita ni una confirmacion de Mauro; el ID queda reservado sin elevar estado. |
| M30 | PSX Style implementation — commit `f756029` | `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED` | ID historico reservado. |
| M30.3 | Usable item prefab migration — commit `b86a616` | `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED` | ID historico reservado. |
| M30.4 | Safe crate editor visuals — commit `6ea3114` | `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED` | ID historico reservado. |
| M31.0 | Player visual and basic animator — commit `254392d` | `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED` | ID historico reservado. |
| M32.2 | Real Door System v0 — commit `a3ba9e5` | `VALIDATED` | `Development_Log.md` registra validacion manual confirmada; el snapshot anterior estaba stale. |
| M32.3 | House container variants — commit `92f57b3` | Alias historico de M32 | No se crea un segundo milestone funcional. |

### Colisiones Y Aliases Reconciliados

| Referencia anterior | Referencia canonica | Decision |
| --- | --- | --- |
| `Milestone 28: Container State / Naming Cleanup v0` planificado | `ID TBD — Container State / Naming Cleanup v0` | M28 ya pertenece al commit historico de ground item drop/pickup. El cleanup queda diferido y sin ID hasta una priorizacion futura. |
| `M32.3 — House Container Variants` en Git | `M32 — Debug Test House Kitchen Containers v0` en el ledger vivo | Se conserva M32.3 como alias de commit; no se renombra Git. |
| `M35.2.3 — Inventory Window Redesign Phase C1` | `M35.2.3 — Unified Corpse Belongings Surface` | `Inventory Window Redesign Phase C1` queda como alias funcional. |
| `M35.2.3.1` como continuacion de UI | `M35.2.3.1 — Universal Corpse Item Actions` | ID conservado, trabajo `DEFERRED — RECLASSIFIED` como futura necesidad transaccional cross-actor. |

## Ledger Funcional Reciente

| Milestone | Estado canonico | Nota |
| --- | --- | --- |
| M32 — Debug Test House Kitchen Containers v0 | `VALIDATED` | `Development_Log.md` registra validacion manual por confirmacion del usuario. |
| M32.2 — Real Door System v0 | `VALIDATED` | `Development_Log.md` registra validacion manual por confirmacion del usuario. |
| M32.4 — Interior Visibility Raycast v0 | `IMPLEMENTED — PENDING UNITY VALIDATION` | Incluido en el batch `420e087d512a24c50b1b8849205b818928649ff8`; no se eleva sin evidencia explicita reconciliada. |
| M32.4.1 — Door Pivot Repair + Interior Visibility Cast Debug/Stability | `IMPLEMENTED — PENDING UNITY VALIDATION` | Incluido en el batch `420e087d512a24c50b1b8849205b818928649ff8`; el cierre manual canonico sigue pendiente. |
| Grid Inventory Backend v0 | `IMPLEMENTED — PENDING UNITY VALIDATION` | ID legacy no numerado; fue incluido en el batch `420e087d512a24c50b1b8849205b818928649ff8`, pero no tiene cierre manual aislado. |
| M33.1, M33.1.1, M33.2, M33.2.1, M33.2.2, M33.3 y M33.3.1 | `VALIDATED` | Base espacial, UI debug y peso. |
| M34.1, M34.1.1, M34.1.2, M34.1.3, M34.2, M34.2.1, M34.2.1a, M34.2.1b y M34.2.1c | `VALIDATED` | Ownership, Equipment, storage item-owned y acciones unificadas. |
| M34.1.4 — Item Inspection Panel | `DEFERRED — RECLASSIFIED` | Retomar dentro de UI/UX de produccion si aporta informacion real. |
| M35.0 — Universal Visual Rig & Attachment Framework | `VALIDATED` | Pipeline visual data-driven y sincronizacion por commit. |
| M35.1 — Lootable Actor Real Equipment Bootstrap | `VALIDATED` | Equipment real inicializado desde Actor Profiles. |
| M35.2 — Lootable Entity Inventory UI V1 | `DONE — FUNCTIONAL SCOPE CLOSED AFTER M35.2.3` | El objetivo funcional se considera satisfecho por M35.2.1–M35.2.3. |
| M35.2.1 — Inventory Window Redesign Phase A | `VALIDATED` | Equipment ocupado y contextuales existentes. |
| M35.2.2 — Inventory Window Redesign Phase B | `VALIDATED` | Ventana flotante unica para item-owned storage. |
| M35.2.3 — Unified Corpse Belongings Surface | `VALIDATED` | Commit funcional `27bf438637b621141ca553a39579349a12ff8700`; cierre documental `2956bcae19719a5f9073e24d58da4705742732fa`. |
| M35.2.3.1 — Universal Corpse Item Actions | `DEFERRED — RECLASSIFIED` | Retomar solo si un loop posterior necesita transaccion cross-actor y despues de contratos de identidad/persistencia. |
| M35.2.4 — Persistent Body Review | `DEFERRED — RECLASSIFIED` | Significa reabrir un cuerpo vacio; no equivale a save/load. Retomar por necesidad jugable comprobada. |
| M35.2.5 — Multiple Floating Storage Windows | `DEFERRED — RECLASSIFIED` | Retomar en UI/UX de produccion si la investigacion de uso justifica multiples ventanas. |
| ID TBD — Container State / Naming Cleanup v0 | `DEFERRED — ID REQUIRED ON REACTIVATION` | Deuda de naming/tags legacy; no bloquea M36/M37. |
| ID TBD — Global Content ID Namespace Foundation | `VALIDATED — FOUNDATION COMPLETE` | Foundation actual: `namespace:local_id`, namespace `core`, identidad canónica de `GameDatabase`, migración Core, compatibilidad legacy temporal, normalización schema-v1 y diagnósticos. No implementa manifests, provenance completa, dependencies ni patches/load-order. El nombre queda reservado con `ID TBD`; no consume un número histórico. |

## Roadmap Estrategico Desde M36

Los IDs siguientes quedan reservados por M36.0. No expresan fechas ni autorizan implementacion por si solos.

| Horizonte | Milestone | Tipo | Estado | Dependencias | Resultado / gate |
| --- | --- | --- | --- | --- | --- |
| CERRADO | M36.0 — Old Scars Strategic Production Roadmap Rebaseline | Gobernanza | `DONE — DOCUMENTATION REVIEWED` | M35.2 cerrado | Checkpoints A/B y Documentation Review Correction Pass 1 revisados y aprobados por Mauro; Unity validation `NOT APPLICABLE`. |
| CERRADO | M36.1 — Foundation Freeze & Persistent Identity Contract | Arquitectura | `DONE — FOUNDATION FREEZE APPROVED` | M36.0 revisado | Identidad durable/authored, ownership, rollback, stack granularity y politica exacta de `Condition` congelados con evidencia automatizada y manual; no implemento save/load. |
| CERRADO | M37.0 — Save Format & Persistence Core | Arquitectura | `DONE — PERSISTENCE CORE VALIDATED` | M36.1 | Envelope V1, serializacion, safe write, backup/recovery, version policy y migration seam validados sin integrar estado gameplay. |
| CERRADO | ID TBD — Global Content ID Namespace Foundation | Arquitectura/datos | `VALIDATED — FOUNDATION COMPLETE` | M37.0 | `ContentId`, namespace `core`, referencias globales canónicas, compatibilidad legacy acotada, normalización schema-v1 y diagnósticos. Excluye manifests, provenance completa, dependencies y patches/load-order. |
| CERRADO | M37.1 — Current Slice Persistent Round-Trip | Arquitectura/jugable | `DONE — CURRENT SLICE PERSISTENCE VALIDATED` | M37.0; compatibilidad schema-v1 Core validada | Snapshot, apply transaccional, rollback, diagnóstico y round-trip fresh-session manual validados; `Persistence Ready` aprobado para el Current Slice. |
| CERRADO | M38.0 — Actor Runtime & Lifecycle V1 | Arquitectura/jugable | `DONE — ACTOR RUNTIME & LIFECYCLE VALIDATED` | ID TBD y M37.1 cerrados | Identidad durable, Alive/Dead, corpse continuity, spawn/restore runtime y persistencia fresh-session validados con automatización y evidencia manual. |
| SIGUIENTE | M38.1 — Needs, World Clock & Recovery V1 | Jugable | `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION` | M38.0 | Reloj y necesidades conectadas; sueño/descanso MUST, fatiga SHOULD. |
| DESPUES | M39.0 — Localized Health & Medicine V1 | Jugable | `PLANNED` | M38.1 | Regiones, heridas, sangrado, dolor y tratamientos. |
| DESPUES | M40.0 — Combat Resolution & Weapons V1 | Jugable | `PLANNED` | M39.0 | Damage contract, melee/firearms, ammo y reload. |
| DESPUES | M40.1 — Armor & Penetration V1 | Jugable | `PLANNED` | M40.0 | Cobertura y penetracion explicables, con seam futuro para condition; gate `Combat Ready`. |
| DESPUES | M41.0 — Navigation & Perception Foundation | Arquitectura/jugable | `PLANNED` | M38.0 | Navegacion, percepcion y diagnostico. |
| DESPUES | M41.1 — Human Encounter AI V1 | Jugable | `PLANNED` | M40.0, M41.0 | Evitar, alertarse, huir y luchar; gate `AI Ready`. |
| DESPUES | M42.0 — Weather, Exposure & Environment V1 | Jugable | `PLANNED` | M38.1 | Clima, forecast, exposicion y proteccion. |
| DESPUES | M42.1 — Food, Water, Animals & Ecology V1 | Jugable | `PLANNED` | M42.0; M41.0 para animales moviles | Calidad, purificacion, deterioro y animales acotados; gate `World Systems Ready`. |
| DESPUES | M43.0 — Condition, Repair & Disassembly V1 | Jugable | `PLANNED` | M37.1 | Condition mutable, reparacion y desmontaje preservando identidad. |
| DESPUES | M43.1 — Bounded Crafting & Workstations V1 | Jugable | `PLANNED` | M43.0 | Recetas cerradas y estaciones limitadas. |
| DESPUES | M44.0 — Skills & Long-Term Progression V1 | Jugable | `PLANNED` | M39.0, M40.1, M41.1, M42.1, M43.1 | Competencias que habilitan opciones, sin grind. |
| DESPUES | M44.1 — Shelter & Recovery Progression V1 | Jugable | `PLANNED` | M38.1, M39.0, M42.1, M43.1, M44.0 | Refugio funcional y recuperacion; gate `Survival Systems Ready`. |
| DESPUES | M45.0 — Content Tools & World Sectorization | Herramientas/arquitectura | `PLANNED` | M37.1 | Sectores, validators, catalogos e inspector sobre contratos estabilizados; gate `Content Pipeline Ready`. |
| DESPUES | M45.1 — Old Scars Vertical Slice Candidate: La estacion de bombeo | Contenido/jugable | `PLANNED — CANDIDATE, NOT NARRATIVE CANON` | M36.1, M37.1, M40.1, M41.1, M42.1, M43.1, M44.1, M45.0 | Loop preparacion–expedicion–retorno persistente con barra audiovisual acotada; gate `Vertical Slice Approved`. |
| FUTURO | M46.0 — Settlements, Trade & Patrimonial Value | Jugable/contenido | `PLANNED` | M45.1 | Asentamientos y economia material acotada. |
| FUTURO | M46.1 — Faction Identity, Disposition & Memory V1 | Jugable | `PLANNED` | M41.1, M46.0 | MUST limitado a identidad, disposicion y memoria minima; no guerra estrategica. |
| FUTURO | M47.0 — Controlled Secondary World Variation V1 | Arquitectura/herramientas | `PLANNED` | M45.0, M46.1 | MUST limitado a variacion secundaria controlada, determinista y persistente. |
| FUTURO | M47.1 — Narrative, Events & Objectives V1 | Contenido/jugable | `PLANNED` | M46.1, M47.0 | Eventos y objetivos autorales acotados. |
| FUTURO | M48.0 — Production UI/UX & Accessibility | Produccion | `PLANNED` | M45.1, M47.1 | UI de produccion sin reescribir backends. |
| FUTURO | M48.1 — Art, Animation & Audio Production Pipeline | Produccion/herramientas | `PLANNED` | M45.1 | Pipeline repetible con budgets. |
| FUTURO | M49.0 — Content Production & Optimization | Contenido/produccion | `PLANNED` | M47.1, M48.0, M48.1 | Contenido a escala sin sistemas nuevos. |
| FUTURO | M50.0 — Modding & Data Compatibility V1 | Arquitectura/produccion | `PLANNED` | M37.1, M45.0, M49.0 | Manifests, versiones, dependencias, overrides y compatibilidad; gate `Production Ready`. |
| FUTURO | M51.0 — Alpha | Produccion | `PLANNED` | Production Ready | Feature complete y recorrido de inicio a fin; gate `Alpha`. |
| POSTERIOR AL ALPHA | M52.0 — Content Complete | Contenido/produccion | `PLANNED` | Alpha | Contenido de lanzamiento integrado; gate `Content Complete`. |
| POSTERIOR AL ALPHA | M53.0 — Beta | Produccion | `PLANNED` | Content Complete | Feature/content lock, estabilidad y balance; gate `Beta`. |
| POSTERIOR AL ALPHA | M54.0 — Release Candidate | Produccion | `PLANNED` | Beta | Build publicable y recuperable; gate `Release Candidate`. |
| POSTERIOR AL ALPHA | M55.0 — Launch | Produccion | `PLANNED` | Release Candidate | Lanzamiento, soporte inicial y rollback operativo. |

## Gates Canonicos

Este archivo es autoridad sobre los nombres y la ubicacion de los gates. Sus criterios detallados se desarrollan en [Production_Gates_and_Risks.md](Production_Gates_and_Risks.md).

Estado vigente: `Foundation Freeze — APPROVED` en M36.1; `Persistence Ready — APPROVED` para el Current Slice validado en M37.1.

| Gate | Cierre previsto |
| --- | --- |
| Foundation Freeze | M36.1 |
| Persistence Ready | M37.1 |
| Combat Ready | M40.1 |
| AI Ready | M41.1 |
| World Systems Ready | M42.1 |
| Survival Systems Ready | M44.1 |
| Content Pipeline Ready | M45.0 |
| Vertical Slice Approved | M45.1 |
| Production Ready | M50.0 |
| Alpha | M51.0 |
| Content Complete | M52.0 |
| Beta | M53.0 |
| Release Candidate | M54.0 |

## Dependencias Y Camino Critico

Camino base vigente:

`M36.0 → M36.1 → M37.0 → (ID TBD validado + M37.1 cerrado) → Persistence Ready APPROVED → M38.0 DONE → M38.1`

Ramas que deben converger antes de la vertical slice candidata:

- M38.1 → M39.0 → M40.0 → M40.1;
- M38.0 → M41.0 y M40.0 → M41.1;
- M38.1 → M42.0 → M42.1; animales moviles requieren tambien M41.0;
- M37.1 → M43.0 → M43.1;
- M38.1, M39.0, M42.1, M43.1 y M44.0 → M44.1;
- M37.1 → M45.0;
- M40.1, M41.1, M42.1, M43.1, M44.1 y M45.0 → M45.1.

Dependencias de produccion:

- persistencia antes de escalar actores, NPCs o mundo;
- actor lifecycle antes de heridas, necesidades complejas e IA;
- world clock antes de clima, deterioro y eventos;
- condition antes de reparacion; reparacion/desmontaje antes de crafting;
- sectorizacion y tools antes de proceduralidad o contenido masivo;
- economia material antes de comercio;
- save, actores y comercio antes de consecuencias regionales.
- toda Definition global nueva debe usar `ContentId` canónico; no se permite volver a IDs globales simples durante los milestones intermedios.

## Alcance Inmediato

### M36.0 — Checkpoint A

Reconciliar autoridad documental, ledger historico, estados, dependencias, gates y cola inmediata. No cambia codigo ni contenido gameplay.

### M36.0 — Checkpoint B

Alinear el baseline de diseño revisado, arquitectura, JSON rules, reglas de desarrollo, template, gates y riesgos. El GDD Maestro v3.1 se conserva intacto como fuente historica auditada; las decisiones ambiguas no se resuelven por inferencia.

### M36.0 — Documentation Review Correction Pass 1

Corregir clasificaciones de diseño revisadas por Mauro, formalizar el workflow proporcional de Codex, commits, publicacion, evidencia visual y subagentes, ajustar la semantica estructural de R03 y reconciliar puntualmente M29 sin iniciar M36.1 ni cambiar contenido jugable.

### M36.0 — Documentation Review Closeout

Mauro aprobo la jerarquia documental, el GDD Markdown como baseline revisado, el roadmap M36–M55, los trece gates, R01–R23, el workflow de Codex/Git y las clasificaciones corregidas. M36.0 queda `DONE — DOCUMENTATION REVIEWED`; las decisiones creativas etiquetadas siguen abiertas y M36.1 requiere autorizacion independiente.

### M36.1 — Foundation Freeze Aprobado / Limite Obligatorio

Checkpoint A corregido implementa IDs `item_<GUID N lowercase>` opacos e inmutables, rutas separadas `CreateNew`/`Rehydrate`, unicidad de IDs activos, ownership estricto, item-owned storage explicito y reglas de stack/split/merge/rollback consumibles por M37. Un stack conserva una `ItemInstance` representativa y cantidad fungible; `Condition` get-only forma parte del estado de instancia y de la compatibilidad de stack.

Los pases correctivos agregan attachment detached y registro explicito de item-owned storage, bootstrap transaccional de containers, cleanup de IDs en merges totales, rechazo atomico de removal terminal cuando el storage propio no esta vacio y transiciones comprometidas de ownership. La validacion automatizada esta verde y Mauro confirmo manualmente pickup, drop, equip/unequip, equip directo desde el mundo, item-owned storage, mochila no vacia, containers y transfers con cuerpos sin duplicaciones ni ownership exceptions. Checkpoint A queda validado y cerrado.

Checkpoint B recupera la implementacion local parcial y congela identidad authored para el slice actual. `PersistentSceneObjectId` identifica exactamente 14 roots stateful de `SampleScene`: 3 actores, 3 puertas y 8 contenedores. Los dos world items authored usan `ItemInstance.CreateAuthored` con IDs `item_<32 hex lowercase>` exactos y separados de `DefinitionId`; los drops runtime conservan su instancia existente y no reciben un authored ID nuevo. `Debug Strange Machine` permanece excluida.

Runtime y Editor compilaron en Unity 6.4.6f1. `M36.1 Foundation Identity Validation` paso despues de aplicar la tabla, despues de reabrir la escena y en una reaplicacion idempotente; Checkpoint A volvio a dar `PASS`. Mauro valido manualmente crowbar y Lee-Enfield authored, pickup, equip directo desde el mundo, inventario y drop sin errores funcionales nuevos. M36.1 debe seguir siendo corto y no implementa:

- save/load;
- condition;
- repair/disassembly;
- actor lifecycle;
- gameplay nuevo;
- UI final.

Decisiones congeladas para M37:

- M37 debe rehidratar el `InstanceId` exacto y el `Condition` exacto validado; no debe crear otro ID durante carga;
- items no stackeables y cada stack visible conservan identidad durable; las unidades fungibles internas de un stack no poseen IDs individuales;
- `Foundation Freeze` queda `APPROVED`; estas decisiones son el contrato de entrada de M37 y no autorizan reinterpretarlas durante el closeout.

### ID TBD — Global Content ID Namespace Foundation / Límite Obligatorio

Unidad técnica interpuesta por autorización explícita para evitar que nuevos sistemas acumulen supuestos de ID global simple y Core implícito. Su estado es `VALIDATED — FOUNDATION COMPLETE`.

Implementa solamente:

- contrato central `ContentId` para `namespace:local_id` y namespace oficial reservado `core`;
- canonicalización en la frontera de carga de las Definition families registradas globalmente y de sus referencias reales;
- registries de `GameDatabase` con una sola clave canónica y compatibilidad temporal Core sin alias registrados;
- distinción de Global Content ID, Local ID, runtime/instance ID, persistent scene ID, tags y asset keys;
- migración explícita de `Mods/Core`, seam de source context y compatibilidad schema v1 para Definition ID/layout/equipment slots sin subir versión;
- fixture Editor temporal para coexistencia entre namespaces y referencias cross-namespace.

No implementa manifest, provenance persistida completa, dependencies, overrides/patches, Workshop, SDK, scripting, DLL mods, hot reload, AssetBundles ni namespace de tags. La secuencia posterior queda `manifest → provenance → dependencies → patches`; M50.0 continúa futuro y no se considera iniciado.

### M37 — Limite Obligatorio

M37 persiste primero el slice actual: jugador, items, inventory/grid, Equipment, ownership, item-owned storages, containers, cuerpos, puertas, world items y runtime tags existentes. No serializa sistemas hipoteticos para actores, clima, facciones o mundo procedural.

M37.0 está `DONE — PERSISTENCE CORE VALIDATED`. M37.1 queda `DONE — CURRENT SLICE PERSISTENCE VALIDATED`: los diagnostics automatizados y la validación fresh-session manual, incluida la compatibilidad schema-v1 de Global Content IDs Core, pasaron. `Persistence Ready` permanece aprobado para el Current Slice y no se reabre.

### M38.0 — Límite Obligatorio

M38.0 queda `DONE — ACTOR RUNTIME & LIFECYCLE VALIDATED`. Extiende el Current Slice con identidad durable distinta de profile/locator, lifecycle `Alive/Dead`, pose/health y referencias a storages existentes; reconcilia authored bootstrap y representaciones runtime mediante el apply/rollback de M37.1. La validación automatizada y la evidencia manual fresh-session pasaron. No implementa M38.1, AI, combat, needs/world clock, world streaming, spawn a escala ni el playable exploration prototype. M38.1 queda `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION` y no comienza en este cierre.

## Trabajo Congelado O Diferido

- No ampliar OnGUI ni continuar la serie M35.2 durante M36/M37.
- No iniciar combate, IA, mundo procedural, facciones amplias, UI final o produccion masiva de contenido antes de sus dependencias.
- No introducir enfermedades generales, agricultura o vehiculos en la version inicial completa sin un nuevo rebaseline aprobado.
- No convertir JSON en scripting libre.
- No crear sistemas universales preventivos sin necesidad actual demostrada.

## Regla Transversal De Integracion

Profundidad mediante sistemas conectados es una regla de aceptacion: todo sistema nuevo debe recibir estado relevante de otro sistema, modificar al menos una decision jugable y ofrecer feedback explicable. Una barra o simulacion aislada no cumple esta regla.
