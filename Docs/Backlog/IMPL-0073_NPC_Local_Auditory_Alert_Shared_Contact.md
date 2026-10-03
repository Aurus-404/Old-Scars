# IMPL-0073 — NPC Local Auditory Alert / Shared Hostile Contact

- **Estado:** `PLANNED / DEFERRED — REQUIRES OWN FUTURE SCOPE`
- **Fecha/origen:** 2026-10-03 — decisión de diseño de Mauro a partir del comportamiento esperado cuando un NPC detecta un enemigo y avisa verbalmente a aliados cercanos.
- **Naturaleza:** backlog futuro aprobado. Registrar este contrato no abre implementación inmediata ni altera el scope activo.
- **Dependencia conceptual:** el repo ya menciona `SharedContact/combat awareness` en IMPL-0070/0072, pero hasta esta entrada no existía un contrato explícito de alcance, propagación ni anti-chain.

## Problema que queremos evitar

Cuando un NPC reconoce legítimamente a un enemigo puede gritar una alerta para avisar a otros actores cercanos. Esa comunicación **no puede funcionar como un broadcast global** ni como conocimiento telepático de facción.

Un grito emitido en un encuentro local no debe alertar NPC a cientos de metros o kilómetros ni terminar notificando a todo el mapa por una cadena infinita de retransmisiones.

La alerta debe representar una comunicación humana localizada:

`NPC A reconoce enemigo → grita → aliados que realmente pueden recibir la alerta prestan atención → orientan su atención hacia el sector indicado → usan su propia Perception para confirmar`

## Separación obligatoria: bark vs estímulo gameplay

El bark visible/audible y el conocimiento AI son conceptos relacionados pero distintos.

Que el NPC A diga:

`¡ENEMIGO!`

no significa que todos los NPC de su afiliación reciban automáticamente un target.

El bark puede generar un estímulo acotado tipo:

`SharedHostileContactStimulus`

El estímulo sólo puede ser consumido por actores elegibles dentro de su alcance efectivo.

El presenter/log de Barks sigue siendo presentación/observabilidad; no debe convertirse en una autoridad global de Threat.

## Alcance físico obligatorio

Toda alerta vocal debe tener un **rango máximo efectivo**.

Valores iniciales sugeridos únicamente para tuning/diagnostics, **no balance final**:

- conversación normal: aproximadamente `8–12 m`;
- bark fuerte: aproximadamente `15–20 m`;
- grito de combate/alarma: aproximadamente `25–35 m`;
- fuera del alcance máximo: el receptor no obtiene estímulo ni conocimiento alguno.

Los valores finales deben ser data-driven por tipo/intensidad de voz cuando exista el consumer real correspondiente.

No usar un radio enorme sólo para simplificar encounters.

## Oclusión y entorno

La distancia no es la única restricción.

La recepción debe poder considerar de forma proporcional:

- distancia emisor → receptor;
- paredes/obstáculos importantes;
- interior/exterior cuando exista soporte real;
- intensidad/tipo de vocalización.

V1 **no necesita un sistema acústico completo** ni simulación física de sonido.

Puede usarse una aproximación barata y determinista de obstrucción/atenuación. El objetivo es impedir casos absurdos como escuchar una alerta perfectamente a través de múltiples edificios o a distancias no razonables, no construir todavía ballistic/acoustic simulation universal.

## Regla de conocimiento

Recibir una alerta aliada **NO equivale automáticamente** a:

- `Threat = enemigo`;
- reconocer ActorInstanceId del enemigo;
- conocer coordenadas world-space exactas;
- conocer su posición actual después de que se mueva;
- compartir mágicamente LastKnownPosition con precisión perfecta.

El receptor obtiene como máximo evidencia social razonable:

- identidad del emisor si es conocida/visible por los contratos existentes;
- tipo de alerta;
- dirección/sector aproximado comunicado;
- timestamp/age;
- confianza/calidad sólo si existe un consumer real;
- afiliación/contexto suficiente para decidir si debe prestar atención.

La posición/sector comunicado debe provenir de una **observación legítima del emisor**, no de leer directamente el transform actual de un target oculto.

## Reacción esperada del receptor

Ejemplo:

1. Red A reconoce al Player mediante Perception normal.
2. Red A grita una alerta de combate.
3. Red B está dentro del rango efectivo pero mirando en otra dirección.
4. Red B recibe el estímulo local.
5. Red B interrumpe Ambient/Social de baja prioridad cuando corresponda.
6. Red B orienta cuerpo/gaze hacia el sector aproximado comunicado.
7. Red B usa **su propia** Perception/FOV/LOS para intentar confirmar.
8. Si obtiene evidencia válida, Recognition/Threat/Encounter continúan por las autoridades normales.
9. Si no confirma, puede permanecer Alerted/investigar brevemente según el behavior disponible, pero no recibe conocimiento perfecto.
10. Red C, fuera del rango efectivo, no cambia de conducta.

## Anti-propagación infinita

Este punto es obligatorio.

No queremos:

`A avisa a B → B retransmite automáticamente a C → C a D → ... → toda la facción/mapa queda alertada`

