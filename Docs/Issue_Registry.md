# Old Scars — Issue Registry

Registro persistente de bugs, deudas, tooling problems y sospechas técnicas. No sustituye al Roadmap, Development Log ni Implementation Backlog.

Este archivo se mantiene deliberadamente compacto para que pueda leerse en cambios de chat/sesión sin cargar cientos de líneas de evidencia repetida. La evidencia histórica detallada permanece en `Development_Log.md`, Git, diagnostics y documentos de prueba asociados.

## Regla de uso

- Problema nuevo real/sospechado → registrar aquí.
- Fuera de alcance → registrar, no arreglar por inercia.
- `RESOLVED` nunca se borra: conserva causa, commit y validación resumida.
- `SUSPECTED` = síntoma/evidencia insuficiente.
- `CONFIRMED` = reproducido o demostrado por código/arquitectura.
- `RESOLVED` = corrección identificable + validación proporcional.

## Severidad

- `P0 / RED` — rompe contrato central o invalida pruebas/trabajo posterior.
- `P1 / ORANGE` — defecto importante de gameplay/AI/combat/tooling/integración.
- `P2 / YELLOW` — deuda/ergonomía/docs no bloqueante.

## Tipos

`BUG` · `DESIGN_DEBT` · `TOOLING` · `DOCS`

---

## Issues activos

### ISSUE-0023 — Posible desajuste Perception eye origin / representación humana
- **Tipo/estado/severidad:** `BUG` · `SUSPECTED` · `P2 / YELLOW`.
- **Origen:** cierre P1/F6, 2026-09-06; candidato separado indicado por Mauro.
- **Evidencia/límite:** CURRENT representa el origen productivo actual; queda por comprobar su coincidencia visual con los ojos de la representación humana. Sin causa confirmada ni corrección en P1.
- **Plan:** investigar en tarea separada antes de proponer cambios de eyeHeight, humanoid_standard o actor profiles; no reabrir Perception ni aim/F8 por este cierre.

### ISSUE-0025 — x100 acelera fisiología sin acelerar acciones activas
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P2 / YELLOW`
- **Origen:** prueba manual posterior a P3, 2026-09-08.
- **Síntoma:** durante combate con `WorldClock` x100 varios NPC pueden quedar `Incapacitated/Unconscious/Dead` en pocos segundos reales y la escena puede parecer detenida/colapsada.
- **Causa:** el multiplicador debug acelera `WorldClock/GameTimeAdvanced`; bleeding, blood/trauma recovery y needs avanzan con game time, mientras Encounter AI, Navigation, wound treatment y minimum Unconscious dwell siguen usando tiempo real normal. No se observaron 100 substeps por frame ni `Time.timeScale = 100`.
- **Impacto/alcance:** el comportamiento vuelve incoherente usar x100 durante combate activo, pero no invalida P2/P3 y no bloquea M41/F8. La mecánica x100 puede no sobrevivir al producto final.
- **Plan:** dejar registrado y no corregir ahora. Si el fast-forward se conserva, definir primero su contrato; evitar retunear medicina, KO o AI sólo para compensar este tooling.

### ISSUE-0026 — Comparación 30v30 Blue/Red con orden de spawn invertido pendiente
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P2 / YELLOW`
- **Origen:** primera prueba manual 30 Blue vs 30 Red, 2026-09-12; Red terminó con 8 supervivientes y Blue con 0.
- **Límite de la evidencia:** una sola corrida no sirve para atribuir ventaja a una afiliación. `SandboxNpcController` comparte `spawnSequence`, por lo que spawnear primero 30 Blue y luego 30 Red asigna bloques distintos de seeds deterministas, posiciones y loadouts; la primera corrida además mostró diferencias de armas, munición y armadura entre bandos.
- **Plan:** cuando la telemetry de combate esté disponible, ejecutar dos pruebas manuales comparables con mismo world/sandbox seed y escenario: A) 30 Blue → 30 Red; B) reset completo y 30 Red → 30 Blue. Dejar 5–10 minutos o hasta eliminación de un bando. Comparar ganador/supervivientes, roster/loadouts, munición/armadura, shots, hit rate, distancia/spread y distribución anatómica.
- **No hacer:** retunear afiliaciones, armas, accuracy o AI a partir de una única 30v30.

### ISSUE-0027 — Variedad anatómica de impactos en combate dinámico aún no validada
- **Tipo/estado/severidad:** `DESIGN_DEBT` · `SUSPECTED` · `P2 / YELLOW`
- **Origen:** cierre F8A/F8B/F8C + revisión posterior de distribución, 2026-09-12/13.
- **Evidencia actual:** `ISSUE-0008` quedó resuelto al reemplazar el centro de `ActorLocomotionCollider` por `ActorPrimaryAimPoint`; la comparación pareada eliminó el sesgo bajo. Sin embargo, el fixture controlado de primer disparo a ~10 m/full focus produjo PRIMARY con 110 Torso, 1 LeftArm y 0 Head/Legs, por lo que todavía falta comprobar si en combate dinámico real aparecen brazos/cabeza con variedad razonable.
- **Límite:** esto NO reabre `ISSUE-0008` ni demuestra por sí solo un bug de accuracy; el fixture F8C estaba diseñado para aislar el aim point, no para representar una batalla completa.
- **Plan:** medir primero con telemetry en las dos 30v30 invertidas: Head/Torso/LeftArm/RightArm/LeftLeg/RightLeg/Miss/World, junto con distancia, focus, spread y movimiento. Si Head/Arms siguen siendo anecdóticos bajo condiciones dinámicas, investigar en este orden geometría/exposición de hitboxes, distancia/movimiento/context penalties y spread; cambiar tuning sólo con evidencia.
- **No hacer:** RNG artificial por parte del cuerpo, porcentajes hardcodeados de Head/Arms, volver a apuntar al locomotion center o retunear spread antes de medir.


