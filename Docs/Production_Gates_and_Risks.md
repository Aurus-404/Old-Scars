# Old Scars - Production Gates and Risks

## Proposito Y Autoridad

Este documento desarrolla criterios de salida, evidencia, deuda y riesgos para los gates publicados en [Project_Roadmap.md](Project_Roadmap.md).

[Project_Roadmap.md](Project_Roadmap.md) conserva la autoridad sobre IDs, estados, dependencias y nombres/ubicacion de gates. Este documento no reserva IDs, no cambia estados y no autoriza implementacion.

Mauro conserva la autoridad creativa y de producto. [Game_Design_Document.md](Game_Design_Document.md) contiene el baseline de diseño revisado; el GDD Maestro v3.1 se conserva intacto como fuente historica y de diseño auditada. [Technical_Architecture.md](Technical_Architecture.md) documenta los contratos tecnicos vigentes despues de contrastarlos con el codigo real.

## Regla De Aprobacion

Un gate puede aprobarse solamente cuando:

- su milestone de cierre y todas sus dependencias duras estan `VALIDATED` o `DONE`;
- existe evidencia reproducible del resultado jugable o tecnico requerido;
- la validacion separa checks estaticos, compilacion, pruebas automatizadas y prueba manual;
- la deuda aceptada tiene impacto, motivo y trigger de retorno registrados;
- todo riesgo asignado al cierre del gate esta `CLOSED`;
- todo riesgo asignado a revision tiene responsable y evidencia vigente; si no esta `CLOSED`, requiere mitigacion y estado `MITIGATING` o `ACCEPTED` explicito;
- los documentos de autoridad reflejan el mismo estado;
- existe evidencia Git del alcance exacto revisado.

`IMPLEMENTED` o una compilacion correcta no equivalen a gate aprobado. Un gate no crea una segunda taxonomia para milestones.

## Evidencia Comun

Cada revision de gate debe registrar:

- milestone y commits exactos;
- escenario de aceptacion e invariantes comprobadas;
- checks estaticos, compilaciones y pruebas automatizadas aplicables;
- evidencia manual en Unity cuando corresponda;
- estado de Console y fallos conocidos;
- evidencia de persistencia, rendimiento o tooling cuando formen parte del gate;
- deuda aceptada y deuda bloqueante;
- riesgos cerrados, mitigados o aceptados explicitamente;
- decision final de revision.

No se fijan cantidades de NPCs, tiempos, FPS ni otros umbrales numericos hasta aprobar plataforma objetivo, hardware de referencia, escenario representativo y presupuesto medible.

## Politica De Deuda

Es aceptable solamente la deuda que:

- no rompe identidad, ownership, referencias, persistencia ni el loop central;
- esta acotada y documentada;
- tiene un milestone o condicion concreta de retorno;
- no obliga al siguiente milestone a adivinar contratos.

Bloquea un gate la deuda que:

- permite perdida, duplicacion o regeneracion silenciosa de identidad;
- deja referencias rotas o fuentes de verdad competidoras;
- impide reproducir la ruta critica;
- oculta errores de carga, recovery o migracion;
- depende de bypasses hardcodeados;
- mantiene sin validar un comportamiento central del gate;
- carece de diagnostico suficiente para investigar fallos.

## Gates Canonicos

### Foundation Freeze - M36.1

Debe validar:

- identidad durable y fuentes de verdad para items, actores y objetos mundiales del slice actual;
- invariantes de ownership, referencias y mutaciones;
- limites entre definiciones JSON, instancias runtime y futuro estado persistido;
- decision explicita sobre persistir, rederivar o excluir justificadamente el `ItemInstance.Condition` get-only actual, sin implementar condition mutable;
- granularidad de identidad congelada para items particulares y unidades fungibles dentro de stacks;
- contratos que M37 puede consumir sin implementar save/load;
- ausencia de abstracciones nuevas sin consumidor real;
- evidencia de que M37 puede consumir los contratos congelados;
- ausencia de save/load implementado preventivamente dentro de M36.1;
- ausencia de generalizaciones para sistemas hipoteticos;
- seams de prueba y baseline del slice actual.

