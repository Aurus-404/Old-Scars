# Old Scars - Next Sprints

Este documento contiene solo los proximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Proximos Tres Milestones

### 1. M37.1 — Current Slice Persistent Round-Trip

Estado: `IN PROGRESS — SNAPSHOT CONTRACT & SEMANTIC PREFLIGHT COMPLETE; TRANSACTIONAL REHYDRATION PENDING`.

Versión siguiente del mismo milestone: `Transactional Rehydration & Real-Scene Round-Trip Pass 2`.

El Pass 1 ya captura el slice real en DTOs durables, valida su semántica sin mutar, guarda/lee mediante M37.0 y compara canónicamente el resultado. `Persistence Ready` permanece no aprobado hasta completar apply, rollback, round-trip real y validación manual.

Objetivo:

- implementar rehydration exacta de jugador, items, inventory/grid, Equipment, ownership e item-owned storages sobre el snapshot ya validado;
- aplicar containers, cuerpos, puertas, authored/runtime world items y runtime state sin reseed ni duplicaciones;
- capturar rollback pre-load y restaurarlo mediante el mismo pipeline ante un fallo de apply;
- agregar `Load Debug Slot` y demostrar `capture → save → mutate → load → recapture → compare` en escena real.

Fuera de alcance:

- cambios al contrato del snapshot sin evidencia de bug real;
- autosave, UI final de save/load, cloud y profiles;
- actor lifecycle futuro, clima, facciones o proceduralidad;
- gameplay nuevo fuera del round-trip del slice existente.

Salida: gate `Persistence Ready`.

### 2. M38.0 — Actor Runtime & Lifecycle V1

Estado: `PLANNED`.

Objetivo:

- definir IDs, spawn y lifecycle durable de actores sobre `Persistence Ready`;
- integrar muerte y cuerpos persistibles sin reabrir los contratos congelados de M36.1;
- mantener este trabajo bloqueado por M37.1.

Fuera de alcance:

- necesidades/world clock de M38.1;
- combate, IA o UI final;
- mundo o contenido a escala.

### 3. M38.1 — Needs, World Clock & Recovery V1

Estado: `PLANNED`.

Objetivo:

- implementar world clock y necesidades conectadas sobre actor lifecycle persistente;
- incluir sueño/descanso como MUST y fatiga como SHOULD;
- mantener este trabajo bloqueado por M38.0.

Fuera de alcance:

- salud localizada, combate, IA o UI final;
- weather/ecology posteriores;
- mundo o contenido a escala.

## No Iniciar Todavia

- nuevas ampliaciones OnGUI;
- UI final;
- combate o IA;
- condition, repair o crafting;
- actores o mundo a escala;
- facciones amplias;
- generacion procedural;
- produccion masiva de contenido.
