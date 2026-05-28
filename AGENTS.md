# Old Scars — instrucciones para Codex

Este proyecto es un juego desarrollado en Unity con C#.

Codex debe actuar como asistente de programación, no como director creativo autónomo.

## Reglas principales del proyecto

- Old Scars usa arquitectura data-driven.
- JSON define contenido.
- C# ejecuta lógica.
- IDs conectan archivos.
- Tags conectan sistemas.
- Las definiciones viven en JSON.
- Las instancias viven en runtime o en el sistema de guardado.
- No hardcodear objetos concretos como Crowbar.cs, Door.cs o Rifle.cs.
- No usar ScriptableObjects como base principal del contenido moddable.
- No agregar sistemas grandes sin pedir confirmación primero.

## Filtro de Old Scars

Toda propuesta de código, sistema, sprint o mecánica debe respetar la dirección de Old Scars.

Antes de proponer o implementar algo, Codex debe verificar:

- si ayuda al núcleo del juego;
- si respeta sistemas ya construidos;
- si no perjudica mecánicas futuras probables;
- si no mete sistemas prematuros;
- si no convierte el juego en otra cosa;
- si mantiene la arquitectura data-driven con JSON, tags, IDs y C# ejecutando lógica;
- si aporta a una base jugable real y ordenada.

No proponer ramas grandes como IA compleja, combate completo, inventario final, UI final, save system, mapa grande o scripting libre en JSON salvo que el usuario lo pida explícitamente o el roadmap indique que llegó el momento.

## Milestone actual

Para saber el milestone actual, Codex debe leer:

- Docs/Project_Roadmap.md
- Docs/Current_Milestone.md

El alcance permitido para cada sprint debe derivarse del milestone actual y de los documentos de planificación del proyecto.

Antes de proponer o implementar un sprint, Codex debe comparar la propuesta contra milestones ya validados, restricciones actuales, decisiones técnicas vigentes y el checklist de incongruencias de Docs/Project_Roadmap.md.

## No implementar todavía

- inventario;
- equipamiento runtime;
- entidades runtime;
- loot tables;
- save system;
- IA;
- combate real;
- UI;
- sonido, ruido o detección;
- armas de fuego completas;
- munición;
- protección;
- mundo procedural.

## Reglas de JSON

- Los IDs deben usar snake_case.
- No usar espacios, mayúsculas ni guiones en IDs.
- Los IDs duplicados son errores fuertes.
- El campo type debe validarse.
- Los tags usados deben existir en tags.json.
- Las referencias rotas deben dar error.
- No guardar lógica compleja dentro de JSON.
- No leer JSON constantemente durante gameplay; se carga al inicio.

## Reglas de implementación

Antes de cada sprint nuevo, Codex debe leer también:

- Docs/Project_Roadmap.md
- Docs/Current_Milestone.md
- Docs/Development_Log.md
- Docs/Next_Sprints.md
- Docs/DataDriven_JSON_Rules.md

Antes de modificar código, Codex debe:

1. Leer este archivo.
2. Revisar los archivos existentes.
3. Explicar brevemente qué va a cambiar.
4. Hacer cambios pequeños y revisables.
5. No agregar sistemas fuera del milestone actual.
