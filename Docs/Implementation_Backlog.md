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
- **Por qué:** `HumanEncounterAIController` no debería inspeccionar anatomía/colliders internos del target para adivinar su center mass.
- **Trigger/dependencias:** Fase 8A debe confirmar que el aim actual contribuye materialmente a `ISSUE-0008`.
- **Límites:** un único Primary Aim Point V1. Sin weak points, scoring, head targeting, mobility targeting, enums grandes ni manager.
- **Relación:** NPC Sanitation F8B; `ISSUE-0008`.

## IMPL-0002 — Instrumentación reproducible de distribución de disparos NPC

- **Estado:** `READY`.
- **Fecha/origen:** 2026-09-03 — Prueba 2 + investigación de aim.
- **Qué queremos:** diagnostic controlado que registre aim source/point, focus/spread, shot origin/direction, collider/hit point, BodyRegion y miss bajo seeds/condiciones reproducibles.
- **Por qué:** separar target-point, spread, origin y geometría antes de retunear gameplay.
- **Trigger/dependencias:** Fase 8A después de Prueba 3.3.
- **Límites:** tooling/diagnostic; no cambiar balance ni accuracy durante la medición.
- **Relación:** `ISSUE-0008`.

## IMPL-0003 — Revisión mínima de accuracy después del target-point fix

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03 — investigación comparativa de IA shooter.
- **Qué queremos:** revisar uno por uno Focus, distance penalty, target movement, shooter movement, automatic burst y contribución del arma sólo después de corregir/validar el punto base de aim.
- **Por qué:** evitar reemplazar un problema geométrico con retuning arbitrario.
- **Trigger/dependencias:** Fase 8C completa y evidencia residual real.
- **Límites:** medir primero; ningún factor se elimina o expande por intuición.
- **Relación:** NPC Sanitation F8D.

## IMPL-0004 — Convertir el spread de arma debug en contrato productivo mínimo

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03 — revisión de `firearm_profiles`.
- **Qué queremos:** si la evidencia lo requiere, reemplazar/renombrar `debug_accuracy_spread` por una contribución productiva simple de error mecánico/base del arma.
- **Por qué:** hoy gran parte de la identidad de precisión vive en IA.
- **Trigger/dependencias:** después de `IMPL-0001/0003`; sólo si armas reales necesitan distinguir precisión.
- **Límites:** no crear `AccuracyProfile`, recoil/ergonomics/MOA/heat/stability frameworks.

## IMPL-0005 — Migrar consumers restantes fuera de actor capsule-only

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03 — decisión posterior a Fase 7.
- **Qué queremos:** migrar perfiles/fixtures humanos que todavía dependan de representación legacy capsule-only hacia representación 3D válida con contratos explícitos.
- **Por qué:** la cápsula legacy fue compatibilidad transicional de Fase 7.
- **Trigger/dependencias:** Fase 8 estabilizada; identificar consumers reales antes de borrar fallback.
- **Límites:** no eliminar la cápsula técnica invisible de locomoción si NavMesh/collision todavía la necesita.
- **Relación:** NPC Sanitation F8E.

## IMPL-0006 — Eliminar fallback visual capsule-only

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03.
- **Qué queremos:** retirar `missing representation → GameObject.CreatePrimitive(Capsule)` cuando todos los consumers legítimos estén migrados.
- **Por qué:** una representación faltante debe ser error/configuración inválida, no crear silenciosamente un actor ficticio.
- **Trigger/dependencias:** `IMPL-0005` completa.
- **Límites:** conservar `ActorLocomotionCollider` técnico si corresponde.
- **Relación:** NPC Sanitation F8E.

## IMPL-0007 — Eliminar BodyRegion geométrico legacy por cápsula

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03.
- **Qué queremos:** retirar inferencia `bounds/hitPoint → BodyRegion` cuando todos los actores combatibles relevantes usen `ActorCombatHitRegion` explícito.
- **Por qué:** anatomía productiva no debe depender de porcentajes de una cápsula.
- **Trigger/dependencias:** migración anatómica completa; diagnostics reemplazados.
- **Límites:** no quitar fallback mientras haya consumers legítimos no migrados.
- **Relación:** NPC Sanitation F8E.

## IMPL-0008 — Player Debug: Invisible-to-AI

