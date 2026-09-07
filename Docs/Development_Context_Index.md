# Old Scars — Development Context Index

Este archivo existe para que un nuevo chat/sesión de desarrollo pueda reconstruir el estado del proyecto desde el repo sin depender de memoria conversacional.

## Orden de lectura recomendado al cambiar de chat/sesión

1. `AGENTS.md` — reglas permanentes de trabajo, Git, validación, alcance y routing ChatGPT/Codex.
2. `Docs/Current_Milestone.md` — estado operativo actual y próximo paso exacto.
3. `Docs/Next_Sprints.md` — cola real de trabajo a corto plazo y secuencia post-M41 ya aprobada.
4. `Docs/Prueba_3_Findings.md` — evidencia manual integrada más reciente de Prueba 3/3.1/3.2.
5. `Docs/Issue_Registry.md` — bugs/deudas/sospechas/resoluciones persistentes.
6. `Docs/Implementation_Backlog.md` — mecánicas/mejoras aprobadas para después; no son milestones ni bugs.
7. `Docs/NPC_AI_Sanitation_Plan.md` — plan completo del bloque activo M41.
8. `Docs/NPC_Combat_Targeting_Research.md` — investigación/decision record de aim/accuracy.
9. `Docs/Technical_Architecture.md` y `Docs/DataDriven_JSON_Rules.md` — contratos implementados.
10. `Docs/Development_Log.md` — cronología/evidencia histórica.
11. `Docs/Project_Roadmap.md` — IDs/estados/dependencias de milestones grandes.

## Qué documento responde qué pregunta

| Pregunta | Fuente principal |
| --- | --- |
| ¿Qué estamos haciendo ahora? | `Current_Milestone.md` |
| ¿Qué hacemos después? | `Next_Sprints.md` |
| ¿Qué mostró la última prueba manual integrada? | `Prueba_3_Findings.md` |
| ¿Qué milestone grande corresponde? | `Project_Roadmap.md` |
| ¿Qué bug/deuda real sigue abierto? | `Issue_Registry.md` |
| ¿Qué mecánica/mejora aprobada queremos recordar para después? | `Implementation_Backlog.md` |
| ¿Cuál es el plan completo del saneamiento NPC/AI? | `NPC_AI_Sanitation_Plan.md` |
| ¿Qué aprendimos sobre aim/accuracy/targets? | `NPC_Combat_Targeting_Research.md` |
| ¿Cómo está implementado técnicamente el sistema hoy? | `Technical_Architecture.md` + código |
| ¿Qué reglas data-driven/modding son autoridad? | `DataDriven_JSON_Rules.md` |
| ¿Qué ocurrió históricamente y con qué evidencia? | `Development_Log.md` |

## Regla de precedencia

- El código publicado y diagnostics prueban qué existe técnicamente.
- Un working tree local no publicado puede ser una implementación candidata, pero no convierte un feature en DONE.
- `Technical_Architecture.md` describe contratos implementados; research/findings no convierten propuestas en implementación.
- `Current_Milestone.md`/`Next_Sprints.md` prevalecen para el orden operativo actual cuando otros documentos conservan wording histórico.
- `Prueba_3_Findings.md` conserva evidencia manual, no sustituye `Issue_Registry.md`.
- `Issue_Registry.md` prevalece para bugs/deudas confirmadas o sospechadas.
- `Implementation_Backlog.md` prevalece para mejoras aprobadas aún no implementadas.
- Mauro/GDD conservan autoridad de producto.

## Regla de investigación y cuota

Cuando un problema pueda investigarse leyendo el repo/GitHub, hacerlo fuera de Codex primero. Codex se usa para implementación, Unity/local diagnostics, assets y validación que realmente requiere el checkout.

Para auditorías sistémicas amplias, Astra puede usarse como investigador/arquitecto cuando aporte valor; no es el modelo por defecto para slices ya acotados.

No repetir auditorías exhaustivas si el repo ya estableció el seam y el próximo trabajo sólo requiere implementación/validación.

## Estado de continuidad al 2026-09-07

Bloque activo:

`M41 — NPC Combat / AI Foundation`

Foundation cerrada:

- F2 Behavior ownership + Ambient roaming;
- F3 Gaze/Attention V1;
- F4 tracking visual bounded;
- F5 production perception centrada en Current Gaze;
- F6 LostContact/Search V1;
- F7 representación humana + hitboxes anatómicos explícitos;
- Player Debug Invisible-to-AI.

