# Old Scars — Development Rules for ChatGPT + Codex

Version: 0.8

Purpose: reglas tecnicas y de trabajo durables para colaborar sobre Old Scars. La direccion de producto, el estado operativo y la historia tienen autoridades separadas; este archivo no las sustituye.

## 0. Politica De Autoridad

- Mauro conserva la autoridad creativa y la decision final de producto.
- Las decisiones explicitas recientes y los milestones aprobados/validados prevalecen sobre fuentes anteriores.
- [Game_Design_Document.md](Game_Design_Document.md) es el baseline de diseño revisado y mantenible; distingue direccion confirmada, objetivo, estado tecnico, propuesta y decision pendiente.
- El GDD Maestro v3.1 externo se conserva intacto como fuente historica y de diseño auditada, no como especificacion incuestionable.
- [Project_Roadmap.md](Project_Roadmap.md) es la autoridad de IDs, estados, dependencias y gates.
- [Current_Milestone.md](Current_Milestone.md) resume el trabajo activo.
- [Next_Sprints.md](Next_Sprints.md) contiene solo los proximos trabajos reales.
- [Development_Log.md](Development_Log.md) es append-only y registra eventos/evidencia sin reescribir snapshots historicos.
- [Technical_Architecture.md](Technical_Architecture.md) documenta contratos implementados.
- [DataDriven_JSON_Rules.md](DataDriven_JSON_Rules.md) documenta el schema y la validacion JSON vigentes.
- Git conserva evidencia historica de cambios; no reemplaza el estado operativo canonico del Roadmap.

Una contradiccion se resuelve segun su dominio y se registra: el repositorio prueba el estado tecnico, pero no vuelve canon final un prototipo. Las ambiguedades creativas, de producto o de alcance material se elevan a Mauro; no se completan por inferencia ni se sincronizan copias mediante memoria.

## 1. Desarrollo General

- No crear sistemas por anticipacion. Todo cambio necesita objetivo, milestone autorizado y resultado verificable.
- Preferir milestones suficientemente pequeños para ser revisables, pero suficientemente completos para entregar una unidad funcional util.
- No crear un milestone por clase, archivo, boton o ajuste menor. Agrupar cambios que comparten sistema, contratos, archivos, validacion y resultado jugable; separar tareas cuando mezclan sistemas independientes, riesgos distintos o decisiones abiertas.
- Evitar tanto la microfragmentacion como los milestones gigantes. Una unidad funcional completa no significa un sistema universal.
- Auditar implementacion, datos, escena, historial y deuda relevante antes de diseñar o borrar.
- Reutilizar contratos validados; no reescribirlos sin una razon concreta y aprobada.
- Separar foundation reutilizable de tooling o presentacion debug temporal.
- Evitar UI, arte, animacion, VFX y audio finales antes de que sus contratos y gates correspondan.
- Si una solicitud rompe una dependencia dura, un gate o el alcance autorizado, detenerse y explicar el conflicto.

### 1.1 Encargo Y Configuracion De Codex

Todo prompt debe declarar como nucleo minimo, con profundidad proporcional al riesgo:

- milestone con ID y nombre oficial, y milestone padre cuando exista;
- objetivo, estado inicial y estado esperado;
- relacion con milestones anterior y siguiente;
- alcance incluido y fuera de alcance;
- archivos o dominios autorizados y prohibidos;
- validacion requerida y documentacion afectada;
- estrategia Git.

Tambien debe indicar la configuracion recomendada de Codex:

- modelo: `GPT-5.6 Sol`, `Terra` o `Luna`;
- esfuerzo: `Mínimo`, `Bajo`, `Medio`, `Alto`, `Muy alta` o `Ultra`;
- velocidad: `Estándar` o `Rápida`;
- modo: `Plan` u `Objetivo`.

Reglas de seleccion:

- Luna para trabajo pequeño, mecanico y localizado;
- Terra para trabajo cotidiano, balanceado y de alcance acotado;
- Sol para arquitectura, transacciones, ownership, rollback, persistencia, ambiguedad o riesgo alto;
- Plan para auditorias, arquitectura, ambiguedad o alcance todavia abierto;
- Objetivo cuando objetivo, alcance y aceptacion ya estan definidos;
- no cambiar de modelo durante una tarea sin motivo explicito.

