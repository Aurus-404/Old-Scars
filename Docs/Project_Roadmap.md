# Old Scars — Project Roadmap

## Autoridad del documento

Este archivo es la autoridad canónica para:

- IDs reservados y aliases históricos;
- estado actual de milestones grandes;
- dependencias y orden general de ejecución;
- horizontes de producción;
- nombres y ubicación de gates.

No es una lista de bugs ni de implementaciones pequeñas:

- `Current_Milestone.md` = snapshot operativo actual;
- `Next_Sprints.md` = cola inmediata;
- `Issue_Registry.md` = bugs/deudas/sospechas;
- `Implementation_Backlog.md` = mecánicas/mejoras menores aprobadas para después;
- `Prueba_3_Findings.md` = evidencia manual integrada reciente;
- `Development_Log.md` = cronología y evidencia histórica detallada.

Mauro conserva autoridad creativa y de producto. Los IDs históricos no se renombran retrospectivamente y una coding unit/intervención dentro de un milestone no consume automáticamente un nuevo ID.

## Estado de producción — 2026-09-03

| Campo | Estado canónico |
| --- | --- |
| Milestone grande cerrado más reciente | M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1 |
| Milestone grande activo | M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1 |
| Estado M41.4 | `IN PROGRESS — IMPLEMENTED BASELINE; POST-PLAYTEST SANITATION / PRUEBA 3 CORRECTIONS ACTIVE` |
| Persistence Ready | `APPROVED` |
| Combat Ready | `APPROVED` |
| AI Ready | `APPROVED` |
| Open World Rebaseline | `APPROVED DESIGN DIRECTION — PARTIALLY IMPLEMENTED FOUNDATIONS` |
| Próximo trabajo | Prueba 3 Correction Pass → Prueba 3.3 → F8A Aim Bias Evidence |
| Después | F8B/C condicional → F8D/E → Player Debug/Observability closeout → QA integrado → decisión explícita antes de otro sistema grande |

### Qué significa el estado actual de M41.4

M41.4 **no está esperando ser implementado desde cero**. El baseline de affiliation, automatic threat acquisition, engagement por rango, combate físico e imperfect aim ya existe y fue probado. La Prueba 2 abrió un saneamiento post-implementación que produjo:

- F2 — Behavior ownership + Ambient roaming;
- F3 — Gaze/Attention V1;
- F4 — tracking visual bounded;
- F5 — production Perception centrada en Current Gaze;
- F6 — LostContact/Search V1;
- F7 — representación humana + hitboxes anatómicos explícitos.

Prueba 3/3.1/3.2 confirmó gran parte de esas correcciones en ejecución real y detectó nuevos problemas de tooling/KO que deben resolverse antes de medir aim con rigor. El detalle vive en `NPC_AI_Sanitation_Plan.md` y `Prueba_3_Findings.md`.

El gate `AI Ready` aprobado en M41.1 **no se reabre**: estas correcciones pertenecen a integración/game feel/QA del bloque M41.4 y no implican que Navigation/Perception foundation haya dejado de estar validada.

---

## Camino crítico inmediato

```text
M41.4 baseline implementado
    ↓
Prueba 2 sanitation F2–F7 completada
    ↓
Prueba 3 manual integrada
    ↓
Correction Pass:
  1. Player Invisible-to-AI mínimo
  2. F6 current-vs-last + multi-NPC mínimo
  3. KO / combat-memory continuity
  4. minimum real-time KO dwell
    ↓
Prueba 3.3 — 1 Blue vs 1 Red limpio
    ↓
F8A — Aim Bias Evidence
    ↓
F8B — Primary Aim Point genérico, sólo si evidencia
    ↓
F8C — before/after controlado
    ↓
F8D — review pequeño de accuracy
    ↓
F8E — cleanup legacy capsule/anatomy
    ↓
F9/F10 — completar Player Debug + Observability V2
    ↓
F11–F15 — QA integrado / game feel / cleanup
    ↓
Decisión explícita antes de otro sistema grande
```

