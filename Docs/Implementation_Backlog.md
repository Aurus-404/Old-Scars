# Old Scars — Implementation Backlog

Este documento registra mecánicas, mejoras técnicas y pequeñas capacidades aprobadas que conviene implementar después y no perder entre chats/sesiones. No sustituye al Roadmap, no crea milestones y no es un registro de bugs.

## Qué entra aquí

- una mecánica o mejora concreta que sí queremos implementar;
- una limpieza técnica futura con trigger claro;
- tooling útil que no merece milestone propio;
- una mejora de arquitectura pequeña que debe esperar a otra dependencia.

## Qué NO entra aquí

- bugs/sospechas/resoluciones → `Issue_Registry.md`;
- milestones grandes/IDs/dependencias → `Project_Roadmap.md`;
- tareas inmediatas de la próxima sesión → `Next_Sprints.md`;
- ideas no aprobadas o brainstorming sin decisión.

## Estados

- `READY`: investigación/alcance suficientes; puede convertirse en tarea cuando llegue su dependencia/turno.
- `PLANNED`: aprobado, pero aún necesita evidencia/diseño menor o no es próximo.
- `DEFERRED`: aprobado como dirección, pero no debe implementarse hasta que exista un trigger/consumer real.
- `DONE`: implementado y validado; se conserva el historial cuando tenga valor de continuidad.

## Campos por entrada

- ID
- Nombre
- Estado
- Fecha/origen
- Qué queremos
- Por qué
- Trigger/dependencias
- Límites de alcance
- Relación con roadmap/issues cuando corresponda

---

## IMPL-0001 — Primary Aim Point genérico del lado del target

- **Estado:** `READY` condicionado a Fase 8A.
- **Fecha/origen:** 2026-09-03 — investigación repo + comparación Source/Unreal.
- **Qué queremos:** un punto primario genérico que cada target/representation exponga como ubicación razonable para aim normal. Humano → center mass; futuros animales/robots → punto equivalente definido por ellos.
- **Por qué:** `HumanEncounterAIController` no debería inspeccionar anatomía/colliders internos del target para adivinar su center mass. El contrato debe servir para humanos y targets no humanoides.
- **Trigger/dependencias:** Fase 8A debe confirmar que el aim actual sobre locomotion center contribuye a `ISSUE-0008`.
- **Límites:** un único Primary Aim Point V1. Sin weak points, scoring, head targeting, mobility targeting, enums grandes ni manager.
- **Relación:** NPC Sanitation F8B; `ISSUE-0008`.

## IMPL-0002 — Instrumentación reproducible de distribución de disparos NPC

- **Estado:** `READY`.
- **Fecha/origen:** 2026-09-03 — Prueba 2 + investigación de aim.
- **Qué queremos:** diagnostic controlado que registre aim source/point, focus/spread, shot origin/direction, collider/hit point, BodyRegion y miss bajo seeds/condiciones reproducibles.
- **Por qué:** separar target-point, spread, origin y geometría antes de retunear gameplay.
- **Trigger/dependencias:** Fase 8A, después del Prueba 3 Correction Pass para evitar contaminación del Player/tooling stale.
- **Límites:** tooling/diagnostic; no cambiar balance ni accuracy durante la medición.
- **Relación:** `ISSUE-0008`.

## IMPL-0003 — Revisión mínima de accuracy después del target-point fix

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03 — investigación comparativa de IA shooter.
- **Qué queremos:** revisar uno por uno Focus, distance penalty, target movement, shooter movement, automatic burst y contribución del arma sólo después de corregir/validar el punto base de aim.
- **Por qué:** evitar reemplazar un problema geométrico con retuning arbitrario y evitar un Accuracy V2 sobrediseñado.
- **Trigger/dependencias:** Fase 8C completa.
- **Límites:** medir primero; ningún factor se elimina o expande por intuición.
- **Relación:** NPC Sanitation F8D.

## IMPL-0004 — Convertir el spread de arma debug en contrato productivo mínimo

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03 — revisión de `firearm_profiles`.
- **Qué queremos:** si la evidencia lo requiere, reemplazar/renombrar `debug_accuracy_spread` por una contribución productiva simple de error mecánico/base del arma.
- **Por qué:** hoy los perfiles principales observados aportan 0 y gran parte de la identidad de precisión vive en IA.
- **Trigger/dependencias:** después de `IMPL-0001/0003`; sólo si diferentes armas necesitan realmente distinguir precisión.
- **Límites:** no crear `AccuracyProfile`, recoil/ergonomics/MOA/heat/stability frameworks.

## IMPL-0005 — Migrar consumers restantes fuera de actor capsule-only

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03 — decisión posterior a Fase 7.
- **Qué queremos:** migrar perfiles/fixtures humanos que todavía dependan de representación legacy capsule-only hacia representación 3D válida con contratos explícitos.
- **Por qué:** la cápsula legacy fue compatibilidad transicional de Fase 7 y no forma parte de la arquitectura final.
- **Trigger/dependencias:** Fase 8 estabilizada; identificar consumers reales antes de borrar fallback.
- **Límites:** no eliminar la cápsula técnica invisible de locomoción si NavMesh/collision todavía la necesita.
- **Relación:** NPC Sanitation F8E.

