# IMPL-0066 — Social Conversation V2: branching, multi-turn y memoria contextual

- **Estado:** `PLANNED / AFTER CURRENT SOCIAL V1 CLOSEOUT`
- **Fecha/origen:** 2026-09-19 — feedback manual de Mauro durante validación de Social V1.
- **Naturaleza:** backlog futuro aprobado. No autoriza abrir este scope antes de cerrar por completo el scope Social/Barks actual.

## Qué queremos

Evolucionar las interacciones sociales NPC↔NPC desde intercambios fijos de pregunta/respuesta hacia conversaciones **data-driven, variables y contextuales**, manteniendo comportamiento determinista, observable y acotado.

La conversación no debe ser siempre:

`Pregunta A → Respuesta A`

sino permitir:

- varias respuestas válidas para una misma línea;
- continuaciones opcionales;
- conversaciones de longitud variable;
- ramificaciones por contexto real del actor;
- referencias a hechos recientes o estados persistentes reales;
- variación suficiente para evitar que las mismas parejas repitan siempre el mismo diálogo.

Ejemplo conceptual:

```text
¿Viste algo raro?
→ Nada todavía.
→ Aún nada.
→ No vi nada raro.
→ Quizás. Uno de los nuestros estaba preocupado.
```

Algunas respuestas pueden abrir otro turno:

```text
¿Viste algo raro?
→ No vi nada raro.
→ La verdad que otro de los nuestros estaba preocupado porque creyó ver algo.
→ Sí, me enteré también.
```

Los ejemplos son de intención. El contenido final debe ser data-driven y no depender de cadenas hardcodeadas dentro del controlador.

## Conversaciones multi-turn

La estructura futura debe poder representar, de forma simple:

`ConversationTopic → Turn → ResponseVariants → OptionalContinuation`

Cada turno puede:

- terminar la conversación;
- seleccionar una de varias respuestas válidas;
- abrir otro turno;
- consultar contexto estructurado antes de habilitar una variante.

No todas las conversaciones deben tener la misma longitud. Una charla puede terminar en 2 líneas y otra extenderse a 4–6 líneas si el contexto lo permite.

Debe existir un límite/budget para impedir conversaciones excesivamente largas o loops.

## Selección y determinismo

La selección entre variantes debe:

- conservar determinismo bajo seed/contexto reproducible;
- evitar RNG global no controlada;
- respetar cooldowns y anti-spam;
- evitar repetición inmediata de la misma pareja + mismo intercambio cuando existan alternativas;
- conservar Social como actividad de baja prioridad frente a combate, daño, reconocimiento y otras necesidades de gameplay.

No aumentar llamadas por frame sólo para buscar más variedad.

## Memoria contextual estructurada

Los NPC pueden incorporar a conversaciones hechos reales de su estado actual o reciente.

La V1 de esta memoria debe ser **estructurada y verificable**, no texto libre inventado.

Ejemplos de hechos elegibles:

- heridas actualmente presentes;
- región corporal dañada;
- dolor/bleeding relevante cuando exista un criterio productivo;
- combate reciente;
- contacto enemigo reciente;
- haber quedado incapacitado recientemente;
- aliado caído o evento reciente sólo cuando exista una autoridad real que lo exponga;
- interlocutor/actor relacionado si existe identidad/nombre productivo real.

No inventar recuerdos, nombres, eventos o relaciones que el runtime no pueda respaldar.

## Referencias dinámicas a anatomía

Este punto es obligatorio.

Los textos de Social y Barks **NO deben hardcodear nombres de partes del cuerpo** para cada región.

Incorrecto:

```text
"Me dispararon en el brazo."
"Me dispararon en la pierna."
"Me dispararon en el torso."
```

Correcto conceptualmente:

```text
"Me dieron en {BodyRegionDisplayName}."
"No te imaginás cómo me duele {BodyRegionDisplayName}."
```

`{BodyRegionDisplayName}` debe resolverse desde la autoridad anatómica/localización correspondiente a la región real.

Si en el futuro se agregan:

- manos;
- dedos;
- mandíbula;
- pies;
- otras regiones;

las mismas plantillas deben seguir funcionando sin crear una familia nueva de barks por región.

## Contrato de token/contexto

El sistema debería admitir tokens/context values tipados, por ejemplo:

- `BodyRegionDisplayName`
- `PartnerDisplayName` sólo si existe naming productivo real;
- `RecentEventType`
- otros futuros valores con consumer real.

Los tokens deben resolverse **antes** de entregar el texto aceptado a `ActorBarkPresenter`.

El presenter sigue siendo responsable de:

- aceptar/rechazar bark;
- prioridad/cooldown;
- mostrar el texto final exacto;
- registrar exactamente ese mismo texto final en `[AI][BARK]`.

La Console debe seguir mostrando el texto exacto que vio el jugador.

## DamageReceived bark

El bark inmediato al recibir daño debe reutilizar la misma resolución dinámica de anatomía.

Conceptualmente:

```text
"¡Me dieron en {BodyRegionDisplayName}!"
```

