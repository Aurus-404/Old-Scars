# Old Scars — instrucciones para Codex

Este proyecto es un juego desarrollado en Unity con C#. Codex actua como asistente de programacion y ejecucion tecnica; no redefine de forma autonoma la direccion creativa ni el alcance de produccion.

## Reglas principales del proyecto

- Old Scars usa arquitectura data-driven.
- JSON define contenido y parametros; C# ejecuta logica cerrada.
- IDs conectan archivos y tags conectan sistemas.
- Las definiciones viven en JSON. Las instancias viven en runtime o, cuando exista, en el sistema de guardado.
- No hardcodear objetos concretos como `Crowbar.cs`, `Door.cs` o `Rifle.cs`.
- No usar ScriptableObjects como base principal del contenido moddable.
- No agregar sistemas grandes ni ampliar el alcance sin autorizacion explicita del milestone.

## Jerarquia documental

- Mauro conserva la autoridad creativa y la decision final de producto.
- Las decisiones explicitas recientes y los milestones aprobados/validados prevalecen sobre fuentes de diseño anteriores.
- [Game_Design_Document.md](Docs/Game_Design_Document.md) es el baseline de diseño revisado y mantenible del repositorio; separa direccion confirmada, objetivo, estado tecnico, propuesta y decision pendiente.
- El GDD Maestro v3.1 externo se conserva intacto como fuente historica y de diseño auditada. No es una especificacion incuestionable ni puede convertir propuestas en canon por si solo.
- [Project_Roadmap.md](Docs/Project_Roadmap.md) es la autoridad canonica de IDs, estados, dependencias y gates.
- [Current_Milestone.md](Docs/Current_Milestone.md) es un snapshot operativo breve del trabajo activo.
- [Development_Log.md](Docs/Development_Log.md) es cronologia append-only; una entrada nueva puede reconciliar estados anteriores, pero no se reescribe la historia.
- [Next_Sprints.md](Docs/Next_Sprints.md) contiene solamente los proximos trabajos reales.
- [Technical_Architecture.md](Docs/Technical_Architecture.md) documenta contratos tecnicos implementados, no estados de milestone.
- [DataDriven_JSON_Rules.md](Docs/DataDriven_JSON_Rules.md) documenta el contrato vigente de definiciones y validacion JSON.
- [Production_Gates_and_Risks.md](Docs/Production_Gates_and_Risks.md) desarrolla criterios, evidencia y riesgos; el Roadmap conserva la autoridad sobre nombre y ubicacion de cada gate.

Ante una contradiccion, no elegir silenciosamente una version: el repositorio decide que existe tecnicamente, pero no decide por si solo el diseño final. Verificar la fuente con autoridad, escalar a Mauro toda ambiguedad creativa o de producto y registrar la reconciliacion en el documento que corresponda.

## Filtro de Old Scars

Antes de proponer o implementar codigo, sistema, sprint o mecanica, verificar:

- que ayuda al nucleo de exploracion, recuperacion, supervivencia y consecuencias persistentes;
- que respeta sistemas y contratos ya construidos;
- que no perjudica mecanicas futuras probables ni convierte el juego en otra cosa;
- que no introduce sistemas prematuros o universales preventivos;
- que mantiene JSON, tags e IDs como datos y C# como logica;
- que aporta una base jugable real, ordenada y verificable.

Profundidad mediante sistemas conectados es una regla transversal: un sistema nuevo debe recibir estado relevante de otro sistema, cambiar al menos una decision jugable y ofrecer feedback explicable. Una barra o simulacion aislada no alcanza.

## Autorizacion de alcance

No existe una prohibicion permanente sobre inventario, Equipment, loot, UI, save, actores, combate, IA u otros dominios: algunos ya poseen foundations validadas y otros estan reservados en el Roadmap. Su implementacion o ampliacion solo esta autorizada cuando el milestone activo y sus dependencias lo permiten.

- Reutilizar foundations validadas dentro del alcance aprobado; no reescribirlas sin evidencia y autorizacion.
- No adelantar sistemas de milestones futuros aunque aparezcan en el Roadmap.
- No ampliar UI debug, contenido, schemas o arquitectura por conveniencia preventiva.
- No iniciar ramas grandes como IA compleja, combate completo, UI final, mundo a escala o scripting libre en JSON sin milestone autorizado.
- Si una solicitud contradice el milestone, un gate o una dependencia dura, detenerse y explicar el conflicto antes de modificar archivos.

## Reglas de JSON

- Los Global Content IDs de Definitions registradas usan `namespace:local_id` canónico; ambos segmentos aceptan sólo letras minúsculas ASCII, dígitos y `_`. `core` está reservado para contenido oficial.
- Los Local IDs, tags, runtime/instance IDs, persistent scene IDs, save slot IDs y asset keys son dominios separados; no agregarles `core:` por sufijo o búsqueda textual.
- Los IDs duplicados dentro de su tipo o registro son errores fuertes; el runtime actual no ofrece overrides de definiciones. La reutilizacion textual entre familias distintas no debe presentarse como identidad compartida.
- El campo `type` debe validarse.
- Los tags usados deben existir en `tags.json`.
- Las referencias rotas deben producir error.
- No guardar logica compleja ni scripting libre dentro de JSON.
- No leer JSON continuamente durante gameplay; se carga y valida al inicio.
- No mezclar definiciones de contenido con estado de save/runtime.
- No asumir identidad individual para cada unidad fungible de un stack: el contrato actual usa una `ItemInstance` representativa mas cantidad; M36.1 debe congelar la granularidad durable.

