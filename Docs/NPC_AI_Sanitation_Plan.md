# Old Scars — NPC AI Sanitation Plan

Este documento fija la secuencia de saneamiento posterior a Prueba 2 y su revisión de 2026-09-03. Su objetivo es mantener una NPC Foundation V1 completa, funcional y observable sin convertirla en un stack innecesariamente complejo.

La regla principal sigue siendo la misma: no conservar arquitectura por sunk cost, pero tampoco reemplazar autoridades que ya demostraron funcionar.

Research asociado al bloque de combate/aim: `NPC_Combat_Targeting_Research.md`.

## Objetivo final

Cerrar una `NPC FOUNDATION V1` donde:

- White/Blue/Red poseen vida ambiental real;
- Behavior ownership es inequívoco;
- Gaze, Perception, Recognition, Encounter y Search están separados por responsabilidad;
- tracking lateral y occlusion son físicos y bounded;
- LostContact usa información conocida y puede Search/reacquire/release;
- shots usan una ruta física compartida;
- targets humanos usan geometría anatómica explícita;
- el aim normal no depende de que el shooter entienda anatomía humana;
- incapacidad/death cancelan conducta;
- QA puede observar NPC↔NPC y NPC↔Player sin alterar gameplay cuando debug está OFF;
- diagnostics prueban resultados observables, no proxies.

## Arquitectura que se conserva

No reabrir por inercia:

- `ActorBehaviorController` — ownership `Ambient / Encounter / Search / Inactive`;
- `ActorNavigationController` — autoridad técnica de movimiento;
- `ActorGazeController` — atención lógica bounded;
- `ActorVisualPerceptionService` — range/FOV/LOS productivos;
- `ActorThreatAcquisitionController` — discovery/recognition/threat;
- Search V1;
- `WeaponCombatService`;
- `PhysicalShotPathResolver`;
- health/medical/condition/vital;
- `ActorCombatHitRegion` y `ActorLocomotionCollider` como contratos separados.

Una regresión real puede justificar cambios; el mero tamaño de una clase no autoriza un framework nuevo.

## Regla arquitectónica de combate revisada

El aim/attack pipeline objetivo es:

```text
Threat / Encounter
    ↓
Target
    ↓
Primary Aim Point
    ↓
Shooter focus/context error
    ↓
Weapon parameters/cadence
    ↓
PhysicalShotPathResolver
    ↓
world / miss / actual target collider
    ↓
CombatResolution
    ↓
receiver consequences
```

Preguntas separadas:

- target acquisition decide a quién atacar;
- target/representation decide dónde es razonable intentar impactarlo;
- shooter decide cuánto error tiene;
- weapon decide sus parámetros;
- physics decide dónde pegó;
- receiver decide qué significa ese hit.

No acoplar firearm aim normal a `BodyRegion.Torso`: futuros animales, mutantes, robots u otros actors pueden no tener anatomía humana.

## Fuera de alcance

No introducir por inercia:

- Behavior Trees complejos, GOAP o Utility AI general;
- generic blackboard/planner;
- weak-point/aim-point scoring framework;
- headshot/mobility targeting AI;
- cover/flanking/squads avanzados;
- hearing/noise/schedules/jobs;
- morale/suppression/stance/breathing/weapon-skill frameworks;
- full ballistics, bullet travel/drop/drag/wind;
- machine/vehicle damage framework antes de un consumidor real;
- attack-method framework para animales/mutantes antes del primer atacante real que lo necesite.

---

# Fases cerradas

## Fase 0 — Registry / baseline / documentación

`COMPLETADA`.

Se crearon/establecieron el Issue Registry y el plan persistente para evitar perder problemas entre pruebas/sesiones.

## Fase 1 — Auditoría destructiva

`COMPLETADA`.

Decisión: reemplazar/simplificar la coordinación alta de behavior sin rehacer Perception/Navigation/Combat/Health/Equipment/Affiliation/Persistence.

## Fase 2 — Behavior ownership + Ambient roaming

`COMPLETADA` — commit funcional `7fa47c59d8bbe1df61b598f01875e91b2b51c089`.

White/Blue/Red demostraron desplazamiento físico real. Encounter interrumpe Ambient sin competencia y Ambient reanuda al terminar.

## Fase 3 — Gaze/Attention V1

