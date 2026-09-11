# Old Scars — Mechanic Idea Pool

Este documento conserva ideas de mecánicas que surgieron durante brainstorming pero que **NO están aprobadas todavía** como dirección de producto.

No sustituye `Implementation_Backlog.md`, no crea milestones, no autoriza implementación y no debe convertirse en trabajo inmediato por inercia.

## Estado de estas ideas

Todas las entradas de este archivo se consideran:

`OPEN IDEA / LOW CONFIDENCE`

Significa que Mauro respondió con dudas del tipo `quizás`, `no sé`, `todavía lo estoy pensando`, `puede ser` o equivalente. Son posibilidades que vale la pena no perder, pero tienen baja probabilidad de implementarse en su forma actual y pueden cambiar o descartarse por completo.

Una idea sólo debe promocionarse a `Implementation_Backlog.md` cuando Mauro la confirme explícitamente y exista un contrato de producto suficientemente claro.

---

## IDEA-0001 — Humo simplificado de fuentes de combustión

- **Origen:** brainstorming 2026-09-10, idea 5.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** una fogata, incendio u otra fuente podría producir una consecuencia de humo muy simplificada en interiores o alrededor de la fuente.
- **Restricción conocida:** no queremos simulación volumétrica detallada, ventilación compleja ni dinámica de gases.
- **Por decidir:** si el humo aporta suficiente gameplay para justificar incluso una representación abstracta.

## IDEA-0002 — Huellas de paso como evidencia de actividad

- **Origen:** brainstorming 2026-09-10, ideas 10–12.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** caminar sobre ciertas superficies podría dejar una marca temporal simple y barata. Potenciales usos futuros: caza de animales, detectar que una persona pasó recientemente por una zona o alimentar eventos/consumers concretos.
- **Restricción conocida:** deben ser visualmente baratas, temporales y sometidas a budget/pooling. No queremos envejecimiento detallado por huella.
- **Relación:** `IMPL-0026` ya cubre la foundation de evidencia ambiental temporal y cleanup climático, pero NO aprueba footprints completos.
- **Por decidir:** superficies válidas, duración, si existe evidencia lógica separada del visual y si NPC/eventos pueden consumirla.

## IDEA-0003 — Lectura de objetivos a larga distancia

- **Origen:** brainstorming 2026-09-10, idea 16.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** explorar en el futuro cómo clima, distancia y dirección visual del juego afectan la legibilidad de siluetas/objetivos lejanos.
- **Restricción conocida:** no diseñar el sistema hasta que esté definida la representación visual final de personajes, terreno y objetos a distancia.
- **Por decidir:** si se resuelve sólo por rendering/LOD/niebla o si requiere alguna regla jugable adicional.

## IDEA-0004 — Acústica interior simplificada

- **Origen:** brainstorming 2026-09-10, idea 18.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** disparos, pasos u otros sonidos podrían tener tratamiento audiovisual distinto en interiores frente a exteriores.
- **Restricción conocida:** mejora de calidad audiovisual, no prioridad sistémica. Sin propagación acústica compleja ni simulación de habitaciones.
- **Por decidir:** si basta con snapshots/reverb zones/audio profiles o si no merece implementación propia.

## IDEA-0005 — Contenedores rígidos vs flexibles

- **Origen:** brainstorming 2026-09-10, idea 21.
- **Estado:** `OPEN IDEA / VERY LOW CONFIDENCE`.
- **Intención original:** diferenciar recipientes cuyo volumen exterior permanece fijo de otros que podrían colapsar al vaciarse.
- **Problema conocido:** puede crear casos problemáticos de inventario cuando un contenedor cambia de tamaño al llenarse y deja de caber donde estaba.
- **Por decidir:** probablemente descartar salvo que aparezca un consumer concreto que justifique una versión mucho más simple.

## IDEA-0006 — Protección del contenido por el contenedor

- **Origen:** brainstorming 2026-09-10, idea 24.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** ciertos contenedores podrían proteger su contenido de condiciones específicas como agua, impactos, calor u otros hazards.
- **Restricción conocida:** no crear una matriz universal de resistencias ni simulación profunda del material del recipiente.
- **Por decidir:** primer caso jugable concreto que justifique este comportamiento y qué propiedad mínima debe exponerse.

## IDEA-0007 — Desmontaje de ítems con recuperación condicionada por habilidad

- **Origen:** brainstorming 2026-09-10, idea 36.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** ciertos ítems podrían declarar componentes recuperables al ser desmontados. Una habilidad tipo `Tinkerer`/`Mechanic` podría mejorar la probabilidad o cantidad de componentes útiles recuperados.
- **Dirección tentativa:** evitar que RNG puro convierta una acción razonable en cero recompensa; si se adopta, preferir un resultado mínimo determinista y que la habilidad mejore componentes adicionales/calidad.
- **Por decidir:** existencia y alcance real de skills, tabla de drops por ítem y relación con crafting/repair.

## IDEA-0008 — Averías de armas de fuego

- **Origen:** brainstorming 2026-09-10, ideas 44–45.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** armas extremadamente deterioradas, componentes dañados u otras causas observables podrían producir averías.
- **Restricción conocida:** Mauro no quiere penalizaciones RNG arbitrarias durante acciones cotidianas. Evitar `X% de atasco por disparo` sin una causa legible.
- **Por decidir:** si las averías aportan suficiente gameplay y qué causas deterministas/semideterministas serían aceptables.

## IDEA-0009 — Temperatura de armas

