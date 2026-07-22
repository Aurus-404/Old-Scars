# Old Scars - Documento de Diseño del Juego

- Versión: línea base de repositorio 1.0
- Estado: `APPROVED — REVISED DESIGN BASELINE`
- Derivado de: `Old_Scars_GDD_Maestro_v3.1.docx` (17 de julio de 2026)
- SHA-256 de la fuente: `919966D0BFCDE1FD77C6D7765EE087B4D04211FBDEAAD06B4AAFCCFEE7308AF4`

## Propósito Y Autoridad

Este archivo es la línea base de diseño revisada, resumida y mantenible en Git para Old Scars. Fue derivado del GDD Maestro v3.1 y reconciliado críticamente contra él, las decisiones recientes, los milestones validados, los documentos vivos del repositorio, el historial de Git y el estado técnico implementado.

El GDD Maestro v3.1 sigue siendo una fuente histórica y de diseño importante. No es una especificación incuestionable, no fue sobrescrito y no prevalece automáticamente sobre decisiones posteriores o evidencia actual. Mauro aprobó esta línea base revisada durante `Documentation Review Closeout`; conserva la autoridad creativa y de producto final, y las decisiones etiquetadas como pendientes permanecen abiertas.

La verdad se resuelve en este orden:

1. decisión explícita y reciente de Mauro;
2. decisiones aprobadas y validadas en milestones recientes;
3. documentos vivos del repositorio dentro de su dominio;
4. evidencia del repositorio y de Git para el estado técnico;
5. GDD Maestro v3.1 como fuente de diseño previa y auditable;
6. propuestas editoriales etiquetadas explícitamente;
7. inferencias e ideas abiertas.

Este orden depende del dominio: un documento desactualizado no puede prevalecer sobre código, datos, assets o evidencia de commits validados al determinar qué está implementado técnicamente. En su lugar, debe corregirse el documento técnico vivo correspondiente. A la inversa, el código no decide el canon ni el diseño final de cara al jugador.

Autoridades por dominio:

- Mauro decide la dirección creativa, el canon y el alcance de producto;
- [Project_Roadmap.md](Project_Roadmap.md) define IDs, estados, dependencias, secuencia y gates de milestones;
- [Technical_Architecture.md](Technical_Architecture.md) define los contratos técnicos actuales después de contrastarlos con el código;
- este documento contiene la línea base de diseño revisada y sus decisiones abiertas;
- [DataDriven_JSON_Rules.md](DataDriven_JSON_Rules.md) define el contrato actual de JSON/datos;
- [Production_Gates_and_Risks.md](Production_Gates_and_Risks.md) desarrolla la evidencia de gates y el registro de riesgos;
- el repositorio y los commits prueban qué existe técnicamente, no qué debe convertirse en el diseño final.

Una implementación o prototipo no se convierte en diseño final por el mero hecho de existir. Una declaración de v3.1 no se convierte en canon por el mero hecho de estar escrita. Las propuestas nunca equivalen a canon sin una decisión explícita.

## Etiquetas De Lectura

| Etiqueta | Significado |
| --- | --- |
| `CONFIRMED — RECENT DECISION` | Dirección explícita en el brief autorizado de M36.0 o en una decisión posterior de Mauro. |
| `CONFIRMED — VALIDATED FOUNDATION` | Comportamiento técnico validado en un milestone, dentro del alcance exacto de ese milestone. |
| `TECHNICAL STATE` | Lo que muestra la evidencia actual; todavía puede ser debug, provisional o estar pendiente de validación. |
| `DESIGN TARGET` | Dirección prevista de cara al jugador que aún necesita una feature spec, un playtest o reglas detalladas. |
| `PROPOSAL` | Opción editorial o de diseño conservada para evaluación; no es canon ni alcance comprometido. |
| `PENDING MAURO DECISION` | Elección que cambia identidad, canon, producto o alcance material y que no puede resolverse silenciosamente. |
| `DEFERRED` | No está en la secuencia activa; requiere su trigger declarado antes de reactivarse. |
| `OUT OF CURRENT SCOPE` | No está autorizado por el milestone activo ni por el corte de producción actual. |

## Dirección Confirmada Y Límites

Los siguientes principios de alto nivel provienen de dirección reciente explícita. No aprueban cada mecánica detallada propuesta en v3.1.

