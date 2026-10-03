# Old Scars — Product Playability Rebaseline — 2026-10-02

- **Estado:** `APPROVED PRODUCT/PRODUCTION DIRECTION — NOT AN ACTIVE IMPLEMENTATION SCOPE`
- **Autoridad de la decisión:** Mauro.
- **Input de revisión:** auditoría externa independiente realizada con Claude Opus 5.5 sobre `dev@2ecd54621bcd75ad9ba98e5a4f5af46b349de707`.
- **Estado de esa auditoría:** **V3 FINAL**. Clasificó los 1.208 archivos con 0 sin contabilizar y leyó línea por línea los 289 archivos C# (101.943 líneas: 215 runtime + 74 Editor). No ejecutó Unity, no vio el checkout canónico ni los ~26 archivos locales sin publicar, no interpretó visualmente los PNG y no midió performance. El audit se hizo sobre `dev@2ecd546`; este rebaseline y sus commits posteriores no formaron parte de esa lectura.
- **Regla:** la auditoría es evidencia/review, no autoridad por sí sola. Los hallazgos se vuelven backlog, issue, riesgo o dirección sólo cuando el repo o Mauro los confirman.

## Decisión principal

Old Scars tiene foundations sistémicas y técnicas fuertes, pero **todavía no ha demostrado ser un videojuego jugable y repetible para una persona que no conoce su arquitectura interna**.

La situación actual se trata como:

`technical/systemic prototype → playable game proof → connected product → later breadth`

No se considera un fracaso ni se reduce la ambición del proyecto. El problema identificado es **el orden de producción**: se han resuelto muchas foundations profundas antes de demostrar suficientemente un core loop que alguien ajeno al desarrollo quiera jugar y repetir.

A partir de esta decisión:

1. **No se autoriza por inercia otra cadena de foundations** sólo porque sea técnicamente lógica.
2. Las nuevas mecánicas pueden seguir registrándose en `Implementation_Backlog.md`, pero **no desplazan el objetivo jugable** salvo que sean requisito directo de un slice jugable autorizado.
3. Una foundation nueva debe justificar un **consumer de gameplay/producto real** o una dependencia bloqueante demostrada.
4. El siguiente gran criterio de éxito no es “más sistemas implementados”, sino **un loop jugable corto, claro, repetible y entendible sin conocimiento del desarrollo**.
5. El scope activo no cambia retroactivamente: primero se revisa/cierra el checkpoint IMPL-0063 Stage 1. Stage 2/streaming/persistencia volumétrica no quedan autorizados por esta rebaseline.
6. Después del checkpoint actual, la prioridad de planificación pasa a **Playable Core Loop Proof**, con higiene mínima de integridad del repo antes de continuar.
7. La ambición mecánica, worldgen, modding, streaming, vehículos, crafting, facciones, clima y demás sistemas permanecen como dirección/backlog; se implementan cuando el juego probado los necesite.

## Lectura cualitativa de la auditoría

### Lo técnicamente fuerte

- autoridad de dominio e identidades separadas;
- transacciones de items/equipment con preflight, commit y rollback;
- ownership y storage;
- persistencia/recovery;
- determinismo por pass en worldgen;
- validación de datos;
- combate → medicina → condición;
- contratos compartidos Player/NPC;
- disciplina de scopes y evidencia;
- documentación honesta sobre límites de tests y spikes.

Estas foundations **se protegen**. Product-first no significa tirarlas ni reemplazarlas por código rápido y frágil.

### Lo que falta demostrar como juego

- core loop con objetivo;
- razón para explorar/moverse;
- contenido de producto;
- amenaza/decisiones dentro de una sesión;
- recuperación médica cerrada;
- onboarding;
- feedback y game feel;
- UI mínima no dependiente de tooling de desarrollo;
- una sesión que una persona externa pueda jugar sin F3/diagnostics;
- evidencia de que alguien quiere repetirla.

