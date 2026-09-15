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