| Dirección | Estado | Consecuencia |
| --- | --- | --- |
| Old Scars es un juego de supervivencia, pero no una plantilla genérica de supervivencia. | `CONFIRMED — RECENT DECISION` | Las features deben sostener la identidad específica descrita abajo en lugar de copiar convenciones del género. |
| El mundo es industrial, destruido, escaso y peligroso. | `CONFIRMED — RECENT DECISION` | Los lugares, recursos y amenazas deben comunicar función material y deterioro. La historia detallada permanece abierta. |
| La dirección visual general es PSX, low-poly y de legibilidad retro coherente con Old Scars. | `CONFIRMED — RECENT DECISION` | Esta dirección no aprueba una art bible, referencias, assets, cámara ni especificaciones de producción concretas. |
| Old Scars no tiene zombis. | `CONFIRMED — RECENT DECISION` | Las amenazas provienen de humanos, facciones, animales, clima, distancia, heridas, armas, máquinas y estructuras deterioradas. |
| Los objetos conservan identidad física. | `CONFIRMED — RECENT DECISION` | Los items particulares rastreables deben conservar su identidad a través de los flujos soportados. La granularidad de identidad de las unidades fungibles de un stack sigue siendo una decisión explícita de frontera para M36.1. |
| La supervivencia debe crear decisiones informadas y predecibles en vez de RNG punitivo y opaco. | `CONFIRMED — RECENT DECISION` | Los costos, el peligro, el fallo y la recuperación necesitan causas y alternativas legibles. |
| Las barras, niveles y simulaciones se justifican solo cuando producen decisiones jugables. | `CONFIRMED — RECENT DECISION` | Los medidores de mantenimiento aislados no satisfacen el diseño. |
| La profundidad surge de sistemas conectados. | `CONFIRMED — RECENT DECISION` | Un sistema jugable nuevo consume estado relevante, cambia una decisión y proporciona feedback explicativo. |
| El daño localizado, sangrado, dolor, armadura y penetración pertenecen a la visión futura. | `DESIGN TARGET — HIGH LEVEL CONFIRMED` | La estructura exacta de turnos, el modelo corporal, las fórmulas, la profundidad médica y el balance permanecen pendientes de feature specs y revisión de Mauro. |
| Crafting, reparación, desmontaje, calidad y valor patrimonial son aspectos distintos. | `DESIGN TARGET — HIGH LEVEL CONFIRMED` | No deben colapsarse en un único sistema universal de crafting; se implementan solo en sus milestones del roadmap. |
| El contenido importante es autoral; la variación procedural es secundaria. | `CONFIRMED — RECENT DECISION` | M47.0 se limita a variación secundaria controlada, determinista y persistente. |
| Evitar sistemas universales preventivos y sobreingeniería prematura. | `CONFIRMED — RECENT DECISION` | Construir el contrato más pequeño que requieran el milestone activo y el consumidor actual. |
| La producción no se optimiza únicamente para una demo rápida. | `CONFIRMED — RECENT DECISION` | Los sistemas grandes pueden existir en el roadmap extenso cuando sean necesarios, pero su presencia allí no autoriza iniciarlos antes de tiempo. |

Decisiones de producción adicionales ya fijadas por M36.0:

- sueño/descanso es `MUST`; fatiga es `SHOULD`;
- el alcance de facciones, cuando corresponda, se limita inicialmente a identidad, disposición y memoria mínima;
- el alcance procedural es variación secundaria controlada, no un mundo generado;
- la estación de bombeo es candidata a vertical slice, no canon narrativo cerrado;
- M36.1 es un freeze corto y un contrato de identidad, no save/load, condition, repair ni actor lifecycle;
- M37 persiste primero el slice actual y no pre-serializa sistemas hipotéticos.

## Identidad De Producto En Revisión

### Promesa De Trabajo

`PROPOSAL — PENDING MAURO DECISION`

> Old Scars es un juego de supervivencia sistémico sobre exploración y recuperación en un mundo industrial devastado, donde cada expedición pide al jugador gestionar recursos físicos, heridas, equipamiento, riesgo humano y consecuencias persistentes.

Esta formulación conserva la dirección más sólida compartida por el brief de M36.0 y v3.1 sin decidir la combinación final de géneros, la cadencia de combate, el protagonista ni la campaña.

### Matriz De Decisiones De Identidad

| Tema | Línea base revisada | Estado |
| --- | --- | --- |
| Género central | La supervivencia está confirmada. `Tactical RPG`, el posicionamiento immersive sim y la combinación comercial exacta de géneros siguen siendo candidatos. | `PARTIAL CONFIRMATION / PENDING MAURO DECISION` |
| Modo de juego | v3.1 propone un juego para un jugador. | `PENDING MAURO DECISION` |
| Presentación | La dirección visual general PSX/low-poly y de legibilidad retro está confirmada. El juego actual es 3D; la cámara del prototipo tiene orientación isométrica y puede rotar, pero su composición y comportamiento finales siguen abiertos. | `CONFIRMED VISUAL DIRECTION / TECHNICAL STATE / FINAL CAMERA PENDING` |
| Cadencia de combate | v3.1 propone encuentros por turnos con puntos de acción; el rifle actual es un prototipo técnico debug/en tiempo real y no resuelve el combate final. | `DESIGN TARGET — PENDING MAURO DECISION` |
| Palabra rectora | v3.1 propone `PENSAR`. | `PENDING MAURO DECISION` |
| Tono | La escasez industrial y el peligro están confirmados; los límites melancólicos, curiosos, hostiles, adultos y de horror necesitan una decisión tonal consolidada. | `PARTIAL CONFIRMATION / PENDING MAURO DECISION` |
| Zombis | No pertenecen a Old Scars. | `CONFIRMED — RECENT DECISION` |
| Progreso | v3.1 propone crecer desde una persona irrelevante hacia mayor relevancia social/logística sin invulnerabilidad. | `DESIGN TARGET — PENDING MAURO DECISION` |
| Plataforma y tienda | PC/Windows, Steam y una experiencia premium para un jugador son propuestas, no compromisos comerciales aprobados. | `PROPOSAL — PENDING MAURO DECISION` |
| Idiomas y clasificación | Español/inglés y 16+/M son solo hipótesis de planificación. | `PROPOSAL — PENDING MAURO DECISION` |