`COMPLETADA` — `e1bd7d7ce6d0f0a6885cb23a7047d53d31fd0509`.

Gaze lógico independiente de locomotion; Ambient/Candidate/Encounter/LostContact/Inactive; no-wallhack; yaw inicial deterministic.

## Fase 4 — Tracking visual bounded

`COMPLETADA` — `e72feeb67edfe9b208eefa4d4c6c13f488df62cc`.

Movimiento observado → velocidad bounded → predicción corta; target switch reset; LostContact no sigue posiciones ocultas.

## Fase 5 — Production Perception usa Current Gaze

`COMPLETADA` — `2fc27d946f5a807abd4f046d2dee85331490b7c2`.

FOV productivo centrado en `CurrentGazeDirection`, LOS físico intacto, Ambient discovery real, tracking lateral integrado y límites humanos.

## Fase 6 — LostContact / Search V1

`COMPLETADA` — `7590ec6f868da89a72a5514a85f7c042fb89e36f`.

Fight: `LostContact → Search → Reacquire/Encounter OR Release/Ambient`; SearchAnchor congelado, orden única, arrival/inspection, no ataques sin percepción fresca.

## Fase 7 — Human representation + explicit anatomical hitboxes

`COMPLETADA` — `96cccbe514177d8eb05d8c5c439909b4657f252e`.

`humanoid_standard` reutiliza `PSX_Char_Male_Base`, sin Animator; visual rig, locomotion collider y seis combat hit regions están separados. `PhysicalShotPathResolver` sigue compartido y los hits explícitos resolvieron 6/6 regiones.

La compatibilidad capsule-only/geometric BodyRegion conservada durante F7 es transición, no arquitectura final.

---

# Fase 8 revisada — Combat targeting / accuracy

La antigua Fase 8 de investigación abierta queda sustituida por etapas pequeñas y verificables. La investigación previa ya está en `NPC_Combat_Targeting_Research.md`; Codex no debe repetirla exhaustivamente.

## Fase 8A — Aim Bias Evidence

**Estado:** `NEXT`.

No cambiar gameplay.

Instrumentar el aim NPC actual bajo condiciones reproducibles y registrar por shot:

- target/TargetId;
- source del aim point;
- aim point actual;
- proposed human center-mass;
- focus;
- current spread;
- shot origin;
- final direction;
- hit collider/hit point;
- BodyRegion o miss;
- seed/condiciones.

Hipótesis fuerte a probar:

- firearm aim actual usa el centro del `ActorLocomotionCollider`;
- ese punto queda más bajo que el centro del Torso explícito;
- el spread radial normal puede convertir una base baja en demasiados impactos de piernas.

No tocar Focus/spread/distance/movement/burst/damage/anatomy durante 8A.

**Gate:** explicar con evidencia si el base aim point contribuye materialmente a `ISSUE-0008` o si la causa real es otra.

## Fase 8B — Generic target-side Primary Aim Point

**Estado:** `CONDITIONAL / READY`.

Sólo si 8A lo justifica.

Introducir la abstracción mínima del lado del target, por ejemplo `ActorPrimaryAimPoint` según conventions reales.

V1:

- un único punto primario;
- humano → center mass;
- futuros targets → punto equivalente definido por su representation;
- Encounter no busca `Torso` ni adivina anatomía;
- sin manager, scoring, weak points, head/mobility roles ni schema JSON nuevo por anticipación.

Firearm aim normal deja de usar el locomotion center cuando el target expone Primary Aim Point.

## Fase 8C — Controlled Before/After

Repetir exactamente la muestra de 8A con:

- mismas seeds;
- mismo shooter/target;
- misma arma/distancia;
- mismo focus/spread;
- mismas hitboxes.

La única diferencia relevante debe ser el base target point.

Comparar Head/Torso/Arms/Legs/Miss. No exigir uniformidad; exigir distribución explicable y ausencia de sesgo geométrico absurdo.

`ISSUE-0008` sólo se resuelve con evidencia reproducible.

## Fase 8D — Accuracy Simplification Review

No es un refactor automático.

Revisar después de 8C:

- Focus — KEEP salvo evidencia;
- shooter movement penalty — KEEP salvo evidencia;
- target movement penalty — KEEP provisionalmente;
- automatic burst spread — KEEP V1 salvo evidencia;
- distance penalty — medir posible doble penalización con el cono angular;
- weapon spread — decidir si `debug_accuracy_spread` debe convertirse en contribución productiva mínima.

