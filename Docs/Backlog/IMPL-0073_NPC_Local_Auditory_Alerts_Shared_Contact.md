# IMPL-0073 — NPC Local Auditory Alerts / Shared Hostile Contact

- **Estado:** `PLANNED / DEFERRED — REQUIRES OWN FUTURE SCOPE`
- **Fecha/origen:** 2026-10-03 — decisión de diseño de Mauro durante revisión de percepción, barks y coordinación NPC.
- **Naturaleza:** backlog futuro aprobado. Este documento define la propagación local de alertas verbales entre NPCs; no autoriza implementación inmediata ni trabajo paralelo con el scope activo.
- **Dependencia conceptual:** Social/Barks puede emitir la alerta; Perception/Recognition sigue siendo la autoridad que confirma visualmente al enemigo.

## Problema a evitar

Cuando un NPC reconoce un enemigo es razonable que grite para avisar a aliados cercanos. Ese aviso **no puede comportarse como un broadcast global** ni informar a todos los NPC del mapa, independientemente de distancia, obstáculos o contexto.

Tampoco debe convertir un bark en una forma de telepatía:

- oír `"¡ENEMIGO!"` no equivale automáticamente a conocer `ActorInstanceId` del hostil;
- no debe revelar coordenadas exactas del enemigo;
- no debe asignar `Threat` confirmado sin evidencia propia suficiente;
- no debe saltarse Perception/FOV/LOS/Recognition.

## Contrato de conocimiento

Separar dos conceptos:

1. **Auditory stimulus:** el receptor percibe que alguien gritó desde una dirección/origen aproximado.
2. **SharedContact:** el receptor entiende que un aliado cercano está alertando sobre una amenaza aproximada en cierto sector.

`SharedContact` debe poder orientar atención y comportamiento, pero no reemplaza percepción propia.

Flujo esperado:

`NPC A reconoce enemigo → emite bark de alerta → aliados dentro de alcance reciben estímulo auditivo → orientan atención hacia A/sector indicado → usan Perception/FOV/LOS → Recognition/Encounter sólo si obtienen evidencia válida`

## Alcance espacial obligatorio

Toda alerta vocal debe tener un **rango máximo efectivo**. Fuera de ese radio, el evento no se entrega al receptor.

Tuning inicial/provisional para pruebas:

- conversación normal: **8–12 m**;
- bark fuerte: **15–20 m**;
- grito de combate/alarma: **25–35 m**.

Estos valores son únicamente baseline de diseño para validación y **no son balance final**.

El sistema debe permitir tuning data-driven por tipo de emisión/actor/contexto cuando exista consumer real.

## Distancia y obstáculos

V1 debe asumir propagación local y degradable:

- la distancia reduce efectividad/confianza del estímulo;
- paredes/edificios deben poder reducir o bloquear la recepción cuando exista un seam físico razonable;
- no hace falta construir acústica compleja ni simulación de reverberación;
- no implementar ballistic acoustics universal dentro de este scope.

La primera versión puede usar una atenuación/occlusion simple y determinista, siempre que respete el principio de que un NPC a decenas o cientos de metros no recibe la misma información que uno cercano.

## Reacción del receptor

Un NPC que acepta una alerta válida puede:

1. interrumpir o preemptar Ambient/Social de baja prioridad;
2. orientar body/gaze hacia el emisor o sector reportado;
3. elevar temporalmente su atención/alertness;
4. intentar adquirir evidencia usando Perception/Recognition normal;
5. investigar brevemente el sector si el diseño de Search/SharedContact lo permite;
6. entrar a Encounter sólo cuando exista autoridad válida para hacerlo.

Ejemplo:

- Red 1 ve al Player a ~20 m y grita `"¡ENEMIGO!"`.
- Red 2 está a ~10 m de Red 1 y mirando en dirección contraria.
- Red 2 oye la alerta, cancela Ambient, gira hacia el sector indicado e intenta confirmar visualmente.
- Si ve/reconoce al Player: Encounter normal.
- Si no lo ve: atención/investigación acotada, sin conocimiento exacto.
- Red 3 está a ~80 m: **no recibe nada**.

## No propagación infinita