### Pilares Candidatos

Estos son candidatos a revisión, no canon aprobado silenciosamente.

| Candidato | Fantasía y decisión del jugador | Sistemas de soporte | Contradicción | Evidencia de éxito |
| --- | --- | --- | --- | --- |
| Supervivencia informada | Leer la presión, prepararse y aceptar un costo legible. | Necesidades, tiempo, refugio, tratamiento, equipamiento. | Castigo opaco o mantenimiento repetitivo de barras. | El jugador puede explicar por qué cambió una elección de ruta, item o recuperación. |
| Objetos físicos con identidad | Transportar, proteger, equipar, usar y recuperar cosas particulares. | `ItemInstance`, storage, peso, Equipment, ownership y condition más adelante. | Objetos rastreables que se duplican o reinician silenciosamente. | Los items no fungibles conservan identidad y consecuencias; la semántica aprobada de stacks sobrevive a las transferencias soportadas y a los round-trips de save. |
| Vulnerabilidad con consecuencias creíbles | Evitar, negociar, luchar o retirarse mientras las heridas importan. | Salud, daño, armadura, medicina, IA y feedback. | Crecimiento de poder que trivializa humanos, ambiente o lesiones. | Una herida cambia decisiones posteriores sin producir una espiral de muerte ilegible. |
| Expedición y regreso | Prepararse, abandonar la seguridad, adaptarse, recuperarse y vivir con el resultado. | Inventario, mundo, tiempo, refugio, persistencia y encuentros. | Un ciclo de extracción donde la cantidad de loot es el único objetivo. | La preparación y el regreso cambian el estado y las prioridades futuras. |
| Mundo humano y persistente | Personas y lugares responden causalmente a acciones presenciadas y al tiempo. | Actor lifecycle, memoria, facciones, eventos y save. | Simulación global opaca o consecuencias sin una causa legible. | Al regresar se revela un cambio que el jugador puede conectar con una acción previa. |
| Profundidad de sistemas conectados | Resolver un problema mediante recursos y estados que interactúan. | Acciones data-driven más servicios de dominio y feedback. | Scripts universales, medidores aislados o autoridades duplicadas. | Una feature consume el estado de otro sistema y cambia una elección significativa. |

## Bucles Jugables

La siguiente estructura de bucles es un objetivo de diseño mantenible derivado de v3.1 y la auditoría de M36.0. Describe lo que el juego futuro debe demostrar; no representa el estado de implementación actual.

| Bucle | Actividad del jugador | Decisiones y riesgo | Foundations requeridas | Soporte actual |
| --- | --- | --- | --- | --- |
| Inmediato | Observar, inspeccionar, moverse, interactuar, reorganizarse o retirarse. | Tiempo, posición, herramienta, exposición/riesgo y costo de oportunidad. | Interacción, input, feedback, items. | Existen foundations de interacción contextual e items; el feedback y el input finales no. |
| Encuentro | Leer una persona, animal, máquina o peligro y elegir evitación, contexto, negociación, fuerza o retirada. | Lesión, munición, exposición, reputación y tiempo. | Actor lifecycle, combate, IA/percepción, ambiente. | Rifle/salud son prototipos; el diseño final de encuentros está ausente. |
| Expedición | Preparar carga y ruta, abandonar la seguridad, adaptarse, recuperar recursos útiles y regresar. | Capacidad, distancia, suministros, lesiones y prioridades cambiantes. | Persistencia, reloj, supervivencia, estructura del mundo, refugio. | Existe logística de objetos; el bucle completo no. |
| Refugio y recuperación | Almacenar, tratar, descansar, reparar, planificar y elegir el siguiente compromiso. | Seguridad, tiempo, uso de recursos y profundidad de recuperación. | Sueño/descanso, medicina, condition/repair, refugio. | Planificado entre M38–M44. |
| Progresión a largo plazo | Obtener conocimiento, capacidades, equipamiento, relaciones y alcance logístico. | Especialización, obligación y vulnerabilidad conservada. | Skills, economía, refugio, memoria del mundo. | Planificado; el modelo exacto de progresión está pendiente. |
| Regional/social | Influir sobre grupos y lugares locales mediante decisiones presenciadas. | Acceso, confianza, represalia, escasez y tiempo. | Mínimo de facciones, eventos, persistencia, contenido autoral. | Futuro M46–M47; mapa, facciones y campaña están pendientes. |

