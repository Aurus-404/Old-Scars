# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los problemas persistentes viven en [Issue_Registry.md](Issue_Registry.md); la secuencia completa de saneamiento NPC/AI está en [NPC_AI_Sanitation_Plan.md](NPC_AI_Sanitation_Plan.md).

> `Project_Roadmap.md` todavía conserva wording anterior que presenta M41.4 como trabajo por iniciar. El desfase está registrado como `ISSUE-0016`. No renumerar ni reasignar IDs por esta reconciliación operativa.

## Próximo Trabajo

### 1. Completar Fase 0 — Registry / Baseline / Docs

Estado: `IN PROGRESS`.

Objetivo:

- conservar un registro persistente de bugs/deudas/sospechas/resoluciones;
- fijar la secuencia de saneamiento para no perder problemas entre pruebas o sesiones;
- dejar claro que el bloque M41/NPC sigue abierto por estabilización posterior a Prueba 2, no porque M41.4 aún no exista.

Gate: `Issue_Registry.md` y `NPC_AI_Sanitation_Plan.md` publicados, snapshots operativos reconciliados y próximo trabajo técnico inequívoco.

### 2. Fase 1 — Auditoría destructiva de la IA actual

No implementar un refactor grande de entrada.

Auditar en el repo real:

- todos los writers/owners de `ActorNavigationController`;
- estados y transiciones de AI;
- threat assignment / clear / reset;
- roaming/ambient behavior;
- LastKnownPosition;
- body facing / rotation / `transform.forward`;
- combat target;
- llamadas a `Stop()` y órdenes de navegación.

Revisar especialmente `SandboxActorRoamingController`, `HumanEncounterAIController`, `ActorThreatAcquisitionController`, `ActorNavigationController`, `SandboxNpcController` y cualquier seam adicional encontrado en código.

Entregable obligatorio:

`componente → qué decide → qué escribe → cuándo se activa → cuándo se desactiva → quién puede interrumpirlo`.

Gate: poder responder inequívocamente quién posee navegación en Ambient/Encounter/Search/Inactive y decidir entre:

- **A — Salvable:** simplificación/refactor acotado de la capa actual; o
- **B — Reemplazo:** retirar/simplificar la capa de decisión problemática reutilizando autoridades inferiores válidas.

No aceptar una solución basada en una proliferación de flags/patches de ownership.

### 3. Fase 2 — Behavior ownership + Ambient roaming real

Sólo después de Fase 1.

Objetivo:

- White/Blue/Red comparten Ambient roaming cuando están realmente Idle;
- threat/encounter interrumpe Ambient de forma limpia;
- al terminar encounter se libera ownership y vuelve Ambient;
- incapacitado/muerto no navega ni combate;
- el diagnostic prueba desplazamiento real, no sólo accepted orders.

Gate: White/Blue/Red recorren distancia verificable sin amenaza y no existe competencia de navegación.

### 4. Fases 3–6 — Gaze / Tracking / Perception / Search

Ejecutar secuencialmente, revisando cada commit antes de continuar:

- Fase 3: orientación inicial + Gaze/Attention V1;
- Fase 4: tracking visual continuo con predicción corta limitada;
- Fase 5: FOV/LOS integrado con gaze real;
- Fase 6: LostContact/Search V1 basado en LastKnownPosition.

No usar visión 360°, conocimiento mágico ni sistemas complejos de cover/squad/hearing para resolver estos problemas.

### 5. Fases 7–10 — Geometría corporal + tooling de prueba

- Human debug actor estático/bind pose/T-pose;
- hitboxes anatómicos explícitos;
- investigación determinista/estadística del posible sesgo hacia piernas/pies;
- Player Invisible-to-AI / Invincible debug;
- observabilidad multi-NPC V2.

### 6. Fases 11–15 — QA integrado, Prueba 3 y cierre

- batería automatizada pequeña pero fuerte;
- Prueba 3 automatizada multi-NPC;
- Prueba 3B con Player;
- prueba manual final de game feel;
- limpieza y reconciliación documental.

El bloque NPC Foundation V1 sólo se cierra cuando el flujo completo `Ambient → Perception/Gaze → Recognition → Encounter → Tracking/Combat → LostContact/Search → Reacquire/Release → Ambient` funciona de forma observable y sin ownership contradictorio.

## Issues Que Bloquean El Cierre Actual

Consultar [Issue_Registry.md](Issue_Registry.md). Los bloqueantes principales al iniciar el saneamiento son:

- `ISSUE-0001` Blue/Red sin roaming efectivo Idle — `CONFIRMED / P0`;
- `ISSUE-0002` gate de roaming valida proxy y no desplazamiento — `CONFIRMED / P0`;
- `ISSUE-0003` posible competencia Ambient/Encounter/Navigation — `SUSPECTED / P0`;
- `ISSUE-0004` percepción excesivamente dependiente del facing de spawn — `CONFIRMED / P1`;
- `ISSUE-0005` falta Gaze/Attention — `CONFIRMED / P1`;
- `ISSUE-0006` tracking lateral deficiente — `CONFIRMED / P1`;
- `ISSUE-0007` LostContact sin Search V1 — `CONFIRMED / P1`;
- `ISSUE-0008` posible sesgo de impactos hacia piernas/pies — `SUSPECTED / P1`;
- `ISSUE-0009` cápsula insuficiente para hit testing anatómico — `CONFIRMED / P1`.

Los tooling issues `ISSUE-0010`–`ISSUE-0013` se resuelven dentro de las fases correspondientes, no antes por scope creep.

## Estado Cerrado Relevante

- `M41.3 — NPC Sandbox Spawn & Randomized Loadouts V1` — `DONE — VALIDATED`, commit `a90dc4e1a38bef69e3762e398a378a666a9f993e`;
- `M41.2 — Basic Equipment & Weapon Coverage V1` — `DONE — VALIDATED`, commit `4f877da10dee813b0bed816194110b5a27087683`;
- AI P0 incapacity/affiliation correction — commit `b42e17c40ad843244fd390c9b0eeb707b6462d31`;
- Condition stabilization — commit `feab22115f384d908397b2836ffe4316075cd552`;
- Vital Damage integration — commit `a0769e14bc71946dc35fbef5085a191785d27c35`;
- .303 combat pressure calibration — commit `8d567b4ce6779e8bff8495c497b9a1cbfc3aec35`;
- M41 NPC combat observability — commit `eae5f14bed6aae82840762faf6561bf0b0e1625d`;
- `Deformable Volumetric Terrain Foundation / Technical Spike` — `VALIDATED — TECHNICAL SPIKE COMPLETE`, commit técnico `d0309cf053be220a22151cae2dae9aca6f988e6f`, integrado por `1b41ead829cd566c55df5adfc0522e33e1dffb96`.

## No Iniciar Todavía

Mientras el saneamiento NPC/AI esté abierto, no iniciar por inercia:

- Behavior Trees/GOAP/Utility AI generales;
- squads/tactics/cover AI sofisticada;
- hearing/noise, schedules/jobs o strategic/off-sector AI;
- full ballistics/bullet drop/travel time/wind;
- facciones/reputación productivas completas;
- final NPC population/ecology;
- production UI;
- Bounded History / World Persistence general / Sector Blueprint;
- whole-world voxels, streaming/sector transition o LOD productivo;
- weather/seasons/final rivers;
- mining/geology/fluid simulation;
- condition/repair/crafting;
- producción masiva de contenido.

El próximo paso técnico real, después de cerrar Fase 0, es **Fase 1 — Auditoría destructiva de la IA actual**.
