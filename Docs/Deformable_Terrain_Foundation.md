# Old Scars — Deformable Volumetric Terrain Foundation

- Estado de dirección: `APPROVED — PRODUCT REQUIREMENT PRESERVED`
- Estado de implementación: `VALIDATED — TECHNICAL SPIKE COMPLETE`
- Tipo: arquitectura/world runtime/terrain foundation
- Autoridad relacionada: [Project_Roadmap.md](Project_Roadmap.md), [Open_World_Architecture.md](Open_World_Architecture.md)
- Commit técnico validado: `d0309cf053be220a22151cae2dae9aca6f988e6f`
- Commit de integración con `dev`: `1b41ead829cd566c55df5adfc0522e33e1dffb96`

## Decisión De Producto

El terreno productivo de Old Scars debe ser físicamente alterable de forma volumétrica y persistente.

El objetivo no es un terreno cúbico tipo Minecraft. La representación final debe poder verse continua/suave y, al mismo tiempo, admitir modificaciones tridimensionales reales.

Casos de uso objetivo futuros:

- cavar con herramientas;
- crear pozos, zanjas y trincheras;
- cráteres y deformación por explosiones;
- excavación lateral;
- túneles y cuevas;
- socavar o cortar terreno;
- revelar estratos/materiales subterráneos;
- conservar las modificaciones al guardar, descargar y volver a visitar una zona.

Una solución limitada a heightmap no satisface este requisito porque no puede representar techos, overhangs, cavidades, túneles ni múltiples superficies verticales para un mismo X/Z.

## Resultado Del Technical Spike

El spike cerró satisfactoriamente la pregunta técnica principal: Old Scars puede derivar una representación volumétrica local desde la Macro Geography existente, dividirla en chunks técnicos, generar mesh/collider, aplicar mutaciones tridimensionales localizadas y reconstruir/persistir esas mutaciones sin reemplazar las autoridades macro ni crear una matriz voxel mundial.

Representación validada:

`Macro Geography → bounded shared density lattice → technical chunks → Marching Tetrahedra mesh/collider → localized terrain mutation → spike persistence/replay`

Baseline probado:

- `2×1×2` chunks técnicos;
- `24×32×24` cells por chunk;
- spacing horizontal `2`;
- spacing vertical resuelto `1.759`;
- `79,233` density samples;
- baseline density aproximada `713,097 B`;
- baseline mesh `55,296` vertices / `18,432` triangles / ~`1,990,656 B`;
- comparación adicional `16×22×16` cells/chunk, spacing `3`, `25,047` samples / `225,423 B`.

Meshing elegido para el spike: `Marching Tetrahedra`. Esta elección queda validada como implementación técnica pequeña y auditable para la foundation, pero no congela para siempre el algoritmo productivo si evidencia posterior demuestra una alternativa mejor.

Pruebas volumétricas validadas:

- cráter mediante `SubtractSphere`;
- túnel/cápsula con techo y suelo reales, imposible de representar correctamente con una sola heightmap;
- collider funcional en roof/floor;
- deformaciones cross-chunk;
- dirty rebuild de `1` chunk cuando queda contenido, `2` en bordes X/Z y `4` en esquina;
- shared-border mesh agreement exacto después de la corrección del seam;
- player existente atravesando chunk boundary, crater y túnel sin controlador paralelo.

## Persistencia Del Spike

El payload técnico es `deformable_terrain_spike_v1`, schema `1`, marcado explícitamente `SPIKE_NON_PRODUCTION`.

Reutiliza el envelope/store M37 y no cambia `world_session_v1`, que permanece schema `7`.

La prueba persistió dos operaciones en `1,511 B` y validó:

`teardown → baseline reconstruction → canonical replay → mismo density evidence`

Hash de evidencia reproducido:

`bad63130d7a6e7053334864f1bf65cc96c3c471428349919d6673cf38a18eb59`

Foreign-world, mutation kind inválido y partial replay fallan antes de reemplazar estado válido. La representación exacta de persistencia productiva/compaction permanece futura.

## Evidencia De Rendimiento Del Spike

Mediciones finales reportadas:

- density: `6–16 ms`;
- initial mesh: `156–160 ms`;
- assignment: `5 ms`;
- collider creation: `6 ms`;
- corner mutation: `0 ms`;
- affected mesh rebuild: `149 ms`;
- collider update: `5 ms`;
- local NavMesh baseline: `44 ms`, `400` vertices;
- local NavMesh deformed: `31 ms`, `499` vertices.

No son budgets productivos ni justifican todavía Jobs/Burst/GPU. Sirven como baseline para decisiones posteriores de resolución, chunk size, scheduling, LOD y navegación dinámica local.

## System Harmony Validado

El cierre confirmó que no se introdujo:

- segunda autoridad de Macro Geography;
- segunda autoridad de WorldSession/persistence;
- segunda autoridad de terrain mutation;
- mutación de raw density buffers por consumidores gameplay;
- whole-world voxel allocation;
- mesh voxel único gigante;
- rebuild global por una mutation local;
- schema bump oculto de `world_session_v1`;
- player controller paralelo;
- navigation authority paralela;
- package/plugin externo;
- framework Jobs/Burst/GPU preventivo;
- geología/mining/fluid scope creep;
- afirmación de que la persistencia del spike ya sea la persistencia productiva final.

Los goldens Plan/Geography/Water/Climate/Environment/Human Geography permanecieron intactos y las regresiones de runtime, persistence, player, Environment, pass isolation, Terrain Materialization anterior y navegación local pasaron.

## Consecuencia Sobre El Spike Heightmap Anterior

`Terrain Materialization Technical Spike` permanece `VALIDATED — TECHNICAL SPIKE COMPLETE` y no se considera trabajo perdido.

El spike con Unity `Terrain/TerrainCollider` sigue siendo evidencia válida para:

- proyección Macro Geography → espacio Unity;
- escala local provisional;
- coastline/ocean context;
- roads diagnósticas;
- player bootstrap;
- WorldRuntime integration;
- NavMesh y timings de referencia.

Pero Unity Terrain deja de ser candidato implícito a representación productiva definitiva. Es un benchmark/prototipo de materialización heightmap, no una autoridad de producto que futuras features deban asumir.

No construir nuevas dependencias productivas que requieran que el terreno final sea una única heightmap.

## Dirección Técnica Preservada

La dirección validada queda:

`macro world truth → sector/local terrain baseline → chunked volumetric density/material field → smooth mesh/collider → localized deformation → persistent terrain mutations`

Debe conservar estas capacidades:

1. representación volumétrica real;
2. partición técnica local/chunked;
3. mesh visible continuo o suficientemente suave para la dirección artística;
4. colliders actualizables localmente;
5. modificación localizada sin reconstruir el mundo entero;
6. persistencia de las modificaciones;
7. compatibilidad con la futura arquitectura de sector activo/inactivo;
8. consumo de Macro Geography/Water/Climate/Environment sin reemplazar esas autoridades.

`World Sector` y `Voxel/Technical Chunk` siguen siendo conceptos distintos. El producto no mantendrá el mundo completo como una matriz voxel de máxima resolución en memoria.

## Mutación Y Persistencia Productiva — Dirección Futura

Principio deseado:

`procedural/committed baseline + persistent deformation state = current terrain`

La foundation probó operations/replay como evidencia técnica, pero no congela el formato productivo. Más adelante debe elegirse proporcionalmente entre operations/deltas, compacted dirty chunks o una combinación, manteniendo preflight, canonicalización y reconstrucción reproducible.

Pala, explosivos, maquinaria y herramientas futuras deben consumir una frontera común de terrain mutation; no crear su propio sistema de deformación ni escribir buffers internos directamente.

## Materiales Y Apariencia Cercana

El spike validó un baseline visual técnico mate y legible con materiales simples Surface/Soil/Rock y checker textures generadas en memoria. No se comprometieron texture assets finales.

Esto prueba la viabilidad de lectura cercana y vertical faces sin convertir el spike en un terrain art pipeline. Materiales, texturas, shader y biome realization finales permanecen fuera de alcance.

## Fuera De Alcance Que Permanece Futuro

No quedan implementados por este cierre:

- minería como loop completo;
- inventario de bloques/tierra;
- crafting asociado a excavación;
- geología completa;
- minerales/ore generation de producción;
- fluid simulation o agua entrando en túneles;
- derrumbes/structural soil simulation;
- destrucción completa de edificios;
- soporte/estabilidad de cimientos de producción;
- whole-world voxel allocation;
- streaming sectorial final;
- LOD productivo definitivo;
- caves mundiales masivas;
- fauna/vegetation final;
- terrain materials/art pipeline final;
- dynamic NavMesh de producción;
- optimización preventiva con Jobs/Burst/GPU.

## Estado De Cierre Y Siguiente Paso

`Deformable Volumetric Terrain Foundation / Technical Spike` queda `VALIDATED — TECHNICAL SPIKE COMPLETE`.

La foundation demuestra suficientemente que el camino volumétrico es viable para continuar el proyecto sin volver a asumir una heightmap productiva definitiva. Las decisiones de producción sobre resolución, chunk size, scheduling, LOD, compaction y navegación dinámica se tomarán cuando un consumidor real las necesite.

La prioridad inmediata pasa deliberadamente a la secuencia jugable documentada en [NPC_Sandbox_and_Equipment_Sequence.md](NPC_Sandbox_and_Equipment_Sequence.md), empezando por `M41.2 — Basic Equipment & Weapon Coverage V1`.
