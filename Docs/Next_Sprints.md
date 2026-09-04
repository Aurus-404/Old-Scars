# Old Scars — Next Sprints

Este documento contiene sólo los próximos trabajos reales. `Current_Milestone.md` resume el estado; `Issue_Registry.md` conserva defectos; `Implementation_Backlog.md` conserva mecánicas/mejoras menores aprobadas para después; `NPC_AI_Sanitation_Plan.md` mantiene la secuencia completa del bloque; `Prueba_3_Findings.md` conserva la evidencia manual más reciente.

## Próximo trabajo

### 1. Prueba 3 Correction Pass B — F6 correctness + multi-NPC mínimo

Estado: `NEXT`.

Corregir dos problemas que hoy vuelven engañosa la observabilidad:

- distinguir `CURRENT` de `LAST` perception/LOS/search evidence;
- current FOV/Gaze debe salir del eye/origin actual del actor, no de un snapshot viejo que quedó atrás mientras caminaba;
- Dead/Inactive no debe mostrar un snapshot histórico como si fuese current perception;
- gaze/FOV/LOS world visuals deben poder mostrarse para varios/todos los NPCs simultáneamente;
- `Selected NPC` sólo elige el inspector detallado.

No duplicar raycasts/perception para debug; consumir datos productivos/read-only.

Referencias: `ISSUE-0010`, `ISSUE-0011`, findings nuevos de Prueba 3.

### 2. Prueba 3 Correction Pass C — KO / combat memory continuity

Estado: `NEXT AFTER F6`.

Problema confirmado: incapacidad temporal hace que atacante libere threat y que el incapacitado limpie su encounter/memoria, produciendo `KO → Ambient → recovery → redescubrimiento → encounter nuevo`.

Contrato V1 deseado:

- un incapacitado/noqueado deja de ser amenaza activa y no recibe ataques deliberados;
- atacante y noqueado conservan quién era el enemigo del encounter reciente;
- recovery puede reanudar contexto hostil sin redescubrimiento artificial;
- memoria no da posición actual/wallhack; Perception/LKP/Search siguen siendo autoridad espacial;
- muerte sigue siendo terminal;
- no crear memory framework/blackboard/planner general.

Referencia: `IMPL-0014` y `Prueba_3_Findings.md`.

### 3. Prueba 3 Correction Pass D — Minimum KO dwell

Estado: `NEXT AFTER KO MEMORY`.

Agregar un mínimo real-time configurable durante el cual un knockout/unconscious no puede recuperar capacidad activa sólo porque el `WorldClock` avanza acelerado. Una vez cumplido ese mínimo, la fisiología/thresholds vigentes deciden si puede despertar.

No fijar balance final sin playtest y no sustituir `ActorConditionComponent` como autoridad fisiológica.

Referencia: `IMPL-0015`.

### 4. Prueba 3.3 — 1 Blue vs 1 Red limpio

Estado: `GATE BEFORE F8A`.

Condiciones:

- Player Invisible-to-AI ON;
- sólo 1 Blue + 1 Red;
- observabilidad simultánea de ambos;
- registrar arma de cada uno;
- observar KO start, duración real, recovery, memoria del enemigo y reanudación;
- confirmar que el rival no golpea deliberadamente al KO;
- observar LostContact/Search si aparece;
- anotar wounds/regions sin intervención del Player.

Gate: el combate 1v1 debe poder interpretarse sin contaminación del Player ni tooling stale.

### 6. Fase 8A — Aim Bias Evidence

Estado: `QUEUED AFTER PRUEBA 3.3`.

Objetivo: demostrar con evidencia reproducible qué está causando `ISSUE-0008` antes de cambiar gameplay.

Implementar un diagnostic que capture por disparo:

- target y aim source;
- aim point actual (`ActorLocomotionCollider.bounds.center` en el contrato actual);
- proposed human center-mass;
- focus/current spread;
- shot origin/direction;
- hit collider/hit point;
- BodyRegion o miss;
- seed/condición.

Usar mismo shooter, target, arma, distancia, focus, spread y seeds para poder comparar.

**No cambiar todavía:** spread, Focus, distance/movement penalties, burst spread, damage, .303, anatomy ni weapon balance.

Gate: explicar si el punto base actual contribuye materialmente al exceso de piernas/pies o si la evidencia apunta a otra causa.

### 7. Fase 8B — Generic Target-side Primary Aim Point

Estado: `CONDITIONAL / READY`.

Sólo ejecutar si F8A justifica el cambio.

