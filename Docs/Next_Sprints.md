# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximo Trabajo

### 1. M41.0 — Manual Unity Validation & Closeout

Estado: `IMPLEMENTED — AUTOMATED NAVIGATION / PERCEPTION VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Checklist pendiente:

1. Abrir `SampleScene` en una sesión Unity fresca y entrar en Play.
2. Ejecutar `Old Scars/Diagnostics/AI/M41.0 Prepare Manual Validation`.
3. Verificar que el navigator rodea la barrera, no la atraviesa y queda detenido en `Reached`.
4. Alternar `M41.0 Toggle Manual Perception Blocker` y confirmar `Occluded` con barrera / `Perceived` sin barrera.
5. Confirmar ausencia de errores M41.0 y registrar evidencia.
6. Realizar el closeout documental; no reabrir implementación salvo una regresión funcional real.

### 2. M41.1 — Human Encounter AI V1

Estado: `PLANNED`.

Permanece posterior al cierre manual de M41.0 y dependiente de sus contratos. No está iniciado ni autorizado; no adelantar hostility, alert states, chase, flee, combat decisions ni otra conducta humana durante el closeout de la foundation.

## Dirección De Producción

El pequeño playable exploration prototype no está iniciado. Podrá reutilizar foundations validadas en un trabajo autorizado posterior; no es una vertical slice final, no recibe milestone ID nuevo y no adelanta M45.1.

## Secuencia De Modding Posterior

La foundation actual no implementa el sistema completo. Las próximas piezas se planifican como unidades separadas en este orden conceptual:

`manifest → provenance → dependencies → patches`

M50.0 conserva el alcance de compatibilidad de producción; ID TBD — Global Content ID Namespace Foundation no lo sustituye ni lo marca iniciado.

## No Iniciar Todavía

- M41.1, AI combat o cualquier comportamiento humano antes del closeout manual de M41.0;
- nuevas ampliaciones OnGUI sin milestone autorizado;
- UI final;
- condition, repair o crafting;
- actores o mundo a escala fuera de las foundations implementadas;
- facciones amplias;
- generación procedural;
- producción masiva de contenido.