Evidencia: matriz de identidad y ownership, contratos documentados, pruebas de invariantes y lista explicita de decisiones congeladas.

Deuda aceptable: save/load, condition, repair/disassembly, actor lifecycle y gameplay nuevo.

Bloquea: IDs regenerables o ambiguos, ownership sin autoridad, mutaciones que eluden servicios existentes o cualquier contrato que M37 deba inferir.

### Persistence Ready - M37.1

Debe validar:

- envelope y version de save;
- escritura atomica, recovery y politica de migracion o rechazo;
- round-trip del slice actual;
- jugador y estado actual de sus componentes dentro del round-trip;
- conservacion de `InstanceId`, cantidades, placements, Equipment, ownership, item-owned storage, containers, cuerpos, puertas, world items y runtime tags;
- fallo de carga seguro y explicable.

Evidencia: snapshots antes/despues, escenarios de round-trip, recovery y versionado, checks automatizados aplicables y validacion manual sin errores relacionados en Console.

Deuda aceptable: cloud save y lifecycle/serializacion general de actores futuros, clima, facciones o proceduralidad que aun no existen. No se difieren el jugador ni los cuerpos actuales.

Bloquea: perdida o duplicacion, referencias colgantes, reset silencioso, saves sin version o recovery no demostrado.

### Combat Ready - M40.1

Debe validar:

- contrato unico de damage integrado con salud localizada;
- melee, firearms, ammo y reload;
- armor y penetration con resultados explicables;
- feedback suficiente para comprender impacto y proteccion;
- seams de integracion futura con condition sin implementar desgaste prematuramente;
- escenarios debug reproducibles.

Evidencia: matriz de combate con y sin proteccion, trazas del resolver, estado antes/despues y validacion manual.

Deuda aceptable: amplitud de armas, balance final, animacion/audio final y desgaste reparable futuro, siempre que M43 pueda integrarlo sin romper el contrato.

Bloquea: resoluciones paralelas, consumo de municion inconsistente, armor opaca o un contrato que impida integrar condition despues.

### AI Ready - M41.1

Debe validar:

- navegacion y percepcion diagnosticables;
- humanos capaces de evitar, alertarse, huir y luchar;
- uso del mismo contrato de combate;
- transiciones interrumpibles y feedback observable;
- comportamiento acotado, sin requerir facciones estrategicas.

Evidencia: escenarios de ruta valida/invalida, percepcion, perdida de contacto, huida y combate.

Deuda aceptable: pocos arquetipos, tacticas avanzadas, conversaciones y sistema amplio de facciones.

Bloquea: omnisciencia, estados no reproducibles, loops sin salida, bypass del combate o ausencia de diagnostico.

### World Systems Ready - M42.1

Debe validar:

- world clock y recovery;
- sueño/descanso como MUST;
- weather, exposure y proteccion conectados;
- comida, agua, purificacion y deterioro;
- animales/ecologia dentro del alcance acotado aprobado y sobre Navigation Foundation si requieren movilidad;
- persistencia del estado mutable incorporado.

Evidencia: escenarios donde tiempo, refugio, equipo y recursos cambian decisiones y ofrecen feedback explicable.

Deuda aceptable: fatiga como SHOULD, amplitud ecologica, contenido y presentacion finales.

Bloquea: omitir sueño/descanso, medidores aislados, RNG punitivo sin informacion o agentes animales moviles sin dependencia tecnica resuelta.

### Survival Systems Ready - M44.1

Debe validar:

- integracion entre necesidades, salud, medicina, entorno, condition, repair, crafting acotado, skills, refugio y recovery;
- loop preparacion-expedicion-retorno-recuperacion;
- estado persistente de los sistemas incorporados;
- profundidad mediante sistemas conectados.

Evidencia: recorrido integrado con decisiones de riesgo, herida, tratamiento, recursos, reparacion, refugio, progreso y guardado.

Deuda aceptable: balance final, contenido amplio, fatiga y UI/audio de produccion.

