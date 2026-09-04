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
- **Estado:** `RESOLVED`
- **Severidad:** `P0 / RED`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 manual integrada
- **Momento de descubrimiento:** observación previa al combate y durante períodos sin target
- **Síntoma observado:** el NPC random White se desplaza de forma ambiental, mientras Blue y Red permanecen quietos y sólo comienzan a moverse cuando adquieren un target.
- **Evidencia:** observación manual repetida y flujo confirmado por código. `TrySpawnCombatNpc` sí añade y configura `SandboxActorRoamingController` para Blue/Red, y éste puede aceptar un destino. Sin embargo, `HumanEncounterAIController.EvaluateEncounter` trata `threat == null` como "Threat became unavailable", llama `ClearThreat` y termina en `ResetEncounter`; ese reset ejecuta `navigation.Stop()` y vuelve a poner `nextDecisionTime = 0`, por lo que el mismo camino vuelve a evaluarse en el frame siguiente. White usa el perfil sin `encounter_ai`, no recibe `HumanEncounterAIController` y su orden ambiental no sufre esa cancelación.
- **Causa confirmada o hipótesis:** `CONFIRMADO POR CÓDIGO`: Blue/Red sí reciben roaming, pero Encounter cancela repetidamente sus órdenes cuando no existe threat. No es una ausencia de `SandboxActorRoamingController`, un fallo demostrado de NavMesh, una deshabilitación del componente ni un problema demostrado del home anchor.
- **Sistemas afectados:** sandbox NPC, ambient behavior, encounter AI, navigation.
- **Solución prevista:** Fases 1–2 de `NPC_AI_Sanitation_Plan.md`: auditar writers/owners y dejar un único dueño de navegación por frame; todos los humanos deben compartir Ambient roaming cuando estén realmente Idle.
- **Commit de corrección:** `7fa47c59d8bbe1df61b598f01875e91b2b51c089`.
- **Validación:** Runtime/Editor compile `PASS`; `M41 Sandbox Preparation Diagnostics: PASS` con desplazamiento físico individual Blue `0,75 m`, Red `0,75 m`, White `0,75 m`; `M41.1 Human Encounter AI Diagnostics: PASS`; `M41.0 Navigation & Perception Diagnostics: PASS`; `Actor Consciousness & Incapacitation Diagnostics: PASS`.
- **Notas:** White sigue siendo referencia del mismo contrato Ambient, no una IA separada. Fase 2 reemplazó la coordinación competidora por `ActorBehaviorController`; ausencia de threat ya no ejecuta un reset destructivo por frame.

## ISSUE-0002 — El gate de roaming puede aceptar órdenes sin demostrar desplazamiento real

- **Tipo:** `BUG`
- **Estado:** `RESOLVED`
- **Severidad:** `P0 / RED`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** revisión del diagnóstico posterior a Prueba 2
- **Momento de descubrimiento:** contraste entre diagnóstico automatizado y comportamiento visible
- **Síntoma observado:** una señal como `AcceptedOrderCount > 0` puede pasar aunque Blue/Red continúen visualmente inmóviles.
- **Evidencia:** el comportamiento manual contradice la garantía que parecía ofrecer el diagnóstico. En `M41SandboxPreparationDiagnostics.ObserveInitialRoaming`, Blue/Red/White superan el primer gate con `AcceptedOrderCount >= 1`; la comprobación de posición sólo impone un límite máximo respecto del home anchor y no exige distancia recorrida. El gate de resume también exige únicamente que aumente el contador. `M41NpcSandboxDiagnostics` es algo más fuerte, pero acepta `Moving` como alternativa a desplazamiento y sólo exige que un actor de doce cumpla la condición, no que White/Blue/Red demuestren movimiento individual.
- **Causa confirmada o hipótesis:** `CONFIRMADO POR CÓDIGO`: los tests principales pueden observar una orden aceptada antes de que exista desplazamiento y pueden pasar aunque Encounter la cancele inmediatamente después.
- **Sistemas afectados:** diagnostics/QA de AI y navigation.
- **Solución prevista:** validar posición inicial/final, distancia recorrida real, cambios de destino y límites de home radius.
- **Commit de corrección:** `7fa47c59d8bbe1df61b598f01875e91b2b51c089`.
- **Validación:** `M41SandboxPreparationDiagnostics` acumuló recorrido horizontal real por frame y exigió al menos `0,75 m` individual para White/Blue/Red, home radius acotado, `0,751 m` de reanudación Red y menos de `0,05 m` durante Inactive; resultado `PASS`. `M41NpcSandboxDiagnostics` ya no acepta `Moving` como sustituto de recorrido en su gate de roaming.
- **Notas:** se eliminaron las aserciones engañosas basadas únicamente en accepted orders. Principio permanente: los gates importantes validan contratos observables, no sólo que una función haya sido llamada.

