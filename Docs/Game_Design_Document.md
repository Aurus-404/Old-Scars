# Old Scars - Documento de Diseño del Juego

- Versión: línea base de repositorio 1.1
- Actualizado: 6 de agosto de 2026
- Estado: `APPROVED — REVISED DESIGN BASELINE`
- Derivado de: `Old_Scars_GDD_Maestro_v3.1.docx` (17 de julio de 2026)
- SHA-256 de la fuente: `919966D0BFCDE1FD77C6D7765EE087B4D04211FBDEAAD06B4AAFCCFEE7308AF4`

## Changelog 1.1

- Se confirma que la identidad principal es sobrevivir, explorar, viajar e improvisar sistémicamente; el looteo es importante, pero no define el juego.
- Se reemplaza la dirección PSX estricta por realismo estilizado nostálgico de PC/consola de mediados y finales de los 2000 y comienzos de los 2010.
- Se fija la dirección de vehículos y movilidad utilitaria, incluyendo bicicletas, motos, utilitarios, remolques, maquinaria de oruga y vehículos especiales raros.
- Se fija la distribución del arsenal: predominio de rifles de cerrojo y revólveres, jerarquía de armas automáticas y antiblindaje, variedad de munición y armas caseras previsiblemente deficientes.
- Estas decisiones de diseño no autorizan por sí mismas nuevos milestones ni alteran el roadmap técnico sin un rebaseline explícito.

## Propósito Y Autoridad

Este archivo es la línea base de diseño revisada, resumida y mantenible en Git para Old Scars. Fue derivado del GDD Maestro v3.1 y reconciliado críticamente contra él, las decisiones recientes, los milestones validados, los documentos vivos del repositorio, el historial de Git y el estado técnico implementado.

El GDD Maestro v3.1 sigue siendo una fuente histórica y de diseño importante. No es una especificación incuestionable, no fue sobrescrito y no prevalece automáticamente sobre decisiones posteriores o evidencia actual. Mauro conserva la autoridad creativa y de producto final, y las decisiones etiquetadas como pendientes permanecen abiertas.

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
| `CONFIRMED — RECENT DECISION` | Dirección explícita aprobada por Mauro. |
| `CONFIRMED — VALIDATED FOUNDATION` | Comportamiento técnico validado en un milestone, dentro del alcance exacto de ese milestone. |
| `TECHNICAL STATE` | Lo que muestra la evidencia actual; todavía puede ser debug, provisional o estar pendiente de validación. |
| `DESIGN TARGET` | Dirección prevista de cara al jugador que aún necesita una feature spec, un playtest o reglas detalladas. |
| `PROPOSAL` | Opción editorial o de diseño conservada para evaluación; no es canon ni alcance comprometido. |
| `PENDING MAURO DECISION` | Elección que cambia identidad, canon, producto o alcance material y que no puede resolverse silenciosamente. |
| `DEFERRED` | No está en la secuencia activa; requiere su trigger declarado antes de reactivarse. |
| `OUT OF CURRENT SCOPE` | No está autorizado por el milestone activo ni por el corte de producción actual. |

## Dirección Confirmada Y Límites

