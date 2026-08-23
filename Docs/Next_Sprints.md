# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximo Trabajo

### 1. Sin milestone de implementación activo

Estado: `WORLD SESSION + PERSISTENCE V1 / NEW GAME SAVE-LOAD APPLICATION SHELL CLOSED`.

M41.1 está `DONE — HUMAN ENCOUNTER AI V1 VALIDATED`, con validation `AUTOMATED + MANUAL UNITY PASSED`; `AI Ready` está `APPROVED`. El hardening posterior compactó el workflow y sus skills, y confirmó una consulta MCP real de solo lectura (`editor_status`) contra el Editor del worktree. Unity MCP queda aceptado provisionalmente para trabajo real; `com.unity.pipeline` se conserva sólo porque ese bridge técnico lo requiere. Unity CLI global es opcional y no forma parte de los requisitos de Old Scars.

La dirección [Open World Architecture](Open_World_Architecture.md) está `APPROVED DESIGN DIRECTION — NOT IMPLEMENTED`. Sus primeras dos foundations quedaron:

`ID TBD — Minimum Content Source Identity & Provenance Foundation`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — World Identity, Topology & Determinism Foundation`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

`ID TBD — World Session + Persistence V1 / New Game Save-Load Application Shell`

Estado: `VALIDATED — APPLICATION SHELL COMPLETE`.

La shell implementada posee session lifecycle único, `world_session_v1` hermano sobre M37, bootstrap temporal de un sector, save catalog, Main Menu, World Runtime placeholder e in-game Save/Return. `current_slice_v1` permanece intacto y provenance se guarda sólo como evidencia.

El próximo coding unit candidato es `ID TBD — Macro World Plan V1`, con estado `PLANNED — NOT AUTHORIZED`. Debe producir global truth determinista anterior a local realization y fijar únicamente el alcance macro expresamente autorizado. No autoriza terrain, materialización, history, sector transitions, generation compatibility ni gameplay world simulation.

Fuera de alcance mientras no exista autorización específica:

- cualquier implementación open-world posterior a la application shell sin autorización del siguiente unit;
- M42.0 u otro milestone jugable;
- cambios de gameplay, content contracts o persistencia fuera del nuevo payload acotado ya cerrado;
- reabrir la arquitectura M41.1 validada.

M42.0 permanece planificado, pero su secuencia requiere rebaseline y ya no constituye el siguiente trabajo automático. Todo trabajo nuevo requiere autorización explícita.

## Connected First Playable

El Connected First Playable es la prueba integrada objetivo después de las foundations open-world. Debe demostrar A→B→A, continuidad cross-sector, mutaciones persistentes, save, full process exit y fresh load usando M32–M41.1. No está iniciado, no es la vertical slice audiovisual final y no adelanta M45.1.

## Modding Y Provenance

La Global Content ID Foundation y la Minimum Content Source Identity & Provenance Foundation están validadas. Cada source requiere manifest `source_id`/`namespace`/`version`; ownership de declaraciones, orden estable y SHA-256 de recognized inputs están implementados sobre el pipeline Core/mod existente.

`Provenance` prueba qué fuentes/inputs estuvieron presentes. `Generation compatibility` continúa no implementada y será responsable de decidir si inputs semánticos siguen siendo compatibles con un mundo; no se infiere desde igualdad/diferencia del fingerprint.

Dependencies, overrides/patches y compatibilidad de producción permanecen en alcance posterior M50.0. La nueva foundation no sustituye M50.0 ni lo marca iniciado.

## No Iniciar Todavía

- Macro World Plan V1, otros coding units open-world o M42.0 sin autorización específica;
- nuevas ampliaciones OnGUI sin milestone autorizado;
- UI final;
- condition, repair o crafting;
- actores o mundo a escala fuera de las foundations implementadas;
- facciones amplias;
- macro generation, geography, sectors jugables, transición, world history o gameplay world persistence antes de sus units autorizadas;
- producción masiva de contenido.
