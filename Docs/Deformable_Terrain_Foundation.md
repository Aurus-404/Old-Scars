# Old Scars — Deformable Volumetric Terrain Foundation

- Estado de dirección: `APPROVED — IMMEDIATE PRIORITY`
- Estado de implementación: `NOT IMPLEMENTED YET`
- Tipo: arquitectura/world runtime/terrain foundation
- Autoridad relacionada: [Project_Roadmap.md](Project_Roadmap.md), [Open_World_Architecture.md](Open_World_Architecture.md)

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

## Consecuencia Sobre El Spike Actual

`Terrain Materialization Technical Spike` permanece `VALIDATED — TECHNICAL SPIKE COMPLETE` y no se considera trabajo perdido.

El spike actual con Unity `Terrain/TerrainCollider` sigue siendo evidencia válida para:

- proyección Macro Geography → espacio Unity;
- escala local provisional;
- coastline/ocean context;
- roads diagnósticas;
- player bootstrap;
- WorldRuntime integration;
- NavMesh y timings de referencia.

Pero Unity Terrain deja de ser candidato implícito a representación productiva definitiva. Es un benchmark/prototipo de materialización heightmap, no una autoridad de producto que futuras features deban asumir.

No construir nuevas dependencias productivas que requieran que el terreno final sea una única heightmap.

## Dirección Técnica Preferida

La primera dirección a validar es:

`macro world truth → sector/local terrain baseline → chunked volumetric density/material field → smooth mesh/collider → localized deformation → persistent terrain mutations`

La implementación concreta puede evolucionar si la evidencia técnica lo exige, pero debe conservar estas capacidades:

1. representación volumétrica real;
2. partición técnica local/chunked;
3. mesh visible continuo o suficientemente suave para la dirección artística;
4. colliders actualizables localmente;
5. modificación localizada sin reconstruir el mundo entero;
6. persistencia de las modificaciones;
7. compatibilidad con la futura arquitectura de sector activo/inactivo;
8. consumo de Macro Geography/Water/Climate/Environment sin reemplazar esas autoridades.

Una implementación smooth-voxel/density-field con meshing por chunks es la hipótesis inicial preferida, no un mandato de usar un algoritmo de meshing específico antes de medirlo.

## Modelo De Escala

Old Scars no mantendrá el mundo completo como una matriz voxel de máxima resolución en memoria.

La separación objetivo es:

`mundo macro barato → sector/ventana activa → chunks volumétricos cercanos materializados`

Sólo la representación necesaria alrededor del gameplay activo debe pagar mesh, collider y otros costes pesados. Sectores inactivos conservan truth/baseline/mutations sin simulación física pesada.

`World Sector` y `Voxel/Technical Chunk` son conceptos distintos. Un chunk es una partición de implementación dentro de la representación local; no adquiere significado de mundo, viaje o save slot independiente.

## Mutación Y Persistencia

Principio deseado:

`procedural/committed baseline + persistent deformation state = current terrain`

No se debe asumir que cada save almacene todo el volumen mundial sin compresión.

La primera foundation debe investigar y validar una estrategia proporcional como:

- operaciones/deltas localizados mientras son pocos;
- dirty chunk data compactada cuando la cantidad de cambios lo justifique;
- canonicalización suficiente para save/load reproducible;
- reconstrucción de una zona modificada sin depender del orden temporal de visita.

La forma final de compaction/versioning no queda congelada antes de medir.

## Deformation API — Dirección

La foundation debe tender a una frontera explícita de terrain mutation en lugar de permitir que cualquier gameplay edite buffers internos.

Ejemplos conceptuales, no nombres de API congelados:

- subtract sphere/volume;
- add/fill sphere/volume;
- material-aware excavation;
- query de material/solidez en un volumen;
- dirty-region notification para mesh/collider/navigation/persistence.

Pala, explosivos, maquinaria y herramientas futuras deben consumir esa autoridad común; no crear su propio sistema de deformación.

## Materiales Y Apariencia Cercana

El primer spike volumétrico también debe corregir el problema actual de lectura cercana: el Terrain técnico se percibe demasiado plástico/reflejante y tosco a corta distancia aunque funcione razonablemente de lejos.

No se exige arte final. Para validar rendering y escala de textura, Codex está autorizado a crear assets de prueba simples y task-owned, incluyendo texturas PNG procedurales o generadas por script, y utilizarlas dentro del prototipo.

Las texturas de prueba deben ser claramente placeholders y fáciles de reemplazar. Pueden representar como mínimo superficies simples tipo:

- topsoil/grass-dirt;
- soil/earth;
- rock.

Objetivos del baseline visual técnico:

- superficie mayormente mate, sin brillo plástico exagerado;
- tiling/texel density legible de cerca;
- variación visual suficiente para evaluar geometría;
- transición visible entre superficie y material excavado si el prototipo usa más de un material;
- no convertir este spike en un pipeline artístico final.

No hace falta usar IA generativa para estos placeholders: texturas programáticas simples son válidas y preferibles si son suficientes para la prueba. Si una herramienta de generación de imágenes disponible en el entorno puede producir un placeholder task-owned de forma reproducible y legal, también puede usarse; el criterio es utilidad técnica, no acabado de producción.

## Primer Coding Unit — Alcance Autorizado

`ID TBD — Deformable Volumetric Terrain Foundation / Technical Spike`

Estado: `AUTHORIZED — IMMEDIATE PRIORITY`.

Debe trabajar directamente en el checkout canónico de Old Scars bajo el workflow vigente sin worktrees.

El objetivo de este primer unit es demostrar, con evidencia, que una representación volumétrica chunked puede reemplazar razonablemente al supuesto heightmap productivo antes de que más sistemas dependan de Unity Terrain.

MUST:

- inspeccionar y preservar las autoridades Macro Geography/Water/Climate/Environment, WorldSession y WorldRuntime existentes;
- producir una ventana/área volumétrica local desde la truth de mundo existente;
- generar mesh y collision funcionales;
- demostrar al menos una deformación subtract localizada en runtime que produzca un cráter/cavidad real;
- demostrar una forma que una heightmap no pueda representar correctamente, por ejemplo excavación lateral, overhang, túnel corto o cavidad con techo;
- limitar el rebuild a chunks/regiones afectadas;
- medir coste de generación inicial, mesh rebuild, collider update y memoria aproximada;
- demostrar save/load o una prueba persistente equivalente de al menos una deformación task-owned sin regenerarla silenciosamente;
- dejar una estrategia explícita para dirty chunks/mutation state;
- conservar el gameplay/runtime existente sin crear una segunda autoridad mundial;
- incluir un baseline visual cercano suficientemente mate/legible para evaluar el terreno;
- validar que el player puede caminar sobre la superficie y dentro/alrededor de la deformación producida.

SHOULD:

- comparar más de una resolución/chunk size razonable;
- usar materiales simples surface/soil/rock si el coste es proporcional;
- medir el impacto sobre navegación local y documentar la estrategia recomendada de actualización, sin obligarse todavía a resolver toda la navegación dinámica de producción;
- exponer controles de debug development-only para deformar/restaurar el área de prueba.

MAY:

- generar proceduralmente texturas placeholder simples y guardarlas como assets task-owned;
- mantener el Terrain spike viejo detrás de tooling/diagnostics como referencia comparativa mientras no interfiera con la nueva prueba.

## Fuera De Alcance Del Primer Spike

No implementar todavía:

- minería como loop completo;
- inventario de bloques/tierra;
- crafting asociado a excavación;
- geología completa;
- minerales/ore generation de producción;
- fluid simulation;
- agua entrando físicamente en túneles;
- derrumbes/structural soil simulation;
- destrucción completa de edificios;
- soporte/estabilidad de cimientos de producción;
- whole-world voxel allocation;
- streaming sectorial final;
- LOD productivo definitivo;
- caves mundiales masivas;
- fauna/vegetation final;
- terrain materials/art pipeline final;
- dynamic NavMesh de producción para cualquier escala;
- optimización preventiva con Jobs/Burst/GPU antes de medir la versión simple.

## Criterios De Éxito

El spike es exitoso si demuestra que Old Scars puede tener terreno volumétrico alterable con un coste y una arquitectura compatibles con la dirección de sectores, y deja evidencia suficiente para elegir la representación productiva siguiente.

No es necesario que el primer prototipo sea bonito ni escalable al mundo completo. Sí debe evitar una prueba falsa basada únicamente en deformar una heightmap.

El resultado debe permitir decidir con datos:

- resolución voxel/density razonable;
- tamaño de chunk razonable;
- algoritmo de meshing inicial;
- coste de collider rebuild;
- estrategia de persistencia;
- estrategia de navegación local tras deformación;
- límites que deban considerarse antes de Sector Materialization productiva.

## Prioridad De Roadmap

Esta foundation se hace ahora, antes de continuar profundizando el worldgen macro o construir materialización productiva basada en una representación que después haya que reemplazar.

Tras cerrar esta prueba, la prioridad vuelve a pulir mecánicas/core gameplay durante un tramo corto antes de retomar las capas grandes de mundo, salvo que el spike descubra un blocker técnico que exija una corrección inmediata.
