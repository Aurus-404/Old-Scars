# Old Scars — Issue Registry

Este documento es el registro persistente de problemas conocidos, sospechas técnicas y correcciones verificadas del proyecto. No sustituye al roadmap ni al Development Log: sirve para que bugs, deudas y problemas observados no desaparezcan entre pruebas, prompts o sesiones de desarrollo.

## Regla de uso

- Todo problema nuevo detectado durante una prueba, implementación, revisión de repo o diagnóstico debe registrarse aquí si puede requerir acción futura.
- Todo prompt de Codex que pueda descubrir problemas debe indicar que los registre aquí en vez de confiar en memoria temporal.
- Si un problema está fuera del alcance de la tarea actual, se registra pero no se arregla por inercia.
- Los issues `RESOLVED` nunca se borran: conservan el historial, commit y validación de la corrección.
- `SUSPECTED` significa que existe evidencia o un síntoma plausible, pero la causa o incluso la existencia exacta del defecto todavía requiere confirmación.
- `CONFIRMED` significa que el problema o deuda fue observado de forma reproducible o está demostrado por código/arquitectura.
- `RESOLVED` significa que hubo una corrección identificable y una validación proporcional al problema.

## Severidad

- `P0 / RED`: rompe un contrato central, bloquea pruebas fiables, provoca comportamiento sistémicamente incorrecto o puede invalidar trabajo posterior.
- `P1 / ORANGE`: defecto importante de gameplay, IA, combate, percepción, tooling o integración que debe corregirse antes de cerrar el bloque correspondiente.
- `P2 / YELLOW`: deuda, ergonomía, observabilidad o inconsistencia no bloqueante que conviene resolver pero no impide por sí sola continuar.

## Tipos

`BUG` · `DESIGN_DEBT` · `TOOLING` · `DOCS`

## Campos obligatorios por issue

Cada entrada debe conservar, cuando exista información suficiente:

- ID
- Nombre
- Tipo
- Estado
- Severidad
- Fecha de descubrimiento
- Prueba / origen
- Momento de descubrimiento
- Síntoma observado
- Evidencia
- Causa confirmada o hipótesis
- Sistemas afectados
- Solución prevista
- Commit de corrección
- Validación
- Notas

---

## ISSUE-0001 — Blue/Red no realizan roaming efectivo mientras están Idle

- **Tipo:** `BUG`
- **Estado:** `CONFIRMED`
- **Severidad:** `P0 / RED`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 manual integrada
- **Momento de descubrimiento:** observación previa al combate y durante períodos sin target
- **Síntoma observado:** el NPC random White se desplaza de forma ambiental, mientras Blue y Red permanecen quietos y sólo comienzan a moverse cuando adquieren un target.
- **Evidencia:** observación manual repetida; contradice el comportamiento esperado tras la corrección que pretendía componer roaming para White/Blue/Red.
- **Causa confirmada o hipótesis:** pendiente de auditoría. Debe contrastarse `SandboxActorRoamingController`, `HumanEncounterAIController`, threat acquisition y ownership de `ActorNavigationController`.
- **Sistemas afectados:** sandbox NPC, ambient behavior, encounter AI, navigation.
- **Solución prevista:** Fases 1–2 de `NPC_AI_Sanitation_Plan.md`: auditar writers/owners y dejar un único dueño de navegación por frame; todos los humanos deben compartir Ambient roaming cuando estén realmente Idle.
- **Commit de corrección:** pendiente.
- **Validación:** debe demostrar desplazamiento real, no sólo órdenes aceptadas.
- **Notas:** White es referencia observable del comportamiento ambiental deseado, no una IA separada a copiar ciegamente.

## ISSUE-0002 — El gate de roaming puede aceptar órdenes sin demostrar desplazamiento real

