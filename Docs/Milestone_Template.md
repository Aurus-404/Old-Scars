# Old Scars - Milestone Template

Usar esta plantilla para autorizar, implementar, validar o cerrar milestones. [Project_Roadmap.md](Project_Roadmap.md) conserva la autoridad del ID, estado, dependencias y gate.

## Identificacion

- Milestone: `MXX.X — Nombre oficial`
- Tipo: `arquitectura | jugable | herramientas | contenido | produccion | gobernanza`
- Milestone padre: `ID | N/A`
- Responsable de decision:
- Fecha de inicio:
- Fecha de cierre: `PENDING`

## Estado

Estados permitidos:

- `PLANNED`
- `IN PROGRESS`
- `IMPLEMENTED`
- `PENDING UNITY VALIDATION`
- `VALIDATED`
- `DONE`
- `DEFERRED`
- `BLOCKED`
- `REJECTED`

Los calificadores deben aclarar el estado sin crear otra taxonomia. Ejemplos: `IMPLEMENTED — PENDING UNITY VALIDATION`, `IMPLEMENTED — PENDING DOCUMENT REVIEW` y `DEFERRED — RECLASSIFIED`.

- Estado anterior:
- Estado posterior solicitado:
- Motivo de la transicion:

`VALIDATED` nunca significa solo que compila. `DONE` exige validacion/cierre aplicable y documentacion coherente.

## Objetivo Y Resultado Verificable

Objetivo:

- Describir el problema y la decision que habilita.

Resultado verificable:

- Comportamiento, contrato o artefacto observable que debe existir.
- Evidencia que permite distinguir exito de una implementacion parcial.

## Dependencias Y Gate

- Dependencias duras:
- Dependencias blandas:
- Trabajo paralelo permitido:
- Gate de entrada:
- Gate de salida:
- Evidencia requerida por el gate:

## Alcance

Incluido:

- Unidad funcional completa 1.
- Unidad funcional completa 2.

Fuera de alcance:

- Sistema o variante excluida 1.
- Sistema o variante excluida 2.

No objetivos:

- Refactors preventivos, UI final o contenido no necesario para el resultado.

## Sistemas Conectados

Para milestones jugables o sistemicos, completar todos los campos. Para gobernanza, documentacion o tooling sin comportamiento jugable directo, usar `NOT APPLICABLE` con motivo y declarar que contrato o consumidor downstream protege.

- Estado/sistema de entrada:
- Decision jugable afectada:
- Estado/sistema de salida:
- Feedback explicable:
- Comportamiento ante fallo:

## Impacto Tecnico

### Runtime Y Editor

- Contratos nuevos o modificados:
- Contratos reutilizados sin cambios:
- Tooling de Editor:
- Invariantes y rollback:

### Datos

- Definiciones/IDs/tags afectados:
- Schema/validator/loader:
- Compatibilidad y migracion:
- Contenido requerido:

### UI Y Feedback

- Superficie debug o de produccion:
- Input, feedback y accesibilidad:
- Estado vacio/error/stale:

### Persistencia

- Estado durable afectado:
- Identidad y referencias:
- Version/migration/round-trip:
- `NOT APPLICABLE` con motivo, si corresponde.

### QA Y Rendimiento

- Riesgos de regresion:
- Escenarios limite:
- Presupuesto o baseline aplicable:
- Instrumentacion necesaria:

## Archivos

Archivos autorizados:

- `Ruta/Archivo`

Archivos o dominios prohibidos:

- `Ruta/Dominio`

## Plan De Implementacion

1. Paso pequeño y revisable.
2. Integracion con contratos existentes.
3. Verificacion proporcional al riesgo.

## Criterios De Aceptacion

- [ ] Objetivo y resultado verificable completos.
- [ ] Dependencias e invariantes respetadas.
- [ ] Sistemas conectados y feedback comprobados, o `NOT APPLICABLE` justificado.
- [ ] Datos/referencias validos.
- [ ] Fallos, stale state y rollback cubiertos cuando aplican.
- [ ] Fuera de alcance no implementado.
- [ ] Deuda y riesgos registrados.

## Matriz De Validacion

### Checks Estaticos

- `git diff --check`:
- Diff/stat/lista exacta:
- Enlaces/referencias/IDs:
- Otros:

### Compilacion

- Runtime assembly: `PASS | FAIL | NOT RUN | NOT APPLICABLE`
- Editor assembly: `PASS | FAIL | NOT RUN | NOT APPLICABLE`
- Evidencia:

### Pruebas Automatizadas

- EditMode:
- PlayMode:
- Unit/integration externas:
- Evidencia:

### Unity Manual

- Estado: `PASS | FAIL | PENDING | NOT APPLICABLE`
- Escenario y pasos:
- Console:
- Confirmado por:
- Evidencia:

### Revision Documental

- Estado: `PASS | FAIL | PENDING | NOT APPLICABLE`
- Revisor:
- Decisiones pendientes:

Para milestones exclusivamente documentales puede usarse `UNITY VALIDATION NOT APPLICABLE`. No pueden pasar a `DONE` hasta completar la revision documental exigida.

## Riesgos, Deuda Y Follow-Ups

- Riesgo:
- Mitigacion:
- Deuda aceptada:
- Deuda bloqueante:
- Trabajo diferido y trigger de retorno:
- ID reservado o `ID TBD`:

## Documentacion Afectada

- Roadmap: `si/no + motivo`
- Current: `si/no + motivo`
- Development Log: `append-only entry | no`
- Next: `si/no + cambio de cola`
- Architecture/JSON Rules/GDD/Gates: `si/no + contrato`

## Evidencia Git

- Rama:
- HEAD inicial:
- Archivos staged exactos:
- Titulo del commit:
- Resumen del cuerpo:
- Hash del commit:
- Cuerpo inspeccionado con `git log -1 --format=full`: `si/no`
- Push y remoto:
- Estado final del worktree:

## Puerta De Salida

- Criterios que deben estar completos:
- Deuda permitida al salir:
- Deuda que bloquea salida:
- Estado final aprobado:
- Proximo milestone autorizado:
