# Old Scars — Procedural Worldgen + Deformable Terrain Research

- **Fecha de corte:** 2026-09-16
- **Estado del documento:** `RESEARCH / ARCHITECTURE REFERENCE — NOT IMPLEMENTATION AUTHORITY`
- **Objetivo:** conservar una fotografía técnica del estado real de Old Scars al 2026-09-16, contrastarla con referencias externas de generación procedural y terreno deformable, y proponer una secuencia concreta para convertir la foundation actual en terreno procedural deformable de producción.
- **Baseline de repo auditado antes de crear este documento:** `dev` en `43e4c8729427e37f7387b01b7763e5e583fcb2d1`.
- **Alcance de verdad del repo:** este documento toma como verdad implementada lo publicado en `dev`. Cambios locales no publicados no se consideran autoridad de worldgen/terrain.

> Este archivo no asigna IDs nuevos, no reemplaza `Project_Roadmap.md`, no autoriza por sí solo una implementación y no convierte recomendaciones futuras en contratos ya implementados. Su función es dejar un baseline fechado para que, cuando se retome worldgen/terrain, se pueda comparar qué cambió desde 2026-09-16 y decidir qué partes de esta investigación siguen vigentes.

---

## 1. Resumen ejecutivo

La conclusión principal de esta investigación es que **Old Scars no necesita reemplazar su generación procedural para obtener terreno destruible**. La separación arquitectónica ya existente es compatible con la dirección correcta:

```text
WorldSeed / generation contracts
        ↓
MacroWorldPlan
        ↓
MacroGeography
        ↓
MacroWater
        ↓
MacroClimate
        ↓
MacroEnvironment
        ↓
WorldGameplayQuality / starter selection
        ↓
MacroHumanGeography
        ↓
WorldSession committed truth
        ↓
TerrainMaterializationPlan
        ↓
local physical representation
```

Además, la foundation volumétrica ya demostró el segundo bloque necesario:

```text
TerrainMaterializationPlan
        ↓
DeformableTerrainVolume
        ↓
shared density lattice
        ↓
technical chunks
        ↓
Marching Tetrahedra / Indexed Marching Cubes
        ↓
mesh + collider + local navigation
        ↓
localized 3D mutations
        ↓
dirty chunk rebuild
        ↓
operation replay persistence spike
```

La decisión estratégica que surge del contraste con Vintage Story, 7 Days to Die, Empyrion, No Man's Sky, Space Engineers y Enshrouded es:

> **El mundo global debe seguir siendo lógico, determinista, barato y persistente; la representación volumétrica debe existir sólo localmente donde hace falta. El terreno actual de una zona debe resultar de `baseline procedural determinista + estado de deformación persistente`.**

El problema técnico pendiente no es descubrir cómo generar montañas ni cómo hacer un túnel: ambas capacidades ya existen por separado y están parcialmente conectadas. Lo que falta es transformar el spike local en un sistema productivo mediante:

1. coordenadas estables de technical terrain chunks;
2. baseline volumétrica reproducible por chunk;
3. streaming/load/unload local;
4. autoridad única de mutaciones en coordenadas persistentes;
5. persistencia de edits por journal/delta con futura compaction;
6. scheduling de remesh/collider/navigation;
7. LOD y transiciones cuando el profiling real lo exija;
8. mayor detalle local del terrain baseline sin destruir las autoridades macro ya validadas.

---

## 2. Documentos y código de Old Scars usados como baseline

Este research debe leerse junto con:

- [Project_Roadmap.md](Project_Roadmap.md)
- [Technical_Architecture.md](Technical_Architecture.md)
- [Open_World_Architecture.md](Open_World_Architecture.md)
- [Deformable_Terrain_Foundation.md](Deformable_Terrain_Foundation.md)
- [Current_Milestone.md](Current_Milestone.md)

Código relevante auditado:

- `Assets/_OldScars/Scripts/Core/World/MacroGeographyGenerator.cs`
- `Assets/_OldScars/Scripts/Core/World/MacroWaterGenerator.cs`
- `Assets/_OldScars/Scripts/Core/World/MacroClimateGenerator.cs`
- `Assets/_OldScars/Scripts/Core/World/MacroEnvironmentGenerator.cs`
- `Assets/_OldScars/Scripts/Core/World/MacroHumanGeographyGenerator.cs`
- `Assets/_OldScars/Scripts/Core/World/TerrainMaterializationPlan.cs`
- `Assets/_OldScars/Scripts/Core/World/DeformableTerrainVolume.cs`
- `Assets/_OldScars/Scripts/Core/World/DeformableTerrainSpikeConfiguration.cs`
- `Assets/_OldScars/Scripts/Core/World/WorldDeformableTerrainSpikeController.cs`
- `Assets/_OldScars/Scripts/Core/Application/WorldRuntimeSceneController.cs`
- `Assets/_OldScars/Scripts/Core/Persistence/DeformableTerrainSpikePersistence.cs`
- `Assets/_OldScars/Editor/DeformableTerrainSpikeDiagnostics.cs`

### Nota sobre documentación histórica

`Open_World_Architecture.md` conserva partes escritas antes de que Climate/Environment y la foundation volumétrica quedaran implementados. Por eso, cuando existe una diferencia entre una frase histórica de ese documento y el estado actual del código/`Technical_Architecture.md`/`Project_Roadmap.md`, este research usa el estado implementado más reciente. Al 2026-09-16, Macro Climate, Macro Environment y Deformable Volumetric Terrain Foundation figuran como foundations validadas en el estado técnico actual.

---

## 3. Separar correctamente tres problemas

Para evitar scope creep y malas decisiones conviene mantener tres capas conceptualmente separadas.

### 3.1 Generación del mundo

Decide la verdad inicial de alto nivel:

- forma general de continentes y relieve;
- landforms;
- océanos/costas/drenaje;
- temperatura/humedad;
- familias ambientales/biomas;
- sitios humanos;
- carreteras e infraestructura macro;
- futura geología, vegetación, cuevas, recursos y detalle local.

### 3.2 Representación física

Decide cómo existe una parte del mundo en runtime:

- heightmap;
- bloques discretos;
- campo de densidad/SDF-like;
- malla generada desde volumen;
- colliders;
- NavMesh local;
- LOD.