| Dirección | Estado | Consecuencia |
| --- | --- | --- |
| Old Scars es un juego de supervivencia, exploración, viaje e improvisación sistémica. | `CONFIRMED — RECENT DECISION` | El jugador debe sobrevivir y resolver problemas en un mundo devastado mediante sistemas interconectados; no limitar la experiencia a revisar loot o decidir qué cargar. |
| El looteo es una característica fuerte, no la identidad principal. | `CONFIRMED — RECENT DECISION` | Los objetos importan porque permiten interactuar, improvisar y resolver problemas; la acumulación de botín no debe ser el único objetivo ni el principal criterio de éxito. |
| El mundo está devastado y continúa marcado por una guerra antigua, incierta y posiblemente interminable. | `CONFIRMED — RECENT DECISION` | El viaje debe alternar peligro, silencio, restos de guerra, ruinas, naturaleza recuperando el concreto y belleza melancólica. |
| La dirección visual es realismo estilizado nostálgico de mediados/finales de los 2000 y comienzos de los 2010. | `CONFIRMED — RECENT DECISION` | Usar modelos low/mid-poly, texturas moderadas, materiales simples, arquitectura utilitaria, paisajes desolados, paletas apagadas e iluminación contenida. No aplicar un PSX estricto ni degradar la legibilidad mediante filtros agresivos. |
| Old Scars no tiene zombis. | `CONFIRMED — RECENT DECISION` | Las amenazas provienen de humanos, facciones, animales, clima, distancia, heridas, armas, máquinas y estructuras deterioradas. |
| Los objetos conservan identidad física. | `CONFIRMED — RECENT DECISION` | Los items particulares rastreables deben conservar su identidad a través de los flujos soportados. La granularidad durable de las unidades fungibles de un stack sigue siendo una decisión técnica de frontera. |
| La supervivencia debe crear decisiones informadas y predecibles en vez de RNG punitivo y opaco. | `CONFIRMED — RECENT DECISION` | Los costos, el peligro, el fallo y la recuperación necesitan causas y alternativas legibles. |
| Las barras, niveles y simulaciones se justifican solo cuando producen decisiones jugables. | `CONFIRMED — RECENT DECISION` | Los medidores de mantenimiento aislados no satisfacen el diseño. |
| La profundidad surge de sistemas conectados. | `CONFIRMED — RECENT DECISION` | Un sistema nuevo debe consumir estado relevante, cambiar una decisión y proporcionar feedback explicativo. |
| El daño localizado, sangrado, dolor, armadura y penetración pertenecen a la visión futura. | `DESIGN TARGET — HIGH LEVEL CONFIRMED` | La estructura exacta de combate, el modelo corporal, las fórmulas, la profundidad médica y el balance requieren feature specs. |
| Crafting, reparación, desmontaje, calidad y valor patrimonial son aspectos distintos. | `DESIGN TARGET — HIGH LEVEL CONFIRMED` | No deben colapsarse en un único sistema universal de crafting. |
| Los vehículos forman un ecosistema de herramientas de movilidad, trabajo y supervivencia. | `CONFIRMED — RECENT DECISION / OUT OF CURRENT IMPLEMENTATION SCOPE` | Su dirección de diseño está definida, pero la conducción y sus sistemas técnicos requieren rebaseline y milestones propios. |
| El arsenal tiene una distribución material e histórica clara. | `CONFIRMED — RECENT DECISION / DESIGN TARGET` | Rifles de cerrojo y revólveres predominan; las armas más complejas o especializadas son progresivamente menos comunes y más costosas de mantener y abastecer. |
| El contenido importante es autoral; la variación procedural es secundaria. | `CONFIRMED — RECENT DECISION` | M47.0 se limita a variación secundaria controlada, determinista y persistente. |
| Evitar sistemas universales preventivos y sobreingeniería prematura. | `CONFIRMED — RECENT DECISION` | Construir el contrato más pequeño que requieran el milestone activo y el consumidor actual. |
| La producción no se optimiza únicamente para una demo rápida. | `CONFIRMED — RECENT DECISION` | Los sistemas grandes pueden existir en el roadmap extenso cuando sean necesarios, pero su presencia en el diseño no autoriza iniciarlos antes de tiempo. |

Decisiones de producción adicionales ya fijadas:

- sueño/descanso es `MUST`; fatiga es `SHOULD`;
- el alcance de facciones, cuando corresponda, se limita inicialmente a identidad, disposición y memoria mínima;
- el alcance procedural es variación secundaria controlada, no un mundo generado;
- la estación de bombeo es candidata a vertical slice, no canon narrativo cerrado;
- M36.1 es un freeze corto y un contrato de identidad, no save/load, condition, repair ni actor lifecycle;
- M37 persiste primero el slice actual y no pre-serializa sistemas hipotéticos.

## Identidad De Producto

### Promesa De Trabajo

`CONFIRMED — RECENT DECISION`

> Old Scars es un juego de supervivencia, exploración y viaje en un mundo devastado que todavía carga las cicatrices de una guerra antigua e incierta. El jugador atraviesa rutas, ruinas y paisajes recuperados por la naturaleza, utilizando objetos, herramientas, vehículos, refugios y sistemas del entorno para improvisar soluciones y seguir con vida.

El looteo caracteriza la forma en que el jugador obtiene recursos y posibilidades, pero no define por sí solo la experiencia. La promesa combina vulnerabilidad, libertad sistémica, viaje, silencio, peligro y belleza dentro de la destrucción.

### Matriz De Decisiones De Identidad

| Tema | Línea base revisada | Estado |
| --- | --- | --- |
| Género central | Supervivencia, exploración e improvisación sistémica están confirmadas. La combinación comercial exacta de etiquetas sigue abierta. | `CONFIRMED CORE / MARKET LABELS PENDING` |
| Modo de juego | v3.1 propone un juego para un jugador. | `PENDING MAURO DECISION` |
| Presentación | Realismo estilizado nostálgico de PC/consola de mediados/finales de los 2000 y comienzos de los 2010. El juego actual es 3D; la cámara del prototipo tiene orientación isométrica y puede rotar, pero su composición final sigue abierta. | `CONFIRMED VISUAL DIRECTION / FINAL CAMERA PENDING` |
| Cadencia de combate | El rifle actual es un prototipo técnico debug/en tiempo real. La cadencia final requiere una decisión separada. | `DESIGN TARGET — PENDING MAURO DECISION` |
| Palabra rectora | v3.1 propone `PENSAR`. | `PENDING MAURO DECISION` |
| Tono | Melancolía, peligro, silencio, ruina, viaje y belleza natural sobre la destrucción están confirmados; los límites de horror y violencia todavía requieren consolidación. | `PARTIAL CONFIRMATION` |
| Zombis | No pertenecen a Old Scars. | `CONFIRMED — RECENT DECISION` |
| Progreso | El progreso debe ampliar opciones y alcance sin eliminar vulnerabilidad. El modelo social/logístico exacto sigue abierto. | `DESIGN TARGET` |
| Plataforma y tienda | PC/Windows, Steam y una experiencia premium para un jugador son propuestas, no compromisos comerciales aprobados. | `PROPOSAL — PENDING MAURO DECISION` |
| Idiomas y clasificación | Español/inglés y 16+/M son hipótesis de planificación. | `PROPOSAL — PENDING MAURO DECISION` |

