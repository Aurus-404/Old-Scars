# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M41.1 — Human Encounter AI V1

Estado final:

`DONE — HUMAN ENCOUNTER AI V1 VALIDATED`

Validation: `AUTOMATED + MANUAL UNITY PASSED`.

M41.0 permanece `DONE — NAVIGATION / PERCEPTION FOUNDATION VALIDATED`; M40.1 conserva `Combat Ready — APPROVED`. M41.1 conecta Navigation, Perception y Combat sin una autoridad paralela. `AI Ready — APPROVED`.

### Open World Rebaseline

Estado de dirección:

`APPROVED DESIGN DIRECTION — NOT IMPLEMENTED`

[Open_World_Architecture.md](Open_World_Architecture.md) define el futuro mundo lógico persistente, sectores grandes interconectados, macro planning, blueprints sectoriales, materialización Unity y mutación persistente. Las foundations mínimas de content source identity/provenance, world identity/topology/determinism, Macro World Plan V1, Macro Elevation/Landforms V1, Gameplay Quality/Macro Water V1, Macro Human Geography/Road Network V1 y Macro Climate Baseline V1, más la shell acotada de World Session/New Game/Save/Load, el Terrain Materialization Technical Spike local, la convergencia del gameplay compartido en WorldRuntime y el pass de traversal/camera/debug ergonomics, están implementadas y validadas. Final rivers, geology/biomes, settlement detail, world persistence general de blueprints/mutaciones sectoriales, materialización sectorial/streaming de producción, transición y generation compatibility continúan no implementados.

### ID TBD — Player Traversal / Camera & Runtime Debug Ergonomics Pass

Estado final:

`VALIDATED — RUNTIME ERGONOMICS COMPLETE`

Commit final validado: `ab78da4fbb1af9189d6a5c178515fafdb56f368e`.

La cámara continúa player-centric y `AllowsIndependentPan=false`: RMB controla yaw/pitch con clamp, la rueda conserva el zoom pedido y un `SphereCast` retrae sólo la distancia física actual ante geometría sólida para impedir atravesar paredes/terreno; al desaparecer la obstrucción la cámara recupera suavemente la distancia solicitada. `BuildingVisibilityManager` conserva su autoridad separada.

Shift sprint reutiliza el único `PlayerMovementController`/`CharacterController`. `ActorStaminaComponent` añade stamina gameplay simple con drain/recovery en tiempo real, lockout al agotarse y threshold de recuperación; reservas Hunger/Thirst altas mejoran recovery, mientras stamina baja encarece sólo el coste adicional de seguir sprinting. Descansar con stamina baja no añade consumo metabólico extra. Stamina se captura/preflighta/restaura dentro de Current Slice y su rollback existente; `world_session_v1` no cambió.

`ActorNeedsDebugPanel` quedó como Runtime Debug Tools development-only, oculto por defecto y toggleable con F3, con `ScrollView`, movement multiplier efímero, control de stamina/Hunger/Thirst, presets `1x/2x/3x/5x/10x/20x/50x/100x` sobre la autoridad `WorldClock`, reset de cámara y teleport armado de un solo click limitado a suelo materializado válido. El panel reutiliza `DebugWorldUiInputBlocker`; sus cheats/configuración debug no se persisten.

Player Controls/Health bajo D3D11, M38 Needs/WorldClock/Recovery Play Mode y WorldRuntime/session Play Mode quedaron `PASS`. El cierre incluyó correcciones acotadas al threshold float de recovery, fixture M38 con reservas completas, cobertura de los ocho multiplicadores de reloj, aceptación del panel F3 oculto en validación compartida y resolución perezosa segura del `CharacterController` para teleport.

### ID TBD — Macro Climate Baseline V1

Estado final:

`VALIDATED — FOUNDATION COMPLETE`

Commit validado: `457836e7f10a9b2ddbc08cc1db05ca38cd3f7108`.

