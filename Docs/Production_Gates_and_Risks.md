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

Estado: `APPROVED`.

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

Evidencia acumulada de Checkpoints A/B: Checkpoint A validado y cerrado congela identidad durable de items, `CreateNew`/`Rehydrate`, hydration detached, unicidad activa, item-owned storage, stacking, split/merge, rollback y ownership estricto; Mauro confirmo manualmente los flujos del slice sin duplicaciones ni ownership exceptions. Checkpoint B implementa `CreateAuthored`, dos authored world item IDs exactos y `PersistentSceneObjectId` para 14 roots stateful (3 actores, 3 puertas y 8 contenedores), con visuales, children y `Debug Strange Machine` excluidos. Runtime/Editor compilaron; Foundation Identity paso despues de aplicar, reabrir y reaplicar idempotentemente la escena, y Checkpoint A volvio a dar `PASS`. Mauro valido manualmente crowbar y Lee-Enfield authored, pickup, equip directo desde mundo, inventario y drop sin errores funcionales nuevos observados. `Diagnostic Console Observability Pass 1` completo failures accionables, rollback diagnosticable y reduccion del spam de `InteractionSystem`; ambos diagnosticos permanecieron en `PASS`.

Decision: `Foundation Freeze — APPROVED`. Identidad durable/authored, ownership/rollback, granularidad representativa de stacks, `Condition` exacto y rutas separadas `CreateNew`/`CreateAuthored`/`Rehydrate` quedan congeladas para M37. No se implemento persistencia prematuramente. R03 permanece `MITIGATING` como riesgo estructural y no bloquea M37.0.

Deuda aceptada para milestones posteriores: save/load, condition mutable, repair/disassembly, actor lifecycle, gameplay nuevo y UI final.

Bloquea: IDs regenerables o ambiguos, ownership sin autoridad, mutaciones que eluden servicios existentes o cualquier contrato que M37 deba inferir.

### Persistence Ready - M37.1

Estado: `APPROVED — CURRENT SLICE PERSISTENCE VALIDATED`.

Debe validar:

- envelope y version de save;
- escritura atomica, recovery y politica de migracion o rechazo;
- round-trip del slice actual;
- jugador y estado actual de sus componentes dentro del round-trip;
- conservacion de `InstanceId`, cantidades, placements, Equipment, ownership, item-owned storage, containers, cuerpos, puertas, world items y runtime tags;
- fallo de carga seguro y explicable.

Evidencia: snapshots antes/despues, escenarios de round-trip, recovery y versionado, checks automatizados aplicables y validacion manual sin errores relacionados en Console.

Evidencia de infraestructura M37.0: `DONE — PERSISTENCE CORE VALIDATED`. Envelope V1, serializer Newtonsoft aislado, slots cerrados, temp/primary/backup, overwrite mediante `File.Replace`, fallback preservando backup, recovery, future-version rejection, migration seam y failure codes quedaron implementados. Runtime/Editor compilaron y `M37.0 Persistence Core Diagnostics: PASS` cubrio once escenarios en un root temporal sin residuos ni acceso a saves reales.

Evidencia incremental M37.1 Pass 1: `Snapshot Contract & Semantic Preflight Pass 1` captura el slice real en una tabla única de items y DTOs referenciales, valida identidad/localización/quantity/placements/Equipment/owned storage/world state sin mutar, guarda/lee mediante M37.0 y compara canónicamente. Runtime/Editor, M37.0, Foundation Identity y `M37.1 Snapshot & Semantic Preflight Diagnostics` dieron `PASS`; el diagnóstico temporal no guardó `SampleScene`.

Evidencia incremental M37.1 Pass 2: `Transactional Rehydration & Real-Scene Round-Trip Pass 2` implementa resolución previa, snapshot de rollback, teardown selectivo sin resets globales, rehydration exacta, restore de storages/Equipment/ownership, containers autoritativos, corpses actuales, authored/runtime world state, doors, health/needs y pose del player. `M37.1 Current Slice Persistent Round-Trip Diagnostics: PASS` demostró A → B → load A → C equivalente y el fault post-storage demostró `ApplyFailed` con rollback equivalente. Compile, M37.0, Checkpoint A, Foundation Identity y snapshot/preflight permanecieron en `PASS`; `SampleScene` conservó su hash.

