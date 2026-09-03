# Old Scars — Current Milestone

Este archivo es el snapshot operativo breve. `Project_Roadmap.md` conserva IDs/dependencias de milestones grandes; `Next_Sprints.md` es la cola inmediata; `Issue_Registry.md` almacena defectos; `Implementation_Backlog.md` guarda mecánicas/mejoras menores aprobadas para después; `Development_Context_Index.md` indica qué leer al cambiar de chat/sesión.

## Estado actual

### M41 — NPC Combat / AI Foundation after Prueba 2

Estado operativo:

`IN PROGRESS — SANITATION PHASE 8 / COMBAT TARGETING`

Plan activo: `NPC_AI_Sanitation_Plan.md`.

Research/decision record activo: `NPC_Combat_Targeting_Research.md`.

## Foundation ya cerrada

El bloque ya no está en la situación inicial de Prueba 2. Están implementados y validados:

- F2 — `ActorBehaviorController`: ownership `Ambient / Encounter / Search / Inactive`, roaming físico White/Blue/Red y reanudación real;
- F3 — `ActorGazeController`: gaze/attention lógica bounded, yaw inicial determinista y no-wallhack;
- F4 — tracking visual por movimiento observado con predicción corta bounded;
- F5 — `ActorVisualPerceptionService` centrado en Current Gaze, FOV/LOS productivos y tracking lateral integrado;
- F6 — LostContact/Search V1 con `SearchAnchor` congelado, navegación física, reacquire/release y ownership Search real;
- F7 — humano estático reutilizado + `ActorLocomotionCollider` + seis `ActorCombatHitRegion` explícitos + shot path compartido.

Commits funcionales de referencia:

- F2 `7fa47c59d8bbe1df61b598f01875e91b2b51c089`;
- F3 `e1bd7d7ce6d0f0a6885cb23a7047d53d31fd0509`;
- F4 `e72feeb67edfe9b208eefa4d4c6c13f488df62cc`;
- F5 `2fc27d946f5a807abd4f046d2dee85331490b7c2`;
- F6 `7590ec6f868da89a72a5514a85f7c042fb89e36f`;
- F7 `96cccbe514177d8eb05d8c5c439909b4657f252e`.

No reabrir estas autoridades por inercia. Una regresión real puede justificar cambios, pero no otra reconstrucción global de IA.

## Problema activo

### ISSUE-0008 — posible sesgo de impactos hacia piernas/pies

Estado del issue: `SUSPECTED / P1` hasta validación estadística reproducible.

La investigación de repo posterior a Fase 7 encontró una hipótesis fuerte:

- firearm aim de `HumanEncounterAIController` todavía toma el `ActorLocomotionCollider` del target y usa `bounds.center` como base aim point;
- los `ActorCombatHitRegion` nuevos se ignoran para aim (correctamente durante F7 para no cambiar aim accidentalmente), por lo que la IA sigue apuntando a un centro técnico de locomoción, no a un center-mass del target;
- en `humanoid_standard`, el centro de Torso está aproximadamente 0.18 m por encima del centro de locomoción y el borde superior de piernas queda aproximadamente 0.11 m por debajo del aim histórico;
- el spread actual es radial/simétrico y no muestra por código una inclinación vertical deliberada, por lo que un aim base bajo puede ser amplificado por un error angular normal.

Esto todavía no autoriza cambiar balance/spread. La próxima fase debe demostrarlo bajo condiciones controladas.

## Dirección arquitectónica elegida

No implementar `aim at Torso` dentro de Human Encounter como solución permanente.

La dirección objetivo es genérica para humanos, animales, mutantes, robots u otros targets:

`Target → Primary Aim Point → shooter focus/context error → weapon → PhysicalShotPathResolver → actual hit → receiver`.

El target define dónde es razonable intentar impactarlo; el shooter define cuánto error tiene; el arma define sus parámetros; la física decide dónde pegó; el receptor interpreta el impacto.

Esto está documentado en `NPC_Combat_Targeting_Research.md`.

## Próximo paso exacto

### Fase 8A — Aim Bias Evidence

Implementar únicamente instrumentación/diagnostic reproducible. No cambiar gameplay todavía.

Medir por shot:

- base aim point y su source;
- proposed human center-mass;
- focus/current spread;
- shot origin/direction;
- hit collider/hit point;
- BodyRegion o miss;
- seed/condición reproducible.

Comparar el comportamiento actual contra el proposed target point bajo las mismas condiciones. Si 8A confirma la hipótesis, pasar a F8B; si no, investigar la evidencia real antes de tocar spread.

## Secuencia inmediata revisada

- F8A — Aim Bias Evidence.
- F8B — Generic target-side Primary Aim Point, sólo si 8A lo justifica.
- F8C — comparación before/after con mismas seeds/condiciones, sin retuning.
- F8D — review pequeño de accuracy; ningún factor cambia por defecto.
- F8E — migrar consumers legacy y eliminar actor capsule-only/geometric BodyRegion fallback cuando sea seguro.
- F9 — Player Invisible / Invincible debug.
- F10 — Observability V2.
- F11–F15 — batería integrada, Prueba 3/3B, game feel, cleanup y cierre.

## Reglas de alcance

Conservar salvo evidencia contraria:

- Behavior ownership;
- Navigation;
- Gaze;
- Perception;
- Recognition/Threat Acquisition;
- Search V1;
- `WeaponCombatService`;
- `PhysicalShotPathResolver`;
- explicit combat hit regions;
- Focus y error angular físico.

No iniciar ahora:

- Behavior Trees/GOAP/Utility AI;
- weak-point/aim-point scoring framework;
- head targeting AI;
- full ballistics/drop/wind;
- Accuracy/FireControl/WeaponHandling controllers separados por limpieza preventiva;
- machine/vehicle damage framework sin consumidor real;
- attack-method framework para animales/mutantes antes del primer atacante real de ese tipo.

## Nota sobre compatibilidad legacy

La cápsula invisible técnica de locomoción puede seguir existiendo. Lo que no forma parte de la arquitectura final es:

- el actor capsule-only como fallback de representación;
- inferir anatomía productiva por porcentajes de una cápsula.

Esos fallbacks se retiran sólo después de migrar los consumers/fixtures que todavía los usan.