No mantener listas duplicadas por BodyRegion.

El `BodyRegion` estructurado debe seguir existiendo en metadata/logging aunque el texto visible use un display name localizado.

## Social + heridas

Una conversación posterior al combate puede consultar heridas reales del actor.

Ejemplo conceptual:

```text
— No te imaginás cómo me duele {BodyRegionDisplayName}.
— ¿Seguís sangrando?
— Ya no tanto.
```

Sólo habilitar líneas que sean coherentes con el estado real.

Una línea sobre bleeding no debe aparecer si el actor no tiene bleeding relevante, salvo que el texto se refiera explícitamente a un estado pasado respaldado por una memoria estructurada real.

## Data-driven

El contenido conversacional debe vivir en datos/configuración apropiada y no expandir `ActorSocialInteractionController` con grandes bloques de strings o switch por tema/región.

C# debe resolver:

- elegibilidad;
- contexto;
- branching;
- selección;
- reservas;
- lifecycle;
- prioridad;
- tokens;
- observabilidad.

Los datos deben definir:

- topics;
- lines;
- response variants;
- continuations;
- requirements/context tags;
- weights si realmente hacen falta.

No crear un framework universal antes de tener el primer conjunto real de conversaciones.

## Observabilidad

F6/diagnostics deben poder reconstruir al menos:

- Topic/ConversationId;
- ExchangeId;
- Turn index;
- Speaker;
- Partner;
- selected variant;
- resolved final text;
- contextual facts utilizados;
- motivo de finalización/preemption.

`[AI][BARK]` continúa siendo la evidencia de lo realmente dicho.

Una línea rechazada por prioridad/cooldown no debe registrarse como pronunciada.

## Interrupciones

Una conversación debe cancelarse limpiamente si aparece una prioridad superior, incluyendo:

- daño recibido;
- hostile recognition;
- SharedContact/combat awareness;
- muerte/incapacitación;
- actor/partner inválido;
- otras interrupciones ya soportadas por Social V1.

Una conversación multi-turn no puede retener a un actor artificialmente si el contexto deja de ser válido.

## Alcance del primer slice

Cuando este backlog se active, el primer slice debe ser pequeño y terminable:

1. definir representación data-driven mínima de conversation graph;
2. migrar las conversaciones Social actuales;
3. permitir varias respuestas a una misma línea;
4. permitir una continuación opcional de varios turnos;
5. mantener deterministic selection + anti-repeat;
6. validar preemption;
7. mantener exact bark logging;
8. añadir una sola familia contextual real basada en `BodyRegion`/herida.

No intentar en el primer slice:

- memoria social general;
- relaciones complejas;
- personalidad completa;
- conocimiento de mundo libre;
- LLM runtime;
- generación procedural de diálogo;
- persistencia de recuerdos arbitrarios;
- sistema de nombres si todavía no existe un consumer productivo;
- decenas de topics.

## Trigger / dependencias

No comenzar hasta que el scope actual de Social/Barks esté completamente cerrado:

`implementation → validation → documentation closeout → review → commit → push → HEAD == origin/dev → NEXT EXACT STEP`

Dependencias/reutilización esperada:

- IMPL-0061 — Interacciones sociales ligeras NPC↔NPC;
- `ActorSocialInteractionController`;
- `ActorBarkPresenter`;
- exact `[AI][BARK]` logging;
- `ActorMedicalStateComponent` / anatomía localizada;
- autoridad de display/localización de `BodyRegion`;
- behavior ownership/preemption existente.

## Acceptance objetivo

El slice inicial puede considerarse aceptable cuando:

- una misma pregunta tiene al menos varias respuestas posibles;
- existe al menos una conversación de más de dos turnos;
- selección reproducible bajo seed/contexto;
- no hay loops/repetición inmediata evidente;
- combate/daño siguen preemptando Social;
- una línea contextual de herida usa la región corporal real;
- ninguna plantilla Social/DamageReceived necesita hardcodear nombres de regiones anatómicas;
- agregar una nueva `BodyRegion` no requiere crear barks específicos para esa región;
- texto visual y `[AI][BARK]` coinciden exactamente;
- diagnostics cubren branching, tokens, preemption y determinismo.

## Límites

- No LLM runtime.
- No diálogo libre generado.
- No lore automático.
- No memoria narrativa inventada.
- No hardcode por parte corporal.
- No relationship framework universal preventivo.
- No segundo sistema de anatomía.
- No segunda autoridad de bark/logging.
- No empezar este scope en paralelo con el cierre Social/Barks actual.

## Relación

- `IMPL-0061 — Interacciones sociales ligeras NPC↔NPC`
- Localized Health / Medical State
- Bark observability
- NPC behavior ownership
- futura localización/naming si aparece como consumer real

---

**Nota de integración:** este archivo captura el backlog detallado sin tocar `Docs/Implementation_Backlog.md`, que actualmente puede tener cambios locales no publicados dentro del scope Social/Barks. Durante el documentation closeout de ese scope debe registrarse/referenciarse este IMPL en el backlog canónico sin perder este detalle.
