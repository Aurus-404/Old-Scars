# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronologia y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Milestone Activo

### M36.0 — Old Scars Strategic Production Roadmap Rebaseline

Estado inicial autorizado:

`PLAN REVIEWED — AUTHORIZED WITH CORRECTIONS`

Estado actual:

`IN PROGRESS — CHECKPOINT A IMPLEMENTED; CHECKPOINT B PENDING`

Estado esperado despues de ambos checkpoints:

`IMPLEMENTED — PENDING DOCUMENT REVIEW`

M36.0 es documental y no requiere Unity. No debe marcarse `DONE` antes de la revision explicita de Mauro.

## Checkpoint A

Objetivo: reconciliar autoridad documental, ledger historico, aliases, estados, dependencias, gates y proximos milestones.

Decisiones aplicadas:

- `Project_Roadmap.md` es la autoridad canonica de IDs, estados, dependencias y gates;
- la historia no se renumera;
- las colisiones y aliases quedan registrados;
- el cleanup antes llamado M28 pasa a `ID TBD`;
- M35.2 queda `DONE — FUNCTIONAL SCOPE CLOSED AFTER M35.2.3`;
- M35.2.3 queda `VALIDATED`;
- M35.2.3.1, M35.2.4 y M35.2.5 quedan `DEFERRED — RECLASSIFIED`;
- M32 y M32.2 quedan reconciliados como `VALIDATED` usando la confirmacion manual ya registrada en el log;
- M32.4, M32.4.1 y Grid Inventory Backend v0 conservan estado pendiente;
- `Next_Sprints.md` contiene solamente los tres proximos trabajos reales.

## Checkpoint B Pendiente

Debe alinear:

- el mirror resumido y mantenible del GDD Maestro v3.1, sin competir con su autoridad;
- arquitectura y reglas JSON vigentes;
- reglas de desarrollo y template de milestones;
- gates detallados y registro de riesgos;
- estado final de M36.0 como `IMPLEMENTED — PENDING DOCUMENT REVIEW`.

## Milestone Funcional Anterior

### M35.2 — Lootable Entity Inventory UI V1

Estado:

`DONE — FUNCTIONAL SCOPE CLOSED AFTER M35.2.3`

Base de cierre:

- M35.2.1 — `VALIDATED`;
- M35.2.2 — `VALIDATED`;
- M35.2.3 — `VALIDATED`;
- commit funcional validado: `27bf438637b621141ca553a39579349a12ff8700`;
- commit documental de validacion: `2956bcae19719a5f9073e24d58da4705742732fa`.

El scrollbar vertical de EQUIPADO con overflow real permanece como deuda no bloqueante de una futura etapa de UI.

## Secuencia Inmediata

1. Completar M36.0 Checkpoint B.
2. M36.1 — Foundation Freeze & Persistent Identity Contract.
3. M37.0 — Save Format & Persistence Core.
4. M37.1 — Current Slice Persistent Round-Trip.

M36.1 debe ser corto y no implementa save, condition, repair ni actor lifecycle. M37 persiste primero el slice actual y no diseña serializacion para sistemas hipoteticos.

## Limites Actuales

Durante M36.0 no modificar C#, JSON gameplay, escenas, prefabs, assets, Packages o ProjectSettings; no ejecutar Unity, batchmode ni compilaciones.

No reactivar la serie M35.2, ampliar OnGUI ni iniciar sistemas funcionales nuevos antes de cerrar la revision documental.
