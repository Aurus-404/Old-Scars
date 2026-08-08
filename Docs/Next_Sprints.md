# Old Scars - Next Sprints

Este documento contiene solo los proximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Proximos Tres Milestones

### 1. M37.1 — Current Slice Persistent Round-Trip

Estado: `IMPLEMENTED — AUTOMATED ROUND-TRIP VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

Versión siguiente del mismo milestone: `Manual Unity Validation & Persistence Ready Closeout`.

Pass 1 y Pass 2 ya implementan snapshot/preflight, apply selectivo, rehydration exacta, world reconciliation, rollback y round-trip real automatizado. `Persistence Ready` permanece no aprobado hasta completar la validación manual fresh-session.

Objetivo:

- entrar a Play Mode, modificar pose/player, recoger crowbar, equipar Lee-Enfield, cambiar backpack/container, crear un runtime drop y cambiar una puerta;
- guardar mediante `Save Debug Slot`, salir completamente de Play Mode y entrar a una sesión fresca;
- esperar bootstrap normal, ejecutar `Load Debug Slot` y verificar identidades, quantities/placements, Equipment, owned storage, world state, doors, health/needs y player pose;
- revisar Console y cerrar `Persistence Ready` solamente si Mauro confirma el resultado sin errores funcionales relacionados.

Fuera de alcance:

- cambios de implementación o contrato sin evidencia nueva de bug real;
- autosave, UI final de save/load, cloud y profiles;
- actor lifecycle futuro, clima, facciones o proceduralidad;
- gameplay nuevo fuera del round-trip del slice existente.

Salida: decisión documentada del gate `Persistence Ready` y cierre de M37.1 si la validación manual es exitosa.

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