`MacroClimatePlan` aporta truth climática mundial inmutable bajo `macro_climate_v1`: `ThermalIndex` y `MoistureIndex` fixed-point, gradiente térmico norte-frío/sur-cálido con anomalía regional y enfriamiento por elevación, humedad regional con influencia oceánica gradual y respuesta orográfica acotada, y una dirección dominante persistida entre ocho direcciones canónicas. El escalado Small→Huge aumenta resolución/frecuencia regional en lugar de estirar un único patrón.

`world_session_v1` schema `6` persiste Climate committed con validación estricta; schemas `1`–`5` conservan su truth legacy exacta y no fabrican Climate, incluido schema `5` con Human Geography y `HasMacroClimate=false`. Fresh Process A/B, Main Menu→WorldRuntime→Save→Return→Load, pass isolation, goldens upstream y regresores de worldgen/persistence pasaron. Climate no entra todavía en Gameplay Quality, Starter ni Human Geography V1 y no implementa weather runtime, biomes, vegetation ni materialización local.

### ID TBD — Integrated Gameplay Runtime / SampleScene Convergence

Estado final:

`VALIDATED — RUNTIME CONVERGENCE COMPLETE`

Commit validado: `8c485c78b4ab294de9d983f70ebadfba634ab3e1`.

WorldRuntime es el runtime canónico de gameplay. SampleScene conserva el rol de laboratorio/regresión. Ambos reutilizan una composición gameplay compartida y las autoridades existentes de player, Inventory, Health, Needs, Interaction, Combat y Persistence; worldgen aporta terreno/world truth y representación derivada sin reemplazar esas autoridades. La fixture M32 integrada existe únicamente en Editor/development builds y no constituye aceptación audiovisual ni contrato de compatibilidad de saves para futuros release builds.

La convergencia preservó la identidad durable M36 y la separación entre representación mundial y gameplay. MainMenu → New Game → WorldRuntime integrado, interacción, Inventory/Health/Needs, combate, save → menu → load, rechazo de contaminación entre mundos y fresh-process A/B quedaron validados sin introducir autoridades paralelas.

### ID TBD — Minimum Content Source Identity & Provenance Foundation

Estado final:

`VALIDATED — FOUNDATION COMPLETE`

Cada content source requiere manifest mínimo, identidad/namespace/version estables y ownership único antes de registrar Definitions. Core usa el mismo pipeline. `LoadedContentSet` publica orden y SHA-256 de provenance sólo después de loader + `DataValidator` exitosos; no decide compatibilidad.

### ID TBD — World Identity, Topology & Determinism Foundation

Estado final:

`VALIDATED — FOUNDATION COMPLETE`

`WorldId`, `WorldSeed`, `GeneratorVersion`, `SectorId`, derivación SHA-256 por contrato/scope/pass y `WorldTopology` conectado/multiconexión existen como datos lógicos puros. `GeneratorVersion` es metadata global de creación; Plan y Geography poseen contratos deterministas propios. `WorldId` no entra en generación; provenance tampoco se convierte en compatibilidad ni input automático.

### ID TBD — World Session + Persistence V1 / New Game Save-Load Application Shell

Estado final:

`VALIDATED — APPLICATION SHELL COMPLETE`

`WorldSessionService` posee una única session activa y lifecycle Create/Load/Save/Close. `world_session_v1` es hermano de `current_slice_v1` sobre el envelope/store M37; usa `WorldId` como slot, persiste identity/topology/active sector/provenance evidence y preflighta antes de publicar. New Game actual escribe schema `6` con Climate committed; schema `5` continúa siendo legacy válido con Human Geography y Climate ausente. Main Menu es el startup de producto y WorldRuntime es el runtime canónico integrado: materializa la ventana terrain técnica desde la truth disponible y ejecuta sobre ella el gameplay compartido. SampleScene continúa laboratorio. La persistencia gameplay validada sigue siendo la del Current Slice soportado, no una implementación general de futuras mutaciones/blueprints sectoriales.

### ID TBD — Macro World Plan V1

