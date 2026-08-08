# Old Scars - Next Sprints

Este documento contiene solo los proximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Proximos Tres Milestones

### 1. M37.1 — Current Slice Persistent Round-Trip

Estado: `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`.

M37.0 esta `DONE — PERSISTENCE CORE VALIDATED`. M37.1 es el siguiente milestone, pero este cierre no lo inicia y `Persistence Ready` permanece no aprobado.

Objetivo:

- guardar y rehidratar jugador, items, inventory/grid, Equipment, ownership e item-owned storages;
- guardar y rehidratar containers, cuerpos, puertas, world items y runtime tags existentes;
- comprobar que no se pierden `InstanceId`, cantidades, placements, owners o estados;
- consumir envelope, serializer y filesystem M37.0 sin reescribir su base.

Fuera de alcance:

- autosave, UI save/load, cloud y profiles;
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
