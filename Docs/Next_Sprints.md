# Old Scars - Next Sprints

Este documento funciona como backlog ordenado. La fuente principal del roadmap vivo es `Docs/Project_Roadmap.md`.

## Proximo Recomendado

### Validar M34.2.1a: Fix Equipment From Item-Owned Storage

Estado: `implemented`; pendiente de validacion manual en Unity. M34.2 y M34.2.1 siguen pendientes hasta completar esta correccion. M33.3.1 esta `validated` por confirmacion del usuario.

- equipar palanca desde mochila hacia cada mano, tanto con slot libre como reemplazando rifle 2H;
- arrastrar rifle desde mochila a cualquiera de las manos y confirmar una sola entry ocupando ambos slots reales;
- llenar la grilla personal e intentar replacement; confirmar source/equipment/placements intactos ante rechazo;
- repetir menu contextual y drag para comprobar que ambos usan la misma revalidacion item-owned;
- soltar un item sobre `back` ocupado por mochila y confirmar transferencia first-fit sin cambiar el compartimento visible;
- confirmar que hover no abre automaticamente la mochila; esa mejora UX queda diferida;
- repetir regresiones de equip personal, no-nesting, doble clic, clamp, `Revisar contenedor`, drop/pickup y Console sin errores rojos.

### M33.3.1: Weight-Limited Partial Transfers

Estado: `validated`; validado manualmente en Unity por confirmacion del usuario.

Objetivo inmediato:

- con peso actual `34.20/39.00 kg`, tomar Water Bottle x500 y confirmar transferencia x4, source x496 y peso final `39.00 kg`;
- repetir con ammo `0.025 kg` cerca del hard limit y confirmar floor entero sin perder una unidad valida ni exceder el limite;
- probar un item de peso unitario cero y confirmar que no divide por cero ni limita la cantidad;
- estando exactamente en `HardBlocked`, intentar Take Stack y confirmar rechazo sin mutaciones ni hooks;
- probar Take Stack y Shift+clic external -> player con resultado parcial; confirmar source ID/placement/seleccion External y destination ID real;
- probar parcial que mergea y parcial que crea entry/placement; confirmar cantidades, IDs, grid y ausencia de entries huerfanas;
- llenar la grilla aunque el peso permita entrada y confirmar rechazo espacial/rollback completo;
- probar Take 1 y Take Amount por encima del peso disponible; confirmar comportamiento exacto y rechazo total;
- probar drag exacto y merge dirigido exacto; confirmar que nunca clampan silenciosamente;
- probar Shift+clic player -> external y transferencias entre storages no actor; confirmar comportamiento anterior sin clamp;
- confirmar que container/cadaver con remainder sigue saqueable y que el deposito posterior restaura estado de contenido;
- revisar toast absoluto, seleccion por `InstanceId`, 17 slots, equipment replacement M34.1.3, pickup/drop y Console sin errores rojos.

Fuera de scope: item-owned storage, mochila funcional, pockets, nesting, peso de subtrees, grillas mas granulares, item inspection, equip desde external, drop equipado, consumo desde equipment, armor, save/load, modelos y UI final.

### M34.2: Item-Owned Storage / Backpack Foundation

Estado: `implemented`; pendiente de validacion manual en Unity.

- crear dos `small_backpack_01`, cargar contenidos distintos y confirmar storage/layout independiente por `InstanceId`;
- equipar, reemplazar y desequipar en `back`; confirmar contenido, IDs, placements, ownership y peso sin cambios;
- mover items Personal <-> Mochila y External <-> Mochila por drag, Shift+clic y acciones 1/cantidad/todo;
- confirmar delta cero en movimientos internos aun en `HardBlocked`, y hard limit/clamp exacto al entrar desde external;
- soltar y recoger una mochila con contenido; confirmar una sola instancia, contenido intacto y peso completo de pickup/drop;
- intentar mochila dentro de mochila, self/cycle y confirmar rechazo sin mutaciones con el mensaje v0;
- validar selector de compartimentos, click en equipment `back`, fallback al perder acceso, celdas `32 px`, scroll e iconos;
- confirmar containers/cadaveres, loot state, right_hand, pickup/drop, SampleScene y Data Load sin regresiones.

### Grid Granularity Polish

Estado: `partial`; M34.2 agrega separacion entre dimensiones logicas y celdas visuales configurables de `32 px` con scroll. El rebalance completo de footprints queda pendiente.

### M34.1.4: Item Inspection Panel

Estado: `planned`; `Ver detalles` permanece oculto hasta definir un panel que aporte informacion real sin inventar stats.

## Pendientes De Validacion Previos

### Validar Milestone 32, Milestone 32.2, Milestone 32.4, Milestone 32.4.1 y Grid Inventory Backend v0

