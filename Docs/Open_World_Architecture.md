# Old Scars — Open World Architecture

- Estado: `APPROVED DESIGN DIRECTION — NOT IMPLEMENTED`
- Alcance: arquitectura futura del mundo abierto, generación, sectores, materialización y persistencia mundial
- Implementación actual: las foundations y seams señaladas explícitamente están implementadas; geography, sector detail/materialization y world gameplay continúan futuros

## Autoridad Del Documento

Este documento define la dirección arquitectónica aprobada para el futuro mundo abierto de Old Scars. Distingue decisiones congeladas, límites de integración y decisiones todavía pendientes sin presentar diseños futuros como contratos implementados.

No reemplaza:

- [Project_Roadmap.md](Project_Roadmap.md) para IDs, estados, dependencias, secuencia y gates;
- [Technical_Architecture.md](Technical_Architecture.md) para contratos técnicos ya implementados;
- [DataDriven_JSON_Rules.md](DataDriven_JSON_Rules.md) para schemas y contratos JSON ya implementados;
- [Game_Design_Document.md](Game_Design_Document.md) para la línea base de diseño de producto;
- la autoridad creativa y de producto de Mauro.

Los nombres conceptuales utilizados aquí no congelan nombres públicos de clases, archivos, schemas ni APIs. Una futura implementación debe volver a inspeccionar consumidores, código y datos antes de materializarlos.

## Estado Y Límites

La dirección de diseño está aprobada. No están implementados:

- world generation más allá de Macro World Plan + Macro Elevation/Landforms + Gameplay Quality/Macro Water V1;
- world sectors como regiones jugables/materializadas;
- sector loading o transición;
- climate/moisture, rivers, geology, biomes u otros cross-sector features posteriores a Macro Water V1;
- world persistence de geography posterior, sector blueprints, gameplay mutations y estado activo/inactivo más allá de la shell mínima;
- generation compatibility, generation manifests o world-specific content contracts;
- world history;
- world-scale identity catalogs;
- internal streaming de terreno, NavMesh, física, vegetación o IA.

No se autoriza implementación por la existencia de este documento. Cada unidad requiere alcance, dependencia, validación y autorización propios bajo el Roadmap.

Las foundations mínimas de content source identity/provenance y world identity/topology/determinism sí están implementadas. También existen una application shell acotada, `MacroWorldPlan V1`, `Macro Elevation / Landforms V1` y `Gameplay Quality + Macro Water V1`: `WorldSession`, mundo finito con bounds/placements/topology, campos mundiales fixed-point de elevation/landforms/Water, quality analysis derivada, starter suitable, payload hermano `world_session_v1` schema `4`, Main Menu con tamaño y Land Coverage seleccionables, Load Game, World Runtime placeholder y Save/Return. No existen todavía climate/moisture, rivers, geology/biomes, sectores jugables/materializados, gameplay world state ni compatibility policy.

## Decisión Arquitectónica Central

La dirección congelada es:

`un mundo lógico persistente → sectores grandes interconectados → planificación macro mundial → blueprint/realización local sectorial → materialización Unity → mutación/estado persistente`

Consecuencias:

1. El mundo lógico es autoridad sobre geografía, topología, features continuos, contexto histórico y estado presente inicial.
2. Un sector consume una porción coherente del plan mundial; no inventa aisladamente sus bordes importantes.
3. Un blueprint sectorial es dato lógico validable anterior a GameObjects.
4. La materialización Unity es una representación runtime reemplazable, no la autoridad durable del mundo.
5. Después de comprometer una baseline local o una mutación de gameplay, persistencia pasa a ser autoridad; el seed no puede sobrescribirla.

## Mundo Lógico Persistente

Old Scars posee un único mundo lógico conectado. El mundo conserva conceptualmente:

- identidad durable propia;
- contrato/contexto de generación;
- topología prevista;
- macrogeografía;
- features cross-sector;
- infraestructura y sitios principales;
- historia causal acotada;
- estado presente del plan mundial;
- blueprints locales ya resueltos;
- mutaciones durables de gameplay;
- sector activo coherente en cada checkpoint persistente.

El seed participa en la creación inicial, pero no identifica ni persiste por sí solo el mundo.

## World Sector

