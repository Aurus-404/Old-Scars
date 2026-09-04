# Old Scars — Prueba 3 Findings

Este documento conserva la evidencia manual integrada posterior a NPC AI Sanitation Fases 2–7. No sustituye a `Issue_Registry.md`, `Implementation_Backlog.md` ni `Project_Roadmap.md`: registra qué se observó en Prueba 3/3.1/3.2, qué conclusiones son válidas y qué cambios de prioridad produjo la prueba.

Fecha de revisión: 2026-09-03.

## Baseline probado

Prueba 3 se ejecutó después de:

- F2 — Behavior ownership + Ambient roaming;
- F3 — Gaze/Attention V1;
- F4 — tracking visual bounded;
- F5 — production Perception centrada en Current Gaze;
- F6 — LostContact/Search V1;
- F7 — representación humana estática + `ActorLocomotionCollider` + seis `ActorCombatHitRegion` explícitos.

F8 de combat targeting/accuracy todavía no había comenzado. Player Invisible/Invincible y Observability V2 tampoco estaban implementados.

---

## Prueba 3 — multi-NPC

### Confirmado visualmente

- White/Blue/Red usan representación humana estática y Blue/Red siguen distinguibles por tint/debug affiliation.
- Ambient roaming produce desplazamiento físico real y reanudación después de encounters.
- Gaze Ambient es visible y el FOV productivo puede devolver `OutsideFov`.
- LOS físico puede devolver `Occluded`.
- LostContact/Search aparece en ejecución real y puede transferir ownership a Search.
- Dead/funcionalmente inactivos dejan de ejecutar acciones activas.
- Anatomía explícita produce wounds diferenciadas; se observaron impactos Torso y LeftLeg sin el patrón anterior de sólo piernas.

### No cerrado por esta muestra

`ISSUE-0008` sigue `SUSPECTED`: la muestra mezcló múltiples shooters y también hubo intervención/player shots, por lo que no permite inferir una distribución NPC limpia.

---

## Prueba 3.1 — intento Blue vs Red

### Hallazgo: Player contamina pruebas NPC-only

Red es hostil a Blue y Player. Durante la corrida se observó que Red podía perder Blue y adquirir Player como threat. Esto vuelve una pelea nominalmente NPC↔NPC una prueba de tres actores.

Conclusión: `Invisible-to-AI` deja de ser sólo comodidad futura. Debe adelantarse como herramienta mínima antes de tomar estadísticas o conclusiones fuertes de combates NPC-only. El toggle debe excluir al Player del candidate/acquisition boundary sin apagar su GameObject, collider o interacción física.

### Hallazgo: incapacidad temporal reinicia artificialmente la pelea

El comportamiento observado y la revisión de código coinciden:

1. un combatiente queda funcionalmente incapacitado;
2. deja de cumplir `CanPerformActiveActions`;
3. el rival deja de considerarlo un hostile candidate activo y libera Encounter;
4. el incapacitado entra en `Inactive` y su Encounter limpia threat/memoria;
5. el rival vuelve a Ambient;
6. al recuperar capacidad, el actor vuelve a Ambient/Idle;
7. ambos pueden redescubrirse y comenzar un encounter nuevo.

Esto produce visualmente el ciclo `golpe → KO/incapacidad → separarse/pasear → recuperación → redescubrimiento → pelea nueva`.

### Decisión de producto para KO / memoria de combate

La conducta deseada V1 queda fijada así:

- un actor noqueado/incapacitado deja de representar una amenaza activa;
- el atacante deja de atacarlo deliberadamente mientras siga incapacitado;
- knockout temporal **no equivale a muerte ni a borrar la memoria de combate**;
- atacante y noqueado conservan la identidad del enemigo/contexto reciente de pelea;
- al recuperar capacidad, el actor puede volver a combatir al enemigo conocido sin tener que tratarlo como un desconocido recién descubierto;
- la memoria no otorga wallhack: si el enemigo ya no es visible, sólo se conserva información legítimamente conocida/última posición conocida y vuelven a aplicar Perception/Search;
- `Dead` sí permanece terminal para conducta activa.

No introducir un `MemorySystem`, blackboard o planner general sólo para este contrato. Resolver primero con el seam mínimo dentro de las autoridades existentes.

### Hallazgo: knockout puede ser demasiado breve

La recuperación de `ActorConditionComponent` usa progreso fisiológico por `WorldClock`; el baseline de world time avanza mucho más rápido que tiempo real. No existe un dwell mínimo real explícito para permanecer KO/unconscious antes de permitir recuperación funcional.

Decisión: un knockout debe poseer un **minimum real-time incapacitation/unconscious dwell configurable**. Después de ese mínimo, la fisiología vigente decide si el actor realmente puede recuperarse. No fijar todavía un valor final de balance sin playtest.

---