- **Tipo:** `BUG`
- **Estado:** `CONFIRMED`
- **Severidad:** `P0 / RED`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** revisión del diagnóstico posterior a Prueba 2
- **Momento de descubrimiento:** contraste entre diagnóstico automatizado y comportamiento visible
- **Síntoma observado:** una señal como `AcceptedOrderCount > 0` puede pasar aunque Blue/Red continúen visualmente inmóviles.
- **Evidencia:** el comportamiento manual contradice la garantía que parecía ofrecer el diagnóstico.
- **Causa confirmada o hipótesis:** el test valida una señal indirecta en vez del resultado observable.
- **Sistemas afectados:** diagnostics/QA de AI y navigation.
- **Solución prevista:** validar posición inicial/final, distancia recorrida real, cambios de destino y límites de home radius.
- **Commit de corrección:** pendiente.
- **Validación:** White/Blue/Red deben recorrer una distancia mínima verificable durante una ventana sin amenaza.
- **Notas:** principio general: los gates importantes validan contratos observables, no sólo que una función haya sido llamada.

## ISSUE-0003 — Posible competencia Ambient/Encounter sobre Navigation

- **Tipo:** `DESIGN_DEBT`
- **Estado:** `SUSPECTED`
- **Severidad:** `P0 / RED`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** análisis arquitectónico posterior a Prueba 2
- **Momento de descubrimiento:** investigación conceptual del roaming Blue/Red
- **Síntoma observado:** el patrón White-mueve / Blue-Red-no-mueven sugiere que composición, resets o ownership entre roaming, encounter y acquisition pueden estar compitiendo o anulándose.
- **Evidencia:** síntoma de ISSUE-0001; la causa exacta aún no está confirmada.
- **Causa confirmada o hipótesis:** múltiples controladores podrían escribir/invalidar órdenes sobre `ActorNavigationController` o el estado AI.
- **Sistemas afectados:** roaming, encounter AI, threat acquisition, navigation.
- **Solución prevista:** Fase 1: mapa completo de writers, estados y transiciones. Fase 2: una única decisión de alto nivel controla movimiento en cada frame.
- **Commit de corrección:** pendiente.
- **Validación:** debe poder responder inequívocamente quién posee navegación en Ambient, Encounter, Search e Inactive.
- **Notas:** si la solución exige muchas banderas de ownership/parches, considerar reemplazar la capa de decisión en vez de conservarla por inercia.

## ISSUE-0004 — La percepción inicial depende demasiado de la orientación corporal de spawn

- **Tipo:** `BUG`
- **Estado:** `CONFIRMED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 manual integrada
- **Momento de descubrimiento:** observación de NPCs recién spawneados
- **Síntoma observado:** un NPC que aparece orientado en dirección contraria puede no detectar actores cercanos porque permanece mirando al frente hasta que otra conducta lo hace moverse.
- **Evidencia:** observación manual.
- **Causa confirmada o hipótesis:** la percepción usa el facing/forward actual y no existe todavía una capa autónoma de gaze/attention/idle scanning.
- **Sistemas afectados:** perception, spawn presentation, AI attention.
- **Solución prevista:** Fases 3–5: orientación inicial razonable más `Gaze/Attention V1`; no resolver dando visión 360°.
- **Commit de corrección:** pendiente.
- **Validación:** un NPC Idle debe explorar visualmente su entorno con límites humanos sin necesitar movimiento locomotor.
- **Notas:** la orientación aleatoria de spawn no es por sí sola un bug; el defecto es quedar funcionalmente ciego por depender exclusivamente de ella.

## ISSUE-0005 — Falta una autoridad de Gaze/Attention humana

- **Tipo:** `DESIGN_DEBT`
- **Estado:** `CONFIRMED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 manual integrada
- **Momento de descubrimiento:** observación de NPC quieto
- **Síntoma observado:** el NPC se comporta como un tanque: si está quieto no gira cabeza/mirada ni inspecciona direcciones; su visión sólo cambia cuando cambia el cuerpo/movimiento.
- **Evidencia:** observación manual.
- **Causa confirmada o hipótesis:** no existe todavía un seam explícito de atención visual separado de locomotion/body facing.
- **Sistemas afectados:** perception, encounter AI, ambient behavior, presentation.
- **Solución prevista:** Fase 3: agregar una autoridad mínima de gaze que no duplique percepción ni haga raycasts propios.
- **Commit de corrección:** pendiente.
- **Validación:** estados Ambient/Candidate/Combat/LostContact deben orientar atención de forma observable y limitada.
- **Notas:** no introducir Behavior Trees/GOAP por esta necesidad.

## ISSUE-0006 — Tracking visual lateral deficiente

