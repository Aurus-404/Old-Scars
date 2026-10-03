# IMPL-0071 — NPC Incoming Damage Awareness / Damage-Direction Reaction

- **Estado:** `PLANNED / DEFERRED — REQUIRES OWN FUTURE SCOPE`
- **Fecha/origen:** 2026-09-19 — observación manual de Mauro durante pruebas locales históricas 1v1 de Social/Barks/KO; refinado 2026-10-03 con reproducción explícita de persecución melee por detrás.
- **Naturaleza:** backlog futuro aprobado. La referencia original a un "current Social/Barks closeout" es histórica: IMPL-0061 no está publicado en `dev`. IMPL-0071 sólo puede abrirse mediante autorización explícita cuando llegue su turno y nunca en paralelo con el scope activo.
- **ID note:** renumerado de un duplicado histórico `IMPL-0067` a `IMPL-0071` el 2026-10-02; `IMPL-0067` pertenece canónicamente a Input de movimiento/cámara compatible con Inventory abierto.

## Problema observado

Cuando un NPC recibe daño mientras se está desplazando puede continuar caminando casi en línea recta, sin una reacción conductual clara al hecho de haber sido alcanzado.

El actor ya puede registrar el daño médicamente y producir feedback como `DamageReceived`, pero falta una capa de **conciencia del estímulo entrante** que pueda alterar orientación/búsqueda sin regalar conocimiento omnisciente del atacante.

### Reproducción concreta observada — persecución melee por detrás

Caso reportado por Mauro el 2026-10-03:

1. NPC A camina delante siguiendo su navegación/ruta normal.
2. NPC B hostil lo persigue por detrás y está dentro de rango melee.
3. NPC B golpea a NPC A con una palanca/crowbar desde fuera del cono frontal de visión de A.
4. NPC A recibe correctamente el daño y puede emitir un bark como `¡ME DIERON EN EL [bodypart: "Torso"]!`.
5. A pesar de que el runtime ya confirmó que A recibió un impacto, NPC A no altera su conducta: continúa caminando, no pausa la ruta, no gira el cuerpo/gaze hacia el origen aproximado y no intenta localizar al agresor.
6. NPC B puede seguir caminando detrás y encadenar nuevos golpes mientras A permanece conductualmente indiferente porque el atacante nunca entró en su FOV frontal.

Este caso demuestra una separación concreta entre **damage awareness médico/feedback** y **damage awareness conductual**. El bark no debe confundirse con una reacción AI válida: hoy prueba que el impacto fue registrado, pero no que el actor haya convertido ese estímulo en orientación, investigación o adquisición perceptiva.

La expectativa de gameplay es humana y mínima: un actor capaz de reaccionar que siente un golpe por detrás debe interrumpir la indiferencia de navegación, orientarse hacia el sector del impacto y usar las autoridades existentes de Perception/FOV/LOS para descubrir qué ocurrió.

## Qué queremos

Cuando un actor reciba un impacto válido y **no esté en un estado de prioridad superior** como huida/pánico, debe poder:

1. detectar que recibió daño;
2. obtener una **dirección aproximada** desde la cual llegó el impacto;
3. interrumpir o pausar temporalmente una navegación incompatible si corresponde;
4. orientar cuerpo/gaze hacia ese sector;
5. usar Perception/FOV/LOS existentes para intentar localizar/reconocer al atacante;
6. si no lo reconoce, entrar en una búsqueda/investigación coherente con una dirección aproximada, no con una posición exacta revelada mágicamente.

## Regla de conocimiento

Recibir daño **NO** equivale a conocer:

- posición exacta del atacante;
- ActorInstanceId como amenaza reconocida;
- coordenadas world-space exactas;
- target confirmado.

La reacción debe transportar solamente la evidencia sensorial razonable necesaria, por ejemplo:

- dirección/bearing aproximado del impacto;
- timestamp/age;
- intensidad/severidad si existe un consumer real;
- BodyRegion impactada para feedback/medicina, sin usarla para inferir mágicamente al atacante.

Si CombatImpact ya posee attacker/shot origin para causalidad interna, esos datos no deben convertirse automáticamente en percepción perfecta del receptor.

## Seam sugerido

Evaluar un estímulo acotado tipo:

`IncomingDamageStimulus`