Un world sector es una región grande, visible y significativa para el jugador. Puede contener:

- ciudades y pueblos;
- countryside y largos espacios de viaje;
- carreteras y ferrocarriles;
- ríos, streams, costa, mar y vías navegables;
- bosques y espacios naturales;
- puertos, industria y sitios militares;
- edificios, interiores y sitios autorados;
- futuro tránsito terrestre, acuático, ferroviario o vehicular.

Los sectores:

- pertenecen a una única geografía lógica continua;
- pueden diferir en tamaño y extensión;
- no requieren hexágonos, cuadrados ni una grilla rígida;
- son unidades de gameplay, materialización y transición;
- no congelan todavía una geometría de borde o representación poligonal concreta.

La geometría exacta de límites permanece abierta hasta que terrain, tooling y transition consumers demuestren qué contrato mínimo necesitan.

## Internal Technical Partition

Una internal technical partition, cell, chunk o tile es una división de implementación dentro de un sector para uno o más de estos fines:

- terrain;
- rendering y culling;
- vegetación;
- LOD;
- NavMesh;
- physics;
- activación de IA o interiores;
- persistence batching;
- profiling y budgets.

No es un world sector y no adquiere automáticamente:

- significado visible para el jugador;
- identidad mundial de producto;
- fronteras de viaje;
- topología regional;
- save independiente.

La arquitectura mundial sólo exige que las particiones internas respeten el frame local, el lifecycle del sector, la continuidad y la autoridad del estado. Su tamaño y tecnología se decidirán mediante medición.

## Active E Inactive Sector Model

### Sector Activo

Inicialmente existe un solo sector con autoridad sobre la simulación pesada normal. Puede contener:

- GameObjects;
- physics;
- NavMesh y NavMeshAgents;
- visual perception;
- encounter AI;
- combat;
- animación;
- interacción y gameplay normal;
- máquinas/vehículos cuando esos sistemas existan.

Las autoridades existentes de gameplay conservan su responsabilidad dentro del sector activo. La capa mundial entrega contexto, estado lógico, lifecycle y representación; no absorbe navegación, percepción, combate, medicina, inventario ni IA de encuentro.

### Sector Inactivo

Un sector inactivo puede conservar:

- macro plan;
- blueprint resuelto cuando ya fue materializado;
- estado durable;
- actores e items durables sin representación activa;
- contexto e historia mundial.

Inicialmente no ejecuta:

- GameObjects normales;
- physics;
- NavMeshAgents;
- visual perception;
- combat;
- encounter AI;
- animación/IK costosa;
- simulación estratégica general.

No se crea una segunda IA, navegación o combate para sectores inactivos.

### Staging Durante Transición

Un solo sector activo no significa que Unity jamás pueda contener datos o scene state temporal de otro sector. Durante una transición, el destino puede prepararse inertemente mientras el origen conserva autoridad. El staging no puede ejecutar una segunda simulación autoritativa ni comprometer estado antes de superar validación.

## Topología Y Continuidad Cross-Sector

La topología futura debe admitir:

- sectores de extensión variable;
- adyacencia explícita;
- más de una conexión entre un par de sectores;
- múltiples salidas sin imponer un único teleport point;
- endpoints o crossing regions emparejados;
- mapeo coherente de posición y orientación;
- futuro tránsito a pie, terrestre, acuático o ferroviario.

La foundation implementada materializa el mínimo lógico como `SectorId`, `SectorConnection` y `WorldTopology`: nodos explícitos, connection keys únicas, endpoints existentes, más de una conexión por par, rechazo de self-loop y un único componente conectado. Las conexiones mínimas son no dirigidas y normalizan endpoints; dirección física/flujo pertenece a features futuros. Su descripción/hash SHA-256 es independiente del insertion order y no contiene geometry.

Esto no implementa regiones jugables, crossing regions, features, shape, extensión, coordenadas, transición ni materialización. Esos contratos continúan conceptuales y se agregarán sólo con consumidores reales.

### Features Mundiales Primero

Los features continuos importantes son entidades lógicas mundiales antes de convertirse en segmentos locales. Incluyen, cuando corresponda:

- ríos y streams;
- carreteras;
- ferrocarriles;
- coastlines;
- vías navegables;
- infraestructura mayor.

Cada sector consume la porción asignada por el plan mundial. Dos sectores no generan de forma independiente una continuación y luego esperan coincidir.

La autoridad común debe conservar únicamente lo requerido por consumidores reales, como:

- identidad del feature;
- sectores/segmentos participantes;
- trayectoria o contexto macro;
- crossing regions/anchors coherentes;
- invariantes de continuidad.

Ancho, nivel de agua, elevación, tangente, gauge u otros parámetros se agregan solamente cuando terrain, navegación o tránsito los consuman.

### Crossing Region Y Frames

Una transición no se reduce necesariamente a un punto. El contrato futuro debe poder representar una región o segmento de cruce. Una embarcación que cruza un río debe reaparecer en la parte correspondiente del mismo río, no en un anchor genérico desconectado.

Suposición inicial:

- preservar world heading;
- preferir frames sectoriales alineados;
- mapear pose dentro de la crossing region correspondiente;
- no introducir escala, reflection ni rotaciones arbitrarias sin una necesidad futura probada.

## Coordinate Model

La separación conceptual congelada es:

`logical world position = sector identity + sector-local pose`

`Unity runtime position = coordenadas Unity locales dentro del sector activo`

No se requiere un espacio Unity gigante para todo el mundo. No se congelan todavía tipos numéricos, precisión, nombres públicos ni formato de serialización.

Existing player movement, physics, raycasts, combat, perception y `ActorNavigationController` continúan usando coordenadas Unity del sector activo. Una futura frontera de mapping convierte entre la pose lógica sectorial y la representación runtime sin transformar esos sistemas en autoridades mundiales.

El payload `current_slice_v1` conserva su contrato de pose existente y no se convierte retroactivamente al modelo sectorial.

## World Generation Pipeline

El orden arquitectónico aprobado es:

1. generation context;
2. world topology;
3. macro geography;
4. cross-sector natural networks;
5. human geography, sites e infrastructure;
6. bounded causal history;
7. present-day world plan;
8. sector blueprint;
9. blueprint validation;
10. Unity materialization;
11. persistent gameplay mutation.

Se congelan el orden de autoridad y la separación logical/runtime, no nombres de clases ni un número fijo de pases.

La implementación actual cubre generation context, settings resueltos, bounds finitos y placements/topology dentro de `MacroWorldPlan V1`; un campo mundial committed de elevation normalizada y landforms (`Plains`, `RollingHills`, `Highlands`, `Mountains`); Macro Water committed con sea/ocean/coastline/conditioned drainage/basin candidates; y quality analysis para rechazar casos claramente patológicos y seleccionar el starter. Esa verdad se consulta por coordenadas macro y precede todo detalle sectorial. No simula ni infiere climate/moisture, rivers, geology, vegetation/biomes, roads, sites o history.

Principio preferido:

`generate → validate → materialize`

No:

`instantiate random GameObjects → infer authoritative world afterward`

## Macro Plan Y Lazy Sector Detail

### Truth Resuelta En New Game

New Game debe generar y persistir suficiente verdad global para que el mundo sea coherente. En V1 ya quedan resueltos y persistidos identity, generation context/settings, bounds, placements, topology, elevation/landforms y Macro Water; los demás puntos continúan en sus passes futuros:

- world identity;
- generation contract/context;
- intended sector topology;
- macro geography;
- cross-sector features;
- major infrastructure;
- major settlements/sites;
- bounded historical consequences;
- present-day macro world state.

Esto no obliga a resolver completamente cada sector antes de jugar.

### Sector Local No Resuelto

Un sector no visitado puede conservar su verdad macro sin blueprint local completo. En su primera materialización debe:

1. resolver el detalle local usando un contexto compatible con el contrato del mundo;
2. validar el blueprint;
3. crear o asignar identidades durables cuando corresponda;
4. comprometer la baseline local y el estado resultante;
5. materializar desde esa baseline.

Después del commit, la baseline persistida manda. Un sector previamente materializado nunca se regenera silenciosamente.

## Generator And Content Version — P0

Un sector puede visitarse mucho después de crear el mundo. Para entonces pueden haber cambiado el juego, el generador, la configuración, los mods, datos o assets de estructuras autoradas.

Invariante P0:

> Un sector todavía no resuelto no puede usar silenciosamente cualquier generador o contenido presente cuando se visita por primera vez.

