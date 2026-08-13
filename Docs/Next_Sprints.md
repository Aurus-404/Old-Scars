# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximo Trabajo

### 1. M41.1 — Human Encounter AI V1

Estado: `PLANNED`.

M41.0 está `DONE — NAVIGATION / PERCEPTION FOUNDATION VALIDATED`, con validation `AUTOMATED + MANUAL UNITY PASSED`. M41.1 queda disponible como siguiente milestone, pero no está iniciado ni autorizado por este closeout.

Alcance previsto por el Roadmap:

- humanos capaces de evitar, alertarse, huir y luchar;
- reutilización de Navigation, Perception y del contrato de combate existente;
- transiciones interrumpibles y feedback observable;
- comportamiento acotado, sin facciones estratégicas ni framework universal de AI.

Antes de implementar se debe congelar un prompt M41.1 conforme a `OldScars_Development_Rules.md` y `Milestone_Template.md`, revisar dependencias reales y obtener autorización explícita de Mauro.

## Dirección De Producción

El pequeño playable exploration prototype no está iniciado. Podrá reutilizar foundations validadas en un trabajo autorizado posterior; no es una vertical slice final, no recibe milestone ID nuevo y no adelanta M45.1.

## Secuencia De Modding Posterior

La foundation actual no implementa el sistema completo. Las próximas piezas se planifican como unidades separadas en este orden conceptual:

`manifest → provenance → dependencies → patches`

M50.0 conserva el alcance de compatibilidad de producción; ID TBD — Global Content ID Namespace Foundation no lo sustituye ni lo marca iniciado.

## No Iniciar Todavía

- M41.1 o AI combat sin autorización específica;
- nuevas ampliaciones OnGUI sin milestone autorizado;
- UI final;
- condition, repair o crafting;
- actores o mundo a escala fuera de las foundations implementadas;
- facciones amplias;
- generación procedural;
- producción masiva de contenido.
