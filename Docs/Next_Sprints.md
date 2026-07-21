# Old Scars - Next Sprints

Este documento contiene solo los proximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Proximos Tres Milestones

### 1. M36.1 — Foundation Freeze & Persistent Identity Contract

Estado: `PLANNED`.

Objetivo:

- congelar contratos actuales que M37 debe poder persistir;
- definir identidad durable para items, actores y objetos mundiales actuales;
- documentar invariantes de ownership y referencias;
- crear seams de prueba y un baseline proporcional.

Fuera de alcance:

- save/load;
- condition, repair o disassembly;
- actor lifecycle;
- gameplay o UI final.

Salida: gate `Foundation Freeze`.

### 2. M37.0 — Save Format & Persistence Core

Estado: `PLANNED`.

Objetivo:

- definir save envelope y version;
- implementar serializacion, escritura atomica, recovery y migrations;
- soportar primero las entidades y estados que existen en el slice actual.

Fuera de alcance:

- cloud save;
- streaming mundial;
- actores, clima, facciones o proceduralidad hipoteticos;
- contenido jugable nuevo.

### 3. M37.1 — Current Slice Persistent Round-Trip

Estado: `PLANNED`.

Objetivo:

- guardar y rehidratar jugador, items, inventory/grid, Equipment, ownership e item-owned storages;
- guardar y rehidratar containers, cuerpos, puertas, world items y runtime tags existentes;
- comprobar que no se pierden `InstanceId`, cantidades, placements, owners o estados.

Salida: gate `Persistence Ready`.

## No Iniciar Todavia

- nuevas ampliaciones OnGUI;
- UI final;
- combate o IA;
- condition, repair o crafting;
- actores o mundo a escala;
- facciones amplias;
- generacion procedural;
- produccion masiva de contenido.