Estado final:

`VALIDATED — FOUNDATION COMPLETE`

New Game usa `WorldGenerationSettings` con `Small`, `Medium`, `Large` y `Huge` —default `Large`— para crear un `MacroWorldPlan` finito completo antes de entrar a runtime. Settings resueltos, bounds, placements, topology y hash canónico permanecen como foundation separada. WorldId, provenance y futuro worker budget no alteran generación.

### ID TBD — Macro Elevation / Landforms V1

Estado final:

`VALIDATED — FOUNDATION COMPLETE`

New Game genera después del plan un `MacroGeographyPlan` global fixed-point, continuo y compacto. Elevation normalizada e identidades regionales `Plains`, `RollingHills`, `Highlands` y `Mountains` se consultan por coordenadas macro, no por sector/topology. `world_session_v1` schema `3` conserva ese formato legacy sin Water fabricada. El spike consume esta truth en una ventana física; geology, vegetation/biomes y materialización terrain de producción continúan pendientes.

### ID TBD — Worldgen Gameplay Quality + Macro Water V1

Estado final:

`VALIDATED — FOUNDATION COMPLETE`

New Game genera `MacroWaterPlan` global después de MacroGeography: Land Coverage `Low`/`Medium`/`High` —default `High`—, sea level, ocean mask/bodies, coastline, conditioned drainage y basin candidates. Un quality analysis fixed-point distingue hard failures de soft findings y selecciona el starter desde anchors terrestres adecuados; no declara Walkable/NavMesh/Buildable. `world_session_v1` schema `4` persiste Water committed y schemas `1`/`2`/`3` conservan su truth legacy sin fabricación. El MST del plan sigue siendo scaffold lógico, no physical adjacency/road/travel graph.

### ID TBD — Worldgen Pass Isolation Correction

Estado final:

`VALIDATED — SYSTEMIC CORRECTION COMPLETE`

`WorldDeterminism.DerivePassDomainKey` separa `WorldSeed + pass generation contract + scope + pass` de la versión global del pipeline. Plan conserva `macro_plan_v1`, Geography `macro_geography_v1`, Water `macro_water_v1`, Climate `macro_climate_v1`, Human Geography `macro_human_roads_v1`, y New Game registra `world_pipeline_v4` sólo como metadata global. Cambiar una versión downstream ya no re-seedea truth upstream sin una dependencia real; los saves schemas `1`–`6` rehidratan únicamente su truth committed sin regeneración ni upgrade silencioso.

### ID TBD — Worldgen / World Session Observability Correction

Estado final:

`VALIDATED — OBSERVABILITY CORRECTION COMPLETE`

Create/Load/Runtime Ready y Save manual poseen eventos estructurados únicos y filtrables en sus límites reales. `WORLD_CREATED` resume identity/settings/contracts/hashes/starter, `LOAD_OK` declara schema y truth presente o ausente, `SESSION_READY` confirma la session publicada, y `SAVE_OK` aporta contexto semántico sin reemplazar `[Persistence][WRITE_COMMIT]`. Climate schema `6` extiende esa observabilidad con presencia/ausencia y evidencia climática sin duplicar lifecycle logs.

### ID TBD — Macro Human Geography / Road Network V1

Estado final:

`VALIDATED — FOUNDATION COMPLETE`

New Game genera después de quality/starter un `MacroHumanGeographyPlan` mundial bajo `macro_human_roads_v1`: hubs Regional/Local en tierra, backbone Primary conectado por landmass, enlaces alternativos/ciclos y branches Secondary, todos con IDs estables y polylines globales routeadas sobre un cost field entero de relief/traversal. No usa el MST de `WorldTopology` como road graph, no cruza océano y no materializa settlements, roads, bridges, terrain ni navegación física. Su truth nació en `world_session_v1` schema `5`; schema `6` la preserva sin hacer de Climate un input de Human Geography V1. Schemas `1`–`4` permanecen legacy exactos y no fabrican infraestructura.

