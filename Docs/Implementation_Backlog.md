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

- **Estado:** `DONE / ACCEPTED`.
- **Fecha/origen:** 2026-09-03 — Prueba 3 runtime warnings.
- **Resolución:** la composición runtime vincula explícitamente el `ActorEquipmentComponent` real con el `EntityEquipmentVisualSynchronizer` y el `EntityVisualRigRuntime` de la misma representación. Player conserva el mismo seam authored. El tint debug Blue/Red recorre renderers corporales y excluye subárboles `EquippedVisualInstanceMarker`.
- **Validación:** compile Runtime/Editor, diagnóstico IMPL-0016 y cobertura M41.2 `PASS`; aceptación manual Blue/Red confirmó crowbar, Lee-Enfield y small backpack visibles, sin tint de Equipment, sin duplicados ni warning de source faltante.
- **Límites:** sin IK, animation, sockets nuevos, materiales de armas ni otra autoridad de Equipment.

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

## IMPL-0024 — Humedad, secado y protección térmica de la ropa

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre ideas 1–3.
- **Qué queremos:** cada prenda equipada puede mantener una humedad `0..100%`, donde `0%` es seca y `100%` empapada. La humedad aumenta la masa efectiva de la prenda y empeora su efecto térmico mediante valores propios de la definición; no asumir que todas las prendas absorben la misma proporción de su peso. La ropa también expone aislamiento/calor y protección al viento.
- **Secado:** acción contextual equivalente a `Wring out clothes` reduce aproximadamente un porcentaje grande de la humedad actual —70% como intención inicial, no balance congelado—. La humedad restante desciende con tiempo de juego y puede secarse más rápido equipada, colgada o cerca de una fuente de calor.
- **Viento/interiores:** el viento puede afectar al actor según la protección combinada de la ropa. Estar dentro de un interior válido elimina o reduce de forma simple la exposición exterior al viento; no calcular sotavento geométrico alrededor de paredes/rocas.
- **Por qué:** convierte ropa, clima, masa y temperatura en decisiones conectadas sin requerir simulación material detallada.
- **Trigger/dependencias:** cuando se implemente clothing/environment temperature gameplay sobre Equipment y World Climate ya disponibles como foundations.
- **Límites:** sin sistema general de absorción por material, sin mar de tags `wet/dry`, sin simulación de evaporación/humedad ambiental y sin mapas/raycasts de viento alrededor de obstáculos. Valores de absorción, secado y penalización son tuning futuro.
- **Relación:** Equipment, Carry Weight, World Climate/Temperature, Heat Sources, Persistence y JSON/modding.

## IMPL-0025 — Fuentes de calor y combustibles térmicos data-driven

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre ideas 4 y 7, con Vintage Story como referencia conceptual.
- **Qué queremos:** una fuente de calor expone potencia/modificador térmico y radio de acción con caída simple por distancia. Los sistemas interesados consumen ese resultado: actor, ropa secándose, líquidos, comida u otros consumers futuros. Los ítems utilizables como combustible de fogata/horno exponen al menos temperatura máxima alcanzable y duración de combustión.
- **Combustibles:** la capacidad de ser combustible térmico debe ser explícita y separada de conceptos como combustible de vehículo. Leña, carbón u otros fuels pueden diferenciarse por temperatura máxima y duración, permitiendo que un proceso requiera una temperatura que ciertos combustibles no alcanzan.
- **Por qué:** permite cocina, secado y futuros procesos térmicos mediante un contrato común y data-driven en vez de recetas hardcodeadas por combustible.
- **Trigger/dependencias:** primer consumer productivo de fogata/horno/cocción o temperatura local.
- **Límites:** sin combustión química, oxígeno, humedad del combustible, propagación física del fuego ni simulación detallada de humo. La taxonomía exacta del tag/capability se define al implementar el primer consumer.
- **Relación:** Item definitions, World Temperature, Cooking/Crafting, Fluids, Equipment wetness y JSON/modding.

