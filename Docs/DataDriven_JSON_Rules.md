# Old Scars — Reglas JSON / Data-Driven

## Regla principal

JSON define datos.
C# ejecuta lógica.

Los JSON no deben contener lógica compleja ni scripts embebidos.

## Paquete Core

Mods/Core representa el contenido base oficial del juego y debe estar estructurado como un mod interno que sirva de ejemplo a modders.

El juego debe cargar Core primero. Los mods externos deben poder agregar o modificar contenido usando la misma estructura.

Core JSON define contenido. Save/runtime define el estado concreto de la partida. C# ejecuta lógica.

## IDs

Todos los IDs deben usar snake_case.

Correcto:

- rusted_crowbar_01
- force_door
- improvised_blunt_medium

Incorrecto:

- RustedCrowbar
- force-door
- Force Door

## Tags

Los tags conectan sistemas.

Ejemplo de tags de un item:

- item
- tool
- weapon
- crowbar
- metal
- can_pry

Todo tag usado por un item, acción o perfil debe existir en tags.json.

## Definitions vs Instances

Una Definition vive en JSON y describe qué es algo.

Ejemplo:

- rusted_crowbar_01
- blunt_swing
- improvised_blunt_medium

Una Instance vive en runtime o en el save system y guarda datos variables.

Ejemplo futuro:

{
  "definition_id": "rusted_crowbar_01",
  "condition": 63,
  "owner": "player"
}

## Items

Los items pueden tener:

- display
- categories
- tags
- physical
- economy
- equip
- combat

Los items no deben declarar dónde aparecen en loot.

Incorrecto:

{
  "loot_sources": ["factory_crate"]
}

Eso lo hacen LootTables v0 desde Milestone 15.

## Loot Tables v0

Las loot tables definen contenido posible de contenedores u otras fuentes futuras.

Milestone 15 quedo validado con loot deterministico minimo:

{
  "loot_tables": [
    {
      "type": "loot_table",
      "id": "debug_sealed_container_loot_01",
      "entries": [
        { "item_id": "scrap_metal_01", "count": 1 }
      ]
    }
  ]
}

Reglas:

- `type` debe ser `loot_table`.
- `id` debe usar snake_case.
- `entries` debe existir y no estar vacio.
- `item_id` debe referenciar un item cargado.
- `count` debe ser mayor que 0.
- `GameDataLoader` carga `loot_tables/*.json`.
- `GameDatabase` registra y expone loot tables.
- `DataValidator` valida loot tables sin errores cuando la data es correcta.
- `debug_sealed_container_loot_01` carga desde `container_loot.json`.
- No usar chance, pesos, rarezas, condiciones, economia ni random avanzado todavia.

## World Object Profiles v0

Los World Object Profiles definen datos iniciales reutilizables para objetos del mundo colocados en escena.

Ejemplo:

{
  "world_object_profiles": [
    {
      "type": "world_object_profile",
      "id": "debug_locked_door_01",
      "display_name": "Debug Locked Door",
      "initial_tags": ["locked_door", "inspectable"]
    }
  ]
}

Reglas:

- `type` debe ser `world_object_profile`.
- `id` es obligatorio, unico y snake_case.
- `display_name` es obligatorio.
- `initial_tags` es obligatorio, no vacio, sin duplicados y solo puede usar tags registrados.
- El profile define configuracion inicial reutilizable; no guarda estado runtime.
- La escena referencia el profile ID mediante `WorldObjectProfileComponent`.
- Los objetos no leen JSON directamente; la carga pasa por `GameDataLoader` y `GameDatabase`.
- `GameDataLoader` carga `world_object_profiles/*.json`; el perfil Core actual vive en `world_object_profiles/world_object_profiles.json`.
- No soportar `loot_table_id`, storage, contenedores ni estado runtime dentro del profile v0.

## Equipamiento

Por ahora usar solo:

{
  "equip": {
    "allowed_slots": ["right_hand", "left_hand"]
  }
}

No usar required_sockets todavía.

## Actions

Las acciones deben usar cost:

{
  "cost": {
    "stamina": 5,
    "time": 0.8
  }
}

No usar duration_seconds por ahora.

## Action Effects

Las acciones pueden declarar una lista opcional effects.

effects no es scripting libre dentro de JSON. Solo describe efectos simples soportados por C#.

Por ahora solo se permiten estos effect type:

- add_tag
- remove_tag
- show_target_info
- pick_up_item
- search_container
- open_storage
- apply_damage
- kill_actor
- search_actor_inventory

Por ahora el unico target valido es:

- target

add_tag y remove_tag requieren tag.

show_target_info no requiere tag. No contiene texto narrativo ni scripts en JSON; C# lee la informacion debug desde WorldObjectDebugInfo en el target.

pick_up_item no requiere tag. Es un effect cerrado de C# validado en Milestone 14 que llama a WorldItemPickup en el target y agrega una ItemInstance al InventoryComponent del actor. No es scripting libre, loot generico, contenedor, drop system ni save system.

search_container no requiere tag. Desde Milestone 27 representa solo la primera revision de un contenedor natural con `opened_container + unsearched_container`; C# remueve `unsearched_container`, agrega `storage_accessible` y abre el panel de storage existente.

open_storage no requiere tag. Es un effect cerrado separado que abre un storage con `storage_accessible`, incluso vacio, sin generar loot nuevo ni repetir la primera revision.

Ejemplo:

{
  "effects": [
    { "type": "remove_tag", "target": "target", "tag": "locked_door" },
    { "type": "add_tag", "target": "target", "tag": "forced_open" }
  ]
}

Todo tag usado por un effect debe existir en tags.json.

Ejemplo no destructivo:

{
  "effects": [
    { "type": "show_target_info", "target": "target" }
  ]
}

Ejemplo de pickup debug v0:

{
  "effects": [
    { "type": "pick_up_item", "target": "target" }
  ]
}

Ejemplo de container loot v0:

{
  "effects": [
    { "type": "search_container", "target": "target" }
  ]
}

Ejemplo de apertura posterior de storage:

{
  "effects": [
    { "type": "open_storage", "target": "target" }
  ]
}

## No incluir todavía

No incluir por ahora:

- noise
- sound
- creates_noise
- noise_level
- accepted_ammo
- protection_profile
- loot avanzado
- rarezas
- loot con pesos o chances
- entities
- save data
- scripting dentro de JSON
