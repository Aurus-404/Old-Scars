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

### ISSUE-0008 — Posible sesgo de impactos hacia piernas/pies
- **Tipo/estado/severidad:** `BUG` · `SUSPECTED` · `P1 / ORANGE`
- **Origen:** Prueba 2, 2026-09-01.
- **Síntoma:** muestra manual inicial concentrada en piernas/pies.
- **Evidencia actual:** F7 agregó anatomía física explícita y diagnostic 6/6; Prueba 3 mostró múltiples Torso/Arm/Leg y no reprodujo cualitativamente el patrón extremo, pero todavía no existe muestra NPC estadística limpia.
- **Hipótesis fuerte:** firearm aim sigue usando `ActorLocomotionCollider.bounds.center`, más bajo que center-mass humano; spread radial normal puede amplificarlo.
- **Plan:** después de Correction Pass + Prueba 3.3, F8A instrumenta aim source/point, spread, direction, collider/region/miss. F8B/C sólo cambian a Primary Aim Point genérico si evidencia lo justifica.
- **No hacer:** retunear spread/damage/anatomy por intuición.

### ISSUE-0010 — Observabilidad global insuficiente para peleas multi-NPC
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P1 / ORANGE`
- **Origen:** Prueba 2; reconfirmado Prueba 3.
- **Síntoma:** sólo un NPC seleccionado recibe world visuals útiles; comparar ambos lados exige ciclar F6.
- **Estado actual:** existe candidato local no publicado de Correction Pass B con PASS automático; falta aceptación visual/manual y commit.
- **Plan:** cerrar junto a ISSUE-0011/0019; selección sólo controla inspector profundo y overlay global consume datos read-only de producción.

### ISSUE-0011 — Inspector F6 demasiado dependiente de selección
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P2 / YELLOW`
- **Origen:** Prueba 2/3.
- **Síntoma:** estados simultáneos son difíciles de comparar.
- **Estado/plan:** mismo candidato local de F6; resolver sólo después de aceptación visual y publicación.

### ISSUE-0012 — Falta modo debug Invincible
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P2 / YELLOW`
- **Origen:** post-Prueba 2.
- **Plan:** después de estabilizar KO y F8 targeting. Pipeline real detection→shot→region→wounds/trauma/bleeding/condition continúa, pero QA puede bloquear coherentemente terminal Dead. OFF = gameplay normal.
- **Riesgo conocido:** no basta con saltar `ProcessDeath`; `IsDead`, lifecycle y fatal blood loss deben seguir consistentes.

### ISSUE-0019 — F6 presenta snapshots históricos como percepción actual
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P1 / ORANGE`
- **Origen:** Prueba 3/3.1/3.2 + revisión de repo, 2026-09-03.
- **Síntoma:** FOV/LOS puede quedar atrás del NPC, parecer salir del piso/desaparecer; Dead/Inactive puede seguir mostrando `Perceived` histórico.
- **Causa publicada:** tooling consume `LastPerception`/`LastAcquisitionPerception` + `ObserverOrigin` snapshot y no diferencia claramente CURRENT vs LAST. No implica que perception productiva vea desde el origen viejo.
- **Estado local:** candidato F6 no publicado ya separa CURRENT/LAST y multi-NPC, pero requiere revisión de coherencia temporal completa. En particular, evidencia LAST no debe mezclar un `ObserverOrigin` histórico con geometría actual de un blocker móvil.
- **Plan:** cerrar el candidato local con aceptación manual; no duplicar Perception/raycasts.

### ISSUE-0020 — Incapacidad temporal borra contexto de enemigo y reinicia el combate
- **Tipo/estado/severidad:** `BUG` · `CONFIRMED` · `P1 / ORANGE`
- **Origen:** Prueba 3.1/3.2 + código, 2026-09-03.
- **Síntoma:** `Fight → KO → rival Ambient → KO recovery → rediscovery → encounter nuevo`.
- **Causa:** acquisition deja de aceptar como current threat a quien no puede realizar active actions; `EnterInactive/ReleaseEncounter` limpian threat/contexto. Se mezcla `no es amenaza activa ahora` con `ya no recuerdo a este enemigo`.
- **Decisión de producto:** atacante deja de golpear al KO, pero ambos conservan identidad/contexto mínimo del enemigo reciente. Memory no otorga posición actual; Perception/LKP/Search siguen siendo autoridad espacial. Death terminal.
- **Plan operativo:** ejecutar DESPUÉS de ISSUE-0021 para estabilizar primero la transición funcional de Unconscious. El recuerdo no debe equivaler a `Threat != null` ni bloquear por sí solo self-treatment/AmbientTopOff.
- **No hacer:** MemorySystem/blackboard/planner general.

### ISSUE-0021 — Knockout/Unconscious sin minimum real-time dwell
- **Tipo/estado/severidad:** `DESIGN_DEBT` · `CONFIRMED` · `P1 / ORANGE`
- **Origen:** Prueba 3.1/3.2 + revisión `ActorConditionComponent`/`WorldClock`, 2026-09-03.
- **Gap confirmado:** no existe garantía explícita de permanencia mínima en `Unconscious`; recovery physiology corre sobre world time acelerado.
- **Plan operativo:** próximo después de cerrar F6 local y ANTES de ISSUE-0020. Mínimo configurable de tiempo real mientras el actor realmente está `Unconscious`; después del mínimo physiology/thresholds/hysteresis siguen decidiendo si puede despertar.
- **Límites:** no extender automáticamente a toda `Incapacitated`; no crear otro reloj global; cumplir el mínimo no fuerza wake-up; save/load no debe permitir bypass accidental.

### ISSUE-0022 — Loaded ammo desaparece del cálculo de carry mass
- **Tipo/estado/severidad:** `BUG` · `CONFIRMED` · `P1 / ORANGE`
- **Origen:** auditoría Carry Weight + revisión de repo, 2026-09-06.
- **Síntoma/causa:** `WeaponCombatService` consume munición owned del Inventory al recargar y la firearm conserva `LoadedAmmoProfileId + LoadedRounds` como estado interno. `ItemWeightResolver` suma item definitions, cantidades y owned-storage subtrees, pero no suma `LoadedRounds`. Por lo tanto recargar puede reducir artificialmente `CurrentWeightKg`; disparar puede no reducir masa desde la representación correcta.
- **Impacto:** hoy es una inconsistencia física; con Encumbrance podría cambiar locomoción artificialmente (por ejemplo, recargar cerca del 100% podría devolver movimiento sin descargar masa real).
- **Riesgo de solución:** `LoadedAmmoProfileId` no necesariamente identifica una única `ItemDefinition` en presencia de mods; no elegir arbitrariamente “el primer item” compatible.
- **Plan:** resolver después de cerrar M41 y ANTES de `IMPL-0020` Carry Weight / Encumbrance. Validar reload parcial/completo, fire, rollback, equipment/storage y save/load.
- **No hacer:** cambiar política de reload NPC, introducir cargadores físicos o weapon framework nuevo por este bug.

---

## Issues resueltos / historial

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
