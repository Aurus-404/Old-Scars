# Old Scars — Next Sprints

Este documento contiene sólo los próximos trabajos reales. `Current_Milestone.md` resume el estado; `Issue_Registry.md` conserva defectos; `Implementation_Backlog.md` conserva mecánicas/mejoras menores aprobadas para después; `NPC_AI_Sanitation_Plan.md` mantiene la secuencia completa del bloque.

## Próximo trabajo

### 1. Fase 8A — Aim Bias Evidence

Estado: `NEXT`.

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

Gate: explicar si el punto base actual contribuye de forma material al exceso de piernas/pies o si la evidencia apunta a otra causa.

### 2. Fase 8B — Generic Target-side Primary Aim Point

Estado: `CONDITIONAL / READY`.

Sólo ejecutar si F8A justifica el cambio.

Objetivo: dejar de hacer que `HumanEncounterAIController` adivine el center mass inspeccionando la anatomía/collider técnico del target.

Contrato V1 esperado:

`Target → Primary Aim Point`.

- humano: center mass del torso;
- futuros animales/robots/otros actors: punto equivalente definido por su propia representation;
- el shooter no conoce `Torso` ni especies concretas.

Límites:

- un único punto primario;
- sin manager;
- sin weak points;
- sin scoring;
- sin head/mobility targeting;
- sin schema JSON nuevo salvo evidencia concreta de necesidad.

Referencia: `IMPL-0001` y `NPC_Combat_Targeting_Research.md`.

### 3. Fase 8C — Controlled Before/After

Repetir exactamente la muestra de F8A después del nuevo target point.

Mismas:

- seeds;
- arma;
- distancia;
- focus;
- spread;
- shooter/target;
- hitboxes.

Comparar Head/Torso/Arms/Legs/Miss. No exigir distribución uniforme; exigir una distribución explicable y sin sesgo geométrico absurdo.

`ISSUE-0008` sólo puede resolverse con esta evidencia o con otra causa demostrada.

### 4. Fase 8D — Accuracy Simplification Review

No es un refactor automático.

Revisar uno por uno después de F8C:

- Focus — mantener salvo evidencia;
- shooter movement penalty — mantener salvo evidencia;
- target movement penalty — mantener provisionalmente;
- automatic burst spread — mantener V1 salvo evidencia;
- distance penalty — medir posible doble penalización con cono angular;
- `debug_accuracy_spread`/weapon contribution — decidir si necesita convertirse en un campo productivo mínimo.

No crear `AccuracyController`, `WeaponHandlingController`, `FireControlController` o similares sólo para repartir líneas.

### 5. Fase 8E — Legacy migration / cleanup

Migrar consumers/fixtures que todavía dependan de actor capsule-only.

Después, cuando no haya consumers legítimos:

- quitar fallback `missing representation → CreatePrimitive(Capsule)`;
- quitar BodyRegion legacy inferido por bounds/hitPoint;
- quitar diagnostics cuyo único objetivo sea preservar esos contratos reemplazados.

Mantener `ActorLocomotionCollider` técnico si sigue siendo necesario para movimiento/collision/NavMesh/collapse.

### 6. Fase 9 — Debug Player

- `IMPL-0008` Invisible-to-AI;
- `IMPL-0009` Invincible.

Gate: OFF = gameplay normal; ON permite QA prolongado sin sustituir Perception/combat/damage por mocks.

### 7. Fase 10 — Observability V2

- `IMPL-0010` overlay global multi-NPC + inspector seleccionado;
- `IMPL-0011` targeting/accuracy observability cuando Primary Aim Point exista.

Debe mostrar datos read-only de producción, no crear otra autoridad.

### 8. Fases 11–15 — QA integrado y cierre

- batería automatizada pequeña pero fuerte;
- Prueba 3 multi-NPC con Player Invisible;
- Prueba 3B con Player Invincible;
- game-feel manual;
- cleanup final y reconciliación documental.

## Investigación antes de Codex

Para problemas nuevos cuyo código/causa pueda inspeccionarse desde GitHub, realizar primero la investigación de repo y entregar a Codex el seam concreto, alcance y DONE. Codex debe concentrarse en implementación, Unity/local diagnostics, assets y validación que no pueda obtenerse sólo desde el repo.

No gastar cuota repitiendo una auditoría exhaustiva ya resuelta por `NPC_Combat_Targeting_Research.md`.

## Issues activos relevantes

- `ISSUE-0008` — posible sesgo de impactos piernas/pies — `SUSPECTED / P1`; próximo objetivo F8A.
- Issues de fases anteriores del saneamiento ya resueltos permanecen en `Issue_Registry.md` como historial.

## No iniciar todavía

- Behavior Trees/GOAP/Utility AI generales;
- cover/squad tactics sofisticadas;
- hearing/noise/schedules/strategic AI;
- full ballistics/drop/wind;
- aim-point/weak-point scoring;
- generic machine/vehicle damage framework;
- attack-method framework sin consumidor;
- producción masiva de contenido;
- otro milestone grande antes de cerrar NPC Foundation V1.