### ID TBD — Terrain Materialization Technical Spike

Estado final:

`VALIDATED — TECHNICAL SPIKE COMPLETE`

Una session con la macro truth requerida puede proyectar alrededor del active-sector anchor una ventana Unity local con Terrain/TerrainCollider, ocean mask, roads diagnósticas, player sobre tierra y una NavMesh terrestre local consumida por `ActorNavigationController`. La baseline provisional es `768×768` Unity units, relief `240`, ventana lógica `1800×1800` y heightmap `257`. Product sector no equivale a Terrain tile, la escala final permanece unfrozen y no existen streaming, transitions, mutations, voxels ni materialización productiva. Climate schema `6` no cambia este contrato ni colorea/materializa el clima.

## Evidencia De Cierre

- Runtime/Editor compile y `M41.1 Human Encounter AI Diagnostics`: `PASS`.
- Mauro confirmó `Idle → Alerted → Avoiding`, `Fleeing` y `Fighting`, con navegación física y `NAVIGATION_MOVING` observable.
- Fight reutilizó Lee-Enfield, ammo/reload/disparo y armor del contrato M40; el estado final `0 loaded / 0 reserve` fue consumo manual deliberado, no un defecto.
- LOS confirmó `Perceived → Occluded → LostContact → Idle`; al retirar la barrera y reasignar explícitamente el threat volvió a `Alerted`, sin omnisciencia.
- El menú Editor muestra sólo la fixture M41.1 explícita; diagnostics históricos siguen invocables por automatización sin exposición manual obsoleta.
- Runtime/Editor compile, real Core + `DataValidator`, `Minimum Content Source Identity & Provenance Foundation` y `Global Content ID Namespace Foundation`: `PASS` en Editor batchmode aislado.
- Runtime/Editor compile y `World Identity / Topology / Determinism Foundation`: `PASS` en dos procesos aislados con domain/topology golden hashes estables; M36.1 Foundation Identity y M37.0 Persistence Core permanecen `PASS`.
- Runtime/Editor compile, `World Session / Persistence V1 Application Shell Diagnostics`, flujo Play Mode real Main Menu→Runtime→Menu→Main Menu→Load y scene wiring: `PASS`.
- Fresh process A/B creó y reabrió desde disco el mismo `WorldId`, seed, topology hash y active sector; `M37.0`, M37.1 semantic preflight, World Identity/Topology/Determinism y Content Source Identity/Provenance permanecen `PASS`.
- Runtime/Editor compile y `Macro World Plan V1 Diagnostics`: `PASS`; cubre same-input/WorldId independence, cuatro escalas, bounds/spacing/uniqueness/connectivity, insertion order, golden plan, 12 seeds por preset, schema 4 round-trip, schemas 1/2 legacy y timing de los cuatro presets.
- Fresh process A/B reconstruyó exactamente `WorldId`, seed, size `Huge`, MacroWorldPlan hash, topology hash y active sector desde disco; el flujo Play Mode Main Menu con size seleccionado, Save/Return/Load y las regresiones M37.0/M37.1, World Foundation y Content Provenance permanecen `PASS`.
- Runtime/Editor compile y `Macro Elevation / Landforms V1 Diagnostics`: `PASS`; cubre same-input/WorldId independence, different seed, bounds/interpolation, continuidad global sin SectorId, variedad/coherencia/range, order independence, golden geography, `8 seeds × 4 presets`, schema `3` round-trip y schemas `1`/`2` legacy.
- Preview 2D golden inspeccionada: grandes lowlands, macizos/ridges altos y regiones contiguas de los cuatro landforms, con sector overlay sin seams. Timings diagnósticos plan+geography: Small `14 ms`, Medium `49 ms`, Large `199 ms`, Huge `910 ms`; samples raw `7.2–38.3 KB`.
- Fresh process A/B schema `3`: `PASS`; reconstruyó el mismo WorldId, seed, size, MacroWorldPlan hash, MacroGeography hash, topology y active sector. World Session edit-mode, Play Mode Main Menu→Runtime→Save/Return→Load, M37.0, M37.1 Current Slice, World Foundation y Content Provenance permanecen `PASS`.
- Baseline previo a thresholds: `192` mundos (`48 seeds × 4 sizes`) confirmó regiones low-relief amplias y ruggedness conservada; no se retuneó MacroGeography. Stress ampliado: `384` variantes (`32 seeds × 4 sizes × 3 coverages`), `0` hard rejections y `0` generation failures después de un ajuste conservador del piso de corredor/anchor neighborhood.
- `Worldgen Gameplay Quality + Macro Water V1 Diagnostics`: `PASS` para determinismo/WorldId independence, isolation de Land Coverage, plan/geography hashes invariantes, ocean/coastline/drainage/basins, quality/starter, routine fuzz, schema `4` round-trip, schemas `1`/`2`/`3` legacy y preview de seis paneles. Después de restaurar la Geography V1 original, el golden Water legítimo es `ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0`.
- `Worldgen Pass Isolation Correction Diagnostics`: `PASS` en procesos frescos; cambiar sólo la versión global histórica/actual/futura conserva Plan/Geography/Water, cambiar el contrato Geography conserva Plan y cambia Geography/Water, y el fuzz `2 seeds × 4 sizes` permanece aislado. Goldens restaurados: Plan `3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a`, Geography `c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e`.
- Timings diagnósticos finales plan+geography+Water+quality aproximados: Small `16 ms`, Medium `53 ms`, Large `209 ms`, Huge `907 ms`; payload schema `4` serializado `45.0/83.6/134.1/250.6 KB` y raw Water estimado `16.9/29.8/46.3/90.0 KB`. No son budgets de producción. La preview inspeccionada mostró plains/hills/highlands/mountains, land/ocean/coastline significativa, suitability/corridors y drainage/basins sin seams sectoriales.
- Schema `4` edit/Play flow y fresh Process A/B: `PASS`; el segundo proceso reconstruyó mismo WorldId, seed `-3141592653589793`, size `Huge`, coverage `High`, Water hash, topology y active sector y limpió su root temporal. M37.0, M37.1 snapshot/round-trip, Macro World/Geography, World Foundation, Content Provenance/Namespaces y M41 Navigation/Perception permanecen `PASS`.
- Observability correction: Runtime/Editor compile, World Session edit-mode y Play Mode flow, Pass Isolation, Macro Plan, Macro Geography, Water/Quality y M37 Persistence Core: `PASS`. El flow confirmó exactamente `WORLD_CREATED=1`, `LOAD_OK=1`, `SESSION_READY=2`, `SAVE_OK=1` y `WRITE_COMMIT=2`; los schemas legacy declararon truth ausente sin hashes fabricados y los goldens Plan/Geography/Water permanecieron intactos.
- `Macro Human Geography / Road Network V1 Diagnostics`: `PASS`; golden hash `a786f018ce3bdea44aeb066c80e38cb1f5dc8e114c65bd7eb352489628245ba6`, routing determinista/WorldId-independent, hubs/endpoints terrestres, backbone + secondary branches + ciclos, preferencia de terrain cost, starter access, order/pass isolation y schema `5` exacto. Los goldens Plan/Geography/Water permanecieron intactos.
- Corpus rutinario `36/36` y stress `144/144` (`12 seeds × 4 sizes × 3 coverages`) generaron sin rechazos duros; `126` mundos emitieron findings blandos de cobertura/gap para tuning futuro. Timings/payload aproximados: Small `28 ms / 52,442 B`, Medium `79 ms / 98,139 B`, Large `295 ms / 160,050 B`, Huge `1,203 ms / 288,407 B`; no son budgets productivos.
- Preview golden de seis paneles inspeccionada: backbone/branches/ciclos visibles sobre tierra y coast background, sin ocean crossing ni spaghetti. Fresh Process A/B reconstruyó el mismo Human Geography hash `7099469990ae9cfd21e4c5b27a233f5aff5a46f4f908b2ef62b5be0556260d18`; Play Mode confirmó Main Menu→Create→Runtime→Save→Return→Load y cardinalidad `WORLD_CREATED=1`, `LOAD_OK=1`, `SESSION_READY=2`, `SAVE_OK=1`, `WRITE_COMMIT=2`.
- Runtime/Editor compile, World Foundation, Macro Plan, Macro Geography, Water/Quality, Pass Isolation, World Session, M37 Persistence Core, Content Source Provenance, Global Content Namespace y M41 Navigation/Perception: `PASS` en procesos aislados. Los mensajes de licensing/package assembly del Editor se conservaron separados de errores del producto.
- `Terrain Materialization Technical Spike Diagnostics`: `PASS`; un Terrain + TerrainCollider local consumió samples de MacroGeography, coast/sea committed y roads persisted sin seams de SectorId, con equivalencia determinista y aislamiento de escala respecto de los hashes Plan/Geography/Water/Human Geography.
- Baseline `768×768 / relief 240 / logical 1800×1800 / heightmap 257`: projection `13 ms`, Terrain `12 ms`, NavMesh `796 ms`, total `823 ms`, memoria estimada `463,392 B` y `11` objetos. Candidatos `512×512/h129` y `1024×1024/h257` midieron total `500 ms` y `1,295 ms`; son evidencia técnica, no budgets productivos.
- Play Mode Main Menu→Create→WorldRuntime→Save→Return→Load: `PASS`; emitió exactamente dos `[WorldMaterialization][READY]` y un actor Core real aceptó un path mediante `ActorSpawnService` + `ActorNavigationController`. Fresh Process A/B, schemas `1`–`5`, M37, M41 y goldens de Plan/Geography/Water/Human Geography permanecieron `PASS`.
- Previews temporales inland/plain, rugged y coastal fueron inspeccionadas: relief regional coherente, costa/océano alineados y polylines viales globales continuas. El probe rugged usó pendiente física máxima `51.52°`; los `142/142` samples por encima del contrato NavMesh `45°` quedaron excluidos. Los colores/lines son diagnóstico, no arte o surfaces finales.
- Integrated Gameplay Runtime / SampleScene Convergence: Runtime/Editor compile, WorldSessionApplicationDiagnostics, MainMenu→New Game→WorldRuntime integrado, cardinalidad de player/camera/WorldClock/Inventory/interacción/Needs/Health, container transfer, crowbar pickup/equip, door contextual action, save→menu→load repetido, contaminación World A→B rechazada y Fresh Process A/B: `PASS`.
- M36.1 Foundation Identity Validation permaneció `PASS` con 14 `PersistentSceneObjectId`, 2 `ItemInstanceId`, 3 actores, 3 puertas, 8 contenedores y cero IDs duplicados o inválidos. La captura D3D11 mostró player y fixture de integración sobre terreno generado; `git diff --check` pasó. El commit publicado de cierre es `8c485c78b4ab294de9d983f70ebadfba634ab3e1`.
- `Macro Climate Baseline V1 Diagnostics`: `PASS`; goldens upstream preservados — Plan `3f300ba2129962493d2ab8f2ad6ec0863e96aa0ceeb400f9899f91889a34e91a`, Geography `c2d412fcdcb1b0e1b41f4fdbda2df01258758e6db9c6b93aac59b446be7dbd3e`, Water `ec29f501e4f36ae3b2313d3da6089f2fe6e92b052f18079c649e21ce8faabfc0`, Human `a786f018ce3bdea44aeb066c80e38cb1f5dc8e114c65bd7eb352489628245ba6`— y Climate golden `a4b7869a7d8deab093eb9b9c5f7a2da118156f22c61ac466fbd0a9e64958eec1`.
- Climate schema `6` round-trip exacto, schemas `1`–`5` legacy sin Climate fabricado, Fresh Process A/B, Main Menu→WorldRuntime→Save→Return→Load, Pass Isolation, Water/Quality, Human Geography, Terrain Materialization D3D11, M37 y Content Provenance: `PASS`. Las previews Small→Huge fueron inspeccionadas y mostraron mayor frecuencia/provincias con el preset, tendencia térmica norte-frío/sur-cálido, humedad costa/interior gradual y ausencia de seams/ruido estático evidente. Hubo `13` findings blandos de distribución y cero fallo contractual reportado. El commit publicado es `457836e7f10a9b2ddbc08cc1db05ca38cd3f7108`.
- Player Traversal / Camera & Runtime Debug Ergonomics Pass: `PlayerControlsHealthWindowDiagnostics` bajo D3D11, `M38NeedsWorldClockRecoveryDiagnostics` Play Mode y `WorldSessionApplicationDiagnostics`/WorldRuntime Play Mode: `PASS`. La validación cubrió follow/yaw/pitch/clamp, colisión y restauración de zoom, WASD camera-relative, sprint/stamina/Needs, Current Slice rollback, ocho multiplicadores de WorldClock y teleport seguro. Commit final publicado: `ab78da4fbb1af9189d6a5c178515fafdb56f368e`.