Para V1:

- **recibir SharedContact no autoriza por sí solo a retransmitirlo**;
- sólo un actor que obtenga una observación/recognition legítima propia puede originar una nueva alerta equivalente;
- el estímulo tiene TTL corto y no persiste como conocimiento global;
- no existe flood-fill de afiliación;
- no existe broadcast por sector/mapa;
- no existe relay automático de relay.

Si en el futuro se implementan radios, teléfonos, alarmas, sirenas o redes militares, deberán ser consumers/sistemas explícitos con sus propios alcances y reglas; no ampliar silenciosamente este bark local.

## Relación con percepción y atención

SharedContact **orienta atención; no reemplaza percepción**.

La intención es permitir:

`escucho alerta → miro/investigo hacia allí → confirmo por mis propios sentidos`

y evitar:

`escucho alerta → sé exactamente quién es el enemigo y dónde está`.

Esto es coherente con IMPL-0071: tanto incoming damage como una alerta aliada pueden cambiar orientación/behavior sin regalar omnisciencia.

## Prioridad / Behavior Ownership

La alerta social es preemptable y debe respetar prioridades existentes/futuras.

Puede interrumpir:

- Ambient;
- Social/conversación;
- navegación rutinaria de baja prioridad cuando sea necesario para prestar atención.

No debe imponerse ciegamente sobre:

- Dead/Unconscious/Incapacitated;
- Flee/Panic activo;
- otra emergencia explícitamente de mayor prioridad;
- Encounter ya más informado que el SharedContact recibido.

Un NPC que ya combate a un enemigo visible no debe abandonar ese combate sólo porque otro aliado gritó sobre otra cosa, salvo que una política futura explícita lo justifique.

## Data-driven

Cuando exista implementación, el contrato debe permitir datos como:

- categoría/intensidad de voz;
- max hearing radius;
- attenuation/occlusion simple;
- TTL;
- elegibilidad por afiliación/relación;
- cooldown/anti-spam.

Evitar:

- radios hardcodeados por NPC concreto;
- checks por nombre de profile;
- búsqueda global de todos los actores del mapa;
- polling costoso por frame;
- segunda autoridad de Threat/Perception.

## Observabilidad

Diagnostics/F6 deberían poder mostrar al menos:

- último SharedContact recibido;
- emisor;
- distancia;
- edad/TTL;
- accepted/rejected;
- motivo de rechazo: out of range, occluded, wrong affiliation, higher-priority behavior, stale, etc.;
- sector/dirección aproximada recibida;
- si la posterior adquisición hostil provino de Perception propia;
- si una retransmisión fue bloqueada por no existir observación propia.

Nunca registrar `perceived enemy` sólo porque se recibió el bark.

## Acceptance V1

### Caso A — aliado cercano

- Red A reconoce legítimamente al Player y emite una alerta.
- Red B está dentro del rango efectivo y mirando en otra dirección.
- B recibe la alerta, sale de indiferencia Ambient/Social si corresponde y orienta atención hacia el sector comunicado.
- B no recibe Threat ni posición exacta por la alerta sola.
- Si B obtiene FOV/LOS válido, Recognition/Encounter continúan normalmente.

### Caso B — fuera de rango

- Red C está claramente fuera del alcance máximo.
- La alerta no cambia su behavior, attention, Search, Threat ni memoria.

### Caso C — oclusión

- Un receptor separado por obstrucción suficiente no recibe la misma calidad/efectividad que uno cercano sin bloquear.
- V1 puede usar una aproximación simple; no requiere acústica avanzada.

### Caso D — anti-chain

- B recibe una alerta de A sin ver al enemigo.
- B **no** retransmite automáticamente SharedContact a C.
- Sólo después de que B obtenga percepción/reconocimiento propio puede originar una nueva alerta local.

### Caso E — no omnisciencia

- El enemigo abandona el sector observado antes de que B mire.
- B no obtiene su posición actual por SharedContact y debe depender de Perception/Search/investigación normal.

## Límites del primer slice

- No sistema acústico universal.
- No propagación kilométrica.
- No radio/telefonía.
- No sirenas/alarmas globales.
- No tactical squad network.
- No exact enemy reveal.
- No nueva Threat authority.
- No flood-fill por afiliación.
- No retransmisión automática.
- No polling global por frame.
- No abrir este scope en paralelo con el scope activo.

## Relación

- `IMPL-0070 — Social Conversation V2`
- `IMPL-0071 — NPC Incoming Damage Awareness / Damage-Direction Reaction`
- `IMPL-0072 — NPC Flee / Panic Behavior`
- ActorBarkPresenter / Barks
- ActorAffiliationComponent
- ActorVisualPerceptionService
- ActorThreatAcquisitionController
- HumanEncounterAIController
- ActorBehaviorController / Behavior Ownership
- Gaze/orientation
- Search/LKP

---

**Nota de integración:** esta entrada debe permanecer como contrato de SharedContact local hasta que un scope propio autorice su implementación. Los rangos numéricos indicados son tuning inicial sugerido, no valores finales de balance.
