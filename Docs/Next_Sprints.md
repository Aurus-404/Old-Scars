# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximos Tres Trabajos

### 1. M38.0 — Actor Runtime & Lifecycle V1

Estado: `IMPLEMENTED — AUTOMATED ACTOR LIFECYCLE VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Objetivo:

- completar el closeout manual fresh-session de actor authored Alive;
- comprobar visualmente Dead/corpse con misma identidad, transform, Inventory y Equipment después de load;
- comprobar spawn/save/fresh-session/load del actor runtime y Console sin errores de lifecycle/persistence.

Fuera de alcance:

- necesidades/world clock de M38.1;
- combate, IA o UI final;
- mundo o contenido a escala.

### 2. M38.1 — Needs, World Clock & Recovery V1

Estado: `PLANNED — BLOCKED BY M38.0 MANUAL CLOSEOUT`.

Objetivo: reloj y necesidades conectadas; sueño/descanso MUST y fatiga SHOULD, sobre el lifecycle de M38.0.

### 3. M39.0 — Localized Health & Medicine V1

Estado: `PLANNED`.

Objetivo: regiones, heridas, sangrado, dolor y tratamientos después de M38.1.

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
