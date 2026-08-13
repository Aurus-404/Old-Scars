# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M41.0 — Navigation & Perception Foundation

Estado final:

`DONE — NAVIGATION / PERCEPTION FOUNDATION VALIDATED`

Validation: `AUTOMATED + MANUAL UNITY PASSED`.

M40.1 permanece `DONE — ARMOR / PENETRATION V1 VALIDATED` y `Combat Ready — APPROVED`. M41.0 cierra capacidades separadas y data-driven de navegación NPC y percepción visual explicable sobre identidad/lifecycle M38. `AI Ready` permanece pendiente de M41.1.

## Evidencia De Cierre

- Runtime/Editor compile, Data validation, `M41.0 Navigation & Perception Diagnostics` y regresión directa M38.0: `PASS`.
- Mauro confirmó que el runtime actor recibió destination, se desplazó físicamente rodeando la barrera y completó `Moving → Reached` sin atravesar geometría bloqueante.
- Con observer/target deterministas y barrera activa, Perception informó `Occluded` y blocker exacto `Navigation Perception Barrier`.
- Al retirar la barrera, Perception informó `Perceived: True`, `Reason: Perceived` y `Blocker: <NONE>`.
- El helper manual fue corregido por `b4345890d9185d439d408cdece211424c88b8b21` para restaurar poses canónicas antes de cada evaluación y compartir el contrato del diagnóstico automático.
- El residuo local de la prueba manual en `SampleScene` contenía sólo desplazamiento accidental de la fixture y normalización de campos vacíos de Unity; se restauró desde la escena publicada sin perder contenido funcional.

## Contratos Cerrados

- `ActorNavigationController` posee exclusivamente la orden efímera, destino y estados `Idle`, `Moving`, `Reached` y `Failed`; no teleporta, no reintenta y respeta lifecycle `Dead`.
- `ActorVisualPerceptionService` permanece independiente de Navigation y Combat y explica identidad, range, FOV horizontal, LOS y blocker.
- Las capacidades se declaran mediante bloques opcionales `navigation` y `visual_perception` de `ActorProfileDefinition`; el player conserva sus autoridades propias.
- Orden, path y resultados de percepción permanecen efímeros. M41.0 no agrega estado durable ni cambia schema/envelope; tras restore Navigation queda `Idle`.

## Próximo Trabajo

M41.1 — Human Encounter AI V1 permanece `PLANNED`, disponible como siguiente milestone y no iniciado. `AI Ready` continúa pendiente de su validación.