### 3.3 Mutación

Decide qué ocurre cuando gameplay altera el terreno:

- cavar;
- abrir trincheras;
- explosiones/cráteres;
- túneles;
- excavación lateral;
- maquinaria;
- relleno futuro;
- persistencia de esas alteraciones.

**Regla recomendada:** no hacer que la capa de worldgen sea mutable por cada palada, y no hacer que el sistema de deformación se convierta en una segunda autoridad de worldgen.

---

# PARTE A — ESTADO ACTUAL DE OLD SCARS

## 4. Macro World Plan y determinismo

Old Scars ya separa identidad de mundo, seed, contracts por pass y truth committed. La dirección correcta para un mundo grande está presente:

- `WorldId` no es el seed;
- cada pass tiene un generation contract estable;
- los subdominios se derivan determinísticamente;
- no se usa `UnityEngine.Random` como autoridad de worldgen;
- los outputs lógicos se validan antes de materializar;
- hashes y provenance sirven como evidencia reproducible;
- el orden de visita/materialización no debería redefinir la truth macro.

Esta separación es especialmente valiosa para terrain streaming: un chunk técnico futuro debe poder reconstruir su baseline sin depender de qué chunk se cargó antes.

---

## 5. MacroGeography actual

`MacroGeographyGenerator` ya es un generador multi-field, no un único Perlin aplicado como heightmap.

Campos/domains implementados incluyen:

- `landform_regions`;
- `regional_upheaval`;
- `base_elevation`;
- `relief_detail`;
- `mountain_ridges`;
- `surface_roughness`.

El inner loop usa ruido determinista/fixed-point propio (`StableValueNoise2D`), fBm y una transformación ridged. El relieve final combina elevación base, detalle, rugosidad y crestas en función de un percentile de relieve.

Landforms actuales:

- `Plains`;
- `RollingHills`;
- `Highlands`;
- `Mountains`.

### Fortaleza

Esto ya se parece al enfoque correcto de sistemas como Vintage Story: varios campos con distinta escala y responsabilidad, no una sola función de ruido que intenta resolver todo.

### Limitación actual

El output sigue siendo **macro 2D**: elevation + landform. No contiene todavía una función 3D de subsuelo, cuevas naturales, fallas, estratos o provincias geológicas.

---

## 6. MacroWater actual

`MacroWaterGenerator` ya tiene una semántica geográfica más fuerte que “todo debajo de X altura es agua”.

Implementa:

- selección de sea level contra target de land coverage;
- océano limitado a agua conectada al boundary mundial;
- labels de cuerpos oceánicos;
- coastline;
- conditioned drainage;
- basin candidates.

### Fortaleza

Water consume la Geography committed y no reescribe el relieve. Esto debe mantenerse.

### Limitación actual

No existe todavía un sistema de ríos finales/materialización hidráulica local de producción ni fluid simulation de túneles excavados.

---

## 7. MacroClimate actual

`MacroClimateGenerator` produce un baseline térmico/hídrico global de creation-time.

Factores ya implementados:

- baseline latitudinal de temperatura;
- anomalía térmica regional;
- enfriamiento por elevación;
- moisture regional;
- distancia al océano;
- dirección dominante de transporte de humedad;
- efecto orográfico windward;
- reducción leeward/rain shadow.

### Fortaleza

El clima tiene causalidad con Geography y Water, en lugar de ser un mapa de biomas independiente. Esto se alinea bien con worldgen emergente.

### Limitación actual

Es climate baseline, no weather runtime, seasons ni simulación atmosférica continua.

---

## 8. MacroEnvironment actual

`MacroEnvironmentGenerator` clasifica ecological families a partir de Climate + Water. No agrega otro ruido arbitrario.

Cada muestra conserva:

- biome family principal;
- secundaria;
- transition weight.

### Fortaleza

Permite transiciones continuas y evita tratar los biomas como cajas desconectadas.

### Limitación actual

No existen todavía fields locales separados de forestation, shrubs, flora, suelo, rock province, etc.

---

## 9. MacroHumanGeography actual

`MacroHumanGeographyGenerator` ya genera hubs y roads sobre la verdad geográfica existente.

La selección de sitios considera, entre otros:

- land components;
- site/traversal potential;
- landform;
- coast distance;
- gradient;
- local relief;
- spacing determinista.

Las rutas consumen un traversal cost donde Mountains cuestan mucho más que Plains y Ocean es impassable.

### Fortaleza

Old Scars ya sigue un patrón útil para un survival procedural: primero geography, después human geography. Las carreteras no nacen como líneas aleatorias desconectadas del terreno.

### Limitación actual

La representación física de roads sigue siendo de spike/diagnóstico. No existe todavía road cut/fill, shoulder, bridges, local street network ni settlement blueprint de producción.

---

## 10. TerrainMaterializationPlan actual

`TerrainMaterializationPlan` es el seam fundamental entre world truth y representación Unity.

Contiene una ventana local con:

- heights;
- landforms;
- ocean mask;
- roads proyectadas;
- sea level;
- hashes/provenance de inputs macro.

Expone, entre otras cosas:

- `HeightNormalizedAtLocal(x,z)`;
- `LandformAt...`;
- `IsOcean...`;
- roads proyectadas.

### Decisión importante ya correcta

El plan es **ephemeral projection**, no world truth durable. Esto permite reemplazar el renderer/materializer sin reescribir MacroGeography.

---

## 11. Deformable Volumetric Terrain Foundation actual

La foundation documentada ya probó:

```text
Macro Geography
→ bounded shared density lattice
→ technical chunks
→ smooth polygonized mesh/collider
→ localized 3D mutation
→ dirty remesh
→ persistence/replay spike
```

Capacidades demostradas:

- cráter con `SubtractSphere`;
- túnel/cápsula con techo y piso reales;
- cross-chunk deformation;
- shared-border continuity;
- rebuild sólo de chunks afectados;
- colliders actualizados;
- local NavMesh;
- replay de mutaciones;
- validación contra foreign world/geography mismatch.

### Baseline técnico actual

`DeformableTerrainSpikeConfiguration.CreateBaseline()` resuelve actualmente:

- 2 × 2 chunks horizontales;
- 2 partitions verticales;
- 24 × 24 cells por chunk horizontal;
- 32 cells verticales totales;
- spacing horizontal 2 m;
- footprint aproximado 96 × 96 m;
- underground depth 32 m;
- air headroom 20 m;
- Surface layer 1.5 m;
- Soil hasta 10 m;
- después Rock.

### Cómo se construye actualmente el density field

La foundation consulta el surface height del `TerrainMaterializationPlan` y usa esencialmente:

```text
depth = surfaceHeight - localY
density = depth
```

Esto es suficiente para convertir el height-based macro surface en volumen excavable. También significa que el **baseline natural actual sigue siendo topológicamente 2.5D**: es una superficie extruida hacia abajo. Los overhangs/túneles existen después de una mutación, no porque MacroGeography ya genere terreno natural 3D.

### Estado de persistencia

El payload actual `deformable_terrain_spike_v1` está marcado explícitamente `SPIKE_NON_PRODUCTION`.

Guarda evidencia como:

- WorldId;
- SectorId;
- GeographyHash;
- configuración;
- origin;
- vertical spacing;
- operation list.

Al cargar, reconstruye baseline y replayea las mutaciones.

Esta es la idea correcta para la primera persistencia productiva, pero el formato final y la compaction siguen abiertos.

---

# PARTE B — INVESTIGACIÓN EXTERNA

## 12. Vintage Story

### 12.1 Qué hace

Vintage Story divide worldgen en múltiples stages. Su documentación pública describe, a alto nivel:

1. noise maps;
2. landforms;
3. rock strata;
4. caves;
5. block/surface layers;
6. deposits;
7. structures;
8. ponds;
9. vegetation/patches;
10. finalize.

También genera mapas separados para propiedades como:

- Climate;
- Flower;
- Bush;
- Forest;
- Beach;
- Geological Province;
- Landform.

Su worldgen es modular y se ejecuta por regiones/chunks a demanda. La documentación de modding define:

- chunk: 32 × 32 × 32 blocks;
- chunk column: columna vertical de chunks;
- map region: 16 × 16 chunk columns;
- generation passes;
- world generators en background thread.

### 12.2 Lecciones útiles para Old Scars

**Lección A — muchos fields, no un solo noise.**

Old Scars ya sigue esta dirección en MacroGeography/Water/Climate/Environment. Conviene profundizarla con nuevos fields, no reemplazarla.

**Lección B — geología separada de ecosistema.**

Vintage Story diferencia rock strata/geologic province de clima/vegetación. Old Scars hoy sólo tiene Surface/Soil/Rock en el terreno volumétrico. Un futuro `MacroGeology` puede responder “de qué está hecho el suelo”, mientras `MacroEnvironment` responde “qué ecosistema debería existir”.

**Lección C — worldgen por escalas y chunks.**

El mundo puede ser enorme porque las regiones físicas se generan cuando se necesitan. Old Scars debe adoptar ese principio para la materialización volumétrica.

**Lección D — biomas emergentes.**

Vintage Story combina temperatura, lluvia, roca, forestación y agua en lugar de depender exclusivamente de etiquetas discretas. Nuestro Climate/Environment ya está bien orientado a ese enfoque.

### 12.3 Qué NO copiar

Vintage Story usa bloques discretos como representación final. Old Scars tiene un requisito explícito de terreno suave/continuo con cavidades reales. Debemos copiar su **arquitectura modular/chunked**, no su estética ni su discretización visible.

---

## 13. 7 Days to Die

### 13.1 Qué hace

La documentación pública de RWG describe generación desde seed de:

- terrain;
- biomes;
- cities/towns;
- rural/wilderness;
- roads;
- Points of Interest.

Muchos POIs son prefabs authored que el generador coloca según reglas. El juego publicita cientos de ubicaciones authored dentro de mundos random.

El terreno y los edificios son destructibles, y el juego además posee structural stability para colapsos por daño/soporte insuficiente.

### 13.2 Lecciones útiles para Old Scars

**Lección A — procedural layout + authored content es una combinación fuerte.**

No conviene que Old Scars intente generar matemáticamente cada fábrica, vivienda o búnker. Una dirección más fuerte es:

```text
procedural macro geography
+ procedural site selection
+ procedural network layout
+ authored/modular POIs/buildings
```

Esto encaja especialmente bien con `MacroHumanGeography` y el futuro sector blueprint.

**Lección B — la destrucción no invalida el uso de prefabs.**

Un POI authored puede seguir existiendo dentro de un mundo con terreno mutable. La autoría de edificios y la representation terrain no necesitan ser el mismo sistema.

**Lección C — terrain adaptation importa.**

Roads/cities no deberían ser overlays visuales desconectados. El terrain local debe poder adaptarse a infrastructure mediante constraints/cut-fill limitados.

### 13.3 Qué NO copiar todavía

No necesitamos implementar structural integrity de edificios/suelo al mismo tiempo que terrain streaming. Eso es una feature separada y costosa.

---

## 14. Empyrion — Galactic Survival

### 14.1 Qué muestran las fuentes públicas

Eleon explicó históricamente que su procedural terrain se construía mediante sets de reglas/modules y después el seed generaba variaciones sobre esas reglas. También introdujeron heightmap-based terrains como alternativa para obtener formas más controladas.

A la vez, su material oficial sigue describiendo cada planeta como terrain voxel deformable que puede:

- aplanarse;
- excavarse;
- abrirse para recursos;
- convertirse en túneles.

Changelogs modernos siguen mencionando `terrain stamps` para formas como canyons y otras variantes.

### 14.2 Lección crítica para Old Scars

**La fuente inicial del terrain y la representación editable no tienen que ser la misma.**

Eso valida directamente nuestro seam actual:

```text
MacroGeography / projected surface
       ↓
DeformableTerrainVolume
```

Podemos generar el baseline desde una superficie 2D/macro y después representarla en un volumen 3D editable.

### 14.3 Terrain stamps / local features

Empyrion sugiere una idea útil para cerrar el gap entre nuestro macro relief y el terreno jugable cercano: una capa de **Local Terrain Features** o stamps deterministas.

Ejemplos futuros:

- ravines;
- escarpes;
- rocky outcrops;
- terraces;
- dry channels;
- crater-like natural formations;
- plateau cuts.

