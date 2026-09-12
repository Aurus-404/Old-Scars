# Old Scars — Next Sprints

Este documento contiene sólo los próximos trabajos reales. `Current_Milestone.md` resume el estado; `Issue_Registry.md` conserva defectos; `Implementation_Backlog.md` conserva mecánicas/mejoras aprobadas; `NPC_AI_Sanitation_Plan.md` mantiene el bloque completo de NPC Foundation.

## Secuencia operativa vigente — 2026-09-12

### 1. P1 — F6 cerrado

P1/F6 / Correction Pass B: **DONE / ACCEPTED / PUBLISHED** el 2026-09-06 en `5aac763c14c399bfe09a3e925c50698658ad2716`. CURRENT Gaze/FOV multi-NPC desde origen productivo actual; selección sólo para inspector detallado. LAST usa ObserverOrigin → ObservedPosition históricos, visual secundario y toggle independiente, sin reconstruir blocker hit con collider actual. Sin evidencia: LAST: No evidence. Dead/Inactive sin CURRENT engañoso. Sin nueva Perception/raycasts productivos.

F6 Observability, Gaze/Perception, LostContact/Search y compile Runtime/Editor PASS previos; aceptación visual manual final confirmada por Mauro. IMPL-0010 minimum slice completado; F10 completo pendiente. Posible desajuste eye origin/representación humana separado en ISSUE-0023, sin resolver.

### 2. P2 — Minimum real-time KO dwell

Estado: `DONE / PUBLISHED` — `9ca0335cdc8b85bd49d20ddbe97ad814f44c8578`, 2026-09-07. Core `5 s` inicial de prueba, no balance definitivo; diagnostic P2 y regresiones proporcionales PASS.

Objetivo:

- al entrar realmente en `Unconscious`, impedir recovery activo antes de un mínimo configurable de tiempo real;
- después del mínimo, physiology/thresholds/hysteresis vigentes siguen decidiendo si puede despertar;
- el timer no fuerza wake-up;
- `WorldClock` acelerado no reduce ese mínimo;
- death sigue terminal.

Current Slice v1 persiste el tiempo real restante y la continuidad de Unconscious. Load reanuda el restante; offline no lo consume. Saves legacy que derivan Unconscious reciben un mínimo completo. Sin cambio de schema.

No convertir toda `Incapacitated` en KO por conveniencia.

Referencia: `IMPL-0015`, `ISSUE-0021`.

### 3. P3 — KO / combat-memory continuity

Estado: `DONE / PUBLISHED`, `394d01886b8c6697ca2d492c4450282f561ba688`, 2026-09-07.

Problema resuelto:

`Fight → KO → context/threat cleanup → Ambient → recovery → rediscovery → encounter nuevo`.

Contrato:

- incapacitado/noqueado deja de ser amenaza activa y no recibe ataques deliberados;
- atacante y noqueado pueden conservar identidad/contexto mínimo del enemigo reciente;
- recordar identidad NO equivale a `Threat != null`;
- ese recuerdo por sí solo no debe impedir self-treatment ni AmbientTopOff;
- recovery puede reanudar conflicto sin redescubrimiento artificial;
- memoria no actualiza posición escondida;
- Perception/LKP/Search siguen siendo autoridad espacial;
- death/invalidation terminan la continuidad;
- no crear MemorySystem/blackboard/planner general.

Ventana `encounter_ai.recent_enemy_memory_seconds`: Core `60 s` provisional, no balance final; tiempo real pausado sólo por incapacidad propia, renovado por observaciones legítimas. Memoria efímera de una identidad, sin posición ni persistencia.

Diagnostic P3 y regresiones P2 dwell, Human Encounter, Behavior ownership, Search, Gaze/Perception, Timed Bandaging/NPC self-treatment, Opportunistic Reload y Actor Lifecycle PASS.

Referencia: `IMPL-0014`, `ISSUE-0020`.

### 4. P4 — Prueba 3.3 limpia

Estado: `NEXT — GATE M41`. Sigue pendiente la aceptación manual integrada; los diagnostics F8 completados no sustituyen este gate.

Escenario:

- 1 Blue + 1 Red;
- Player Invisible-to-AI ON;
- ninguna intervención del Player;
- observabilidad simultánea aceptada;
- arma/loadout identificable por debug aunque el visual físico todavía sea incompleto;
- registrar KO start, duración real, recovery, context, wounds/regions, LostContact/Search si aparece.

Gate:

- la pelea debe poder interpretarse sin tooling stale;
- el rival no debe seguir atacando deliberadamente al KO;
- recovery no debe parecer un reset de personalidad;
- un run sin KO no valida recovery: repetir una preparación equivalente sin retunear balance productivo.

### 5. P5 — F8A Aim Bias Evidence

Estado: `DONE / PASS / PUBLISHED`. Evidencia y resultado en `Issue_Registry.md`; `ISSUE-0008 RESOLVED`. Sin tuning de accuracy.

### 6. P6 — F8B Generic Primary Aim Point + F8C paired control

Estado: `DONE / PASS / PUBLISHED`. Se conserva el seam target-side mínimo y el fallback legacy para targets sin punto. Las condiciones y transiciones F8C están registradas en `Issue_Registry.md`.