### Pilares De Diseño

| Pilar | Fantasía y decisión del jugador | Contradicción |
| --- | --- | --- |
| Sobrevivir de forma informada | Leer el entorno, prepararse, improvisar y aceptar costos legibles. | Castigo opaco, RNG punitivo o mantenimiento repetitivo de barras. |
| Explorar y viajar | Atravesar grandes espacios, encontrar rutas, refugios, ruinas, vehículos y señales de vida o guerra. | Convertir el mundo en una sucesión de contenedores de loot sin paisaje, ritmo ni descubrimiento. |
| Objetos físicos con identidad | Transportar, proteger, equipar, usar, reparar, modificar, desarmar, vender o guardar objetos concretos. | Objetos intercambiables sin historia, estado ni consecuencias. |
| Improvisación sistémica | Resolver un problema mediante varias herramientas y sistemas existentes. | Botones o soluciones únicas creadas para un caso aislado. |
| Vulnerabilidad creíble | Evitar, negociar, luchar o retirarse mientras las heridas, la munición y la exposición importan. | Crecimiento de poder que trivializa humanos, ambiente o lesiones. |
| Belleza dentro de la destrucción | Encontrar silencio, naturaleza, escala y memoria entre las ruinas. | Saturar cada espacio con combate, loot o exposición narrativa. |
| Mundo humano y persistente | Personas y lugares responden causalmente a acciones presenciadas y al tiempo. | Simulación global opaca o consecuencias sin causa legible. |

## Bucles Jugables

| Bucle | Actividad del jugador | Decisiones y riesgo | Soporte actual |
| --- | --- | --- | --- |
| Inmediato | Observar, inspeccionar, moverse, interactuar, reorganizarse o retirarse. | Tiempo, posición, herramienta, exposición y costo de oportunidad. | Existen foundations de interacción contextual e items; el feedback final no. |
| Encuentro | Leer una persona, animal, máquina o peligro y elegir evitación, negociación, fuerza o retirada. | Lesión, munición, exposición, reputación y tiempo. | Rifle/salud son prototipos; el encuentro final no está resuelto. |
| Viaje y expedición | Elegir ruta, preparar suministros, recorrer espacios desolados, adaptarse, descubrir y regresar o continuar. | Distancia, terreno, clima, carga, combustible, refugio, lesiones y prioridades cambiantes. | Existe logística de objetos; el bucle completo no. |
| Refugio y recuperación | Almacenar, tratar, descansar, reparar y planificar. | Seguridad, tiempo, uso de recursos y profundidad de recuperación. | Planificado entre M38–M44. |
| Progresión a largo plazo | Obtener conocimiento, capacidades, equipamiento, relaciones y alcance logístico. | Especialización, obligación y vulnerabilidad conservada. | Planificado; el modelo exacto sigue pendiente. |
| Regional/social | Influir sobre grupos y lugares locales mediante decisiones presenciadas. | Acceso, confianza, represalia, escasez y tiempo. | Futuro M46–M47. |

## Línea Base De Sistemas

Los estados exactos de milestones siguen bajo la autoridad de [Project_Roadmap.md](Project_Roadmap.md).