- **Origen:** brainstorming 2026-09-10, idea 46.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** disparos sostenidos podrían elevar la temperatura del arma y esa temperatura podría alimentar consumers futuros como condición, fiabilidad o interacción.
- **Restricción conocida:** no implementar heat por realismo aislado; requiere un efecto jugable concreto y medible.
- **Por decidir:** qué armas lo necesitan y qué consecuencias justifican el sistema.

## IDEA-0010 — Interacción del arma larga con paredes/obstáculos

- **Origen:** brainstorming 2026-09-10, idea 47.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** armas largas podrían resultar incómodas al apuntar muy cerca de una pared u obstáculo, posiblemente reduciendo la capacidad de mantenerlas extendidas.
- **Problema conocido:** detección de colisiones/contacto del arma, representación visual y riesgo de añadir complejidad a aim/animation.
- **Por decidir:** si el beneficio jugable en cámara orbital justifica el coste y cuál sería la detección mínima.

## IDEA-0011 — Near-miss / supresión por proyectiles cercanos

- **Origen:** brainstorming 2026-09-10, idea 55.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** proyectiles que pasan muy cerca sin impactar podrían producir una respuesta perceptible o afectar estados del actor.
- **Restricción conocida:** no crear un `SuppressionSystem` grande sin necesidad. Cualquier reacción debería usar el shot path real existente.
- **Por decidir:** efecto sobre Player/NPC, umbral espacial, relación con miedo/agitación y coste de detección.

## IDEA-0012 — Rotura física de ventanas/cristales

- **Origen:** brainstorming 2026-09-10, idea 61.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** ventanas y superficies de vidrio podrían romperse y cambiar de estado físico/visual.
- **Relación:** `IMPL-0040` ya aprueba que vidrio roto pueda ser un hazard mitigable por protección; esta entrada sólo conserva la duda sobre cómo ocurre y se representa la rotura.
- **Restricción conocida:** evitar cientos de fragmentos físicos persistentes.
- **Por decidir:** estados, interacción, visuales y comportamiento del hueco tras romperse.

## IDEA-0013 — Vault contextual

- **Origen:** brainstorming 2026-09-10, idea 64.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** permitir superar obstáculos bajos mediante una acción de vault contextual en lugar de depender siempre del salto.
- **Por decidir:** necesidad real según level design, detección mínima, animaciones y convivencia con navegación NPC.

## IDEA-0014 — Paso por espacios estrechos

- **Origen:** brainstorming 2026-09-10, idea 65.
- **Estado:** `OPEN IDEA / VERY LOW CONFIDENCE`.
- **Intención:** ciertos espacios podrían requerir atravesar lateralmente o limitar el paso según volumen/equipo.
- **Problema conocido:** interacción fuerte con collider, animación, equipment y level design para una ganancia jugable todavía poco clara.
- **Por decidir:** probablemente descartar salvo que un entorno concreto lo necesite.

## IDEA-0015 — Resbalones o pérdida de estabilidad por superficie

- **Origen:** brainstorming 2026-09-10, idea 66.
- **Estado:** `OPEN IDEA / VERY LOW CONFIDENCE`.
- **Intención:** algunas combinaciones de pendiente, superficie y velocidad podrían generar pérdida de estabilidad.
- **Restricción conocida:** evitar RNG frustrante y sobre-simulación de fricción.
- **Por decidir:** si alguna versión determinista/simple aporta suficiente gameplay.

## IDEA-0016 — Tracción de vehículos por superficie

- **Origen:** brainstorming 2026-09-10, idea 68.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** además de multiplicadores simples de velocidad por superficie, futuros vehículos podrían diferenciar cuánto agarre/tracción tienen sobre carretera, tierra, barro u otras superficies.
- **Restricción conocida:** priorizar primero un contrato sencillo `surface → vehicle movement modifier`; no simular fuerza individual por rueda sin evidencia de valor.
- **Por decidir:** si la diferencia de tracción necesita existir separada de velocidad.

## IDEA-0017 — Presión de neumáticos

- **Origen:** brainstorming 2026-09-10, idea 69.
- **Estado:** `OPEN IDEA / VERY LOW CONFIDENCE`.
- **Intención:** neumáticos podrían tener presión y esa presión influir en comportamiento o mantenimiento.
- **Restricción conocida:** alto riesgo de sobre-simulación para poco gameplay.
- **Por decidir:** sólo reconsiderar cuando exista un sistema real de vehículos/neumáticos y un consumer claro.

## IDEA-0018 — Pinchazos progresivos

- **Origen:** brainstorming 2026-09-10, idea 70.
- **Estado:** `OPEN IDEA / LOW CONFIDENCE`.
- **Intención:** un neumático dañado podría perder presión gradualmente en lugar de pasar instantáneamente de sano a destruido.
- **Restricción conocida:** depende de que presión/neumáticos tengan gameplay suficiente; no diseñarlo anticipadamente.
- **Por decidir:** relación con damage de vehículo, reparación, tiempo de pérdida y feedback al jugador.

---

## Regla de promoción

- `OPEN IDEA / LOW CONFIDENCE` no significa aprobado.
- No crear tareas, milestones, arquitectura preventiva ni código basándose sólo en este archivo.
- Cuando Mauro confirme explícitamente una idea, moverla o reformularla como `IMPL-*` en `Implementation_Backlog.md` con alcance, trigger, límites y relaciones concretas.
- Si una idea se descarta explícitamente, puede eliminarse de este pool o marcarse `REJECTED` si conservar el historial aporta valor.