Las fases/subfases anteriores son **coding units dentro del cierre M41.4/NPC Foundation**, no nuevos milestones reservados.

## Reglas de alcance para M41.4

Conservar salvo evidencia real:

- `ActorBehaviorController`;
- `ActorNavigationController`;
- `ActorGazeController`;
- `ActorVisualPerceptionService`;
- Recognition/Threat Acquisition;
- Search V1;
- `WeaponCombatService`;
- `PhysicalShotPathResolver`;
- health/medical/condition/vital;
- `ActorCombatHitRegion`;
- `ActorLocomotionCollider` técnico;
- Focus y error angular físico.

No introducir por inercia:

- Behavior Trees / GOAP / Utility AI generales;
- blackboard/planner general;
- memory framework general;
- weak-point/aim-point scoring;
- head targeting;
- full ballistics/drop/drag/wind;
- Accuracy/FireControl/WeaponHandling controllers por limpieza preventiva;
- cover/squad/strategic AI sofisticada;
- hearing/noise/schedules/jobs;
- machine/vehicle damage framework sin consumidor real;
- attack-method framework para animales/mutantes antes del primer consumidor real.

## Decisiones activas derivadas de investigación y Prueba 3

### Targeting

Dirección objetivo:

`Target → Primary Aim Point → shooter focus/context error → weapon → PhysicalShotPathResolver → actual hit → receiver`.

El shooter no debe conocer `Torso` ni anatomía/especie concreta para encontrar un center-mass razonable. `ISSUE-0008` sigue `SUSPECTED` hasta F8A/8C; Prueba 3 no reprodujo cualitativamente el patrón extremo de sólo piernas, pero la muestra no alcanza para cerrarlo.

### KO / memoria mínima de combate

- KO/Unconscious no es Dead.
- El noqueado deja de ser amenaza activa y el rival deja de atacarlo deliberadamente.
- La incapacidad temporal no debe borrar automáticamente quién era el enemigo reciente.
- Recovery puede reanudar contexto hostil, pero sin wallhack: Perception/LKP/Search siguen siendo autoridad espacial.
- Debe existir minimum real-time KO dwell antes de que physiology pueda permitir recovery.
- No crear un sistema general de memoria para esta V1.

### QA/observabilidad

- Player Invisible-to-AI se adelanta porque Prueba 3 demostró contaminación de NPC-only tests.
- F6 necesita current-vs-last correcto y visuals multi-NPC antes de usarlo para conclusiones finas de aim/combat.
- F9/F10 permanecen en el plan; sólo se adelantan slices mínimos necesarios para test fiable.

---

## Milestones y foundations cerrados relevantes

| Unidad | Estado | Evidencia / nota |
| --- | --- | --- |
| M36.0 — Strategic Production Roadmap Rebaseline | `DONE — DOCUMENTATION REVIEWED` | Roadmap/gates/workflow reconciliados. |
| M36.1 — Foundation Freeze & Persistent Identity Contract | `DONE — FOUNDATION FREEZE APPROVED` | Identidad durable/ownership/stack/rollback congelados. |
| M37.0 — Save Format & Persistence Core | `DONE — PERSISTENCE CORE VALIDATED` | Envelope/persistence core validado. |
| M37.1 — Current Slice Persistent Round-Trip | `DONE — CURRENT SLICE PERSISTENCE VALIDATED` | Fresh-session round-trip validado; `Persistence Ready`. |
| M38.0 — Actor Runtime & Lifecycle V1 | `DONE — ACTOR RUNTIME & LIFECYCLE VALIDATED` | Actor identity/lifecycle/corpse continuity. |
| M38.1 — Needs, World Clock & Recovery V1 | `DONE — WORLD TIME / NEEDS / RECOVERY VALIDATED` | WorldClock/needs/recovery/persistence. |
| M39.0 — Localized Health & Medicine V1 | `DONE — LOCALIZED HEALTH / MEDICINE VALIDATED` | Seis regiones, wounds, bleeding, pain, treatment. |
| M40.0 — Combat Resolution & Weapons V1 | `DONE — COMBAT RESOLUTION & WEAPONS V1 VALIDATED` | Ruta única melee/firearm a M39. |
| M40.1 — Armor & Penetration V1 | `DONE — ARMOR / PENETRATION V1 VALIDATED` | `Combat Ready APPROVED`. |
| M41.0 — Navigation & Perception Foundation | `DONE — NAVIGATION / PERCEPTION FOUNDATION VALIDATED` | Navigation/Perception separadas y validadas. |
| M41.1 — Human Encounter AI V1 | `DONE — HUMAN ENCOUNTER AI V1 VALIDATED` | Avoid/Flee/Fight/LostContact baseline; `AI Ready APPROVED`. |
| M41.2 — Basic Equipment & Weapon Coverage V1 | `DONE — VALIDATED` | Commit `4f877da10dee813b0bed816194110b5a27087683`. |
| M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1 | `DONE — VALIDATED` | Commit `a90dc4e1a38bef69e3762e398a378a666a9f993e`. |
| M41.4 — Affiliation, Range-Aware Combat & Imperfect Aim V1 | `IN PROGRESS — POST-PLAYTEST SANITATION` | Baseline implementado; Prueba 2→F2–F7; Prueba 3 correction pass activo. |

