# IMPL-0068 — NPC Flee / Panic Behavior data-driven

- **Estado:** `PLANNED / DEFERRED — AFTER CURRENT SOCIAL/BARKS CLOSEOUT`
- **Fecha/origen:** 2026-09-19 — dirección aprobada por Mauro durante pruebas manuales de combate NPC.
- **Naturaleza:** backlog futuro aprobado. No autoriza implementación inmediata ni trabajo paralelo al scope actual.

## Qué queremos

Agregar una respuesta de **huida del combate** para NPCs que, bajo ciertas condiciones, decidan priorizar supervivencia y escapar de la amenaza en vez de continuar peleando.

La huida no debe ocurrir siempre ni ser idéntica para todos los actores.

Factores posibles, sólo cuando estén respaldados por autoridades reales:

- daño acumulado;
- heridas/severidad;
- dolor;
- bleeding/estado fisiológico;
- pánico/fear cuando exista como estado productivo;
- desventaja táctica observable;
- traits/tags/perfil del actor.

## Decisión data-driven

La tendencia a huir debe ser configurable mediante datos del NPC.

Ejemplo de intención:

- un actor con trait/tag equivalente a `coward` tiene mayor tendencia a huir;
- un actor más resistente/valiente puede tolerar más daño antes de hacerlo;
- perfiles especiales pueden modificar thresholds/probabilidad.

Esto debe resolverse mediante el sistema de actor profiles/traits/tags o el seam data-driven mínimo que exista cuando se active el scope.

Prohibido hardcodear dentro del controlador de combate:

`if (trait == "coward") flee = true;`

Si todavía no existe un contrato productivo de traits que satisfaga el consumer, el primer slice debe crear **el seam mínimo necesario**, no un framework universal de personalidad.

## Variabilidad y determinismo

"Puede huir" no significa tirar RNG cada frame.

La decisión debe ser reproducible y estable:

- evaluar en eventos/transiciones concretas;
- usar seed/contexto determinista si existe componente probabilístico;
- aplicar hysteresis/cooldown para evitar Fight ↔ Flee oscilando cada frame;
- no reevaluar continuamente sin cambio material de contexto.

## Estado Flee / Panic

Durante una huida activa:

- escapar es la prioridad conductual principal;
- el actor intenta aumentar distancia respecto de la amenaza conocida o del sector peligroso;
- puede sprintar/correr si su estado físico y locomoción lo permiten;
- no debe detenerse a combatir normalmente salvo transición explícita;
- no debe iniciar Social;
- no debe iniciar acciones de baja prioridad incompatibles.

### Daño durante la huida

"Ignore el daño" significa **ignorar la reacción conductual secundaria**, no ignorar la medicina.

Si recibe otro disparo mientras huye:

- el daño, heridas, bleeding, dolor, KO/death se aplican normalmente;
- puede emitir feedback/bark compatible si la prioridad lo permite;
- NO cancela la huida sólo para detenerse y mirar hacia la nueva dirección;
- NO ejecuta la reacción estándar de `IMPL-0067` mientras Panic/Flee tenga prioridad.

Si el daño lo incapacita o mata, las autoridades médicas/terminales siguen ganando.

## Dirección de escape sin telepatía

El destino de huida debe derivarse de conocimiento legítimo:

- Threat reconocido;
- Last Known Position;
- percepción actual;
- SharedContact válido;
- incoming-damage direction aproximado;
- otros estímulos reales futuros.

Si el actor sólo sabe que recibió fuego desde una dirección aproximada, puede escapar en sentido opuesto/seguro sin conocer la posición exacta del atacante.

No otorgar coordenadas mágicas.

## Destino / navegación

V1 debe mantenerlo simple:

- elegir un punto navegable que aumente distancia del peligro;
- validar con Navigation/NavMesh existentes;
- replanificar sólo cuando sea necesario;
- evitar oscillation/path spam;
- si no existe ruta válida, degradar limpiamente a otra conducta definida.

Cover selection, squads, safe zones complejas y path tactical scoring son scopes posteriores.

## Salida de Panic/Flee

Debe existir un criterio claro de salida, por ejemplo combinación de:

- distancia suficiente;
- tiempo sin contacto;
- amenaza no percibida;
- descenso de panic;
- cambio de estado médico.

La salida no debe forzar automáticamente volver a Fighting si el actor ya no tiene evidencia válida del enemigo.

Puede degradar a Search, recovery, self-treatment o Ambient según las autoridades existentes.

## Traits / tags / perfiles

Diseñar el consumer para que distintos NPC puedan ser configurados sin código nuevo.

Ejemplos conceptuales de modificadores futuros:

- flee tendency;
- panic threshold;
- pain tolerance;
- willingness to re-engage;
- minimum safe distance.

Los nombres finales y formato de datos deben seguir el sistema productivo real; estos ejemplos no autorizan crear todos los stats a la vez.

## Observabilidad

F6/diagnostics deben mostrar:

- Flee/Panic active;
- trigger principal;
- threat/danger source type;
- escape direction/destination;
- trait/data modifiers aplicados;
- enter/exit reason;
- suppressed damage-reaction reason;
- replan count/budget.

## Acceptance V1

- Bajo una condición configurada, un NPC puede elegir huir en vez de pelear.
- Otro perfil/trait puede producir una tendencia diferente bajo el mismo setup.
- La decisión es reproducible con seed/contexto controlado.
- El actor aumenta distancia usando Navigation real.
- Damage continúa aplicándose mientras huye.
- Recibir nuevos impactos no cancela Flee para girarse/investigar.
- KO/death siguen preemptando.
- Social y acciones de baja prioridad no interrumpen la huida.
- Sin telepatía de posición enemiga.
- No Fight ↔ Flee oscillation por frame.

## Primer slice acotado

1. Auditar el seam actual de Behavior Ownership + actor profile/tags/traits.
2. Definir un único trigger real y verificable de Flee.
3. Implementar un único modificador data-driven de tendencia/threshold.
4. Elegir escape direction desde Threat/LKP o incoming-damage direction legítima.
5. Navegar y mantener prioridad hasta criterio de salida.
6. Integrar explícitamente la supresión de `IMPL-0067` durante Flee.
7. Añadir diagnostic reproducible + observabilidad mínima.

No agregar en ese slice:

- morale de escuadrón;
- surrender;
- cover tactical planner;
- relaciones;
- liderazgo;
- personalidad universal;
- squad retreat;
- safe-zone graph;
- decenas de traits.

## Límites

- No invulnerabilidad durante pánico.
- No inmunidad al dolor/bleeding.
- No teleport.
- No omnisciencia.
- No RNG por frame.
- No personality framework universal preventivo.
- No implementación simultánea con el scope actual.

## Relación

- `IMPL-0067 — NPC Incoming Damage Awareness / Damage-Direction Reaction`
- ActorBehaviorController / Behavior Ownership
- HumanEncounterAIController
- Actor profiles / tags / future trait seam
- Health/Medical/Condition
- Navigation
- Perception/Search/LKP/SharedContact

---

**Nota de integración:** registrar/referenciar esta entrada en `Docs/Implementation_Backlog.md` durante un documentation closeout seguro cuando el checkout local no tenga cambios conflictivos en ese archivo.