- **Estado:** `DONE`.
- **Fecha/origen:** Prueba 2; prioridad elevada por Prueba 3.1/3.2.
- **Resolución:** Correction Pass A, `321f26d1d3c1e765e19e86ab66f316238734c8fe`. Toggle `Invisible to AI` en Runtime Debug Tools; ON excluye al Player de acquisition automática y libera su threat automático actual; OFF conserva elegibilidad normal.
- **Validación:** diagnostic WorldRuntime OFF → Player, ON → Blue y OFF → Player `PASS`; no altera Perception/FOV/LOS, colliders, combat, input ni persistence.
- **Relación:** `ISSUE-0013` resuelto.

## IMPL-0009 — Player Debug: Invincible

- **Estado:** `PLANNED`.
- **Fecha/origen:** Prueba 2.
- **Qué queremos:** toggle que permita detection, physical hit, regions, wounds/pain/bleeding/trauma/KO reales pero bloquee coherentemente la transición terminal a Dead durante QA.
- **Por qué:** probar NPC→Player durante períodos largos sin reiniciar la prueba.
- **Trigger/dependencias:** después de estabilizar KO y F8 targeting; antes de QA Player final.
- **Límites:** OFF = gameplay normal; no sustituir daño por mocks, no volver intangible al Player, no curar ni resucitar al apagar.
- **Relación:** NPC Sanitation F9; `ISSUE-0012`.

## IMPL-0010 — Observability V2 multi-NPC

- **Estado:** `MINIMUM SLICE DONE / ACCEPTED / PUBLISHED — FULL F10 PENDING`.
- **Fecha/origen:** Prueba 2; prioridad elevada por Prueba 3.
- **Qué queremos:** overlay global compacto multi-NPC + inspector profundo del seleccionado. El mínimo adelantado muestra gaze/FOV/LOS simultáneamente y distingue CURRENT vs LAST; F10 completa targeting/shot observability.
- **Cierre mínimo:** `5aac763c14c399bfe09a3e925c50698658ad2716`; diagnostics PASS y aceptación visual manual final confirmada por Mauro el 2026-09-06.
- **Pendiente:** F10 completo, incluyendo targeting/shot observability.
- **Límites:** no crear debug framework general; usar datos read-only de producción; no duplicar Perception/raycasts como segunda verdad.
- **Relación:** `ISSUE-0010`, `ISSUE-0011`, `ISSUE-0019`.

## IMPL-0011 — Observabilidad de targeting/accuracy

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03.
- **Qué queremos:** exponer target, Primary Aim Point, focus, spread, shot origin/direction, hit collider/region y miss cuando esos contratos existan.
- **Por qué:** diagnosticar game feel sin logs masivos ni inferencias visuales.
- **Trigger/dependencias:** F8A genera el mínimo reutilizable; F10 lo integra al tooling estable.
- **Límites:** visualización read-only; no alterar aim.

## IMPL-0012 — Fire-control más weapon-driven cuando existan múltiples arquetipos reales

- **Estado:** `DEFERRED`.
- **Fecha/origen:** investigación comparativa Source/STALKER/Insurgency, 2026-09-03.
- **Qué queremos:** permitir que weapon data contribuya de forma simple a cadence/burst/rest/precision cuando bolt-action, SMG, shotgun, MG, etc. realmente lo necesiten.
- **Trigger/dependencias:** al menos dos/tres arquetipos productivos con necesidad demostrada.
- **Límites:** sin WeaponHandling/FireControl framework especulativo.

## IMPL-0013 — Abstracción de método de ataque sólo al aparecer el primer consumidor no-firearm

- **Estado:** `DEFERRED`.
- **Fecha/origen:** investigación 2026-09-03.
- **Qué queremos:** separar `Threat/Attack Intent` del método concreto sólo cuando exista un atacante real que no encaje en firearm/melee actual.
- **Trigger/dependencias:** primer consumidor no-firearm productivo.
- **Límites:** no crear `AttackSolver`/capability framework antes del consumer.

## IMPL-0014 — Continuidad de memoria de combate durante KO temporal