## Foundations open-world ya validadas

Estas unidades siguen cerradas y no se reabren por el trabajo NPC:

| Unidad | Estado |
| --- | --- |
| ID TBD — Global Content ID Namespace Foundation | `VALIDATED — FOUNDATION COMPLETE` |
| ID TBD — Minimum Content Source Identity & Provenance Foundation | `VALIDATED — FOUNDATION COMPLETE` |
| ID TBD — World Identity, Topology & Determinism Foundation | `VALIDATED — FOUNDATION COMPLETE` |
| ID TBD — World Session + Persistence V1 / New Game Save-Load Application Shell | `VALIDATED — APPLICATION SHELL COMPLETE` |
| ID TBD — Macro World Plan V1 | `VALIDATED — FOUNDATION COMPLETE` |
| ID TBD — Macro Elevation / Landforms V1 | `VALIDATED — FOUNDATION COMPLETE` |
| ID TBD — Worldgen Gameplay Quality + Macro Water V1 | `VALIDATED — FOUNDATION COMPLETE` |
| ID TBD — Worldgen Pass Isolation Correction | `VALIDATED — SYSTEMIC CORRECTION COMPLETE` |
| ID TBD — Worldgen / World Session Observability Correction | `VALIDATED — OBSERVABILITY CORRECTION COMPLETE` |
| ID TBD — Macro Human Geography / Road Network V1 | `VALIDATED — FOUNDATION COMPLETE` |
| ID TBD — Terrain Materialization Technical Spike | `VALIDATED — TECHNICAL SPIKE COMPLETE` |
| ID TBD — Integrated Gameplay Runtime / SampleScene Convergence | `VALIDATED — RUNTIME CONVERGENCE COMPLETE` |
| ID TBD — Macro Climate Baseline V1 | `VALIDATED — FOUNDATION COMPLETE` |
| ID TBD — Player Traversal / Camera & Runtime Debug Ergonomics Pass | `VALIDATED — RUNTIME ERGONOMICS COMPLETE` |
| ID TBD — Macro Environment / Biome Regions V1 | `VALIDATED — FOUNDATION COMPLETE` |
| ID TBD — Deformable Volumetric Terrain Foundation / Technical Spike | `VALIDATED — TECHNICAL SPIKE COMPLETE` |

Referencia de commits destacada:

- runtime convergence `8c485c78b4ab294de9d983f70ebadfba634ab3e1`;
- Macro Climate `457836e7f10a9b2ddbc08cc1db05ca38cd3f7108`;
- Player Traversal/Camera `ab78da4fbb1af9189d6a5c178515fafdb56f368e`;
- Macro Environment `55bcb0db479af43351f28908dfe05125dd9d62e1`;
- Deformable Volumetric Terrain spike `d0309cf053be220a22151cae2dae9aca6f988e6f`, integrado en `dev` por `1b41ead829cd566c55df5adfc0522e33e1dffb96`.

