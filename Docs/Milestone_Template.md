# Old Scars - Milestone Template

Usar esta plantilla para autorizar, implementar, validar o cerrar milestones. [Project_Roadmap.md](Project_Roadmap.md) conserva la autoridad del ID, estado, dependencias y gate. Completar siempre el Nivel A y agregar solamente los bloques pertinentes del Nivel B.

La informacion debe ser proporcional al riesgo. Una correccion localizada no necesita decenas de campos irrelevantes; arquitectura, persistencia, transacciones, ownership, grandes sistemas y milestones de produccion requieren la extension que corresponda.

## Nivel A - Nucleo Obligatorio

### Identificacion Y Configuracion

- Milestone: `MXX.X — Nombre oficial`
- Version o correction pass:
- Tipo: `arquitectura | jugable | herramientas | contenido | produccion | gobernanza`
- Milestone padre: `ID | N/A`
- Milestone anterior:
- Milestone siguiente:
- Responsable de decision:
- Modelo Codex: `GPT-5.6 Sol | Terra | Luna`
- Esfuerzo: `Mínimo | Bajo | Medio | Alto | Muy alta | Ultra`
- Velocidad: `Estándar | Rápida`
- Modo: `Plan | Objetivo`
- Motivo de la configuracion:

### Objetivo Y Estados

Objetivo:

- Problema concreto, decision que habilita y resultado buscado.

- Estado inicial:
- Estado esperado:
- Motivo de la transicion:

Estados canonicos:

- `PLANNED`
- `IN PROGRESS`
- `IMPLEMENTED`
- `PENDING UNITY VALIDATION`
- `VALIDATED`
- `DONE`
- `DEFERRED`
- `BLOCKED`
- `REJECTED`

Los calificadores aclaran el estado sin crear otra taxonomia. `VALIDATED` nunca significa solamente que compila y `DONE` exige validacion/cierre aplicable y documentacion coherente.

### Dependencias Esenciales

- Dependencias duras:
- Dependencias blandas:
- Contratos existentes que deben reutilizarse:
- Decisiones abiertas que bloquean:

### Alcance

Incluido:

- Unidad funcional util 1.
- Unidad funcional util 2.

Fuera de alcance:

- Sistema, variante o refactor excluido 1.
- Sistema, variante o refactor excluido 2.

No crear un milestone por clase, archivo, boton o ajuste menor. Agrupar cambios que comparten sistema, contratos, archivos, validacion y resultado; separar sistemas independientes, riesgos distintos o decisiones abiertas.

### Archivos Y Dominios

Archivos o dominios autorizados:

- `Ruta/Archivo`

Archivos o dominios prohibidos:

- `Ruta/Dominio`

### Resultado Y Aceptacion

Resultado verificable:

- Artefacto, contrato o comportamiento observable.
- Evidencia que distingue exito de implementacion parcial.

Criterios de aceptacion:

- [ ] Objetivo y unidad funcional completos.
- [ ] Dependencias y contratos preservados.
- [ ] Fuera de alcance no implementado.
- [ ] Fallos y estados limite aplicables cubiertos.
- [ ] Deuda y trabajo siguiente registrados.

### Validacion Aplicable

- Checks estaticos:
- Compilacion Runtime: `PASS | FAIL | NOT RUN | NOT APPLICABLE`
- Compilacion Editor: `PASS | FAIL | NOT RUN | NOT APPLICABLE`
- Pruebas automatizadas:
- Validacion manual: `PASS | FAIL | PENDING | NOT APPLICABLE`
- Escenario, pasos y evidencia:
- Console:
- Confirmado por:
- Revision documental: `PASS | FAIL | PENDING | NOT APPLICABLE`

Para trabajo visual, adjuntar capturas disponibles, describirlas y solicitar validacion visual posterior. Una compilacion correcta no demuestra que layout, clipping, camara, animacion o arte esten resueltos.