## Prueba 3.2 — segundo intento controlado

### Flujo reproducido

La corrida volvió a demostrar el defecto de incapacidad/memoria:

- Blue y Red adquirieron threat mutuo y entraron en Fight;
- Blue quedó funcionalmente incapacitado;
- Red liberó Encounter con `Assigned actor is no longer a living hostile candidate` y volvió a Ambient;
- Blue pasó `Encounter → Inactive`;
- Blue recuperó active behavior capacity, volvió a Ambient/Idle y más tarde volvió a adquirir Red;
- el combate se reabrió como encounter nuevo en lugar de continuar con memoria de la pelea previa.

Esto eleva el problema de simple sensación de game feel a defecto confirmado del contrato actual.

### Anatomía / ISSUE-0008

En las muestras manuales post-F7 se observaron múltiples Torso, algún Arm/Leg y corridas sin concentración extrema en piernas. Esto es evidencia cualitativa positiva, pero insuficiente para cerrar `ISSUE-0008`.

F8A sigue siendo necesaria y debe medir únicamente shots NPC reproducibles con aim source/point, spread, direction, actual collider/region y miss.

---

## Hallazgo de tooling — F6 muestra datos stale como si fueran actuales

Durante Prueba 3/3.1/3.2:

- líneas FOV/LOS podían quedar separadas del NPC y parecer salir del piso o de una posición anterior;
- en algunos momentos desaparecían;
- un actor `Dead / Inactive / Threat <NONE> / Attention Inactive` podía seguir mostrando `Perception: True / Perceived` y `LOS Perceived`.

La revisión de código indica que parte del visual usa snapshots `LastPerception`/`LastAcquisitionPerception`, incluido su `ObserverOrigin`, mientras el actor puede haber seguido moviéndose. El tooling presenta ese snapshot sin distinguir claramente `CURRENT` de `LAST`.

Esto no demuestra que la percepción productiva esté viendo desde el piso; sí demuestra que F6 no es suficientemente fiable para interpretar estado actual durante una pelea móvil.

### Contrato de corrección

- current Gaze/FOV debe originarse en la posición/eye origin actual del actor;
- un resultado histórico debe marcarse explícitamente como `LAST` y conservar su origen histórico sólo cuando sea útil;
- Dead/Inactive no debe presentar un snapshot antiguo como `current perception`;
- la visualización global debe funcionar para todos los NPCs simultáneamente;
- `Selected NPC` sólo controla el inspector profundo, no quién recibe world visuals.

Esto extiende los issues de observabilidad ya abiertos; no crea una segunda autoridad de perception para debug.

---

## Prioridad revisada después de Prueba 3

Antes de continuar con F8A, realizar un correction pass pequeño porque los hallazgos actuales contaminan las pruebas de combat targeting:

1. **Player Invisible-to-AI mínimo** — permitir NPC↔NPC limpio.
2. **F6 observability correctness + multi-NPC mínimo** — current-vs-last correcto, origen actual y world visuals simultáneas.
3. **KO / combat memory continuity** — suspender amenaza activa sin borrar enemigo/contexto por incapacidad temporal.
4. **Minimum KO dwell** — impedir recuperaciones casi inmediatas por aceleración del WorldClock.
5. **Prueba 3.3 controlada 1 Blue vs 1 Red** — Player Invisible, observabilidad simultánea y medición de duración KO/reanudación.
6. Recién entonces **F8A Aim Bias Evidence**.

Esto es una resecuenciación local dentro de M41/NPC Foundation. No elimina ni olvida F8B–F8E, Invincible, Observability V2 completa ni las Fases 11–15.

---

## Plan que permanece vigente

Después del correction pass:

- F8A — Aim Bias Evidence;
- F8B — Generic target-side Primary Aim Point si la evidencia lo justifica;
- F8C — Controlled Before/After sin retuning;
- F8D — Accuracy Simplification Review pequeño;
- F8E — legacy capsule/geometric anatomy migration/cleanup;
- F9 — completar Player Debug, especialmente Invincible y cierre de toggles;
- F10 — completar Observability V2/targeting observability más allá del mínimo adelantado;
- F11 — batería automatizada pequeña pero fuerte;
- F12/F13 — nuevas pruebas integradas NPC-only y Player;
- F14 — manual game feel;
- F15 — cleanup/documentation closure.

No iniciar por inercia BT/GOAP/Utility AI, memory framework general, weak-point targeting, full ballistics, cover/squad AI o un stack nuevo de Accuracy/FireControl.

## Evidencia / archivos de prueba

Los logs manuales de Prueba 3 se conservaron fuera del repo durante la sesión y fueron revisados junto con capturas de WorldRuntime/F6. Este documento conserva sólo las conclusiones verificadas y las decisiones de producto derivadas; el estado formal de cada defecto sigue perteneciendo a `Issue_Registry.md`.