El backend runtime vigente del spike volumétrico es la implementación publicada actual; cualquier documentación histórica de mesher debe contrastarse con código antes de usarla como autoridad técnica.

---

## Open World Rebaseline — dirección futura aprobada

La dirección open-world permanece aprobada, pero no está autorizada para continuar automáticamente antes de cerrar el bloque NPC actual.

| Orden | Unidad | Estado | Dependencia / propósito |
| --- | --- | --- | --- |
| 1 | Minimum Content Source Identity & Provenance | `VALIDATED` | Fuente/version/input determinista. |
| 2 | World Identity, Topology & Determinism | `VALIDATED` | IDs/seed/topology. |
| Bridge | World Session + Persistence V1 | `VALIDATED` | lifecycle New Game/Load. |
| 3 | Macro World Plan V1 | `VALIDATED` | bounds/settings/placements. |
| 4 | Macro Elevation / Landforms V1 | `VALIDATED` | truth global de elevation/landforms. |
| 5 | Worldgen Gameplay Quality + Macro Water V1 | `VALIDATED` | water/coastline/quality/starter. |
| Correction | Pass Isolation | `VALIDATED` | versionado determinista por pass. |
| Correction | Worldgen/Session Observability | `VALIDATED` | lifecycle/evidence. |
| Infrastructure | Macro Human Geography / Road Network V1 | `VALIDATED` | hubs/roads macro. |
| Spike | Terrain Materialization Technical Spike | `VALIDATED` | benchmark local de materialización/NavMesh. |
| Integration | Gameplay Runtime / SampleScene Convergence | `VALIDATED` | runtime gameplay canónico. |
| 6 | Macro Climate Baseline V1 | `VALIDATED` | thermal/moisture truth. |
| 6b | Macro Environment / Biome Regions V1 | `VALIDATED` | biome/environment truth. |
| Spike volumétrico | Deformable Volumetric Terrain Foundation | `VALIDATED` | terrain 3D deformable technical evidence. |
| Tramo jugable | M41.2 | `DONE` | equipment/firearms coverage. |
| Tramo jugable | M41.3 | `DONE` | NPC sandbox/loadouts. |
| Tramo jugable activo | M41.4 | `IN PROGRESS — POST-PLAYTEST SANITATION` | combat/AI integration/game feel. |
| Review activo | Prueba 3 / NPC Foundation review | `IN PROGRESS — CORRECTIONS REQUIRED` | cerrar defectos encontrados antes de otro sistema grande. |
| 7 | ID TBD — Bounded History & Present-Day Resolution | `PLANNED — NOT AUTHORIZED` | historia estructurada acotada. |
| 8 | ID TBD — World Persistence | `PLANNED — NOT AUTHORIZED` | persistencia world state general. |
| 9 | ID TBD — Sector Blueprint & Authored Composition | `PLANNED — NOT AUTHORIZED` | blueprint local/materialización autorada. |
| 10 | ID TBD — Large-Sector Navigation & Performance Gate | `PLANNED — NOT AUTHORIZED` | performance/nav sobre terrain real. |
| 11 | ID TBD — Sector Materialization & Transition | `PLANNED — NOT AUTHORIZED` | sector activo + transición. |
| 12 | ID TBD — Connected First Playable | `PLANNED — NOT AUTHORIZED` | A→B→A con mutations/save/fresh load. |
| 13 | ID TBD — Open World Playtest & Roadmap Rebaseline | `PLANNED — NOT AUTHORIZED` | evidencia para nueva priorización. |

Nada de esta tabla autoriza continuar por inercia. La representación/algoritmo productivo final de terrain, LOD, geología, mining loop, fluid dynamics y budgets siguen sin congelarse por el simple hecho de existir un spike validado.

---

## Milestones reservados posteriores

Los IDs siguientes permanecen reservados. Su presencia no autoriza implementación ni fija fecha.