### Documentacion

- Roadmap: `si/no + motivo`
- Current: `si/no + motivo`
- Development Log: `append-only entry | no`
- Next: `si/no + cambio de cola`
- Architecture/JSON Rules/GDD/Gates: `si/no + contrato`

### Git Y Publicacion

- Estrategia Git:
- Rama:
- HEAD inicial:
- Archivos staged exactos:
- Titulo del commit:
- Resumen obligatorio del cuerpo:
- Hash:
- Cuerpo inspeccionado con `git log -1 --format=full`: `si/no`
- Push a `origin/dev`: `PASS | BLOCKED | EXPLICITLY FORBIDDEN`
- HEAD == origin/dev:
- Worktree limpio:

El cuerpo del commit registra milestone completo, padre, version/correction pass, objetivo, estados, cambios, contratos preservados, verificaciones, validacion manual, deuda y trabajo diferido. No publicar un commit con cuerpo vacio. No usar amend despues de publicar, force push ni rebase sin autorizacion explicita.

### Deuda Y Siguiente Trabajo

- Deuda aceptada:
- Deuda bloqueante:
- Trabajo diferido y trigger:
- Proximo milestone autorizado:
- Estado final propuesto:

## Nivel B - Extension Condicional

Agregar solo los bloques que correspondan al riesgo o arquitectura de la tarea. No rellenar secciones irrelevantes con listas de `NOT APPLICABLE`.

### Sistemas Conectados

- Estado/sistema de entrada:
- Decision jugable afectada:
- Estado/sistema de salida:
- Feedback explicable:
- Comportamiento ante fallo:

### Datos, Schemas Y Compatibilidad

- Definiciones, IDs y tags:
- Schema/validator/loader/runtime:
- Referencias y errores esperados:
- Compatibilidad y migration:
- Contenido requerido:

### Persistencia Y Save/Load

- Estado durable:
- Identidad y referencias:
- Snapshot/hidratacion:
- Version, integridad y migration:
- Escritura atomica y recovery:
- Round-trip y escenarios de fallo:

### Ownership, Transacciones Y Rollback

- Fuente de verdad y owner:
- Invariantes:
- Preview/preflight:
- Commit:
- Rollback y snapshots:
- Hooks post-commit:
- Stale state y concurrencia:

### UI, Feedback Y Evidencia Visual

- Superficie debug o produccion:
- Input y autoridad de sesion:
- Estados vacio/error/stale:
- Accesibilidad:
- Capturas de entrada:
- Validacion visual posterior:

### Rendimiento

- Escenario representativo:
- Hardware/plataforma objetivo:
- Baseline:
- Presupuesto:
- Instrumentacion y profiling:

### Pruebas Automatizadas Y Tooling

- Unit/EditMode/PlayMode/integration:
- Fixtures, seams y casos limite:
- Tooling Editor o pipeline:
- Reproducibilidad y diagnostics:

### Gates Y Riesgos

- Gate de entrada:
- Gate de salida:
- Evidencia requerida:
- Riesgos revisados:
- Riesgos que deben cerrar:
- Mitigacion, responsable y proxima revision:

### Networking

- Autoridad y ownership de red:
- Replicacion y orden de eventos:
- Reconciliacion/rollback:
- Latencia y desconexion:
- Seguridad:

## Variante Compacta

Para correcciones localizadas, ajustes visuales, documentacion simple, JSON pequeño o bugs acotados, puede resumirse el Nivel A en:

1. milestone/version/configuracion Codex;
2. objetivo y estados;
3. incluido/fuera de alcance;
4. archivos autorizados/prohibidos;
5. resultado y aceptacion;
6. validacion aplicable y capturas si hay superficie visual;
7. documentacion;
8. commit con cuerpo, push y limpieza;
9. deuda y siguiente trabajo.

La variante compacta no reduce los contratos ni la evidencia: elimina solamente campos irrelevantes.