Debe ocurrir una de estas categorías de solución revisada:

- comportamiento de generación compatible conservado;
- inputs suficientes persistidos;
- blueprint o datos locales persistidos anticipadamente;
- migración explícita;
- rechazo seguro con diagnóstico accionable;
- otra estrategia posteriormente aprobada.

Este documento no elige todavía la estrategia productiva. Está prohibido reinterpretar silenciosamente mundos existentes.

## Determinism

Principio congelado:

`same seed + same pass generation contracts + same generation-relevant settings + compatible generation content = same initial logical result`

Requisitos:

- ningún estado global de `UnityEngine.Random` puede ser autoridad;
- RNG domains/passes deterministas e independientes;
- canonical iteration order;
- realización de sectores independiente del orden de visita/materialización;
- golden seeds;
- canonical logical hashes;
- cambios decorativos no desplazan infraestructura mayor;
- outputs lógicos no dependen de frame timing, GameObject enumeration ni orden incidental de archivos.

La foundation implementada agrega `WorldGenerationContext` con `WorldSeed` signed 64-bit y `GeneratorVersion`, más `WorldDeterminism.DerivePassDomainKey`. La versión global del pipeline es metadata de creación/compatibilidad futura; cada pass procedural posee un contrato estable propio. La derivación SHA-256 canónica usa seed/contrato-del-pass/scope/pass y excluye la versión global, `WorldId`, provenance, global random, runtime hash y orden de ejecución. `MacroWorldPlanGenerator` posee `macro_plan_v1`; `MacroGeographyGenerator`, `macro_geography_v1`; Water conserva `macro_water_v1` y es determinista por construcción sobre geography + settings del pass. Downstream evolution no puede perturbar upstream generation sin una dependencia explícita. Geography continúa derivando una vez domains separados de regiones, upheaval/base, detail, ridges y roughness y luego usa sampling fixed-point/mixer estable en el inner loop, sin `System.Random`, Unity Perlin ni SHA por celda.

Regla permanente: **overall pipeline version is compatibility/creation metadata; each procedural pass owns a stable generation contract. Downstream evolution must not perturb unrelated upstream generation.** Un pass downstream sí puede cambiar cuando cambia una entrada upstream real que consume; por ejemplo, Water puede cambiar si cambia MacroGeography.

Los golden hashes cubren domain derivation, topology, Macro World Plan, Macro Geography y una única evidencia canónica de Macro Water. Quality analysis no agrega otro fingerprint. Continúa sin implementarse generation compatibility ni una selección de generation-relevant content inputs; `LoadedContentSet` no se incorpora automáticamente a randomness.

El futuro presupuesto de CPU/workers es un setting de ejecución/rendimiento, no un generation-relevant world setting. No participa en SectorId, placements, topology, hashes, geography ni otros resultados. Cuando existan passes realmente paralelizables, `1 worker` y `N workers` con seed/settings idénticos deberán producir exactamente el mismo mundo lógico. No existe todavía selector ni soporte multithread sin consumidor.

## Bounded Causal History

La historia generada sigue este concepto:

`structured historical event → valida sujetos/lugares reales → cambia o explica world-plan state → queda registrado → puede producir presentación localizada/revelada`

Posibles hechos:

- settlement founded;
- infraestructura construida;
- railway reached a town;
- conflicto o batalla;
- bridge destroyed;
- site abandoned;
- nature reclamation.

Un evento no es flavor text. Debe referir entidades o lugares reales del plan y dejar una consecuencia comprobable o explicar un estado presente real.

### No Event-Sourced Persistence

World history no es event-sourced world persistence. El `World Plan` y el estado presente son autoridades. Cargar un mundo no requiere reproducir toda su historia para reconstruirlo.

History conserva causalidad, inspección y soporte de presentación. Las mutaciones del jugador pertenecen al estado persistente correspondiente, no a una reutilización indiscriminada del history model.

### True Vs Revealed History

Se distinguen:

- **true world history:** hechos estructurados que realmente forman parte del mundo;
- **revealed/presented history:** subconjunto que el jugador conoce o que la UI muestra.

La pantalla de generación puede revelar hechos seleccionados sin exponer todos los secretos, sitios o causas del mundo. La localización transforma datos estructurados en texto; el texto no se convierte en autoridad.

