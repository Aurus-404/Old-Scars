# Old Scars - Next Sprints

Este documento contiene solo los proximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Proximos Tres Milestones

### 1. M37.0 — Save Format & Persistence Core

Estado: `PLANNED — READY FOR IMPLEMENTATION AUTHORIZATION`.

M36.1 esta `DONE — FOUNDATION FREEZE APPROVED`. M37.0 es el siguiente milestone, pero este cierre documental no lo inicia.

Objetivo:

- definir save envelope y version;
- implementar serializacion, escritura atomica, recovery y migrations;
- soportar primero las entidades y estados que existen en el slice actual.

Fuera de alcance:

- cloud save;
- streaming mundial;
- actores, clima, facciones o proceduralidad hipoteticos;
- contenido jugable nuevo.

### 2. M37.1 — Current Slice Persistent Round-Trip

Estado: `PLANNED`.

Objetivo:

- guardar y rehidratar jugador, items, inventory/grid, Equipment, ownership e item-owned storages;
- guardar y rehidratar containers, cuerpos, puertas, world items y runtime tags existentes;
- comprobar que no se pierden `InstanceId`, cantidades, placements, owners o estados.

Salida: gate `Persistence Ready`.

### 3. M38.0 — Actor Runtime & Lifecycle V1

Estado: `PLANNED`.

Objetivo:

- definir IDs, spawn y lifecycle durable de actores sobre `Persistence Ready`;
- integrar muerte y cuerpos persistibles sin reabrir los contratos congelados de M36.1;
- mantener este trabajo bloqueado por M37.1.

Fuera de alcance:

- necesidades/world clock de M38.1;
- combate, IA o UI final;
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