Capacidades recientes también cerradas:

- Timed Bandaging V1 + NPC self-treatment;
- Blood Trails V1/V1.1;
- NPC Opportunistic Reload (`4b90b9f4c8f5ae3c896d8b1fc21d688095172b0a`).

### P1/F6 publicado

P1/F6 / Correction Pass B: **DONE / ACCEPTED / PUBLISHED** el 2026-09-06 en `5aac763c14c399bfe09a3e925c50698658ad2716`. CURRENT Gaze/FOV multi-NPC desde origen productivo actual; selección sólo para inspector detallado. LAST usa ObserverOrigin → ObservedPosition históricos, visual secundario y toggle independiente, sin reconstruir blocker hit con collider actual. Sin evidencia: LAST: No evidence. Dead/Inactive sin CURRENT engañoso. Sin nueva Perception/raycasts productivos.

F6 Observability, Gaze/Perception, LostContact/Search y compile Runtime/Editor PASS previos; aceptación visual manual final confirmada por Mauro. IMPL-0010 minimum slice completado; F10 completo pendiente. Posible desajuste eye origin/representación humana separado en ISSUE-0023, sin resolver.

### Secuencia operativa aprobada para cerrar M41

P2 — Minimum real-time Unconscious dwell: **DONE / PUBLISHED**, `9ca0335cdc8b85bd49d20ddbe97ad814f44c8578`. Core `5 s` inicial de prueba; restante durable en Current Slice v1, sin progreso offline y legacy seguro. P2 y regresiones proporcionales PASS; siguiente trabajo P3, no iniciado. La aceptación manual integrada de M41 sigue pendiente.

1. F6 / Correction Pass B cerrado, aceptado y publicado;
2. minimum real-time KO dwell cerrado y publicado;
3. P3 — KO / combat-memory continuity, próximo;
4. Prueba 3.3 1 Blue vs 1 Red limpia;
5. F8A Aim Bias Evidence;
6. F8B/C y F8D sólo según evidencia;
7. Player Debug Invincible;
8. completar Observability V2/F10;
9. legacy migration + QA integrada + aceptación manual + cierre M41.

Cambio deliberado respecto de wording anterior: KO dwell se ejecuta antes de KO memory para estabilizar primero la transición funcional que la memoria debe soportar.

### Después de M41

Orden sistémico aprobado:

- Equipment visuals humanoides cuando convenga para lectura visual;
- corregir `ISSUE-0022` loaded ammo mass;
- implementar `IMPL-0020` Carry Weight / Encumbrance compartido Player/NPC;
- implementar `IMPL-0021` Localized Limb Impairment después de Encumbrance.

Equipment visuals y loaded ammo mass pueden intercambiar posición. Loaded ammo mass sí debe resolverse antes de Encumbrance.

No introducir Encumbrance entre F8A y sus comparaciones: velocidad NPC participa en las condiciones de accuracy y contaminaría la medición.

## Contrato futuro de Carry Weight ya aprobado

- Carry Capacity no es storage capacity.
- `0..75%` sin penalización.
- `>75%..100%` penalización progresiva.
- `100%` todavía móvil.
- `>100%` traslación cero.
- Inventory/transfer/drop/equipment/use/reload/treatment siguen operativos según sus propias autoridades.
- Player/NPC comparten el contrato.
- `Overloaded` no significa `Incapacitated`.
- restore sobrecargado conserva items.

No implementar todavía Strength, backpack capacity modifiers ni Limb HP.

## Riesgos/relaciones que deben recordarse

- KO memory no debe mantener al KO como `Threat` activo ni bloquear por sí solo self-treatment/ambient reload.
- Invincible debe conservar heridas/bleeding/pain/trauma/KO reales y bloquear coherentemente Dead; no basta con saltar `ProcessDeath`.
- Search debe distinguir path/order válido de futura inmovilidad por Encumbrance.
- `IsSprinting` futuro debe representar sprint efectivo, no sólo Shift solicitado cuando traslación está bloqueada.
- Loaded ammo actualmente puede desaparecer del cálculo de masa al convertirse en `LoadedRounds`; corregir antes de que peso gobierne locomoción.

No iniciar por inercia Behavior Trees, GOAP/Utility AI, memory framework general, weak-point framework, full ballistics, cover/squad/hearing/schedules, Strength/stats ni weapon viability/fallback sin una tarea propia y evidencia real.