- **Tipo:** `BUG`
- **Estado:** `CONFIRMED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 manual integrada
- **Momento de descubrimiento:** jugador desplazándose lateralmente frente a NPC hostil
- **Síntoma observado:** al moverse el objetivo hacia un costado, el NPC puede perderlo y tardar en reencontrarlo en vez de tratar de mantenerlo dentro de la mirada.
- **Evidencia:** observación manual.
- **Causa confirmada o hipótesis:** atención/percepción centrada en el forward corporal sin tracking visual continuo.
- **Sistemas afectados:** gaze, perception, combat targeting.
- **Solución prevista:** Fase 4: seguimiento visual continuo y predicción corta basada en movimiento observado, con velocidad angular limitada.
- **Commit de corrección:** pendiente.
- **Validación:** un objetivo móvil visible debe ser seguido de forma continua; movimientos extremos aún pueden romper contacto.
- **Notas:** no confundir con intercepción balística completa.

## ISSUE-0007 — LostContact no ejecuta una búsqueda real

- **Tipo:** `BUG`
- **Estado:** `CONFIRMED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-08-31
- **Prueba / origen:** Prueba 1
- **Momento de descubrimiento:** pérdida de LOS durante encounter
- **Síntoma observado:** al perder contacto, la IA conserva el estado/memoria durante un timeout pero no investiga físicamente la última posición conocida.
- **Evidencia:** Prueba 1 y revisión del flujo LostContact.
- **Causa confirmada o hipótesis:** Search V1 aún no fue implementado.
- **Sistemas afectados:** encounter AI, navigation, perception memory.
- **Solución prevista:** Fase 6: LKP → navegar → inspección breve → reacquire o release → Ambient.
- **Commit de corrección:** pendiente.
- **Validación:** timeline observable `Fighting → LostContact → Searching → Reacquired` o `Released → Idle/Ambient`.
- **Notas:** no agregar cover, flanking, hearing, squad search ni room clearing en V1.

## ISSUE-0008 — Posible sesgo de impactos hacia piernas/pies

- **Tipo:** `BUG`
- **Estado:** `SUSPECTED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 manual integrada
- **Momento de descubrimiento:** revisión de heridas del jugador/NPCs después de intercambio de disparos
- **Síntoma observado:** los daños observados se concentraron en piernas/pies; el jugador también recibió impactos únicamente en pies durante la muestra observada.
- **Evidencia:** muestra manual limitada.
- **Causa confirmada o hipótesis:** puede provenir de aim point/spread, geometría de cápsula, selección de collider o algoritmo legacy de región. No asumir causa todavía.
- **Sistemas afectados:** physical shot path, hit geometry, body region resolution, combat diagnostics.
- **Solución prevista:** Fases 7–8: hitboxes anatómicos explícitos + instrumentación por impacto + tests deterministas y estadísticos.
- **Commit de corrección:** pendiente.
- **Validación:** todas las regiones deben ser alcanzables y la distribución real no debe mostrar un sesgo inexplicable.
- **Notas:** no modificar medicina/Vital Integrity para arreglar un problema de geometría/aim.

## ISSUE-0009 — La cápsula humana es insuficiente para validar hit testing anatómico

- **Tipo:** `DESIGN_DEBT`
- **Estado:** `CONFIRMED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 manual integrada
- **Momento de descubrimiento:** análisis de los impactos por región
- **Síntoma observado:** el actor de prueba no tiene geometría física diferenciada para cabeza, brazos, torso y piernas; la región se deriva de un collider simplificado.
- **Evidencia:** representación actual y limitación observada durante combate.
- **Causa confirmada o hipótesis:** la cápsula fue adecuada para la foundation, no para evaluación anatómica final.
- **Sistemas afectados:** actor presentation, combat hit testing, body region resolution.
- **Solución prevista:** Fase 7: usar modelo humano estático/bind pose o T-pose con hitboxes `Head/Torso/LeftArm/RightArm/LeftLeg/RightLeg`, conservando collider locomotor separado.
- **Commit de corrección:** pendiente.
- **Validación:** disparos dirigidos a cada collider deben resolver la región correcta.
- **Notas:** animaciones siguen fuera de alcance hasta que aporten valor a la prueba.

## ISSUE-0010 — Observabilidad global insuficiente para peleas multi-NPC