La frase operativa que resume la rebaseline es:

> **No optimizar Old Scars para ser una demo técnica cada vez más completa. Optimizar el siguiente tramo para demostrar que las foundations existentes producen un juego que vale la pena jugar.**

## Playable Core Loop Proof — gate de producto

Este gate es anterior al Connected First Playable open-world existente. No lo sustituye: prueba primero el **juego**, mientras el Connected First Playable posterior prueba integración world/sector/persistence a mayor escala.

### Objetivo

Demostrar una sesión de aproximadamente **10–15 minutos** que pueda jugar una persona no autora sin necesitar explicación de arquitectura ni herramientas debug para hacer lo esencial.

### Debe demostrar, con el menor alcance razonable

- entrada desde Main Menu al runtime jugable;
- un objetivo inmediato comprensible o una necesidad concreta;
- una razón para desplazarse/explorar;
- al menos una decisión material de supervivencia o recursos;
- interacción con loot/inventory/equipment mediante una superficie apta para jugador;
- al menos una amenaza o encuentro donde evitar, retirarse o luchar tenga sentido;
- combate/daño/medicina conectados sin una condición inevitable o absurda conocida;
- feedback suficiente para entender qué ocurrió;
- un cierre de sesión/estado que permita sentir progreso, supervivencia o resolución;
- posibilidad de repetir la sesión con decisiones o resultados suficientemente distintos;
- ausencia de dependencia obligatoria de F3, diagnostics o knowledge de developer para completar el loop.

### No necesita todavía

- arte final;
- audio final;
- UI final completa;
- mundo completo;
- streaming sectorial;
- persistencia volumétrica productiva;
- vehículos;
- crafting completo;
- facciones completas;
- economía;
- geología/cuevas;
- todos los sistemas del GDD.

### Gate humano obligatorio

Además de QA técnica, debe existir al menos una prueba con una persona que no haya construido el sistema. Preguntas mínimas:

1. ¿Entendió qué podía/debía hacer sin explicación extensa?
2. ¿Tomó decisiones, o sólo ejecutó instrucciones?
3. ¿Hubo tensión, curiosidad, riesgo o recompensa legible?
4. ¿Qué parte fue aburrida/confusa?
5. ¿Querría volver a jugar otra sesión corta?

No aprobar el gate sólo porque todos los diagnostics técnicos pasen.

## Hallazgos de la auditoría V3 FINAL — ledger

Este ledger conserva **todos los AUD-01…AUD-41** para que ninguno se pierda. Los issues específicos enlazables viven en `Issue_Registry.md`; los hallazgos de diseño/producción de menor urgencia permanecen aquí hasta que un scope real los necesite. La V3 reemplaza a V2 y corrige varios findings sin cambiar los tres HIGH ni la dirección del rebaseline.