### ISSUE-0028 — Selección en storage flotante no alimenta el inspector principal
- **Tipo/estado/severidad:** `BUG` · `CONFIRMED` · `P2 / YELLOW`.
- **Origen:** playtest manual de `IMPL-0066` en `Assets/Scenes/SampleScene.unity`, 2026-10-01.
- **Síntoma:** al abrir el storage propio de una mochila y seleccionar un item dentro de la ventana flotante, el item queda resaltado en esa grilla pero el panel principal `Selected Personal Item` continúa vacío y no muestra stats/identidad/acciones del item seleccionado.
- **Impacto:** la selección del owned-storage queda visualmente aislada del inspector/contexto principal; no se observó pérdida de item, masa, ownership ni fallo de transferencia.
- **Plan:** investigar/reutilizar la selección canónica existente como `owner + InstanceId` (o seam equivalente) para que Player Grid y item-owned storage alimenten el mismo detalle sin crear una segunda autoridad de selección. Corregir en tarea propia; no ampliar IMPL-0066 por inercia.
- **Evidencia:** capturas del playtest muestran el rifle seleccionado dentro de `Mochila pequeña` mientras el panel principal sigue en `Click an item in the grid.`.

### ISSUE-0029 — Export de Play Session atribuye la sesión al scope IMPL-0042
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P2 / YELLOW`.
- **Origen:** log exportado durante el playtest manual de IMPL-0066, 2026-10-01.
- **Síntoma:** el archivo `OldScars_Play_*.txt` comienza con `# IMPL-0042` aunque la sesión corresponde a IMPL-0066.
- **Impacto:** no afecta gameplay ni el contenido capturado, pero puede atribuir evidencia futura al scope equivocado y volver ambiguo el historial de QA.
- **Plan:** revisar el metadata/header de `PlaySessionConsoleLogExporter`; si el scope está hardcodeado, volverlo neutral o derivarlo de una fuente explícita sin convertir el logger en autoridad de milestone. No corregir dentro de IMPL-0066 salvo que bloquee evidencia.


### ISSUE-0030 — Drag de Inventory duplica el item visual y puede renderizarlo debajo de la UI
- **Tipo/estado/severidad:** `BUG` · `CONFIRMED` · `P2 / YELLOW`.
- **Origen:** playtest manual de `IMPL-0066` en `Assets/Scenes/SampleScene.unity`, 2026-10-01.
- **Síntoma:** al arrastrar un item dentro del Inventory, el item original continúa renderizado en su slot de origen mientras también aparece la representación arrastrada, produciendo la impresión de dos copias simultáneas. Además, el visual de drag puede quedar por debajo de paneles/ventanas del Inventory en vez de renderizarse por encima de la interfaz.
- **Comportamiento esperado:** mientras el drag está activo, el contenido del slot de origen no debe dibujar una segunda copia completa del item; puede conservar únicamente su footprint/placeholder de origen si hace falta. La representación arrastrada debe renderizarse en un overlay UI superior, con icono/footprint/rotación/cantidad coherentes con su representación en la grilla.
- **Seguridad:** cancelar o fallar el drop debe restaurar exactamente la representación/estado original sin mutación de quantity, placement, identity u ownership.
- **Plan:** corregir junto con la próxima iteración de Inventory drag UX; integrar el contrato visual en `IMPL-0068` y reutilizar el drag/session state existente, sin crear una segunda autoridad de transferencia.
- **Evidencia:** capturas manuales muestran la bala x1 simultáneamente en el slot de origen y como elemento arrastrado, con este último parcialmente oculto detrás de la UI.


