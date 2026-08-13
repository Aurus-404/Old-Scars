# Old Scars — Reglas JSON / Data-Driven

## Regla principal

JSON define datos.
C# ejecuta lógica.

Los JSON no deben contener lógica compleja ni scripts embebidos.

El deserializador actual ignora campos desconocidos. Ausencia de error no convierte un campo en contrato: solo usar campos documentados y respaldados por definition, validator y runtime.

## Paquete Core

Mods/Core representa el contenido base oficial del juego y debe estar estructurado como un mod interno que sirva de ejemplo a modders.

El juego carga Core primero bajo el namespace reservado `core`. Los mods externos actuales pueden agregar definiciones nuevas usando la misma estructura, pero deben declarar Global Content IDs canónicos explícitos; el nombre de su carpeta todavía no les asigna un namespace. No existe todavia politica de override o reemplazo: un ID duplicado dentro de su tipo/registro es error y la segunda definicion se rechaza.

Core JSON define contenido. Save/runtime define el estado concreto de la partida. C# ejecuta lógica.

## Identidad E IDs

No todos los campos terminados en `_id` pertenecen al mismo dominio. La clasificación obligatoria es:

| Dominio | Forma | Ejemplos | Regla |
| --- | --- | --- | --- |
| Global Content ID | `namespace:local_id` | `core:bandage_01`, `survival_plus:dirty_bandage` | Identifica una Definition en un registry global. Ambos segmentos usan sólo `[a-z0-9_]+`. |
| Local ID | `local_id` | `hand_right` como socket role, `receiver`, `original` | Sólo tiene sentido dentro de su Definition/contrato; no recibe namespace automático. |
| Runtime / Instance ID | contrato propio | `item_<32 hex>` | Identifica una instancia, no contenido; nunca se convierte a `core:*`. |
| Persistent scene ID | contrato propio | IDs authored validados por `PersistentSceneObjectId` | Identifica un root persistente de escena, no una Definition. |
| Tag | `snake_case` actual | `weapon`, `opened_door` | Permanece sin namespace en esta fase y debe existir en `tags.json`. |
| Asset key | `namespace:name` | `core:lee_enfield_world` | Clave secundaria de provider; comparte sintaxis de dos segmentos, pero no es `Definition.id`. |

`ContentId` es el único contrato sintáctico para Global Content IDs. No hace trim, lowercase ni reemplazo silencioso: espacios, mayúsculas, guiones, separador ausente o separadores múltiples son errores claros.

Usan Global Content ID todas las familias registradas en `GameDatabase`: items, item storage profiles, equipment slots/layouts, weapon/firearm/ammo/armor/penetration profiles, actions, loot tables, actor/world object profiles, visual rig capabilities/profiles, visual assets, item visual profiles y attachment poses. Sus referencias entre Definitions usan la misma forma canónica.

La unicidad es por tipo/registry. `core:test_item` y `test_namespace:test_item` pueden coexistir en el registry de items; `test_item` y `core:test_item` no pueden registrarse como dos aliases de Core.

Compatibilidad temporal:

- al cargar `Mods/Core`, un Global Content ID legacy sin namespace puede resolverse explícitamente como `core:<id>` y produce warning agregado;
- los mods externos no reciben esa regla y deben escribir `namespace:local_id`;
- lookups authored y saves schema v1 pueden usar el resolver `LegacyCore` documentado, sin crear keys alias;
- la compatibilidad histórica de Equipment `right_hand` → `core:hand_right` es una excepción de referencia legacy, no una regla general de Content IDs;
- esta ruta es removible después de migrar contenido authored/saves y no sustituye manifests.

Correcto como Global Content ID:

- `core:rusted_crowbar_01`
- `core:force_door`
- `weapons_expanded:g3a3`

Incorrecto como Global Content ID:

- `rusted_crowbar_01` fuera del contexto Core legacy
- `Core:rusted_crowbar_01`
- `core:force-door`
- `core:Force Door`

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

- `core:rusted_crowbar_01` (`ItemDefinition`)
- `core:blunt_swing` (`ActionDefinition`)
- `core:improvised_blunt_medium` (`WeaponProfileDefinition`)

