# Old Scars - Next Sprints

Este documento funciona como backlog ordenado. La fuente principal del roadmap vivo es `Docs/Project_Roadmap.md`.

## Proximo Recomendado

### Validar M34.1 + M34.1.1: Equipment Foundation & UI Cleanup

Estado: `implemented`; pendiente de validacion manual en Unity.

Objetivo inmediato:

- agregar `ActorItemOwnershipComponent` y `ActorEquipmentComponent` solo al Debug Player, dejando que sus referencias al `InventoryComponent` se autocompleten;
- confirmar Data Load OK con `EquipmentSlots: 17`, `EquipmentLayouts: 1`, `human_standard_01` y sin errores/warnings nuevos;
- abrir inventario personal y storage externo y confirmar las tres columnas estables: grilla personal, 17 slots centrales y grilla externa;
- equipar la palanca a `hand_right`, desequiparla y repetir en `hand_left`, conservando el mismo `InstanceId` y el peso total;
- equipar dos items de una mano simultaneamente y confirmar que ocupan manos distintas;
- equipar el rifle y confirmar dos filas `2H`, una sola entry de equipment, un solo `InstanceId` y peso contado una vez;
- intentar equipar el rifle con una mano ocupada y confirmar rechazo sin mutaciones, swaps ni drops;
- llenar la grilla personal y confirmar que desequipar rechaza sin liberar slots ni cambiar ownership/peso;
- confirmar que `force_door`/`pry_open_container` aceptan la palanca tambien desde `hand_left` cuando `hand_right` esta vacia;
- probar scroll persistente, auto-scroll al slot equipado, seleccion de ambas filas del rifle y retorno de seleccion a la grilla al desequipar;
- confirmar en inventario personal y storage externo que las tres columnas empiezan y terminan a la misma altura a `1366x768`;
- comprobar que Equipment no tiene scrollbar horizontal y que slot/item quedan alineados izquierda/derecha con clipping;
- confirmar que el header legacy `Right Hand`/`Unequip` ya no aparece y que Close sigue visible en la cabecera general;
- seleccionar items personales y externos y verificar que Deposit/Take 1/Stack permanecen visibles dentro del footer;
- confirmar que detalles largos usan scroll vertical interno y que ninguna seleccion vieja reemplaza personal/equipment/external en el siguiente `OnGUI`;
- verificar `I`, `Escape`, cancelacion de drag, toast absoluto, pickup/drop, consumibles, firearm y transferencias M33 sin regresiones;
- confirmar Console sin errores rojos antes de marcar M34.1/M34.1.1 como `validated`.

Fuera de scope para esta validacion: Inventory Context Menu v0, weight-limited partial transfers, componentes de equipment en NPCs/cadaveres, item-owned storage, mochila funcional, pockets, nesting, peso de subtrees, equip desde mundo, drop equipado, auto-swap, armor, save/load, modelos y UI final.

### M34.2: Item-Owned Storage / Backpack Foundation

Estado: `planned`; comenzar solo despues de validar M34.1 y aprobar su plan tecnico.

- incorporar el primer nodo item-owned sin duplicar `ItemInstance` ni reemplazar `ItemStorage`;
- definir ownership/traversal y peso de subtree de forma acotada para una mochila funcional;
- preservar equipment slots como referencias y `ActorItemOwnershipComponent` como agregado del actor;
- no adelantar nesting general, pockets arbitrarios, save/load ni UI final sin aprobacion explicita.

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