Estos features no deberían convertirse en otra autoridad mundial. Deben derivarse de la truth macro + un domain determinista local.

---

## 15. No Man's Sky

### 15.1 Lección de representación

El update oficial Worlds Part I informa que terrain generation fue reescrito para incorporar **dual marching cubes voxel meshing**, con el objetivo de reducir vertex count, acelerar generation, mejorar framerate y reducir memoria.

Esto refuerza que un mundo de gran escala puede utilizar:

```text
volumetric scalar field
→ polygonization
→ smooth terrain mesh
```

sin mostrar cubos.

### 15.2 Relación con Old Scars

Old Scars ya posee Marching Tetrahedra y un backend Indexed Marching Cubes dentro de la foundation. Esto no obliga a migrar a Dual Marching Cubes, pero valida la familia técnica de soluciones.

### 15.3 Decisión recomendada

Antes de adoptar un mesher nuevo, medir nuestros backends existentes con datasets productivos:

- vertex count;
- triangle count;
- allocation;
- generation time;
- remesh time;
- seam correctness;
- normals;
- collider cost;
- memory.

No elegir un algoritmo por prestigio externo sin profiling local.

---

## 16. Space Engineers

### 16.1 Evidencia pública útil

La documentación oficial de modding identifica `.vx2` como voxel data de asteroid/planet dentro de saves y expone voxel materials como parte del contenido moddable.

### 16.2 Lección para Old Scars

Un terrain volumétrico grande necesita tratar **material** y **densidad/ocupación** como conceptos separados.

Para Old Scars:

```text
Density → sólido / vacío / superficie
Material → qué tipo de sólido es
```

La foundation actual ya tiene una forma mínima de esto con Surface/Soil/Rock. La futura Geology debería ampliar el material field sin reemplazar density ni MacroEnvironment.

---

## 17. Enshrouded

### 17.1 Qué dicen sus desarrolladores

Keen Games explica que usan procedural tools durante creación, pero el world final es hand-crafted. En respuestas de desarrollo también describen su mundo como voxel y priorizan coherencia/detalle authored sobre una regeneración random completa por partida.

### 17.2 Lección útil

**Procedural y authored no son opuestos.**

Old Scars puede mantener un mundo seed-driven mientras utiliza:

- authored POIs;
- authored building modules;
- curated industrial/military compounds;
- local terrain templates;
- procedural placement/adaptation.

Esto evita que “procedural” se convierta en sinónimo de “todo parece noise”.

---

## 18. Transvoxel como referencia futura de LOD

Transvoxel existe para conectar meshes voxel de distintas resoluciones mediante transition cells y evitar cracks entre LODs.

Esto es relevante para Old Scars **sólo cuando exista terrain volumétrico streamable con más de una resolución real**.

No debe implementarse preventivamente durante la primera integración procedural/deformable.

---

# PARTE C — COMPARACIÓN DIRECTA

## 19. Matriz comparativa

| Sistema | Worldgen macro | Terrain físico | Deformación | Persistencia/streaming | Lección principal |
| --- | --- | --- | --- | --- | --- |
| Vintage Story | Multi-pass, climate, landforms, geology, caves, vegetation | voxel blocks | edición directa de bloques | chunk/region a demanda | modularidad, escalas, geología |
| 7 Days to Die | terrain + biomes + roads + settlements + prefabs/POIs | block/voxel world | destrucción de terreno/estructuras | world chunks + authored POIs | procedural placement + authored content |
| Empyrion | rules/modules + seed + heightmap/stamps | voxel terrain | flatten/dig/tunnels | planetary local data | baseline generation separada de deformabilidad |
| No Man's Sky | planetary procedural generation | voxel scalar field → smooth mesh | terrain editing | streaming/LOD a gran escala | smooth voxel polygonization |
| Space Engineers | procedural planets/asteroids | voxel data + materials | drilling/deformation | voxel save data | material field separado, persistencia volumétrica |
| Enshrouded | procedural tools + handcrafted final map | voxel terrain | player terrain editing | authored world state | procedural + authored pueden coexistir |
| Old Scars 2026-09-16 | Plan + Geography + Water + Climate + Environment + Human Geography | Unity Terrain spike o local deformable volume | sphere/capsule mutation | replay spike, sin production streaming | foundations correctas; falta productization |

---

## 20. Dónde Old Scars ya está fuerte

Comparado con estas referencias, Old Scars ya tiene buenas decisiones estructurales:

1. **Pass isolation determinista.**
2. **Macro Geography separada de Water/Climate/Environment.**
3. **Climate causal** con ocean influence y orography.
4. **Human Geography** dependiente del terrain en vez de totalmente random.
5. **World truth separada de Unity representation.**
6. **TerrainMaterializationPlan** como seam explícito.
7. **Volumetric density local** ya probado.
8. **Dirty chunk mutation rebuild** ya probado.
9. **Collider + local navigation** ya probados sobre terrain volumétrico.
10. **Replay persistence** ya demostrado conceptualmente.
11. **Prohibición explícita de whole-world voxel allocation.**
12. **World Sector separado de technical chunk.**

Estas decisiones deben conservarse.

---

## 21. Gaps principales al 2026-09-16

### Gap 1 — El volume es local/spike, no world-addressable

Actualmente el volumen tiene un origin local y un footprint acotado. No existe todavía un grid estable de terrain chunks direccionable en coordenadas mundiales.

### Gap 2 — No existe streaming productivo

No hay todavía carga/descarga de density/mesh/collider por proximidad al Player.

### Gap 3 — Persistencia todavía usa semántica de spike

`SPIKE_NON_PRODUCTION`; no existe journal productivo por chunk ni compaction.

### Gap 4 — Baseline natural 3D limitada

La topología inicial es una surface field extruida hacia abajo. Las caves actuales son producto de edits de prueba, no de worldgen.

### Gap 5 — Material field demasiado simple

Surface/Soil/Rock sólo prueba el seam. No hay geologic province/strata/material variation de producción.

### Gap 6 — Meso/micro relief insuficiente

MacroGeography da buena forma regional, pero todavía falta una capa local de terrain features para evitar superficies demasiado suaves/genéricas al acercarse.