## IMPL-0006 — Eliminar fallback visual capsule-only

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03.
- **Qué queremos:** retirar `missing representation → GameObject.CreatePrimitive(Capsule)` cuando todos los consumers legítimos estén migrados.
- **Por qué:** una representación faltante debe ser un error/configuración inválida, no crear silenciosamente un actor ficticio.
- **Trigger/dependencias:** `IMPL-0005` completa.
- **Límites:** conservar `ActorLocomotionCollider` técnico si corresponde.
- **Relación:** NPC Sanitation F8E.

## IMPL-0007 — Eliminar BodyRegion geométrico legacy por cápsula

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03.
- **Qué queremos:** retirar inferencia `bounds/hitPoint → BodyRegion` cuando todos los actores combatibles relevantes usen `ActorCombatHitRegion` explícito.
- **Por qué:** la anatomía productiva ya no debe depender de porcentajes de una cápsula.
- **Trigger/dependencias:** migración anatómica completa; diagnostics reemplazados.
- **Límites:** no quitar fallback mientras haya consumers legítimos no migrados.
- **Relación:** NPC Sanitation F8E.

## IMPL-0008 — Player Debug: Invisible-to-AI

- **Estado:** `DONE`.
- **Fecha/origen:** Prueba 2; prioridad elevada por Prueba 3.1/3.2.
- **Resolución:** Correction Pass A, `321f26d1d3c1e765e19e86ab66f316238734c8fe`. Toggle `Invisible to AI` en `ActorNeedsDebugPanel`; ON aplica un marker target-side efímero que excluye al Player de acquisition automática y libera su threat actual; OFF conserva elegibilidad normal.
- **Validación:** `M41 Player Invisible-to-AI Diagnostics: PASS` con OFF → Player, ON → Blue y OFF → Player; no altera Perception/FOV/LOS, colliders, combat, input ni persistence.
- **Relación:** `ISSUE-0013` resuelto; slice mínimo de F9 adelantado para QA. `IMPL-0009` Invincible queda pendiente.

## IMPL-0009 — Player Debug: Invincible

- **Estado:** `PLANNED`.
- **Fecha/origen:** Prueba 2.
- **Qué queremos:** toggle que permita detection, physical hit, regions, wounds/pain/bleeding/trauma reales pero bloquee la transición terminal a Dead durante QA.
- **Por qué:** probar NPC→Player durante períodos largos sin reiniciar la prueba.
- **Trigger/dependencias:** después de Fase 8 / cierre del correction pass.
- **Límites:** OFF debe ser gameplay normal; no sustituir el pipeline de daño por mocks.
- **Relación:** NPC Sanitation F9.

## IMPL-0010 — Observability V2 multi-NPC

- **Estado:** `READY — MINIMUM SLICE IMMEDIATE`.
- **Fecha/origen:** Prueba 2; prioridad elevada por Prueba 3.
- **Qué queremos:** overlay global compacto multi-NPC más inspector profundo del seleccionado. El mínimo adelantado debe mostrar gaze/FOV/LOS simultáneamente para varios/todos los NPC y distinguir current vs last evidence; F10 completará targeting/shot observability más rica.
- **Por qué:** Prueba 3 confirmó que selección única dificulta comparar ambos lados de un encounter y que world lines basadas en snapshots históricos pueden quedar atrás del actor o parecer salir del piso.
- **Trigger/dependencias:** Prueba 3 Correction Pass B antes de F8A; expansión completa en F10.
- **Límites:** no crear debug framework general; usar datos read-only de producción; no duplicar Perception/raycasts para inventar una segunda verdad.
- **Relación:** `ISSUE-0010`, `ISSUE-0011` y issue de snapshot stale de Prueba 3.

## IMPL-0011 — Observabilidad de targeting/accuracy

- **Estado:** `PLANNED`.
- **Fecha/origen:** investigación 2026-09-03.
- **Qué queremos:** en tooling de combate, exponer target, Primary Aim Point, focus, current spread, shot origin/direction, hit collider/region y miss cuando esos contratos existan.
- **Por qué:** diagnosticar game feel sin logs masivos ni inferencias visuales.
- **Trigger/dependencias:** `IMPL-0001` + F10.
- **Límites:** visualización read-only; no alterar aim.

## IMPL-0012 — Fire-control más weapon-driven cuando existan múltiples arquetipos reales

- **Estado:** `DEFERRED`.
- **Fecha/origen:** investigación comparativa Source/STALKER/Insurgency, 2026-09-03.
- **Qué queremos:** permitir que weapon data contribuya de forma simple a cadence/burst/rest/precision cuando bolt-action, SMG, shotgun, MG, etc. realmente lo necesiten.
- **Por qué:** no conviene que toda identidad de disparo viva para siempre hardcodeada en `HumanEncounterAIController`.
- **Trigger/dependencias:** al menos dos/tres arquetipos productivos que requieran comportamiento distinto demostrado.
- **Límites:** no implementar ahora; sin WeaponHandling/FireControl framework especulativo.

