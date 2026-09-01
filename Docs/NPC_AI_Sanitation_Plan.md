# Old Scars — NPC AI Sanitation Plan

Este documento fija la secuencia de saneamiento posterior a Prueba 2 para que el trabajo no dependa de memoria de chat ni cambie de dirección entre días. El objetivo no es conservar arquitectura por orgullo: si la capa de decisión actual resulta innecesariamente compleja o contradictoria, puede simplificarse o reemplazarse reutilizando las autoridades inferiores válidas.

## Objetivo final

Cerrar una `NPC FOUNDATION V1` simple pero sólida:

- NPC humano libre realiza comportamiento ambiental en vez de quedar congelado;
- percepción visual depende de una mirada/atención humana observable, no de visión 360° ni exclusivamente de locomotion facing;
- al reconocer una amenaza, la conducta de encounter toma ownership de forma limpia;
- durante combate el NPC intenta mantener visualmente al objetivo y puede perderlo de forma física;
- LostContact conserva información limitada, busca brevemente y reacquire o abandona;
- navegación tiene un único dueño de alto nivel por frame;
- impactos usan geometría corporal suficiente para validar regiones anatómicas;
- localized wounds, condition y Vital Integrity siguen usando las autoridades ya existentes;
- incapacidad y muerte cancelan conducta activa;
- herramientas de debug permiten observar NPC↔NPC y NPC↔Player sin destruir la prueba;
- diagnostics validan resultados observables, no meros method calls/proxies.

## Regla arquitectónica central

En cualquier frame debe existir una respuesta inequívoca a:

> ¿Qué conducta tiene derecho a ordenar movimiento a este actor ahora?

Ambient, Encounter, Search e Inactive no deben competir entre sí sobre `ActorNavigationController`.

La simplificación puede eliminar o reemplazar una capa de decisión si la auditoría demuestra que conservarla requiere banderas, resets o ownership patches crecientes. Esto no autoriza a duplicar Perception, Navigation, Combat, Health, Equipment, Affiliation o Persistence.

## Fuera de alcance del saneamiento

No introducir por inercia Behavior Trees complejos, GOAP, Utility AI general, squads, cover avanzado, flanking sofisticado, hearing/noise, schedules/jobs, strategic AI, off-sector AI, morale complejo, procedural animation, full ballistics, bullet travel/drop/wind ni una facción/reputation productiva completa.

---

## Fase 0 — Registro, baseline y documentación

**Estado:** `IN PROGRESS` al crear este documento.

- Crear `Issue_Registry.md`.
- Registrar problemas confirmados, sospechosos y resueltos de Prueba 1/Prueba 2.
- Fijar severidad y evidencia.
- Reconciliar documentación operativa con el estado post-Pass D sin inventar estados históricos.
- Preservar el baseline Git y cambios user-owned.

**Gate:** existe una fuente persistente para bugs y una secuencia de trabajo estable.

## Fase 1 — Auditoría destructiva de la IA actual

Sin refactor de gameplay inicialmente.

Auditar todos los writers/owners de:

- `ActorNavigationController`;
- state AI;
- threat/target assignment y clear/reset;
- roaming;
- LastKnownPosition;
- body rotation / `transform.forward`;
- combat target;
- `Stop()` / navigation commands.

Revisar especialmente `SandboxActorRoamingController`, `HumanEncounterAIController`, `ActorThreatAcquisitionController`, `ActorNavigationController`, `SandboxNpcController` y seams reales encontrados en repo.

Entregar mapa `componente → decide → escribe → activa/desactiva → puede interrumpir`.

**Gate:** decidir con evidencia entre:

A. refactor pequeño de la capa actual; o
B. reemplazar/simplificar la capa de decisión conservando autoridades inferiores válidas.

No aceptar una solución basada en proliferación de flags de ownership.

## Fase 2 — Behavior ownership + Ambient roaming real

**Estado:** `COMPLETADA — 2026-09-01` (`7fa47c59d8bbe1df61b598f01875e91b2b51c089`).

