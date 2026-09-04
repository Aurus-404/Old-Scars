# Old Scars — NPC AI Sanitation Plan

Este documento fija la secuencia de saneamiento posterior a Prueba 2 y su revisión de 2026-09-03. Su objetivo es mantener una NPC Foundation V1 completa, funcional y observable sin convertirla en un stack innecesariamente complejo.

La regla principal sigue siendo la misma: no conservar arquitectura por sunk cost, pero tampoco reemplazar autoridades que ya demostraron funcionar.

Research asociado al bloque de combate/aim: `NPC_Combat_Targeting_Research.md`.

Evidencia manual más reciente: `Prueba_3_Findings.md`.

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
- incapacidad/death cancelan conducta activa, pero una incapacidad temporal no borra por defecto el contexto de enemigo reciente;
- un knockout tiene una duración mínima de tiempo real antes de que physiology pueda permitir recovery;
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

## Regla de KO / amenaza activa / memoria reciente

Prueba 3 demostró que el contrato actual mezcla `no puede actuar ahora` con `ya no recuerdo a este enemigo`.

V1 debe separar ambas cosas sin crear un memory framework general:

- `Conscious/Dazed` puede representar amenaza activa según hostility/perception;
- `Incapacitated/Unconscious` deja de ser amenaza activa y no debe recibir ataques deliberados sólo por seguir siendo hostile;
- una incapacidad temporal no borra automáticamente quién era el enemigo del encounter reciente;
- atacante y noqueado conservan una referencia/contexto mínimo del enemigo hasta recovery, invalidación real o expiración definida;
- esa memoria no actualiza la posición oculta: Perception/LKP/Search siguen siendo la única autoridad espacial;
- `Dead` permanece terminal para conducta activa;
- knockout/unconscious debe respetar un minimum real-time dwell configurable antes de que recovery fisiológico pueda devolver active behavior.

No crear `MemorySystem`, relationship history general, blackboard o planner para resolver esta V1.

## Fuera de alcance

No introducir por inercia:

- Behavior Trees complejos, GOAP o Utility AI general;
- generic blackboard/planner;
- memory framework general;
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

# Prueba 3 — evidencia integrada y Correction Pass

Prueba 3/3.1/3.2 validó gran parte de F2–F7 manualmente, pero encontró problemas que deben corregirse antes de medir aim con rigor.

Detalle: `Prueba_3_Findings.md`.

## Hallazgos que cambian prioridad

1. Player puede contaminar NPC↔NPC porque Red también puede adquirirlo como hostile.
2. F6 puede usar `LastPerception`/snapshots históricos para dibujar LOS/FOV y presentarlos como actuales; las líneas pueden quedar atrás del actor y Dead/Inactive puede seguir mostrando `Perceived` histórico.
3. F6 sigue siendo focal: sólo un NPC seleccionado obtiene world visuals útiles a la vez.
4. incapacidad temporal hace que rival libere Encounter y que el incapacitado limpie contexto, creando `KO → Ambient → recovery → rediscovery`.
5. no existe un minimum real-time KO dwell; recovery puede quedar dominado por physiology sobre `WorldClock` acelerado.
6. el viejo sesgo extremo de piernas no volvió a reproducirse cualitativamente, pero `ISSUE-0008` sigue abierto hasta medición NPC reproducible.

## Correction Pass A — Player Invisible-to-AI mínimo

**Estado:** `COMPLETED — 2026-09-04`.

Slice mínimo de F9 completado para QA:

- Player sigue físico/interactivo;
- ON lo excluye antes de candidate/Recognition/Perception de adquisición y libera su threat automático actual;
- OFF conserva gameplay normal;
- no desactiva Perception global, GameObject ni colliders.

Implementación: marker target-side efímero `ActorDebugAiAcquisitionExclusion` expuesto como `Invisible to AI` en Runtime Debug Tools para el Player real de la composición. Commit funcional `321f26d1d3c1e765e19e86ab66f316238734c8fe`; diagnostic WorldRuntime OFF → Player, ON → Blue, OFF → Player `PASS`.

## Correction Pass B — F6 correctness + multi-NPC mínimo

**Estado:** `NEXT`.

- current Gaze/FOV parte del eye/origin actual;
- `CURRENT` y `LAST` evidence se distinguen explícitamente;
- un snapshot histórico no se presenta como current perception de Dead/Inactive;
- world visuals de Gaze/FOV/LOS pueden verse para varios/todos los NPC simultáneamente;
- selección sólo controla el inspector detallado;
- no duplicar perception/raycasts como segunda autoridad debug.

Este slice adelanta lo mínimo de F10 requerido para pruebas fiables; F10 completa después targeting/shot observability.

## Correction Pass C — KO / combat memory continuity

**Estado:** `NEXT AFTER B`.

- KO deja al actor sin active actions;
- rival deja de atacarlo deliberadamente;
- ambos conservan identidad/contexto mínimo del enemigo reciente;
- recovery puede volver a Encounter sin redescubrimiento artificial;
- si se perdió LOS, no se conoce la posición actual: usar Perception/LKP/Search;
- muerte invalida conducta activa;
- sin framework general de memoria.

## Correction Pass D — Minimum KO dwell

**Estado:** `NEXT AFTER C`.

Agregar un mínimo configurable de tiempo real para knockout/unconscious. Al finalizar ese mínimo, `ActorConditionComponent` y sus thresholds siguen decidiendo si la fisiología permite despertar.

No crear otra autoridad temporal global ni fijar balance final sin playtest.

## Gate — Prueba 3.3

Antes de F8A ejecutar un 1 Blue vs 1 Red con:

- Player Invisible ON;
- observabilidad simultánea de ambos;
- ninguna intervención del Player;
- registro de armas, KO start/duration/recovery, memoria/reanudación y wounds/regions.

Gate: la pelea debe poder interpretarse limpiamente y el KO no debe parecer un reset de personalidad/encounter.

---

# Fase 8 revisada — Combat targeting / accuracy

La antigua Fase 8 de investigación abierta queda sustituida por etapas pequeñas y verificables. La investigación previa ya está en `NPC_Combat_Targeting_Research.md`; Codex no debe repetirla exhaustivamente.

## Fase 8A — Aim Bias Evidence

**Estado:** `QUEUED AFTER PRUEBA 3.3`.

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

## Fase 9 — completar Player Debug

El slice mínimo Invisible-to-AI se adelanta al Correction Pass. F9 conserva:

- cierre/regresiones del toggle Invisible;
- Invincible: pipeline real de detection/shot/hit/region/wounds/condition continúa, pero QA puede bloquear terminal Dead;
- OFF debe equivaler a gameplay normal.

## Fase 10 — completar Observability V2

El slice mínimo current-vs-last/multi-NPC se adelanta al Correction Pass. F10 completa:

- overlay global compacto;
- inspector seleccionado;
- targeting/accuracy (`PrimaryAimPoint`, focus, spread, shot origin/direction, hit collider/region) cuando esos contratos existan;
- shot traces útiles para QA.

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
8. KO dwell + combat memory continuity;
9. incapacity/death;
10. anatomy 6/6;
11. Primary Aim Point / physical imperfect shot;
12. Invisible;
13. Invincible;
14. multi-NPC observability current-vs-last.

## Fase 12 — Prueba integrada NPC-only

Referencia: 1 White, 3 Blue, 3 Red, Player Invisible. Observar vida ambiental, encounters, gaze, Search, shots, aim point, actual regions, wounds, KO/memoria, incapacity/death y retorno a Ambient.

Registrar problemas; no corregirlos silenciosamente durante la prueba.

## Fase 13 — Prueba Player

Invisible OFF, Invincible ON. Player se mueve lateralmente, cruza obstáculos, cambia distancia y rodea NPCs para probar la cadena completa de perception → target → aim → physical hit → damage.

## Fase 14 — Manual game feel

Preguntas humanas:

- ¿parecen humanos y no aimbots/tanques?;
- ¿hay tiempo de reacción razonable?;
- ¿focus vuelve peligroso a quien mantiene target?;
- ¿moverse/usar obstáculos cambia el combate?;
- ¿un KO dura lo suficiente y conserva contexto sin producir wallhack?;
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

NPC Foundation V1 sólo se cierra cuando el flujo `Ambient → Gaze/Perception → Recognition → Encounter → Aim/Physical Combat → KO/Recovery or LostContact/Search → Reacquire/Release → Ambient` funciona de forma observable, sin ownership contradictorio, con memoria mínima de combate coherente, anatomía/hits coherentes, player debug suficiente y una prueba manual satisfactoria.
