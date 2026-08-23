# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximo Trabajo

### 1. Sin milestone de implementación activo

Estado: `MINIMUM CONTENT SOURCE IDENTITY & PROVENANCE FOUNDATION CLOSED`.

M41.1 está `DONE — HUMAN ENCOUNTER AI V1 VALIDATED`, con validation `AUTOMATED + MANUAL UNITY PASSED`; `AI Ready` está `APPROVED`. El hardening posterior compactó el workflow y sus skills, y confirmó una consulta MCP real de solo lectura (`editor_status`) contra el Editor del worktree. Unity MCP queda aceptado provisionalmente para trabajo real; `com.unity.pipeline` se conserva sólo porque ese bridge técnico lo requiere. Unity CLI global es opcional y no forma parte de los requisitos de Old Scars.

La dirección [Open World Architecture](Open_World_Architecture.md) está `APPROVED DESIGN DIRECTION — NOT IMPLEMENTED`. Su primera foundation quedó:

`ID TBD — Minimum Content Source Identity & Provenance Foundation`

Estado: `VALIDATED — FOUNDATION COMPLETE`.

El próximo coding unit candidato es `ID TBD — World Identity, Topology & Determinism Foundation`, con estado `PLANNED — NOT AUTHORIZED`. Requiere autorización específica; este cierre no lo inicia.

Fuera de alcance mientras no exista autorización específica:

- cualquier implementación open-world posterior a provenance;
- M42.0 u otro milestone jugable;
- cambios de gameplay, datos o persistencia;
- reabrir la arquitectura M41.1 validada.

M42.0 permanece planificado, pero su secuencia requiere rebaseline y ya no constituye el siguiente trabajo automático. Todo trabajo nuevo requiere autorización explícita.

## Connected First Playable

El Connected First Playable es la prueba integrada objetivo después de las foundations open-world. Debe demostrar A→B→A, continuidad cross-sector, mutaciones persistentes, save, full process exit y fresh load usando M32–M41.1. No está iniciado, no es la vertical slice audiovisual final y no adelanta M45.1.

## Modding Y Provenance

La Global Content ID Foundation y la Minimum Content Source Identity & Provenance Foundation están validadas. Cada source requiere manifest `source_id`/`namespace`/`version`; ownership de declaraciones, orden estable y SHA-256 de recognized inputs están implementados sobre el pipeline Core/mod existente.

`Provenance` prueba qué fuentes/inputs estuvieron presentes. `Generation compatibility` continúa no implementada y será responsable de decidir si inputs semánticos siguen siendo compatibles con un mundo; no se infiere desde igualdad/diferencia del fingerprint.

Dependencies, overrides/patches y compatibilidad de producción permanecen en alcance posterior M50.0. La nueva foundation no sustituye M50.0 ni lo marca iniciado.

## No Iniciar Todavía

- World Identity/Topology/Determinism, otros coding units open-world o M42.0 sin autorización específica;
- nuevas ampliaciones OnGUI sin milestone autorizado;
- UI final;
- condition, repair o crafting;
- actores o mundo a escala fuera de las foundations implementadas;
- facciones amplias;
- generación, sectores, transición, world history o world persistence antes de sus foundations autorizadas;
- producción masiva de contenido.