## Contratos Cerrados

- `HumanEncounterAIController` sólo posee decision state/target/timers/response y requiere bloques data-driven `navigation`, `visual_perception` y `encounter_ai`.
- Perception conserva LOS; Navigation conserva path; `WeaponCombatService` conserva ammo, reload, impacto y consecuencias. Player y NPC comparten `PhysicalShotPathResolver`.
- `LostContact` usa sólo last-known de percepción positiva, cancela acción y exige reacquisition explícita tras timeout; `Dead` deja IA y Navigation inactivas.
- Encounter state, target, timers, órdenes y resultados de percepción siguen efímeros; M41.1 no cambia schema/envelope.
- `MacroClimatePlan` es la única truth climática committed de esta foundation; no existe GameObject o simulación runtime Climate paralela.
- `MoistureIndex` expresa tendencia climática de largo plazo, no humedad del aire/suelo ni lluvia actual. Weather y Biome Regions permanecen consumidores futuros separados.
- `CameraRigController` conserva follow player-centric y `AllowsIndependentPan=false`; yaw/pitch/zoom/collision son una única autoridad de cámara y el collision query no reemplaza Interior Visibility.
- `ActorStaminaComponent` es estado gameplay del player; `PlayerMovementController` sigue siendo la única autoridad de movimiento, `ActorNeedsComponent` la autoridad de Hunger/Thirst y `WorldClock` la única autoridad temporal. Los controles F3/teleport/multiplicadores son development-only y efímeros.

## Próximo Trabajo

No hay milestone de implementación activo. `ID TBD — Player Traversal / Camera & Runtime Debug Ergonomics Pass` queda cerrado y publicado en `ab78da4fbb1af9189d6a5c178515fafdb56f368e`. El siguiente coding unit candidato es `ID TBD — Macro Environment / Biome Regions V1`, `PLANNED — NOT AUTHORIZED`. Debe consumir la truth ya validada de landform/Water/Climate para resolver regiones environment/biome globales sin confundir landform con biome ni iniciar vegetation/materiales finales.

Macro Climate Baseline V1 queda cerrado y no autoriza weather runtime, seasons, vegetation, final rivers, geology, terrain materials ni retuning climático sin un alcance posterior explícito. El Terrain Materialization Technical Spike y la convergencia runtime tampoco autorizan materialización productiva, whole-world Terrain/NavMesh, sector streaming/transitions ni mutación persistente general. El pass de traversal/camera/debug ergonomics no autoriza look-ahead/aim-camera, free pan, UI final ni una ampliación general de stats/fitness. M42.0 conserva su ID y alcance planificado, pero ya no es el siguiente trabajo automático. La secuencia M42.0–M47.1 requiere reconciliación posterior sin renumeración ni reutilización silenciosa.