Una alerta recibida **NO debe retransmitirse automáticamente**.

Evitar:

`A avisa a B → B replica a C → C replica a D → toda la región termina alertada`.

Regla V1:

- sólo un actor que **realmente percibe/reconoce** al hostil puede originar una nueva alerta hostil completa;
- recibir `SharedContact` por sí solo no habilita un nuevo broadcast equivalente;
- el contacto compartido debe tener un **TTL corto**;
- expiración, invalidación del emisor o evidencia obsoleta limpian el estímulo;
- no conservar una cadena arbitraria de relay/hops.

Si más adelante se necesita retransmisión táctica, debe ser otro contrato explícito con límite de hops/TTL y consumer real.

## Información transportada

El estímulo compartido puede contener, como máximo, datos equivalentes a:

- posición/dirección del emisor;
- bearing/sector aproximado de la amenaza señalado por el emisor;
- timestamp/age;
- tipo de alerta;
- affiliation/relationship necesaria para decidir si confiar en el aviso;
- confianza/precisión sólo si existe consumer real.

No debe transportar automáticamente:

- posición exacta world-space del enemigo;
- `ActorInstanceId` como amenaza reconocida por el receptor;
- LastKnownPosition perfecta copiada sin degradación;
- acceso directo al transform actual del hostil;
- estado oculto del objetivo.

## Prioridad y preemption

La alerta debe respetar Behavior Ownership.

Puede preemptar actividades bajas como:

- Ambient;
- Social;
- navegación rutinaria compatible.

No debe imponerse sobre:

- Dead/Unconscious/Incapacitated;
- Flee/Panic de mayor prioridad;
- emergency behaviors explícitamente superiores;
- otras reservas de comportamiento que el contrato declare no interrumpibles.

## Data-driven

Evitar radios y respuestas especiales hardcodeadas por NPC concreto.

Preferir datos por tipo de vocalización/contexto, por ejemplo:

- normal conversation;
- bark;
- combat shout;
- future whisper/radio/electronic channel si algún consumer lo requiere.

No crear un framework universal de sonido antes del primer consumer jugable.

## Observabilidad

Diagnostics/F6 deberían poder mostrar:

- emitter ActorInstanceId;
- stimulus type;
- source position/direction;
- effective range;
- receiver distance;
- occlusion/attenuation result;
- accepted/rejected;
- rejection reason;
- age/TTL;
- resulting behavior transition;
- si Recognition posterior provino de percepción propia.

Nunca registrar que el receptor "vio" o "reconoció" al enemigo sólo porque oyó el grito.

## Acceptance V1

- Un NPC que reconoce un enemigo puede emitir una alerta local.
- Un aliado cercano y dentro de rango puede reaccionar aunque estuviera mirando en otra dirección.
- El receptor orienta atención y usa su propia Perception/Recognition.
- Un NPC claramente fuera del radio efectivo no recibe el evento.
- Una alerta no asigna Threat ni posición exacta del hostil por sí sola.
- Un SharedContact recibido no se retransmite automáticamente.
- TTL/expiry evita conocimiento auditivo permanente.
- Distancia y una obstrucción básica pueden reducir/bloquear la recepción.
- El comportamiento es determinista y diagnosticable.

## Límites

- No audio propagation global.
- No radio/comms electrónicos dentro de este slice.
- No simulación acústica avanzada.
- No second Perception system.
- No omnisciencia grupal.
- No auto-relay infinito.
- No tactical squad planner completo.
- No threat assignment automático sólo por bark.
- No abrir este scope en paralelo con otro scope activo.

## Relación

- `IMPL-0070 — Social Conversation V2`
- `IMPL-0071 — NPC Incoming Damage Awareness / Damage-Direction Reaction`
- `IMPL-0072 — NPC Flee / Panic Behavior`
- ActorBarkPresenter / futura baseline Social V1
- ActorVisualPerceptionService / Recognition
- ActorBehaviorController / Behavior Ownership
- HumanEncounterAIController
- Search/LKP / SharedContact

---

**Nota de integración:** registrar resumidamente esta entrada en `Docs/Implementation_Backlog.md` como IMPL-0073.