## ISSUE-0003 — Posible competencia Ambient/Encounter sobre Navigation

- **Tipo:** `DESIGN_DEBT`
- **Estado:** `RESOLVED`
- **Severidad:** `P0 / RED`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** análisis arquitectónico posterior a Prueba 2
- **Momento de descubrimiento:** investigación conceptual del roaming Blue/Red
- **Síntoma observado:** el patrón White-mueve / Blue-Red-no-mueven sugiere que composición, resets o ownership entre roaming, encounter y acquisition pueden estar compitiendo o anulándose.
- **Evidencia:** `SandboxActorRoamingController` llama `TryNavigate` y `Stop`; `HumanEncounterAIController` llama `TryNavigate` para retreat/engagement y `Stop` desde assignment, override, pérdida de percepción, fight tactics, inactive y reset; `ActorPhysicalCollapseController` también fuerza `Stop` por incapacidad física/death; persistence aplica pose mediante el mismo controller. No existe token/lease/owner enum ni orden de ejecución explícito entre Ambient, Acquisition y Encounter. El caso concreto de ISSUE-0001 demuestra la colisión: Ambient acepta una orden y Encounter la cancela desde el flujo sin threat.
- **Causa confirmada o hipótesis:** `CONFIRMADO POR CÓDIGO`: la capa alta distribuye ownership implícito entre componentes independientes que escriben el mismo `ActorNavigationController`. `ActorThreatAcquisitionController` además controla parte del lifecycle del target mediante `ClearThreat`, mientras `HumanEncounterAIController` mantiene la otra parte y resetea navegación/estado.
- **Sistemas afectados:** roaming, encounter AI, threat acquisition, navigation.
- **Solución prevista:** decisión de Fase 1: **B — reemplazar/simplificar la capa de decisión**, conservando `ActorNavigationController`, `ActorVisualPerceptionService`, combat, health/medical/condition, equipment, affiliation y persistence. Fase 2 debe introducir una única decisión de behavior ownership por actor/frame para Ambient, Encounter, Search e Inactive, sin proliferar flags entre controladores actuales.
- **Commit de corrección:** `7fa47c59d8bbe1df61b598f01875e91b2b51c089`.
- **Validación:** `M41 Sandbox Preparation Diagnostics: PASS` demostró `Ambient → Encounter → Ambient`, cero nuevas órdenes Ambient durante Encounter, reanudación física y ownership Inactive sin oscilación; búsqueda está reservada como valor `Search` sin implementación. Búsqueda de writers confirma que las órdenes normales pasan por `ActorBehaviorController`; collapse conserva sus interrupciones técnicas.
- **Notas:** `ActorBehaviorController` es la única capa alta que posee navegación normal. `ActorNavigationController` sigue siendo la autoridad inferior; Physical collapse/death y persistence conservan precedencia técnica legítima.

## ISSUE-0004 — La percepción inicial depende demasiado de la orientación corporal de spawn