## IMPL-0026 — Evidencia ambiental temporal y cleanup por condiciones del mundo

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre ideas 13 y 67.
- **Qué queremos:** conservar evidencia física/visual de acciones del mundo cuando el sistema correspondiente ya la produzca —por ejemplo sangre existente, puertas abiertas, vidrio roto, casquillos u otros estados físicos— y permitir que marcas temporales opt-in sean eliminadas/aceleradas por condiciones ambientales como lluvia cuando corresponda.
- **Optimización:** la representación visual temporal debe poder usar pooling, budgets y reciclado; el clima puede servir también como regla de cleanup natural. Si en el futuro una evidencia participa de gameplay/eventos, su verdad lógica no debe depender de que el decal/render siga vivo.
- **Por qué:** hace que el mundo comunique actividad pasada sin HUD y al mismo tiempo impone límites claros al coste gráfico.
- **Trigger/dependencias:** ampliar el sistema sólo al aparecer nuevos tipos de evidencia aprobados. Blood Trails V1 ya aporta la primera marca temporal existente.
- **Límites:** esta entrada NO aprueba todavía footprints completos, marcas de paso/disparo generales, tracking de IA ni envejecimiento detallado de huellas; esas ideas siguen pendientes de decisión específica.
- **Relación:** `IMPL-0018`, Weather/Climate, world interaction, pooling/performance y futuros event consumers.

## IMPL-0027 — Sonido de pasos por superficie

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre idea 17.
- **Qué queremos:** seleccionar feedback de pasos según la superficie real bajo el actor, de modo que metal, tierra, madera, grava, interiores, etc. puedan producir audio apropiado sin que cada actor conozca casos hardcodeados.
- **Por qué:** mejora lectura, calidad audiovisual y ofrece una base reutilizable si más adelante Hearing necesita consumir intensidad/material de pasos.
- **Trigger/dependencias:** cuando exista el pipeline productivo de audio/material de superficies.
- **Límites:** inicialmente es feedback audiovisual; no introduce por sí solo Hearing AI, propagación acústica, reverberación compleja ni simulación física de materiales.
- **Relación:** Surface/material definitions, locomotion, Audio y futuro Hearing si se aprueba.

## IMPL-0028 — Tiempo de acceso por almacenamiento y quick access derivado del equipo

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre ideas 19–20.
- **Qué queremos:** añadir una propiedad de acceso —nombre final por definir, preferentemente `accessTime`/`retrieveTime` o multiplicador equivalente— al lugar de almacenamiento/equipment, no al ítem consumido. Sacar munición de una bandolera, una pistola de una funda o un rifle de un sling puede ser más rápido que buscar el mismo objeto en un bolsillo/mochila.
- **Quick access:** los accesos rápidos deben emerger de equipment real: holsters, slings, bandoleras u otros carriers exponen qué objetos pueden extraerse/cambiarse rápidamente. No existe una hotbar mágica independiente del equipo.
- **Autoridad:** la autoridad central de inventory/equipment/action debe calcular la disponibilidad y duración; UI sólo presenta opciones/estado.
- **Por qué:** da valor funcional real al equipo especializado sin bonuses abstractos de velocidad y conecta layout del inventario con combate/manipulación.
- **Trigger/dependencias:** después de estabilizar contenedores especializados/equipment visuals y cuando haya al menos un consumer real de draw/reload/access timing.
- **Límites:** no crear un Action Scheduler general sólo para esto, no añadir una tecla distinta por tipo de extracción y no convertir cada slot en una clase especial.
- **Relación:** `IMPL-0022`, Equipment, Inventory transfers, firearms/ammo y modding data.

## IMPL-0029 — Fluidos volumétricos, mezcla y temperatura

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre ideas 25–29.
- **Qué queremos:** un sistema común de líquidos donde contenedores compatibles mantengan cantidades volumétricas reales, permitan trasvasado parcial y puedan contener mezclas de más de un fluido. El contenido líquido mantiene además temperatura para habilitar consumers como agua caliente para té/café.
- **Mezclas:** la arquitectura no debe asumir `un recipiente = un único liquidId`; debe poder representar proporciones/cantidades. La semántica de qué mezclas son útiles, inválidas o producen nuevas propiedades se define por consumers concretos más adelante.
- **Temperatura:** evitar variantes de ítem como `hot_water`/`cold_water` cuando el mismo contenido puede conservar una temperatura numérica coherente.
- **Por qué:** habilita cocina, bebida, combustible líquido y futuros procesos sin crear sistemas de líquidos específicos por uso.
- **Trigger/dependencias:** primer recipiente/líquido productivo que necesite cantidad parcial o temperatura; integrar después con heat sources/cooking según consumidores.
- **Límites:** por ahora sin residuos persistentes del recipiente, contaminación química de paredes, limpieza compleja, presión ni fluid dynamics. La mezcla no obliga a implementar inmediatamente reacciones químicas.
- **Relación:** Containers/`canHold`, Item Instances, `IMPL-0025`, Cooking/Crafting, Persistence y JSON/modding.