F8D no está iniciado ni es el siguiente trabajo: sólo se reabre una revisión pequeña si aparece evidencia concreta de un problema residual. No retunear por intuición.

### 7. P7 — Player Debug Invincible

Estado: `AFTER P4 MANUAL INTEGRATION GATE`; el targeting F8 ya está estabilizado para ISSUE-0008.

Objetivo QA:

ON conserva el pipeline real:

`AI detection → shot → collider → BodyRegion → wounds → bleeding → pain → trauma → condition`

pero bloquea coherentemente el desenlace terminal `Dead`.

No basta con omitir `ProcessDeath`: `IsDead`, lifecycle y fatal blood loss deben permanecer consistentes.

OFF = gameplay normal.

Pruebas obligatorias:

- daño vital letal;
- fatalidad por sangre;
- KO permitido mientras Invincible está ON;
- OFF vuelve a reglas normales sin curar ni resucitar;
- no cambia Perception/AI/colliders.

Referencia: `IMPL-0009`, `ISSUE-0012`.

### 8. P8 — Completar Observability V2 / F10

Estado: `AFTER F8 EVIDENCE CONTRACTS EXIST`.

Evolucionar el mismo tooling de P1/F8A:

- overlay compacto multi-NPC;
- inspector profundo del seleccionado;
- target;
- Primary Aim Point si existe;
- focus/spread;
- shot origin/direction;
- collider/region/miss;
- traces suficientes para QA.

Read-only. No segunda autoridad de Perception/Combat.

### 9. P9 — Legacy migration + QA integrada + cierre M41

Estado: `AFTER P4–P8`.

Migrar sólo consumers reales que todavía dependan de representation/anatomy legacy.

Después ejecutar:

- batería automatizada pequeña;
- NPC-only integrada;
- NPC↔Player con Invisible OFF / Invincible ON cuando corresponda;
- manual game feel;
- console review;
- cleanup de instrumentation temporal y código legacy realmente sin consumers;
- reconciliación documental.

NPC Foundation V1 se cierra sólo con aceptación manual de Mauro.

---

# Después de M41

### 10. P10 — Equipment visuals humanoides

Estado: `DONE / ACCEPTED` — adelantado por necesidad de validación visual.

Equipment real quedó integrado en `humanoid_standard` mediante el seam visual existente. Player/NPC comparten el contrato; el tint debug corporal excluye visuals equipados. Sin nuevo Equipment system, IK ni animación final.

Compile, diagnóstico IMPL-0016, cobertura M41.2 y aceptación visual manual Blue/Red `PASS`.

Referencia: `IMPL-0016`.

### 11. P11 — Loaded ammo mass

Estado: `REQUIRED BEFORE ENCUMBRANCE`.

Problema confirmado por código: reload consume ammo owned del Inventory y la firearm guarda `LoadedAmmoProfileId + LoadedRounds`, pero el resolver de peso no suma esa munición cargada. Recargar puede reducir masa calculada artificialmente.

Resolver la conservación de masa sin elegir arbitrariamente “el primer item” compatible con un ammo profile en presencia de mods.

Gate: reload/disparo/save-load conservan masa coherente.

Referencia: `ISSUE-0022`.

P10 y P11 pueden intercambiar posición; P11 sí debe preceder Encumbrance.

### 12. P12 — Carry Weight / Encumbrance compartido

Estado: `READY AFTER M41 + ISSUE-0022`.

Contrato aprobado:

- Carry Capacity no limita storage/acceptance;
- `0..75%` normal;
- `>75%..100%` penalización progresiva;
- `100%` aún móvil;
- `>100%` traslación cero;
- girar/mirar/manipular inventory/drop/equipment/use/reload/treatment siguen según sus autoridades;
- Player y NPC comparten el contrato;
- `Overloaded != Incapacitated`;
- restore sobrecargado conserva items;
- descargar recupera movimiento.

Debe retirar todos los vetos/clamps de peso de Inventory/Transfer/Equipment sin romper grid, ownership, rollback ni access.

Navigation debe distinguir orden/path válido de bloqueo físico por carga; Search no debe convertir sobrecarga temporal en falso path failure/deadline imposible.

Referencia: `IMPL-0020`.

### 13. P13 — Localized Limb Impairment

Estado: `PLANNED AFTER ENCUMBRANCE`.

Usar heridas reales existentes como causa funcional, sin limb HP paralelo.

Orden futuro recomendado:

1. piernas → locomoción/sprint;
2. brazos → handling/reload/melee sólo cuando reglas/consumers estén definidos.

Bandaging reduce bleeding; no repara automáticamente impairment.

Carry y Medical no deben conocerse entre sí.

Referencia: `IMPL-0021`.

## No iniciar todavía

- Encumbrance antes de cerrar M41/F8;
- weapon-driven fire-control sin varios arquetipos reales;
- weapon viability/fallback sin tarea propia;
- Strength/stats y backpack capacity modifiers;
- MemorySystem/blackboard/planner;
- cover/squad/hearing/schedules;
- full ballistics/drop/wind;
- weak-point/head-targeting framework;
- blood tracking AI/footprints/puddles;
- Limb HP paralelo;
- producción masiva de contenido antes del cierre de la foundation.