### ISSUE-0031 — Integridad del árbol publicado no demostrada frente al working tree local
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P1 / ORANGE`.
- **Origen:** auditoría externa V2, 2026-10-02; corresponde a AUD-01 + AUD-15.
- **Evidencia/límite:** validaciones recientes registran 13 archivos tracked y 13 untracked preexistentes, y publicaciones parciales por hunks sobre archivos con cambios locales preservados. La evidencia prueba el working tree canónico usado en esas corridas; no demuestra de forma aislada que el árbol publicado `dev` compile/cargue por sí solo. No se afirma que `dev` esté roto.
- **Impacto:** un clon/CI/colaborador puede descubrir una dependencia accidental de cambios locales; además el dirty state de larga vida encarece cada publicación y revisión.
- **Plan:** después del checkpoint IMPL-0063 Stage 1, realizar una comprobación acotada de integridad del `dev` publicado y decidir el destino del trabajo local histórico sin reset/clean/stash destructivo. La excepción de aislamiento, si hiciera falta, requiere autorización explícita de Mauro.
- **No hacer:** crear un worktree/clone Unity frío por comodidad ni descartar cambios locales.

### ISSUE-0032 — La pausa no suspende timers de gameplay basados en tiempo real
- **Tipo/estado/severidad:** `BUG` · `CONFIRMED` · `P1 / ORANGE`.
- **Origen:** auditoría externa V2, AUD-04.
- **Evidencia:** el menú pausa con `Time.timeScale = 0`, mientras wound treatment, minimum KO dwell, recent-enemy memory/self-treatment timing y blood-mark expiry usan `Time.realtimeSinceStartupAsDouble`. `Update` sigue corriendo con timeScale 0.
- **Impacto:** acciones/ventanas temporales pueden completar o expirar mientras physiology/WorldClock está detenido; ejemplo: un vendaje puede terminar durante la pausa sin avanzar sangrado.
- **Relación:** comparte la raíz conceptual de múltiples bases de tiempo con ISSUE-0025, pero el caso de pausa es un defecto de producto distinto.
- **Plan:** definir una base de “real gameplay time” que no escale con WorldClock pero sí respete pausa, y validar los consumers antes del Playable Core Loop Proof.

### ISSUE-0033 — Terrain mutation no revierte estado si falla mesh/collider/NavMesh rebuild
- **Tipo/estado/severidad:** `BUG` · `CONFIRMED` · `P2 / YELLOW`.
- **Origen:** auditoría externa V2, AUD-05.
- **Evidencia:** `WorldDeformableTerrainSpikeController.TryMutate` aplica la operación/densidad y registra mutation antes del rebuild; un fallo posterior retorna false sin restaurar el volumen/representación previa.
- **Impacto actual:** limitado al path volumétrico de desarrollo.
- **Impacto futuro:** bloquearía persistencia productiva: un caller podría recibir failure con estado ya mutado/parcial.
- **Plan:** no corregir por inercia durante el checkpoint; debe quedar resuelto antes del primer consumer durable de terrain mutation.

### ISSUE-0034 — Diagnostics sin runner agregado ni cadencia de regresión global
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P1 / ORANGE`.
- **Origen:** auditoría externa V3 final, AUD-07.
- **Evidencia:** la lectura completa de los 56 diagnostics de Editor confirma una estrategia fuerte (goldens de worldgen, fuzz/stress, injected failures + rollback, pruebas de integración reales), pero no existe harness compartido ni corrida agregada/cadencia global. El andamiaje de Play Mode está duplicado en ~40 archivos y parte de la suite está atada a tuning, assets y texto de logs.
- **Impacto:** la calidad individual de los diagnostics es alta, pero muchos PASS históricos quedan viejos mientras capas compartidas cambian; además mantener el harness copiado consume tiempo de producción.
- **Plan:** cuando el Playable Core Loop necesite baseline estable, definir un punto de entrada/cadencia proporcional y reducir duplicación sólo donde aporte mantenimiento real. No usar este issue como excusa para construir más tooling que gameplay.

### ISSUE-0035 — Reload usa sólo el primer stack compatible de munición
- **Tipo/estado/severidad:** `BUG` · `CONFIRMED` · `P2 / YELLOW`.
- **Origen:** auditoría externa V2, AUD-12.
- **Evidencia:** `WeaponCombatService.ReloadEquipped` retorna después del primer stack compatible; el diagnostic M40 cubre stacks únicos y no el caso multistack.
- **Impacto:** un arma puede quedar parcialmente recargada aunque existan rounds compatibles repartidos en otros stacks.
- **Plan:** cubrir caso multistack y reconciliar con el contrato “consume exactamente el faltante” cuando un scope de gameplay/combat lo habilite.

### ISSUE-0036 — Medical V1 no tiene cierre/recuperación de heridas
- **Tipo/estado/severidad:** `DESIGN_DEBT` · `CONFIRMED` · `P1 / ORANGE`.
- **Origen:** auditoría externa V2, AUD-16; elevado por Product Playability Rebaseline.
- **Evidencia:** bandage reduce el bleeding multiplier pero no existe reducción/eliminación de wound bleeding/pain con el tiempo; blood recovery sólo ocurre con bleeding efectivo cero.
- **Impacto:** una herida sangrante vendada puede convertirse en una muerte diferida inevitable y descansar acelera el tiempo fisiológico sin cerrar la herida. Esto bloquea un survival loop razonable si no es una decisión explícita.
- **Plan:** decidir primero el diseño mínimo de cierre (coagulación/cierre natural, nueva intervención, sutura u otra regla) y después implementar sólo lo que el Playable Core Loop necesite. No retunear combate para compensarlo.

### ISSUE-0037 — Input/acciones de producto dependen de superficies y controladores Debug
- **Tipo/estado/severidad:** `DESIGN_DEBT` · `CONFIRMED` · `P1 / ORANGE`.
- **Origen:** auditoría externa V2, AUD-09.
- **Evidencia:** `GameplayRuntimeComposition` agrega/valida superficies Debug; `FirearmDebugController` es adaptador de input de combate y `DebugActionExecutor` ejecuta effects y abre `ItemStorageDebugPanel`.
- **Impacto:** el producto no puede apagar tooling de desarrollo sin perder caminos reales de interacción/combate.
- **Plan:** no reescribir UI. Durante el Playable Core Loop separar únicamente input/intención/presentación que el loop necesite, preservando los backends existentes.

