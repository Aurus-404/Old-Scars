# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximo Trabajo

### 1. Sin milestone de implementación activo

Estado: `PLAYER TRAVERSAL / CAMERA & RUNTIME DEBUG ERGONOMICS PASS CLOSED`.

M41.1 está `DONE — HUMAN ENCOUNTER AI V1 VALIDATED`, con validation `AUTOMATED + MANUAL UNITY PASSED`; `AI Ready` está `APPROVED`. El hardening posterior compactó el workflow y sus skills, y confirmó una consulta MCP real de solo lectura (`editor_status`) contra el Editor del worktree. Unity MCP queda aceptado provisionalmente para trabajo real; `com.unity.pipeline` se conserva sólo porque ese bridge técnico lo requiere. Unity CLI global es opcional y no forma parte de los requisitos de Old Scars.

La dirección [Open World Architecture](Open_World_Architecture.md) está `APPROVED DESIGN DIRECTION — NOT IMPLEMENTED` para sus capacidades futuras restantes. Sus foundations y application shell cerradas incluyen:

`ID TBD — Minimum Content Source Identity & Provenance Foundation`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — World Identity, Topology & Determinism Foundation`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — World Session + Persistence V1 / New Game Save-Load Application Shell`

Estado: `VALIDATED — APPLICATION SHELL COMPLETE`.

`ID TBD — Macro World Plan V1`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — Macro Elevation / Landforms V1`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — Worldgen Gameplay Quality + Macro Water V1`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — Worldgen Pass Isolation Correction`

Estado: `VALIDATED — SYSTEMIC CORRECTION COMPLETE`.

`ID TBD — Worldgen / World Session Observability Correction`

Estado: `VALIDATED — OBSERVABILITY CORRECTION COMPLETE`.

`ID TBD — Macro Human Geography / Road Network V1`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — Terrain Materialization Technical Spike`

Estado: `VALIDATED — TECHNICAL SPIKE COMPLETE`.

`ID TBD — Integrated Gameplay Runtime / SampleScene Convergence`

Estado: `VALIDATED — RUNTIME CONVERGENCE COMPLETE`.

Commit de cierre: `8c485c78b4ab294de9d983f70ebadfba634ab3e1`.

`ID TBD — Macro Climate Baseline V1`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

Commit de cierre: `457836e7f10a9b2ddbc08cc1db05ca38cd3f7108`.

Climate aporta `ThermalIndex` y `MoistureIndex` mundiales bajo `macro_climate_v1`, con norte-frío/sur-cálido, anomalía regional, enfriamiento por elevación, influencia oceánica gradual, orografía acotada y una dirección dominante persistida entre ocho direcciones canónicas. Small→Huge aumenta resolución/frecuencia regional. `world_session_v1` schema `6` persiste esa truth; schemas `1`–`5` siguen siendo legacy exactos sin Climate fabricado.

`ID TBD — Player Traversal / Camera & Runtime Debug Ergonomics Pass`

Estado: `VALIDATED — RUNTIME ERGONOMICS COMPLETE`.

Commit final validado: `ab78da4fbb1af9189d6a5c178515fafdb56f368e`.

La cámara continúa centrada en el player y sin pan independiente; RMB aporta yaw/pitch, el zoom pedido se conserva separado de la retracción por `SphereCast` y la cámara vuelve suavemente tras una obstrucción. Shift sprint reutiliza el único `PlayerMovementController`; `ActorStaminaComponent` añade stamina real con recovery/lockout, coste adicional de Hunger/Thirst sólo durante sprint y Current Slice persistence dentro de la transacción existente. El panel F3 development-only unifica ScrollView, movement multiplier, stamina/Needs, presets de WorldClock `1x/2x/3x/5x/10x/20x/50x/100x`, reset de cámara y teleport acotado a suelo materializado. Player Controls/Health D3D11, M38 Needs/WorldClock/Recovery Play Mode y WorldRuntime/session Play Mode: `PASS`.

WorldRuntime sigue siendo el runtime canónico de gameplay y SampleScene el laboratorio/regresión. Climate no crea una segunda autoridad gameplay ni un GameObject/runtime simulation paralelo. La shell implementada posee session lifecycle único, save catalog, Main Menu, WorldRuntime e in-game Save/Return. New Game actual genera plan finito → elevation/landforms → Macro Water → Macro Climate → quality/starter → Macro Human Geography; Climate todavía no es input de Gameplay Quality, Starter ni Human Geography V1. La materialización local consume la truth persistida que necesita y no convierte Climate en terrain materials, weather o biomes.

El próximo coding unit candidato es `ID TBD — Macro Environment / Biome Regions V1`, con estado `PLANNED — NOT AUTHORIZED`. Debe consumir landform/Water/Climate ya validados para resolver regiones environment/biome globales antes del detalle local, sin confundir landform con biome ni iniciar vegetation/materiales finales.

Fuera de alcance mientras no exista autorización específica:

- implementar `Macro Environment / Biome Regions V1` o cualquier unit open-world posterior;
- retunear/reabrir `Macro Climate Baseline V1` sin una corrección o unit expresamente autorizada;
- ampliar nuevamente traversal/camera/debug ergonomics sin un alcance explícito;
- M42.0 u otro milestone jugable;
- cambios de gameplay, content contracts o persistencia fuera de los schemas ya cerrados;
- reabrir la convergencia runtime o la arquitectura M41.1 validada.

M42.0 permanece planificado, pero su secuencia requiere rebaseline y ya no constituye el siguiente trabajo automático. Todo trabajo nuevo requiere autorización explícita.

## Connected First Playable

El Connected First Playable es la prueba integrada objetivo después de las foundations open-world. Debe demostrar A→B→A, continuidad cross-sector, mutaciones persistentes, save, full process exit y fresh load usando M32–M41.1 dentro del runtime canónico. No está iniciado, no es la vertical slice audiovisual final y no adelanta M45.1.

## Modding Y Provenance

La Global Content ID Foundation y la Minimum Content Source Identity & Provenance Foundation están validadas. Cada source requiere manifest `source_id`/`namespace`/`version`; ownership de declaraciones, orden estable y SHA-256 de recognized inputs están implementados sobre el pipeline Core/mod existente.

`Provenance` prueba qué fuentes/inputs estuvieron presentes. `Generation compatibility` continúa no implementada y será responsable de decidir si inputs semánticos siguen siendo compatibles con un mundo; no se infiere desde igualdad/diferencia del fingerprint.

Dependencies, overrides/patches y compatibilidad de producción permanecen en alcance posterior M50.0. La nueva foundation no sustituye M50.0 ni lo marca iniciado.

## No Iniciar Todavía

- Macro Environment / Biome Regions V1, Terrain Materialization V1 productiva, otros coding units open-world o M42.0 sin autorización específica;
- weather runtime, seasons, final rivers, geology o cualquier ampliación/retuning de Climate fuera de un unit autorizado;
- nuevas ampliaciones OnGUI sin milestone autorizado;
- UI final;
- condition, repair o crafting;
- actores o mundo a escala fuera de las foundations implementadas;
- facciones amplias;
- vegetation/biomes locales, sectors jugables, transición, world history o gameplay world persistence general antes de sus units autorizadas;
- producción masiva de contenido.