con contrato mínimo equivalente a:

- incoming direction / bearing;
- source event time;
- confidence/uncertainty sólo si un consumer real lo necesita;
- BodyRegion opcional;
- sin autoridad de Threat.

Debe reutilizar, cuando sea posible:

- Perception/FOV/LOS;
- gaze/orientation;
- Search/LKP;
- Behavior Ownership;
- CombatResolution como fuente causal del impacto.

No crear un segundo sistema completo de percepción.

## Reacción V1 esperada

Casos básicos:

`Ambient/Moving → recibe disparo desde atrás → stop/pause breve → gira hacia dirección aproximada → Perception intenta confirmar → Search/Encounter según evidencia real`

`Ambient/Moving → recibe golpe melee/crowbar por detrás fuera de FOV → interrumpe navegación indiferente → gira hacia origen aproximado → Perception intenta confirmar al agresor cercano → Encounter/defensa/huida según evidencia real`

Si ya conoce/percibe al enemigo, no hace falta una búsqueda redundante: la reacción puede integrarse con el Encounter existente.

La pausa exacta, velocidad de giro y ventana temporal son tuning posterior.

## Prioridades

La reacción a daño debe ser **preemptable**.

No debe imponerse sobre estados más fuertes, especialmente:

- Dead/Unconscious/Incapacitated;
- Flee/Panic;
- otras futuras emergency behaviors explícitamente de mayor prioridad.

En particular, un NPC huyendo en pánico **NO debe detenerse y girarse por cada impacto**. El daño sigue aplicándose médicamente, pero el behavior prioritario continúa siendo escapar.

## Data-driven

La intensidad/probabilidad/tipo de reacción debe poder ser modulada por datos del actor cuando exista el consumer correspondiente.

Evitar:

- `if (profile == "coward")`;
- strings de traits hardcodeados dentro de HumanEncounterAIController;
- ramas especiales por NPC concreto.

El comportamiento debe leer capacidades/traits/tags/perfil mediante contratos data-driven reutilizables.

## Observabilidad

Diagnostics/F6 deben poder mostrar al menos:

- last incoming-damage direction;
- age;
- whether reaction was accepted/suppressed;
- suppression reason;
- resulting behavior transition;
- whether hostile recognition came from own perception after orientation.

Nunca registrar que el actor "vio" al atacante si sólo recibió un impacto.

## Acceptance V1

- Un NPC alcanzado desde fuera de su FOV no continúa indiferente en la misma navegación si está libre para reaccionar.
- Caso diagnóstico obligatorio: NPC A camina delante; NPC B hostil lo sigue y lo golpea con crowbar/palanca desde atrás, fuera del FOV de A. El daño y bark pueden ocurrir, pero A debe dejar de continuar su ruta como si nada, orientar cuerpo/gaze hacia el sector del impacto y recién adquirir/reconocer a B mediante Perception normal si obtiene evidencia válida.
- Se orienta/investiga hacia un sector aproximado coherente con el impacto.
- No recibe Threat/posición exacta sólo por recibir daño.
- Si al girar obtiene LOS/FOV válido, puede reconocer mediante la autoridad de percepción normal.
- Si no obtiene percepción válida, puede buscar/investigar sin telepatía.
- Durante Flee/Panic, impactos adicionales no cancelan la huida para ejecutar esta reacción.
- Determinismo reproducible en diagnostics.

## Límites

- No suppression system completo en este slice.
- No ballistic acoustics universal.
- No exact attacker reveal.
- No nueva Threat authority.
- No cover system obligatorio.
- No flinch animation framework.
- No refactor amplio de Perception/Search.
- No abrir este scope en paralelo con el scope activo.

## Relación

- `ISSUE-0039 — Falta body turn in place para NPC quieto` es complementario pero no sustituye este IMPL: ISSUE-0039 trata orientación corporal productiva; IMPL-0071 aporta el estímulo conductual que decide reaccionar ante daño entrante.
- HumanEncounterAIController
- ActorBehaviorController / Behavior Ownership
- Perception/FOV/LOS
- Search/LKP
- CombatResolution / DamageReceived
- `IMPL-0072 — NPC Flee / Panic Behavior`

---

**Nota de integración:** esta entrada está registrada resumidamente en `Docs/Implementation_Backlog.md` como IMPL-0071.