## Antes de cada milestone o sprint

Ante un milestone nuevo, un rebaseline, una contradiccion concreta o un cambio de estado/continuidad, leer en este orden:

1. este archivo;
2. [Project_Roadmap.md](Docs/Project_Roadmap.md);
3. [Current_Milestone.md](Docs/Current_Milestone.md);
4. [Development_Log.md](Docs/Development_Log.md);
5. [Next_Sprints.md](Docs/Next_Sprints.md);
6. [DataDriven_JSON_Rules.md](Docs/DataDriven_JSON_Rules.md);
7. los contratos tecnicos y de diseño relevantes al alcance.

En tareas acotadas dentro de un contrato vigente, leer solo los archivos directamente implicados, sus contratos inmediatos y la documentacion cuya verdad pueda cambiar. No releer por rutina documentacion ya conocida ni reconstruir milestones historicos cerrados si no existe una contradiccion concreta. Comparar la propuesta con milestones validados, restricciones actuales, dependencias, gates, deuda conocida y sistemas conectados en proporcion al riesgo real.

## Prompts Y Configuracion De Codex

Todo prompt de trabajo debe identificar, con detalle proporcional al riesgo: milestone con ID y nombre oficial; milestone padre si existe; objetivo; estado inicial y esperado; relacion con milestones anterior y siguiente; incluido y fuera de alcance; archivos o dominios autorizados; validacion; documentacion afectada; y estrategia Git.

Debe indicar tambien la configuracion recomendada:

- modelo: `GPT-5.6 Sol`, `Terra` o `Luna`;
- esfuerzo: `Mínimo`, `Bajo`, `Medio`, `Alto`, `Muy alta` o `Ultra`;
- velocidad: `Estándar` o `Rápida`;
- modo: `Plan` u `Objetivo`.

Usar Luna para trabajo pequeño, mecanico y localizado; Terra para trabajo cotidiano, balanceado y acotado; Sol para arquitectura, transacciones, ownership, rollback, persistencia, ambiguedad o riesgo alto. Usar Plan para auditoria, arquitectura, ambiguedad o alcance abierto, y Objetivo cuando objetivo, alcance y aceptacion ya estan definidos. No cambiar de modelo durante la tarea sin motivo explicito ni convertir ajustes triviales en prompts enormes. Ver el detalle durable en [OldScars_Development_Rules.md](Docs/OldScars_Development_Rules.md) y aplicarlo mediante [Milestone_Template.md](Docs/Milestone_Template.md).

## Presupuesto De Ejecucion Y Eficiencia

### Alcance Y Auditoria

- Implementar la unidad funcional solicitada por el camino minimo correcto. Una vez fijado el diseño, no ampliar el scope salvo blocker funcional real.
- Bugs, mejoras, ergonomia y deuda no bloqueantes se registran como deferred; no se corrigen automaticamente. No iniciar el siguiente milestone dentro de la misma tarea.
- No realizar auditorias amplias del repositorio por defecto. Leer solo los archivos directamente implicados y sus contratos inmediatos.
- No reconstruir innecesariamente milestones historicos cerrados ni releer documentacion conocida salvo contradiccion concreta o cambio real de verdad.

### Subagentes

- No usar subagentes por defecto. Justificarlos solo cuando exista una investigacion realmente independiente o paralelizable.
- El maximo normal es un subagente. Usar mas de uno requiere autorizacion explicita de Mauro o una necesidad tecnica extraordinaria explicada antes de delegar.
- El agente principal integra y decide. Normalmente un solo implementador modifica archivos acoplados; las conclusiones paralelas no sustituyen su revision.

### Presupuesto De Implementacion

- Presupuesto orientativo por unidad: aproximadamente 8 archivos C# y 500 lineas C# nuevas como maximo.
- Superar ese presupuesto requiere autorizacion explicita o detenerse para reportar que el alcance real excede lo previsto.
- Si una tarea define un techo especifico, ese techo reemplaza al default.

### Diagnostics Y Regresiones

- Crear preferentemente un diagnostic principal por unidad. No construir tooling adicional salvo que sea necesario para validar el contrato.
- No perfeccionar fixtures despues del primer PASS funcional requerido. Una dificultad menor de testing manual no justifica ampliar arquitectura productiva.
- Ejecutar solo: compilacion, diagnostic directo de la unidad y regresiones de los seams realmente afectados.
- No ejecutar por rutina toda la historia M36 a M40. No repetir una suite que ya dio PASS si desde entonces no cambio codigo capaz de afectarla.

### Regla De PASS Y Presion De Presupuesto

**PASS significa congelar implementacion.** Despues del PASS requerido no hacer polishing, refactors opcionales, helpers adicionales, mejoras de ergonomia debug, features nuevas ni una ultima mejora.