- **Tipo:** `TOOLING`
- **Estado:** `CONFIRMED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 / revisión de Pass D
- **Momento de descubrimiento:** seguimiento de varios NPCs simultáneamente
- **Síntoma observado:** la observabilidad detallada es buena para un NPC seleccionado, pero no permite entender de un vistazo gaze/FOV/target/navigation/search/shot traces de toda la pelea.
- **Evidencia:** experiencia de Prueba 2 con múltiples NPCs.
- **Causa confirmada o hipótesis:** Pass D priorizó inspección focal mediante selección F6.
- **Sistemas afectados:** development UI, world overlays, diagnostics.
- **Solución prevista:** Fase 10: overlay global por categorías más inspector detallado para un seleccionado.
- **Commit de corrección:** pendiente.
- **Validación:** una pelea multi-NPC debe ser comprensible sin ciclar constantemente selección.
- **Notas:** usar datos/queries de producción; no duplicar percepción o raycasts sólo para debug.

## ISSUE-0011 — El panel de observabilidad actual es demasiado dependiente de selección para pruebas integradas

- **Tipo:** `TOOLING`
- **Estado:** `CONFIRMED`
- **Severidad:** `P2 / YELLOW`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2
- **Momento de descubrimiento:** inspección manual prolongada
- **Síntoma observado:** para observar el sistema completo hay que seleccionar/ciclar actores, lo que dificulta comparar estados simultáneos.
- **Evidencia:** experiencia de prueba.
- **Causa confirmada o hipótesis:** tooling focal de Pass D.
- **Sistemas afectados:** development UI.
- **Solución prevista:** resolver junto con ISSUE-0010, manteniendo panel detallado pero separándolo del overlay global.
- **Commit de corrección:** pendiente.
- **Validación:** inspector y overlay deben complementarse en vez de competir.
- **Notas:** no convertirlo en production UI.

## ISSUE-0012 — Falta modo debug Invincible para pruebas de combate

- **Tipo:** `TOOLING`
- **Estado:** `CONFIRMED`
- **Severidad:** `P2 / YELLOW`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** planificación posterior a Prueba 2
- **Momento de descubrimiento:** necesidad de observar ataques sobre Player sin terminar la prueba por muerte
- **Síntoma observado:** no existe un toggle de desarrollo para dejar que los NPC detecten, disparen y causen consecuencias sin permitir la transición final a Dead.
- **Evidencia:** tooling actual.
- **Causa confirmada o hipótesis:** feature debug aún no implementada.
- **Sistemas afectados:** player debug, health/lifecycle test harness.
- **Solución prevista:** Fase 9: permitir flujo real de daño/heridas/trauma y bloquear únicamente muerte final en V1.
- **Commit de corrección:** pendiente.
- **Validación:** NPC sigue atacando y el pipeline real de daño continúa observable sin terminar la sesión por muerte.
- **Notas:** incapacidad/unconscious puede seguir ocurriendo en V1.

## ISSUE-0013 — Falta modo debug Invisible-to-AI

- **Tipo:** `TOOLING`
- **Estado:** `CONFIRMED`
- **Severidad:** `P2 / YELLOW`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** planificación posterior a Prueba 2
- **Momento de descubrimiento:** necesidad de observar NPC↔NPC de cerca sin que Player altere adquisición de amenazas
- **Síntoma observado:** el Player no puede excluirse limpiamente de percepción/acquisition manteniendo presencia física e interacción.
- **Evidencia:** tooling actual.
- **Causa confirmada o hipótesis:** feature debug aún no implementada.
- **Sistemas afectados:** player debug, AI candidate/acquisition boundary.
- **Solución prevista:** Fase 9: excluir al Player como candidato perceptivo/adquirible sin desactivar GameObject ni collider.
- **Commit de corrección:** pendiente.
- **Validación:** Player puede acercarse a la pelea sin generar recognition/targeting.
- **Notas:** no implementar invisibilidad visual/rendering si no es necesaria para el contrato de IA.

## ISSUE-0014 — Ping-pong Idle↔Inactive en actores incapacitados

- **Tipo:** `BUG`
- **Estado:** `RESOLVED`
- **Severidad:** `P0 / RED`
- **Fecha de descubrimiento:** 2026-08-31
- **Prueba / origen:** Prueba 1
- **Momento de descubrimiento:** actor vivo incapacitado durante combate
- **Síntoma observado:** transición repetida `Idle → Inactive → Idle → Inactive`.
- **Evidencia:** log `[AI][STATE]` y reproducción manual.
- **Causa confirmada o hipótesis:** `HumanEncounterAIController` entraba Inactive mientras ClearThreat/ResetEncounter del acquisition devolvía actor vivo a Idle.
- **Sistemas afectados:** encounter AI, threat acquisition, incapacitation.
- **Solución prevista:** ya aplicada: incapacidad queda estable Inactive y cancela adquisición/navigation/attack.
- **Commit de corrección:** `b42e17c40ad843244fd390c9b0eeb707b6462d31`.
- **Validación:** diagnóstico AI P0 enfocado pasó después de la corrección.
- **Notas:** conservar como regresión permanente.

## ISSUE-0015 — Blue→Red era Neutral en el baseline del sandbox

- **Tipo:** `BUG`
- **Estado:** `RESOLVED`
- **Severidad:** `P0 / RED`
- **Fecha de descubrimiento:** 2026-08-31
- **Prueba / origen:** Prueba 1
- **Momento de descubrimiento:** revisión de relaciones Blue/Red
- **Síntoma observado:** hostilidad direccional incorrecta; Blue no trataba a Red como Hostile.
- **Evidencia:** Prueba 1 y revisión del baseline de affiliation.
- **Causa confirmada o hipótesis:** baseline inicial no representaba la matriz de prueba deseada.
- **Sistemas afectados:** affiliation/disposition, threat acquisition.
- **Solución prevista:** ya aplicada: Blue↔Red mutual Hostile; Blue→Player Neutral; Red→Player Hostile.
- **Commit de corrección:** `b42e17c40ad843244fd390c9b0eeb707b6462d31`.
- **Validación:** diagnóstico AI P0 enfocado pasó después de la corrección.
- **Notas:** same-team permanece no hostil por defecto.

## ISSUE-0016 — Documentación operativa de M41 quedó desactualizada respecto al código publicado

- **Tipo:** `DOCS`
- **Estado:** `CONFIRMED`
- **Severidad:** `P2 / YELLOW`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Fase 0 — saneamiento AI/NPC
- **Momento de descubrimiento:** revisión de `Current_Milestone.md`, `Next_Sprints.md` y `Project_Roadmap.md`
- **Síntoma observado:** documentos todavía presentan M41.4 como `AUTHORIZED — IMMEDIATE PRIORITY`, aunque ya existen implementación, balance, observabilidad Pass D y Prueba 2 manual; el bloque NPC sigue abierto por defectos detectados, no porque M41.4 aún no haya comenzado.
- **Evidencia:** `eae5f14bed6aae82840762faf6561bf0b0e1625d` (`Add M41 NPC combat observability`) y estado documental actual.
- **Causa confirmada o hipótesis:** documentación no reconciliada después de los passes A–D y pruebas posteriores.
- **Sistemas afectados:** roadmap/operational docs.
- **Solución prevista:** Fase 0 actualiza snapshots operativos y deja la reconciliación canónica del roadmap como cambio documental controlado, sin renumerar IDs históricos.
- **Commit de corrección:** pendiente hasta cerrar reconciliación documental.
- **Validación:** `Current_Milestone`, `Next_Sprints` y autoridad canónica deben dejar de afirmar que el próximo paso es implementar M41.4 desde cero.
- **Notas:** no alterar estados históricos sin evidencia.

---

## Regla permanente para prompts de Codex

Todo prompt de implementación/revisión debe incluir una instrucción equivalente a:

> Si durante el trabajo detectas un bug, regresión, deuda técnica o comportamiento sospechoso que no estaba registrado, añade o actualiza su entrada en `Docs/Issue_Registry.md` con evidencia y estado correcto. No arregles problemas fuera de alcance por inercia. Si corriges un issue dentro del alcance, no lo borres: márcalo `RESOLVED`, registra commit/validación y conserva el historial.

La severidad o el estado no deben elevarse por intuición: registrar evidencia y distinguir siempre hechos de hipótesis.
