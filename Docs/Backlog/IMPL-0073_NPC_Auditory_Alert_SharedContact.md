# IMPL-0073 — NPC Auditory Alert / Local SharedContact

- **Estado:** `PLANNED / DEFERRED — REQUIRES OWN FUTURE SCOPE`
- **Fecha/origen:** 2026-10-03 — decisión de diseño de Mauro durante revisión de NPC combat awareness.
- **Naturaleza:** backlog futuro aprobado. Registrar esta intención no abre implementación inmediata ni modifica el scope activo.
- **Objetivo:** permitir que un NPC que detecta/reconoce una amenaza pueda advertir verbalmente a aliados cercanos mediante un estímulo auditivo localizado, sin convertir el bark en un broadcast global ni en una segunda autoridad de Perception/Threat.

## Problema que queremos evitar

Cuando un NPC ve un enemigo es natural que grite una alerta como:

`¡ENEMIGO!`
`¡CONTACTO!`
`¡AHÍ!`

Los NPC cercanos deberían poder prestar atención a esa advertencia. Sin un contrato espacial explícito, un sistema de SharedContact podría degenerar en conocimiento global: un NPC a cientos de metros o kilómetros reaccionando a un grito que físicamente no podría haber oído.

Esto está prohibido. Las alertas verbales deben ser **locales, físicas y acotadas**.

## Contrato conceptual

Flujo esperado:

`NPC A reconoce enemigo → emite bark de alerta → genera estímulo auditivo local → NPC B dentro de alcance lo recibe → orienta atención hacia la fuente/sector → usa Perception propia para confirmar → Encounter/Search según evidencia real`

Escuchar la alerta **NO** equivale automáticamente a:

- adquirir `Threat`;
- conocer `ActorInstanceId` del enemigo;
- conocer coordenadas exactas;
- compartir automáticamente `LastKnownPosition` perfecta;
- saltarse FOV/LOS/Recognition;
- alertar a toda la facción/mapa.

El receptor obtiene evidencia social/auditiva razonable, no telepatía.

## Rango auditivo V1 — tuning provisional

Los siguientes números son **valores iniciales de prueba**, no balance final:

- conversación normal: aproximadamente **8–12 m**;
- bark fuerte: aproximadamente **15–20 m**;
- grito de combate/alarma: aproximadamente **25–35 m**;
- fuera del radio efectivo: el receptor no obtiene el estímulo.

El radio final debe quedar data-driven y depender del tipo/intensidad del evento sonoro cuando exista el consumer productivo.

No asumir estos valores como canon de balance hasta validarlos en Play.

## Distancia y obstáculos

La recepción no debe tratar el radio como una esfera perfecta omnisciente.

Como mínimo:

- la distancia degrada la utilidad/claridad del estímulo;
- paredes, edificios y obstáculos relevantes deberían reducir o bloquear parcialmente la recepción cuando exista un modelo acústico mínimo razonable;
- no hace falta una simulación acústica completa para V1;
- no introducir raycasts/acoustic propagation costosa por actor por frame si un evento puntual puede resolverlo de forma acotada.

La V1 puede empezar con un modelo simple y determinista si preserva el contrato de localidad.

## Qué información comparte la alerta

Separar explícitamente:

### 1. Sonido

El receptor puede saber:

- que oyó una voz/grito;
- posición aproximada de la fuente emisora;
- intensidad/tipo de alerta;
- timestamp/age.

### 2. SharedContact / combat awareness

Si el bark representa una advertencia hostil válida, el receptor puede además recibir una pista acotada equivalente a:

- `hostile alert`;
- sector/dirección aproximada señalada por el emisor, si existe evidencia real para ello;
- confianza/age sólo si existe un consumer real.

Esto puede:

- interrumpir Ambient/Social;
- provocar orientación de body/gaze;
- provocar estado Alerted/investigación;
- dirigir Perception hacia un sector razonable.

No debe asignar automáticamente una amenaza confirmada.

## Regla de confirmación

Un NPC avisado debe utilizar las autoridades existentes:

- Perception/FOV/LOS;
- Recognition/Threat Acquisition;
- Gaze/orientation;
- Search/LKP;
- Behavior Ownership.

Ejemplo:

1. Red 1 ve al Player a 20 m y lo reconoce.
2. Red 1 grita `¡ENEMIGO!`.
3. Red 2 está a 10 m de Red 1 y mirando en dirección contraria.
4. Red 2 recibe la alerta auditiva.
5. Red 2 abandona la indiferencia Ambient, mira hacia la fuente/sector indicado e intenta observar.
6. Si obtiene Perception válida del Player, Recognition/Threat Acquisition normales continúan.
7. Si no obtiene evidencia visual válida, puede permanecer Alerted o investigar brevemente según el contrato futuro, pero no conoce mágicamente la posición exacta del Player.
8. Red 3 está a 80 m: no recibe nada.