## IMPL-0013 — Abstracción de método de ataque sólo al aparecer el primer consumidor no-firearm

- **Estado:** `DEFERRED`.
- **Fecha/origen:** investigación sobre targets/attackers no humanoides, 2026-09-03.
- **Qué queremos:** eventualmente separar `Threat/Attack Intent` del método concreto (rifle, melee, mordida, embestida, etc.) cuando exista un segundo tipo real de atacante.
- **Por qué:** futuros animales/mutantes no deben forzar casos especiales dentro de Human Encounter.
- **Trigger/dependencias:** primer atacante productivo cuyo método no encaje en el ciclo firearm/melee actual.
- **Límites:** no crear `AttackSolver`/capability framework antes del consumer.

## IMPL-0014 — Continuidad de memoria de combate durante KO temporal

- **Estado:** `READY — IMMEDIATE`.
- **Fecha/origen:** 2026-09-03 — Prueba 3.1/3.2 + decisión de producto.
- **Qué queremos:** cuando un enemigo queda incapacitado/noqueado, dejar de tratarlo como amenaza activa y detener ataques deliberados, pero conservar la identidad/contexto del enemigo reciente tanto en atacante como en noqueado. Al recuperar capacidad, el actor puede reanudar el conflicto sin redescubrir al rival como si nunca hubiera existido.
- **Por qué:** el contrato actual borra Encounter/memoria y produce `KO → Ambient → recovery → redescubrimiento → encounter nuevo`, visible como combatientes que se golpean, pasean y vuelven a pelear.
- **Trigger/dependencias:** Prueba 3 Correction Pass C, antes de F8A.
- **Límites:** un seam mínimo dentro de autoridades existentes; sin memory framework, relationship history general, planner o blackboard. La memoria no entrega posición actual ni wallhack: Perception/LKP/Search siguen siendo autoridad espacial. Death sigue terminal.
- **Relación:** issue confirmado de Prueba 3 sobre incapacidad/Encounter.

## IMPL-0015 — Minimum real-time knockout dwell

- **Estado:** `READY — IMMEDIATE`.
- **Fecha/origen:** 2026-09-03 — Prueba 3.1/3.2 + decisión de producto.
- **Qué queremos:** un mínimo configurable en tiempo real durante el cual un actor que quedó KO/unconscious no puede recuperar capacidad activa. Después del mínimo, `ActorConditionComponent`/fisiología vigente decide si puede despertar.
- **Por qué:** el `WorldClock` acelerado puede reducir trauma y cruzar thresholds de recuperación demasiado rápido para que el knockout se sienta como una pérdida real de conciencia.
- **Trigger/dependencias:** Prueba 3 Correction Pass D; validar luego en Prueba 3.3.
- **Límites:** no reemplazar physiology/thresholds; no fijar duración final de balance sin playtest; no crear otro reloj global.
- **Relación:** issue de recovery/KO de Prueba 3.

## IMPL-0016 — Integrar visuales de equipment en `humanoid_standard`

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03 — Prueba 3 runtime warnings.
- **Qué queremos:** permitir que la representación humana runtime exponga el seam visual necesario para que Equipment real pueda sincronizar visuales cuando exista contenido/attachment correspondiente.
- **Por qué:** durante Prueba 3 `EntityEquipmentVisualSynchronizer` reporta que `humanoid_standard(Clone)` no tiene `IEquipmentVisualSource`; el estado de Equipment existe pero la representación debug no puede reflejarlo.
- **Trigger/dependencias:** después de cerrar NPC Foundation/aim, o antes si la falta de visuales impide validar un feature concreto.
- **Límites:** no convertir F7 en sistema de animación/IK ni requerir arte final; reutilizar el framework visual existente y evitar una autoridad paralela.

## IMPL-0017 — Timed Bandaging V1 compartido y self-treatment NPC

- **Estado:** `DONE`.
- **Fecha/origen:** 2026-09-04 — slice médico autorizado fuera del orden del Correction Pass.
- **Resolución:** `ActorWoundTreatmentController` por actor ejecuta bandaging real de 4 s con exact-instance commit; Player puede caminar, sprint/combat cancelan, y NPC usa inventario real tras calma determinista o por emergencia hemorrágica derivada de physiology/WorldClock.
- **Validación:** `Timed Bandaging / NPC Self-Treatment Diagnostics: PASS`, M39.0, Consciousness, Player Controls/Health Window, M40.0, Human Encounter, Search V1, Behavior Ownership e Inventory Interaction `PASS`.
- **Límites:** no persiste progreso; sin Blood Trails, carry-weight changes, NPC sprint authority, Medical AI state, scheduler ni generic action manager.

---

## Regla de mantenimiento

Cuando una entrada se convierta en trabajo inmediato, `Next_Sprints.md` debe referenciar su ID. Cuando se implemente y valide, puede pasar a `DONE` o eliminarse sólo si no aporta historial; una decisión que explique arquitectura futura debería conservarse. Un item no debe crecer automáticamente hasta convertirse en milestone: si el alcance ya es grande, moverlo explícitamente al Roadmap mediante decisión de producto.