Una Instance vive en runtime o en el save system y guarda datos variables.

Ejemplo futuro:

{
  "definition_id": "core:rusted_crowbar_01",
  "condition": 63,
  "owner": "player"
}

Este fragmento es conceptual, no el schema de save vigente. `definition_id` apunta a contenido y usa Global Content ID; la identidad runtime de la instancia usa un contrato separado. `Condition` pertenece al estado de la `ItemInstance` representativa, permanece get-only y debe coincidir para que dos stacks sean compatibles. Un stack usa una `ItemInstance` mas `ItemStorageEntry.Quantity`; sus unidades fungibles internas no poseen IDs individuales.

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
      "id": "core:debug_sealed_container_loot_01",
      "entries": [
        { "item_id": "core:scrap_metal_01", "count": 1 }
      ]
    }
  ]
}

Reglas:

- `type` debe ser `loot_table`.
- `id` debe ser un Global Content ID canónico.
- `entries` debe existir y no estar vacio.
- `item_id` debe referenciar un item cargado.
- `count` debe ser mayor que 0.
- `GameDataLoader` carga `loot_tables/*.json`.
- `GameDatabase` registra y expone loot tables.
- `DataValidator` valida loot tables sin errores cuando la data es correcta.
- `core:debug_sealed_container_loot_01` carga desde `container_loot.json`.
- No usar chance, pesos, rarezas, condiciones, economia ni random avanzado todavia.

M32 agrega loot contextual deterministico para la casa debug en `container_loot.json`:

- `core:house_fridge_loot_01`
- `core:house_oven_loot_01`
- `core:house_countertop_loot_01`
- `core:house_cupboard_loot_01`
- `core:house_upper_cupboard_loot_01`

Estas tablas usan solo item IDs existentes: `core:food_ration_01`, `core:water_bottle_01`, `core:bandage_01`, `core:ammo_303_british_01` y `core:scrap_metal_01`.

## World Object Profiles v0

Los World Object Profiles definen datos iniciales reutilizables para objetos del mundo colocados en escena.

Ejemplo:

{
  "world_object_profiles": [
    {
      "type": "world_object_profile",
      "id": "core:debug_locked_door_01",
      "display_name": "Debug Locked Door",
      "initial_tags": ["locked_door", "inspectable"]
    },
    {
      "type": "world_object_profile",
      "id": "core:debug_closed_door_01",
      "display_name": "Debug Closed Door",
      "initial_tags": ["closed_door", "inspectable"]
    }
  ]
}

Reglas:

- `type` debe ser `world_object_profile`.
- `id` es obligatorio, único dentro del registry y debe ser un Global Content ID canónico.
- `display_name` es obligatorio.
- `initial_tags` es obligatorio, no vacio, sin duplicados y solo puede usar tags registrados.
- El profile define configuracion inicial reutilizable; no guarda estado runtime.
- La escena referencia el profile ID mediante `WorldObjectProfileComponent`.
- `penetration_profile_id` es opcional y referencia un `PenetrationProfileDefinition`; si falta, la geometría es opaca y bloquea el ray M40.
- Los objetos no leen JSON directamente; la carga pasa por `GameDataLoader` y `GameDatabase`.
- `GameDataLoader` carga `world_object_profiles/*.json`; el perfil Core actual vive en `world_object_profiles/world_object_profiles.json`.
- No soportar `loot_table_id`, storage, contenedores ni estado runtime dentro del profile v0.

## Equipamiento

Los slots y layouts son definiciones data-driven cargadas una vez desde `equipment_slots/*.json` y `equipment_layouts/*.json`.

- `EquipmentSlotDefinition.id` y `EquipmentLayoutDefinition.id` son Global Content IDs.
- `EquipmentLayoutDefinition` usa `type`, `id`, `display_name`, `groups` y `slots`.
- Cada grupo usa un Local ID `id`, `display_name` y `display_order`; cada entrada usa `slot_id` global, `group_id` local y `display_order`.
- `core:human_standard_01` contiene exactamente 17 slots. `core:back` es un slot global genérico; no existe `both_hands`.
- `ActorProfileDefinition.equipment_layout_id` puede referenciar un layout cargado.

Los items equipables usan alternativas completas mediante `slot_sets`:

```json
{
  "equip": {
    "equippable": true,
    "slot_sets": [
      ["core:hand_right"],
      ["core:hand_left"]
    ]
  }
}
```

- Cada array interno es un set atomico completo; todos sus slots deben existir y no repetirse.
- Un item de dos manos declara `["core:hand_left", "core:hand_right"]`; no crea un socket `both_hands`.
- Todo item equipable usa `max_stack = 1` y no puede declarar `slot_sets` vacio.
- `armor_profile_id` es opcional y referencia un `ArmorProfileDefinition`; sólo un item equipable, no stackeable y no combinado con firearm/ammo puede declararlo.
- `slot_sets` no puede coexistir con `allowed_slots`/`occupied_slots` legacy.
- El schema `allowed_slots`/`occupied_slots` sigue siendo legacy. Durante la transición Core, la referencia histórica `right_hand` se mapea a `core:hand_right`; un mod externo no obtiene namespace implícito y no se crea una segunda autoridad runtime.
- Equipment runtime guarda una sola entry en un storage lineal y los slots solo referencian su `InstanceId`.
- Un item puede declarar opcionalmente `owned_storage_profile_id`, Global Content ID que referencia un `ItemStorageProfileDefinition` cargado desde `item_storage_profiles/*.json`.
- Todo item con `owned_storage_profile_id` debe usar `max_stack = 1`; cada runtime `ItemInstance` crea un storage independiente y nunca comparte estado por `DefinitionId`.
- Los perfiles usan `type: "item_storage_profile"`, `id`, `display_name`, `width` y `height`; dimensiones validas actuales: `1..64`.
- El JSON define dimensiones y referencia. Contenido, placements, versiones, owner raiz, peso y prohibicion de nesting son estado/logica C# runtime.
- M34.2 v0 permite un solo storage por item y prohibe item-owned storage dentro de otro item-owned storage. No define pockets, multiples compartimentos ni save data.
- `ActorProfileDefinition.inventory_seed_actor_tag` es un bootstrap debug opcional para aplicar solo `initial_inventory` a un actor sin `ActorProfileComponent`; el tag debe existir, aparecer en `initial_tags`, ser unico entre profiles y tener contenido inicial.

Ejemplo de item-owned storage profile:

```json
{
  "item_storage_profiles": [
    {
      "type": "item_storage_profile",
      "id": "core:backpack_small_01",
      "display_name": "Mochila pequena",
      "width": 8,
      "height": 10
    }
  ]
}
```

`required_sockets` no forma parte del contrato actual.

### Actor Profiles: inventario y Equipment inicial

`ActorProfileDefinition` puede declarar dos listas independientes:

```json
{
  "equipment_layout_id": "core:human_standard_01",
  "initial_inventory": [
    { "item_id": "core:bandage_01", "quantity": 2 }
  ],
  "initial_equipment": [
    { "item_id": "core:small_backpack_01", "slot_ids": ["core:back"] },
    { "item_id": "core:rusted_crowbar_01", "slot_ids": ["core:hand_right"] }
  ]
}
```

Reglas de `initial_inventory`:

- cada `item_id` debe ser un Global Content ID canónico y referenciar un item cargado;
- `quantity` debe ser mayor que cero;
- el bootstrap crea instancias runtime reales en el inventario, no una lista paralela de loot;
- las entradas se aplican individualmente; una entrada fallida no revierte las ya creadas;
- `inventory_seed_actor_tag` aplica solo esta lista a su ruta debug y no aplica Equipment.

Reglas de `initial_equipment`:

- si la lista tiene entradas, `equipment_layout_id` es obligatorio y debe referenciar un layout cargado;
- cada `item_id` debe ser un Global Content ID canónico, existir, ser equipable, declarar slots globales y tener `max_stack = 1`;
- `slot_ids` debe omitirse o coincidir exactamente con una alternativa completa declarada por `equip.slot_sets`;
- si `slot_ids` se omite, debe existir exactamente una alternativa compatible y libre;
- no se permiten slots duplicados, inexistentes ni solapados entre entradas;
- cada entrada crea una `ItemInstance` real de cantidad uno y la equipa mediante los servicios transaccionales existentes;
- el lote de Equipment es atomico: ante cualquier fallo restaura el snapshot tomado despues de `initial_inventory`, incluyendo inventario, Equipment, slots, storages item-owned creados y secuencia runtime de IDs;
- ese rollback no revierte display, tags, health, layout ni `initial_inventory`; el profile completo no es una transaccion;
- `initial_inventory` e `initial_equipment` son listas independientes; repetir una definicion en ambas crea instancias distintas.

### Actor Profiles: capacidades M41.0

`ActorProfileDefinition` puede declarar dos bloques opcionales e independientes:

```json
{
  "navigation": {
    "speed": 3.5,
    "acceleration": 8.0,
    "angular_speed": 360.0,
    "stopping_distance": 0.2
  },
  "visual_perception": {
    "visual_range": 15.0,
    "horizontal_fov_degrees": 120.0,
    "eye_height": 1.6
  }
}
```

- la presencia de cada bloque declara esa capacidad runtime; ausencia significa que el actor no la recibe;
- todos los valores deben ser finitos y estrictamente mayores que cero;
- `horizontal_fov_degrees` además debe ser `<= 360`;
- Navigation y Visual Perception pueden existir por separado y no declaran decisiones de comportamiento;
- estos bloques son Definitions: no guardan orden, path, estado `Moving/Reached/Failed`, target observado ni resultado de percepción;
- el player Core no declara `navigation`; su movimiento continúa bajo sus controladores propios;
- no agregar hostility, bravery, faction disposition, alertness, combat preference ni scripting AI a estos bloques.

## Actions

`ActionDefinition.id` es un Global Content ID, por ejemplo `core:force_door`. Las referencias `combat.actions[]` y `WeaponProfileDefinition.default_actions[]` usan el mismo contrato. En cambio, `effects[].type`, `contexts[]` y `target` son tokens/local IDs cerrados consumidos por C# y no reciben namespace.

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

Las Definitions `core:force_door`, `core:open_door` y `core:close_door` usan sólo effects locales `remove_tag` y `add_tag`.
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

## Visual rigs y attachments M35.0

- `visual_capabilities/*.json` declara Global Content IDs cerrados de compatibilidad estructural. No representan especies ni clases C#.
- `visual_rig_profiles/*.json` declara un Global Content ID para el profile y Local IDs para familia, partes, jerarquía, sockets y roles. Capabilities y mappings desde equipment slots son referencias globales. Parent parts, sockets y capabilities deben existir y no puede haber ciclos ni duplicados.
- `visual_assets/*.json` separa `asset_key` namespaced (`namespace:name`) del provider. M35.0 admite solamente `provider_id = builtin`; no se permiten tipos, metodos, reflection ni scripts desde JSON.
- `item_visual_profiles/*.json` usa Global Content ID propio y vincula `item_definition_id`, capabilities y `persistent_pose_id` globales con asset keys, política cerrada de socket, roles/grips locales y fallback cerrado.
- `attachment_poses/*.json` usa Global Content ID propio y referencias globales a visual profile/rig exacto; familia, socket ID/role, posición, rotación Euler y escala permanecen locales al contrato. La escala debe ser positiva y todos los valores finitos.
- `actor_profiles.visual_rig_profile_id` referencia por Global Content ID el rig visual de la entidad, separado de `equipment_layout_id`.
- Equipment slots siguen siendo autoridad gameplay. Sockets, poses y prefabs son presentacion y no pueden contener storage, ownership, interacciones, arma runtime, colliders ni rigidbodies.
- Asset keys pueden ser agregadas por mods de datos, pero M35.0 no carga AssetBundles, assemblies ni scripting de mods.

## Compatibilidad De Municion Vigente

