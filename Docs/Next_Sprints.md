# Old Scars — Next Sprints

Este documento contiene sólo los próximos trabajos reales. `Current_Milestone.md` resume el estado; `Issue_Registry.md` conserva defectos; `Implementation_Backlog.md` conserva mecánicas/mejoras aprobadas; `NPC_AI_Sanitation_Plan.md` mantiene el bloque completo de NPC Foundation.

## Secuencia operativa aprobada — 2026-09-06

### 1. P1 — Cerrar F6 local / Observability minimum slice

Estado: `NEXT`.

Existe una implementación local candidata, todavía no publicada:

- `SandboxNpcObservabilityPanel.cs` modificado;
- `M41F6ObservabilityDiagnostics.cs` nuevo;
- `.meta` nuevo.

Los diagnostics locales ya llegaron a PASS, pero falta aceptación visual/manual.

Cierre requerido:

- Gaze/FOV CURRENT desde eye/origin actual;
- CURRENT y LAST separados;
- varios NPC visibles simultáneamente;
- selección sólo controla el inspector profundo;
- Dead/Inactive no presenta history como current;
- revisar que `LAST` no mezcle origen histórico con datos actuales de un blocker móvil;
- no duplicar Perception/raycasts productivos.

Gate manual: Game View con Blue + Red, movimiento real, selección alternada, oclusión y actor inactive/dead. Sólo después publicar.

Referencias: `IMPL-0010`, `ISSUE-0010`, `ISSUE-0011`, `ISSUE-0019`.

### 2. P2 — Minimum real-time KO dwell

Estado: `NEXT AFTER F6`.

Objetivo:

- al entrar realmente en `Unconscious`, impedir recovery activo antes de un mínimo configurable de tiempo real;
- después del mínimo, physiology/thresholds/hysteresis vigentes siguen decidiendo si puede despertar;
- el timer no fuerza wake-up;
- `WorldClock` acelerado no reduce ese mínimo;
- death sigue terminal.

Antes de implementar, resolver explícitamente la semántica de save/load para que cargar no permita saltar el mínimo por accidente.

No convertir toda `Incapacitated` en KO por conveniencia.

Referencia: `IMPL-0015`, `ISSUE-0021`.

### 3. P3 — KO / combat-memory continuity

Estado: `NEXT AFTER KO DWELL`.

Problema confirmado:

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

Regresiones obligatorias: Search, Timed Bandaging/NPC self-treatment, Opportunistic Reload, Behavior ownership.

Referencia: `IMPL-0014`, `ISSUE-0020`.

### 4. P4 — Prueba 3.3 limpia

Estado: `GATE BEFORE F8A`.

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

Estado: `AFTER PRUEBA 3.3`.

No cambiar gameplay.

Capturar por shot, de forma correlacionada y reproducible:

- target/TargetId;
- aim source;
- aim point actual;
- proposed center-mass relevante;
- focus/current spread;
- shot origin/direction;
- hit collider/hit point;
- BodyRegion o miss;
- seed/condiciones.

No tocar Focus, spread, distance/movement penalties, burst, damage, .303 ni anatomy durante la medición.

Gate: concluir si `ISSUE-0008` depende materialmente del base aim point o si la evidencia apunta a otra causa.

### 6. P6 — F8B/C y F8D sólo si corresponde

Estado: `CONDITIONAL`.

Si F8A justifica cambiar el punto base:

- introducir sólo el Primary Aim Point target-side mínimo;
- humano → center-mass razonable;
- futuros targets → su punto equivalente;
- shooter no inspecciona Torso/especie;
- sin weak points/scoring/head targeting.

Después repetir before/after con mismas condiciones.

F8D review de accuracy sólo ocurre si quedan problemas demostrados después de F8C. No retunear por intuición.

### 7. P7 — Player Debug Invincible

Estado: `AFTER F8 TARGETING STABILIZATION`.

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

Estado: `PLANNED AFTER M41`.

Integrar Equipment real en `humanoid_standard` usando el seam visual existente. Sin nuevo Equipment system, IK ni animación final.

Puede adelantarse sólo si una validación concreta exige ver físicamente el arma; para Prueba 3.3 basta identificación debug inequívoca.

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