### ISSUE-0038 — Servicios de equipment duplicados ya divergieron en placement null
- **Tipo/estado/severidad:** `BUG` · `CONFIRMED` · `P2 / YELLOW`.
- **Origen:** auditoría externa V2, AUD-31.
- **Evidencia:** `EquipmentTransactionService`, `EquipmentOwnedStorageTransactionService` y `WorldItemEquipmentTransactionService` repiten el mismo flujo/helpers; el camino owned-storage exige `DestinationPlacement != null` donde el hermano de inventario lineal acepta null, pudiendo producir `StaleState`.
- **Impacto actual:** caso latente/no demostrado en gameplay productivo; el Player grid no lo expone.
- **Plan:** no hacer refactor DRY general. Verificar el caso cuando se toque equipment y unificar sólo la semántica de placement necesaria.

### ISSUE-0039 — Falta body turn in place para NPC quieto
- **Tipo/estado/severidad:** `DESIGN_DEBT` · `CONFIRMED` · `P2 / YELLOW`.
- **Origen:** auditoría externa V3 final, AUD-33; reclasifica el finding V2.
- **Evidencia:** Perception usa gaze y `ActorGazeController` limita la mirada a ±65° del body. Los diagnostics prueban que un target fuera del cono no debe percibirse y que gaze no rota el cuerpo. Lo que falta es una conducta productiva que rote el cuerpo cuando el NPC está quieto; hoy sólo el `NavMeshAgent` cambia facing al desplazarse.
- **Límite:** **no es bug de Perception ni del gaze cone**. El efecto jugable de rodear a un NPC quieto y forzar LostContact/Search es PROBABLE hasta verificarlo en Play.
- **Plan:** verificar el efecto en una prueba mínima. Si es indeseado, diseñar body-turn-in-place hacia target/atención sin retunear FOV ni volver omnisciente la percepción.

### ISSUE-0040 — Mojibake e idioma mezclado en textos visibles
- **Tipo/estado/severidad:** `BUG` · `CONFIRMED` · `P2 / YELLOW`.
- **Origen:** auditoría externa V3 final, AUD-35.
- **Evidencia:** mojibake localizado en **5 archivos / 37 líneas**: `actions/actions.json`, `WorldInteractionDebugTester.cs`, `WorldItemEquipmentTransactionService.cs`, `WorldItemPickup.cs` e `InventoryContextActionResolver.cs`. Además hay mensajes visibles mezclando español e inglés.
- **Impacto:** el jugador ve texto roto; los archivos siguen siendo UTF-8 válido, así que parser/JSON no lo detectan.
- **Plan:** corregir literales/encoding en un scope de higiene/UI y fijar idioma base; no crear un framework completo de localización por este issue.

### ISSUE-0041 — Fallos de datos/world bootstrap pueden dejar espera indefinida o error poco visible
- **Tipo/estado/severidad:** `BUG` · `CONFIRMED` · `P2 / YELLOW`.
- **Origen:** auditoría externa V2, AUD-24.
- **Evidencia:** con errores y `haltOnDataErrors`, `GameDataManager.IsReady` no pasa a true mientras varios componentes esperan en coroutine; worldgen/materialization tiene rutas de excepción/feedback limitadas.
- **Impacto:** un JSON roto propio/modded o un fallo inesperado puede parecer un juego congelado en vez de mostrar un fallo accionable.
- **Plan:** antes de exposición a jugadores/mods, publicar un failure state visible y salidas acotadas.

### ISSUE-0042 — TerrainChunkKey Stage 1 no es identidad durable global de terreno
- **Tipo/estado/severidad:** `DESIGN_DEBT` · `CONFIRMED` · `P1 / ORANGE`.
- **Origen:** auditoría externa V2, AUD-02/AUD-30; amplía el límite ya documentado por Stage 1.
- **Evidencia:** la volume lattice resuelve origin Y y vertical cell size desde min/max surface de la ventana activa; X/Z también son locales a esa ventana. La key incluye SectorId, ventana y layout/tuning. El mismo suelo bajo otra ventana puede recibir otra lattice/key.
- **Impacto actual:** ninguno productivo; el Stage 1 sigue siendo un checkpoint válido y el documento ya prohíbe asumir persistent mutation coordinates.
- **Impacto futuro:** streaming/persistencia durable requieren una lattice/address world-stable; persistir mutations con la key actual las dejaría huérfanas tras el rebaseline.
- **Plan:** aceptar Stage 1 por su alcance, pero no usar la key como identidad persistente ni iniciar Stage 2 por inercia. Resolver sólo con consumer real y autorización separada.


### ISSUE-0043 — Acciones contextuales pueden conservar alcance después de alejarse
- **Tipo/estado/severidad:** `BUG` · `SUSPECTED` · `P2 / YELLOW`.
- **Origen:** auditoría externa V3 final, AUD-37.
- **Evidencia estática:** `WorldInteractionDebugTester` valida los 2,5 m al abrir el menú. `ContextualActionDebugPanel.TryRevalidateAction` reevalúa tags/requisitos pero no distancia; `DebugActionProgressController` y `DebugActionExecutor` tampoco la validan y el menú contextual no bloquea WASD. Las acciones rápidas de pickup/equip sí revalidan.
- **Hipótesis:** abrir junto a una puerta/contenedor, alejarse y luego hacer click podría iniciar/completar la acción desde cualquier distancia. No fue ejecutado.
- **Plan:** verificar en Play. Si se reproduce, revalidar alcance al iniciar y al completar; definir oclusión por separado, no asumirla.

