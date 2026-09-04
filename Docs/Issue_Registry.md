# Old Scars — Issue Registry

Registro persistente de bugs, deudas, tooling problems y sospechas técnicas. No sustituye al Roadmap, Development Log ni Implementation Backlog.

Este archivo se mantiene deliberadamente compacto para que pueda leerse en cambios de chat/sesión sin cargar cientos de líneas de evidencia repetida. La evidencia histórica detallada permanece en `Development_Log.md`, Git/commits, diagnostics y documentos de prueba asociados.

## Regla de uso

- Problema nuevo real/sospechado → registrar aquí.
- Fuera de alcance → registrar, no arreglar por inercia.
- `RESOLVED` nunca se borra: conserva causa, commit y validación resumida.
- `SUSPECTED` = síntoma/evidencia insuficiente para afirmar causa o incluso defecto exacto.
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
- **Hipótesis fuerte:** firearm aim sigue usando `ActorLocomotionCollider.bounds.center`, más bajo que center-mass humano; spread radial normal puede amplificarlo. No se ha demostrado todavía que sea la causa final.
- **Plan:** F8A instrumentar aim source/point, spread, direction, collider/region/miss; F8B/C sólo cambiar a Primary Aim Point genérico si evidencia lo justifica.
- **No hacer:** retunear spread/damage/anatomy por intuición.

### ISSUE-0010 — Observabilidad global insuficiente para peleas multi-NPC
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P1 / ORANGE`
- **Origen:** Prueba 2; reconfirmado Prueba 3.
- **Síntoma:** sólo un NPC seleccionado recibe world visuals útiles; comparar ambos lados de un encounter exige ciclar F6.
- **Plan:** Prueba 3 Correction Pass B adelanta un slice mínimo: Gaze/FOV/LOS simultáneos para varios/todos; selección sólo controla inspector profundo. F10 completa targeting/shot observability.
- **Validación requerida:** pelea multi-NPC comprensible sin cambiar selección constantemente.

### ISSUE-0011 — Inspector F6 demasiado dependiente de selección
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P2 / YELLOW`
- **Origen:** Prueba 2/3.
- **Síntoma:** estados simultáneos son difíciles de comparar.
- **Plan:** resolver junto a ISSUE-0010 manteniendo inspector seleccionado + overlay global separado.

### ISSUE-0012 — Falta modo debug Invincible
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P2 / YELLOW`
- **Origen:** post-Prueba 2.
- **Plan:** F9; pipeline real detection→shot→region→wounds/trauma continúa, pero debug puede bloquear terminal Dead.
- **Gate:** OFF = gameplay normal.

### ISSUE-0013 — Falta modo debug Invisible-to-AI
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P1 / ORANGE`
- **Origen:** post-Prueba 2; necesidad demostrada en Prueba 3.1.
- **Síntoma:** Red puede abandonar un supuesto Blue↔Red 1v1 y adquirir Player.
- **Plan:** adelantado a Prueba 3 Correction Pass A. Player sigue físico/interactivo pero queda fuera de candidate/acquisition con toggle ON; no apagar GameObject/collider ni Perception global.
- **Gate:** Player puede observar de cerca sin recognition/targeting; OFF restaura conducta normal.

### ISSUE-0019 — F6 presenta snapshots históricos como percepción actual
- **Tipo/estado/severidad:** `TOOLING` · `CONFIRMED` · `P1 / ORANGE`
- **Origen:** Prueba 3/3.1/3.2 + revisión de repo, 2026-09-03.
- **Síntoma:** FOV/LOS puede quedar atrás del NPC, parecer salir del piso/desaparecer; Dead/Inactive puede seguir mostrando `Perceived` histórico.
- **Causa:** tooling consume `LastPerception`/`LastAcquisitionPerception` + `ObserverOrigin` snapshot y no diferencia claramente CURRENT vs LAST. No implica que perception productiva vea realmente desde el origen viejo.
- **Plan:** Correction Pass B: current Gaze/FOV desde eye/origin actual; last evidence rotulada `LAST`; Dead/Inactive no afirma current perception; resolver junto a multi-NPC sin duplicar raycasts.

### ISSUE-0020 — Incapacidad temporal borra contexto de enemigo y reinicia el combate
- **Tipo/estado/severidad:** `BUG` · `CONFIRMED` · `P1 / ORANGE`
- **Origen:** Prueba 3.1/3.2 + código, 2026-09-03.
- **Síntoma:** `Fight → KO → rival Ambient → KO recovery → rediscovery → encounter nuevo`.
- **Causa:** acquisition deja de aceptar como current threat a quien no puede realizar active actions; `EnterInactive/ReleaseEncounter` limpian threat/contexto. Se mezcla `no es amenaza activa ahora` con `ya no recuerdo al enemigo`.
- **Decisión de producto:** atacante deja de golpear al KO, pero ambos conservan identidad/contexto mínimo del enemigo reciente. Memory no otorga posición actual; Perception/LKP/Search siguen siendo autoridad espacial. Death sigue terminal.
- **Plan:** `IMPL-0014`, Correction Pass C; seam mínimo dentro de autoridades existentes, sin MemorySystem/blackboard/planner general.
- **Gate:** después de recovery el conflicto puede reanudarse sin redescubrimiento artificial y sin wallhack.