Continuar unicamente con:

`auditoria final -> documentacion necesaria -> commit -> push`

salvo que aparezca una regresion funcional real.

- Prioridad obligatoria: `compile -> funcionalidad central -> diagnostic directo -> regresion imprescindible -> commit/push`, antes que polish, tooling extra, documentacion extensa, auditorias generales o mejoras opcionales.
- Si una unidad crece inesperadamente, no continuar expandiendola: completar el nucleo coherente ya iniciado, validar, publicar un checkpoint funcional y registrar el resto como deferred.
- Si pasan aproximadamente 20-25 minutos sin alcanzar un primer PASS funcional, reevaluar el plan y eliminar trabajo no esencial. No cortar arbitrariamente una implementacion coherente a mitad de camino: dejar siempre el checkout funcional, validado y publicable.

### Git Y Cierre

- Cuando la tarea funcional esta validada, cerrar Git cuanto antes: commit descriptivo, `git log -1 --format=full`, push normal, `HEAD == origin/dev`, divergencia `0/0` y arbol limpio.
- No postergar el commit por mejoras opcionales.

### Trabajo En Segundo Plano Y Control Del Escritorio

- Codex esta autorizado a trabajar en segundo plano mediante terminal, PowerShell, Git, filesystem, scripts, compiladores, Unity batchmode/CLI y diagnostics automaticos.
- Puede consultar y terminar de forma segura procesos tecnicos relacionados con la tarea. Puede finalizar procesos batchmode o workers que haya iniciado si quedan colgados, despues de verificar razonablemente su identidad.
- Puede eliminar `Temp/UnityLockfile` solo despues de confirmar que ningun proceso Unity valido sigue usando el proyecto.
- Esta absolutamente prohibido usar control grafico o interactivo del escritorio: no mover mouse, hacer clicks, simular teclado, cambiar ventanas, navegar Unity GUI, tomar foco ni controlar aplicaciones graficamente.
- Mauro debe poder continuar usando o jugando en la PC mientras Codex trabaja. No terminar procesos personales claramente ajenos al desarrollo.

### Disciplina Documental

- Actualizar unicamente los documentos cuya verdad haya cambiado realmente. No convertir cada modificacion pequeña en una reescritura de Roadmap, Current Milestone, Next Sprints, GDD, Architecture y Gates.
- `Development_Log.md` permanece append-only cuando una entrada sea realmente necesaria.
- Las reglas permanentes de workflow viven en `AGENTS.md`; no duplicarlas en multiples documentos.

## DIAGNOSTIC LOGGING POLICY

- Los failure boundaries deben producir mensajes accionables en Unity Console / `Editor.log`, con contexto suficiente para diagnostico remoto y valores ausentes importantes representados como `<NONE>`, `<EMPTY>` o `<UNKNOWN>`.
- Los success logs importantes deben ser breves y correlacionables por IDs; no registrar por frame, refresh de UI, celda de grid ni metodo interno rutinario.
- No crear frameworks preventivos de logging, telemetry o analytics. Toda tarea futura que modifique un failure boundary debe revisar tambien la calidad y proporcionalidad de sus logs.

## Workflow Operativo

- Todo trabajo mutante autorizado que supera sus verificaciones debe terminar con commit con cuerpo descriptivo, inspeccion mediante `git log -1 --format=full`, push a `origin/dev` y confirmacion de arbol limpio/sincronizado.
- Se exceptuan tareas de solo lectura, auditorias sin cambios, fallos de verificacion, bloqueos de alcance, cambios locales ajenos o una instruccion explicita de Mauro de no publicar. No usar amend despues de publicar, force push ni rebase sin autorizacion.
- En tareas visuales, adjuntar capturas cuando esten disponibles. Codex debe abrirlas todas, describir brevemente que muestran, relacionar el defecto con codigo/layout/escena/asset y solicitar validacion visual posterior; compilar no demuestra que un defecto visual este resuelto.
- Aplicar la politica de subagentes y presupuesto definida en `Presupuesto De Ejecucion Y Eficiencia`.
- Preferir milestones suficientemente pequeños para ser revisables y suficientemente completos para entregar una unidad funcional util. No crear milestones por clase, archivo, boton o ajuste menor; agrupar cambios que comparten sistema, contratos, archivos, validacion y resultado, y separar sistemas independientes o decisiones abiertas.

## Reglas de implementacion

Antes de modificar archivos:

1. revisar la implementacion y los datos reales existentes;
2. explicar brevemente que se va a cambiar;
3. confirmar archivos autorizados, archivos prohibidos y validacion requerida;
4. hacer cambios pequeños, revisables y compatibles con el milestone;
5. preservar identidad por `InstanceId`, ownership, atomicidad y rollback donde apliquen;
6. separar `IMPLEMENTED` de `VALIDATED` y no afirmar prueba Unity sin evidencia explicita;
7. registrar deuda y fuera de alcance sin convertirlos en implementacion implicita.

No ejecutar Unity, batchmode, compilaciones, commits o pushes cuando el encargo los prohiba.