Validación manual aprobatoria: Mauro ejecutó `Save Debug Slot`, salió completamente de Play Mode, entró a un bootstrap fresh-session y ejecutó `Load Debug Slot` con `Success`. El save/load informó 23 items, 11 storages, 3 world items, 8 containers, 0 corpses y 3 doors; el load tuvo `MutationStarted: True`, sin rollback requerido. La verificación visual confirmó Inventory, Equipment, item-owned storage, containers y estado esperado sin duplicados, pérdidas, fallos de ownership, rehydration o persistence.

Alcance aprobado: player pose, health/needs representados, identidad/DefinitionId/Condition de items, stacks, placements, Inventory, Equipment, ownership, item-owned storage, containers, corpse surfaces actuales, doors, authored/runtime world items y runtime mutable state de M37.1. Deuda aceptable: cloud save, lifecycle/serialización general de actores vivos, transform durable de NPCs, alive/dead entre fresh sessions, spawn/despawn runtime, AI, clima, facciones o proceduralidad. No se difieren el jugador ni los cuerpos actuales.

Bloquea: perdida o duplicacion, referencias colgantes, reset silencioso, saves sin version o recovery no demostrado.

### M38.0 Manual Actor Lifecycle Closeout

Estado: `DONE — ACTOR RUNTIME & LIFECYCLE VALIDATED`.

Validación requerida para M38.0:

- actor authored Alive guardado/cargado en fresh session con mismo ActorInstanceId, profile, pose, Inventory y Equipment;
- bootstrap authored Alive visible antes de cargar un save Dead y reemplazo por el mismo actor/corpse Dead, health 0 y lootable, sin doble representación;
- runtime actor spawn/save/fresh-session/load con mismo ActorInstanceId y pose;
- Console sin errores de actor lifecycle, identity, ownership o persistence.

Evidencia manual confirmada por Mauro: authored actor Alive → Save → fresh Play session → Load → Alive restaurado; actor Dead → corpse lootable → Save Dead; fresh Play session con bootstrap Alive antes de Load; Load reemplazó ese bootstrap por Dead persistido; corpse con Inventory y Equipment; sin actor vivo + corpse duplicado y sin errores de lifecycle, ownership o persistence. Los warnings visibles fueron los legacy Content ID conocidos y aceptados.

Evidencia automatizada: Runtime/Editor compile, M36.1 Foundation Identity, Global Content ID Namespace Foundation, M37.0, M37.1 Snapshot/Preflight, M37.1 Current Slice Round-Trip y `M38.0 Actor Runtime & Lifecycle Diagnostics` dieron `PASS`. El diagnóstico M38 usó dos Play sessions, cubrió Alive/Dead, runtime spawn/restore, unicidad, selectividad y fault post-reconciliation con rollback equivalente. `SampleScene` conservó SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`; sólo permanecen seis warnings preexistentes.

Deuda aceptada: representation visual genérica es cápsula lógica sin rig humano; authored identity usa fallback hash versionado hasta materializar overrides serializados; no existe world streaming, permanent despawn, AI ni combat. `Persistence Ready` queda `APPROVED` y M37 no se reabre.

No quedan bloqueos de closeout. M38.1 fue autorizado e implementado con validación automatizada; su cierre manual se controla por separado a continuación.

### M38.1 Needs, World Clock & Recovery Closeout

Estado: `DONE — WORLD TIME / NEEDS / RECOVERY VALIDATED`.

Evidencia manual confirmada por Mauro:

- World Clock visible y progresando; bootstrap Day 1; Day/HH:MM y Hunger/Thirst avanzaron con el mismo tiempo del mundo;
- Rest 1h y Sleep 8h avanzaron el reloj y las necesidades coherentemente sin curar ni revivir;
- comida y agua restauraron Hunger/Thirst por las rutas existentes;
- Save Current Slice terminó `Success` con `ElapsedGameSeconds: 100052.668084139`;
- fresh Play session volvió al bootstrap y Load Current Slice terminó `Success`, restaurando World Clock y needs;
- tras el load el reloj y las necesidades continuaron progresando normalmente;
- no se observaron errores runtime atribuibles a M38.1.

Evidencia automatizada: Runtime/Editor compile, Global Content ID Namespace Foundation, M36.1 Foundation Identity, M37.0, ambos M37.1, M38.0 e Inventory Interaction UX Correction dieron `PASS`. `M38.1 Needs, World Clock & Recovery Diagnostics: PASS` cubrió bootstrap/derivación temporal, avance único de Hunger/Thirst, reactivación, consumibles, rest/sleep, rechazos inválidos/disabled/Dead, round-trip fresh-session, compatibilidad legacy, preflight sin mutación y rollback post-runtime-state equivalente. `SampleScene` conservó SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`; sólo permanecen seis warnings preexistentes.