## IMPL-0030 — Deterioro de comida dependiente del almacenamiento

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre ideas 31–32.
- **Qué queremos:** el deterioro de comida no debe ser sólo un countdown fijo por definición; condiciones relevantes de almacenamiento, especialmente temperatura y tipo de contenedor/espacio cuando corresponda, modifican la velocidad de deterioro. Técnicas de conservación como secado, salado, ahumado u otras serán recetas/procesos de crafting que alteren el estado o perfil de conservación, no un framework paralelo.
- **Por qué:** convierte almacenamiento y preparación de alimentos en logística jugable y reutiliza temperatura/containers.
- **Trigger/dependencias:** cuando food/needs deje de ser sólo foundation y se amplíe a cocina/conservación productiva.
- **Límites:** sin microbiología, humedad interna detallada ni simulación química de alimentos. Empezar con pocos factores observables y data-driven.
- **Relación:** Needs, World Temperature, `IMPL-0025`, `IMPL-0029`, Containers, Crafting y Persistence.

## IMPL-0031 — Condición por componentes, filo y reparaciones degradables

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre ideas 33–35 y 40.
- **Qué queremos:** permitir que herramientas/armas que realmente lo necesiten expongan condición de componentes funcionales separados —ejemplo: mango y hoja/cabeza— y, cuando aporte gameplay, un valor de filo independiente. La inspección genérica del ítem debe permitir conocer su condición; el feedback diegético puede comunicar deterioro evidente durante el uso.
- **Reparación:** cada método/material/herramienta válida declara cuánto puede recuperar. Reparaciones sucesivas deben perder efectividad o reducir el máximo recuperable, de modo que reparar no restaure indefinidamente un objeto a estado de fábrica.
- **Por qué:** da significado al desgaste y a la elección de repuestos/herramientas sin convertir durabilidad en una única barra abstracta.
- **Trigger/dependencias:** primer arma/herramienta productiva cuya reparación necesite distinguir componentes o calidad del método.
- **Límites:** no crear un grafo universal de piezas para todos los objetos. Un ítem sólo declara componentes/filo que tengan consumers reales. Los valores de degradación por reparación no quedan balanceados todavía.
- **Relación:** Item Instances, durability, crafting/repair, generic Inspect action, Equipment y modding data.

## IMPL-0032 — Transporte físico de objetos sobredimensionados con ambas manos

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre idea 37.
- **Qué queremos:** ciertos world items grandes no pueden entrar en inventarios normales y sólo pueden transportarse físicamente usando ambas manos. Mientras se transportan ocupan las manos; para disparar/manipular con ellas el jugador debe soltar el objeto. Cuando el objeto admita desmontaje, sus componentes resultantes sí pueden volverse inventariables según sus propias reglas.
- **Por qué:** evita mochilas mágicas para baterías, cajas, piezas grandes u objetos del mundo y crea decisiones logísticas físicas.
- **Trigger/dependencias:** primer world item productivo que deba moverse pero no ser almacenado en grid inventory.
- **Límites:** sin sistema general de fuerza/agarre ni animación procedural compleja inicialmente; reutilizar world item identity, Carry/locomotion y hand/equipment ownership existentes cuando corresponda.
- **Relación:** Inventory/World Items, Equipment/hands, Carry Weight, Interaction, Persistence y `IMPL-0023`.