No se implementa una simulación completa de civilizaciones estilo Dwarf Fortress.

## Identity Domains

Se preservan sin reinterpretación:

- `ContentId`;
- `ItemInstanceId`;
- `ActorInstanceId`;
- `PersistentSceneObjectId`.

Ya están implementados como dominios separados:

- `WorldId` durable `world_<32 hex lowercase>`, independiente del seed;
- `SectorId` determinista `sector_<32 hex lowercase>`, independiente de scenes/Transforms/array index.

El futuro mundo todavía necesita dominios conceptuales separados para:

- cross-sector feature identity;
- generated placement identity/key;
- durable generated world-object identity cuando exista estado mutable;
- structured historical-event identity.

No se congelan todavía nombres públicos ni formatos exactos para los dominios futuros restantes.

### Placement Vs Durable Identity

Generation placement explica la colocación inicial. Durable identity acompaña a la entidad después de mutaciones, traslado, daño, apertura, loot, reparación o destrucción.

Una estructura autorada repetida necesitará world-placement context más sus authored local identities. `PersistentSceneObjectId` continúa identificando roots autorados dentro de su contexto; no se vuelve un ID procedural universal.

## World-Wide Durable Uniqueness

`ActorRuntimeRegistry` y los registries de IDs de items actuales siguen siendo autoridades sobre identidades/representaciones activas de la sesión. Los sectores inactivos contendrán eventualmente `ActorInstanceId` y `ItemInstanceId` durables sin GameObjects ni registro runtime activo.

Integración obligatoria:

> La persistencia mundial debe garantizar unicidad durable a través del estado activo e inactivo completo.

No se crea una segunda autoridad de gameplay para actores o items. El mecanismo exacto permanece abierto y deberá validarse. Puede ser un preflight global, índice/catalogo persistente u otra solución revisada.

## World Persistence

La persistencia mundial reutiliza las garantías de M37:

- envelope versionado;
- migration seam;
- safe writes;
- backup/recovery;
- semantic preflight;
- transactional apply;
- rollback;
- canonical comparison;
- fresh-session validation.

`current_slice_v1` queda sin cambios como regression path. No representa un sector y no se migra silenciosamente a uno.

La implementación actual usa el snapshot type hermano `world_session_v1`. Los mundos Gameplay Quality + Macro Water V1 se escriben con schema `4`, que conserva WorldId/display name, seed/generator version, MacroWorldPlan, MacroGeography, settings/sea/masks/bodies/coastline/conditioned surface/drainage/basins/hash de Water, active sector y provenance de creación. Usa el envelope, serializer, safe-write/recovery y slot rules de M37; `WorldId.Canonical` es el slot y el display name no es identidad de filesystem. Read reconstruye plan, geografía y Water por validators reales desde truth committed —no por seed-only regeneration—, deriva quality de esa truth y sólo publica la session después del semantic preflight.

Schemas `1`, `2` y `3` conservan compatibilidad legacy explícita: schema `1` carga su topología sin inventar tamaño/plan/geografía/Water; schema `2` carga el MacroWorldPlan sin fabricar elevation/landforms/Water; schema `3` carga MacroWorldPlan + MacroGeography sin fabricar Water ni quality truth. Todos permanecen en su schema al guardarse y no se reinterpretan silenciosamente.

Este V1 es application/session persistence con truth macro geográfica/hídrica acotada, no la persistencia mundial completa de la futura unidad conceptual: todavía no contiene climate/moisture, rivers, geology, history, sector blueprints, gameplay mutations, identidades durables mundiales ni partitions. No copia `CurrentSliceLoadService`, no crea serializer/file store paralelo y no aplica `current_slice_v1` como si fuera un sector.

### Logical Persistence Vs Physical Storage

El modelo lógico mundial y el layout físico en disco son decisiones distintas.

- Un First Playable puede usar un documento mundial si resulta práctico.
- La arquitectura no congela un JSON monolítico para producción.
- Un futuro index/sector partitioning puede introducirse si mediciones de tamaño, latencia, recovery y transacción lo justifican.
- No se diseñan todavía transacciones multi-file.

## Runtime Sector Transition

La transición runtime y la política de autosave son contratos separados.