Bloquea: barras aisladas, sistemas que no modifican decisiones, recovery no funcional o estado nuevo que no sobrevive un round-trip.

### Content Pipeline Ready - M45.0

Debe validar:

- sectorizacion y flujo repetible de autorado;
- validators de IDs, tags, referencias y schemas;
- catalogos e inspeccion con errores accionables;
- creacion de contenido representativo sin bypasses ni cirugia manual rutinaria;
- presupuesto de rendimiento definido para contenido.

Evidencia: recorrido desde datos fuente hasta contenido jugable validado, reporte de errores intencionales y resultado reproducible.

Deuda aceptable: templates limitados, trabajo manual excepcional y packaging final de mods.

Bloquea: referencias rotas no detectadas, outputs no reproducibles, schema drift o contenido que exige cambios de codigo por cada caso.

### Vertical Slice Approved - M45.1

Debe validar:

- Foundation Freeze, Persistence Ready, Combat Ready, AI Ready, World Systems Ready, Survival Systems Ready y Content Pipeline Ready;
- M43.1 validado aunque no posea un gate propio;
- loop persistente de preparacion, expedicion y retorno;
- exploracion, encuentro humano, combate, herida, medicina, loot, cuerpo, peso, reparacion y consecuencias;
- una barra visual, sonora y de feedback representativa pero acotada; M48.1 debe convertirla despues en pipeline escalable;
- referencias visuales y sonoras con derechos trazables para cualquier evidencia externa o publica;
- una baseline acotada de inputs, legibilidad y alternativas de acceso; M48.0 debe convertirla despues en solucion de produccion;
- promesa y pilares aprobados por Mauro y demostrados mediante sistemas conectados.

Evidencia: decision documental de Mauro sobre promesa/pilares, playthrough reproducible, matriz de sistemas, save/load durante el recorrido, perfil de rendimiento y disposicion de riesgos.

Deuda aceptable: mapa acotado, balance y contenido no finales, y que la estacion de bombeo siga siendo candidata no canonica. El slice puede usar un objetivo y una consecuencia local acotados sobre contratos ya autorizados, pero no introduce por esa via sistemas generales de quests, reputacion o facciones.

Bloquea: sistemas simulados por bypasses, perdida persistente, feedback ilegible, pipeline irrepetible, promesa/pilares sin aprobar, declarar canonica la localizacion sin aprobacion o depender de un sistema general de quests/facciones no incorporado al roadmap.

### Production Ready - M50.0

Debe validar:

- sistemas posteriores a la vertical slice integrados sin romper contratos;
- asentamientos y economia material acotados;
- facciones limitadas a identidad, disposicion y memoria minima;
- variacion secundaria controlada, determinista y persistente;
- UI/UX y accesibilidad de produccion;
- pipelines de arte, animacion, audio y contenido escalables;
- rendimiento y compatibilidad de datos/mods bajo presupuestos aprobados.

Evidencia: produccion representativa mediante herramientas, validacion de compatibilidad/migracion, perfiles de rendimiento y regresion del loop central.

Deuda aceptable: contenido de lanzamiento incompleto, polish y tuning registrados.

Bloquea: feature churn, schemas inestables, contenido one-off, pipeline manual no escalable, overrides inseguros o falta de budgets.

### Alpha - M51.0

Debe validar: feature complete, recorrido de inicio a fin, integracion de todos los sistemas de lanzamiento y politica estable de saves.

Evidencia: recorrido completo, regresion, perfil de rendimiento y lista priorizada de defectos.

Deuda aceptable: contenido pendiente, balance, polish y defectos no bloqueantes documentados.

Bloquea: sistemas esenciales ausentes, crashes, perdida de progreso, incompatibilidad frecuente o ausencia de recorrido completo.

### Content Complete - M52.0

Debe validar: todo el contenido de lanzamiento aprobado integrado, referenciable, recorrible y compatible con persistencia.

Evidencia: inventario contra plan de contenido, validators, smoke tests y cobertura visual/sonora/UI.