- **Tipo:** `BUG`
- **Estado:** `RESOLVED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 manual integrada
- **Momento de descubrimiento:** observación de NPCs recién spawneados
- **Síntoma observado:** un NPC que aparece orientado en dirección contraria puede no detectar actores cercanos porque permanece mirando al frente hasta que otra conducta lo hace moverse.
- **Evidencia:** observación manual.
- **Causa confirmada o hipótesis:** `CONFIRMADO Y CORREGIDO`: el FOV productivo estaba centrado exclusivamente en body-forward aunque Fase 3 ya exponía una mirada lógica acotada.
- **Sistemas afectados:** perception, spawn presentation, AI attention.
- **Solución prevista:** aplicada en Fase 5: `ActorVisualPerceptionService` usa `CurrentGazeDirection` como único forward del FOV cuando el Gaze está configurado/válido, con fallback explícito a body-forward.
- **Commit de corrección:** `2fc27d946f5a807abd4f046d2dee85331490b7c2` — `Center production perception on current gaze`.
- **Validación:** Runtime/Editor compile `PASS`; `M41 Gaze-Centered Production Perception Diagnostics: PASS`: half-FOV `60°`, Current-vs-Desired `65°/65°/10° → OutsideFov` hasta que Current llegó a `59,957°`, body-only `55°` pero gaze `75,03° → OutsideFov`, y Ambient `70°` body / `59,991°` gaze → `Perceived → Candidate`; fallback sin Gaze `Perceived` desde body-forward. LOS con barrera permaneció `Occluded`. Regresiones Gaze/Attention, Progressive Recognition, Human Encounter AI, Sandbox Preparation y Navigation/Perception `PASS`.
- **Notas:** no existe body-FOV OR gaze-FOV ni uso de `DesiredGazeDirection`. El scanning Ambient cambia percepción real sin target previo; F6 dibuja el mismo `CurrentPerceptionForward` productivo.

## ISSUE-0005 — Falta una autoridad de Gaze/Attention humana

- **Tipo:** `DESIGN_DEBT`
- **Estado:** `RESOLVED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 manual integrada
- **Momento de descubrimiento:** observación de NPC quieto
- **Síntoma observado:** el NPC se comporta como un tanque: si está quieto no gira cabeza/mirada ni inspecciona direcciones; su visión sólo cambia cuando cambia el cuerpo/movimiento.
- **Evidencia:** observación manual.
- **Causa confirmada o hipótesis:** `CONFIRMADO Y CORREGIDO`: faltaba un seam explícito de atención visual separado de locomotion/body facing.
- **Sistemas afectados:** perception, encounter AI, ambient behavior, presentation.
- **Solución prevista:** aplicada en Fase 3 mediante `ActorGazeController`, autoridad lógica por actor que consume atención legítima y produce desired/current gaze acotado sin adquirir targets, evaluar LOS, navegar, combatir ni poseer behavior.
- **Commit de corrección:** `e1bd7d7ce6d0f0a6885cb23a7047d53d31fd0509` — `Add NPC gaze and attention V1`.
- **Validación:** Runtime/Editor compile `PASS`; `M41 Gaze & Attention Diagnostics: PASS` con dos direcciones Ambient, cambio `22,996°`, yaw máximo `22,996°`, step máximo `0,136°`, convergencia Candidate `72,959° → 45,92°`, Encounter `59,08° → 32,071°`, rechazo Candidate/Encounter cross-observer, LostContact por `LastKnownPosition` e Inactive estable; regresiones Sandbox Preparation, Progressive Recognition y Human Encounter AI `PASS`; `git diff --check: PASS`.
- **Notas:** los modos son `Ambient`, `Candidate`, `Encounter`, `LostContact` e `Inactive`. No se introdujeron Behavior Trees/GOAP. La integración del FOV productivo pertenece a Fase 5.

## ISSUE-0006 — Tracking visual lateral deficiente