| Dominio | Dirección de diseño revisada | Estado técnico | Frontera del roadmap |
| --- | --- | --- | --- |
| Datos, tags y acciones | Los datos describen contenido y solicitudes cerradas; C# valida y ejecuta lógica. | Foundation validada. Los mods son aditivos. | Compatibilidad/empaquetado de mods en M50.0. |
| Identidad de items | Los objetos particulares rastreables conservan identidad; cantidad y placement son aspectos separados. | Una `ItemInstance` representativa más `ItemStorageEntry.Quantity`; IDs todavía no durables. | M36.1 congela identidad durable; M37 realiza round-trip. |
| Inventario, Equipment y peso | La organización física y los trade-offs siguen siendo importantes. | Grilla, peso, slots y rutas transaccionales relevantes validados; UI OnGUI debug. | UI de producción en M48.0. |
| Ownership y storage perteneciente a items | Una instancia tiene un nodo owner; el storage contenido pertenece al item particular. | Validado en rutas actuales, sin nesting v0. | Persistir en M37; no generalizar antes de tiempo. |
| Loot, contenedores y cuerpos | El contenido proviene de storages reales; el loot sirve a supervivencia e improvisación, no como identidad exclusiva del juego. | Flujos actuales validados. | Reactivar extensiones solo desde un bucle demostrado. |
| Interacción y puertas | El contexto, el estado y las herramientas proporcionan varias resoluciones legibles. | Effects C# cerrados y estados de puerta existentes; visibilidad parcialmente pendiente. | Variantes futuras requieren contratos autorizados. |
| Rigs visuales | La presentación reacciona a Equipment confirmado sin ser dueña del gameplay. | M35.0 validado. | Pipeline de arte/animación en M48.1. |
| Bootstrap de actores | Los perfiles crean inventario y Equipment reales sin estado falso paralelo. | M35.1 validado. | Lifecycle durable en M38.0. |
| Save y persistencia | Preservar identidad y consecuencias actuales de forma segura. | Current Slice durable validado; tiempo, actores, estado médico y firearm state se extienden aditivamente sobre V1. | M36.1–M40.0; ampliar sólo por milestone. |
| Salud y muerte | Vulnerabilidad, transición coherente a muerte/cuerpo y política de recuperación. | M39 validó seis regiones, heridas durables, sangrado, dolor, venda localizada y muerte por reserva vital. | Tuning posterior de severity/bleeding; no amplía el alcance médico V1. |
| Combate, daño y armadura | Vulnerabilidad creíble, penetración explicable y arsenal con roles materiales claros. | M40.0 validó resolución única melee/firearm hacia M39, ammo/reload, seis regiones, near-cover blocking, estado durable por `ItemInstance` y persistence fresh-session. Armadura y penetración no están implementadas. | M40.1 `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`. |
| Arsenal y munición | Predominan rifles de cerrojo y revólveres; las armas complejas, automáticas, antiblindaje y especiales son progresivamente más raras. | No existe el sistema final de armas, modificaciones ni familias de munición. | Requiere specs dentro de combate, items, condition y crafting. |
| Vehículos y movilidad | Bicicletas, motos, motocarros, utilitarios, remolques, maquinaria de oruga y vehículos raros funcionan como herramientas de viaje, trabajo y supervivencia. | No existe conducción ni backend vehicular final. | `OUT OF CURRENT SCOPE`; requiere rebaseline y milestones propios. |
| Necesidades, tiempo y ambiente | Las presiones importan cuando alteran ruta, preparación o recuperación. | WorldClock, Hunger/Thirst y Rest/Sleep validados; bleeding consume el mismo delta. | Fatigue diferida; clima M42.0; comida/agua/ecología M42.1. |
| Condition, repair, disassembly y crafting | Decisiones materiales distintas, no un árbol enciclopédico universal. | Condition inicial no mutable como sistema validado. | M43.0–M43.1. |
| Skills y refugio | El progreso amplía opciones mientras la recuperación conserva costos. | No son sistemas finales. | M44.0–M44.1. |
| IA y navegación | Comenzar con comportamiento diagnosticable de evitar, alertarse, huir o luchar. | Navegación, percepción e IA finales ausentes. | M41.0–M41.1. |
| Mundo, contenido y narrativa | Lugares autorales, viaje legible, belleza melancólica y variación secundaria controlada. | POIs debug; no hay campaña ni topología final. | M45.0–M47.1. |
| Facciones | Alcance inicial: identidad, disposición y memoria mínima. | No hay sistema final. | M46.1; no hay simulación estratégica de guerra. |
| UI, accesibilidad, arte y audio | Decisiones legibles, errores recuperables y presentación coherente con la dirección nostálgica. | Presentación debug y foundations visuales. | M45.1, M48.0 y M48.1. |

### Invariantes Técnicas Actuales

`CONFIRMED — VALIDATED FOUNDATION`, dentro de las rutas implementadas exactas:

- los IDs usan snake_case; los duplicados se rechazan dentro de su tipo/registro;
- las definiciones viven en JSON/GameDatabase y las instancias/el estado runtime no se escriben de vuelta en los datos de Core;
- `ItemInstance.InstanceId` identifica el item runtime concreto; la cantidad pertenece a `ItemStorageEntry`, mientras el placement y la orientación de grilla pertenecen a `GridInventoryLayout`;
- un stack tiene una `ItemInstance` representativa más `Quantity`; un split crea un hermano y un merge conserva una instancia representativa;
- un item pertenece a un nodo de storage y los slots de Equipment hacen referencia a la misma instancia en vez de duplicarla;
- las transferencias soportadas usan preflight/commit/rollback y conservan identidad, cantidades, placements y ownership;
- los hooks y observers post-commit son notificaciones best-effort y no forman parte del rollback del estado confirmado;
- Equipment multi-slot se deduplica y se representa visualmente una sola vez por `InstanceId`;
- el storage perteneciente a un item pertenece a la instancia concreta y el nesting se rechaza en v0;
- el estado visual se publica solo después de un commit exitoso de Equipment;
- los paneles OnGUI y el comportamiento debug de salud/armas son prototipos, no UX de producto ni combate final;
- no se afirma atomicidad universal entre actores: M35.2.3.1 sigue diferido y el riesgo R18 continúa activo;
- el loader registra las definiciones parseadas en `GameDatabase` y luego valida la base cargada.

Ver [Technical_Architecture.md](Technical_Architecture.md) y [DataDriven_JSON_Rules.md](DataDriven_JSON_Rules.md) para los contratos completos.

## Arsenal, Munición Y Fabricación

`CONFIRMED — RECENT DECISION / DESIGN TARGET`

### Distribución Del Arsenal

- **Rifles de cerrojo:** armas comunes predominantes por su fabricación masiva para soldados de ambos bandos. Son robustos, abundantes y aptos para reparación, mejora, adaptación, desmontaje, comercio y almacenamiento.
- **Revólveres:** comunes por su simpleza, durabilidad, fabricación y reparación relativamente accesibles. Sus conversiones requieren cilindros, recámaras, cañones o adaptadores físicamente coherentes.
- **Ametralladoras antiguas:** menos comunes por su complejidad, pero valiosas y relativamente frecuentes en grupos militares organizados por su ventaja táctica. Diseños con cargadores de tambor o plato encajan en esta familia.
- **Battle rifles y fusiles de asalto antiguos:** existen y son encontrables, pero son menos frecuentes que los rifles de cerrojo debido a su complejidad, coste y menor disponibilidad.
- **Rifles antiblindaje:** armas raras, pesadas y difíciles de manejar, inspiradas en sistemas como Tankgewehr, PTRD o PTRS. Su munición escasa se reserva para máquinas, caminantes y blindados ligeros; usarla contra caza o infantería suele ser un desperdicio.
- **Lanzadores HEAT portátiles:** más fáciles de transportar que un rifle antiblindaje, pero difíciles de encontrar. Se reservan para tanques, mechas pesados y maquinaria fuertemente blindada.
- **Armas especiales:** rifles lanzagranadas, arpones, pistolas de gran calibre, lanzallamas y otras herramientas de nicho. Son raras y su valor depende de que el contexto justifique su peso y logística.

### Munición

La munición se diferencia por calibre, fabricación y función. Entre sus variantes se incluyen:

- FMJ;
- HP;
- AP;
- trazadoras;
- surplus de baja calidad;
- cargas o lotes especiales definidos cuando produzcan una decisión jugable clara.

Las diferencias deben afectar penetración, efecto terminal, precisión práctica, suciedad, desgaste, disponibilidad y valor comercial, no ser simples multiplicadores de daño.

### Armas Caseras Y Reconstruidas

Las armas caseras pueden ser fabricadas por el jugador o encontrarse en manos de personas sin acceso a armamento industrial. Son baratas y relativamente comunes. Pueden aceptar distintos cartuchos mediante piezas o adaptadores concretos, pero no son universalmente multicalibre.

Sus limitaciones deben ser previsibles:

- baja precisión o miras deficientes;
- menor velocidad de salida cuando el sellado o el cañón son pobres;
- recarga y extracción lentas;
- poca capacidad;
- desgaste rápido;
- necesidad frecuente de mantenimiento;
- ergonomía, retroceso o estabilidad deficientes.

No deben explotar aleatoriamente en la cara del jugador. Pueden decepcionar en un momento crítico por limitaciones conocidas y legibles, no por RNG punitivo.

### Regla De Identidad Y Uso

Cualquier arma puede encontrarse, usarse, mejorarse, adaptarse, desarmarse, venderse o guardarse. Su valor depende de potencia, munición disponible, estado, peso, complejidad, mantenimiento y función, no únicamente del daño nominal.

## Vehículos Y Movilidad

`CONFIRMED — RECENT DECISION / OUT OF CURRENT IMPLEMENTATION SCOPE`

La movilidad abarca un ecosistema más amplio que autos y camiones:

- bicicletas y bicicletas de carga;
- motos, motocarros y triciclos utilitarios;
- micro utilitarios, furgones, camionetas, buses y transportes civiles;
- jeeps y utilitarios militares o civiles reconvertidos;
- carros manuales, remolques y plataformas desplegables;
- generadores, cocinas, herramientas, cureñas y otros equipos sobre ruedas;
- vehículos compactos de oruga para barro, nieve, ruina o arrastre;
- raros vehículos anfibios, de ingeniería, rescate o recuperación.

