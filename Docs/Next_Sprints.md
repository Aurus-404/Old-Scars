# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximo Trabajo

### 1. M41.0 — Navigation & Perception Foundation

Estado: `PLANNED`.

M40.1 está `DONE — ARMOR / PENETRATION V1 VALIDATED`, con validation `AUTOMATED + MANUAL FRESH-SESSION PASSED`; `Combat Ready` está `APPROVED`. M41.0 queda disponible para autorización, pero su implementación no está iniciada ni autorizada por este closeout.

Alcance previsto por el Roadmap:

- navegación foundation acotada;
- percepción diagnosticable;
- integración con la identidad/lifecycle M38 sin adelantar Human Encounter AI;
- diagnóstico reproducible y criterios de validación explícitos.

Antes de implementar se debe congelar el prompt de milestone conforme a `OldScars_Development_Rules.md` y `Milestone_Template.md`, revisar dependencias reales y obtener autorización explícita de Mauro.

### 2. M41.1 — Human Encounter AI V1

Estado: `PLANNED`.

Permanece posterior a M41.0 y dependiente de sus contratos. No está iniciado ni autorizado; no adelantar AI combat durante la foundation de navegación/percepción.

## Dirección De Producción

El pequeño playable exploration prototype no está iniciado. Podrá reutilizar foundations validadas en un trabajo autorizado posterior; no es una vertical slice final, no recibe milestone ID nuevo y no adelanta M45.1.

## Secuencia De Modding Posterior

La foundation actual no implementa el sistema completo. Las próximas piezas se planifican como unidades separadas en este orden conceptual:

`manifest → provenance → dependencies → patches`

M50.0 conserva el alcance de compatibilidad de producción; ID TBD — Global Content ID Namespace Foundation no lo sustituye ni lo marca iniciado.

## No Iniciar Todavía

- nuevas ampliaciones OnGUI sin milestone autorizado;
- UI final;
- M41.1, AI combat o cualquier ampliación posterior antes de cerrar M41.0;
- condition, repair o crafting;
- actores o mundo a escala fuera del seam mínimo implementado por M38.0;
- facciones amplias;
- generación procedural;
- producción masiva de contenido.