No se exige un prompt enorme para trabajo trivial. Una correccion localizada puede usar la variante compacta de [Milestone_Template.md](Milestone_Template.md); arquitectura, persistencia y cambios de alto riesgo requieren la extension condicional aplicable.

### 1.2 Evidencia Visual

Cuando una tarea involucra layout, UI, arte, modelos, texturas, escenas, colliders visibles, animaciones, camara, clipping, alineacion u otro defecto visual, el prompt debe adjuntar capturas cuando esten disponibles.

Codex debe:

1. abrir e inspeccionar todas las imagenes;
2. describir brevemente que muestra cada una;
3. relacionar el defecto con codigo, layout, escena o asset;
4. evitar una correccion basada solo en indicaciones abstractas;
5. no afirmar que el problema visual se resolvio porque compilo;
6. solicitar validacion visual posterior.

Los cambios puramente internos sin superficie visual no requieren capturas.

### 1.3 Uso De Subagentes

- Usarlos cuando la escala o independencia lo justifique: auditoria grande, investigacion paralela, arquitectura, QA, documentacion extensa o areas independientes.
- No usarlos por defecto para ajustes pequeños.
- Un agente principal integra, revisa contradicciones y toma la decision final.
- Normalmente un solo implementador modifica archivos o sistemas acoplados; varios agentes no los editan simultaneamente.
- Los subagentes pueden analizar areas independientes en paralelo, pero sus conclusiones no sustituyen la revision del agente principal.

## 2. Profundidad Mediante Sistemas Conectados

Todo sistema jugable nuevo debe:

- consumir estado relevante de al menos otro sistema;
- modificar una decision o costo jugable observable;
- emitir feedback comprensible sobre causa y resultado;
- definir como se valida la integracion y que ocurre al fallar;
- declarar impacto en datos, UI, persistencia, QA y rendimiento.

Una barra aislada, simulacion sin decisiones o backend sin consumidor real no satisface esta regla.

## 3. Data-Driven Y JSON

- JSON define contenido y parametros; C# ejecuta logica cerrada.
- Definitions viven en JSON/mods; instances y estado mutable viven en runtime o save.
- Los datos se cargan una vez, se validan y se consultan mediante `GameDatabase`.
- El deserializador actual ignora campos desconocidos; solo los campos documentados y respaldados por definition, validator y runtime forman parte del contrato.
- IDs son estables y snake_case. Deben ser unicos dentro de su tipo/registro; una reutilizacion textual entre familias no implica identidad compartida.
- `Mods/Core` carga primero. Mods externos pueden agregar IDs; no hay overrides, manifests ni versionado y los duplicados dentro del mismo tipo/registro se rechazan.
- Loot tables son definiciones separadas; los items no declaran sus fuentes de spawn.
- Effects JSON se limitan a tipos C# permitidos; no hay scripting libre.
- No agregar campos futuros, placeholders o schemas aspiracionales sin loader, validator y milestone aprobados.
- Estado de save nunca se escribe dentro de definiciones de contenido.

## 4. Contrato Actual De Items

- `ItemDefinition.max_stack` plano es la autoridad de stacking actual: uno significa no stackable; mayor que uno permite merge simple.
- `physical.weight_kg` es obligatorio y no negativo.
- `inventory.footprint`, `initial_orientation` e `icon_id` forman el bloque espacial/presentacional actual.
- `equip.equippable` y `equip.slot_sets` son la autoridad slot-aware; los campos planos/legacy solo existen por compatibilidad y no pueden contradecirla.
- Cada `slot_sets[]` es una alternativa atomica completa. Un item de dos manos usa `hand_left` y `hand_right` dentro del mismo set; nunca `both_hands`.
- `back` es un slot generico. Item-owned backpack storage ya existe y es un contrato independiente mediante `owned_storage_profile_id`.
- Los visuales M35 viven en perfiles separados: rig, assets, item visual profiles y attachment poses. No crear un bloque inline `equip_visual`.
- Condition actual es un valor runtime inicial get-only. M36.1 decide si M37 lo persiste, lo rederiva o lo excluye justificadamente; mutacion, desgaste, repair y disassembly pertenecen a M43.0.
- No adoptar `schema_version`, un bloque `stacking` u otra forma objetivo sin una migracion expresamente autorizada.

## 5. Inventory, Equipment, Ownership Y Storage

- `ItemStorage` permanece como base comun para inventario, Equipment, containers, cuerpos e item-owned storages.
- `ItemInstance.InstanceId`, no `DefinitionId` ni un indice, identifica la instancia representativa. Un stack actual usa una instancia mas `ItemStorageEntry.Quantity`; sus unidades fungibles no tienen IDs individuales. M36.1 congela la granularidad durable por categoria.
- Un item pertenece a exactamente un storage node; `ActorItemOwnershipComponent` valida ownership unico a traves del subtree.
- Equipment guarda una sola entry por item y sus slots referencian el mismo `InstanceId`.
- UI, visuales y diagnostics no mutan storage directamente; usan preview/commit/rollback y servicios existentes.
- Hooks y observers post-commit no extienden la atomicidad del estado: una excepcion se diagnostica y no revierte gameplay ya confirmado.
- Item-owned storage, ownership y subtree weight estan implementados y validados. El nesting de item-owned storage sigue prohibido en v0.
- Transfers dentro del mismo root owner no agregan peso; entradas externas respetan la politica de capacidad del actor.
- Los visuales se publican solo despues de un commit exitoso y no poseen gameplay.

## 6. Actor Profiles Y Bootstrap

- Inventario y Equipment iniciales se definen por actor profiles, no por listas probabilisticas inventadas ni hardcode por objeto.
- `initial_inventory` usa `item_id` y `quantity` y crea instancias reales en el storage personal; sus entradas no forman un lote atomico.
- `initial_equipment` requiere `equipment_layout_id`; cada entrada usa un item equipable de cantidad uno y `slot_ids` opcional que debe representar una alternativa completa.
- Si se omite `slot_ids`, debe existir una unica alternativa compatible libre.
- El lote `initial_equipment` es atomico y valida ownership; un fallo restaura el snapshot tomado despues de `initial_inventory`, pero no revierte el resto del profile.
- `inventory_seed_actor_tag` es una ruta debug limitada a `initial_inventory`; no aplica el profile completo.
- Actores muertos saqueables exponen sus storages reales; no se convierten en containers estaticos paralelos.
- `ActorHealthComponent` es una base escalar debug. Daño localizado, heridas y medicina no se infieren de su existencia.

## 7. Interaccion, Estado Y Feedback

- `InteractionSystem` permanece desacoplado de UI, inventario, loot, pickup y detalles de `MonoBehaviour`.
- Disponibilidad proviene de `ActionAvailabilityEvaluator` y `ActionAvailabilityResult`; panels y diagnostics no duplican esa logica.
- Menus ejecutables muestran acciones disponibles. Acciones bloqueadas pertenecen a diagnostics.
- `WorldObjectTags` posee estado runtime por tags. `WorldObjectStateView` lo presenta, pero no muta tags ni decide gameplay.
- `GameplayFeedbackLog` registra hechos ocurridos; `ActionAvailabilityDiagnostics` explica disponibilidad. No mezclar ambos contratos.
- Evitar scripts por objeto cuando un componente o regla data-driven cerrada resuelve el caso.

## 8. UI, Escena Y Presentacion

- Los panels `OnGUI` actuales son debug, no UI final ni fundamento para expansion indefinida.
- `InventoryUISessionController` conserva sesion, input, menu, modal y drag; los panels no crean otra autoridad.
- Helpers visuales pueden modificar hijos visuales, nunca deshabilitar el root de gameplay ni controlar estado.
- Mantener `SampleScene` y sus POIs estables salvo que el milestone autorice cambios concretos.
- Usar placeholders para validar comportamiento y evitar perseguir arte final antes de tiempo.
- Eventos/callbacks locales con ownership claro son validos. No introducir un bus global o una arquitectura universal sin necesidad y milestone aprobados.

## 9. Limites De M36.1 Y M37

