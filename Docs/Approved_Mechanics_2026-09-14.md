# Old Scars — Approved Mechanics / Tooling — 2026-09-14

Documento de continuidad para decisiones aprobadas durante brainstorming. No crea milestones ni altera `Next_Sprints.md`. Reconciliado con `Implementation_Backlog.md` como `IMPL-0042`–`IMPL-0056` el 2026-09-15; las ideas aún no aprobadas quedaron en `Mechanic_Idea_Pool.md` como `IDEA-0019`–`IDEA-0029`.

## Scope decision — WorldClock / TimeScale

- No seguir trabajando ahora en ampliar o corregir gameplay alrededor de TimeScale/WorldClock acelerado.
- No queda descartado para siempre: puede reconsiderarse si vuelve a entrar en la visión del juego.
- Codex no debe abrir trabajo de x10/x100 por inercia.

## Aprobado para implementación futura

### QA / tooling
- Archivo automático `.txt` del Console Log al terminar cada Play/Stop, guardado en carpeta estable accesible a Codex. Ausencia de warnings/errors no equivale a PASS.
- Pruebas save/load disparadas por transiciones concretas de estado para verificar persistence de estados transitorios.
- Auditoría de tamaño de saves por dominio y posterior investigación de optimización basada en evidencia.
- Pruebas de frontera/estrés con valores bajo, sobre y fuera de rango, incluyendo negativos cuando el contrato lo permita.
- Debug spawner de world items directamente al suelo en cantidades grandes, sin pasar por capacidad de inventario; posible extensión a fixtures/estructuras de QA.
- Performance HUD/recorder unificado: FPS, frame time, TPS/frecuencias relevantes, spikes, GC/allocations, Update/FixedUpdate, physics queries y awake/sleep rigidbodies mediante counters baratos u opt-in.
- Detector de rigidbodies que permanecen awake por jitter u otras causas.
- Auditor de instanciación accidental de materiales y reporte de materiales duplicados/equivalentes.
- Budget estructural de humanoides: renderers, materiales, transforms, colliders y componentes relevantes.
- Counters de performance propios de Old Scars integrables al Performance HUD.
- Pruebas largas de memoria con snapshots/object counts comparables.
- Detector de event subscriptions/listeners huérfanos al despawn/destroy.
- Provenance de valores runtime para saber Core/base → override/mod → valor efectivo.
- Detector de conflictos de mods/datos: IDs, overrides, referencias, tags/categorías y orden de carga.
- Herramienta `Who uses this?` para rastrear referencias data-driven.

### Crafting / items / interaction
- Recetas con requisitos flexibles por item exacto, categoría o tag, evitando recetas rígidas duplicadas.
- Requisitos contextuales de crafting: mesa, calor mínimo, agua u otras condiciones justificadas.
- Crafting multietapa como cadena de recetas con ítems intermedios reales, persistibles y lootables; no workflow engine separado.
- Estado On/Off para dispositivos/estructuras que consuman recursos mientras están activos.
- Recursos intercambiables como instancias Provider/Consumer: el proveedor declara tipo/cantidad y el consumidor qué recurso acepta/consume.
- Alarmas, relojes y temporizadores físicos configurables como distracciones o recordatorios.
- Dirección de producto: objetos del mundo deben poder romperse/dañarse completos o por partes cuando su representación lo justifique; preferir estados/partes authored y contracts simples frente a destrucción procedural universal.
- Condición de herramientas afecta gradualmente capacidad/eficiencia antes de llegar a cero; evitar el patrón 1 HP = perfecto / siguiente uso = destruido.
- Acciones pueden consumir recursos/cargas de forma transaccional al ejecutarse.
- Contenedores renombrables/etiquetables por el jugador sin alterar definición ni `canHold`.
- Favorite/Lock de ítems como guardia UX contra transfer/drop/use accidental, sin cambiar ownership real.
- Las autoridades que rechazan acciones deben devolver un motivo estructurado reutilizable por UI/debug; la explicación no es una segunda validación.
- Comparación contextual de equipment basada sólo en diferencias relevantes y datos productivos, sin score global arbitrario.
- Calidad/capacidad de herramienta separada de durabilidad; debe diseñarse junto al efecto gradual de condición.
- Requisitos composables mínimos para acciones: herramienta/capacidad, manos, item/recurso, temperatura/contexto y actor state, agregando tipos sólo cuando exista consumer real.

## No promovido todavía

Quedan fuera de aprobación formal por ahora: presets de escenarios Codex, debug damage applicator y todos los puntos para los que Mauro pidió explicación adicional antes de decidir.
