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

Los objetos colocados en escena tambien deben usar tags registrados cuando esos tags conectan sistemas o documentan semantica de gameplay. Para M32 se agregaron tags de cocina como `kitchen`, `food_storage`, `oven`, `cooking_station`, `workstation_candidate`, `countertop`, `food_prep_surface`, `cupboard`, `storage` y `upper_cupboard`. Esos tags son semanticos: no implementan crafting, recetas ni workstation runtime por si mismos.

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
- inventory
- combat

El peso fisico de cada item es explicito y obligatorio:

```json
"physical": {
  "weight_kg": 2.3
}
```

- `physical.weight_kg` debe estar presente, ser finito y ser mayor o igual a cero.
- El peso de un stack es `weight_kg * quantity`; inventario personal y equipment storage se suman por entry. Las referencias de slots no agregan peso, por lo que un item multi-slot pesa una sola vez.
- La capacidad de carga es politica runtime opcional del owner y no pertenece a `ItemStorage`, al layout espacial ni al JSON del item.

El bloque espacial de inventario es cerrado y opcional durante la migracion:

```json
"inventory": {
  "footprint": {
    "width": 1,
    "height": 2
  },
  "initial_orientation": "rotated",
  "icon_id": "water_bottle_01"
}
```

- `footprint.width` y `footprint.height` deben ser enteros positivos.
- `inventory.initial_orientation` es opcional y solo admite `original` o `rotated`; si falta, usa `original`.
- La orientacion inicial es data-driven: no se deriva de `id`, categorias, tags, tipo, `icon_id` ni dimensiones.
- El first-fit prueba primero `initial_orientation` y despues la alternativa para footprints no cuadrados.
- La orientacion inicial se aplica solo a nuevas colocaciones y reconstrucciones; no fuerza placements existentes ni impide rotar manualmente con `R`.
- Todos los footprints rectangulares pueden rotarse intercambiando `width` y `height` mediante `GridPlacement.IsRotated`.
- Rotar un footprint cuadrado es un exito no-op: conserva geometria, orientacion existente y version del layout.
- Un item sin `inventory.footprint` usa fallback `1x1` y genera warning de validacion.
- El placement runtime pertenece a `ItemInstance.InstanceId`; no se define en JSON ni por indice de lista.
- `inventory.icon_id` es opcional y referencia un Sprite bajo `Resources/OldScars/InventoryIcons/`.
- `icon_id` no se deriva de `ItemDefinition.id`; distintos items pueden compartirlo y cambiarlo sin modificar C#.
- Un `icon_id` ausente o sin Sprite disponible usa fallback visual y no bloquea la carga de datos.

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

M32 agrega loot contextual deterministico para la casa debug en `container_loot.json`:

- `house_fridge_loot_01`
- `house_oven_loot_01`
- `house_countertop_loot_01`
- `house_cupboard_loot_01`
- `house_upper_cupboard_loot_01`

Estas tablas usan solo item IDs existentes: `food_ration_01`, `water_bottle_01`, `bandage_01`, `ammo_303_british_01` y `scrap_metal_01`.

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
    },
    {
      "type": "world_object_profile",
      "id": "debug_closed_door_01",
      "display_name": "Debug Closed Door",
      "initial_tags": ["closed_door", "inspectable"]
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

Los slots y layouts son definiciones data-driven cargadas una vez desde `equipment_slots/*.json` y `equipment_layouts/*.json`.

- `EquipmentSlotDefinition` usa `type`, `id` y `display_name`.
- `EquipmentLayoutDefinition` usa `type`, `id`, `display_name`, `groups` y `slots`.
- Cada grupo usa `id`, `display_name` y `display_order`; cada entrada usa `slot_id`, `group_id` y `display_order`.
- `human_standard_01` contiene exactamente 17 slots. `back` es generico; no existe `both_hands`.
- `ActorProfileDefinition.equipment_layout_id` puede referenciar un layout cargado.

Los items equipables usan alternativas completas mediante `slot_sets`:

```json
{
  "equip": {
    "equippable": true,
    "slot_sets": [
      ["hand_right"],
      ["hand_left"]
    ]
  }
}
```

- Cada array interno es un set atomico completo; todos sus slots deben existir y no repetirse.
- Un item de dos manos declara `["hand_left", "hand_right"]`; no crea un socket `both_hands`.
- Todo item equipable usa `max_stack = 1` y no puede declarar `slot_sets` vacio.
- `slot_sets` no puede coexistir con `allowed_slots`/`occupied_slots` legacy.
- El schema legacy se acepta temporalmente para mods antiguos y mapea `right_hand` a `hand_right`; no crea una segunda autoridad runtime.
- Equipment runtime guarda una sola entry en un storage lineal y los slots solo referencian su `InstanceId`.
- Item-owned storage, backpack y peso de subtrees quedan diferidos a M34.2.

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
    { "type": "add_tag", "target": "target", "tag": "opened_door" }
  ]
}

Puertas reales v0 usan estados canonicos:

- `locked_door`: puerta bloqueada, solo puede forzarse con una herramienta valida.
- `closed_door`: puerta cerrada no bloqueada, puede abrirse normalmente.
- `opened_door`: puerta abierta, puede cerrarse normalmente.

`force_door`, `open_door` y `close_door` usan solo `remove_tag` y `add_tag`.
`forced_open` queda como tag legacy y no debe usarse como estado principal nuevo.

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