### Gap 7 — Roads todavía no modifican terrain de producción

El plan las proyecta, pero todavía no existe cut/fill/materialization real.

### Gap 8 — LOD volumétrico no existe

Todavía no hay near/far representations ni seam entre resoluciones.

### Gap 9 — Remesh cost aún no es un budget de gameplay

El spike documentó remesh de alrededor de 149 ms en un caso medido. Es evidencia de viabilidad, no un tiempo aceptable por cada golpe de pala.

### Gap 10 — Dynamic navigation todavía es spike

No existe una política productiva de cuándo/debounced cómo reconstruir navigation tras edits frecuentes.

---

# PARTE D — ARQUITECTURA OBJETIVO RECOMENDADA

## 22. Regla central

La dirección recomendada es:

```text
CURRENT TERRAIN
=
DETERMINISTIC PROCEDURAL BASELINE
+
PERSISTENT LOCAL DEFORMATION STATE
```

Esto implica:

- no guardar el baseline completo si puede reconstruirse exactamente;
- no recalcular edits del jugador desde worldgen;
- no permitir que un cambio de generador reinterprete silenciosamente un mundo ya committed;
- no materializar el mundo completo en máxima resolución.

---

## 23. Pipeline objetivo

```text
WorldSeed + pass contracts + committed WorldSession truth
        ↓
Macro Geography / Water / Climate / Environment / Human Geography
        ↓
Sector / local materialization context
        ↓
Local Terrain Baseline Sampler
        ├─ macro surface
        ├─ local landform refinement
        ├─ optional deterministic terrain features/stamps
        ├─ road/site terrain constraints
        ├─ future caves
        └─ future geology/material assignment
        ↓
Stable TerrainChunk coordinates
        ↓
Density + Material fields
        ↓
Mesher
        ↓
Mesh + Collider
        ↓
Navigation contribution
        ↓
Persistent Mutation Layer
        ↓
Dirty remesh/collider/nav scheduling
```

---

## 24. Stable TerrainChunk coordinates

Este es el siguiente contrato técnico importante.

Un technical terrain chunk debe representar siempre la misma región lógica independientemente de:

- GameObject instance;
- orden de carga;
- frame timing;
- Player spawn;
- sector-local Transform runtime.

Conceptualmente:

```text
TerrainChunkKey
  sector/world context
  chunkX
  chunkY
  chunkZ
  terrain baseline contract/version
```

No se congela todavía el tipo público ni schema exacto.

### Invariante

```text
same world committed truth + same chunk key + same baseline contract
= same baseline density/material data
```

---

## 25. World Sector != Technical Terrain Chunk

Conservar esta separación del design existente.

Un sector es una unidad lógica/gameplay grande.

Un terrain chunk es una partición interna para:

- density memory;
- meshing;
- collider;
- LOD;
- mutation dirty bounds;
- persistence batching;
- streaming;
- profiling.

Cambiar el tamaño técnico de chunks en una futura optimización no debe cambiar WorldId/SectorId ni la geografía del mundo.

---

## 26. Baseline density y material

La forma mínima actual:

```text
surface = MacroGeography projection

density(position) = surfaceHeight(x,z) - y
```

es una buena V1 productiva.

La expansión futura debe mantener separadas dos consultas conceptuales:

```text
Density(position)
Material(position)
```

### Density

Responde:

- sólido;
- vacío;
- superficie/interpolación.

### Material

Responde qué existe si es sólido:

- topsoil;
- clay;
- sand;
- gravel;
- limestone;
- granite;
- shale;
- future ore-bearing material;
- etc.

No es necesario implementar esta variedad en la primera integración.

---

## 27. Local Terrain Features

Se recomienda una capa futura derivada entre MacroGeography y density final cercana.

Ejemplo:

```text
MacroLandform = Highlands
        ↓
local deterministic domain
        ↓
regional feature set
        ↓
ravine / escarpment / outcrop / terrace / minor valley
```

Propiedades deseadas:

- deterministic;
- estable por coordenadas;
- no depende de orden de carga;
- consulta upstream macro truth;
- no reescribe MacroGeography;
- puede ser versionada independientemente;
- no necesita GameObjects para decidir geometry.

Esta capa es la mejor candidata para mejorar detalle cercano sin inflar la resolución del raster macro global.

---

## 28. MacroGeology futura

Recomendación fuerte, pero no requisito para la primera integración.

Separar:

```text
MacroEnvironment → clima/ecosistema
MacroGeology    → composición física del subsuelo
```

MacroGeology podría contener:

- province identity;
- rock families;
- strata tendencies;
- faults/large formations;
- resource/deposit suitability;
- future cave/geologic context.

Después, el local terrain sampler puede realizar esos fields a resolución jugable.

---

## 29. Cuevas naturales

No deben agregarse alterando MacroGeography.

Dirección recomendada:

```text
surface density
+
3D cave field/operators
=
baseline volumetric terrain
```

Las caves de worldgen deben ser baseline, mientras los túneles del jugador son mutations posteriores.

Esta distinción es necesaria para persistencia:

```text
Natural cave → reproducible baseline
Player tunnel → persistent mutation
```

---

## 30. Roads como constraints geométricas

Las roads ya existen en `TerrainMaterializationPlan` como polylines proyectadas.

No deberían terminar sólo como una textura o línea encima de una pendiente imposible.

Dirección futura:

```text
MacroRoad polyline
      ↓
local corridor sampling
      ↓
cut / fill / grade constraint
      ↓
shoulder / ditch / material surface
```

El resultado debe conservar terreno natural alrededor y adaptar sólo el corredor necesario.

Esto también prepara puentes/culverts más adelante sin hacer que el road system sea dueño del terrain global.

---

## 31. POIs, edificios y sites

Inspirado por 7DTD y Enshrouded, Old Scars debería favorecer un híbrido:

```text
procedural site selection
+
procedural orientation/context
+
authored/modular content
+
local terrain adaptation
```

No conviene generar matemáticamente cada edificio desde cero salvo que una feature futura lo justifique.

---

# PARTE E — PERSISTENCIA DE DEFORMACIÓN

## 32. Primera estrategia productiva

La foundation ya probó la idea adecuada:

```text
baseline procedural reproducible
+
mutation journal
```

Ejemplos de operaciones:

- subtract sphere;
- subtract capsule;
- future add/fill operation;
- future material replacement si existe un consumidor real.

### Requisito nuevo

Las operaciones productivas deben expresarse en coordenadas persistentes de terrain/world, no depender del local origin efímero de un único volume spike.

---

## 33. Por qué no guardar el world voxel completo

Guardar una matriz volumétrica completa de máxima resolución para un mundo de kilómetros sería costoso en:

- memoria;
- IO;
- save size;
- load time;
- migration;
- compatibility;
- backups.

Si el baseline es determinista, guardar el baseline es redundante.

Por eso:

```text
REGENERATE BASELINE
+
LOAD ONLY MUTATIONS
```

es la estrategia inicial preferida.

---

## 34. Compaction futura

Un journal puro puede degradarse si una zona recibe miles de edits.

Dirección futura híbrida:

```text
Baseline
+
Compacted dirty-chunk delta/snapshot
+
Recent operations
```

No se recomienda fijar ahora:

- threshold de compaction;
- formato binario;
- compression;
- snapshot cadence.

Primero medir saves reales.

---

## 35. Versionado y safety

Cada terrain mutation payload productivo debería poder validar como mínimo:

- world identity;
- sector/terrain region identity;
- baseline generation contract;
- relevant upstream hash/provenance;
- chunk/key compatibility;
- mutation schema version.

Un mismatch debe fallar con diagnóstico; nunca aplicar silenciosamente edits sobre otro baseline.

La foundation ya demostró parte de esta disciplina con `WorldId`, `SectorId`, `GeographyHash` y configuración.

---

# PARTE F — STREAMING, RENDER, PHYSICS Y NAVIGATION

## 36. Streaming recomendado

No mantener density/mesh/colliders del mundo completo.

Conceptualmente:

```text
near terrain
  full density
  full mesh
  collider
  mutation-ready
  navigation contribution

mid terrain
  lower-cost representation
  limited/no collider según necesidad

far terrain
  macro/simplified visual representation
```

Los radii/tamaños no deben congelarse sin profiling.

---

## 37. Unload/reload

Flujo esperado:

```text
enter chunk range
→ resolve baseline chunk
→ load compacted state/journal
→ replay/apply mutation state
→ mesh
→ collider
→ navigation registration

leave chunk range
→ flush dirty persistent state
→ release mesh/collider/density runtime data
```

Test fundamental:

```text
load chunk
→ capture baseline evidence
→ unload
→ reload
→ same baseline evidence
```

Y con edits:

```text
load
→ mutate
→ save/unload
→ reload
→ same resulting terrain
```

---

## 38. Dirty remesh

Conservar la propiedad ya validada:

```text
mutation bounds
→ affected technical chunks only
→ rebuild only affected chunks
```

No hacer:

- remesh de sector completo;
- rebuild global;
- whole-world collider update.

---

## 39. Collider scheduling

Para una pala/explosión:

- gameplay debe ver la nueva superficie con latencia bounded;
- no necesariamente cada sub-edit necesita un collider rebuild individual si varios edits ocurren en el mismo frame/intervalo;
- se debe permitir coalescing por dirty chunk.

La policy exacta requiere profiling.

---

## 40. Navigation scheduling

No reconstruir NavMesh completo por cada palada.

Dirección sugerida:

1. terrain chunk cambia;
2. navigation dirty region se marca;
3. edits cercanos se agrupan/debounce;
4. rebuild local acotado;
5. ActorNavigationController sigue siendo autoridad de movimiento, no terrain.

El terrain system publica geometría/navegabilidad; no crea una segunda AI/navigation authority.

---

# PARTE G — LOD Y MESHING

## 41. Mesher actual

La foundation validó Marching Tetrahedra y existe Indexed Marching Cubes.

No declarar todavía un backend final.

### Benchmark requerido antes de elegir

Mismo dataset y mismas mutations para ambos:

- initial generation ms;
- remesh ms;
- allocations;
- vertices;
- triangles;
- mesh bytes;
- collider ms;
- seam agreement;
- normals/visual quality;
- reproducibility.

---

## 42. LOD futuro

Cuando existan chunks a distintas resoluciones surgirán cracks en boundaries si se triangulan independientemente.

Transvoxel es una referencia válida para resolver transitions entre grids de distinta resolución.

**No implementar antes de tener un consumidor real de multi-resolution terrain.**

---

## 43. Far terrain y edits pequeños

No hace falta que un agujero pequeño sea visible a kilómetros.

Una estrategia razonable:

- near: full deformation;
- mid: aggregated/simplified terrain;
- far: baseline macro surface.

Sólo deformaciones muy grandes podrían requerir propagación a un LOD lejano.

Esto se decide cuando exista LOD real.

---

# PARTE H — RENDIMIENTO

## 44. Qué prueban los números actuales

`Deformable_Terrain_Foundation.md` registra aproximadamente:

- density: 6–16 ms;
- initial mesh: 156–160 ms;
- assignment: ~5 ms;
- collider: ~6 ms;
- affected mesh rebuild: ~149 ms en la medición reportada;
- local NavMesh baseline/deformed: decenas de ms.

Estos números prueban **viabilidad**, no budgets de shipping.

---

## 45. Optimización en orden correcto

No saltar directamente a Jobs/Burst/GPU.

Orden recomendado:

1. definir chunk identity y streaming real;
2. medir con chunks productivos;
3. reducir trabajo innecesario;
4. coalesce edits;
5. elegir chunk size/resolution por evidencia;
6. comparar meshers;
7. separar generation/meshing/collider/nav scheduling;
8. recién después evaluar Jobs/Burst/GPU si el profiler demuestra necesidad.

---

## 46. Resolución

El spacing actual de 2 m es excelente para un technical spike, pero probablemente demasiado grueso para ciertas trincheras, cortes pequeños o herramientas manuales.

No se recomienda decidir hoy un valor final.

Benchmark futuro sugerido, en el mismo footprint/chunk contract:

- 2.0 m;
- 1.5 m;
- 1.0 m;
- 0.5 m sólo si existe una necesidad visual/gameplay fuerte.

Medir memoria, triangles, remesh y collider. Bajar spacing incrementa muy rápidamente el número de samples.