### ISSUE-0021 — Knockout/Unconscious sin minimum real-time dwell
- **Tipo/estado/severidad:** `DESIGN_DEBT` · `CONFIRMED` · `P1 / ORANGE`
- **Origen:** Prueba 3.1/3.2 + revisión `ActorConditionComponent`/`WorldClock`, 2026-09-03.
- **Gap confirmado:** no existe garantía explícita de permanencia mínima en KO/unconscious; recovery physiology corre sobre world time acelerado.
- **Plan:** `IMPL-0015`, Correction Pass D: mínimo configurable de tiempo real; después del mínimo physiology/thresholds vigentes siguen decidiendo si puede despertar. No crear otro reloj global.
- **Gate:** no recovery antes del mínimo aunque WorldClock avance; cumplir el mínimo no fuerza wake-up si physiology todavía no permite recuperación.

---

## Issues resueltos / historial

### ISSUE-0001 — Blue/Red no realizaban roaming efectivo Idle
- **Estado:** `RESOLVED / P0`.
- **Causa:** Encounter cancelaba navegación Ambient al no haber threat.
- **Corrección:** `ActorBehaviorController` único owner alto `Ambient/Encounter/Search/Inactive`.
- **Commit:** `7fa47c59d8bbe1df61b598f01875e91b2b51c089`.
- **Validación:** desplazamiento físico individual White/Blue/Red + ownership interruption/resume PASS.

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
- **Validación:** ruta física real 6/6, capsule bypass, fallback transition y regressions PASS.

### ISSUE-0014 — Ping-pong Idle↔Inactive en incapacitados
- **Estado:** `RESOLVED / P0`.
- **Corrección:** incapacidad queda estable Inactive y cancela acquisition/navigation/attack.
- **Commit:** `b42e17c40ad843244fd390c9b0eeb707b6462d31`.
- **Nota:** ISSUE-0020 es distinto: trata memoria/contexto al KO, no ping-pong state.

### ISSUE-0015 — Blue→Red era Neutral
- **Estado:** `RESOLVED / P0`.
- **Corrección:** Blue↔Red Hostile; Blue→Player Neutral; Red→Player Hostile; same-team no hostil.
- **Commit:** `b42e17c40ad843244fd390c9b0eeb707b6462d31`.

### ISSUE-0016 — Documentación M41 desactualizada respecto al código
- **Estado:** `RESOLVED / P2`.
- **Causa:** roadmap/snapshots seguían describiendo M41.4 como no iniciado después de implementación, sanitation y pruebas.
- **Corrección:** `Current_Milestone`, `Next_Sprints`, `NPC_AI_Sanitation_Plan`, `Development_Context_Index`, `Prueba_3_Findings` y `Project_Roadmap` reconciliados al estado post-Prueba 3.
- **Commit de cierre canónico:** `2ddc2ad19680e1f02d1c5d32169230238e6cbfc3` (`docs(roadmap): reconcile M41 after Prueba 3`).
- **Validación:** Roadmap ya declara M41.4 baseline implementado, post-playtest sanitation activo y preserva M42–M55/open-world path sin autorización automática.

### ISSUE-0017 — Fixture M41NpcSandbox mataba target antes de segunda región
- **Estado:** `RESOLVED / P1`.
- **Causa:** fixture asumía supervivencia a headshot con balance letal vigente.
- **Commit:** `e0d5fb9c40fba6b62fe8c1ffa60a24cb9cfeb06f`.
- **Validación:** targets independientes para Head/LeftLeg; sandbox diagnostics PASS.

### ISSUE-0018 — Gate Inactive confundía physical collapse con locomoción Behavior
- **Estado:** `RESOLVED / P1`.
- **Causa:** root displacement usado como proxy de locomoción normal.
- **Commit:** `e1bd7d7ce6d0f0a6885cb23a7047d53d31fd0509`.
- **Validación:** ownership/revisions/orders/Ambient travel estables; collapse displacement sólo informativo.

---

## Regla permanente para prompts de Codex

Todo prompt de implementación/revisión debe incluir una instrucción equivalente a:

> Si durante el trabajo detectas un bug, regresión, deuda técnica o comportamiento sospechoso que no estaba registrado, añade o actualiza su entrada en `Docs/Issue_Registry.md` con evidencia y estado correcto. No arregles problemas fuera de alcance por inercia. Si corriges un issue dentro del alcance, no lo borres: márcalo `RESOLVED`, registra commit/validación y conserva el historial.

La severidad/estado no se elevan por intuición. Para detalles antiguos que ya no entren aquí, consultar `Development_Log.md`, Git y el diagnostic/commit citado.