- **Tipo:** `BUG`
- **Estado:** `RESOLVED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 manual integrada
- **Momento de descubrimiento:** jugador desplazándose lateralmente frente a NPC hostil
- **Síntoma observado:** al moverse el objetivo hacia un costado, el NPC puede perderlo y tardar en reencontrarlo en vez de tratar de mantenerlo dentro de la mirada.
- **Evidencia:** observación manual.
- **Causa confirmada o hipótesis:** `CONFIRMADO Y CORREGIDO`: Fase 4 implementó tracking lógico, pero el FOV body-forward impedía que ese tracking conservara percepción productiva lateral.
- **Sistemas afectados:** gaze, perception, combat targeting.
- **Solución prevista:** completada en Fase 5 conectando el FOV productivo al Current Gaze y verificando tracking lateral integrado, límites humanos y occlusion física.
- **Commit de corrección:** `2fc27d946f5a807abd4f046d2dee85331490b7c2` — `Center production perception on current gaze` (sobre tracking lógico Fase 4 `e72feeb67edfe9b208eefa4d4c6c13f488df62cc`).
- **Validación:** `M41 Gaze-Centered Production Perception Diagnostics: PASS`: segunda muestra `0,2 s`, velocidad `(-4,02, 0,00, -1,15)` / `4,183 m/s`; el target lateral quedó a `82°` del body, `40,457°` del gaze y siguió `Perceived` con half-FOV `60°`. El caso humano extremo quedó a `96,543°` del gaze y devolvió `OutsideFov`; la barrera devolvió `Occluded` y, al retirarla, `Perceived`. `M41 Gaze & Attention Diagnostics: PASS` conservó caps `0,35 s` / `1,5 m`, target-switch reset, expiry y no-wallhack. Regresiones Progressive Recognition, Human Encounter AI, Sandbox Preparation y Navigation/Perception `PASS`.
- **Notas:** tracking no es infalible y no alimenta aim, spread, shots ni ballistic lead. El cierre resuelve el síntoma productivo de FOV lateral sin cambiar combat.

## ISSUE-0007 — LostContact no ejecuta una búsqueda real

- **Tipo:** `BUG`
- **Estado:** `RESOLVED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-08-31
- **Prueba / origen:** Prueba 1
- **Momento de descubrimiento:** pérdida de LOS durante encounter
- **Síntoma observado:** al perder contacto, la IA conserva el estado/memoria durante un timeout pero no investiga físicamente la última posición conocida.
- **Evidencia:** Prueba 1 y revisión del flujo LostContact.
- **Causa confirmada o hipótesis:** `CONFIRMADO Y CORREGIDO`: LostContact retenía información legítima pero sólo esperaba el timeout; `ActorBehaviorOwner.Search` estaba reservado sin API ni conducta productiva.
- **Sistemas afectados:** encounter AI, navigation, perception memory.
- **Solución prevista:** aplicada en Fase 6: Fight congela `SearchObservedPosition`/`SearchAnchor` desde LKP, transfiere ownership real a Search, emite una orden, inspecciona al llegar y reacquire a Encounter o release a Ambient. Avoid/Flee conservan LostContact sin persecución Search.
- **Commit de corrección:** `7590ec6f868da89a72a5514a85f7c042fb89e36f` — `Add bounded LostContact search`.
- **Validación:** Runtime/Editor compile `PASS`; `M41 LostContact / Search V1 Diagnostics: PASS`. Reacquire: states `Idle → Alerted → Fighting → LostContact → Searching → Alerted`, owners `Encounter → Search → Encounter`, `0,642 m`, mismo threat y sin Ambient/ataque. Release: `Idle → Alerted → Fighting → LostContact → Searching → Idle`, owners `Encounter → Search → Ambient`, `8 m`, error de llegada `0 m`, inspección `0,8 s`, Ambient posterior `0,501 m`. Anchor observado `(26,000, 1,050, 34,000)` y proyectado `(26,000, 0,050, 34,000)` permanecieron congelados con una sola orden pese al movimiento oculto. Avoid `0` búsquedas; incapacidad → Inactive; target inválido → Aborted. Regresiones Human Encounter, Sandbox Preparation, Gaze/Attention, Gaze-Centered Perception, Progressive Recognition y Navigation/Perception `PASS`.
- **Notas:** Search V1 no agrega controller/planner/blackboard ni lee la posición oculta. Reutiliza `lost_contact_timeout_seconds` como ventana post-arrival. No incluye cover, flanking, hearing, squad search, grids ni room clearing.

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
- **Commit de corrección:** Fase 7 `96cccbe514177d8eb05d8c5c439909b4657f252e` agrega geometría anatómica explícita y prueba determinista; no declara corrección del sesgo.
- **Validación:** Fase 7 confirma `6/6` regiones físicas alcanzables. Prueba 3 observó múltiples Torso/Arm/Leg y no reprodujo cualitativamente el patrón extremo anterior, pero la distribución NPC real todavía debe medirse en Fase 8A.
- **Notas:** `HumanEncounterAIController` y Perception filtran los nuevos combat hitboxes para conservar sus centros/semánticas previas; no se ajustaron aim, spread, medicina ni Vital Integrity. El sesgo permanece `SUSPECTED`.

## ISSUE-0009 — La cápsula humana es insuficiente para validar hit testing anatómico