Lifecycle conceptual:

1. validar conexión, destino, generation contract y estado;
2. bloquear nueva interacción de transición;
3. preparar el destino inertemente cuando corresponda;
4. capturar rollback state runtime;
5. liberar/materializar representaciones de manera transaccional;
6. colocar al traveler en la crossing region correspondiente;
7. validar el resultado materializado;
8. promover el destino como único sector autoritativo;
9. recuperar el origen si el proceso falla.

El traveler inicial es player más su estado durable y grafo de items/Equipment/ownership. El boundary no debe impedir un futuro traveler compuesto por vehículo, barco, tren, pasajeros o carga, pero esos sistemas no se diseñan ni implementan ahora.

### Persistence Checkpoint Policy

No se exige una escritura en disco antes o después de cada frontera. Autosave, manual save y transition checkpoints son decisiones posteriores de producto y rendimiento.

En todo checkpoint persistente comprometido debe existir exactamente un mundo coherente y un active-sector state autoritativo. Nunca se guarda un híbrido parcialmente transicionado.

## WorldClock Y Off-Sector Time

`WorldClock` permanece como única autoridad de tiempo de gameplay.

Los sectores inactivos no ejecutan simulación normal, pero no quedan permanentemente exentos de elapsed-time semantics. Se conserva un futuro seam de time reconciliation:

- si un subsystem durable existente define semántica de tiempo transcurrido;
- y su estado vuelve a ser relevante después de inactividad;
- se reconcilia determinísticamente mediante la autoridad y reglas de ese subsystem.

No se introduce una interfaz universal preventiva, global offscreen AI ni strategic simulation. Cada reconciliación requiere un consumidor real y respeta al dueño del estado.

## Modding, Provenance Y Generation Compatibility

Los mundos persistentes generados requieren procedencia estable de sus fuentes de contenido.

### Provenance

Responde qué fuentes, versiones e inputs estuvieron presentes. La foundation mínima implementada extiende `ContentLoadContext` y `GameDataLoader`, conserva `GameDatabase`/`TagRegistry`/`DataValidator` como autoridades y publica un `LoadedContentSet` validado con fingerprints SHA-256 de provenance.

El contrato actual cubre manifests de source identity (`source_id`, namespace y version) y los JSON ya reconocidos por el loader. `world_session_v1` persiste un snapshot de la identidad/version/fingerprint por source y el fingerprint agregado presentes al crear el mundo. Esa evidencia no incluye authored assets/templates futuros, no se incorpora automáticamente a la aleatoriedad y no decide compatibilidad.

### Generation Compatibility

Responde si los inputs semánticos necesarios siguen siendo compatibles con el contrato de generación de un mundo. No equivale automáticamente a igualdad de bytes.

La arquitectura no congela:

- un manifest schema final;
- un raw-byte fingerprint universal;
- JSON como único input posible;
- una política de migration/rejection final.

Las estructuras y assets autorados pueden convertirse en inputs relevantes. Una implementación futura debe identificar solamente los inputs realmente consumidos por generación y registrar evidencia suficiente sin adelantar el alcance completo de M50.0.

[DataDriven_JSON_Rules.md](DataDriven_JSON_Rules.md) documenta el contrato mínimo ya implementado. Cualquier ampliación exige consumidor y validación reales.

## Authored Content And Procedural Composition

Los sitios y estructuras importantes permanecen principalmente autorados. Generation decide, según el consumidor autorizado:

- selección;
- placement y orientación;
- relación con geografía y redes;
- contexto circundante;
- condición histórica/presente;
- composición.

No se genera cada pared, habitación o pieza por defecto.

Cuando una estructura se materializa, continúan siendo autoridades los sistemas existentes de:

- doors e interactions;
- containers, tags y loot;
- Inventory, Equipment y ownership;
- items y actors;
- combat y medical state;
- navigation y perception;
- encounter AI.

Worldgen decide contexto y baseline; no reimplementa gameplay.

## Runtime Performance Y Travel Pacing

Regla transversal para datos mundiales costosos:

`precompute → persist → event-driven/low-frequency → continuous simulation only with real gameplay consumer`