| ID | Severidad reportada | Naturaleza | Disposición en Old Scars |
| --- | --- | --- | --- |
| AUD-01 | HIGH | dev publicado no demostrado en validación aislada del working tree local | `ISSUE-0031` |
| AUD-02 | HIGH | TerrainChunkKey nombra una lattice local/no global | `ISSUE-0042`; condiciona cualquier persistencia/streaming |
| AUD-03 | HIGH | core loop no demostrado; gameplay útil depende de tooling | dirección de producto + R27 |
| AUD-04 | MEDIUM | pausa no detiene timers basados en realtime | `ISSUE-0032` |
| AUD-05 | MEDIUM | terrain mutation no rollback si rebuild falla | `ISSUE-0033` |
| AUD-06 | LOW | el gate Stage 1 compara la misma shared lattice; el mesh seam sí está cubierto por otro diagnostic | límite de evidencia; no invalida Stage 1 ni la suite global |
| AUD-07 | MEDIUM | no hay regresión agregada/cadencia global | `ISSUE-0034` |
| AUD-08 | MEDIUM | coste alto de legacy save antes de usuarios | decisión de maintainability; cortar sólo con scope autorizado |
| AUD-09 | MEDIUM | tooling Debug es superficie/adapter de gameplay | `ISSUE-0037` |
| AUD-10 | MEDIUM | decisiones de producto entraron vía commits de drift sin traza clara | corregido por esta rebaseline + Development Log |
| AUD-11 | MEDIUM | data-driven parcial; claves sin consumer; contenido filtrado a C# | deuda de arquitectura; tocar con consumer real |
| AUD-12 | LOW | reload usa un solo stack compatible | `ISSUE-0035` |
| AUD-13 | MEDIUM | trabajo/allocations por frame que escalan con actores | medir antes de optimizar; candidatos preservados |
| AUD-14 | MEDIUM | assembly único + diagnostic runtime | deuda; no autoriza refactor asmdef masivo |
| AUD-15 | MEDIUM | trabajo local de larga vida contamina cierre/publicación | incluido en `ISSUE-0031` |
| AUD-16 | MEDIUM | wounds no cierran/recuperan; sangrado residual permanente | `ISSUE-0036`; bloquea tuning serio del loop |
| AUD-17 | LOW | productName `Old Scarss` / DefaultCompany | higiene pre-build/distribución |
| AUD-18 | LOW | paquetes sin uso aparente/pre-release | revisar sólo con evidencia; no podar a ciegas |
| AUD-19 | LOW | SampleScene/settings template viajan en build | higiene del primer build jugable |
| AUD-20 | LOW | archivos/fuentes/Resources obsoletos | limpieza diferida |
| AUD-21 | LOW | fragmentos documentales/comentarios stale | corregir cuando se toque su autoridad |
| AUD-22 | INFO | namespaces Editor duplicados | sin acción por sí solo |
| AUD-23 | LOW | evidence hash usa bits float sensibles a backend | relevante sólo si decide compatibilidad durable |
| AUD-24 | LOW | fallo de datos/worldgen puede dejar flujo sin salida visible | `ISSUE-0041` |
| AUD-25 | LOW | allocations/lookups evitables por frame | medir primero; candidatos preservados |
| AUD-26 | LOW | float.Epsilon/fallback geométrico legacy | tratar con consumers/IMPL legacy correspondientes |
| AUD-27 | LOW | Destroy diferido deja representaciones solapadas un frame | condición a considerar antes de streaming |
| AUD-28 | LOW | evidencia vive en Logs/Temp gitignored | trade-off conocido; no fingir auditabilidad histórica |
| AUD-29 | INFO | queries de Physics incluyen triggers | sin problema mientras consumers filtren |
| AUD-30 | LOW | resolución Y del volumen cambia con relieve | parte de `ISSUE-0042` |
| AUD-31 | LOW | duplicación/divergencia real en equipment transactions | `ISSUE-0038` |
| AUD-32 | INFO | item use consume antes de efectos; fallo hoy inalcanzable | conservar como edge case, no arreglar preventivamente |
| AUD-33 | LOW | hueco de diseño: un NPC quieto no tiene conducta de giro corporal; perder target fuera del gaze cone sí es contrato probado | `ISSUE-0039` DESIGN_DEBT; verificar efecto en Play antes de implementar |
| AUD-34 | INFO | iniciar WASD cancela acción temporizada | decisión de diseño pendiente, no bug asumido |
| AUD-35 | LOW | mojibake localizado en 5 archivos / 37 líneas + idioma visible mezclado | `ISSUE-0040` |
| AUD-36 | INFO | worldgen fija shares; la topología de sectores es árbol, la red de caminos tiene ciclos; clima orientado y algunos quality gates quedan satisfechos por construcción | observación de diseño para cuando worldgen tenga gameplay consumer |
| AUD-37 | LOW · PROBABLE | alcance de interacción se valida al abrir menú, no al ejecutar/completar | `ISSUE-0043`; verificar en Play |
| AUD-38 | LOW | dos diagnostics de Blood Trails escriben assets productivos al correr | `ISSUE-0044` |
| AUD-39 | LOW | cuatro entradas de menú pueden abrir escena y descartar cambios no guardados | `ISSUE-0045` |
| AUD-40 | INFO | herramientas Editor y constantes quedaron inalcanzables tras limpieza de menús | mantenimiento diferido; no issue separado por ahora |
| AUD-41 | INFO | 41 callbacks permanentes por tick de Editor; uno hace búsqueda global aun sin ventana abierta | medir/atacar junto con harness compartido si el coste importa |