- **Tipo:** `DESIGN_DEBT`
- **Estado:** `RESOLVED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 manual integrada
- **Momento de descubrimiento:** análisis de los impactos por región
- **Síntoma observado:** el actor de prueba no tiene geometría física diferenciada para cabeza, brazos, torso y piernas; la región se deriva de un collider simplificado.
- **Evidencia:** representación actual y limitación observada durante combate.
- **Causa confirmada o hipótesis:** la cápsula fue adecuada para la foundation, no para evaluación anatómica final.
- **Sistemas afectados:** actor presentation, combat hit testing, body region resolution.
- **Solución prevista:** aplicada en Fase 7: el FBX existente `PSX_Char_Male_Base` se materializa como prefab estático por family de visual rig, con cápsula locomotora marcada y seis hitboxes explícitos `Head/Torso/LeftArm/RightArm/LeftLeg/RightLeg`.
- **Commit de corrección:** `96cccbe514177d8eb05d8c5c439909b4657f252e` — `feat(ai): add human anatomical hitboxes`.
- **Validación:** `M41 Human Debug Actor & Anatomical Hitboxes Diagnostics: PASS`: `6/6` chain real `PhysicalShotPathResolver → CombatHitbox → CombatResolution → wound`; la cápsula habilitada fue atravesada para Torso, fallback legacy resolvió `Torso`, Perception conservó `Perceived/OutsideFov/Occluded` y physical collapse preservó ownership de las seis regiones. Runtime/Editor compile, M40.0/M40.1, M41 Sandbox, Navigation/Perception, Sandbox Preparation y Search V1 pasaron.
- **Notas:** animaciones, IK, ragdoll articulado, target anatomical AI y distribución estadística continúan fuera de alcance. Los assets de representación runtime son actualmente built-in `Resources` por family de rig; el contrato visual existente ya declara que AssetBundles/Mod Kit no forman parte del slice actual.

## ISSUE-0010 — Observabilidad global insuficiente para peleas multi-NPC

- **Tipo:** `TOOLING`
- **Estado:** `CONFIRMED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2 / revisión de Pass D; reconfirmado en Prueba 3.
- **Momento de descubrimiento:** seguimiento de varios NPCs simultáneamente
- **Síntoma observado:** la observabilidad detallada es buena para un NPC seleccionado, pero no permite entender de un vistazo gaze/FOV/target/navigation/search/shot traces de toda la pelea.
- **Evidencia:** experiencia de Prueba 2 y Prueba 3; para comparar ambos lados de un 1v1 sigue siendo necesario ciclar selección.
- **Causa confirmada o hipótesis:** Pass D priorizó inspección focal mediante selección F6.
- **Sistemas afectados:** development UI, world overlays, diagnostics.
- **Solución prevista:** adelantar un slice mínimo antes de F8A: world visuals Gaze/FOV/LOS simultáneas para varios/todos los NPC y selección sólo para inspector profundo. F10 completa targeting/shot observability.
- **Commit de corrección:** pendiente.
- **Validación:** una pelea multi-NPC debe ser comprensible sin ciclar constantemente selección.
- **Notas:** usar datos/queries de producción; no duplicar percepción o raycasts sólo para debug.

## ISSUE-0011 — El panel de observabilidad actual es demasiado dependiente de selección para pruebas integradas

- **Tipo:** `TOOLING`
- **Estado:** `CONFIRMED`
- **Severidad:** `P2 / YELLOW`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** Prueba 2; reconfirmado en Prueba 3
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
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** planificación posterior a Prueba 2; necesidad confirmada por Prueba 3.1
- **Momento de descubrimiento:** necesidad de observar NPC↔NPC de cerca sin que Player altere adquisición de amenazas
- **Síntoma observado:** el Player no puede excluirse limpiamente de perception/acquisition manteniendo presencia física e interacción. Prueba 3.1 mostró a Red abandonando el supuesto 1v1 y adquiriendo Player como threat.
- **Evidencia:** tooling actual + log manual de Prueba 3.1.
- **Causa confirmada o hipótesis:** feature debug aún no implementada.
- **Sistemas afectados:** player debug, AI candidate/acquisition boundary.
- **Solución prevista:** adelantada al Prueba 3 Correction Pass: excluir al Player como candidato/adquirible sin desactivar GameObject ni collider; OFF conserva gameplay normal.
- **Commit de corrección:** pendiente.
- **Validación:** Player puede acercarse a una pelea NPC↔NPC sin generar recognition/targeting.
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
- **Síntoma observado:** documentos todavía presentaban M41.4 como `AUTHORIZED — IMMEDIATE PRIORITY`, aunque ya existen implementación, balance, observabilidad Pass D y pruebas manuales posteriores; el bloque NPC sigue abierto por saneamiento/correcciones, no porque M41.4 aún no haya comenzado.
- **Evidencia:** `eae5f14bed6aae82840762faf6561bf0b0e1625d` (`Add M41 NPC combat observability`) y estado documental.
- **Causa confirmada o hipótesis:** documentación no reconciliada después de los passes A–D y pruebas posteriores.
- **Sistemas afectados:** roadmap/operational docs.
- **Solución prevista:** `Current_Milestone`, `Next_Sprints`, sanitation plan y contexto ya fueron reconciliados; `Project_Roadmap.md` también debe dejar de presentar M41.4 como trabajo no iniciado sin renumerar IDs históricos.
- **Commit de corrección:** parcial; pendiente cierre canónico del `Project_Roadmap.md`.
- **Validación:** todos los snapshots/camino crítico deben describir el correction pass post-Prueba 3 y conservar M42+ sin autorización automática.
- **Notas:** no alterar estados históricos sin evidencia.

