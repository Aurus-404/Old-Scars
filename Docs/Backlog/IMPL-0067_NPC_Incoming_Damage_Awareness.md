# IMPL-0067 — NPC Incoming Damage Awareness / Damage-Direction Reaction

- **Estado:** `PLANNED / DEFERRED — AFTER CURRENT SOCIAL/BARKS CLOSEOUT`
- **Fecha/origen:** 2026-09-19 — observación manual de Mauro durante pruebas 1v1 de Social/Barks/KO.
- **Naturaleza:** backlog futuro aprobado. No autoriza abrir este scope antes de cerrar completamente el scope actual.

## Problema observado

Cuando un NPC recibe daño mientras se está desplazando puede continuar caminando casi en línea recta, sin una reacción conductual clara al hecho de haber sido alcanzado.

El actor ya puede registrar el daño médicamente y producir feedback como `DamageReceived`, pero falta una capa de **conciencia del estímulo entrante** que pueda alterar orientación/búsqueda sin regalar conocimiento omnisciente del atacante.

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

Caso básico:

`Ambient/Moving → recibe disparo desde atrás → stop/pause breve → gira hacia dirección aproximada → Perception intenta confirmar → Search/Encounter según evidencia real`

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
- No abrir este scope en paralelo con el cierre actual.

## Relación

- HumanEncounterAIController
- ActorBehaviorController / Behavior Ownership
- Perception/FOV/LOS
- Search/LKP
- CombatResolution / DamageReceived
- `IMPL-0068 — NPC Flee / Panic Behavior`

---

**Nota de integración:** registrar/referenciar esta entrada en `Docs/Implementation_Backlog.md` durante un documentation closeout seguro cuando el checkout local no tenga cambios conflictivos en ese archivo.