## Hallazgos prioritarios para el siguiente rebaseline técnico

No son autorización simultánea. Orden conceptual:

1. **Integridad del árbol publicado** — demostrar qué compila realmente en `dev` y resolver el trabajo local de larga vida.
2. **Tiempo de gameplay y pausa** — eliminar exploits/incoherencias antes de usar tiempo como presión jugable.
3. **Medicina cerrable** — una herida no puede convertir todo el loop en una muerte diferida inevitable salvo que sea diseño explícito.
4. **Core loop** — construir con sistemas existentes antes de pedir otra foundation amplia.
5. **Debug → producto** — separar sólo los adapters necesarios para que el loop no dependa de herramientas de developer.
6. **Regresión global proporcional** — punto de entrada/cadencia suficiente para saber que las foundations compartidas siguen sanas.
7. **Terrain identity** — no usar `TerrainChunkKey` actual como ID durable hasta resolver una lattice global/world-addressable.
8. **Performance** — medir un escenario representativo antes de caches/Jobs/Burst/refactors.

## Qué NO cambia

- Mauro conserva autoridad creativa/producto.
- Un audit externo no abre scopes por sí mismo.
- IMPL-0063 Stage 1 sigue siendo el checkpoint activo pendiente de review.
- IMPL-0066 sigue esperando sólo tuning acceptance.
- No se autoriza Stage 2.
- No se autoriza refactor general de UI, asmdefs, performance, modding, terrain o saves.
- Los findings nuevos se registran ahora para impedir que se pierdan; se implementan sólo cuando el orden operativo los habilite.

## Cierre de la auditoría V3

La auditoría quedó **finalizada** para su alcance declarado. Sus calificaciones globales finales:

| Dimensión | Nota |
| --- | ---: |
| Técnica | 7 / 10 |
| Producción / workflow | 6 / 10 |
| Juego actual como experiencia jugable | 2 / 10 |
| Potencial | 7,5 / 10 |

Conteo final: **0 CRITICAL · 3 HIGH · 11 MEDIUM · 27 LOW/INFO**.

Correcciones relevantes frente a V2 incorporadas aquí:

- se retira por falso el supuesto método de 1.841 líneas; el mayor real tiene 528 y sólo 5 superan 200;
- AUD-06 baja a LOW porque sí existe verificación de mesh seams en el diagnostic del spike;
- AUD-33 se reclasifica de bug a hueco de diseño: el gaze cone funciona como fue especificado; falta body turn in place;
- AUD-36 distingue correctamente tree de sectores vs road network con ciclos;
- AUD-35 afecta 5 archivos / 37 líneas;
- la lectura completa de Editor mejora la confianza en la estrategia de diagnostics: goldens, fuzz/stress e injected failures con rollback son evidencia real, aunque sigue faltando harness/cadencia agregada y tree-clean validation.

Las limitaciones del informe continúan siendo parte de su lectura: **no ejecutó Unity, no reprodujo PASS, no vio el checkout local, no midió performance y no leyó completos todos los documentos/lore**. Por lo tanto, un finding PROBABLE sigue requiriendo Play/Unity antes de convertirse en comportamiento reproducido.

La conclusión de producto no cambia: **preservar el rigor técnico, cambiar qué se prioriza**.
