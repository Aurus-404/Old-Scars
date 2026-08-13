# Old Scars - Current Milestone

Este archivo es un snapshot operativo breve. La autoridad de IDs, estados, dependencias y gates es [Project_Roadmap.md](Project_Roadmap.md). La cronología y evidencia permanecen en [Development_Log.md](Development_Log.md).

## Estado Actual

### M41.0 — Navigation & Perception Foundation

Estado:

`IMPLEMENTED — AUTOMATED NAVIGATION / PERCEPTION VALIDATION PASSED; MANUAL UNITY VALIDATION PENDING`

M40.1 permanece `DONE — ARMOR / PENETRATION V1 VALIDATED` y `Combat Ready — APPROVED`. M41.0 implementa capacidades separadas y data-driven de navegación NPC y percepción visual explicable sobre identidad/lifecycle M38. M41.1 permanece `PLANNED` y no iniciado.

## Evidencia Automatizada

- Runtime/Editor compile: `PASS`; permanecen seis warnings C# preexistentes y no atribuibles a M41.0.
- Data validation: `PASS` dentro del diagnóstico con `GameDataManager.Report.HasErrors == false`.
- `M41.0 Navigation & Perception Diagnostics: PASS`: spawn/registry, configuración de profile, destino alcanzable con desplazamiento y `Reached`, destinos fuera del NavMesh con `Failed` estable, muerte que detiene el path, autoridad del player intacta, range/FOV/LOS, barrera opaca, self, child collider, restore y estado `Idle`.
- Regresión directa `M38.0 Actor Runtime & Lifecycle Diagnostics: PASS`.
- `SampleScene` contiene una fixture aislada y un NavMesh bakeado reproducible para la validación espacial.

## Contratos Implementados

- `ActorNavigationController` posee exclusivamente la orden efímera, destino y estados `Idle`, `Moving`, `Reached` y `Failed`; usa `NavMeshAgent`, no teleporta, no reintenta y rechaza actores `Dead`.
- `ActorVisualPerceptionService` es independiente de Navigation y Combat; evalúa identidad, lifecycle, range, FOV horizontal y LOS físico y devuelve causa, IDs, posición, distancia, ángulo, blocker y timestamp de `WorldClock` cuando existe.
- Los bloques opcionales `navigation` y `visual_perception` de `ActorProfileDefinition` declaran capacidades y tuning. Su ausencia no agrega componentes ni defaults productivos ocultos.
- `ActorProfileComponent` aplica las capacidades en bootstrap y restore. El player conserva `CharacterController`, `PlayerMovementController` y `PlayerMovementInputController` sin recibir navegación NPC.
- Orden/path/resultados de percepción permanecen efímeros. Current Slice no cambia schema/envelope; tras restore la pose durable se aplica y Navigation queda estable en `Idle`.

## Validación Manual Pendiente

1. Abrir `SampleScene`, entrar en Play y ejecutar `Old Scars/Diagnostics/AI/M41.0 Prepare Manual Validation`.
2. Confirmar que la cápsula rodea la barrera, no la atraviesa y termina detenida en `Reached`.
3. Ejecutar `M41.0 Toggle Manual Perception Blocker`: activa debe informar `Occluded`; inactiva, `Perceived`.
4. Confirmar que la Console no muestra errores M41.0.

## Próximo Trabajo

Cerrar la validación manual fresh-session de M41.0. M41.1 — Human Encounter AI V1 permanece `PLANNED`, no iniciado y fuera del alcance actual.