## ISSUE-0017 — El fixture M41NpcSandbox puede matar el target antes de validar la segunda región

- **Tipo:** `TOOLING`
- **Estado:** `RESOLVED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-01
- **Prueba / origen:** regresión adicional de Fase 2, `M41NpcSandboxDiagnostics.RunBatchWorldRuntime`
- **Momento de descubrimiento:** evidencia de combate localizada posterior al gate de roaming
- **Síntoma observado:** el diagnóstico termina con exit code `1` después de que el disparo inicial a Head mata al NPC; el disparo siguiente a LeftLeg es rechazado con `Dead actors cannot receive new M40 wounds`.
- **Evidencia:** log `Phase2_regression_sandbox.log`: `ActorPhysicalCollapseController` registra lifecycle `Dead` tras el primer disparo y `M41NpcSandboxDiagnostics.BeginCombatAndDeathEvidence` falla al exigir éxito del segundo disparo. El gate de behavior/roaming ya había sido superado y el fallo ocurre en la fixture de combat/medical no modificada por Fase 2.
- **Causa confirmada o hipótesis:** `CONFIRMADO POR EJECUCIÓN Y CÓDIGO`: la fixture selecciona un actor sin armor, dispara primero a Head con el balance letal actual y asume que seguirá vivo para comprobar LeftLeg. Es una expectativa diagnóstica incompatible con el balance vigente, no evidencia de una regresión de behavior ownership.
- **Sistemas afectados:** diagnostics M41 sandbox, fixture de combat/medical localizada.
- **Solución prevista:** en una tarea de diagnostics/combat autorizada, usar objetivos separados o un orden/fixture no letal que valide regiones sin depender de que el target sobreviva un headshot.
- **Commit de corrección:** `e0d5fb9c40fba6b62fe8c1ffa60a24cb9cfeb06f` — `Fix M41 NPC sandbox anatomy fixture`.
- **Validación:** Unity `6000.4.6f1` batchmode `-nographics`: Runtime compile `PASS`; Editor compile `PASS`; `M41.3 NPC Sandbox Spawn & Randomized Loadouts Diagnostics: PASS` con dos objetivos sin armadura independientes, Head/LeftLeg resueltos por combat y medical reales, death/corpse/persistence `Result: Success`; `git diff --check: PASS`.
- **Notas:** el diagnostic conserva el gate de roaming físico de Fase 2. Sólo se corrigió su fixture; no se modificó combat, medical, balance, .303, Vital Integrity, AI, behavior ownership, navigation, perception ni perfiles productivos.

## ISSUE-0018 — El gate Inactive confundía physical collapse con locomoción de Behavior

- **Tipo:** `TOOLING`
- **Estado:** `RESOLVED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-02
- **Prueba / origen:** regresión final de Fase 3, `M41SandboxPreparationDiagnostics`
- **Momento de descubrimiento:** revalidación de incapacidad después de variar el yaw inicial determinista del sandbox
- **Síntoma observado:** el diagnostic falló su umbral de desplazamiento Inactive aunque ownership, estado, scans, ataques, órdenes Ambient y Navigation permanecieron estables.
- **Evidencia:** el actor registró sólo desplazamiento físico pequeño durante collapse (`0,026 m` en la corrida final), con `ActorBehaviorOwner.Inactive`, delta de `AmbientDistanceTravelled=0 m`, cero nuevas órdenes y Navigation no `Moving`.
- **Causa confirmada o hipótesis:** `CONFIRMADO POR EJECUCIÓN Y CÓDIGO`: el gate usaba movimiento del root como sustituto de locomoción normal y podía atribuir al Behavior el movimiento permitido de `ActorPhysicalCollapseController`.
- **Sistemas afectados:** diagnostics de AI behavior ownership e incapacidad.
- **Solución prevista:** aplicada: el gate exige estabilidad de ownership/revisiones/acquisition/attack, cero delta de recorrido Ambient y Navigation no `Moving`; conserva el desplazamiento físico de collapse como evidencia informativa y failure context accionable.
- **Commit de corrección:** `e1bd7d7ce6d0f0a6885cb23a7047d53d31fd0509` — `Add NPC gaze and attention V1`.
- **Validación:** `M41 Sandbox Preparation Diagnostics: PASS`: Blue/Red/White `0,751 m`, ownership `Ambient → Encounter → Ambient`, reanudación Red `0,751 m`, Inactive con delta Ambient `0 m`, physical collapse `0,026 m` y sin threat/navigation/attack.
- **Notas:** no se cambió `ActorPhysicalCollapseController`, gameplay de incapacidad ni el contrato de Behavior; sólo se corrigió la semántica y observabilidad del gate.