### ISSUE-0044 — Diagnostics de Blood Trails escriben assets productivos
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P2 / YELLOW`.
- **Origen:** auditoría externa V3 final, AUD-38.
- **Evidencia:** `BloodTrailsV1Diagnostics.RunBatch` reescribe `BloodTrailVisualSettings.asset` con constantes del diagnostic y guarda assets; `BloodTrailsR0Diagnostics.RunBatch` puede modificar `PC_Renderer.asset` y crear material/textura.
- **Impacto actual:** los valores hoy coinciden, pero el test puede revertir silenciosamente tuning de Inspector y ensuciar el árbol que pretende validar.
- **Plan:** separar setup/autoring de validación o hacer que el diagnostic verifique sin mutar assets productivos.

### ISSUE-0045 — Entradas de menú Editor pueden descartar escenas no guardadas
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P2 / YELLOW`.
- **Origen:** auditoría externa V3 final, AUD-39.
- **Evidencia:** cuatro entry points llaman `EditorSceneManager.OpenScene(..., Single)` sin `SaveCurrentModifiedScenesIfUserWantsTo`: CarryEncumbrance, LoadedAmmoMassConservation, M41F8CAimBiasEvidence e Impl00163RigidWearablesTools.GenerateAll.
- **Impacto:** ejecutarlos desde menú con una escena dirty puede descartar trabajo sin aviso.
- **Plan:** copiar el guard que ya usan diagnostics de terreno; no requiere rediseño de tooling.

---

## Issues resueltos / historial

### ISSUE-0022 — Loaded ammo desaparecía del cálculo de carry mass
- **Tipo/estado/severidad:** `BUG` · `RESOLVED` · `P1 / ORANGE`.
- **Origen/causa:** auditoría Carry Weight, 2026-09-06. Reload consumía ammo owned y la convertía en `ItemInstance.LoadedRounds`, pero `ItemWeightResolver` sólo sumaba masa base, quantity y owned-storage subtree.
- **Resolución:** `AmmoProfileDefinition.round_weight_kg` es la autoridad física canónica por round. `ItemWeightResolver` suma `LoadedRounds × round_weight_kg` como masa interna de la firearm y rechaza profile faltante/inválido o firearm mutable stackeada. No busca un `ItemDefinition` compatible y por tanto no depende del orden de mods.
- **Validación de datos:** todo ammo item debe tener `physical.weight_kg > 0` y coincidir con el profile dentro de `0.000001 kg`; Core `.303` conserva `0.025 kg`. Divergencias producen error con ambos IDs.
- **Conservación/persistencia:** reload parcial/completo y cancelado, equip/unequip, storage owned, drop/pickup, fire simple/múltiple/seco y Current Slice fueron verificados. Save conserva profile ID + rounds; la masa sigue derivada, sin schema migration ni blob duplicado.
- **Validación/commit:** `TEST-20260915-001` a `TEST-20260915-004`; commit funcional `481183ddfac82f9ff9e547da6c72a1af13ecc088`.
- **Resultado:** `DONE / RESOLVED / PUBLISHED`; `IMPL-0020` queda como próximo paso exacto.

### ISSUE-0012 — Falta modo debug Invincible
- **Tipo/estado/severidad:** `TOOLING` · `RESOLVED` · `P2 / YELLOW`.
- **Origen/causa:** post-Prueba 2; QA necesitaba observar wounds/bleeding/pain/trauma/KO reales sin que `Dead` terminara prematuramente la prueba. Saltar sólo `ProcessDeath` habría contradicho Vital Integrity, lifecycle, tags y fatal blood.
- **Resolución:** `ActorDebugInvincible` es un marker Player-oriented, efímero y no persistido. `ActorHealthComponent` conserva la autoridad terminal y limita Vital Integrity a un máximo de `0.001` sólo al intento Alive→Dead protegido, sin aumentarla. `ActorConditionComponent` conserva bleeding/KO y, después de atravesar `Condition → Health.Kill`, mantiene Blood apenas sobre `fatalBloodFraction` para que Alive, snapshot y physiology sean coherentes. OFF no cura ni resetea; la siguiente evaluación fatal vuelve a publicar Dead. Un actor ya Dead no revive y un NPC sin marker conserva el baseline.
- **Validación:** Runtime compile `PASS`; Editor compile `PASS`; `M41 Player Debug Invincible Diagnostics: PASS`; `Actor Consciousness & Incapacitation Diagnostics: PASS`. Sin cambios a AI/Perception/Combat/accuracy/KO dwell/persistence schema.
- **Commit funcional:** `c96900589816239bfdf6553fba699bca6b79a54f`.