Deuda aceptable: correcciones, balance y polish; no contenido nuevo fuera del plan.

Bloquea: contenido requerido ausente, placeholders criticos, referencias rotas o contenido que invalida budgets.

### Beta - M53.0

Debe validar: feature/content lock, estabilidad, balance, accesibilidad y rendimiento sobre la matriz objetivo aprobada.

Evidencia: regresion completa, sesiones prolongadas, upgrade/recovery de saves y profiling representativo.

Deuda aceptable: defectos acotados sin impacto en datos, progreso o ruta critica.

Bloquea: cambios de schema no excepcionales, crashes, data loss, progression blockers o rendimiento sin caracterizar.

### Release Candidate - M54.0

Debe validar: build publicable, versionado, reproducible y recuperable, con soporte y rollback operativos.

Evidencia: artifact y commit exactos, checklist de release, save migration, instalacion/actualizacion aplicable y ensayo de rollback.

Deuda aceptable: known issues explicitamente aprobados para release.

Bloquea: cualquier cambio no reverificado, save incompatible, fallo critico, artifact irreproducible o rollback no disponible.

M55.0 ejecuta Launch despues de Release Candidate; no agrega un gate canonico nuevo.

## Carriles Transversales

| Carril | Obligacion continua | Gates donde debe demostrarse |
| --- | --- | --- |
| Datos y modding | IDs estables, schemas validados, referencias explicables y compatibilidad planificada. | Foundation Freeze, Content Pipeline Ready, Production Ready |
| Persistencia | Todo estado mutable nuevo declara identidad, serializacion, migration y round-trip o un `NOT APPLICABLE` justificado. | Persistence Ready y todos los gates jugables posteriores |
| UI y feedback | Cada decision y fallo central tiene feedback legible; debug UI no se confunde con produccion. | Todos los gates jugables, Vertical Slice Approved, Production Ready |
| QA y regresion | Escenarios reproducibles, invariantes, pruebas proporcionales y evidencia sin estados eternamente pendientes. | Todos los gates |
| Rendimiento | Baseline temprano y profiling sobre escenarios representativos; presupuestos solo despues de plataforma objetivo. | Content Pipeline Ready, Vertical Slice Approved, Production Ready, Beta |
| Herramientas de contenido | Autorado repetible, validators y diagnostics antes de escalar contenido. | Content Pipeline Ready en adelante |
| Accesibilidad | Inputs, lectura y alternativas se definen al madurar cada superficie, no al final. | Vertical Slice Approved, Production Ready, Beta |
| Arte y audio | Barra representativa acotada en slice y pipeline escalable posterior. | Vertical Slice Approved, Production Ready, Content Complete |
| Documentacion | Autoridades sin duplicacion, decisiones trazables y log append-only. | Todos los gates |
| Git y versionado | Commits revisables, evidencia exacta y releases reproducibles. | Todos los gates; obligatorio en Release Candidate |

## Matriz Gate A Riesgos

`Debe cerrar` exige estado `CLOSED` antes de aprobar el gate. `Debe revisar` admite `CLOSED` con evidencia vigente o, para un riesgo estructural abierto, `MITIGATING`/`ACCEPTED` con mitigacion, responsable y proxima revision explicitos.

| Gate | Debe cerrar | Debe revisar |
| --- | --- | --- |
| Foundation Freeze | Ninguno especifico del registro actual | R01, R03, R05, R14, R16, R17, R18, R19, R20, R22 |
| Persistence Ready | R05, R14, R19 | R01, R09, R16, R17, R18, R20, R22 |
| Combat Ready | Ninguno especifico del registro actual | R01, R10, R11, R15, R16, R17, R18, R22 |
| AI Ready | R06 | R01, R10, R11, R15, R16, R17, R22 |
| World Systems Ready | Ninguno especifico del registro actual | R01, R10, R11, R15, R16, R17, R22 |
| Survival Systems Ready | R10 | R01, R11, R15, R16, R17, R22 |
| Content Pipeline Ready | R08 | R01, R02, R09, R11, R13, R16, R17, R20, R21, R22 |
| Vertical Slice Approved | R15, R18 | R01, R02, R04, R09, R10, R11, R12, R13, R16, R17, R19, R20, R21, R22, R23 |
| Production Ready | R04, R07, R09, R12, R13, R20, R21 | R01, R02, R10, R11, R16, R17, R19, R22, R23 |
| Alpha | Ninguno adicional | R01, R02, R10, R11, R16, R17, R19, R20, R22, R23 |
| Content Complete | Ninguno adicional | R01, R09, R11, R12, R13, R16, R17, R19, R20, R21, R22, R23 |
| Beta | R11 | R01, R02, R10, R16, R17, R19, R20, R22, R23 |
| Release Candidate | R16, R23 | R01, R02, R05, R11, R17, R19, R20, R21, R22 |