## ISSUE-0019 — F6 presenta snapshots históricos de Perception/LOS como si fueran estado actual

- **Tipo:** `TOOLING`
- **Estado:** `CONFIRMED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-03
- **Prueba / origen:** Prueba 3 / 3.1 / 3.2 + revisión de repo
- **Momento de descubrimiento:** observación de world lines mientras NPCs se desplazaban y después de death/inactive
- **Síntoma observado:** FOV/LOS puede parecer salir del piso o de una posición anterior, desaparecer por momentos y mostrar `Perception: True / Perceived` o `LOS Perceived` en un actor que ya está `Dead / Inactive / Threat <NONE> / Attention Inactive`.
- **Evidencia:** capturas manuales repetidas. La revisión del tooling muestra que parte de F6 consume `LastPerception`/`LastAcquisitionPerception` y su `ObserverOrigin` snapshot mientras el actor puede haber seguido moviéndose; el panel no separa con suficiente claridad current state de last evidence.
- **Causa confirmada o hipótesis:** `CONFIRMADO POR CÓDIGO + MANUAL`: el visualizador usa evidencia histórica legítima pero la presenta como si fuese current en contextos donde el snapshot ya quedó espacial/temporalmente stale. Esto no demuestra que la percepción productiva realmente observe desde esa posición vieja.
- **Sistemas afectados:** F6 observability, world overlays, manual QA de perception/search/combat.
- **Solución prevista:** Prueba 3 Correction Pass B: current Gaze/FOV debe originarse en eye/origin actual; last result debe marcarse explícitamente `LAST`; Dead/Inactive no presenta snapshot histórico como current perception. Resolver junto con multi-NPC sin duplicar raycasts/perception.
- **Commit de corrección:** pendiente.
- **Validación:** mover un NPC después de una percepción no deja current FOV/LOS anclado atrás; Dead/Inactive no afirma current perception; last evidence puede seguir inspeccionándose sólo si está rotulado como histórico.
- **Notas:** relacionado con `ISSUE-0010/0011`, pero distinto: éstos cubren selección/multi-NPC; este issue cubre veracidad temporal/espacial del visualizador.

## ISSUE-0020 — La incapacidad temporal borra el contexto de enemigo y reinicia artificialmente el combate

- **Tipo:** `BUG`
- **Estado:** `CONFIRMED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-03
- **Prueba / origen:** Prueba 3.1/3.2 + revisión de repo
- **Momento de descubrimiento:** Blue/Red durante Fight cuando uno queda `Incapacitated/Unconscious`
- **Síntoma observado:** el rival deja de atacar al incapacitado — comportamiento deseable — pero además libera Encounter y vuelve a Ambient; el actor KO entra Inactive y limpia threat/contexto. Al recuperarse, vuelve a Ambient/Idle y debe redescubrir al mismo enemigo como si la pelea anterior no hubiese ocurrido. Visualmente produce `golpe → KO → paseo/separación → recovery → redescubrimiento → pelea nueva`.
- **Evidencia:** Prueba 3.2 registró `Fighting → Inactive` en el KO, `Encounter → Ambient` en el rival con `Assigned actor is no longer a living hostile candidate`, luego `Inactive → Ambient/Idle` y posterior `THREAT_ASSIGNED` nuevo. En código, acquisition exige `CanPerformActiveActions` para mantener current threat; `EnterInactive`/`ReleaseEncounter` limpian threat y encounter memory.
- **Causa confirmada o hipótesis:** `CONFIRMADO`: el contrato actual trata `no puede actuar ahora` como si también significara `ya no existe contexto de enemigo`. Threat activo y memoria mínima de combate están acoplados de forma demasiado destructiva.
- **Sistemas afectados:** threat acquisition, Human Encounter, incapacity/recovery, combat continuity.
- **Solución prevista:** `IMPL-0014`: separar amenaza activa de memoria mínima del enemigo reciente. Mientras KO, no atacar deliberadamente; atacante y noqueado conservan contexto del enemigo. Recovery puede reanudar conflicto sin redescubrimiento artificial. La memoria no actualiza posición oculta: Perception/LKP/Search siguen siendo autoridad espacial. No crear memory framework general.
- **Commit de corrección:** pendiente.
- **Validación:** 1v1: actor A noquea B; A deja de golpearlo; B permanece inactivo; tras recovery ambos conservan al rival como enemigo conocido, y la reanudación usa percepción/LKP correctos sin wallhack ni `Ambient rediscovery` obligatorio.
- **Notas:** `Dead` sigue siendo terminal. Este issue no reabre `ISSUE-0014` ping-pong Idle/Inactive, que permanece resuelto.

