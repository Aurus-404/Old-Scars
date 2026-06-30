# Old Scars - Current Milestone

## Estado Actual

Milestone 32: Debug Test House Kitchen Containers v0 esta implementado en el checkout y pendiente de validacion manual en Unity.

No esta cerrado como `validated` hasta que Play Mode confirme el flujo completo.

Los ultimos milestones cerrados y validados en Unity son:

- Milestone 25: World Object Profile v0.
- Milestone 26: Storage Transfer v0 / Bidirectional Item Transfer.
- Milestone 26.0.1: Storage Panel Layout Swap.
- Milestone 27: Search vs Open Storage v0.

La validacion confirmada incluye Data Load OK con 0 errors y 0 warnings, regresiones principales funcionando y los cambios pusheados.

## Milestone En Curso / Pendiente De Validacion

### Milestone 32: Debug Test House Kitchen Containers v0

Estado: `implemented`.

- `M32_DebugTestHouse/Containers/Fridge`, `Oven`, `Countertop`, `Cupboard` y `Upper countertop` fueron configurados como containers funcionales en `SampleScene`.
- Todos reutilizan `WorldObjectTags`, `WorldObjectDebugInfo`, `ContainerLootComponent`, `WorldObjectStateView`, `search_container`, `open_storage` e `ItemStorageDebugPanel`.
- Cada container tiene display name, inspect text, tags y loot table propios.
- Loot tables nuevas: `house_fridge_loot_01`, `house_oven_loot_01`, `house_countertop_loot_01`, `house_cupboard_loot_01` y `house_upper_cupboard_loot_01`.
- Item IDs existentes usados: `food_ration_01`, `water_bottle_01`, `bandage_01`, `ammo_303_british_01` y `scrap_metal_01`.
- `Oven` queda solo preparado semanticamente para una workstation futura mediante tags; no se implementaron crafting, recetas, WorkstationComponent ni UI nueva.
- No se tocaron player, movimiento, puertas, armas, animaciones, crates existentes, scripts C# ni prefabs.
- Pendiente de validar en Play Mode: search inicial con barra, apertura de storage, transferencias bidireccionales, reapertura con `open_storage` sin regenerar loot y regresion de containers debug viejos.

## Ultimo Estado Validado

### World Object Profiles

- `WorldObjectProfileDefinition` y `world_object_profiles.json` forman el pipeline minimo de perfiles reutilizables para objetos del mundo.
- `GameDataLoader`, `GameDatabase` y `DataValidator` cargan, registran y validan World Object Profiles.
- `WorldObjectProfileComponent` aplica una sola vez `display_name` e `initial_tags` sobre componentes existentes.
- Debug Locked Door usa `worldObjectProfileId = debug_locked_door_01`.
- `force_door` sigue funcionando con la puerta cargada desde profile.

### Storage Bidireccional

- `ItemStorageDebugPanel` permite transferir items entre Player Inventory y Open Storage.
- Acciones disponibles: `Take 1`, `Take Stack`, `Take All`, `Deposit 1` y `Deposit All`.
- Transferencias completas conservan la instancia.
- Transferencias parciales dividen stacks correctamente.
- No se duplican ni destruyen items.
- Si se deposita completamente un item equipado, se limpia `right_hand`.
- Contenedores y cuerpos restauran estado de contenido al depositar cuando corresponde.
- Layout validado: Player Inventory a la izquierda y Open Storage a la derecha.

### Search Vs Open Storage

- `search_container` representa la primera revision de un contenedor natural.
- Requiere `opened_container + unsearched_container`, conserva barra de carga, remueve `unsearched_container`, agrega `storage_accessible` y abre `ItemStorageDebugPanel`.
- `open_storage` es una accion y effect cerrado separado.
- Requiere `storage_accessible`, dura 0 y abre el mismo panel aunque el storage este vacio.
- `open_storage` no genera loot nuevo ni repite la revision inicial.
- Vaciar un contenedor no elimina `storage_accessible`.
- Debug Sealed Container, Survival Supply Debug Crate y Misc Debug Crate usan el nuevo modelo.
- `search_body` no fue redisenado.

## Decisiones Vigentes

- JSON define datos; C# ejecuta logica.
- Perfiles data-driven definen configuracion inicial reutilizable, no estado runtime.
- Las escenas referencian profile IDs cuando corresponde.
- `ItemStorage` sigue siendo la base runtime comun de inventarios y storages.
- El acceso a un storage y la existencia de su contenido son conceptos separados.
- `ItemStorageDebugPanel` sigue siendo UI debug reusable, no UI final.
- `lootable_container` y `looted_container` se mantienen temporalmente por compatibilidad.

## Deuda Registrada

- Limpiar gradualmente la dependencia de tags legacy `lootable_container` / `looted_container` sin romper compatibilidad.
- Normalizar titulos debug inconsistentes como `Contenedor saqueado Contents (Debug)`.

## Proximo Recomendado

Preparar Milestone 28: Container State / Naming Cleanup v0.

Alcance recomendado:

- limpiar nombres y titulos debug de contenedores;
- aclarar el rol de tags legacy despues de `storage_accessible`;
- reducir deuda de estado/naming sin cambiar el comportamiento validado;
- preservar `search_container`, `open_storage`, transferencia bidireccional y `search_body`.

No implementar todavia:

- UI final;
- save system;
- storage de refugio/base;
- contenedores creados por jugador;
- rediseno de cuerpos;
- loot avanzado;
- combate o IA.