## Línea Base De Sistemas

Esta tabla separa intencionalmente la dirección de diseño de la evidencia técnica. Los estados exactos de milestones siguen bajo la autoridad de [Project_Roadmap.md](Project_Roadmap.md).

| Dominio | Dirección de diseño revisada | Estado técnico en M36.0 | Frontera del roadmap |
| --- | --- | --- | --- |
| Datos, tags y acciones | Los datos describen contenido y solicitudes cerradas; C# valida y ejecuta lógica. | Foundation validada. Los mods son aditivos; no hay overrides de definiciones, manifests ni versionado. | La compatibilidad/empaquetado de mods se amplía en M50.0. |
| Identidad de items | Los objetos particulares rastreables conservan identidad; la cantidad de stack y el placement espacial son aspectos separados. | Un stack usa una `ItemInstance` representativa más `ItemStorageEntry.Quantity`; las unidades fungibles dentro de ese stack no tienen IDs individuales. Un split crea un ID hermano y un merge conserva una instancia representativa. `GridInventoryLayout` posee el placement y la orientación por `InstanceId`. Los IDs son estables durante la sesión, no durables. | M36.1 congela la identidad durable y decide qué categorías de items requieren identidad por objeto; M37 realiza el round-trip de la semántica aprobada. |
| Inventario, Equipment y peso | La organización física y los trade-offs siguen siendo importantes. | La grilla espacial, el peso, los slots, los sets multi-slot y las rutas transaccionales relevantes están validados; la UI es OnGUI debug. | Preservar los backends; la UI de producción es M48.0. |
| Ownership y storage perteneciente a items | Una instancia tiene un nodo owner; el storage contenido pertenece al item particular. | Validado en las rutas actuales, sin nesting v0. | Persistir el slice actual en M37; no generalizar nesting antes de tiempo. |
| Loot, contenedores y cuerpos | El contenido proviene de storages reales; los cuerpos exponen pertenencias reales. | Los flujos actuales de contenedores y M35.2.3 están validados. Reabrir cuerpos vacíos y las acciones universales sobre cadáveres están diferidos. | Reactivar solo a partir de un bucle posterior demostrado. |
| Interacción y puertas | El contexto, el estado y las herramientas proporcionan varias resoluciones legibles. | Effects C# cerrados y estados de puerta `locked_door`, `closed_door`, `opened_door`; parte del trabajo de visibilidad sigue pendiente de validación. | Las puertas rotas u otras variantes de interacción requieren contratos futuros autorizados. |
| Rigs visuales | La presentación reacciona a Equipment confirmado sin ser dueña del gameplay. | M35.0 validado; las parts/sockets del rig están anidadas en perfiles de rig, mientras capabilities/assets/perfiles de item/poses son familias separadas. | El pipeline de arte/animación escala en M48.1. |
| Bootstrap de actores | Los perfiles crean inventario y Equipment reales sin estado falso paralelo. | M35.1 validado. El inventario inicial se aplica por entry; el lote de Equipment inicial es atómico después del inventario. | El lifecycle durable comienza en M38.0. |
| Save y persistencia | Preservar la identidad y las consecuencias actuales de forma segura. | No existe save/load ni rehidratación durable. | Contrato de identidad en M36.1; formato/recovery/round-trip del slice actual en M37. |
| Salud y muerte | Vulnerabilidad, transición coherente a muerte/cuerpo y política de recuperación. | Salud escalar y tags de muerte debug; no es el lifecycle final ni el diseño de game over. | Lifecycle en M38.0; salud localizada/medicina en M39. |
| Combate, daño y armadura | Vulnerabilidad creíble futura con protección explicable y alternativas. | Rifle/munición/aiming son prototipos técnicos, no el combate final. | M39–M40.1; turnos/AP/cuerpo/fórmulas exactos pendientes de Mauro y feature specs. |
| Necesidades, tiempo y ambiente | Las presiones importan solo cuando alteran ruta, preparación o recuperación. | Necesidades debug parciales; no hay bucle de supervivencia integrado. | Sueño/descanso `MUST`, fatiga `SHOULD` en M38.1; clima en M42.0; comida/agua/ecología en M42.1. Una enfermedad general necesita un nuevo rebaseline. |
| Condition, repair, disassembly y crafting | Decisiones materiales distintas en vez de un único árbol enciclopédico de crafting. | Existe un valor inicial de condition, pero no es un sistema mutable validado. | Condition/repair/disassembly en M43.0; crafting acotado en M43.1. |
| Skills y refugio | El progreso amplía opciones mientras la recuperación conserva costos. | No son sistemas finales. | Skills en M44.0; refugio/recuperación funcionales en M44.1. |
| IA y navegación | Comenzar con comportamiento diagnosticable de evitar/alertarse/huir/luchar. | Navegación, percepción e IA finales ausentes. | M41.0–M41.1. La memoria social llega después, en M46.1. |
| Mundo, contenido y narrativa | Lugares autorales con variación secundaria controlada y consecuencias causales. | La escena/los POIs debug soportan pruebas técnicas; no hay campaña ni topología final del mundo. | Herramientas/sectorización en M45.0; slice candidato en M45.1; narrativa en M47.1. |
| Facciones | El alcance inicial del sistema es solo identidad, disposición y memoria mínima. | No hay sistema final de facciones ni roster moderno aprobado. | M46.1; no hay simulación de guerra estratégica. |
| UI, accesibilidad, arte y audio | Decisiones legibles, errores recuperables y una barra representativa coherente. | Presentación OnGUI/debug y foundations visuales; no hay UI ni pipeline de audio finales. | Baseline del slice en M45.1; UI en M48.0; pipeline de arte/animación/audio en M48.1. |