### ISSUE-0008 — Sesgo de impactos hacia piernas/pies
- **Tipo/estado/severidad:** `BUG` · `RESOLVED` · `P1 / ORANGE`
- **Origen/síntoma:** Prueba 2, 2026-09-01; impactos NPC concentrados en piernas/pies.
- **Causa demostrada:** el aim de firearms usaba `ActorLocomotionCollider.bounds.center`, a `-0.18 m` del centro del collider Torso en el humano authored. El spread existente amplificaba ese punto de partida bajo.
- **Corrección/publicación:** F8B añade el seam target-side mínimo `ActorPrimaryAimPoint` y un único punto authored center-mass en `humanoid_standard`; `HumanEncounterAIController` lo usa para firearms y conserva el fallback legacy sólo para targets sin punto. `PhysicalOrigin`, melee y los valores de accuracy/spread/focus/damage/anatomy no cambiaron. Publicado en `dev` como `fix(combat): add target-side primary aim point` (cierre F8A/F8B/F8C, 2026-09-12).
- **Evidencia F8A:** 120 shots legacy en 75 seeds; 68 Torso, 15 LeftLeg, 14 RightLeg, 17 Miss y 6 Ground/world. Runtime/Editor compile y diagnóstico F8A PASS.
- **Evidencia F8C pareada:** 120 seeds únicos; 120 LEGACY + 120 PRIMARY, exactamente un primer shot por condición y `aim_sample_sequence == 1`. LEGACY: Torso 71, Left/RightArm 0, Legs 30, Head 0, Miss 12, Ground/world 7. PRIMARY: Torso 110, LeftArm 1, RightArm/Legs/Head 0, Miss 9, Ground/world 0. Transiciones: Leg→Torso 28, Leg→Miss 2, Torso→Torso 71, Torso→Leg 0, Miss→Torso 4, Miss→Miss 7, Ground/world→Torso 7, Miss→LeftArm 1. Aim point `-0.18 m → 0.00 m`; mean/max absolute paired spread delta `0.000061°`. Seed, sequence, focus, movement, firearm, ammo, position y shot origin pasaron equivalencia.
- **Validación:** F8A/F8B/F8C y Runtime/Editor compile `PASS`; ISSUE-0008 se resuelve por comparación controlada. No se retuneó accuracy ni se abrió F8D.

### ISSUE-0020 — Incapacidad temporal borra contexto de enemigo y reinicia el combate
- **Tipo/estado/severidad:** `BUG` · `RESOLVED` · `P1 / ORANGE`
- **Origen/causa:** Prueba 3.1/3.2 + código, 2026-09-03. Acquisition libera al target sin capacidad activa y `EnterInactive/ReleaseEncounter` limpiaba todo el contexto: `Fight → KO → Ambient → recovery → encounter nuevo`.
- **Corrección/publicación:** P3, `394d01886b8c6697ca2d492c4450282f561ba688`, 2026-09-07. Una identidad/contexto reciente en Encounter, separada de Threat, sin posición. Ambos lados conservan continuidad; no hay ataques deliberados al KO. Reacquisition exige Recognition/Perception y retoma Fighting sin otro Alerted.
- **Expiración:** `recent_enemy_memory_seconds`, `60 s` Core provisional, no balance final; tiempo real pausado sólo por incapacidad propia y renovado por observaciones legítimas. Expiry/death/invalidation/reemplazo limpia. No MemorySystem ni Search nueva.
- **Validación:** P3 PASS: KO con rival armado, tratamiento y AmbientTopOff con memoria, recovery visible/oculto, reconocimiento gradual, ausencia de posición oculta, expiración, muerte, retirada del runtime y reemplazo. Ocho regresiones PASS: P2, Human Encounter, Behavior ownership, Search, Gaze/Perception, Timed Bandaging, Opportunistic Reload y M38 lifecycle. Fixture Human Encounter actualizado a continuidad real sin relajar percepción/LKP; sin cambio de gameplay adicional.
- **Siguiente:** P9 legacy migration + integrated QA + cleanup + formal M41 closeout.

### ISSUE-0021 — Knockout/Unconscious sin minimum real-time dwell
- **Tipo/estado/severidad:** `DESIGN_DEBT` · `RESOLVED` · `P1 / ORANGE`
- **Origen/causa:** Prueba 3.1/3.2 + revisión Condition/WorldClock, 2026-09-03; recovery fisiológico acelerado carecía de un mínimo real explícito.
- **Corrección/publicación:** P2, `9ca0335cdc8b85bd49d20ddbe97ad814f44c8578`, 2026-09-07. Gate por episodio en Condition compartida Player/NPC; Core `5 s` inicial de prueba, no balance final. No extiende Incapacitated ni fuerza wake-up; physiology y Death continúan.
- **Persistencia:** Current Slice v1 guarda restante/continuidad; offline no consume el mínimo. Legacy que deriva Unconscious recibe mínimo completo, sin migración de schema.
- **Validación:** P2 x1 `5.001 s`, x100 `5.002 s`, configuración `0.3 s`, sesión Play nueva con `4.000 s` preservados después de más de `6 s` offline, invalid preflight/rollback exacto; Consciousness, Collapse, M39, M38, Human Encounter, Behavior ownership y Timed Bandaging/NPC Self-Treatment PASS.
- **Continuidad posterior:** P3 / ISSUE-0020 DONE/PUBLISHED; P4 fue aceptado manualmente y el próximo paso operativo es P9 después de P8.