| Milestone | Estado | Alcance resumido / condición |
| --- | --- | --- |
| M42.0 — Weather, Exposure & Environment V1 | `PLANNED — SEQUENCE REBASELINE REQUIRED` | Weather runtime/exposure; no equivale a Macro Climate. |
| M42.1 — Food, Water, Animals & Ecology V1 | `PLANNED — SEQUENCE REBASELINE REQUIRED` | Food/water/ecology/animals acotados. |
| M43.0 — Condition, Repair & Disassembly V1 | `PLANNED — SEQUENCE REBASELINE REQUIRED` | Condition mutable/repair/disassembly. |
| M43.1 — Bounded Crafting & Workstations V1 | `PLANNED — SEQUENCE REBASELINE REQUIRED` | Crafting acotado y estaciones. |
| M44.0 — Skills & Long-Term Progression V1 | `PLANNED — SEQUENCE REBASELINE REQUIRED` | Competencias/progresión sin grind. |
| M44.1 — Shelter & Recovery Progression V1 | `PLANNED — SEQUENCE REBASELINE REQUIRED` | Refugio y recovery. |
| M45.0 — Content Tools & World Sectorization | `PLANNED — SCOPE REBASELINE REQUIRED` | Tooling/sectorization histórica a reconciliar con path ID TBD. |
| M45.1 — Vertical Slice Candidate: La estación de bombeo | `PLANNED — CANDIDATE, NOT NARRATIVE CANON` | Posterior al Connected First Playable. |
| M46.0 — Settlements, Trade & Patrimonial Value | `PLANNED — SEQUENCE REBASELINE REQUIRED` | asentamientos/economía material. |
| M46.1 — Faction Identity, Disposition & Memory V1 | `PLANNED — SEQUENCE REBASELINE REQUIRED` | facciones/disposition/memoria amplia; no confundir con memoria mínima temporal del KO M41.4. |
| M47.0 — Controlled Secondary World Variation V1 | `PLANNED — SCOPE REBASELINE REQUIRED` | variación secundaria. |
| M47.1 — Narrative, Events & Objectives V1 | `PLANNED — SCOPE REBASELINE REQUIRED` | eventos/objetivos autorales. |
| M48.0 — Production UI/UX & Accessibility | `PLANNED` | UI de producción. |
| M48.1 — Art, Animation & Audio Production Pipeline | `PLANNED` | pipeline de assets/audio/animation. |
| M49.0 — Content Production & Optimization | `PLANNED` | contenido a escala/performance. |
| M50.0 — Modding & Data Compatibility V1 | `PLANNED` | manifests/dependencies/overrides/compatibilidad productiva. |
| M51.0 — Alpha | `PLANNED` | feature complete; gate Alpha. |
| M52.0 — Content Complete | `PLANNED` | contenido de lanzamiento integrado. |
| M53.0 — Beta | `PLANNED` | feature/content lock, estabilidad/balance. |
| M54.0 — Release Candidate | `PLANNED` | build publicable/recuperable. |
| M55.0 — Launch | `PLANNED` | lanzamiento/soporte/rollback operativo. |

## Gates canónicos

| Gate | Cierre previsto | Estado actual |
| --- | --- | --- |
| Foundation Freeze | M36.1 | `APPROVED` |
| Persistence Ready | M37.1 | `APPROVED` |
| Combat Ready | M40.1 | `APPROVED` |
| AI Ready | M41.1 | `APPROVED` |
| World Systems Ready | M42.1 | `PLANNED / REBASELINE REQUIRED` |
| Survival Systems Ready | M44.1 | `PLANNED / REBASELINE REQUIRED` |
| Content Pipeline Ready | M45.0 | `PLANNED / REBASELINE REQUIRED` |
| Vertical Slice Approved | M45.1 | `PLANNED / REBASELINE REQUIRED` |
| Production Ready | M50.0 | `PLANNED` |
| Alpha | M51.0 | `PLANNED` |
| Content Complete | M52.0 | `PLANNED` |
| Beta | M53.0 | `PLANNED` |
| Release Candidate | M54.0 | `PLANNED` |