### Principios

- Son herramientas de viaje, supervivencia, carga y trabajo, no premios que resuelven el juego.
- Deben ser viejos, austeros, funcionales, reparables y de silueta legible.
- La mezcla civil, industrial y militar debe mostrar una guerra prolongada y una cultura de reutilización.
- No todo lo móvil se conduce: algunos equipos se empujan, arrastran, remolcan o despliegan.
- Cuanto más raro y especializado sea un vehículo, menos frecuente debe ser y más clara debe ser su función.
- La movilidad silenciosa y de baja tecnología tiene presencia real porque no depende de combustible.

### Trade-offs

Los vehículos deben distinguirse por ruido, consumo, carga, protección, tracción, terreno, maniobrabilidad, mantenimiento, visibilidad y dificultad de recuperación. Encontrar uno operativo no elimina la supervivencia: introduce combustible, piezas, averías previsibles, rutas limitadas y el riesgo de abandonarlo lejos de un refugio.

La dirección de diseño está confirmada, pero su implementación no se incorpora silenciosamente al roadmap actual.

## Ledger De Decisiones De Mundo, Lore Y Narrativa

| Tema | Tratamiento revisado | Estado |
| --- | --- | --- |
| Mundo industrial devastado | Dirección de setting confirmada. | `CONFIRMED — RECENT DECISION` |
| Guerra antigua e incierta | El mundo continúa atravesado por una guerra cuyo inicio, final y comprensión completa son inciertos. | `CONFIRMED — RECENT DECISION / DETAILS PENDING` |
| Naturaleza y belleza entre ruinas | La naturaleza recupera concreto y espacios abandonados; la belleza melancólica forma parte del viaje. | `CONFIRMED — RECENT DECISION` |
| Colapso mediante guerra por recursos y enfermedad | Diseño previo importante; causas y secuencia exactas carecen de una decisión cerrada. | `PENDING MAURO DECISION` |
| Vandor y Velgrad | Existen y sus nombres forman parte de la dirección actual. Historia, geografía, culturas, doctrinas y cronología siguen abiertas. | `CONFIRMED — RECENT DECISION / LORE PENDING` |
| Industria persistente y máquinas autónomas | Compatible con la dirección industrial; canon y prevalencia exactos siguen abiertos. | `DESIGN TARGET — PENDING MAURO DECISION` |
| Protagonista | Comenzar siendo nadie es una propuesta de progresión, no un contrato fijo. | `PENDING MAURO DECISION` |
| Abuelo y bandidos con cicatrices | Semilla narrativa opcional. | `PROPOSAL — PENDING MAURO DECISION` |
| Campaña principal | Dirección más libertad; objetivo, estructura y finales abiertos. | `PENDING MAURO DECISION` |
| Mapa y regiones | Los arquetipos previos son lentes de diseño, no topología canónica. | `PROPOSAL — PENDING MAURO DECISION` |
| Facciones modernas | No hay roster aprobado. | `PENDING MAURO DECISION` |
| Sin zombis | Dirección explícita. | `CONFIRMED — RECENT DECISION` |
| Compañeros | Posible propuesta sin milestone reservado. | `DEFERRED — PENDING MAURO DECISION` |
| Vehículos | La dirección jugable y estética está confirmada; el alcance técnico y de producción requiere rebaseline. | `CONFIRMED DESIGN / OUT OF CURRENT SCOPE` |

El contenido de lore debe distinguir hecho confirmado, rumor dentro del mundo, verdad desconocida y propuesta editorial. No completar la historia faltante a partir de equivalentes del mundo real ni de convenciones del género.

## Presentación, UX Y Comunicación

### Dirección Visual

- La referencia principal es el realismo estilizado de juegos de PC/consola de mediados y finales de los 2000 y comienzos de los 2010, con influencias como Fallout 3, Oblivion, Men of War y Kenshi.
- Usar modelos low/mid-poly, texturas de resolución moderada, materiales simples, arquitectura utilitaria, grandes paisajes desolados, paletas apagadas e iluminación contenida.
- La nostalgia debe surgir de una construcción visual coherente, no de filtros superficiales.
- Evitar como regla general jitter agresivo, deformación constante de texturas, pixelación excesiva y tratamientos que reduzcan la legibilidad.
- La dirección debe favorecer exploración, supervivencia, vehículos, interiores, ruinas, objetos reconocibles y belleza melancólica.
- La art bible exacta, budgets, shaders, pipeline y criterios de consistencia permanecen como `DESIGN TARGET / FUTURE PRODUCTION SPEC`.
- El comportamiento final de cámara está `PENDING MAURO DECISION`; la rotación actual es evidencia del prototipo.
- Los roots de gameplay permanecen estables mientras la presentación reemplazable se adjunta mediante contratos validados.
- Las referencias visuales externas son moodboard interno mientras sus derechos no estén verificados; el material público requiere reemplazos propios, licenciados o permitidos.

