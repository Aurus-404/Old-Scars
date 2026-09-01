# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates sigue siendo [Project_Roadmap.md](Project_Roadmap.md); la cronología/evidencia permanece en [Development_Log.md](Development_Log.md). Los problemas persistentes y su estado viven en [Issue_Registry.md](Issue_Registry.md).

> Nota de reconciliación: `Project_Roadmap.md` todavía conserva wording anterior que presenta M41.4 como `AUTHORIZED — IMMEDIATE PRIORITY`. Eso está registrado como `ISSUE-0016` y no debe interpretarse como que M41.4 todavía no fue implementado. No renumerar ni reasignar IDs por este desfase documental.

## Estado Actual

### M41 — NPC Combat / AI Stabilization after Prueba 2

Estado operativo:

`IN PROGRESS — SANITATION / REVIEW`

Plan activo: [NPC_AI_Sanitation_Plan.md](NPC_AI_Sanitation_Plan.md).

Registro de problemas: [Issue_Registry.md](Issue_Registry.md).

M41.4 ya avanzó más allá del estado documental anterior. El baseline publicado incluye affiliation/threat acquisition, range-aware combat, imperfect aim, fixes de incapacidad/hostilidad/roaming intentado, consolidación health/condition/vital damage, balance .303 y observabilidad NPC/combate.

Commits relevantes del baseline reciente:

- `b42e17c40ad843244fd390c9b0eeb707b6462d31` — estabilización P0 de AI/incapacitación y matriz Blue/Red;
- `feab22115f384d908397b2836ffe4316075cd552` — estabilización de condition/recovery;
- `a0769e14bc71946dc35fbef5085a191785d27c35` — integración de Vital Damage;
- `8d567b4ce6779e8bff8495c497b9a1cbfc3aec35` — calibración .303;
- `eae5f14bed6aae82840762faf6561bf0b0e1625d` — observabilidad M41 de NPC/combate.

La Prueba 2 manual confirmó que el bloque todavía no está listo para cerrarse. Los problemas activos incluyen, entre otros:

- Blue/Red no muestran roaming ambiental efectivo mientras están Idle aunque White sí;
- el diagnóstico de roaming puede demostrar órdenes aceptadas sin demostrar desplazamiento real;
- existe una sospecha de ownership/competencia entre Ambient/Encounter/Navigation que requiere auditoría antes de seguir parchando;
- la percepción depende demasiado del facing corporal de spawn y falta una autoridad de gaze/attention;
- tracking visual lateral deficiente;
- LostContact todavía no realiza Search V1;
- posible sesgo de impactos hacia piernas/pies aún no confirmado;
- la cápsula actual es insuficiente para validar regiones anatómicas de combate;
- faltan herramientas debug Invisible-to-AI / Invincible y mejor observabilidad multi-NPC.

La lista completa, severidad, evidencia y estado están en `Issue_Registry.md`.

## Objetivo Del Bloque Activo

No agregar nuevas features grandes. Primero sanear y simplificar la foundation humana hasta poder demostrar:

`Ambient → Perception/Gaze → Recognition → Encounter → Tracking/Combat → LostContact/Search → Reacquire o Release → Ambient`

con ownership inequívoco de navegación, daño localizado coherente, incapacidad/muerte estables y tooling suficiente para probarlo.

No existe obligación de conservar la capa de decisión actual si la auditoría demuestra sobreingeniería. Sí deben preservarse/reutilizarse las autoridades inferiores válidas de Perception, Navigation, Combat, Health/Medical/Condition, Equipment, Affiliation y Persistence en vez de crear stacks paralelos.

## Fase Actual

### Fase 0 — Registro, baseline y documentación

Se está fijando una fuente persistente de issues y el plan secuencial de saneamiento para evitar perder problemas entre días/pruebas/prompts.

Al completar Fase 0, el próximo trabajo de código es:

### Fase 1 — Auditoría destructiva de la IA actual

Antes de refactorizar, inspeccionar writers/owners reales de Navigation, AI state, threat assignment/reset, roaming, LastKnownPosition, facing/rotation y combat target. El gate es decidir con evidencia si la capa actual se simplifica mediante refactor acotado o si conviene reemplazar la capa de decisión conservando las autoridades inferiores.

## Cierres Relevantes Previos

### M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1

`DONE — NPC SANDBOX SPAWN & RANDOMIZED LOADOUTS V1 VALIDATED`

Commit funcional: `a90dc4e1a38bef69e3762e398a378a666a9f993e`.

### M41.2 — Basic Equipment & Weapon Coverage V1

`DONE — BASIC EQUIPMENT & WEAPON COVERAGE V1 VALIDATED`

Commit funcional: `4f877da10dee813b0bed816194110b5a27087683`.

### Deformable Volumetric Terrain Foundation / Technical Spike

`VALIDATED — TECHNICAL SPIKE COMPLETE`

Commit técnico: `d0309cf053be220a22151cae2dae9aca6f988e6f`.

Integración publicada en `dev`: `1b41ead829cd566c55df5adfc0522e33e1dffb96`.

Autoridad de evidencia: [Deformable_Terrain_Foundation.md](Deformable_Terrain_Foundation.md).

## Contratos Que No Deben Duplicarse

- M37/M37.1: persistence/Current Slice.
- M38: actor identity/lifecycle/spawn foundation.
- M39: localized health/wounds.
- M40/M40.1: combat, firearms/ammo/reload, armor y penetration.
- M41.0: Navigation/Perception foundation, salvo refactor explícito basado en auditoría.
- `ActorEquipmentComponent`, item-owned storage, ownership e `ItemInstance`: equipment/state.
- JSON declara contenido; C# ejecuta comportamiento genérico.

## No Iniciar Todavía

Durante el saneamiento no iniciar por inercia:

- Behavior Trees/GOAP/Utility AI generales;
- squads, cover tactics sofisticadas o strategic/off-sector AI;
- hearing/noise y schedules/jobs;
- full ballistics/bullet drop/travel time/wind;
- facciones/reputación productivas completas;
- final NPC population/ecology;
- production UI;
- world streaming/LOD/navigation productivos;
- minería/geología/fluid simulation;
- condition/repair/crafting;
- producción masiva de contenido.

## Próximo Paso Exacto

Completar Fase 0 documental. Después ejecutar **Fase 1 — Auditoría destructiva de la IA actual** sin implementar todavía un nuevo stack de IA. La auditoría debe terminar con un mapa claro de ownership/writers y una decisión explícita: `REFactor salvable` o `reemplazo/simplificación de la capa de decisión`.