- **Estado:** `DONE / PUBLISHED`, P3 `394d01886b8c6697ca2d492c4450282f561ba688`, 2026-09-07.
- **Fecha/origen:** 2026-09-03 — Prueba 3.1/3.2 + decisión de producto.
- **Qué queremos:** al incapacitar/noquear a un enemigo, dejar de tratarlo como amenaza activa y detener ataques deliberados, pero conservar identidad/contexto mínimo del enemigo reciente. Recovery puede reanudar conflicto sin redescubrimiento artificial.
- **Por qué:** el contrato anterior producía `KO → Ambient → recovery → rediscovery → encounter nuevo`.
- **Trigger/dependencias:** después de estabilizar el minimum KO dwell; antes de Prueba 3.3.
- **Límites:** memoria no entrega posición oculta; Perception/LKP/Search siguen siendo autoridad espacial. Recordar identidad no equivale a `Threat != null` y no debe bloquear por sí solo self-treatment/AmbientTopOff. Death sigue terminal. Sin MemorySystem/blackboard/planner.
- **Relación:** `ISSUE-0020`.
- **Implementado/validado:** una identidad reciente en Encounter, separada de Threat y sin posición; ventana real configurable `60 s` Core provisional, pausada por incapacidad propia y renovada por observaciones legítimas. Recognition/Perception reanuda Fighting sin otro Alerted; expiry/death/invalidation/reemplazo limpia. P3 y ocho regresiones PASS, sin bloquear routine treatment ni AmbientTopOff. Próximo P4 Prueba 3.3; no ejecutado.

## IMPL-0015 — Minimum real-time knockout dwell

- **Estado:** `DONE / PUBLISHED (P2)` — `9ca0335cdc8b85bd49d20ddbe97ad814f44c8578`, 2026-09-07.
- **Fecha/origen:** 2026-09-03 — Prueba 3.1/3.2 + decisión de producto.
- **Qué queremos:** mínimo configurable de tiempo real durante el cual un actor realmente `Unconscious` no puede recuperar active behavior. Después del mínimo, `ActorConditionComponent`/fisiología vigente decide si puede despertar.
- **Por qué:** `WorldClock` acelerado puede cruzar thresholds demasiado rápido en tiempo real.
- **Trigger/dependencias:** después de F6 aceptado/publicado; antes de `IMPL-0014` y Prueba 3.3.
- **Límites:** no extender automáticamente a toda `Incapacitated`; no reemplazar physiology/thresholds; el timer no fuerza wake-up; death terminal; no crear otro reloj global. Save/load debe tener semántica que no permita bypass accidental.
- **Relación:** `ISSUE-0021`.
- **Implementado/validado:** gate en Condition compartida, `5 s` Core inicial de prueba, restante durable sin progreso offline, legacy seguro; P2 x1/x100, sesión Play nueva y regresiones proporcionales PASS. No balance final ni P3.

## IMPL-0016 — Integrar visuales de equipment en `humanoid_standard`

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-03 — Prueba 3 runtime warnings.
- **Qué queremos:** permitir que la representación humana runtime exponga el seam visual necesario para que Equipment real sincronice visuales cuando exista contenido/attachment correspondiente.
- **Por qué:** Equipment existe pero la representation debug no siempre lo refleja.
- **Trigger/dependencias:** después de cerrar NPC Foundation/aim, o antes sólo si un gate concreto exige arma físicamente visible.
- **Límites:** no convertir F7 en IK/animation system ni crear otra autoridad de Equipment.

## IMPL-0017 — Timed Bandaging V1 compartido y self-treatment NPC

- **Estado:** `DONE`.
- **Fecha/origen:** 2026-09-04.
- **Resolución:** `ActorWoundTreatmentController` por actor ejecuta bandaging real de 4 s con exact-instance commit; Player puede caminar, sprint/combat cancelan; NPC usa inventario real tras calma determinista o emergencia hemorrágica.
- **Validación:** Timed Bandaging/NPC Self-Treatment, M39.0, Consciousness, Player Controls/Health Window, M40.0, Human Encounter, Search V1, Behavior Ownership e Inventory Interaction `PASS`.
- **Límites:** progreso no persistido; sin scheduler/action manager general.

## IMPL-0018 — Blood Trails V1: marcas médicas por distancia

- **Estado:** `DONE`.
- **Fecha/origen:** 2026-09-05.
- **Resolución:** `ActorBloodTrailEmitter` observa bleeding médico real y solicita marcas por distancia al pool global. V1.1 usa diámetro base `0,25 m` configurable y `RaycastNonAlloc`; reutiliza renderer/material R0.
- **Validación:** V1.1/R0, M39.0, Timed Bandaging y M38 Actor Lifecycle `PASS`; Player/NPC, terrain/piso/slope, filtros, x1/x100, budget, expiry/recycling y evidencia RenderTexture validados.
- **Límites:** textura provisional; sin persistence, puddles, spray, tracking AI, footprints ni weather cleanup.

## IMPL-0019 — NPC Opportunistic Reload

- **Estado:** `DONE`.
- **Fecha/origen:** 2026-09-05.
- **Resolución:** `HumanEncounterAIController` usa `AmbientTopOff` en ventana segura y `EmptyWeapon` durante Fighting/LostContact/Search/Ambient seguro. Empty reload conserva instance/completion a través de transitions; top-off se cancela al aparecer threat. `WeaponCombatService.ReloadEquipped` sigue siendo autoridad transaccional.
- **Validación:** M41 NPC Opportunistic Reload, M40.0, Human Encounter, Search V1 y Timed Bandaging/NPC Self-Treatment `PASS`.
- **Límites:** sin weapon switching/fallback, planner, FireControl ni cambios Player reload.

## IMPL-0020 — Carry Weight / Encumbrance compartido

- **Estado:** `READY AFTER M41 + ISSUE-0022`.
- **Fecha/origen:** 2026-09-06 — decisión de producto + auditoría profunda de repo/Astra.
- **Qué queremos:** redefinir Carry Capacity como capacidad física de transporte, NO como límite de storage. Player y NPC comparten el mismo contrato locomotor.
- **Contrato aprobado:** `0..75%` sin penalización; `>75%..100%` penalización progresiva; `100%` aún móvil; `>100%` traslación cero. Inventory/transfer/drop/equipment/use/reload/treatment siguen bajo sus propias autoridades y pueden operar sobrecargados.
- **Por qué:** el hard limit actual mezcla storage acceptance con consecuencia física; además futuros Leg Impairment y Encumbrance deben componerse sin acoplar Carry/Medical.
- **Trigger/dependencias:** cerrar M41/F8 para no contaminar accuracy; resolver primero `ISSUE-0022` loaded ammo mass.
- **Límites:** sin Strength, stats, backpack capacity modifiers, cache global, Limb Impairment ni rebalanceo de accuracy. `Overloaded != Incapacitated`.
- **Riesgos conocidos:** retirar todos los vetos/clamps de peso sin romper grid/stack/access/ownership/rollback; NPC spawn sin Carry actual; sprint falso; Search deadline y Navigation reversible; restore sobrecargado debe conservar items.

## IMPL-0021 — Localized Limb Impairment

- **Estado:** `PLANNED AFTER IMPL-0020`.
- **Fecha/origen:** 2026-09-06 — dirección de producto ya aprobada.
- **Qué queremos:** que heridas localizadas reales produzcan consecuencias funcionales sin introducir limb HP paralelo.
- **Dirección:** piernas afectan locomoción/sprint; brazos afectan handling/reload/melee cuando sus reglas y consumers estén definidos. Bandaging reduce bleeding, no repara automáticamente impairment.
- **Por qué:** hacer que regiones/wounds existentes tengan consecuencias sistémicas y compatibles con Carry/locomotion.
- **Trigger/dependencias:** Encumbrance compartido y seam locomotor estable; después definir severidad/recuperación de cada consumer.
- **Límites:** Medical y Carry no deben conocerse entre sí; sin targeting de miembros, limb HP, framework universal ni curación implícita por vendaje.

## IMPL-0022 — Contenedores especializados data-driven: filtros y stack local

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-07 — decisión de producto; caso inicial: cajetilla de cigarrillos.
- **Qué queremos:** extender los contenedores existentes con restricciones de contenido data-driven. `canHold` debe aceptar selectores inequívocos por ID exacto, categoría o tag (por ejemplo `item:common_matches_01`, `category:tobacco`, `tag:tobacco.cigarette`) con semántica OR. El contenedor también debe poder imponer un límite de stack local por slot independiente del `maxStack` global del ítem.
- **Contrato de stack:** el límite efectivo dentro de un slot especializado es `min(maxStack del ítem, maxStackPerSlot del contenedor)`. Ejemplo aprobado: cigarrillos `maxStack = 100` en inventario normal; cajetilla `1x1` con `maxStackPerSlot = 20` mantiene como máximo 20 dentro sin alterar el stack global del cigarrillo.
- **Autoridad:** toda transferencia/inserción debe validar estas reglas en la autoridad central de inventario/transferencias; la UI sólo refleja el resultado. No crear inventarios específicos por objeto como `CigaretteBoxInventory`.
- **Contenido inicial:** las reglas de aceptación (`canHold`) permanecen separadas de `initialContents`/loot inicial. Que un contenedor admita un ítem no implica que deba generarse con él.
- **Por qué:** permite cajetillas, cajas de munición, botiquines, estuches y contenedores modded reutilizando el mismo sistema, y permite añadir nuevas variantes de contenido mediante tags/categorías sin editar cada contenedor.
- **Trigger/dependencias:** cuando se retome la expansión de inventario/contenedores o aparezca el primer contenedor especializado productivo que necesite esta capacidad.
- **Límites:** V1 sin expresiones booleanas complejas, query DSL, reglas AND/NOT ni frameworks paralelos de inventario. Mantener IDs/categorías/tags explícitos, data-driven y compatibles con modding.
- **Relación:** Inventory/containers, item definitions, JSON/modding content pipeline.