### UI Y Accesibilidad

- Las superficies OnGUI actuales son instrumentación debug.
- El control de movimiento actual del player es `WASD` relativo a la pantalla/cámara sobre XZ: W siempre avanza hacia la parte superior de pantalla, incluso tras rotar el yaw de cámara. El click izquierdo no crea órdenes de movimiento. La cámara debug sigue continuamente al player; no tiene pan libre por flechas ni borde de pantalla, y conserva órbita RMB, zoom wheel y recenter MMB alrededor del pivot del player.
- La salud escalar actual se consulta en una ventana debug independiente con `H`; no es HUD permanente, no pausa el tiempo ni bloquea el movimiento. Heridas localizadas, medicina y la UI de producción siguen fuera de este contrato.
- La UI de producto debe preservar la autoridad de los backends en vez de duplicar lógica.
- Los resultados y la presentación se actualizan después del estado confirmado.
- Los resultados de accesibilidad se requieren progresivamente, pero controles, resoluciones y dispositivos concretos necesitan specs de producción.
- `1366×768` puede usarse como viewport temporal de regresión; no es un requisito final.
- Los harnesses de save, la UX del slice y la UI de producción son alcances separados.

### Audio Y Localización

- El audio debe comunicar fuente, peligro, distancia y función material.
- No existe una mecánica jugable de ruido confirmada ni un contrato asociado; requiere diseño y milestone explícitos.
- No agregar placeholders JSON de `sound` o `noise` antes de un contrato autorizado.
- Idiomas, fallback, fuentes, expansión de texto y ownership del glosario siguen pendientes.

### Comunicación Comercial

PC/Windows, Steam, precio premium, tags, idiomas, clasificación, deck para publishers, tráiler y copy comercial siguen como `PROPOSAL — PENDING MAURO DECISION`. Ninguna feature futura debe comunicarse como disponible antes de que exista evidencia representativa.

## Línea Base De Producción

El roadmap canónico no se duplica aquí. Su camino crítico actual comienza:

`M36.0 → M36.1 → M37.0 → M37.1 → M38.0`

Los gates, la secuencia M36–M55, las dependencias y los estados viven en [Project_Roadmap.md](Project_Roadmap.md). La evidencia y los riesgos viven en [Production_Gates_and_Risks.md](Production_Gates_and_Risks.md).

La estación de bombeo permanece como:

`M45.1 — PLANNED — CANDIDATE, NOT NARRATIVE CANON`

La casa abandonada sigue siendo un escenario debug/de integración, no un vertical slice competidor.

Las definiciones de arsenal y vehículos fijan dirección de diseño. No adelantan M39–M43 ni incorporan un sistema vehicular al roadmap sin un rebaseline formal.

## Cola De Decisiones Creativas Pendientes De Mauro

1. combinación exacta de géneros de mercado;
2. turnos/AP, tiempo real u otra cadencia de encuentros;
3. `PENSAR` como palabra rectora;
4. cámara final fija o rotatable;
5. límites tonales completos;
6. historia exacta del colapso y rol de la enfermedad;
7. historia, cronología y atributos abiertos de Vandor y Velgrad;
8. prevalencia y rol de las máquinas persistentes;
9. rol del protagonista y semilla del abuelo/bandidos;
10. estructura de campaña, mapa, regiones, facciones modernas y finales;
11. reglas detalladas de daño localizado, armadura, penetración y medicina;
12. muerte, incapacidad, checkpoints, autosave, dificultad y recuperación;
13. compañeros y su horizonte de lanzamiento;
14. profundidad del refugio;
15. alcance técnico, milestones y prioridad de los vehículos usables;
16. balance, catálogo exacto y reglas de modificación del arsenal;
17. profundidad procedural más allá de la variación secundaria controlada;
18. plataforma, tienda, modelo comercial, clasificación e idiomas;
19. art bible y especificación de producción de la dirección nostálgica confirmada;
20. dispositivos de input, resoluciones objetivo y baseline de accesibilidad;
21. alcance de audio y localización;
22. granularidad durable de unidades fungibles de stacks.

Hasta una decisión explícita, el texto correspondiente conserva su etiqueta y no puede usarse como autorización de milestone.

## Reconciliación Y Correcciones Del GDD Maestro v3.1