Objetivo: dejar de hacer que `HumanEncounterAIController` adivine el center mass inspeccionando anatomía/collider técnico del target.

Contrato V1 esperado:

`Target → Primary Aim Point`.

- humano: center mass;
- futuros animales/robots/otros actors: punto equivalente definido por su propia representation;
- el shooter no conoce `Torso` ni especies concretas.

Límites:

- un único punto primario;
- sin manager;
- sin weak points;
- sin scoring;
- sin head/mobility targeting;
- sin schema JSON nuevo salvo necesidad real demostrada.

Referencia: `IMPL-0001` y `NPC_Combat_Targeting_Research.md`.

### 8. Fase 8C — Controlled Before/After

Repetir exactamente la muestra de F8A después del nuevo target point.

Mismas seeds, arma, distancia, focus, spread, shooter/target e hitboxes.

Comparar Head/Torso/Arms/Legs/Miss. No exigir distribución uniforme; exigir una distribución explicable y sin sesgo geométrico absurdo.

`ISSUE-0008` sólo puede resolverse con esta evidencia o con otra causa demostrada.

### 9. Fase 8D — Accuracy Simplification Review

No es un refactor automático.

Revisar uno por uno después de F8C:

- Focus — mantener salvo evidencia;
- shooter movement penalty — mantener salvo evidencia;
- target movement penalty — mantener provisionalmente;
- automatic burst spread — mantener V1 salvo evidencia;
- distance penalty — medir posible doble penalización con cono angular;
- `debug_accuracy_spread`/weapon contribution — decidir si necesita convertirse en campo productivo mínimo.

No crear `AccuracyController`, `WeaponHandlingController`, `FireControlController` o similares sólo para repartir líneas.

### 10. Fase 8E — Legacy migration / cleanup

Migrar consumers/fixtures que todavía dependan de actor capsule-only.

Después, cuando no haya consumers legítimos:

- quitar fallback `missing representation → CreatePrimitive(Capsule)`;
- quitar BodyRegion legacy inferido por bounds/hitPoint;
- quitar diagnostics cuyo único objetivo sea preservar esos contratos reemplazados.

Mantener `ActorLocomotionCollider` técnico si sigue siendo necesario para movimiento/collision/NavMesh/collapse.

### 11. Fase 9 — completar Player Debug

El mínimo Invisible-to-AI se adelanta al Correction Pass. F9 conserva:

- cierre/regresiones del toggle Invisible;
- `IMPL-0009` Invincible;
- OFF = gameplay normal;
- QA prolongado sin sustituir Perception/combat/damage por mocks.

### 12. Fase 10 — completar Observability V2

El mínimo multi-NPC/current-vs-last se adelanta al Correction Pass. F10 conserva la expansión útil:

- overlay global compacto;
- inspector seleccionado;
- targeting/accuracy observability cuando Primary Aim Point exista;
- shot traces/regions suficientemente legibles para QA.

### 13. Fases 11–15 — QA integrado y cierre

- batería automatizada pequeña pero fuerte;
- pruebas NPC-only y NPC↔Player renovadas;
- manual game feel;
- cleanup final y reconciliación documental.

## Investigación antes de Codex

Para problemas nuevos cuyo código/causa pueda inspeccionarse desde GitHub, realizar primero la investigación de repo y entregar a Codex seam concreto, alcance y DONE. Codex debe concentrarse en implementación, Unity/local diagnostics, assets y validación que no pueda obtenerse sólo desde el repo.

No gastar cuota repitiendo auditorías ya resueltas en `NPC_Combat_Targeting_Research.md` o `Prueba_3_Findings.md`.

## Issues activos relevantes

- `ISSUE-0008` — posible sesgo de impactos piernas/pies — `SUSPECTED / P1`; F8A después del correction pass.
- `ISSUE-0010` / `0011` — observabilidad multi-NPC/selection — ahora priorizados por Prueba 3.
- `ISSUE-0013` — Invisible-to-AI resuelto; usar ON como condición de Prueba 3.3.
- findings nuevos: F6 stale current-vs-last y KO/combat-memory continuity deben quedar registrados en `Issue_Registry.md`.

## No iniciar todavía

- Behavior Trees/GOAP/Utility AI generales;
- memory framework general;
- cover/squad tactics sofisticadas;
- hearing/noise/schedules/strategic AI;
- full ballistics/drop/wind;
- aim-point/weak-point scoring;
- generic machine/vehicle damage framework;
- attack-method framework sin consumidor;
- producción masiva de contenido;
- otro milestone grande antes de cerrar NPC Foundation V1.