## ISSUE-0021 — Knockout/Unconscious no tiene minimum real-time dwell antes de recovery

- **Tipo:** `DESIGN_DEBT`
- **Estado:** `CONFIRMED`
- **Severidad:** `P1 / ORANGE`
- **Fecha de descubrimiento:** 2026-09-03
- **Prueba / origen:** Prueba 3.1/3.2 + revisión de `ActorConditionComponent`/`WorldClock`
- **Momento de descubrimiento:** recuperación visible de actores noqueados durante el mismo encounter prolongado
- **Síntoma observado:** un actor puede volver a active behavior relativamente rápido después de quedar KO; no existe una garantía explícita de permanencia mínima en knockout/unconscious.
- **Evidencia:** recovery de condición depende de thresholds/trauma y del progreso por `WorldClock`. El baseline de world time está acelerado respecto de tiempo real, por lo que trauma transitorio puede recuperarse y cruzar thresholds sin un dwell real específico de KO.
- **Causa confirmada o hipótesis:** `CONFIRMADO COMO GAP DE CONTRATO`: no existe un minimum real-time KO dwell. El valor final de duración adecuada todavía requiere playtest, pero la ausencia del gate es real.
- **Sistemas afectados:** ActorCondition, incapacity/unconscious, combat game feel, QA.
- **Solución prevista:** `IMPL-0015`: agregar un mínimo configurable de tiempo real durante el cual knockout/unconscious no puede devolver active actions; después de ese mínimo, physiology/thresholds vigentes siguen decidiendo si recovery es posible. No crear otro reloj global.
- **Commit de corrección:** pendiente.
- **Validación:** un KO no puede recuperarse antes del dwell mínimo aunque `WorldClock` avance; una vez cumplido, un actor fisiológicamente incapaz sigue sin despertar; balance final se ajusta con prueba manual.
- **Notas:** no confundir con healing ni con tiempo de sangrado. Es un gate de pérdida de conciencia/recovery, no una recuperación médica paralela.

---

## Regla permanente para prompts de Codex

Todo prompt de implementación/revisión debe incluir una instrucción equivalente a:

> Si durante el trabajo detectas un bug, regresión, deuda técnica o comportamiento sospechoso que no estaba registrado, añade o actualiza su entrada en `Docs/Issue_Registry.md` con evidencia y estado correcto. No arregles problemas fuera de alcance por inercia. Si corriges un issue dentro del alcance, no lo borres: márcalo `RESOLVED`, registra commit/validación y conserva el historial.

La severidad o el estado no deben elevarse por intuición: registrar evidencia y distinguir siempre hechos de hipótesis.