- M36.1 es un freeze corto: clasifica contratos, define identidad durable, invariantes, boundaries de hidratacion, test seams y baseline.
- M36.1 decide la granularidad durable de stacks y el tratamiento del `ItemInstance.Condition` get-only para M37.
- M36.1 no implementa save I/O, condition, repair/disassembly, actor lifecycle, gameplay nuevo ni UI final.
- M37.0 define formato, version, checksum/integridad, escritura atomica, recovery y migrations para estado real existente.
- M37.1 prueba el round-trip del slice actual: jugador, items, grid, Equipment, ownership, item-owned storages, containers, cuerpos, puertas, world items y runtime tags.
- M37 no pre-serializa clima, facciones, actores futuros, economia regional o proceduralidad hipotetica.

## 10. Estados Y Validacion

- `IMPLEMENTED` significa alcance completado y evidencia estatica registrada; no equivale a prueba manual.
- `PENDING UNITY VALIDATION` identifica implementacion que requiere validacion manual en Unity.
- `VALIDATED` exige la prueba de aceptacion definida y evidencia explicita.
- `DONE` exige cierre funcional/documental y coherencia entre las fuentes correspondientes.
- Para un milestone documental, Unity puede ser `NOT APPLICABLE`; la revision documental requerida sigue siendo obligatoria antes de `DONE`.
- Separar siempre compilacion runtime, compilacion Editor, pruebas automatizadas, prueba manual, Console y revision documental.
- No afirmar validacion que no se ejecuto. Registrar deuda, limites y pruebas pendientes.

## 11. Documentacion Y Git

Actualizar solamente las fuentes afectadas:

- Roadmap cuando cambia un ID, estado, dependencia o gate.
- Current cuando cambia el snapshot activo.
- Development Log agregando un evento, nunca reescribiendo historia.
- Next cuando cambia la cola inmediata.
- Technical Architecture o JSON Rules solo cuando cambia un contrato.
- GDD mirror solo cuando cambia o se reconcilia la fuente de diseño.
- Gates/Risks cuando cambia un criterio, evidencia o riesgo.

Antes de commit:

- revisar `git status --short`, diff, stat y lista exacta de archivos;
- ejecutar `git diff --check`;
- verificar enlaces relativos y ausencia de rutas locales absolutas;
- comprobar IDs, estados, referencias y scope autorizado;
- distinguir validaciones ejecutadas de las no aplicables.

Todo trabajo mutante autorizado que supera las verificaciones aplicables debe terminar con:

1. commit;
2. cuerpo descriptivo del commit;
3. inspeccion de `git log -1 --format=full`;
4. push a `origin/dev`;
5. confirmacion de arbol limpio y sincronizado.

Excepciones: tarea explicita de solo lectura, auditoria sin cambios, fallo de compilacion/verificacion, bloqueo de alcance, cambios locales ajenos o instruccion explicita de Mauro de no publicar. Fuera de esas excepciones no usar formulaciones ambiguas como "hacer push cuando corresponda".

El titulo del commit incluye milestone, accion y descripcion breve. El cuerpo incluye milestone completo, padre, version/correction pass, objetivo, estado anterior/posterior, cambios, contratos preservados, verificaciones, validacion manual, deuda y trabajo diferido. Si el cuerpo inspeccionado esta vacio, corregir el commit local antes de publicar.

No usar amend despues de publicar, force push ni rebase sin autorizacion explicita.

## 12. Trabajo Prematuro

La presencia de un dominio en el Roadmap no autoriza adelantarlo. No introducir sin milestone activo:

- save, actor lifecycle, combate, IA, facciones o proceduralidad;
- UI final, journal/quests o framework global de eventos;
- condition/repair/crafting, audio/ruido o sistemas de produccion completos;
- refactors amplios de foundations validadas;
- schemas, perfiles o abstracciones universales sin consumidor actual.

## 13. Protocolo De Actualizacion

- Mantener este archivo compacto y estructural.
- Agregar solo reglas con probabilidad real de reutilizacion.
- Reemplazar reglas obsoletas en vez de acumular contradicciones.
- Subir la version solamente ante cambios estructurales.