No extraer `ActorAimController`, `AccuracyController`, `FireControlController` o `WeaponHandlingController` por limpieza preventiva.

## Fase 8E — Legacy migration / cleanup

Primero identificar/migrar perfiles y diagnostics que aún dependan de actor capsule-only.

Después, cuando no existan consumers legítimos:

- quitar fallback visual `missing representation → CreatePrimitive(Capsule)`;
- quitar BodyRegion anatómico inferido por bounds/hitPoint;
- quitar tests cuya única misión sea preservar esos contratos reemplazados.

Mantener la cápsula técnica invisible de locomoción si Navigation/collision/avoidance/collapse todavía la necesitan.

---

# Fases siguientes

## Fase 9 — Player Debug: Invisible / Invincible

Invisible: Player sigue físico/interactivo pero se excluye del candidate/acquisition boundary.

Invincible: pipeline real de detection/shot/hit/region/wounds/condition continúa, pero QA puede bloquear el terminal Dead.

OFF debe equivaler a gameplay normal.

## Fase 10 — Observability V2

Overlay global multi-NPC + inspector seleccionado. Incluir targeting/accuracy (`PrimaryAimPoint`, focus, spread, shot origin/direction, hit collider/region) cuando esos contratos existan.

No crear otra autoridad de gameplay.

## Fase 11 — Batería automatizada pequeña

Gates de resultados observables:

1. Ambient movement;
2. Behavior ownership;
3. Encounter interruption/resume;
4. Gaze/FOV/LOS;
5. Recognition;
6. tracking;
7. LostContact/Search;
8. incapacity/death;
9. anatomy 6/6;
10. Primary Aim Point / physical imperfect shot;
11. Invisible;
12. Invincible.

## Fase 12 — Prueba 3 integrada

Referencia: 1 White, 3 Blue, 3 Red, Player Invisible. Observar vida ambiental, encounters, gaze, Search, shots, aim point, actual regions, wounds, incapacity/death y retorno a Ambient.

Registrar problemas; no corregirlos silenciosamente durante la prueba.

## Fase 13 — Prueba 3B Player

Invisible OFF, Invincible ON. Player se mueve lateralmente, cruza obstáculos, cambia distancia y rodea NPCs para probar la cadena completa de perception → target → aim → physical hit → damage.

## Fase 14 — Manual game feel

Preguntas humanas:

- ¿parecen humanos y no aimbots/tanques?;
- ¿hay tiempo de reacción razonable?;
- ¿focus vuelve peligroso a quien mantiene target?;
- ¿moverse/usar obstáculos cambia el combate?;
- ¿los misses y regiones impactadas parecen físicamente creíbles?;
- ¿la pelea multi-NPC se puede leer visualmente?

Diagnostics no sustituyen esta prueba.

## Fase 15 — Cleanup y cierre

Eliminar:

- instrumentation temporal de F8;
- compatibilidad capsule-only/geometric anatomy ya sin consumers;
- tests de contratos reemplazados;
- código muerto/comentarios históricos engañosos.

Conservar:

- Issue Registry;
- Implementation Backlog;
- observability útil;
- debug toggles;
- regressions de contratos importantes.

Reconciliar Roadmap/Current/Next/Development Log/Architecture.

---

## Protocolo entre fases

Después de toda fase con código:

1. la investigación de repo se hace fuera de Codex primero cuando sea posible;
2. Codex recibe objetivo, seam concreto, alcance/DONE y validación proporcional;
3. Codex implementa y explica el cambio en detalle;
4. no se inicia la fase siguiente automáticamente;
5. el commit publicado se revisa otra vez contra el repo;
6. bugs nuevos → `Issue_Registry.md`;
7. mecánicas/mejoras futuras no-bug → `Implementation_Backlog.md`.

## DONE global

NPC Foundation V1 sólo se cierra cuando el flujo `Ambient → Gaze/Perception → Recognition → Encounter → Aim/Physical Combat → LostContact/Search → Reacquire/Release → Ambient` funciona de forma observable, sin ownership contradictorio, con anatomía/hits coherentes, player debug suficiente y una Prueba 3/manual satisfactoria.
