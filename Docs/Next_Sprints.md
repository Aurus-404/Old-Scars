# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximos Tres Trabajos

### 1. M39.0 — Localized Health & Medicine V1

Estado: `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`.

Objetivo:

- regiones, heridas, sangrado, dolor y tratamientos sobre las foundations validadas de health, persistence y necesidades.

Fuera de alcance:

- combat, AI, weather, world-scale spawning y streaming;
- fatigue completa, beds/camping/shelters completos, UI final y playable exploration prototype;
- cualquier implementación antes de autorización explícita.

### 2. M40.0 — Combat Resolution & Weapons V1

Estado: `PLANNED`.

Objetivo: damage contract, melee/firearms, ammo y reload después de M39.0 y sus gates aplicables.

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
