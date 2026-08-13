# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximos Tres Trabajos

### 1. M40.0 — Manual Unity Validation & Closeout

Estado: `IMPLEMENTED — AUTOMATED COMBAT / WEAPONS VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Objetivo: validar manualmente aim/fire/reload/melee, feedback, input/UI, cambio de arma, drop/pickup y Current Slice fresh-session; reconciliar el cierre sin reabrir implementación.

Condición de cierre: evidencia manual explícita de Mauro. Automatización sola no permite `DONE`.

### 2. M40.1 — Armor & Penetration V1

Estado: `PLANNED — BLOCKED BY M40.0 MANUAL CLOSEOUT`.

No iniciar hasta cerrar manualmente M40.0.

## Dirección De Producción

El pequeño playable exploration prototype no está iniciado. Después del closeout aplicable podrá reutilizar la infraestructura existente para evaluar gameplay y presentación; no es una vertical slice final, no recibe milestone ID nuevo y no adelanta M45.1.

## Secuencia De Modding Posterior

La foundation actual no implementa el sistema completo. Las próximas piezas se planifican como unidades separadas en este orden conceptual:

`manifest → provenance → dependencies → patches`

M50.0 conserva el alcance de compatibilidad de producción; ID TBD — Global Content ID Namespace Foundation no lo sustituye ni lo marca iniciado.

## No Iniciar Todavía

- nuevas ampliaciones OnGUI sin milestone autorizado;
- UI final;
- M40.1 antes del closeout manual M40.0 y AI;
- condition, repair o crafting;
- actores o mundo a escala fuera del seam mínimo implementado por M38.0;
- facciones amplias;
- generación procedural;
- producción masiva de contenido.