## IMPL-0023 — Placeables físicos data-driven y attachments al mundo

- **Estado:** `PLANNED`.
- **Prioridad:** `ALTA` — debe tratarse como restricción de diseño transversal desde ahora aunque su implementación completa sea futura.
- **Fecha/origen:** 2026-09-10 — decisión de producto tras analizar Crewman Placeables de MTC y generalizar el concepto para Old Scars.
- **Qué queremos:** un sistema general de entidades físicas placeable que puedan existir como objeto suelto/inventariable, colocarse en el mundo y, cuando sus reglas lo permitan, fijarse a vehículos u otras superficies. No debe ser un sistema exclusivo de vehículos.
- **Contrato base:** cada placeable conserva datos propios data-driven relevantes para sus interacciones: material/categorías/tags, masa física y, cuando corresponda, propiedades balísticas como espesor/blindaje de sus superficies. La compatibilidad de montaje debe depender de propiedades de la pieza, la superficie receptora y el método/herramienta requerido, no de listas hardcodeadas por vehículo.
- **Attachment/prerequisitos:** permitir uniones físicas justificadas por materiales y herramientas/procesos compatibles; ejemplo aprobado de intención: una chapa metálica puede soldarse a una superficie metálica si el jugador dispone de una herramienta de soldadura adecuada. De forma equivalente, piezas de madera pueden colocarse o anclarse al terreno/superficies mediante herramientas apropiadas. La taxonomía exacta de métodos de unión se define sólo cuando exista el primer consumer concreto.
- **Integración balística:** un placeable con protección balística debe actuar como una capa física real del sistema existente de penetración. El proyectil impacta primero esa superficie; la autoridad de penetración resuelve su material/espesor; si no penetra, se detiene allí; si penetra, puede continuar hacia las superficies subyacentes con el resultado residual correspondiente. Nunca convertir la pieza instalada en un bonus abstracto de armor del objeto padre ni crear una segunda autoridad balística para placeables.
- **Masa/assembly:** la masa de piezas fijadas debe contribuir a la masa física efectiva del conjunto/host sin mutar su masa base. Ejemplo: vehículo de 300 kg + chapa de 20 kg = conjunto de 320 kg; nuevas piezas siguen acumulando masa.
- **Uso en el mundo:** la misma base debe servir para futuros vehículos, refuerzo de puertas/estructuras, cobertura y barricadas improvisadas, y colocación/anclaje de materiales encontrados en el entorno. La pieza debe conservar su identidad y propiedades independientemente de dónde se utilice.
- **Por qué:** cruza balística/penetración, materiales/tags, física/masa, inventario/world items, interacción, persistence, modding y futuros vehículos. Por eso debe influir en el diseño de esos sistemas antes de que existan vehículos, evitando decisiones actuales que luego impidan una integración física coherente.
- **Trigger/dependencias:** no convertirlo todavía en milestone aislado. Tener `IMPL-0023` en cuenta al diseñar o modificar penetración multicapa, materiales/tags, masa/física, colocación de world items, interacción/herramientas, persistence y vehículos. Implementar el primer slice sólo cuando aparezca un consumer productivo concreto que permita validar el contrato mínimo end-to-end.
- **Límites:** no convertir Old Scars en un building game ni buscar libertad caótica tipo MTC para apilar monstruosidades; no diseñar ahora una taxonomía universal de attachments; no crear clases específicas por vehículo/material; no duplicar física, inventario ni penetración existentes. La libertad de colocación futura debe conservar restricciones físicas y de herramientas coherentes.
- **Relación:** Ballistics/Penetration, item definitions, material tags, Inventory/World Items, physics/mass, Interaction, Persistence, JSON/modding y futuros Vehicles.

---

## Regla de mantenimiento

Cuando una entrada se convierta en trabajo inmediato, `Next_Sprints.md` debe referenciar su ID. Cuando se implemente y valide, puede pasar a `DONE` o eliminarse sólo si no aporta historial. Bugs reales van a `Issue_Registry.md`, no se duplican aquí como features.