### Invariantes Técnicas Actuales

`CONFIRMED — VALIDATED FOUNDATION`, dentro de las rutas implementadas exactas:

- los IDs usan snake_case; los duplicados se rechazan dentro de su tipo/registro;
- las definiciones viven en JSON/GameDatabase y las instancias/el estado runtime no se escriben de vuelta en los datos de Core;
- `ItemInstance.InstanceId` identifica el item runtime concreto; la cantidad pertenece a `ItemStorageEntry`, mientras el placement y la orientación de grilla pertenecen a `GridInventoryLayout`, indexados por `InstanceId`;
- un stack tiene una `ItemInstance` representativa más `Quantity`; sus unidades fungibles no están identificadas individualmente, un split crea un hermano y un merge conserva una instancia representativa. M36.1 debe decidir si este sigue siendo el contrato durable por categoría de item;
- un item pertenece a un nodo de storage y los slots de Equipment hacen referencia a la misma instancia en vez de duplicarla;
- las transferencias soportadas usan preflight/commit/rollback y conservan identidad, cantidades, placements y ownership;
- los hooks y observers post-commit son notificaciones best-effort y no forman parte del rollback del estado ya confirmado;
- Equipment multi-slot se deduplica y se representa visualmente una sola vez por `InstanceId`;
- el storage perteneciente a un item pertenece a la instancia concreta y el nesting se rechaza en v0;
- el estado visual se publica solo después de un commit exitoso de Equipment;
- los paneles OnGUI y el comportamiento debug de salud/armas son prototipos, no UX de producto ni combate final;
- no se afirma atomicidad universal entre actores: M35.2.3.1 sigue diferido y el riesgo R18 continúa activo;
- el loader registra las definiciones parseadas en `GameDatabase` y luego se valida la base cargada; la documentación no debe invertir esto como si fuera otro pipeline actual.

Ver [Technical_Architecture.md](Technical_Architecture.md) y [DataDriven_JSON_Rules.md](DataDriven_JSON_Rules.md) para los contratos completos.

## Ledger De Decisiones De Mundo, Lore Y Narrativa

| Tema de v3.1 | Tratamiento revisado | Estado |
| --- | --- | --- |
| Mundo industrial devastado | La dirección de setting de alto nivel está confirmada. | `CONFIRMED — RECENT DECISION` |
| Colapso gradual mediante guerra por recursos y enfermedad | Diseño previo importante; las causas y la secuencia exactas carecen de una decisión reciente trazable. | `PENDING MAURO DECISION` |
| Vandor y Velgrad | Ambos existen dentro del universo de Old Scars y sus nombres forman parte de la dirección actual. Su historia, cronología, guerra, fronteras, geografía, pueblos, lenguas, culturas, doctrinas, colores, símbolos, tecnología, relación con el colapso y posibles herederos o facciones modernas siguen abiertos. | `CONFIRMED — RECENT DECISION / LORE PENDING` |
| Industria persistente y máquinas autónomas que siguen órdenes obsoletas | Propuesta de identidad sólida, compatible con la dirección industrial; el canon y la prevalencia exactos siguen abiertos. | `DESIGN TARGET — PENDING MAURO DECISION` |
| El protagonista comienza siendo nadie | Propuesta de progresión, no contrato fijo del personaje. | `PENDING MAURO DECISION` |
| Abuelo asesinado por bandidos con cicatrices | Solo una semilla narrativa; puede ser fija, opcional o eliminarse. | `PROPOSAL — PENDING MAURO DECISION` |
| Campaña principal | Se propone dirección más libertad; el objetivo, la estructura y los finales están abiertos. | `PENDING MAURO DECISION` |
| Mapa y regiones | Los arquetipos regionales de v3.1 son lentes de diseño, no regiones canónicas con nombre ni una topología aprobada. | `PROPOSAL — PENDING MAURO DECISION` |
| Facciones modernas | No hay roster aprobado. La implementación futura se limita primero a identidad, disposición y memoria mínima. | `PENDING MAURO DECISION` |
| Sin zombis | Dirección actual explícita. La enfermedad histórica no autoriza automáticamente un sistema general de enfermedad jugable. | `CONFIRMED — RECENT DECISION` |
| Compañeros | Posible propuesta temática/sistémica sin milestone reservado. | `DEFERRED — PENDING MAURO DECISION` |
| Vehículos | Pueden aparecer como lenguaje del mundo o infraestructura; la conducción/el gameplay central con vehículos quedan fuera del plan base sin rebaseline. | `DEFERRED — PENDING MAURO DECISION` |