Los gates aprobados no se reabren por bugs de una capa posterior. Las ubicaciones M42.1+ permanecen reservas históricas y requieren rebaseline antes de autorización.

---

## IDs y aliases históricos reservados

No reutilizar:

- `CoreDataSystem`, `ActionAvailabilitySystem`;
- M6–M18 y subIDs históricos documentados;
- M19.1, M19.2, M20, M21, M21.0.1, M22, M22.1, M22.1.1, M22.1.2;
- M23, M23.0.1, M23.0.2, M23.0.3, M23.1, M23.1.1, M23.1.2;
- M24, M24.1, M24.2, M24.3, M24.4, M25, M26, M26.0.1, M27;
- M28, M29, M30, M30.3, M30.4, M31.0, M32, M32.2, M32.3, M32.4, M32.4.1;
- M33.x, M34.x, M35.x;
- M36–M55 según este roadmap.

Aliases/colisiones importantes:

| Referencia histórica | Disposición canónica |
| --- | --- |
| M28 historical ground item drop/pickup | M28 queda reservado; antiguo `Container State / Naming Cleanup v0` pasa a `ID TBD` diferido. |
| M32.3 — House Container Variants | alias histórico dentro del bloque M32; no segundo milestone funcional. |
| M35.2.3 — Inventory Window Redesign Phase C1 | alias funcional de `M35.2.3 — Unified Corpse Belongings Surface`. |
| M35.2.3.1 | `Universal Corpse Item Actions`, `DEFERRED — RECLASSIFIED`. |

Estados históricos inciertos no se elevan a `VALIDATED` por inferencia. Para evidencia exacta de commits/validaciones anteriores, consultar `Development_Log.md` y Git.

## Reglas de numeración

1. Commits/tags/IDs históricos no se renombran retrospectivamente.
2. Todo ID nuevo de milestone grande se reserva primero aquí.
3. Una colisión se registra con nombre canónico, alias y disposición.
4. Trabajo sin ID libre usa `ID TBD`; no reutiliza un número histórico.
5. Coding units internas de un milestone no necesitan nuevo ID si no representan un hito grande independiente.
6. `Implementation_Backlog.md` no reserva milestone IDs.

## Dependencias transversales

- persistence antes de escalar actores/NPCs/world state;
- actor lifecycle antes de health/needs/AI;
- WorldClock antes de weather runtime/deterioro/recovery temporal;
- condition antes de repair/disassembly/crafting;
- content provenance/generation compatibility antes de comprometer compatibilidad de mundos persistentes;
- macro truth/cross-sector networks antes de sector blueprints/materialization;
- terrain deformable real debe participar de futuras pruebas de navegación/performance;
- tools/validators antes de contenido masivo;
- economy material antes de trade;
- toda Definition global nueva usa `ContentId` canónico;
- M41.4 reutiliza M41.1/M40/M41.0 y no crea un faction/AI stack paralelo.

## Trabajo congelado o diferido durante cierre M41

No iniciar por inercia:

- Bounded History / World Persistence general / Sector Blueprint / Sector Materialization;
- facciones amplias M46.1;
- UI final;
- producción masiva de contenido;
- mining/geología/fluidos/derrumbes/whole-world voxels derivados automáticamente del terrain spike;
- enfermedades generales/agricultura/vehículos sin rebaseline aprobado;
- JSON como scripting libre;
- sistemas universales preventivos sin necesidad actual demostrada.

## Regla transversal de integración

Profundidad mediante sistemas conectados sigue siendo criterio de aceptación: un sistema nuevo debe consumir estado real relevante, modificar una decisión jugable y producir feedback/resultado explicable. Una simulación aislada o una segunda autoridad paralela no cumple esta regla.

## Regla de evidencia

`VALIDATED` nunca significa sólo “compila”. El detalle histórico de cada validación permanece en `Development_Log.md`; el estado operativo inmediato se lee en `Current_Milestone.md` y `Next_Sprints.md`; bugs en `Issue_Registry.md`; implementaciones menores futuras en `Implementation_Backlog.md`.
