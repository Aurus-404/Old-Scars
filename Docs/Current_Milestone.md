# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronologia y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Milestone Cerrado

### M36.0 — Old Scars Strategic Production Roadmap Rebaseline

Version actual:

`Documentation Review Closeout`

Estado inicial del pass:

`IMPLEMENTED — PENDING FINAL DOCUMENT REVIEW`

Estado actual:

`DONE — DOCUMENTATION REVIEWED`

Estado posterior:

`DONE — DOCUMENTATION REVIEWED`

Mauro completo y aprobo la revision documental final. Unity validation fue `NOT APPLICABLE`; las decisiones creativas etiquetadas como pendientes siguen abiertas y no se convierten en canon por este cierre.

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

## Checkpoint B Implementado

Resultado:

- GDD Maestro v3.1 auditado y conservado intacto como fuente historica/de diseño;
- [Game_Design_Document.md](Game_Design_Document.md) creado como baseline revisado, resumido, etiquetado y mantenible;
- propuestas, estado tecnico, diseño objetivo, canon confirmado y decisiones pendientes separados;
- arquitectura y reglas JSON contrastadas con contratos reales;
- reglas de desarrollo y template de milestones alineados;
- gates detallados y registro de riesgos reconciliados;
- baseline documental aprobado por Mauro; las decisiones creativas abiertas conservan sus etiquetas.

## Documentation Review Correction Pass 1

Resultado:

- se elimino la implicacion de una mecanica de ruido confirmada;
- PSX/low-poly y legibilidad retro quedaron confirmados como direccion visual general, con art bible y especificacion de produccion pendientes;
- la existencia y los nombres Vandor/Velgrad quedaron confirmados sin completar su lore;
- prompts, configuracion Codex, evidencia visual, subagentes, granularidad de milestones, commit y push quedaron formalizados de manera proporcional al riesgo;
- [Milestone_Template.md](Milestone_Template.md) quedo dividido en nucleo obligatorio y extension condicional;
- R03 permanece `MITIGATING` como riesgo estructural permanente y Foundation Freeze revisa su mitigacion local sin cerrarlo globalmente;
- M29 conserva `IMPLEMENTED — HISTORICAL COMMIT; VALIDATION NOT RECONCILED`: existe evidencia de implementacion, pero falta prueba manual explicita o confirmacion de Mauro en el historial auditado;
- el alcance real de Checkpoint B queda registrado desde Git como 10 archivos, 1.345 adiciones y 497 eliminaciones;
- la revision documental final de Mauro fue aprobada; M36.1 no esta iniciado y requiere autorizacion independiente.

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

## Siguiente Milestone Planificado

### M36.1 — Foundation Freeze & Persistent Identity Contract

Estado:

`PLANNED — PENDING AUTHORIZATION`

M36.1 no comenzo. Requiere autorizacion independiente antes de modificar contratos, codigo o datos.

Secuencia posterior:

1. M36.1 — Foundation Freeze & Persistent Identity Contract.
2. M37.0 — Save Format & Persistence Core.
3. M37.1 — Current Slice Persistent Round-Trip.

M36.1 debe ser corto y no implementa save, condition, repair ni actor lifecycle. M37 persiste primero el slice actual y no diseña serializacion para sistemas hipoteticos.

## Limites Actuales

Este closeout no modifica C#, JSON gameplay, escenas, prefabs, assets, Packages o ProjectSettings; Unity, batchmode y compilaciones no aplican.

No reactivar la serie M35.2, ampliar OnGUI ni iniciar M36.1 u otros sistemas funcionales sin una autorizacion nueva.
