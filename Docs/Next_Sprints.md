# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximos Tres Trabajos

### 1. M38.1 — Manual Unity Validation & Closeout

Estado: `IMPLEMENTED — AUTOMATED WORLD TIME / NEEDS / RECOVERY VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Objetivo:

- verificar visualmente `Day / HH:MM`, progresión normal de Hunger/Thirst y consumibles;
- ejecutar `Rest 1h` / `Sleep 8h` y confirmar que clock y needs usan exactamente el mismo delta;
- guardar, salir completamente de Play Mode, entrar en fresh Play, cargar y comprobar clock/needs exactos sin errores en Console.

Fuera de alcance:

- AI, combat, world-scale spawning y streaming;
- playable exploration prototype;
- fatigue deferred SHOULD, health/medicine M39, beds system, UI final y cualquier expansión del scope implementado.

### 2. M39.0 — Localized Health & Medicine V1

Estado: `PLANNED — BLOCKED BY M38.1 MANUAL CLOSEOUT`.

Objetivo: regiones, heridas, sangrado, dolor y tratamientos sólo después del closeout manual de M38.1 y una autorización separada.

## Dirección De Producción

El pequeño playable exploration prototype no está iniciado. Después del closeout aplicable podrá reutilizar la infraestructura existente para evaluar gameplay y presentación; no es una vertical slice final, no recibe milestone ID nuevo y no adelanta M45.1.

## Secuencia De Modding Posterior

La foundation actual no implementa el sistema completo. Las próximas piezas se planifican como unidades separadas en este orden conceptual:

`manifest → provenance → dependencies → patches`

M50.0 conserva el alcance de compatibilidad de producción; ID TBD — Global Content ID Namespace Foundation no lo sustituye ni lo marca iniciado.

## No Iniciar Todavía

- nuevas ampliaciones OnGUI;
- UI final;
- combate o IA;
- condition, repair o crafting;
- actores o mundo a escala fuera del seam mínimo implementado por M38.0;
- facciones amplias;
- generación procedural;
- producción masiva de contenido.