El contenido de lore debe distinguir hecho confirmado, rumor dentro del mundo, verdad desconocida y propuesta editorial. No completar la historia faltante a partir de equivalentes del mundo real ni de convenciones del género.

## Presentación, UX Y Comunicación

### Dirección Visual

- PSX, low-poly y legibilidad retro coherente con Old Scars son `CONFIRMED — RECENT DECISION` como dirección visual general.
- La art bible exacta, resolución y densidad de texturas, paleta, iluminación, shaders, jitter/deformación, filtros, budgets, pipeline y criterios de consistencia permanecen como `DESIGN TARGET / PENDING MAURO DECISION / FUTURE PRODUCTION SPEC`.
- El comportamiento final de cámara está `PENDING MAURO DECISION`; la rotación actual es evidencia del prototipo, no diseño final automático.
- Los roots de gameplay permanecen estables mientras la presentación reemplazable se adjunta mediante contratos validados de rig/socket/pose.
- Los vehículos y máquinas grandes pueden comunicar escala y función sin prometer que cada uno sea utilizable.
- Las 35 imágenes de referencia únicas de v3.1 son material de moodboard interno con derechos no verificados. No son assets del repositorio, material público de marketing ni canon automático de facciones.
- El material público requiere reemplazos propios, licenciados o permitidos explícitamente y un ledger de derechos.

### UI Y Accesibilidad

- Las superficies OnGUI actuales son instrumentación debug.
- La UI de producto debe preservar la autoridad de los backends en vez de duplicar lógica de inventario, Equipment o acciones.
- Los resultados y la presentación se actualizan después del estado confirmado, nunca antes del éxito.
- Los resultados de accesibilidad se requieren progresivamente, pero los mapeos concretos de controles, las resoluciones objetivo y las matrices de dispositivos siguen siendo propuestas hasta sus specs de producción.
- `1366×768` puede usarse como viewport temporal de regresión; no es un requisito de producto aprobado.
- Los harnesses de save en M37, la UX acotada del slice en M45.1 y la UI de producción en M48.0 son alcances separados.

### Audio Y Localización

- El audio debe comunicar fuente, peligro y función material, pero la lista P0/P1/P2 de v3.1 es una propuesta y no un plan de implementación actual.
- M45.1 necesita una barra de audio representativa y acotada; M48.1 crea el pipeline escalable.
- No existe una mecánica jugable de ruido confirmada, ni barra, stat o contrato asociado. Una percepción auditiva interna futura requiere diseño y milestone explícitos.
- No agregar placeholders JSON de `sound`/`noise` antes de un contrato autorizado.
- Una estructura preparada para localización es una preocupación futura. Idiomas, fallback, fuentes, expansión de texto y ownership del glosario siguen siendo decisiones de producto pendientes.

### Comunicación Comercial

PC/Windows, Steam, precio premium, tags de tienda, idiomas, clasificación, deck para publishers, estructura de tráiler y copy de marketing de v3.1 siguen como `PROPOSAL — PENDING MAURO DECISION`. Ninguna feature futura debe comunicarse como disponible antes de que exista evidencia representativa.

## Línea Base De Producción

El roadmap canónico no se duplica aquí. Su camino crítico actual comienza:

`M36.0 → M36.1 → M37.0 → M37.1 → M38.0`

Los trece gates canónicos, la secuencia completa M36–M55, las dependencias y los estados viven en [Project_Roadmap.md](Project_Roadmap.md). La evidencia, la deuda aceptable y R01–R23 viven en [Production_Gates_and_Risks.md](Production_Gates_and_Risks.md).

La estación de bombeo permanece como:

`M45.1 — PLANNED — CANDIDATE, NOT NARRATIVE CANON`

La casa abandonada sigue siendo un escenario debug/de integración, no un vertical slice competidor.

## Cola De Decisiones Creativas Pendientes De Mauro

La aprobación de esta línea base no resuelve las siguientes decisiones. Mauro debe aceptar, rechazar o modificar explícitamente cada una cuando corresponda:

1. género central exacto y combinación de géneros de mercado;
2. turnos/AP, tiempo real u otra cadencia de encuentros;
3. `PENSAR` como palabra rectora;
4. cámara final fija o rotatable;
5. límites tonales completos;
6. historia exacta del colapso y rol de la enfermedad;
7. historia, cronología y atributos todavía abiertos de Vandor y Velgrad, cuya existencia y nombres ya están confirmados;
8. prevalencia y rol de la industria/las máquinas persistentes;
9. rol del protagonista y semilla del abuelo/los bandidos con cicatrices;
10. estructura de campaña, mapa, regiones, facciones modernas y finales;
11. reglas detalladas de daño localizado, armadura, penetración y medicina;
12. muerte, incapacidad, checkpoints, autosave, dificultad y recuperación;
13. compañeros y su horizonte de lanzamiento;
14. profundidad del refugio;
15. vehículos como lenguaje del mundo frente a gameplay central;
16. cualquier profundidad procedural más allá de la variación secundaria controlada de M47.0;
17. plataforma, tienda, modelo comercial, clasificación e idiomas;
18. art bible y especificación de producción de la dirección PSX/low-poly confirmada, composición de cámara y reemplazo/licenciamiento de referencias;
19. dispositivos de input, resoluciones objetivo y baseline de accesibilidad;
20. alcance de audio y localización;
21. granularidad de identidad de unidades fungibles de stacks frente a objetos rastreables individualmente.

Hasta una decisión explícita sobre cada tema, el texto correspondiente conserva su etiqueta y no puede usarse como canon cerrado ni como autorización de milestone.

## Reconciliación Y Correcciones Del GDD Maestro v3.1

