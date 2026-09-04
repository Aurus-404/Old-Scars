# Old Scars — Development Context Index

Este archivo existe para que un nuevo chat/sesión de desarrollo pueda reconstruir el estado del proyecto desde el repo sin depender de memoria conversacional.

## Orden de lectura recomendado al cambiar de chat/sesión

1. `AGENTS.md` — reglas permanentes de trabajo, Git, validación, alcance y routing ChatGPT/Codex.
2. `Docs/Current_Milestone.md` — estado operativo actual y próximo paso exacto.
3. `Docs/Next_Sprints.md` — cola real de trabajo a corto plazo.
4. `Docs/Prueba_3_Findings.md` — evidencia manual integrada más reciente y decisiones derivadas de Prueba 3/3.1/3.2.
5. `Docs/Issue_Registry.md` — bugs/deudas/sospechas/resoluciones persistentes.
6. `Docs/Implementation_Backlog.md` — mecánicas/mejoras menores aprobadas para implementar después; no son milestones ni bugs.
7. El plan específico del bloque activo. Actualmente: `Docs/NPC_AI_Sanitation_Plan.md`.
8. La investigación/decision record específica si existe. Actualmente para combate NPC: `Docs/NPC_Combat_Targeting_Research.md`.
9. `Docs/Technical_Architecture.md` y `Docs/DataDriven_JSON_Rules.md` — contratos ya implementados.
10. `Docs/Development_Log.md` — cronología/evidencia histórica cuando se necesite reconstruir por qué se tomó una decisión.
11. `Docs/Project_Roadmap.md` — IDs/estados/dependencias de milestones grandes. No usarlo como sustituto de `Next_Sprints` ni del Implementation Backlog.

## Qué documento responde qué pregunta

| Pregunta | Fuente principal |
| --- | --- |
| ¿Qué estamos haciendo ahora? | `Current_Milestone.md` |
| ¿Qué hacemos después? | `Next_Sprints.md` |
| ¿Qué mostró la última prueba manual integrada? | `Prueba_3_Findings.md` |
| ¿Qué milestone grande corresponde? | `Project_Roadmap.md` |
| ¿Qué bug/deuda real sigue abierto? | `Issue_Registry.md` |
| ¿Qué mecánica/mejora menor queremos recordar para después? | `Implementation_Backlog.md` |
| ¿Cuál es el plan completo del saneamiento NPC/AI? | `NPC_AI_Sanitation_Plan.md` |
| ¿Qué aprendimos sobre aim/accuracy/targets y por qué cambió Fase 8? | `NPC_Combat_Targeting_Research.md` |
| ¿Cómo está implementado técnicamente el sistema hoy? | `Technical_Architecture.md` + código |
| ¿Qué reglas data-driven/modding son autoridad? | `DataDriven_JSON_Rules.md` |
| ¿Qué ocurrió históricamente y con qué evidencia? | `Development_Log.md` |

## Regla de precedencia

- El código publicado y los diagnostics prueban qué existe técnicamente.
- `Technical_Architecture.md` describe contratos implementados; un research doc o findings doc no convierte una propuesta en implementación.
- `Current_Milestone.md`/`Next_Sprints.md` prevalecen para el trabajo operativo actual cuando el Roadmap conserva wording histórico pendiente de reconciliación.
- `Prueba_3_Findings.md` conserva evidencia manual y decisiones de producto recientes, pero el estado formal de bugs sigue en `Issue_Registry.md`.
- `Issue_Registry.md` prevalece para el estado de bugs.
- `Implementation_Backlog.md` prevalece para mejoras menores aprobadas aún no implementadas.
- El GDD y Mauro conservan autoridad de diseño/producto; una implementación de debug no crea canon de diseño por sí sola.

## Regla de investigación y cuota

Cuando un problema pueda investigarse leyendo el repo/GitHub, la investigación debe hacerse fuera de Codex primero. Codex se usa después para implementar el arreglo acotado y para evidencia que requiera checkout local/Unity/diagnostics/assets. No repetir auditorías exhaustivas en Codex si el repo ya permitió establecer la causa y el prompt puede nombrar el seam concreto.

## Estado de continuidad al 2026-09-03

El bloque activo es `M41 — NPC Combat / AI Foundation after Prueba 3`.

Fases cerradas del saneamiento:

- F2 Behavior ownership + Ambient roaming;
- F3 Gaze/Attention V1;
- F4 tracking visual bounded;
- F5 production perception centrada en current gaze;
- F6 LostContact/Search V1;
- F7 representación humana + hitboxes anatómicos explícitos.

Prueba 3 confirmó gran parte de F2–F7 en ejecución real, pero cambió la prioridad antes de F8A:

1. Player Invisible-to-AI mínimo para pruebas NPC-only limpias;
2. F6 current-vs-last/origin correctness y visuals multi-NPC simultáneas;
3. continuidad de memoria de combate durante KO temporal;
4. minimum real-time KO dwell antes de recovery;
5. Prueba 3.3 1 Blue vs 1 Red limpia;
6. después F8A Aim Bias Evidence.

El plan F8 no se elimina: después siguen Primary Aim Point genérico si la evidencia lo confirma, before/after sin retuning, review pequeño de accuracy y cleanup legacy. Invincible, Observability V2 completa, batería integrada, pruebas Player y cleanup final siguen planificados.

No iniciar por inercia un nuevo stack de accuracy, Behavior Trees, GOAP, Utility AI, memory framework general, weak-point framework, full ballistics ni damage frameworks para máquinas/vehículos sin consumidor real.