### ISSUE-0024 — Fixture de collapse enviaba navegación fuera de Behavior ownership
- **Tipo/estado/severidad:** `TOOLING` · `RESOLVED` · `P2 / YELLOW`
- **Origen/evidencia:** regresión durante P2; tras recuperar, el fixture deshabilitaba Encounter y enviaba una orden directa a Navigation. Behavior retomaba Ambient y cancelaba la orden: actor upright, NavMesh válido, desplazamiento casi cero.
- **Corrección:** `9ca0335cdc8b85bd49d20ddbe97ad814f44c8578`; el fixture espera el dwell restaurado y emite la orden por `EnterEncounter`/`TryNavigateEncounter`. Mantiene assertions de postura, colisión y desplazamiento real. Ningún cambio de gameplay/AI.
- **Validación:** Actor Physical Collapse Diagnostics PASS, exit 0.

### ISSUE-0010 — Observabilidad global insuficiente para peleas multi-NPC
- **Tipo/estado/severidad:** `TOOLING` · `RESOLVED` · `P1 / ORANGE`
- **Origen:** Prueba 2; reconfirmado Prueba 3.
- **Síntoma:** sólo un NPC seleccionado recibe world visuals útiles; comparar ambos lados exige ciclar F6.
- **Corrección/publicación:** `5aac763c14c399bfe09a3e925c50698658ad2716`; CURRENT multi-NPC independiente del inspector, LAST histórico separado sin geometría actual del blocker y Dead/Inactive sin CURRENT engañoso.
- **Validación:** F6 Observability, Gaze/Perception y LostContact/Search PASS; P8/F10 actualizó el tooling global/targeting, Runtime/Editor compile, F10 diagnostic y F6 regression PASS; Mauro dio aceptación visual el 2026-09-13. Commit F10 f4d07434d2b0ea0387584320c81b48dd248bd280. El issue continúa RESOLVED.

### ISSUE-0011 — Inspector F6 demasiado dependiente de selección
- **Tipo/estado/severidad:** `TOOLING` · `RESOLVED` · `P2 / YELLOW`
- **Origen:** Prueba 2/3.
- **Síntoma:** estados simultáneos son difíciles de comparar.
- **Corrección/publicación:** `5aac763c14c399bfe09a3e925c50698658ad2716`; CURRENT multi-NPC independiente del inspector, LAST histórico separado sin geometría actual del blocker y Dead/Inactive sin CURRENT engañoso.
- **Validación:** F6 Observability, Gaze/Perception y LostContact/Search PASS; P8/F10 actualizó el tooling global/targeting, Runtime/Editor compile, F10 diagnostic y F6 regression PASS; Mauro dio aceptación visual el 2026-09-13. Commit F10 f4d07434d2b0ea0387584320c81b48dd248bd280. El issue continúa RESOLVED.

### ISSUE-0019 — F6 presenta snapshots históricos como percepción actual
- **Tipo/estado/severidad:** `TOOLING` · `RESOLVED` · `P1 / ORANGE`
- **Origen:** Prueba 3/3.1/3.2 + revisión de repo, 2026-09-03.
- **Síntoma:** FOV/LOS puede quedar atrás del NPC, parecer salir del piso/desaparecer; Dead/Inactive puede seguir mostrando `Perceived` histórico.
- **Causa publicada:** tooling consume `LastPerception`/`LastAcquisitionPerception` + `ObserverOrigin` snapshot y no diferencia claramente CURRENT vs LAST. No implica que perception productiva vea desde el origen viejo.
- **Corrección/publicación:** `5aac763c14c399bfe09a3e925c50698658ad2716`; CURRENT multi-NPC independiente del inspector, LAST histórico separado sin geometría actual del blocker y Dead/Inactive sin CURRENT engañoso.
- **Validación:** F6 Observability, Gaze/Perception y LostContact/Search PASS; P8/F10 actualizó el tooling global/targeting, Runtime/Editor compile, F10 diagnostic y F6 regression PASS; Mauro dio aceptación visual el 2026-09-13. Commit F10 f4d07434d2b0ea0387584320c81b48dd248bd280. El issue continúa RESOLVED.

### ISSUE-0001 — Blue/Red no realizaban roaming efectivo Idle
- **Estado:** `RESOLVED / P0`.
- **Causa:** Encounter cancelaba navegación Ambient al no haber threat.
- **Corrección:** `ActorBehaviorController` único owner alto `Ambient/Encounter/Search/Inactive`.
- **Commit:** `7fa47c59d8bbe1df61b598f01875e91b2b51c089`.
- **Validación:** desplazamiento físico White/Blue/Red + ownership interruption/resume PASS.

### ISSUE-0002 — Gate de roaming aceptaba órdenes sin probar movimiento
- **Estado:** `RESOLVED / P0`.
- **Causa:** diagnostics medían accepted orders/proxies.
- **Commit:** `7fa47c59d8bbe1df61b598f01875e91b2b51c089`.
- **Validación:** gates exigen recorrido físico real y estabilidad Inactive.

### ISSUE-0003 — Competencia Ambient/Encounter sobre Navigation
- **Estado:** `RESOLVED / P0`.
- **Causa:** varios writers altos implícitos sobre `ActorNavigationController`.
- **Commit:** `7fa47c59d8bbe1df61b598f01875e91b2b51c089`.
- **Resultado:** Behavior decide ownership; Navigation sigue autoridad técnica inferior.