## IMPL-0033 — Packing y compactación de ítems

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre idea 38.
- **Qué queremos:** permitir acciones de `pack/unpack` para contenido que pueda agruparse o prepararse de forma compacta, reduciendo slots utilizados o permitiendo stacks más eficientes según reglas declaradas por datos.
- **Por qué:** hace de la organización física/logística una decisión sin aumentar artificialmente la capacidad global del inventario.
- **Trigger/dependencias:** primer contenido real que necesite distinguir estado suelto vs empaquetado.
- **Límites:** no convertir packing en nesting arbitrario de contenedores ni permitir compresión universal. Cada contenido declara explícitamente si puede empaquetarse y qué resultado produce.
- **Relación:** Inventory Grid/Stacks, Item Instances, Crafting/actions, Persistence y JSON/modding.

## IMPL-0034 — Estado persistente de cargadores y condición de armas de fuego

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre ideas 39–40.
- **Qué queremos:** cada cargador desmontable es una instancia real que conserva exactamente cuántas municiones contiene aunque salga del arma, entre al inventario o quede en el suelo. Gastar 3 de 6 rondas deja ese cargador concreto en `3/6`; recargar requiere cargadores/munición reales según el tipo de arma, no una reserva abstracta estilo arcade.
- **Condición:** armas que lo necesiten pueden distinguir al menos condición del cuerpo y del cañón u otra separación mínima justificada; la inspección genérica revela la condición disponible. El contrato exacto de chamber/internal magazine/cylinder se define por el primer arquetipo que lo requiera.
- **Por qué:** hace que la gestión de munición exista en objetos persistentes y crea continuidad entre combate, inventario, loot y persistence.
- **Trigger/dependencias:** después de M41/weapon baseline y al profundizar reload/ammo instances; coordinar con conservación de masa de munición cargada.
- **Límites:** esta entrada NO aprueba recarga de emergencia con cargador tirado, teclas separadas de `check magazine`, chamber simulation completa ni averías RNG. No duplicar `WeaponCombatService` como autoridad de reload.
- **Relación:** M40/M41 weapons, Inventory/Equipment, Item Instances, Persistence, ammo mass/Carry y `IMPL-0031`.

## IMPL-0035 — Aim contextual por estado, postura y apoyo del arma

- **Estado:** `DEFERRED AFTER M41/F8`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre ideas 48–50.
- **Qué queremos:** permitir que estados adversos reales del actor —por ejemplo baja stamina, miedo/agitación u otros consumers aprobados— modifiquen el aim. Las posturas básicas `standing/crouched/prone` también contribuyen al resultado. Debe existir una forma acotada de representar que el arma está físicamente apoyada sobre una superficie válida y que esa condición mejora estabilidad.
- **Por qué:** conecta condición del personaje y postura con precisión de forma legible en vez de añadir un skill bonus abstracto.
- **Trigger/dependencias:** sólo después de cerrar la medición/corrección F8 actual para no contaminar `ISSUE-0008`; implementar consumidores uno por uno cuando sus estados existan de verdad.
- **Límites:** no crear ahora CoverSystem, BipodSystem, RestingSystem y WeaponMountSystem separados. Para apoyo, buscar un contrato mínimo tipo `supported/not supported` sobre la autoridad de aim existente. No retunear accuracy actual antes de evidencia.
- **Relación:** `IMPL-0001`–`IMPL-0004`, Actor Condition/Needs, locomotion/postures, firearms y future fear/stamina consumers.

## IMPL-0036 — Muzzle flash como fuente de luz temporal

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre idea 53.
- **Qué queremos:** el disparo de un arma puede producir una fuente de iluminación extremadamente breve y coherente con el muzzle flash, visible especialmente en oscuridad y consumible por rendering/percepción sólo si corresponde.
- **Por qué:** mejora lectura audiovisual y hace que disparar de noche revele físicamente actividad sin un marcador abstracto.
- **Trigger/dependencias:** cuando los visuales productivos de armas/disparos y la iluminación nocturna lo justifiquen.
- **Límites:** evitar una luz dinámica cara por cada proyectil si un efecto visual más barato produce el mismo resultado; usar pooling/budget si hace falta. No introducir glare/adaptación ocular.
- **Relación:** Weapon visuals, Lighting, Perception y performance.

