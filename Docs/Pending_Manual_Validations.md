# Old Scars — Pending Manual Validations

Este documento registra gates manuales que todavía NO fueron ejecutados. No reemplaza `Test_Log.md`: un `TEST-*` sólo se crea cuando la prueba realmente ocurre. Tampoco convierte una validación pendiente en bug; un fallo real debe registrarse luego en `Issue_Registry.md`.

---

## IMPL-0061 — Interacciones sociales ligeras NPC↔NPC

**Estado:** `IMPLEMENTED LOCALLY / AUTOMATED PASS / MANUAL ACCEPTANCE PENDING`

**Fecha del handoff:** 2026-09-15

**Contexto:** la implementación Social V1 fue preparada y validada automáticamente en el checkout canónico, pero Mauro no tenía acceso al PC para realizar la aceptación visual. No declarar `DONE / ACCEPTED` hasta completar los dos gates manuales siguientes.

### Gate A — Basic Talking

**Estado:** `NOT RUN / PENDING`

Procedimiento:

1. Abrir `WorldRuntime`.
2. Activar `Invisible-to-AI` para el Player.
3. Spawnear `3 Blue`.
4. Abrir `F6`.
5. No intervenir.
6. Esperar una interacción Social.
7. Capturar evidencia cuando dos NPC estén en `Talking`.

Validar visualmente:

- roaming normal antes de Social;
- un NPC inicia Social;
- selecciona partner;
- el partner deja roaming;
- el initiator se acerca;
- ambos llegan a `Talking`;
- `F6` muestra Social state;
- Partner IDs recíprocos;
- roles `Initiator / Partner` correctos;
- no hay superposición absurda;
- luego de unos segundos ambos vuelven a `Ambient`.

### Gate B — Combat Preemption

**Estado:** `NOT RUN / PENDING`

Ejecutar sólo después de que Gate A pase.

Procedimiento:

1. Tener dos Blue en `Talking`.
2. Introducir un Red hostil cercano.
3. Verificar que Social se cancela.
4. Verificar que `Encounter/combat` toma prioridad.
5. Verificar que no quedan reservations/Partner stale.

### Recordatorio obligatorio al ejecutar estos gates

Cuando Mauro realice una validación manual de IMPL-0061:

1. crear un nuevo `TEST-*` en `Docs/Test_Log.md` con el siguiente ID disponible en ese momento;
2. registrar setup, evidencia y resultado `PASS / FAIL / PARTIAL`;
3. actualizar `Docs/Development_Log.md` en modo append-only;
4. si Gate A pasa, ejecutar y registrar Gate B como otro `TEST-*` independiente;
5. sólo cuando ambos gates pasen, cambiar IMPL-0061 a `DONE / ACCEPTED / PUBLISHED`;
6. reconciliar `Docs/Current_Milestone.md` y `Docs/Next_Sprints.md`;
7. si aparece un fallo real, recién entonces crear/actualizar `Docs/Issue_Registry.md`.

### Tests automatizados ya reportados durante la implementación local

El handoff de implementación reportó `TEST-20260915-005` a `TEST-20260915-008`, con los fallos intermedios preservados y el diagnóstico focalizado final en PASS. No reutilizar esos IDs para la validación manual.

### Próximo trabajo de implementación

La existencia de este gate pendiente no cambia la secuencia principal: `IMPL-0020 — Carry Weight / Encumbrance` sigue siendo el próximo trabajo de implementación. La aceptación manual de IMPL-0061 puede retomarse cuando Mauro vuelva a tener acceso a Unity.

---

## IMPL-0041 — Player Debug — Heal All / Reset Medical State

**Estado:** `IMPLEMENTED ON DEV / VALIDATION PENDING`

**Fecha del handoff:** 2026-09-15

**Implementación publicada:** `PlayerMedicalResetDebugControl` agrega un control de desarrollo visible junto con Runtime Debug Tools cuando F3 está abierto. El control usa únicamente `ActorHealthComponent`, `ActorMedicalStateComponent` y `ActorConditionComponent`; no introduce una segunda autoridad médica ni persistence nueva.

Contrato actual:

- cancela un wound treatment activo antes de limpiar el estado;
- elimina wounds durables mediante `ActorMedicalStateComponent.HealthyBaseline()`;
- deja bleeding y pain en cero como consecuencia del estado médico vacío;
- restaura Blood a `1.0` y TransientTrauma a `0` mediante `ActorConditionComponent.HealthyBaseline()`;
- devuelve FunctionalState a `Conscious`;
- restaura Vital al máximo mediante la autoridad Health existente;
- NO modifica Invincible, Needs, Stamina, inventory ni equipment;
- NO resucita un Player `Dead`; death/lifecycle sigue siendo una autoridad separada.

### Gate A — Compile / runtime wiring

**Estado:** `NOT RUN / PENDING`

Cuando Mauro vuelva al PC:

1. sincronizar `dev` con el checkout canónico sin perder cambios locales ajenos;
2. compilar Runtime/Editor en Unity;
3. abrir `WorldRuntime`;
4. abrir F3 y confirmar que aparece `PLAYER MEDICAL DEBUG` con el botón `Heal All / Reset Medical State`;
5. confirmar ausencia de errores/excepciones nuevos.

### Gate B — Injured Player reset

**Estado:** `NOT RUN / PENDING`

Preparación sugerida:

1. Player vivo con `Invincible` ON;
2. recibir daño real hasta tener al menos una wound y/o bleeding/pain y Vital por debajo del máximo;
3. pulsar `Heal All / Reset Medical State`.

Esperado:

- Vital = máximo;
- Wounds = 0;
- Bleeding = 0;
- Pain = 0;
- Blood = 1.0;
- TransientTrauma = 0;
- FunctionalState = `Conscious`;
- Invincible conserva su valor previo;
- Player sigue `Alive`.

### Gate C — Dead Player safety

**Estado:** `NOT RUN / PENDING`

Verificar mediante diagnostic o fixture controlado que un Player realmente `Dead` NO sea resucitado por este control. El resultado esperado es rechazo explícito del reset y lifecycle sin cambios.

### Recordatorio obligatorio al validar IMPL-0041

Cuando estos gates se ejecuten:

1. crear nuevos `TEST-*` en `Docs/Test_Log.md` sólo para ejecuciones reales;
2. conservar cualquier FAIL y rerun como IDs separados;
3. actualizar `Docs/Development_Log.md` append-only;
4. si compile + reset vivo + dead-safety pasan, actualizar `IMPL-0041` a `DONE / ACCEPTED / PUBLISHED`;
5. reconciliar `Docs/Current_Milestone.md` / `Docs/Next_Sprints.md` sólo si su snapshot operativo lo requiere.