Estado: `implemented`.

Objetivo inmediato:

- validar en Play Mode que Fridge, Oven, Countertop, Cupboard y Upper countertop funcionan como containers reales;
- confirmar `search_container` inicial con barra, apertura de `ItemStorageDebugPanel`, transferencias bidireccionales y reapertura posterior con `open_storage`;
- confirmar que no se regenera loot al reabrir;
- confirmar que Debug Sealed Container, Survival Supply Debug Crate y Misc Debug Crate siguen funcionando;
- validar que la puerta Entrance inicia `locked_door`, muestra `force_door` con crowbar, pasa a `opened_door`, rota fisicamente y permite pasar;
- validar que la puerta Bedroom inicia `closed_door`, muestra `open_door`, pasa a `opened_door`, rota fisicamente y permite pasar;
- validar que una puerta `opened_door` muestra `close_door`, vuelve a `closed_door`, rota a cerrada y bloquea el paso;
- validar que `examine_object` muestra textos coherentes para `locked_door`, `closed_door` y `opened_door`;
- confirmar que la puerta debug vieja sigue mostrando estado coherente con `opened_door`;
- ejecutar `Old Scars/Debug/Validate M32 Door Pivots` para auditar las puertas M32;
- ejecutar `Old Scars/Debug/Repair M32 Door Pivots` si la validacion reporta roots escalados, pivots desplazados o visuales corruptos;
- volver a ejecutar `Validate M32 Door Pivots` y confirmar que no quedan posiciones locales absurdas en `DoorVisualPivot`/`DoorVisual`;
- validar que `HouseInteriorVolume` detecta entrada y salida del Debug Player;
- validar que las paredes entre camara y jugador se ocultan/restauran con `restoreDelay`;
- validar con camara cercana que `SphereCastAll` player -> camara y `OverlapSphere` de camara detectan paredes que bloquean vision;
- mirar debug casts: verde sin hit valido, rojo con hit valido y azul/cyan para overlap de camara;
- validar que las paredes invisibles siguen bloqueando movimiento porque sus colliders estructurales permanecen activos;
- validar que `CasaPrimerPiso` restaura su estado inicial deshabilitado al salir;
- validar que puertas, containers, muebles, player, items y NPCs no son ocultados por el sistema de visibilidad;
- confirmar Console sin errores rojos.
- confirmar que el Debug Player reporta grilla `6x8` y placements deterministas en `InventoryDebugPanel`;
- probar pickup/drop, uso de consumibles, Take/Deposit por item y por stack, y loot de cadaver sin mutaciones parciales;
- probar grilla llena, rotacion, merge sin placement extra, split con placement nuevo y preservacion de `right_hand` si una transferencia falla;
- confirmar que NPCs, cadaveres, containers y world items conservan storage lineal;
- revisar en una fase futura los `max_stack = 999` como deuda de balance, sin cambiarlos durante esta validacion.
- confirmar dual grid con Player a la izquierda, centro provisional y container/cadaver a la derecha en `1366x768`;
- probar recolocacion interna en ambos lados, `R`, Escape, cierre durante drag y preservacion exacta ante destino invalido;
- probar drag de stack completo player <-> container/cadaver y Shift/clic o botones 1/Stack con auto-placement;
- verificar que drop en celda vacia crea stack separado en la posicion/orientacion exactas aunque exista otro compatible;
- verificar merge dirigido por celda ocupada, incluyendo `20 -> 990/999`, source remanente x11, receptor lleno e item incompatible;
- confirmar que merge parcial conserva source selection/`right_hand`, merge completo elimina source placement y selecciona el receptor;
- confirmar toast superpuesto sin `GUILayout` y rect/columnas estables durante mensajes, rotacion, transferencia y storage vacio;
- validar fallback lineal completo ante grilla externa insuficiente, sin perdida de items, tags ni placements parciales;
- validar `I`: abre personal si no hay sesion y cierra cualquier sesion abierta sin cambiar de vista en la misma pulsacion;
- confirmar bloqueo de movimiento, disparo, interacciones y camara mientras la sesion esta abierta, y restauracion al cerrar;
- confirmar tags `searched`, `storage_accessible`, `lootable_container`, `looted_container` y `lootable_actor` despues de Take/Deposit;
- confirmar que pickup, drop, consumibles, firearm y `right_hand` siguen funcionando y que Console no muestra errores rojos.

Fuera de scope para esta validacion:

- crafting;
- recetas;
- WorkstationComponent;
- UI nueva;
- player, movimiento, armas o animaciones.
- cambios JSON/data-driven.
- cambios en `TagManager.asset`.

### Milestone 28: Container State / Naming Cleanup v0

Estado: `planned`, despues de validar M32/M32.2/M32.4 o cuando se retome deuda tecnica de containers.

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