| Sección original | Problema | Corrección en esta línea base | Estado |
| --- | --- | --- | --- |
| Control documental | v3.1 se denomina autoridad maestra. | Aplicar la jerarquía actual de verdad y conservar v3.1 como fuente auditable. | `CORRECTED` |
| §01–§02 | Género, AP/turnos, tono y progresión aparecen juntos como confirmados. | Confirmar supervivencia, exploración, viaje e improvisación; separar las decisiones todavía abiertas. | `RECLASSIFIED` |
| Promesa de producto | La recuperación y el loot podían leerse como identidad central. | El foco es sobrevivir; el loot caracteriza las herramientas disponibles, no define el juego. | `CORRECTED — RECENT DECISION` |
| §04–§05 | Combate final mezclado con rifle/debug. | Rifle y salud son prototipos; arsenal y combate detallado son objetivos de diseño. | `CORRECTED` |
| §04.5 | Casa abandonada denominada vertical slice. | Escenario técnico de integración/debug. | `CORRECTED` |
| Estación de bombeo | Parece slice narrativo definido. | Candidata M45.1, no canon narrativo. | `CORRECTED` |
| Catálogo de sistemas | Los MVP exceden la aprobación demostrada. | Conservar como propuestas salvo decisiones recientes explícitas. | `RECLASSIFIED` |
| Supervivencia | Enfermedad general dentro del plan base. | Excluir sin nuevo rebaseline. | `CORRECTED` |
| Armadura | MVP requiere condition mutable. | M40.1 expone seam futuro; condition llega en M43.0. | `CORRECTED` |
| Save | Primer save demasiado amplio. | M37 cubre el slice actual; lifecycle general comienza en M38. | `CORRECTED` |
| Compañeros y vehículos | Ambos aparecían como progresión planificada. | Compañeros siguen diferidos. La dirección de vehículos está confirmada, pero su implementación está fuera del roadmap actual sin rebaseline. | `RECLASSIFIED` |
| Arsenal | No existía una jerarquía consolidada de disponibilidad y función. | Confirmar predominio de cerrojo/revólver, munición variada, rareza progresiva y armas caseras previsibles. | `ADDED — RECENT DECISION` |
| Lore | Colapso, Vandor/Velgrad y protagonista aparecían confirmados como bloque. | Confirmar solo lo decidido y mantener historia detallada abierta. | `PARTIALLY CONFIRMED` |
| Regiones/facciones | Podían parecer mapa y roster aprobados. | Tratar como lentes y contenido pendiente. | `RECLASSIFIED` |
| Cámara | Cámara fija presentada como confirmada. | Prototipo rotatable; cámara final abierta. | `CORRECTED` |
| Arte | PSX/low-poly presentado como dirección principal. | Sustituir por realismo estilizado nostálgico 2000s–2010s; PSX estricto y filtros agresivos dejan de ser la definición central. | `SUPERSEDED — RECENT DECISION` |
| UI | Menús, bindings y resolución parecían finales. | Separar harness, slice y UI de producción. | `CORRECTED` |
| Pipeline de datos | Validación situada antes del registro. | Cargar/registrar y luego validar según implementación actual. | `CORRECTED` |
| Identidad de items | Quantity y placement atribuidos de forma imprecisa. | Quantity en `ItemStorageEntry`; placement en `GridInventoryLayout`; identidad en `ItemInstance`. | `CORRECTED` |
| Atomicidad | Parecía universal entre actores. | Limitar a servicios validados; conservar riesgo R18. | `CORRECTED` |
| Modding | Namespaces, manifests y overrides figuraban como actuales. | Mods actuales aditivos; compatibilidad avanzada en M50.0. | `CORRECTED` |
| Snapshot técnico | Desactualizado después de M35.0. | Enlazar roadmap vivo y documentos técnicos. | `CORRECTED` |
| Roadmap y riesgos | G0–G5 y R1–R13 competían con documentos vivos. | Usar M36–M55 y registros actuales por referencia. | `CORRECTED` |
| Comunicación comercial | Podía implicar compromisos. | Mantener como propuesta condicionada a evidencia. | `RECLASSIFIED` |
| Referencias visuales | Derechos no demostrados. | Moodboard interno; exigir reemplazo o licencia antes de publicar. | `RISK RETAINED` |

## Preservación De La Fuente Y Futura Revisión Visual

El GDD Maestro v3.1 fue auditado estructuralmente y conservado byte por byte. No se sobrescribe la fuente histórica.

Una futura revisión visual debe:

1. copiar, nunca sobrescribir, v3.1;
2. aplicar esta reconciliación y las decisiones de Mauro;
3. actualizar versión, fecha, autoridad y changelog;
4. regenerar tabla de contenidos, números de página y referencias cruzadas;
5. revisar tablas, cortes de filas, accesibilidad y formato;
6. verificar captions, atribuciones, licencias y reemplazos;
7. renderizar a PDF y comprobar clipping, diagramas, footers y numeración;
8. permanecer `DRAFT — PENDING MAURO REVIEW` hasta recibir aprobación explícita.
