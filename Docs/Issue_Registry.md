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

### ISSUE-0012 — Falta modo debug Invincible
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P2 / YELLOW`
- **Origen:** post-Prueba 2.
- **Plan:** después de estabilizar KO y F8 targeting. Pipeline real detection→shot→region→wounds/trauma/bleeding/condition continúa, pero QA puede bloquear coherentemente terminal Dead. OFF = gameplay normal.
- **Riesgo conocido:** no basta con saltar `ProcessDeath`; `IsDead`, lifecycle y fatal blood loss deben seguir consistentes.

### ISSUE-0022 — Loaded ammo desaparece del cálculo de carry mass
- **Tipo/estado/severidad:** `BUG` · `CONFIRMED` · `P1 / ORANGE`
- **Origen:** auditoría Carry Weight + revisión de repo, 2026-09-06.
- **Síntoma/causa:** `WeaponCombatService` consume munición owned del Inventory al recargar y la firearm conserva `LoadedAmmoProfileId + LoadedRounds` como estado interno. `ItemWeightResolver` suma item definitions, cantidades y owned-storage subtrees, pero no suma `LoadedRounds`. Por lo tanto recargar puede reducir artificialmente `CurrentWeightKg`; disparar puede no reducir masa desde la representación correcta.
- **Impacto:** hoy es una inconsistencia física; con Encumbrance podría cambiar locomoción artificialmente (por ejemplo, recargar cerca del 100% podría devolver movimiento sin descargar masa real).
- **Riesgo de solución:** `LoadedAmmoProfileId` no necesariamente identifica una única `ItemDefinition` en presencia de mods; no elegir arbitrariamente “el primer item” compatible.
- **Plan:** resolver después de cerrar M41 y ANTES de `IMPL-0020` Carry Weight / Encumbrance. Validar reload parcial/completo, fire, rollback, equipment/storage y save/load.
- **No hacer:** cambiar política de reload NPC, introducir cargadores físicos o weapon framework nuevo por este bug.

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

---

## Issues resueltos / historial

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
- **Siguiente:** P4 Prueba 3.3, no iniciado; aceptación manual integrada de M41 pendiente.

### ISSUE-0021 — Knockout/Unconscious sin minimum real-time dwell
- **Tipo/estado/severidad:** `DESIGN_DEBT` · `RESOLVED` · `P1 / ORANGE`
- **Origen/causa:** Prueba 3.1/3.2 + revisión Condition/WorldClock, 2026-09-03; recovery fisiológico acelerado carecía de un mínimo real explícito.
- **Corrección/publicación:** P2, `9ca0335cdc8b85bd49d20ddbe97ad814f44c8578`, 2026-09-07. Gate por episodio en Condition compartida Player/NPC; Core `5 s` inicial de prueba, no balance final. No extiende Incapacitated ni fuerza wake-up; physiology y Death continúan.
- **Persistencia:** Current Slice v1 guarda restante/continuidad; offline no consume el mínimo. Legacy que deriva Unconscious recibe mínimo completo, sin migración de schema.
- **Validación:** P2 x1 `5.001 s`, x100 `5.002 s`, configuración `0.3 s`, sesión Play nueva con `4.000 s` preservados después de más de `6 s` offline, invalid preflight/rollback exacto; Consciousness, Collapse, M39, M38, Human Encounter, Behavior ownership y Timed Bandaging/NPC Self-Treatment PASS.
- **Continuidad posterior:** P3 / `ISSUE-0020` DONE/PUBLISHED; siguiente P4 Prueba 3.3, no iniciado.

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
- **Validación:** F6 Observability, Gaze/Perception, LostContact/Search y compile Runtime/Editor PASS previos; aceptación visual manual final confirmada por Mauro el 2026-09-06. Correction Pass B cerrado; F10 completo pendiente.

### ISSUE-0011 — Inspector F6 demasiado dependiente de selección
- **Tipo/estado/severidad:** `TOOLING` · `RESOLVED` · `P2 / YELLOW`
- **Origen:** Prueba 2/3.
- **Síntoma:** estados simultáneos son difíciles de comparar.
- **Corrección/publicación:** `5aac763c14c399bfe09a3e925c50698658ad2716`; CURRENT multi-NPC independiente del inspector, LAST histórico separado sin geometría actual del blocker y Dead/Inactive sin CURRENT engañoso.
- **Validación:** F6 Observability, Gaze/Perception, LostContact/Search y compile Runtime/Editor PASS previos; aceptación visual manual final confirmada por Mauro el 2026-09-06. Correction Pass B cerrado; F10 completo pendiente.

### ISSUE-0019 — F6 presenta snapshots históricos como percepción actual
- **Tipo/estado/severidad:** `TOOLING` · `RESOLVED` · `P1 / ORANGE`
- **Origen:** Prueba 3/3.1/3.2 + revisión de repo, 2026-09-03.
- **Síntoma:** FOV/LOS puede quedar atrás del NPC, parecer salir del piso/desaparecer; Dead/Inactive puede seguir mostrando `Perceived` histórico.
- **Causa publicada:** tooling consume `LastPerception`/`LastAcquisitionPerception` + `ObserverOrigin` snapshot y no diferencia claramente CURRENT vs LAST. No implica que perception productiva vea desde el origen viejo.
- **Corrección/publicación:** `5aac763c14c399bfe09a3e925c50698658ad2716`; CURRENT multi-NPC independiente del inspector, LAST histórico separado sin geometría actual del blocker y Dead/Inactive sin CURRENT engañoso.
- **Validación:** F6 Observability, Gaze/Perception, LostContact/Search y compile Runtime/Editor PASS previos; aceptación visual manual final confirmada por Mauro el 2026-09-06. Correction Pass B cerrado; F10 completo pendiente.

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