## Prohibición de propagación infinita

No queremos:

`A avisa a B → B avisa a C → C avisa a D → todo el mapa conoce el contacto`

V1 debe impedir cadenas ilimitadas.

Regla inicial recomendada:

- un NPC **no retransmite automáticamente** una alerta sólo porque la escuchó;
- sólo un actor que obtiene su propia evidencia válida de Perception/Recognition puede originar una nueva alerta hostil completa;
- el estímulo compartido tiene TTL corto y no persiste indefinidamente;
- no existe flood global por facción;
- no existe radio/mesh de combate omnisciente.

Si en el futuro se autoriza comunicación por radio, mensajeros u otros sistemas de largo alcance, deberán usar contratos propios y no reutilizar silenciosamente este bark local.

## Relación con barks

El bark visible/audible y el estímulo gameplay deben corresponder al mismo evento aceptado.

Si `ActorBarkPresenter` rechaza una línea por prioridad/cooldown:

- no debe fingirse que el actor gritó;
- no debería generarse una alerta auditiva equivalente salvo que exista explícitamente otro canal gameplay independiente.

Si la alerta fue efectivamente emitida:

- el texto/log debe seguir las reglas de Social/Barks;
- el evento auditivo debe transportar metadata estructurada, no parsear el string visible.

Nunca inferir gameplay leyendo palabras como `"enemigo"` desde texto localizado.

## Data-driven

Cuando exista el consumer productivo, datos/configuración deberían poder definir al menos:

- categoría de voz/alerta;
- radio base;
- intensidad;
- TTL;
- cooldown;
- afiliaciones/receptores elegibles;
- comportamiento de degradación si se necesita.

Evitar:

- radios hardcodeados por NPC concreto;
- checks por nombre de actor;
- strings de bark usados como autoridad;
- broadcast a todos los actores registrados.

## Performance / escala

El sistema debe escalar a mapas grandes sin consultar a todos los NPC del mundo.

Preferir una consulta espacial/event-driven acotada alrededor del emisor.

Un grito local no debe despertar, iterar ni cargar actores a kilómetros de distancia.

Streaming/offline actors fuera de la vecindad activa no deben reaccionar a un bark local salvo que un futuro sistema de simulación abstracta tenga un consumer explícito y compatible.

## Observabilidad

Diagnostics/F6 deberían poder mostrar, para la última alerta relevante:

- emitter;
- alert type;
- emission position;
- base/effective range;
- receiver distance;
- accepted/rejected;
- rejection reason;
- age/TTL;
- dirección/sector compartido si existió;
- si el receptor luego confirmó al enemigo mediante su propia Perception;
- si la alerta fue retransmitida o no.

Nunca registrar `saw enemy` para un NPC que sólo escuchó una advertencia.

## Acceptance V1

- Un NPC que reconoce un hostil puede emitir una alerta audible local.
- Un aliado cercano dentro del rango efectivo puede abandonar Ambient/Social y orientar su atención.
- Un aliado fuera del rango máximo no recibe el evento.
- La alerta recibida no concede `Threat` ni posición exacta por sí sola.
- El receptor confirma al hostil mediante Perception/Recognition normal.
- Un receptor que sólo oyó la alerta no la retransmite automáticamente.
- No existe propagación ilimitada por cadena.
- Obstáculos/distancia no aumentan mágicamente la información.
- El sistema no itera sobre todo el mapa por cada bark.
- Diagnostics reproducibles distinguen claramente `heard alert` de `perceived/recognized hostile`.

## Límites

- No simulación acústica completa.
- No radios/comunicaciones de largo alcance.
- No squad tactical network global.
- No faction-wide omniscience.
- No segunda autoridad de Threat.
- No parseo de texto visible para gameplay.
- No persistencia larga de contactos compartidos.
- No abrir este scope en paralelo con el scope activo.

## Relación

- `IMPL-0070 — Social Conversation V2`
- `IMPL-0071 — NPC Incoming Damage Awareness / Damage-Direction Reaction`
- `IMPL-0072 — NPC Flee / Panic Behavior`
- ActorBarkPresenter / Social/Barks
- Perception / FOV / LOS
- Recognition / Threat Acquisition
- Gaze / body orientation
- Search / LKP
- ActorBehaviorController / Behavior Ownership
- futuras comunicaciones de largo alcance, si alguna vez se autorizan, como sistema separado

---

**Nota de integración:** registrar también esta entrada resumidamente en `Docs/Implementation_Backlog.md`.