Macro fields, Water conditioning y futuros planes globales se calculan al generar, se validan y se persisten. No deben convertirse en una simulación continua world-scale. Una actualización event-driven o de baja frecuencia requiere un consumidor y autoridad concretos; una simulación continua sólo se justifica cuando gameplay real la necesita y su coste está medido.

Old Scars prioriza geografía comprimida creíble sobre distancias 1:1. El world/sector design futuro debe ofrecer identidad regional y barreras significativas sin convertir viaje normal en largos trayectos vacíos. Vehículos, máquinas y barcos podrán cambiar pacing, pero no justifican por anticipado una verdad falsa de transitabilidad o infrastructure.

Whole-world NavMesh queda rechazado. La realización futura del sector activo y sus partitions internas producirán las surfaces/links locales que consume el `ActorNavigationController` existente; worldgen no crea un navigator paralelo. Terrain deformation queda como seam futuro de mutación local durable: esta arquitectura no autoriza ni implica voxels o una simulación global de suelo.

### Future Consumer Matrix

| Consumidor futuro | Truth macro que puede consumir | Sigue pendiente / no inferir ahora |
| --- | --- | --- |
| Player/AI navigation | relief/Water y corridors como hints de blueprint | Walkable/NavMesh/path final; usar partitions activas y `ActorNavigationController` |
| Vehicles/machines | relief, Water y futuros networks para route feasibility | vehicle physics, grades físicas, roads y performance runtime |
| Boats | ocean bodies/coastline y futura hydrology | navigable depth, rivers, ports y boat simulation |
| Roads/rail | corridors, relief y crossings de futuras redes mundiales | trazado, bridges, grade/curvature y travel graph |
| Buildings/construction | site-placement potential y future local blueprint | Buildable final, metros, foundations y terrain mutation |
| POIs | anchor suitability y relación con geography/networks | authored selection/composition y condición histórica |
| Factions/history | bounds, regions y future sites/networks | strategic simulation y event-sourced persistence |
| Underground | world bounds y future geology | caves, strata, entrances y relación con surface terrain |
| Terrain mutation | committed local baseline futura | voxel implementation o edición de macro truth global |

## System Harmony — M32 A M41.1

| Área validada | Autoridad preservada | Integración futura permitida | Prohibición |
| --- | --- | --- | --- |
| M32 interactions/doors/containers | Components, actions, effects y state actuales | Materializar estructuras que los contienen y persistir su estado mundial | Segundo backend de puertas/contenedores o reseed de loot comprometido |
| M33–M35 Inventory/Equipment/ownership/visuals | Storages, transacciones, ownership, Equipment y presentation actuales | Transportar las mismas instancias y reconstruir visuales desde estado confirmado | Inventario mundial paralelo o visuales como autoridad gameplay |
| M36 identities | Definition/Instance y authored identity separados | Agregar dominios mundo/sector/feature/placement | Reutilizar seed o `PersistentSceneObjectId` como ID universal |
| Global Content ID | `ContentId`, loader, database y validator | Extender source context/provenance por el pipeline común Core/mod | Lectura directa de Core o namespace por carpeta como autoridad |
| M37 persistence | Persistence Core y apply/rollback validados | Payload mundial hermano y primitives compartidas con consumidor real | Copiar el load engine o mutar `current_slice_v1` |
| M38 actor lifecycle | `ActorInstanceId`, runtime registry y `ActorSpawnService` | Materializar/desmaterializar representaciones del mismo actor durable | Segundo actor registry/spawner o full actors off-sector |
| M38.1 time | `WorldClock` | Time reconciliation por subsystem cuando exista consumidor | Clock por sector o universal catch-up simulation |
| M39 medicine | Actor-owned medical state | Persistir/reconciliar mediante su propia autoridad | Medical state duplicado en world layer |
| M40/M40.1 combat | Combat, penetration y medical dispatch actuales | Sector entrega geometría y representaciones | World resolver de combate alternativo |
| M41.0 navigation/perception | Navigation y perception separadas | Sector entrega NavMesh/pose local | World navigator que suplante `ActorNavigationController` |
| M41.1 encounter AI | `HumanEncounterAIController` sobre autoridades existentes | Ejecutar sólo en representaciones activas | Strategic AI prematura o encounter authority paralela |

## Large-Sector Navigation And Performance Gate

