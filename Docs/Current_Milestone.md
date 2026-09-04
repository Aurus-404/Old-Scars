# Old Scars — Current Milestone

Este archivo es el snapshot operativo breve. `Project_Roadmap.md` conserva IDs/dependencias de milestones grandes; `Next_Sprints.md` es la cola inmediata; `Issue_Registry.md` almacena defectos; `Implementation_Backlog.md` guarda mecánicas/mejoras menores aprobadas para después; `Development_Context_Index.md` indica qué leer al cambiar de chat/sesión.

## Estado actual

### M41 — NPC Combat / AI Foundation after Prueba 3

Estado operativo:

`IN PROGRESS — PRUEBA 3 CORRECTION PASS BEFORE PHASE 8A`

Plan activo: `NPC_AI_Sanitation_Plan.md`.

Evidencia manual activa: `Prueba_3_Findings.md`.

Research/decision record de targeting: `NPC_Combat_Targeting_Research.md`.

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
- Correction Pass A `321f26d1d3c1e765e19e86ab66f316238734c8fe`.

No reabrir estas autoridades por inercia. Una regresión real puede justificar cambios, pero no otra reconstrucción global de IA.

## Qué confirmó Prueba 3

Prueba 3/3.1/3.2 confirmó en ejecución real:

- Ambient roaming White/Blue/Red físico y sostenido;
- Gaze/FOV/LOS productivos y LostContact/Search visibles;
- representación humana/anatomía explícita funcionando;
- Dead/Inactive deja de ejecutar acciones activas;
- el patrón extremo previo de impactos sólo en piernas no volvió a reproducirse cualitativamente, aunque `ISSUE-0008` sigue sin muestra estadística suficiente;
- F6 mezcla snapshots históricos con estado actual y puede dibujar FOV/LOS desde un origen viejo o mostrar `Perceived` en un actor Dead/Inactive;
- F6 sigue permitiendo world visuals de un solo NPC seleccionado a la vez;
- Prueba 3 confirmó contaminación Player↔NPC; Correction Pass A ya la resuelve con el toggle development-only `Invisible to AI` sin cambiar física, Perception ni afiliación;
- incapacidad temporal corta Encounter, borra el contexto de pelea y provoca `KO → Ambient → recuperación → redescubrimiento → encounter nuevo`;
- no existe un minimum real-time dwell explícito para knockout/unconscious antes de permitir recuperación fisiológica.

Detalle/evidencia: `Prueba_3_Findings.md`.

## Decisión de producto — KO y memoria de combate

`KO / Unconscious != Dead`.

Contrato V1 deseado:

- el actor noqueado deja de representar amenaza activa y no recibe ataques deliberados mientras siga incapacitado;
- atacante y noqueado conservan identidad/contexto del enemigo reciente;
- al recuperar capacidad pueden reanudar la pelea sin tratarse como desconocidos recién descubiertos;
- la memoria no da wallhack: la posición actual sólo puede venir de Perception; si se perdió LOS, sólo se conserva información legítimamente conocida/LKP y aplica Search;
- debe existir un minimum real-time KO dwell configurable antes de permitir recovery; luego la fisiología vigente decide si puede despertar;
- no crear un `MemorySystem`, blackboard o planner general para este contrato.

## Problema de targeting todavía activo

### ISSUE-0008 — posible sesgo de impactos hacia piernas/pies

Estado: `SUSPECTED / P1` hasta validación estadística reproducible.

La investigación de repo posterior a Fase 7 mantiene una hipótesis fuerte:

- firearm aim de `HumanEncounterAIController` todavía toma el `ActorLocomotionCollider` del target y usa `bounds.center` como base aim point;
- los `ActorCombatHitRegion` se ignoran para aim para no alterar targeting durante F7, por lo que la IA sigue apuntando a un centro técnico de locomoción;
- ese punto queda más bajo que el center-mass humano;
- el spread actual es radial/simétrico y no muestra por código una inclinación vertical deliberada.

Prueba 3 aporta evidencia cualitativa favorable — varios Torso/Arm y no un dominio extremo de piernas — pero no autoriza cerrar el issue ni retunear spread.

## Dirección arquitectónica elegida

No implementar `aim at Torso` dentro de Human Encounter como solución permanente.

La dirección objetivo sigue siendo genérica:

`Target → Primary Aim Point → shooter focus/context error → weapon → PhysicalShotPathResolver → actual hit → receiver`.

El target define dónde es razonable intentar impactarlo; el shooter define cuánto error tiene; el arma define sus parámetros; la física decide dónde pegó; el receptor interpreta el impacto.

## Próximo paso exacto — Prueba 3 Correction Pass

Antes de F8A quedan estos correctivos que hoy invalidan o distorsionan pruebas de combate:

1. **F6 observability correctness + multi-NPC mínimo** — corregir current-vs-last/origin stale y permitir gaze/FOV/LOS simultáneo para todos; la selección sólo controla el inspector profundo.
2. **KO / combat memory continuity** — incapacidad suspende amenaza/acciones, no borra el enemigo/contexto reciente.
3. **Minimum KO dwell** — bloquear recuperación demasiado inmediata por aceleración del `WorldClock`.
4. **Prueba 3.3 controlada** — 1 Blue vs 1 Red con Player Invisible y observabilidad simultánea.

Este correction pass es pequeño y está antes de F8A porque mejora la fiabilidad de las próximas mediciones. No se convierte en un framework nuevo.

## Secuencia inmediata revisada

- Prueba 3 Correction Pass — F6 correctness/multi-NPC mínimo + KO memory/dwell (`Invisible` completado).
- Prueba 3.3 — 1v1 limpio.
- F8A — Aim Bias Evidence.
- F8B — Generic target-side Primary Aim Point, sólo si 8A lo justifica.
- F8C — comparación before/after con mismas seeds/condiciones, sin retuning.
- F8D — review pequeño de accuracy; ningún factor cambia por defecto.
- F8E — migrar consumers legacy y eliminar actor capsule-only/geometric BodyRegion fallback cuando sea seguro.
- F9 — completar Player Debug, especialmente Invincible y cierre de toggles.
- F10 — completar Observability V2/targeting observability más allá del mínimo adelantado.
- F11–F15 — batería integrada, pruebas NPC/Player, game feel, cleanup y cierre.

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
- memory framework general;
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
