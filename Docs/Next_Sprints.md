# Old Scars - Next Sprints

Este documento funciona como backlog ordenado. La fuente principal del roadmap vivo es `Docs/Project_Roadmap.md`.

## Proximo Recomendado

### Milestone 28: Container State / Naming Cleanup v0

Estado: `planned`.

Objetivo recomendado:

- limpiar nombres y titulos debug de contenedores;
- revisar el uso actual de `lootable_container` y `looted_container` despues de M27;
- mantener compatibilidad mientras `storage_accessible` queda como tag principal de acceso posterior;
- corregir textos inconsistentes como `Contenedor saqueado Contents (Debug)`;
- preservar exactamente el comportamiento validado de `search_container`, `open_storage` y transferencias bidireccionales.

Restricciones:

- no redisenar `search_body`;
- no eliminar tags legacy sin una migracion segura;
- no cambiar loot tables ni generar loot nuevo;
- no cambiar layout ni logica de transferencia del panel;
- no crear UI final, save system, storage de base/refugio ni contenedores creados por jugador.

## Base Validada Para M28

- M25: World Object Profiles cargan, validan y aplican `display_name` e `initial_tags`; Debug Locked Door usa `debug_locked_door_01`.
- M26: Player Inventory y storages abiertos transfieren items en ambas direcciones sin duplicacion.
- M26.0.1: Player Inventory queda a la izquierda y Open Storage a la derecha.
- M27: `search_container` es primera revision; `open_storage` es acceso posterior incluso vacio.
- `storage_accessible` no se elimina al vaciar un contenedor.
- `search_body` sigue funcionando con el modelo anterior de cuerpos.
- Data Load validado: 0 errors y 0 warnings.

## Deuda A Resolver Mas Adelante

- Tags legacy `lootable_container` / `looted_container` mantenidos por compatibilidad.
- Titulos debug mezclados o dependientes de estado, como `Contenedor saqueado Contents (Debug)`.
- `search_body` todavia no separa Search Body de Open Body; queda fuera de M28 salvo decision explicita futura.

## Sprints Posteriores Posibles

### Search Body Vs Open Body

- Evaluar solo despues de estabilizar estados de contenedores.
- No redisenar actores muertos dentro de M28.

### Tool Requirement Schema Cleanup

- Evaluar una migracion futura de `weapon_tags` a un nombre mas general solo si existe necesidad concreta.
- Mantener compatibilidad con contenido existente.

### Save System Minimo

- Pospuesto hasta que exista suficiente estado runtime estable para persistir.
- No empezar con un save system avanzado.

## Pospuestos / No Tocar Todavia

- combate real;
- IA;
- facciones;
- mapa grande;
- vehiculos;
- crafting completo;
- UI final;
- dialogos complejos;
- procedural world;
- save system avanzado;
- storage de refugio/base;
- contenedores creados por jugador.