## IMPL-0037 — Lanzamiento genérico de world items

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre idea 56.
- **Qué queremos:** los objetos físicos compatibles pueden ser arrojados mediante una interacción común, conservando su identidad al salir de la mano/inventario y reaccionando con la física del mundo. El resultado depende de las propiedades/consumers del objeto: impacto, ruido, rotura u otros comportamientos sólo cuando existan.
- **Por qué:** una única capacidad física puede servir para combate improvisado, distracción y manipulación del entorno sin crear un `ThrowStone`, `ThrowBottle`, etc. por contenido.
- **Trigger/dependencias:** world item handling/equipment físico suficiente para transferir una instancia a una entidad lanzada de forma segura.
- **Límites:** no diseñar ahora trayectorias especiales por ítem, skills de lanzamiento, granadas ni un sistema paralelo de proyectiles. El lanzamiento genérico no implica que todo objeto haga daño útil.
- **Relación:** Inventory/World Items, physics, hands/equipment, item definitions y Persistence.

## IMPL-0038 — Movimiento de cadáveres ocupando ambas manos

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre ideas 57–58.
- **Qué queremos:** permitir mover cadáveres persistentes mediante interacción física; durante el traslado el actor usa ambas manos y debe soltar el cuerpo para volver a usar armas/manos normalmente.
- **Por qué:** aprovecha corpse continuity existente y permite despejar, ocultar o reorganizar cuerpos sin tratarlos como objetos de inventario.
- **Trigger/dependencias:** cuando corpse representation/interaction tenga un consumer jugable que justifique movimiento manual.
- **Límites:** una sola interacción base inicialmente; sin múltiples estilos de carry/drag, ragdoll simulation avanzada ni strength framework especulativo.
- **Relación:** M38 Actor Lifecycle/corpses, Interaction, hands/equipment, locomotion y Persistence.

## IMPL-0039 — Acceso forzado condicionado por herramientas

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre idea 60.
- **Qué queremos:** determinados objetos/entradas pueden ofrecer acciones de forzar/abrir únicamente cuando el actor posee una herramienta o método compatible definido por datos. Crowbar, herramientas de corte, destornilladores u otros métodos son consumers concretos; el interactuable no debe enumerar clases hardcodeadas por herramienta.
- **Por qué:** hace que herramientas y preparación importen en exploración sin resolver acceso mediante skill check abstracta universal.
- **Trigger/dependencias:** primer contenedor/puerta/interactable productivo que requiera acceso alternativo al método normal.
- **Límites:** no introducir daño separado de cerradura/bisagras/hoja por defecto, minijuegos universales ni cientos de acciones contextuales. Implementar la mínima compatibilidad herramienta↔acción necesaria.
- **Relación:** Interaction, Doors/Containers, Item capabilities/tags, `IMPL-0023`, Crafting/tools y modding.

## IMPL-0040 — Vidrio roto como peligro físico mitigable por protección

- **Estado:** `PLANNED`.
- **Fecha/origen:** 2026-09-10 — brainstorming de mecánicas; aprobado por Mauro sobre idea 62.
- **Qué queremos:** superficies/restos de vidrio roto pueden actuar como hazard físico cuando una parte corporal expuesta entra en contacto durante una interacción relevante; equipo protector apropiado, como guantes para las manos, puede evitar o reducir el problema.
- **Por qué:** conecta estado del entorno, protección equipada y Health localizado con una consecuencia comprensible.
- **Trigger/dependencias:** cuando ventanas/vidrio rompible y equipment protection tengan representación productiva suficiente.
- **Límites:** no simular cada fragmento como collider permanente ni crear microcortes aleatorios por proximidad. Usar una representación de hazard acotada y eventos de contacto/interacción claros.
- **Relación:** Localized Health, Armor/Equipment protection, world interaction, breakables y performance.

---

## Regla de mantenimiento

Cuando una entrada se convierta en trabajo inmediato, `Next_Sprints.md` debe referenciar su ID. Cuando se implemente y valide, puede pasar a `DONE` o eliminarse sólo si no aporta historial. Bugs reales van a `Issue_Registry.md`, no se duplican aquí como features.