- `firearm_profiles/*.json` usa `accepted_ammo_profile_ids` como lista obligatoria de perfiles de municion compatibles.
- Cada referencia debe ser un Global Content ID canónico, existir en `ammo_profiles/*.json` y no repetirse.
- La cadena vigente es item con `firearm_profile_id` → firearm profile → `accepted_ammo_profile_ids` → item de municion con `ammo_profile_id`.
- `magazine_capacity` debe ser mayor que cero; `reload_duration`, `cycle_time`, `range`, `muzzle_offset` y `debug_accuracy_spread` son valores finitos validados del profile.
- `AmmoProfileDefinition` declara impacto médico determinista mediante `wound_type`, `wound_severity`, `bleeding_rate_per_game_hour` y `pain_contribution`, más `penetration_power` finito y estrictamente mayor que cero para el proyectil. El runtime lo traduce a M39/M40.1; no declara scripts ni daño directo a HP.
- No existen flags `IsAP`, `CanPenetrate` ni branches por FMJ/AP/HP/tracer/anti-materiel. Toda munición usa el mismo resolver: futuras AP tenderán a mayor `penetration_power` y menor efecto blando, HP al caso inverso y FMJ a un baseline intermedio, siempre como datos y no como clases lógicas.
- `WeaponProfileDefinition` reutiliza el mismo contrato médico para melee y agrega `melee_range`, `attack_duration` y `attack_cooldown`.
- El estado cargado no vive en JSON de definitions: cada `ItemInstance` firearm mantiene ammo profile y rounds en runtime/save; capacity siempre deriva del profile.
- No usar un campo directo `accepted_ammo` en items o acciones; no es el contrato vigente y no reemplaza `accepted_ammo_profile_ids`.

## Armor Y Penetration Profiles M40.1

`penetration_profiles/*.json` declara capas comparables en una escala interna compartida:

```json
{
  "penetration_profiles": [
    {
      "type": "penetration_profile",
      "id": "example_mod:light_plate",
      "display_name": "Light Plate",
      "resistance": 0.325
    }
  ]
}
```

- `type`, Global Content ID canónico, `display_name` y `resistance` son obligatorios.
- `resistance` debe ser finita y `>= 0`; es una magnitud relativa compartida, no mm RHA, joules ni espesor físico.
- El mismo profile puede ser referenciado por wearable armor o por un world object penetrable. C# aplica siempre `power <= resistance → Stopped`, `power > resistance → Penetrated` y resta la resistencia al budget cuando penetra.

`armor_profiles/*.json` declara la adaptación wearable:

- `type: "armor_profile"`, Global Content ID y `display_name` obligatorios;
- `covered_regions` no vacío, sin duplicados y limitado a `Head`, `Torso`, `LeftArm`, `RightArm`, `LeftLeg`, `RightLeg`;
- `penetration_profile_id` canónico y existente;
- `impact_resistance` finita y `>= 0` para melee directo;
- `stopped_blunt_transfer` y `blunt_wound_threshold` finitos dentro de `[0,1]`;
- `layer_priority >= 0`, con desempates canónicos en runtime; el orden del JSON o de componentes no decide el resultado.

Un mod agrega item → `armor_profile_id` → regions/resistance/trauma sin C# nuevo. Sólo Equipment real protege; estas Definitions no guardan owner, slots ocupados, `Condition`, wounds ni outcome. M40.1 no agrega estado de save ni mutable durability.

## Fuera Del Contrato JSON Actual

Los siguientes campos o dominios requieren un milestone que defina schema, validacion, runtime aplicable y analisis explicito de impacto en persistencia antes de agregarse. La persistencia se implementa solamente cuando el dominio introduce estado mutable durable:

- noise
- sound
- creates_noise
- noise_level
- loot avanzado
- rarezas
- loot con pesos o chances
- definiciones de spawn/lifecycle para entidades runtime
- estado de save dentro de JSON de definiciones
- scripting dentro de JSON

El Roadmap puede autorizar estos contratos en milestones futuros. Hasta entonces no deben aparecer como placeholders ni campos ignorados. El save vigente serializa Global Content IDs de definitions junto con estado de instancia en un formato separado; nunca modifica `Mods/Core` ni convierte sus definiciones en estado de partida. Schema v1 conserva una compatibilidad temporal Core para referencias sin namespace, pero el estado nuevo se captura con IDs canónicos.
