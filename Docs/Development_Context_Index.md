# Old Scars — Development Context Index

Este archivo existe para que un nuevo chat/sesión de desarrollo pueda reconstruir el estado del proyecto desde el repo sin depender de memoria conversacional.

## Orden de lectura recomendado al cambiar de chat/sesión

1. `AGENTS.md` — reglas permanentes de trabajo, Git, validación, alcance y routing ChatGPT/Codex.
2. `Docs/Current_Milestone.md` — estado operativo actual y próximo paso exacto.
3. `Docs/Next_Sprints.md` — cola real de trabajo a corto plazo y secuencia post-M41 ya aprobada.
4. `Docs/Terrain_Chunk_Identity_Stage1.md` — contrato y límites exactos del checkpoint IMPL-0063 Stage 1.
5. `Docs/Issue_Registry.md` — bugs/deudas/sospechas/resoluciones persistentes.
6. `Docs/Implementation_Backlog.md` — mecánicas/mejoras aprobadas para después; no son milestones ni bugs.
7. `Docs/Technical_Architecture.md` y `Docs/DataDriven_JSON_Rules.md` — contratos implementados.
8. `Docs/Test_Log.md` — evidencia de validaciones realmente ejecutadas; IDs inexistentes aquí no cuentan como tests canónicos.
9. `Docs/Project_Roadmap.md` — IDs/estados/dependencias de milestones grandes.
10. `Docs/Development_Log.md` — cronología/evidencia histórica detallada.
11. Documentos M41/NPC (`Prueba_3_Findings.md`, `NPC_AI_Sanitation_Plan.md`, `NPC_Combat_Targeting_Research.md`) sólo cuando el trabajo toque ese bloque histórico/cerrado.

## Qué documento responde qué pregunta

| Pregunta | Fuente principal |
| --- | --- |
| ¿Qué estamos haciendo ahora? | `Current_Milestone.md` |
| ¿Qué hacemos después? | `Next_Sprints.md` |
| ¿Qué evidencia de prueba está registrada realmente? | `Test_Log.md` |
| ¿Qué contrato/límite tiene el Stage 1 actual? | `Terrain_Chunk_Identity_Stage1.md` |
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

## Estado de continuidad al 2026-10-02

### Cierres recientes

- M41 — NPC Combat / AI Foundation: **DONE / ACCEPTED / PUBLISHED**.
- ISSUE-0022 — Loaded Ammo Mass Conservation: **DONE / RESOLVED / PUBLISHED**.
- IMPL-0020 — Carry Weight / Encumbrance compartido: **DONE / ACCEPTED / PUBLISHED** el 2026-10-01.
- IMPL-0066 — Container structural capacity / equipped ergonomics: **IMPLEMENTED / AUTOMATED PASS / MANUAL CORE PASS / TUNING ACCEPTANCE PENDING**. No hay implementación activa aquí; queda únicamente decisión manual comparativa de tuning pequeña/media/grande.

### Scope operativo actual

**IMPL-0063 — Stage 1: audit/integration gate + stable terrain chunk identity**

- Base publicada: `89f2a72c26d30668670bff3b485300eb9d2e0a4a`.
- Estado: **VALIDATED / PUBLISHED — STAGE 1 ONLY**.
- El umbrella IMPL-0063 no está DONE.
- `TerrainChunkKey` separa identidad mundial/contextual de `DeformableTerrainChunkId` local y conserva evidencia baseline/layout versionada.
- El consumer productivo de streaming/persistencia todavía no existe: Stage 1 sólo establece y valida el contrato.
- La key incluye la ventana lógica activa y XYZ local; **no es todavía una coordenada mundial definitiva de mutación persistente**.
- Unity Terrain sigue siendo el path/default release; la foundation volumétrica continúa opt-in para desarrollo.
- Streaming, load/unload, persistencia productiva de mutaciones, geología/cuevas, LOD y optimización posterior siguen fuera de alcance.

**NEXT EXACT STEP:** revisar el checkpoint IMPL-0063 Stage 1. No iniciar Stage 2/streaming/persistencia por inercia. IMPL-0021 permanece detrás de IMPL-0063.

### Validaciones/documentación pendientes que no cambian el scope activo

- IMPL-0066: aceptación de tuning pequeña/media/grande.
- IMPL-0041: implementación publicada; validación manual pendiente.
- IMPL-0042: implementación publicada; validación manual pendiente; ISSUE-0029 registra el header de scope hardcodeado.
- IMPL-0061: hubo un handoff local histórico, pero la implementación **no está presente en `dev`**. Sus supuestos TEST-20260915-005..008 no existen en `Test_Log.md` y no deben tratarse como evidencia canónica ni recrearse retroactivamente.

### Regla para documentos históricos

Cuando un documento histórico diga que IMPL-0020 es el próximo paso, que Climate/Environment no existen, que `world_session_v1` actual es schema 5 o que IMPL-0063 aún no fue autorizado, esa frase está superada por el estado actual. Corregir el documento vivo dentro de su dominio; no reinterpretar el código ni inventar evidencia para reconciliarlo.