Antes de una realización sectorial de producción se requiere un spike técnico acotado. Debe medir, sin reemplazar `ActorNavigationController`:

- múltiples NavMesh surfaces/partitions o estrategia equivalente;
- continuidad de paths y links autorizados;
- exteriores e interior mínimo;
- unload/reload o rebuild parcial;
- restauración de pose y retorno estable a `Idle`;
- tiempos de build/load;
- memoria máxima y residual;
- fallos en borders/partitions.

No congela todavía tamaño de tiles, cantidad de surfaces, terrain technology, streaming solution ni budgets numéricos. Esos valores requieren escenario, hardware y mediciones representativas.

## Connected First Playable

El Connected First Playable es la prueba principal de integración, no la vertical slice audiovisual final.

Debe demostrar eventualmente:

`New Game → deterministic world generation → causal history → large Sector A traversal → authored site/building → existing doors/interiors/containers → contextual loot → Inventory/Equipment/ownership → M41 encounter AI → M40 combat → M39 wounds/bandage → M38.1 Hunger/Thirst/WorldClock → storage/rest/sleep → explicit A→B transition → cross-sector continuity → B→A return → prior mutations preserved → save → full process exit → fresh load → same world/history/identities/state`

La prueba no autoriza a agregar sistemas no requeridos por ese recorrido ni equivale a arte, audio, UI o contenido final.

## Conceptual Critical Path

Los IDs, estados y dependencias autorizadas viven exclusivamente en [Project_Roadmap.md](Project_Roadmap.md). La secuencia conceptual aprobada es:

1. Open World Rebaseline;
2. Minimum Content Source Identity / Provenance;
3. World Identity / Topology / Determinism;
4. Macro World Plan;
5. Macro Elevation / Landforms;
6. Gameplay Quality / Macro Water;
7. Macro Geography / Cross-Sector Networks;
8. Bounded History / Present-Day Resolution;
9. World Persistence;
10. Sector Blueprint / Authored Composition;
11. Large-Sector Navigation / Performance Gate;
12. Sector Materialization / Transition;
13. Connected First Playable;
14. Playtest / Rebaseline;
15. sistemas posteriores según dependencias y evidencia reales.

Los pasos 2–6 están `VALIDATED`; también quedó validada una shell operacional intermedia de World Session/New Game/Save/Load sobre M37. Esa shell no marca completo el paso 9: persiste identity, MacroWorldPlan/topology, Macro Elevation/Landforms, Macro Water y provenance evidence, pero mantiene vacíos climate/moisture, rivers/geology, history, local detail y gameplay world state. El próximo candidato es `Macro Climate / Moisture V1`, según alcance y autorización del Roadmap.

Weather/environment, ecology, condition/repair, crafting, progression, deeper shelter, vehicles, machines, settlements, economy, factions, UI y producción no se eliminan. Su orden final posterior no queda permanentemente congelado aquí.

## World Extent — Approved Product Decision

Está aprobado:

- un único mundo lógico gigante e interconectado;
- un mundo finito pero muy grande, sin expansión procedural infinita durante la partida;
- extensión macro completa, bounds físicos lógicos y distribución total de sectores resueltos al crear la partida;
- sectores grandes de extensión variable;
- geografía lógica continua;
- selección durable de tamaño mediante `Small`, `Medium`, `Large` y `Huge`.

El detalle local de sectores puede seguir resolviéndose lazy. Conocer el plan macro completo no congela polígonos, terrain, visualización del borde ni kilómetros exactos.

Los valores resueltos V1 son tuning inicial, pero quedan persistidos junto al preset para que un mundo existente no cambie si los defaults futuros se ajustan.

`FINITE, BOUNDED, VERY LARGE WORLD — APPROVED`

## Explicitly Deferred

- geometría exacta de sectores/fronteras;
- tamaños de sectores o cells;
- tipos numéricos finales de coordenadas;
- PRNG concreto;
- manifest/schema final;
- fingerprint universal;
- política final de generation compatibility;
- storage físico monolítico o particionado;
- multi-file transactions;
- autosave/checkpoint frequency;
- off-sector strategic simulation;
- vehicles, boats, trains, passengers y cargo;
- world-scale faction simulation;
- final terrain, vegetation, NavMesh y scene strategy;
- audiovisual vertical slice.