---

# PARTE I — PLAN DE IMPLEMENTACIÓN RECOMENDADO

## 47. Fase 0 — Audit / Integration Gate

Antes de escribir arquitectura nueva, confirmar cuánto del “merge” ya funciona en `WorldRuntime`.

Gate:

```text
New Game seed
→ WorldSession generado normalmente
→ seleccionar volumetric backend
→ TerrainMaterializationPlan desde truth real
→ DeformableTerrainVolume
→ Player spawn correcto
→ caminar sobre relieve procedural
→ mutate arbitrary position
→ collider/nav siguen coherentes
```

Objetivo: no reimplementar el seam que ya existe.

### Importante

El “agujero central” o crater/tunnel usado por diagnostics no debe confundirse con baseline productivo. Un mundo nuevo debe iniciar con su terrain procedural intacto salvo features naturales generated.

---

## 48. Fase 1 — Stable Terrain Chunk Identity

Implementar una partición técnica world-addressable.

Acceptance mínimo:

- misma seed/world + misma chunk key → mismo baseline hash/evidence;
- orden de generación diferente → mismo resultado;
- chunks adyacentes comparten samples/boundary sin cracks;
- no depende de Transform/GameObject.

No streaming todavía si no hace falta para cerrar este gate.

---

## 49. Fase 2 — Procedural Volumetric Terrain Integration V1

Promover la representation volumétrica desde “single bounded spike volume” a baseline de varios technical chunks estables dentro de una región real.

Acceptance:

- relieve coincide con MacroGeography;
- landform transitions siguen coherentes;
- Water/roads siguen disponibles como inputs aunque todavía no se materialicen final;
- Player/physics funcionan;
- una mutation sólo ensucia chunks intersectados.

---

## 50. Fase 3 — Streaming V1

Cargar un conjunto pequeño alrededor del Player.

Acceptance:

- mover Player fuerza load/unload;
- chunks recargados son bit/evidence-equivalent en baseline;
- memory no crece monotonically al recorrer ida/vuelta;
- no hay seams visibles/físicos entre chunks loaded.

No introducir LOD todavía si full-resolution bounded ring alcanza para probar el lifecycle.

---

## 51. Fase 4 — Persistent Terrain Mutation V1

Mover operations a coordenadas estables y persistencia por terrain chunk/region.

Acceptance:

```text
mutate
→ save/unload
→ reload
→ exact terrain edit restored
```

Casos:

- mutation dentro de un chunk;
- border de 2 chunks;
- corner de 4 chunks;
- foreign world rejected;
- baseline contract mismatch rejected;
- partial/corrupt state no reemplaza state válido.

---

## 52. Fase 5 — Gameplay Terrain Mutation Authority

Introducir una frontera productiva única consumible por futuras herramientas.

Ejemplo conceptual:

```text
Shovel / Explosion / Machine
        ↓
TerrainMutationAuthority
        ↓
canonical mutation operation
        ↓
affected chunks
        ↓
persistence + remesh + collider/nav dirtiness
```

Ningún consumidor debe modificar raw density buffers directamente.

---

## 53. Fase 6 — Performance / Scheduling Pass

Medir gameplay real y resolver:

- mesher;
- chunk dimensions;
- resolution;
- dirty batching;
- async/background work;
- collider schedule;
- nav schedule;
- allocations.

Sólo aquí considerar Jobs/Burst/GPU.

---

## 54. Fase 7 — Terrain LOD

Cuando streaming full-res sea correcto:

- añadir lower-resolution representations;
- medir cracks;
- evaluar Transvoxel u otra transition strategy;
- no permitir que LOD cambie truth/persistence.

---

## 55. Fase 8 — Local Terrain Features / Geology / Caves

Una vez estable la base productiva:

- Local Terrain Features;
- MacroGeology;
- rock/material realization;
- caves naturales;
- deposits/resources.

No mezclar este contenido con la primera implementación de streaming.

---

## 56. Fase 9 — Roads/sites terrain realization

Aplicar cut/fill y adaptación local sobre las roads/sites committed.

Acceptance futuro:

- road evita slopes absurdas;
- terrain corridor conserva continuity;
- no rerollear road identity/path;
- POI placement adapta terreno sin destruir macro geography fuera del footprint.

---

# PARTE J — PLAN DE VALIDACIÓN

## 57. Determinismo

Cada fase debe incluir pruebas fresh-process cuando corresponda:

- same seed;
- same chunk key;
- different generation order;
- unload/reload;
- save/reload;
- hashes/evidence equivalentes.

---

## 58. Seam tests

Obligatorios para terrain chunking:

- density shared border;
- mesh positional agreement;
- normals;
- collider continuity;
- mutation cruzando X;
- mutation cruzando Z;
- mutation en corner X/Z;
- futuro Y partition si se usa runtime streaming vertical.

---

## 59. Mutation tests

- sphere contained;
- capsule/tunnel;
- repeated overlapping operations;
- huge radius rejected/bounded según contract;
- invalid NaN/Infinity rejected;
- operation outside loaded region resuelta a chunk correcto o rechazada explícitamente;
- unload/reload replay.

---

## 60. Performance tests

Registrar por chunk y por mutation:

- density generation ms;
- meshing ms;
- allocations;
- mesh bytes;
- collider update;
- nav update;
- loaded chunk count;
- total terrain memory;
- queue latency.

No concluir rendimiento por FPS global únicamente.

---

# PARTE K — DECISIONES QUE NO DEBEN TOMARSE TODAVÍA

## 61. No congelar aún

- tamaño final de terrain chunk;
- voxel spacing final;
- Marching Tetrahedra vs Indexed MC vs otro mesher final;
- Jobs/Burst/GPU;
- Transvoxel;
- compaction threshold;
- terrain edit save format final;
- full geology schema;
- cave algorithm;
- fluid simulation;
- structural terrain collapse;
- building foundation simulation;
- far terrain deformation fidelity.

Todas necesitan consumidores y profiling reales.

---

## 62. No hacer

- whole-world dense voxel array;
- mesh gigante por sector;
- guardar baseline completo si es reproducible;
- hacer a Shovel/Explosion dueños de raw density;
- convertir terrain deformation en Health/Physics authority paralela;
- reconstruir todo el NavMesh por cada palada;
- mutar MacroGeography después de cada edit local;
- usar GameObject position como identidad durable;
- dejar que una actualización del generator regenere silenciosamente sectores ya committed;
- mezclar de golpe terrain streaming + fluids + collapse + geology + resources.

---

# PARTE L — PRIORIDAD RECOMENDADA DESDE EL ESTADO ACTUAL

## 63. Qué haría cuando se retome terrain/worldgen

**Primer objetivo grande:** convertir el volume spike en materialización procedural volumétrica world-addressable y streamable, sin sumar todavía contenido geológico/caves complejo.

Orden:

```text
1. Audit de integración actual
2. Stable terrain chunk identity
3. Multi-chunk procedural baseline
4. Streaming lifecycle
5. Persistent mutation coordinates/state
6. Unified mutation authority
7. Profiling/scheduling
8. LOD
9. Local features/geology/caves
10. Road/site terrain realization
```

### Razón

La foundation actual ya demuestra que smooth deformable terrain es posible. Agregar geología/cuevas ahora aumentaría contenido pero no resolvería el cuello arquitectónico: todavía no se puede recorrer un mundo grande manteniendo esos volumes de forma estable.

---

## 64. Primer gate visual recomendado

El primer milestone de producto debería culminar en una prueba reconocible:

1. crear world con seed;
2. entrar a una región real generada por MacroGeography;
3. caminar varios cientos de metros mientras terrain chunks cargan/descargan;
4. observar costa/relieve/roads context correctos donde corresponda;
5. cavar un pozo;
6. cavar una trinchera;
7. abrir un túnel lateral;
8. salir del rango;
9. volver;
10. confirmar que las deformaciones persisten.

Cuando eso funcione, se podrá afirmar de forma productiva:

> **Old Scars tiene terrain procedural volumétrico deformable y persistente.**

Al 2026-09-16 la formulación correcta es:

> **Old Scars ya tiene worldgen procedural macro validado y una foundation volumétrica deformable validada que consume el mismo seam de materialización; falta productizar su unión mediante chunk addressing, streaming y persistencia local.**

---

# PARTE M — FUENTES EXTERNAS CONSULTADAS

Fecha de revisión de estas fuentes: **2026-09-16**.

## Vintage Story

- Terrain Generation — stages, noise maps, landforms, rock strata, caves, deposits, vegetation:  
  https://wiki.vintagestory.at/Terrain_Generation
- Modding: WorldGen Concept — chunks 32³, chunk columns, map regions, generation passes, background generation:  
  https://wiki.vintagestory.at/Modding%3AWorldGen_Concept
- World generation / Biomes — environment emergente por temperature, rock, tree/water density:  
  https://wiki.vintagestory.at/Biomes
- World Configuration — world/landform/climate generation parameters:  
  https://wiki.vintagestory.at/World_Configuration

## 7 Days to Die

- Official Wiki — Random World Generation, seed, biomes, cities/towns/rural/wilderness, POI placement:  
  https://7daystodie.wiki.gg/wiki/Random_World_Generation
- Steam product description — random worlds, roads/caves/locations, destructive terrain/structural stability:  
  https://store.steampowered.com/app/251570/7_Days_to_Die/
- Modding Wiki — `rwgmixer.xml`, township/district/streettile/prefab generation rules:  
  https://7d2dmodding.wiki.gg/wiki/Rwgmixer.xml

## Empyrion — Galactic Survival

- Official press kit — voxel-based deformable planetary terrain, flattening, digging and tunnels:  
  https://empyriongame.com/press-kit/
- Developer FAQ/forum — procedural rules/modules + seed, heightmap terrain alternative and performance discussion:  
  https://empyriononline.com/threads/alpha-7-faq-and-feedback-heightmap-terrain-texture-editor.21947/
- Official Steam news/changelogs — terrain stamp variants in modern content pipeline:  
  https://steamcommunity.com/app/383120/allnews/

## No Man's Sky

- Worlds Part I Update — terrain generation rewritten around dual marching cubes voxel meshing; memory/performance rationale:  
  https://www.nomanssky.com/worlds-part-I-update/

## Space Engineers

- Official Modding Basics — voxel materials and `.vx2` voxel data for asteroids/planets in saves:  
  https://www.spaceengineersgame.com/modding-guides/modding/

## Enshrouded

- Developer FAQ — procedural tools during creation + hand-crafted final world:  
  https://steamcommunity.com/app/1203620/discussions/1/3880472899720598427/
- Developer-marked answer — no plan for runtime procedural map; handcrafted world with generation tools:  
  https://steamcommunity.com/app/1203620/discussions/0/4762081419109490933/

## Voxel LOD

- Transvoxel Algorithm — transition cells para unir voxel terrain meshes de distinta resolución:  
  https://transvoxel.org/

---

# PARTE N — CÓMO USAR ESTE DOCUMENTO EN EL FUTURO

## 65. Cuando se vuelva a abrir este archivo

Antes de implementar, verificar:

1. fecha actual vs `2026-09-16`;
2. HEAD/branch actual;
3. si `Technical_Architecture.md` cambió;
4. si `Deformable_Terrain_Foundation.md` fue superseded;
5. si ya existe stable terrain chunk identity;
6. si existe production streaming;
7. si persistencia de mutations ya dejó de ser `SPIKE_NON_PRODUCTION`;
8. si se añadió Geology/caves/local feature layer;
9. si hubo benchmarks nuevos de mesher/resolution;
10. si fuentes externas o versiones de juegos cambiaron de forma relevante.

No asumir que este research sigue siendo current sólo porque la estrategia general siga siendo válida.

---

## 66. Estado final de este research

**RESEARCH COMPLETE — 2026-09-16**

Conclusión preservada:

```text
Old Scars no debe reemplazar su worldgen para obtener terrain deformable.
Debe productizar el seam que ya existe:

committed procedural world truth
→ local deterministic terrain baseline
→ stable chunked volumetric representation
→ smooth mesh/collider
→ persistent localized deformation
→ streaming / unload / rebuild
```

La mejor inversión futura es resolver **chunk identity + streaming + persistent mutation** antes de ampliar la cantidad de contenido geológico o visual del terreno.