## Registro De Riesgos

Estados permitidos: `OPEN`, `MITIGATING`, `ACCEPTED` y `CLOSED`. Un riesgo estructural puede permanecer `ACCEPTED` solo con mitigacion y revision explicitas.

| ID | Estado | Responsable | Riesgo | Probabilidad | Impacto | Sintoma temprano | Mitigacion | Cierre o revision |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R01 | `MITIGATING` | Mauro | Alcance excesivo | Alta | Alta | El milestone crece durante implementacion. | Fijar fuera de alcance y gate antes de codigo. | Riesgo estructural; revisar cada gate. |
| R02 | `OPEN` | Mauro | Desarrollo individual | Alta | Alta | Tooling y validacion se vuelven cuellos de botella. | Reducir amplitud y automatizar tareas repetibles. | Revisar Content Pipeline y Production Ready; aceptacion requiere decision explicita. |
| R03 | `MITIGATING` | Mauro | Sobreingenieria | Media | Alta | Aparecen abstracciones sin consumidor actual. | Exigir necesidad actual, contrato minimo y consumidor demostrado. | Riesgo estructural permanente; revisar en cada milestone arquitectonico y cada gate, especialmente M36.1, M37, M41, M45 y M50. Foundation Freeze revisa su mitigacion local, no lo cierra globalmente. |
| R04 | `MITIGATING` | Mauro | Deuda OnGUI | Alta | Media | UI debug condiciona backends nuevos. | Congelar ampliaciones y reemplazar en M48.0. | Cerrar en Production Ready; aceptacion de deuda requiere decision explicita. |
| R05 | `MITIGATING` | Mauro | Persistencia introducida tarde | Media | Alta | Sistemas nuevos carecen de identidad durable. | M36.1 corto y M37 inmediato. | Cerrar en Persistence Ready; revalidar en releases. |
| R06 | `OPEN` | Mauro | IA demasiado compleja | Media | Alta | Multiples capas antes de un encuentro funcional. | Limitar M41.1 a avoid/alert/flee/fight. | Cerrar en AI Ready. |
| R07 | `MITIGATING` | Mauro | Proceduralidad prematura | Media | Alta | Generacion antes de sectorizacion y tools. | Bloquearla hasta M47.0. | Cerrar al autorizar el alcance acotado de M47.0; reconfirmar en Production Ready. |
| R08 | `OPEN` | Mauro | Falta de herramientas de contenido | Alta | Alta | Cada contenido exige edicion manual fragil. | Validators, catalogos e inspector. | Cerrar en Content Pipeline Ready. |
| R09 | `OPEN` | Mauro | Explosion de datos | Media | Alta | Duplicacion, IDs rotos y schemas divergentes. | Catalogos, validacion y compatibilidad. | Cerrar en Production Ready. |
| R10 | `OPEN` | Mauro | Supervivencia frustrante | Media | Alta | Castigos sin informacion o decision. | Feedback explicable y playtests integrados. | Cerrar en Survival Systems Ready; revisar Alpha/Beta. |
| R11 | `OPEN` | Mauro | Rendimiento tardio | Media | Alta | Ningun escenario representativo tiene baseline. | Aprobar budgets y perfilar por etapa. | Revisar en gates jugables y desde Content Pipeline; cerrar en Beta. |
| R12 | `OPEN` | Mauro | Animacion insuficiente | Alta | Media | Estados jugables no se leen visualmente. | Pipeline y cobertura priorizada. | Cerrar en Production Ready. |
| R13 | `OPEN` | Mauro | Pipeline artistico ausente | Alta | Alta | Assets one-off y reprocesado continuo. | Convenciones, import y budgets repetibles. | Cerrar en Production Ready. |
| R14 | `MITIGATING` | Mauro | Cambios tardios de arquitectura | Media | Alta | Save y nuevos sistemas fuerzan IDs nuevos. | Freeze de contratos y migrations explicitas. | Cerrar en Persistence Ready. |
| R15 | `MITIGATING` | Mauro | Sistemas sin loop integrado | Alta | Alta | Features aisladas sin decisiones compartidas. | Regla transversal de sistemas conectados. | Cerrar en Vertical Slice Approved. |
| R16 | `MITIGATING` | Mauro | Milestones siempre pendientes | Alta | Alta | Se acumula `PENDING UNITY VALIDATION`. | Definir escenario/evidencia antes de implementar. | Revisar cada gate; cerrar en Release Candidate. |
| R17 | `MITIGATING` | Mauro | Drift entre GDD, Roadmap y mirrors | Alta | Alta | Pilares o alcance difieren entre documentos. | Jerarquia explicita y revision cruzada. | Mitigar en M36.0 Checkpoint B; revisar cada gate. |
| R18 | `MITIGATING` | Mauro | Atomicidad cross-actor incompleta | Media | Alta | Transferencias universales duplican, pierden o reasignan items. | Reutilizar identidad, ownership, preview/commit/rollback y no reactivar M35.2.3.1 antes de sus dependencias. | Revisar Foundation/Persistence; cerrar antes de Vertical Slice Approved. |
| R19 | `OPEN` | Mauro | Corrupcion o recovery insuficiente de saves | Media | Alta | Carga parcial, perdida silenciosa o save irrecuperable. | Envelope versionado, integridad, escritura atomica, recovery, migrations y pruebas de fallo. | Cerrar en Persistence Ready; revalidar en releases. |
| R20 | `OPEN` | Mauro | Incompatibilidad entre save, datos y mods | Media | Alta | Un cambio de definicion rompe referencias o altera estado cargado. | Politica de compatibilidad, manifests/versiones, migrations y matriz de upgrades. | Revisar desde Persistence; cerrar en Production Ready y revalidar en releases. |
| R21 | `OPEN` | Mauro | Referencias o assets sin derechos verificables | Media | Alta | Moodboards, marcas o material de autor desconocido entran en build, deck, store o trailer. | Ledger de derechos, cuarentena interna y reemplazo/licencia antes de cualquier uso externo. | Revisar desde Content Pipeline; cerrar antes de material publico y, como maximo, en Production Ready. |
| R22 | `MITIGATING` | Mauro | Trabajo asistido por IA no comprendido o documentacion falsa | Media | Alta | Nadie puede explicar el cambio, la documentacion contradice el repo o un prompt reescribe contratos validados. | Tareas pequeñas, revision humana, evidencia del repo, pruebas e integracion por contratos existentes. | Riesgo estructural; revisar en cada gate. |
| R23 | `OPEN` | Mauro | Claims comerciales superan la evidencia jugable | Media | Alta | Store, deck o trailer prometen canon o features futuras no demostradas. | Claim gate, material propio/licenciado y comunicacion ligada a evidencia representativa. | Revisar Vertical Slice/Production; cerrar en Release Candidate. |

## Plantilla De Revision De Gate

- Gate:
- Milestone de cierre:
- Commits:
- Dependencias validadas:
- Escenario y resultado:
- Evidencia estatica:
- Evidencia automatizada:
- Evidencia manual:
- Persistencia:
- Rendimiento:
- Deuda aceptada:
- Deuda bloqueante:
- Responsables de riesgos:
- Riesgos que el gate debia cerrar:
- Riesgos estructurales aceptados para revision:
- Decision de Mauro:
