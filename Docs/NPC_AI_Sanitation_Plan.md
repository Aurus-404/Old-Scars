# Old Scars — NPC AI Sanitation Plan

Este documento mantiene la secuencia canónica para cerrar `M41 — NPC Combat / AI Foundation V1` después de Prueba 3 y de los cambios implementados entre 2026-09-03 y 2026-09-06.

La regla principal sigue siendo:

- conservar autoridades demostradas;
- corregir bugs reales antes de añadir complejidad;
- medir antes de retunear;
- no crear frameworks generales sin consumidores reales;
- diagnostics prueban resultados observables, pero no sustituyen aceptación manual.

Research asociado a aim/accuracy: `NPC_Combat_Targeting_Research.md`.

Evidencia manual integrada: `Prueba_3_Findings.md`.

Estado operativo corto: `Current_Milestone.md`.

Cola inmediata: `Next_Sprints.md`.

## Objetivo final

Cerrar una NPC FOUNDATION V1 donde:

- White/Blue/Red poseen vida ambiental real;
- Behavior ownership es inequívoco;
- Gaze, Perception, Recognition, Encounter y Search están separados por responsabilidad;
- tracking lateral/occlusion son físicos y bounded;
- LostContact usa información conocida y puede Search/reacquire/release;
- shots usan ruta física compartida;
- targets humanos usan hit regions anatómicas explícitas;
- aim normal no depende de que el shooter entienda anatomía humana;
- incapacidad/death cancelan conducta activa;
- KO temporal no borra automáticamente contexto del enemigo reciente;
- Unconscious respeta un minimum real-time dwell antes de poder recuperar active behavior;
- QA puede observar NPC↔NPC y NPC↔Player con debug OFF equivalente a gameplay normal;
- diagnostics y tooling no crean una segunda autoridad de gameplay.

## Arquitectura que se conserva

No reabrir por inercia:

- `ActorBehaviorController` — ownership `Ambient / Encounter / Search / Inactive`;
- `ActorNavigationController` — autoridad técnica de movimiento NPC;
- `ActorGazeController` — atención lógica bounded;
- `ActorVisualPerceptionService` — range/FOV/LOS productivos;
- `ActorThreatAcquisitionController` — discovery/recognition/threat;
- Search V1;
- `WeaponCombatService`;
- `PhysicalShotPathResolver`;
- Health/Medical/Condition/Vital Integrity;
- `ActorCombatHitRegion` y `ActorLocomotionCollider` como contratos separados.

Una regresión real puede justificar cambios; el mero tamaño de una clase no autoriza un framework nuevo.

## Reglas de combate/aim

Pipeline conceptual:

```text
Threat / Encounter
    ↓
Target
    ↓
Primary Aim Point, si la evidencia lo justifica
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

- acquisition decide a quién atacar;
- target/representation decide dónde es razonable intentar impactarlo;
- shooter decide cuánto error tiene;
- weapon decide parámetros;
- physics decide dónde pegó;
- receiver decide qué significa ese impacto.

No acoplar firearm aim normal a `BodyRegion.Torso` como solución permanente.

## Regla de KO / amenaza activa / memoria reciente

`KO / Unconscious != Dead`.

V1 debe separar:

- amenaza activa;
- capacidad de actuar;
- recuerdo mínimo del enemigo reciente;
- conocimiento espacial actual.

Contrato:

- `Conscious/Dazed` puede representar amenaza activa según hostility/perception;
- `Incapacitated/Unconscious` deja de ser amenaza activa y no debe recibir ataques deliberados sólo por seguir hostile;
- una incapacidad temporal no borra automáticamente quién era el enemigo reciente;
- recordar identidad NO equivale a mantener `Threat != null`;
- recordar identidad por sí solo no bloquea self-treatment ni AmbientTopOff;
- memoria reciente no actualiza posiciones ocultas;
- Perception/LKP/Search siguen siendo autoridad espacial;
- death es terminal.

No crear MemorySystem, blackboard, relationship history general o planner para este contrato.

## Regla de KO dwell

El minimum real-time dwell se aplica al estado realmente `Unconscious`, no por defecto a toda `Incapacitated`.

- empieza al entrar en Unconscious;
- antes de cumplir el mínimo no puede recuperar active behavior;
- cumplir el mínimo NO fuerza wake-up;
- después, physiology/thresholds/hysteresis siguen decidiendo recovery;
- `WorldClock` no acorta el mínimo real;
- death sigue terminal;
- save/load no debe convertirse en bypass accidental.

El balance final de duración se decide por playtest; el contrato no depende de ese número.

## Fases cerradas

### F2 — Behavior ownership + Ambient roaming
`COMPLETED` — `7fa47c59d8bbe1df61b598f01875e91b2b51c089`.

### F3 — Gaze/Attention V1
`COMPLETED` — `e1bd7d7ce6d0f0a6885cb23a7047d53d31fd0509`.

### F4 — Tracking visual bounded
`COMPLETED` — `e72feeb67edfe9b208eefa4d4c6c13f488df62cc`.

### F5 — Production Perception usa Current Gaze
`COMPLETED` — `2fc27d946f5a807abd4f046d2dee85331490b7c2`.

### F6 — LostContact / Search V1
`COMPLETED` — `7590ec6f868da89a72a5514a85f7c042fb89e36f`.

### F7 — Human representation + explicit anatomical hitboxes
`COMPLETED` — `96cccbe514177d8eb05d8c5c439909b4657f252e`.

### Correction Pass A — Player Invisible-to-AI
`COMPLETED` — `321f26d1d3c1e765e19e86ab66f316238734c8fe`.

Player sigue físico/interactivo; ON lo excluye de acquisition automática y libera threat automático hacia él; OFF conserva gameplay normal.

## Capacidades recientes que ahora forman parte de las regresiones M41

Aunque se implementaron fuera del orden original del correction pass, hoy son contratos reales:

### Timed Bandaging V1

- tratamiento real por tiempo;
- Player puede caminar;
- sprint/combat cancelan;
- NPC usa bandage owned real;
- routine self-treatment tras calma determinista;
- emergency por riesgo hemorrágico.

KO memory no debe convertir recuerdo reciente en `Threat != null` permanente y bloquear esta calma por accidente.

### Blood Trails V1/V1.1

- bleeding médico real;
- emisión por distancia;
- Player/NPC;
- pool bounded;
- bandaging reduce densidad emergentemente;
- sin AI tracking.

### NPC Opportunistic Reload

- Ambient top-off sólo en ventana segura;
- empty reload puede continuar en LostContact/Search;
- WeaponCombatService sigue siendo autoridad transaccional.

Cambios de KO/Search deben preservar esta continuidad o registrar regresión real.

---

# Secuencia operativa aprobada — 2026-09-06

## P1 — Correction Pass B / F6 cerrado

P1/F6 / Correction Pass B: **DONE / ACCEPTED / PUBLISHED** el 2026-09-06 en `5aac763c14c399bfe09a3e925c50698658ad2716`. CURRENT Gaze/FOV multi-NPC desde origen productivo actual; selección sólo para inspector detallado. LAST usa ObserverOrigin → ObservedPosition históricos, visual secundario y toggle independiente, sin reconstruir blocker hit con collider actual. Sin evidencia: LAST: No evidence. Dead/Inactive sin CURRENT engañoso. Sin nueva Perception/raycasts productivos.

F6 Observability, Gaze/Perception, LostContact/Search y compile Runtime/Editor PASS previos; aceptación visual manual final confirmada por Mauro. IMPL-0010 minimum slice completado; F10 completo pendiente. Posible desajuste eye origin/representación humana separado en ISSUE-0023, sin resolver.

## P2 — Minimum real-time KO dwell

**Estado:** `NEXT`.

Primero estabilizar cuándo un actor `Unconscious` puede volver a active behavior.

Validar:

- x1/x100 WorldClock;
- recovery physiology antes/después del mínimo;
- nuevas heridas durante KO;
- death;
- semántica save/load acordada.

No cambiar trauma/blood balance por esta tarea.

## P3 — KO / combat-memory continuity

**Estado:** `NEXT AFTER P2`.

Después de estabilizar la transición funcional:

- conservar identidad/contexto mínimo;
- no mantener al KO como threat activo;
- no atacar deliberadamente al KO;
- recovery visible puede reanudar contexto;
- recovery fuera de vista no revela posición;
- Search/Perception siguen siendo autoridad espacial;
- treatment/reload siguen coherentes.

## P4 — Prueba 3.3

**Estado:** `GATE BEFORE F8A`.

1 Blue vs 1 Red:

- Player Invisible ON;
- observabilidad simultánea aceptada;
- arma/loadout identificable por debug;
- registrar KO start/duration/recovery/context;
- wounds/regions;
- LostContact/Search si aparece;
- ninguna intervención del Player.

Un run sin KO no valida recovery.

## P5 — F8A Aim Bias Evidence

**Estado:** `AFTER P4`.

No cambiar gameplay.

Instrumentar evidencia correlacionada por shot:

- target;
- aim source/point;
- proposed center-mass;
- focus/spread;
- origin/direction;
- hit collider/point;
- region/miss;
- seed/condiciones.

La instrumentación útil debe poder evolucionar después hacia F10, no crear un observador paralelo descartable si puede evitarse.

## P6 — F8B/C; F8D sólo si evidencia lo exige

Si F8A confirma que el base aim point contribuye materialmente:

- introducir Primary Aim Point genérico target-side;
- humano → center-mass razonable;
- futuros targets → equivalente propio;
- sin weak points/scoring/head targeting.

Después repetir before/after con mismas condiciones.

F8D sólo revisa accuracy residual con evidencia; no es refactor automático.

## P7 — Player Debug Invincible

**Estado:** `AFTER TARGETING STABILIZATION`.

Invincible ON mantiene:

`detection → shot → physical hit → BodyRegion → wounds → bleeding → pain → trauma → condition/KO`

pero bloquea coherentemente terminal Dead.

No basta con omitir `ProcessDeath`: `IsDead`, lifecycle y fatal blood loss deben seguir consistentes.

OFF = gameplay normal, sin curación ni resurrección implícita.

## P8 — F10 Observability V2 completa

Construir encima de P1/P5:

- overlay multi-NPC compacto;
- inspector seleccionado;
- targeting/aim point;
- focus/spread;
- shot origin/direction;
- collider/region/miss;
- traces útiles.

Read-only; ninguna autoridad nueva.

## P9 — Legacy migration + QA integrada + cierre M41

Migrar sólo consumers reales de compatibility capsule/anatomy legacy.

Después:

- batería automatizada pequeña;
- NPC-only integrada;
- NPC↔Player;
- manual game feel;
- console review;
- cleanup de instrumentation temporal/código realmente muerto;
- reconciliación documental.

### DONE global M41

NPC Foundation V1 sólo cierra cuando:

- P1 publicado/aceptado;
- P2/P3 cumplen contratos;
- Prueba 3.3 aceptada;
- `ISSUE-0008` tiene conclusión sustentada y before/after si hubo fix;
- Invincible funciona ON/OFF;
- F10 permite explicar resultados;
- Search/Bandaging/Reload/Perception/ownership siguen pasando;
- pruebas NPC↔NPC y NPC↔Player funcionan en sesión fresca;
- no quedan blockers incompatibles con el alcance;
- Mauro aprueba el cierre.

---

# Después de M41

Estos trabajos NO forman parte del DONE de NPC Foundation V1.

## Equipment visuals humanoides

`IMPL-0016`.

Puede hacerse como mejora de representación después de M41. No bloquea aim mientras el arma pueda identificarse por tooling.

## Loaded ammo mass

`ISSUE-0022`.

Debe resolverse antes de Encumbrance. Reload consume ammo owned y firearm conserva rounds internos; la masa no puede desaparecer al recargar.

## Carry Weight / Encumbrance

`IMPL-0020`.

Contrato de producto:

- Carry Capacity ≠ storage capacity;
- `0..75%` normal;
- `>75%..100%` penalización progresiva;
- `100%` aún móvil;
- `>100%` traslación cero;
- demás acciones según sus propias autoridades;
- mismo contrato Player/NPC;
- `Overloaded != Incapacitated`.

No introducir este bloque entre F8A y su comparación porque velocidad NPC participa en accuracy y contaminaría la medición.

## Localized Limb Impairment

`IMPL-0021`.

Después de Encumbrance:

- piernas → locomoción/sprint;
- brazos → handling/reload/melee cuando las reglas/consumers estén definidos;
- sin limb HP paralelo;
- bandaging no cura automáticamente impairment;
- Carry y Medical no se conocen directamente.

---

## Fuera de alcance por defecto

No introducir por inercia:

- Behavior Trees/GOAP/Utility AI general;
- generic blackboard/planner;
- memory framework general;
- weak-point scoring/head targeting;
- cover/flanking/squads avanzados;
- hearing/noise/schedules/jobs;
- morale/suppression/stance/breathing/weapon-skill frameworks;
- full ballistics/drop/drag/wind;
- Strength/stats y backpack capacity modifiers;
- weapon viability/fallback sin tarea propia;
- attack-method framework antes del primer consumidor real no-firearm;
- blood tracking AI/footprints/puddles por asociación con Blood Trails.

## Protocolo entre fases

Después de toda fase con código:

1. investigar desde repo primero cuando sea posible;
2. entregar a Codex objetivo/seam/DONE/alcance claros;
3. usar el modelo mínimo suficiente;
4. validar proporcionalmente en Unity;
5. no iniciar la fase siguiente automáticamente;
6. revisar el commit publicado contra el repo;
7. bugs nuevos → `Issue_Registry.md`;
8. features/mejoras → `Implementation_Backlog.md`;
9. aceptación manual cuando el gate dependa de legibilidad/game feel.