### ISSUE-0004 — Perception dependía demasiado de body-facing de spawn
- **Estado:** `RESOLVED / P1`.
- **Corrección:** F5 usa `CurrentGazeDirection` como forward productivo con fallback explícito.
- **Commit:** `2fc27d946f5a807abd4f046d2dee85331490b7c2`.
- **Validación:** Gaze-centered production perception + LOS regressions PASS.

### ISSUE-0005 — Faltaba autoridad Gaze/Attention
- **Estado:** `RESOLVED / P1`.
- **Corrección:** `ActorGazeController` bounded, sin adquirir targets/navegar/combatir.
- **Commit:** `e1bd7d7ce6d0f0a6885cb23a7047d53d31fd0509`.
- **Validación:** Ambient/Candidate/Encounter/LostContact/Inactive gaze diagnostics PASS.

### ISSUE-0006 — Tracking visual lateral deficiente
- **Estado:** `RESOLVED / P1`.
- **Corrección:** observed-motion tracking F4 + FOV productivo centrado en Current Gaze F5.
- **Commits:** `e72feeb67edfe9b208eefa4d4c6c13f488df62cc`, `2fc27d946f5a807abd4f046d2dee85331490b7c2`.
- **Validación:** lateral target dentro de gaze/FOV, human bounds + occlusion PASS.

### ISSUE-0007 — LostContact no ejecutaba búsqueda real
- **Estado:** `RESOLVED / P1`.
- **Corrección:** Search V1 con LKP/SearchAnchor congelado, navigation, inspect, reacquire/release.
- **Commit:** `7590ec6f868da89a72a5514a85f7c042fb89e36f`.
- **Validación:** Search reacquire/release/no-hidden-transform PASS.

### ISSUE-0009 — Cápsula humana insuficiente para anatomy hit testing
- **Estado:** `RESOLVED / P1`.
- **Corrección:** F7 `humanoid_standard` + `ActorLocomotionCollider` + seis `ActorCombatHitRegion`.
- **Commit:** `96cccbe514177d8eb05d8c5c439909b4657f252e`.
- **Validación:** ruta física real 6/6 regiones + regressions PASS.

### ISSUE-0013 — Falta modo debug Invisible-to-AI
- **Estado:** `RESOLVED / P1`.
- **Corrección:** marker target-side efímero `ActorDebugAiAcquisitionExclusion` expuesto como `Invisible to AI`.
- **Commit:** `321f26d1d3c1e765e19e86ab66f316238734c8fe`.
- **Validación:** OFF → Player, ON → Blue y OFF → Player; sin alterar Perception/FOV/LOS, colliders, combat, input ni persistence.

### ISSUE-0014 — Ping-pong Idle↔Inactive en incapacitados
- **Estado:** `RESOLVED / P0`.
- **Corrección:** incapacidad queda estable Inactive y cancela acquisition/navigation/attack.
- **Commit:** `b42e17c40ad843244fd390c9b0eeb707b6462d31`.
- **Nota:** ISSUE-0020 es distinto: trata memoria/contexto durante KO.

### ISSUE-0015 — Blue→Red era Neutral
- **Estado:** `RESOLVED / P0`.
- **Corrección:** Blue↔Red Hostile; Blue→Player Neutral; Red→Player Hostile; same-team no hostil.
- **Commit:** `b42e17c40ad843244fd390c9b0eeb707b6462d31`.

### ISSUE-0016 — Documentación M41 desactualizada respecto al código
- **Estado:** `RESOLVED / P2`.
- **Corrección:** roadmap/snapshots reconciliados al estado post-Prueba 3.
- **Commit canónico:** `2ddc2ad19680e1f02d1c5d32169230238e6cbfc3`.

### ISSUE-0017 — Fixture M41NpcSandbox mataba target antes de segunda región
- **Estado:** `RESOLVED / P1`.
- **Causa:** fixture asumía supervivencia a headshot con balance letal vigente.
- **Commit:** `e0d5fb9c40fba6b62fe8c1ffa60a24cb9cfeb06f`.
- **Validación:** targets independientes Head/LeftLeg; sandbox diagnostics PASS.

### ISSUE-0018 — Gate Inactive confundía physical collapse con locomoción Behavior
- **Estado:** `RESOLVED / P1`.
- **Causa:** root displacement usado como proxy de locomoción normal.
- **Commit:** `e1bd7d7ce6d0f0a6885cb23a7047d53d31fd0509`.
- **Validación:** ownership/revisions/orders/Ambient travel estables; collapse displacement informativo.

---

## Regla permanente para prompts de Codex

Todo prompt de implementación/revisión debe incluir una instrucción equivalente a:

> Si durante el trabajo detectas un bug, regresión, deuda técnica o comportamiento sospechoso que no estaba registrado, añade o actualiza su entrada en `Docs/Issue_Registry.md` con evidencia y estado correcto. No arregles problemas fuera de alcance por inercia. Si corriges un issue dentro del alcance, no lo borres: márcalo `RESOLVED`, registra commit/validación y conserva el historial.

La severidad/estado no se elevan por intuición. Para evidencia antigua, consultar `Development_Log.md`, Git y diagnostics citados.