| Sección original | Problema | Corrección en esta línea base | Evidencia | Estado | Decisión pendiente |
| --- | --- | --- | --- | --- | --- |
| Control documental, T2 | v3.1 se denomina a sí mismo autoridad maestra. | Aplicar la jerarquía actual de verdad de siete niveles y conservar v3.1 como fuente auditable. | Corrección actual de M36.0; documentos de autoridad del repositorio. | `CORRECTED` | Mauro revisa la jerarquía resultante. |
| §01–§02 | Género, AP/turnos, `PENSAR`, tono y progresión se presentan juntos como confirmados. | Separar límites de supervivencia confirmados de elecciones de identidad del producto. | Brief actual más ausencia de aprobación trazable para el conjunto detallado. | `RECLASSIFIED` | Género, cadencia, palabra, tono, progresión. |
| §04–§05 | El lenguaje de combate final se mezcla con una implementación de rifle/debug. | Registrar rifle/salud como prototipos y el combate detallado como objetivo de diseño. | Arquitectura, código y Roadmap M39–M40.1. | `CORRECTED` | Reglas exactas de combate. |
| §04.5 | La casa abandonada se denomina vertical slice. | Tratarla como escenario técnico de integración/debug. | Roadmap y gate de Vertical Slice. | `CORRECTED` | Ninguna. |
| Ejemplo de §04; §11.4 | La estación de bombeo parece un slice narrativo definido. | Conservarla como candidata M45.1, no canon narrativo. | Roadmap M45.1 y blocker del gate. | `CORRECTED` | Si se convierte en canon. |
| Catálogo de sistemas de §05 | Los MVP detallados y el “diseño aprobado” exceden la aprobación demostrada. | Conservarlos como propuestas para feature specs posteriores salvo que una decisión actual diga lo contrario. | Corrección de M36.0; fronteras del Roadmap. | `RECLASSIFIED` | Mecánicas caso por caso. |
| Supervivencia de §05 | La enfermedad general forma parte del plan base. | Excluir la enfermedad general de la versión inicial completa sin un nuevo rebaseline. | Trabajo congelado/diferido del Roadmap. | `CORRECTED` | Si se reintroduce más adelante. |
| Armadura de §05 | El MVP de armadura requiere condition mutable. | M40.1 expone solo un seam futuro; condition mutable llega en M43.0. | Roadmap M40.1/M43.0 y gate Combat Ready. | `CORRECTED` | Ninguna salvo que cambie la secuencia. |
| Save de §05 | El primer save cubre ampliamente actores, quests, reputación y tiempo. | M37 cubre solo el slice actual; el actor lifecycle general comienza en M38. | Roadmap, Arquitectura, Next y Persistence Ready. | `CORRECTED` | UX de save y política de muerte/recuperación. |
| Compañeros/vehículos de §05 | Ambos aparecen planificados dentro de la progresión del producto. | Compañeros no tiene milestone reservado; los vehículos están fuera del plan base sin rebaseline. | Roadmap M36–M55 y trabajo congelado. | `RECLASSIFIED` | Base, posterior o rechazado. |
| Lore de §06 | Colapso, Vandor/Velgrad y la semilla del protagonista se denominan confirmados como un bloque. | Confirmar únicamente la existencia y los nombres Vandor/Velgrad junto con el setting industrial de alto nivel; mantener historia, cronología, geografía, guerra, culturas, símbolos, tecnología, colapso y herederos como decisiones separadas pendientes. | Decisión reciente de Mauro y jerarquía de autoridad actual. | `PARTIALLY CONFIRMED / RECLASSIFIED` | Ledger de lore detallado. |
| Regiones/facciones de §06 | El framework puede parecer un mapa/roster aprobado. | Tratar los arquetipos regionales como lentes y las facciones modernas como indefinidas. | Roadmap M45/M46.1. | `RECLASSIFIED` | Mapa, topología y roster. |
| Cámara de §07 | La cámara fija se presenta como confirmada. | Registrar el prototipo rotatable actual y dejar abierta la cámara final. | Comportamiento de cámara validado en Development Log/código. | `CORRECTED` | Cámara final. |
| Arte de §07 | Las asociaciones del moodboard y la mini art bible mezclan dirección general con especificación operativa y derechos. | Confirmar PSX/low-poly/legibilidad retro como dirección general; mantener art bible exacta, pipeline y referencias concretas como pendientes, y excluir material sin licencia de assets o comunicación pública. | Decisión reciente de Mauro, ledger de derechos de v3.1 y Roadmap M48.1. | `PARTIALLY CONFIRMED / RECLASSIFIED` | Especificación visual de producción y derechos. |
| UI de §08 | Los menús de save, bindings fijos y resolución parecen casi finales. | Separar harness de M37, baseline de M45.1 y UI de producción de M48.0; los números siguen siendo temporales. | Roadmap y Gates. | `CORRECTED` | Dispositivos, resoluciones y detalles de UX. |
| §09.3 | El diagrama sitúa la validación antes del registro en la base de datos. | Describir el flujo actual de cargar/registrar y luego validar sin congelarlo como diseño final. | Implementación de `GameDataManager`. | `CORRECTED` | Ninguna. |
| §09.4, T128 | Quantity se atribuye a Instance y el placement no se separa con claridad. | Quantity pertenece a `ItemStorageEntry`; el placement/la orientación pertenecen a `GridInventoryLayout`; la identidad/el estado pertenecen a `ItemInstance`. | Clases actuales de items y grilla más Arquitectura. | `CORRECTED` | Ninguna. |
| §09.4 / identidad de items | v3.1 puede implicar que cada unidad física de un stack tiene identidad propia. | Los stacks actuales usan una instancia representativa más cantidad; split/merge cambian el conjunto de instancias representativas. | `ItemStorageEntry`, `ItemStorage` y Arquitectura. | `LIMIT DOCUMENTED` | M36.1 decide la granularidad de identidad durable por categoría de item. |
| §09.6 | La atomicidad parece universal, incluidas las rutas entre actores. | Limitar las afirmaciones a servicios transaccionales validados; conservar R18 y M35.2.3.1 diferido. | Ledger del Roadmap y registro de riesgos. | `CORRECTED` | Contrato futuro entre actores. |
| §09.8 | Namespaces, manifests y overrides figuran como modding actual. | Los mods actuales son aditivos y rechazan duplicados; la compatibilidad avanzada pertenece a M50.0. | JSON Rules, Arquitectura y Roadmap. | `CORRECTED` | Política futura de M50. |
| §10 / Anexo J | El snapshot técnico quedó desactualizado después de M35.0. | Enlazar el Roadmap vivo y resumir el estado validado M35.0–M35.2. | Roadmap y Development Log. | `CORRECTED` | Ninguna. |
| §11 | G0–G5 y “salud/muerte siguiente” compiten con el roadmap actual. | Usar M36–M55 y trece gates por referencia; lo siguiente es M36.1 y luego M37. | Roadmap, Current y Next. | `CORRECTED` | Ninguna. |
| §12 | R1–R13 duplican el sistema vivo de riesgos. | Usar R01–R23 y la matriz de gates por referencia. | Production Gates and Risks. | `CORRECTED` | Ninguna. |
| §13 | El copy comercial puede implicar compromisos. | Mantener todo el material comercial etiquetado como propuesta y condicionado a evidencia. | Etiquetas TBD del propio GDD; corrección actual. | `RECLASSIFIED` | Decisiones de producto/comerciales. |
| Anexo C | Las 35 referencias únicas carecen de derechos demostrados. | Conservar solo como moodboard histórico interno; exigir reemplazo/licencia antes de publicar. | Ledger de derechos de v3.1. | `RISK RETAINED` | Derechos asset por asset. |

## Preservación De La Fuente Y Futura Revisión Visual

El GDD Maestro v3.1 fue auditado estructuralmente y conservado byte por byte. No se produce `v3.2_CANDIDATE.docx` en M36.0 porque esta estación no dispone de Word ni LibreOffice para renderizar y comparar de forma segura sus 190 tablas, 61 imágenes incrustadas, estilos, numeración, footer y layout de páginas. Tampoco hay un PDF independiente disponible para comparación.

Una futura revisión visual debe:

1. copiar, nunca sobrescribir, v3.1;
2. aplicar esta reconciliación y las decisiones de Mauro;
3. actualizar versión, fecha, declaración de autoridad y changelog;
4. regenerar tabla de contenidos, números de página y referencias cruzadas;
5. revisar headers de tablas, cortes de filas, accesibilidad y formato directo;
6. verificar cada caption de imagen, atribución, licencia y reemplazo;
7. renderizar cada página a PDF y comparar clipping, viudas/huérfanas, diagramas, footers y numeración;
8. permanecer `DRAFT — PENDING MAURO REVIEW` hasta recibir aprobación explícita.