- White/Blue/Red comparten comportamiento Ambient cuando están realmente Idle.
- Threat/Encounter interrumpe Ambient limpiamente.
- Al terminar encounter se libera ownership y Ambient vuelve.
- Incapacitado/muerto no navega ni combate.
- Diagnostics de roaming miden desplazamiento real, no sólo accepted orders.

**Gate cerrado:** White/Blue/Red recorrieron `0,75 m` físicos individualmente sin amenaza; Encounter tomó ownership sin nuevas órdenes Ambient, lo liberó y Red reanudó `0,751 m`; Inactive permaneció estable. Search continúa reservado y Fase 3 no se inició.

## Fase 3 — Orientación inicial + Gaze/Attention V1

Separar locomotion/body facing de atención visual.

Una autoridad mínima de gaze debe permitir:

- Ambient: inspección ocasional de direcciones;
- Candidate: orientar atención hacia posible amenaza;
- Combat: intentar mantener al objetivo en la mirada;
- LostContact/Search: mirar hacia información conocida/probable.

Gaze no duplica LOS/perception ni crea un segundo sistema de raycasts.

**Gate:** un NPC quieto no funciona como tanque inmóvil ni radar 360°.

## Fase 4 — Tracking visual continuo

Usar movimiento observado reciente para una predicción visual corta, limitada y humana. El objetivo es mantener la mirada sobre un target que se desplaza, especialmente lateralmente, sin snap instantáneo ni conocimiento mágico.

**Gate:** target visible móvil es seguido de forma continua; movimientos rápidos, obstáculos o cruce por detrás todavía pueden romper contacto.

## Fase 5 — Perception integrada con Gaze

El FOV/LOS de producción debe usar la dirección de mirada apropiada, con fallback explícito donde corresponda.

Tests mínimos:

- delante y dentro de FOV → visible si hay LOS;
- fuera de FOV → no visible hasta que gaze lo incluya;
- obstáculo → occluded aunque gaze sea correcto;
- actor detrás y nunca observado → no detección mágica.

**Gate:** overlay de percepción y decisión real coinciden.

## Fase 6 — LostContact / Search V1

Flujo mínimo:

`Seen → LOS lost → retain LastKnownPosition/recent motion hint → navigate to information → inspect briefly → reacquire OR release → Ambient`.

No agregar cover, flanking, hearing, squad search ni room clearing.

**Gate:** timeline observable termina en reacquire o release, no en memoria pasiva hasta timeout.

## Fase 7 — Human debug actor + hitboxes anatómicos

Usar modelo humano disponible estático/bind pose/T-pose para validar combate antes de animaciones.

Hit regions explícitas como mínimo:

- Head
- Torso
- LeftArm
- RightArm
- LeftLeg
- RightLeg

Separar collider locomotor de colliders de combate para que la cápsula no intercepte shots destinados a regiones anatómicas.

**Gate:** disparos dirigidos a cada región resuelven exactamente esa región.

## Fase 8 — Investigación del posible sesgo de piernas/pies

Instrumentar cada impacto relevante con origin, direction, collider hit, hit point, target y BodyRegion resuelta.

Primero tests deterministas por región; después muestra estadística razonable con aim NPC real.

No exigir una distribución artificial uniforme. Investigar sesgos inexplicables en aim point, spread, shot origin, collider selection o pose.

**Gate:** todas las regiones son físicamente alcanzables y no existe un sesgo anómalo sin explicación.

## Fase 9 — Debug Player: Invisible / Invincible

### Invisible-to-AI

Player conserva presencia física/interacción pero queda fuera del candidate/acquisition boundary de IA.

### Invincible

NPCs continúan detectando, disparando e impactando mediante el pipeline real. V1 permite heridas, pain, bleeding, trauma y condición, pero bloquea la transición final a Dead para no terminar la prueba.

**Gate:** ambos modos pueden activarse/desactivarse sin alterar contratos productivos cuando están apagados.

## Fase 10 — Observabilidad V2

Separar:

- overlay global multi-NPC: short ID/affiliation, state, gaze, FOV, LOS/target, LKP/search, navigation destination, shot traces;
- inspector detallado de un seleccionado: Vital, Blood, Trauma, Pain, wounds, ammo, recognition, perception reason, navigation/home/LKP, etc.

Overlays deben poder filtrarse por categorías y usar datos/queries reales de producción.

**Gate:** una pelea multi-NPC se entiende sin ciclar F6 constantemente.

## Fase 11 — Batería automatizada pequeña pero fuerte

Gates objetivos:

1. Ambient Roaming real.
2. Behavior Ownership.
3. Encounter Interruption.
4. Ambient Resume.
5. Idle Gaze.
6. Target Tracking.
7. FOV Integrity.
8. Occlusion.
9. Recognition.
10. LostContact.
11. Search.
12. Search Release/Reacquire.
13. Incapacity cancela conducta.
14. Death cancela conducta.
15. Anatomy / regiones.
16. Invisible Debug.
17. Invincible Debug.

**Regla:** validar resultados observables y contratos, no proxies irrelevantes.

## Fase 12 — Prueba 3 automatizada integrada

Escenario de referencia:

- 1 White;
- 3 Blue;
- 3 Red;
- Player Invisible.

Duración orientativa: 5–10 minutos.

Observar roaming, gaze, encounters, recognition, tracking, Blue↔Red, navigation, shots, hit regions, injuries, incapacity, death, LostContact/Search y retorno a Ambient.

Guardar log, screenshots/event trace y estado final. Durante la prueba no corregir silenciosamente problemas: registrarlos.

## Fase 13 — Prueba 3B con Player

- Invisible OFF.
- Invincible ON.
- Validar Blue→Player Neutral y Red→Player Hostile.
- Player se mueve lateralmente, cruza obstáculos, se acerca/aleja y rodea NPCs.
- Evaluar gaze/tracking/recognition/LostContact/Search/shots/damage.

**Gate:** cubre directamente las fallas de game feel observadas en Prueba 2.

## Fase 14 — Prueba manual final

Validación humana de game feel:

- ¿parece un humano en vez de un tanque?
- ¿mira de forma creíble?
- ¿detecta demasiado o demasiado poco?
- ¿tracking y pérdida de contacto son comprensibles?
- ¿la pelea se puede leer visualmente?
- ¿las heridas/regiones observadas tienen sentido?

Los diagnostics no sustituyen esta prueba.

## Fase 15 — Limpieza y cierre

Eliminar debug temporal, branches/código muerto, compatibilidad ya innecesaria, tests que validen contratos reemplazados y comentarios históricos engañosos.

Conservar:

- Issue Registry e historial `RESOLVED`;
- observabilidad útil;
- toggles Invisible/Invincible;
- regressions de contratos importantes.

Reconciliar Roadmap, Current Milestone, Next Sprints, Development Log y arquitectura técnica.

**Gate final:** NPC Foundation V1 queda cerrada sólo después de pruebas integradas y manuales satisfactorias.

---

## DONE global del saneamiento

El bloque sólo se considera cerrado cuando un escenario White/Blue/Red demuestra que todos tienen vida ambiental, la atención visual puede descubrir amenazas físicamente, Encounter toma ownership sin competir, el target es seguido visualmente, la pérdida de contacto produce una búsqueda breve basada en información limitada, los impactos se resuelven sobre geometría corporal coherente, incapacidad/muerte cancelan conducta, el actor vuelve a Ambient cuando corresponde y todo puede observarse/debuggearse sin herramientas que alteren el resultado.

## Protocolo entre fases

Después de toda fase con código:

1. Codex entrega un **REPORTE FINAL** suficientemente detallado: archivos, contratos, cambios, decisiones, tests, resultados, issues detectados y commit.
2. Se revisa el commit/diff real antes de autorizar la siguiente fase.
3. `Issue_Registry.md` se actualiza con nuevos problemas o resoluciones.
4. No encadenar automáticamente la fase siguiente si el gate anterior no está demostrado.
