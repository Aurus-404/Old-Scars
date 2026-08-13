# Old Scars - Next Sprints

Este documento contiene sólo los próximos trabajos reales. El trabajo activo se resume en [Current_Milestone.md](Current_Milestone.md); los IDs, estados, dependencias y gates se derivan de [Project_Roadmap.md](Project_Roadmap.md).

## Próximo Trabajo

### 1. M40.1 — Manual Unity Validation & Closeout

Estado: `IMPLEMENTED — AUTOMATED ARMOR / PENETRATION VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`.

M40.0 está `DONE — COMBAT RESOLUTION & WEAPONS V1 VALIDATED`. `Persistence Ready` permanece `APPROVED`; `Combat Ready` permanece `PENDING MANUAL M40.1 CLOSEOUT`.

La implementación y automatización están congeladas. El único trabajo siguiente es el recheck manual fresh-session; no iniciar M41.0 antes de completarlo.

Checklist práctico para Mauro:

1. Entrar a Play Mode y preparar/equipar el Lee-Enfield con `.303` mediante el flujo M40 ya validado.
2. Ejecutar una vez `Old Scars > Diagnostics > Combat > M40.1 Prepare or Cycle Manual Armor Target`.
3. Confirmar en Console el target, sus dos `ArmorInstanceIds` y `Mode='StoppedTwoLayers'`; ambas piezas deben estar en Equipment, no sólo en inventory.
4. Disparar Torso y confirmar `Stopped`.
5. Confirmar que ese impacto no crea `Puncture`.
6. Confirmar una única `Blunt` por transferencia de trauma.
7. Disparar Head con la misma armor y confirmar `Unarmored`/`Puncture`; repetir opcionalmente brazo o pierna para aislamiento regional.
8. Ejecutar el mismo menú una vez más y confirmar `Mode='PenetratedOneLayer'`.
9. Disparar Torso con `.303` y confirmar `Penetrated`.
10. Confirmar exactamente una nueva `Puncture` residual para ese impacto.
11. Ejecutar el menú otra vez y confirmar `Mode='UnarmoredInventoryOnly'`.
12. Confirmar que las piezas sólo en inventory no protegen y que Torso vuelve al comportamiento unarmored M40.
13. Ejecutar el menú otra vez para volver a `StoppedTwoLayers`.
14. Golpear Torso directamente con crowbar y confirmar una única consecuencia `Blunt` residual coherente.
15. Colocar cobertura opaca inmediata entre shooter y target, disparar y confirmar que bloquea antes de armor/actor.
16. Volver a una línea limpia y confirmar que el impacto llega otra vez al target.
17. Dejar `StoppedTwoLayers`, registrar ActorInstanceId y ambos ArmorInstanceIds, y guardar Current Slice.
18. Salir completamente de Play Mode.
19. Entrar a una sesión fresh Play y cargar Current Slice.
20. Confirmar el mismo actor, los mismos dos ArmorInstanceIds y ambas piezas equipadas.
21. Repetir el disparo de Torso y confirmar que vuelve a producir `Stopped` sin `Puncture`.
22. Confirmar Console sin errores atribuibles a M40.1 y reportar el resultado para decidir el closeout de `Combat Ready`.

## Dirección De Producción

El pequeño playable exploration prototype no está iniciado. Después del closeout aplicable podrá reutilizar la infraestructura existente para evaluar gameplay y presentación; no es una vertical slice final, no recibe milestone ID nuevo y no adelanta M45.1.

## Secuencia De Modding Posterior

La foundation actual no implementa el sistema completo. Las próximas piezas se planifican como unidades separadas en este orden conceptual:

`manifest → provenance → dependencies → patches`

M50.0 conserva el alcance de compatibilidad de producción; ID TBD — Global Content ID Namespace Foundation no lo sustituye ni lo marca iniciado.

## No Iniciar Todavía

- nuevas ampliaciones OnGUI sin milestone autorizado;
- UI final;
- M41.0, AI combat o cualquier ampliación posterior antes del closeout manual M40.1;
- condition, repair o crafting;
- actores o mundo a escala fuera del seam mínimo implementado por M38.0;
- facciones amplias;
- generación procedural;
- producción masiva de contenido.