Contrato acotado: el reloj guarda segundos de juego desde Day 1 00:00 y avanza a 60 segundos de juego por segundo real; la configuración existente equivale a 1.8 Hunger y 3.0 Thirst por hora de juego. Rest/Sleep no curan health ni wounds. Fatigue permanece `SHOULD — DEFERRED`: no hay todavía definición ni consumidor jugable aprobado que justifique una barra aislada.

M39.0 fue implementado después de este closeout y su gate manual se controla a continuación. Esto no adelanta AI, combat, weather/exposure, world streaming, UI final ni playable exploration prototype.

### M39.0 Localized Health & Medicine Closeout

Estado: `DONE — LOCALIZED HEALTH / MEDICINE VALIDATED`.

Validation: `AUTOMATED + MANUAL FRESH-SESSION PASSED`.

Evidencia manual confirmada por Mauro:

- H abrió la ventana Salud y mostró las seis regiones; una región sana presentó `Se ve bien`;
- una laceración severa debug apareció sólo en Brazo Izq. con gravedad, pain, bleeding y treatment, y el estado general cambió a `Injured`;
- el bleeding redujo la reserva vital; Rest 1h y Sleep 8h no curaron la herida;
- aplicar una venda consumió exactamente x1, mantuvo la herida durable, cambió su estado a vendada y redujo/controló el sangrado;
- Save Current Slice, salida completa de Play Mode, fresh Play y Load restauraron la misma región, herida durable, estado vendado, pain, bleeding y reserva vital;
- el load final informó 26 items, 15 storages, 2 world items, 8 containers, 2 actors, 0 legacy corpses y 3 doors; terminó `Success`, con `MutationStarted: True`, `RollbackAttempted: False` y `RollbackSucceeded: False`, correcto para un load exitoso;
- no se observaron errores runtime atribuibles a M39.0. Los warnings legacy de Global Content ID y EquipmentSlot son deuda Core-only conocida y aceptada.

Evidencia automatizada: Runtime/Editor compile y las suites M36.1, M37.0, ambos M37.1, M38.0, M38.1, Player Controls & Health Window e Inventory Interaction UX dieron `PASS`. `M39.0 Localized Health & Medicine Diagnostics: PASS` cubrió dos Play sessions, actor runtime herido/muerto, legacy V1, preflight sin mutación y rollback post-medical-state equivalente. `SampleScene` conservó SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`; no hubo warnings nuevos.

Contratos cerrados: seis regiones humanas V1; WoundId durable; tipos `Laceration/Puncture/Blunt`; severity acotada; bleeding y Rest/Sleep sobre el mismo `WorldClock`; pain derivado; tratamiento localizado y venda data-driven; consumo exactamente x1; vendaje sin eliminar la herida ni ejecutar `Heal(+X)`; `ActorMedicalStateComponent` como autoridad de wounds/bleeding/pain/treatment; `ActorHealthComponent` como bridge de vitalidad/lifecycle; muerte coherente por agotamiento vital; persistencia de player y actors; strict preflight; rollback transaccional; compatibilidad V1 sin `medicalState`; ventana H cualitativa; exclusividad Health/Inventory y contratos WASD/camera preservados.

Deuda aceptada no bloqueante: revisar mediante balancing la relación severity/bleeding rate/tiempo hasta deterioro crítico o muerte, porque una laceración severa puede tardar demasiado en producir pérdida vital grave. No es un fallo arquitectónico, no rompe persistence y no modifica valores en este closeout.

Fuera de alcance de ese closeout M39: combat resolution, ballistics, armor, penetration, infection, fractures, surgery, organs, blood types, transfusions, antibiotics, complex analgesics, regional movement penalties, limb disability y AI. El estado vigente de M40.0 se controla a continuación.

### M40.0 Combat Resolution & Weapons — Validado

Estado: `DONE — COMBAT RESOLUTION & WEAPONS V1 VALIDATED`.

Validation: `AUTOMATED + MANUAL FRESH-SESSION PASSED`.

Evidencia automatizada: Runtime/Editor compile, Global Content ID Namespace Foundation, M36.1, M37.0, ambos M37.1, M38.0, M38.1, M39.0, Player Controls & Health Window e Inventory Interaction UX dieron `PASS`. `M40.0 Combat Resolution & Weapons Diagnostics: PASS` cubrió dos Play sessions, seis regiones, melee/range, reload exacto y cancelación, fire/miss/dry-fire/cycle, bleeding a Dead/corpse, drop/pickup, Equipment, round-trip fresh-session, legacy unloaded, preflight sin mutación, rollback post-firearm-state equivalente y near-cover Correction Pass 1. `SampleScene` conservó SHA-256 `25810B64A01437969F000D93EC5E0153837CD7C33EB61CD63D3F1C5D7E438335`; no hubo warnings nuevos.

Evidencia manual confirmada por Mauro:

- Lee-Enfield equipable; F/LMB/R; unloaded; reload completo/parcial; capacity 10; ammo compatible y loaded rounds consumidos exactamente; bolt cycle; impactos regionales `Puncture`; world blocking; continuidad Dead/corpse.
- Correction Pass 1: una pared inmediata bloqueó el disparo y evitó la wound del actor detrás; con línea limpia el actor volvió a recibir impacto.
- Crowbar equipable; melee temporizado; heridas `Blunt` observadas en LeftArm, Torso, RightLeg y RightArm; out-of-range `Melee attack missed`; geometría interpuesta; cancelación por WASD; sin ruta médica paralela.
- Drop/pickup preservó `Loaded 8/10` y `InstanceId: item_c0f66d58249e4892aa4632028975816e`, confirmando que el estado durable sigue al `ItemInstance`.
- Current Slice save `Success`, salida completa de Play, fresh Play y load `Phase: Complete`, `FailureCode: Success`, `Result: Success`; Lee-Enfield equipado restaurado en `Loaded 8/10`.
- No aparecieron errores nuevos atribuibles a M40. Los warnings legacy `core:*` siguen como deuda aceptada.

Riesgos/fuera de M40.0: balance final, spread/critical hits, proyectiles físicos, animación/audio final, condition, armor/penetration, dual wield, attachments, UI final y AI combat. El tuning severity/bleeding M39 y la compatibilidad legacy Core Content IDs permanecen como deuda no bloqueante. El estado vigente de armor/penetration y del gate se controla a continuación.

### Combat Ready - M40.1

Estado: `PENDING — MANUAL M40.1 CLOSEOUT`.

M40.1 está `IMPLEMENTED — AUTOMATED ARMOR / PENETRATION VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`. La automatización demostró un único pipeline M40 → armor/world penetration → adapter médico M39, ammo y rounds exactos, Equipment como autoridad, seis regiones, stop/penetration/trauma residual explicables, superficies world acotadas, melee protegido, death/corpse, round-trip fresh-session, compatibilidad V1 y datos inválidos sin mutación. `SampleScene` permaneció unchanged y no aparecieron warnings nuevos atribuibles.

El núcleo de comparación/residual es independiente del receptor y común a wearable armor y world surfaces. Toda munición de proyectil usa `penetration_power > 0` sin flags AP/HP/FMJ; Condition conserva únicamente un seam futuro sin lectura, mutación ni desgaste. El fixture manual permite ciclar dos capas `Stopped`, una capa `Penetrated` y armor sólo en inventory `Unarmored` usando una única definición de armor y la `.303` existente.

Debe validar:

- contrato unico de damage integrado con salud localizada;
- melee, firearms, ammo y reload;
- armor y penetration con resultados explicables;
- feedback suficiente para comprender impacto y proteccion;
- seams de integracion futura con condition sin implementar desgaste prematuramente;
- escenarios debug reproducibles.

Evidencia: matriz de combate con y sin proteccion, trazas del resolver, estado antes/despues y validacion manual.

Evidencia todavía pendiente para aprobar el gate: recheck manual de cobertura/equipped-only, stopped sin `Puncture`, penetrated con una única `Puncture`, melee, near-cover, save → salida total de Play → fresh Play → load, protección post-load e inexistencia de errores M40.1 en Console.

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
| R05 | `CLOSED` | Mauro | Persistencia introducida tarde | Media | Alta | Sistemas nuevos carecen de identidad durable. | M36.1 y M37.1 validaron identidad y Current Slice persistence. | Cerrado en Persistence Ready; revalidar en releases. |
| R06 | `OPEN` | Mauro | IA demasiado compleja | Media | Alta | Multiples capas antes de un encuentro funcional. | Limitar M41.1 a avoid/alert/flee/fight. | Cerrar en AI Ready. |
| R07 | `MITIGATING` | Mauro | Proceduralidad prematura | Media | Alta | Generacion antes de sectorizacion y tools. | Bloquearla hasta M47.0. | Cerrar al autorizar el alcance acotado de M47.0; reconfirmar en Production Ready. |
| R08 | `OPEN` | Mauro | Falta de herramientas de contenido | Alta | Alta | Cada contenido exige edicion manual fragil. | Validators, catalogos e inspector. | Cerrar en Content Pipeline Ready. |
| R09 | `OPEN` | Mauro | Explosion de datos | Media | Alta | Duplicacion, IDs rotos y schemas divergentes. | Catalogos, validacion y compatibilidad. | Cerrar en Production Ready. |
| R10 | `OPEN` | Mauro | Supervivencia frustrante | Media | Alta | Castigos sin informacion o decision. | Feedback explicable y playtests integrados. | Cerrar en Survival Systems Ready; revisar Alpha/Beta. |
| R11 | `OPEN` | Mauro | Rendimiento tardio | Media | Alta | Ningun escenario representativo tiene baseline. | Aprobar budgets y perfilar por etapa. | Revisar en gates jugables y desde Content Pipeline; cerrar en Beta. |
| R12 | `OPEN` | Mauro | Animacion insuficiente | Alta | Media | Estados jugables no se leen visualmente. | Pipeline y cobertura priorizada. | Cerrar en Production Ready. |
| R13 | `OPEN` | Mauro | Pipeline artistico ausente | Alta | Alta | Assets one-off y reprocesado continuo. | Convenciones, import y budgets repetibles. | Cerrar en Production Ready. |
| R14 | `CLOSED` | Mauro | Cambios tardios de arquitectura | Media | Alta | Save y nuevos sistemas fuerzan IDs nuevos. | Foundation Freeze, Content IDs y M37.1 validados. | Cerrado en Persistence Ready. |
| R15 | `MITIGATING` | Mauro | Sistemas sin loop integrado | Alta | Alta | Features aisladas sin decisiones compartidas. | Regla transversal de sistemas conectados. | Cerrar en Vertical Slice Approved. |
| R16 | `MITIGATING` | Mauro | Milestones siempre pendientes | Alta | Alta | Se acumula `PENDING UNITY VALIDATION`. | Definir escenario/evidencia antes de implementar. | Revisar cada gate; cerrar en Release Candidate. |
| R17 | `MITIGATING` | Mauro | Drift entre GDD, Roadmap y mirrors | Alta | Alta | Pilares o alcance difieren entre documentos. | Jerarquia explicita y revision cruzada. | Mitigar en M36.0 Checkpoint B; revisar cada gate. |
| R18 | `MITIGATING` | Mauro | Atomicidad cross-actor incompleta | Media | Alta | Transferencias universales duplican, pierden o reasignan items. | Reutilizar identidad, ownership, preview/commit/rollback y no reactivar M35.2.3.1 antes de sus dependencias. | Revisar Foundation/Persistence; cerrar antes de Vertical Slice Approved. |
| R19 | `CLOSED` | Mauro | Corrupcion o recovery insuficiente de saves | Media | Alta | Carga parcial, perdida silenciosa o save irrecuperable. | Envelope versionado, integridad, escritura atomica, recovery, migrations y pruebas de fallo. | Cerrado en Persistence Ready; revalidar en releases. |
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
