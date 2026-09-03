# Old Scars — NPC Combat Targeting & Accuracy Research

Fecha de reconciliación: 2026-09-03.

Este documento conserva la investigación que cambió el plan de Fase 8. Separa evidencia del repo, patrones externos y decisiones de diseño técnico. No afirma que las propuestas descritas aquí ya estén implementadas.

## 1. Hallazgos confirmados en el repo actual

### 1.1 La foundation NPC saneada debe conservarse

Las Fases 2–7 corrigieron los problemas estructurales observados en Prueba 2 sin necesidad de Behavior Trees/GOAP/Utility AI:

- `ActorBehaviorController`: ownership normal de navegación `Ambient / Encounter / Search / Inactive`;
- `ActorNavigationController`: autoridad técnica de movimiento/NavMesh;
- `ActorGazeController`: atención lógica bounded y tracking de movimiento observado;
- `ActorVisualPerceptionService`: range + FOV centrado en Current Gaze + LOS físico;
- `ActorThreatAcquisitionController`: discovery/recognition/threat sin exigir anatomía humana;
- `HumanEncounterAIController`: orquestación de encounter/search y ejecución de combate actual;
- `WeaponCombatService` + `PhysicalShotPathResolver`: ruta compartida de ataque físico;
- `ActorCombatHitRegion`: identidad anatómica explícita del collider impactado;
- health/medical/condition/vital siguen siendo las autoridades de consecuencias.

No hay evidencia que justifique otra reconstrucción global de IA en este momento.

### 1.2 El punto base de aim actual está en una frontera equivocada

Después de Fase 7, `HumanEncounterAIController` sigue seleccionando para aim el collider de locomoción y usa su `bounds.center` como `aimPoint`, mientras los nuevos `ActorCombatHitRegion` se ignoran deliberadamente para preservar la semántica histórica de aim/perception durante la migración.

En el humano estándar, la cápsula locomotora tiene el centro más bajo que el centro del hitbox `Torso`. La geometría publicada coloca aproximadamente:

- aim histórico: centro de locomotion capsule, alrededor de `y = 0` local;
- centro explícito de Torso: alrededor de `y = +0.18` local;
- comienzo superior aproximado de piernas: alrededor de `y = -0.11` local.

Por tanto, el aim actual queda sólo ~0.11 m por encima de las piernas, mientras un center-mass de torso queda ~0.29 m por encima de ese borde. Con un cono angular real, esta diferencia puede transformar desviaciones normales hacia abajo en impactos de pierna con mucha frecuencia.

Esto eleva la hipótesis de `ISSUE-0008`: el sesgo observado puede provenir principalmente de un base aim point demasiado bajo, amplificado por spread normal, no de un RNG verticalmente sesgado ni de BodyRegion incorrecto.

### 1.3 El spread actual no muestra un sesgo vertical deliberado

`BuildImperfectShotDirection` construye un error radial alrededor de la dirección base. La distribución usa radio derivado de `sqrt(random)` y un ángulo 0–2π, por lo que no se observó una penalización Y negativa deliberada.

Factores actuales de error/precisión que sí existen:

- focus buildup/decay;
- base NPC spread;
- distance penalty;
- target movement penalty;
- shooter movement penalty;
- automatic burst spread/recovery;
- contribución de arma mediante `debug_accuracy_spread`, actualmente 0 en los perfiles principales observados.

No se debe retunear estos factores antes de aislar el error de target point.

### 1.4 Anatomía física y aim intent son preguntas distintas

Fase 7 dejó una separación correcta:

- `ActorLocomotionCollider`: movimiento/collision técnica;
- `ActorCombatHitRegion`: qué región recibió físicamente el impacto;
- visual rig: presentación.

El aim normal no debería volver a acoplarse a `Torso` como conocimiento del shooter. `Torso` describe anatomía del receptor; no es una abstracción válida para jabalíes, perros, mutantes, robots, torretas u otros targets posibles.

## 2. Patrones externos revisados

Estos patrones sirven como referencia conceptual, no como arquitectura a copiar literalmente.

### Source / Half-Life

Source expone una abstracción del lado del target (`BodyTarget`) para devolver un punto razonable al que intentar disparar, mientras el impacto real se resuelve por separado mediante hitgroups/colisión.

Referencia: https://github.com/ValveSoftware/source-sdk-2013/blob/master/src/game/server/baseentity.cpp

Lección aplicable: el shooter no necesita conocer la anatomía interna de todas las clases de objetivo.

### Unreal Engine

`AActor::GetTargetLocation(RequestedBy)` representa explícitamente una ubicación óptima a la que disparar sobre un actor. El hit físico posterior puede informar actor/componente/hueso por separado.

Referencia: https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Runtime/Engine/GameFramework/AActor/GetTargetLocation

Lección aplicable: `target location` y `actual hit location` son contratos distintos.

### Insurgency: Sandstorm

El tuning de IA usa conceptos donde la precisión mejora con tiempo de enfoque/combate y debe evitar snap/pinpoint inmediato; también se ha ajustado precisión/recoil de bursts para legibilidad y reacción.

Referencia general de actualizaciones: https://store.steampowered.com/news/app/581320

Lección aplicable: Focus es una dimensión útil y no debe reemplazarse por un único porcentaje fijo de accuracy.

### S.T.A.L.K.E.R. 2

GSC ha ajustado dispersión NPC por distancia/armas y patrones de disparo, mostrando que weapon/range pueden contribuir al tuning sin sustituir el pipeline físico.

Referencia: https://support.stalker2.com/hc/en-us/articles/32830075654929-Major-Patch-1-2-is-here

Lección aplicable: arma, atacante y contexto pueden aportar error por capas pequeñas; no hace falta un Accuracy framework universal.

## 3. Decisión de arquitectura objetivo

La dirección elegida para Old Scars es:

```text
Threat/Encounter
    ↓
Target
    ↓
Primary Aim Point
    ↓
Shooter focus/context error
    ↓
Weapon data/cadence
    ↓
PhysicalShotPathResolver
    ↓
actual collider hit / world / miss
    ↓
CombatResolution
    ↓
receiver consequences
```

Preguntas y autoridades:

- ¿Quién puedo atacar? → Threat Acquisition.
- ¿Puedo verlo? → Perception.
- ¿Dónde estoy mirando? → Gaze.
- ¿Qué conducta ejecuto? → Behavior/Encounter.
- ¿Dónde es razonable intentar impactar este target? → el target mediante un Primary Aim Point genérico.
- ¿Qué tan bien puede apuntar el atacante? → shooter focus/context error.
- ¿Cómo dispara el arma? → weapon data/cadence.
- ¿Dónde terminó físicamente el disparo? → Physical Shot Path.
- ¿Qué significa el impacto? → Combat Hit Region + Combat Resolution + receiver.

## 4. Primary Aim Point: alcance mínimo previsto

No implementar un manager ni un weak-point framework.

La primera versión debe ser equivalente a un único componente/punto, por ejemplo `ActorPrimaryAimPoint` (nombre final sujeto a conventions del repo).

Ejemplos:

- humano: center mass del torso;
- cuadrúpedo: centro estable del tronco;
- robot: chassis;
- otro actor: punto representativo definido por su prefab/representation.

No asumir `Torso == PrimaryAimPoint`. Un collider puede ser `ActorCombatHitRegion(Torso)` y además tener/acompañar el Primary Aim Point, pero son contratos conceptualmente distintos.

No ampliar todavía JSON, enums de roles, scoring, weak points, head targeting, mobility targeting ni preferencias por arma.

## 5. Factores actuales de accuracy: decisión provisional

Mantener hasta tener evidencia posterior al target-point fix:

- Focus: KEEP.
- Shooter movement penalty: KEEP.
- Target movement penalty: KEEP provisionalmente.
- Automatic burst spread: KEEP V1.
- Distance penalty: REVIEW después del fix; un cono angular ya crece físicamente con distancia y puede existir doble penalización.
- Weapon spread field: REVIEW después del fix. `debug_accuracy_spread` debería convertirse en un contrato productivo mínimo sólo cuando haga falta, no en un `AccuracyProfile` grande.

No crear todavía `ActorAimController`, `AccuracyController`, `FireControlController`, `WeaponHandlingController` ni otros componentes sólo para reducir líneas de `HumanEncounterAIController`.

## 6. Compatibilidad legacy: decisión revisada

La compatibilidad capsule-only introducida durante Fase 7 es transicional, no arquitectura final.

Una vez migrados los consumers/fixtures reales:

- eliminar fallback visual `missing representation → CreatePrimitive(Capsule)`;
- eliminar inferencia anatómica legacy por bounds/hitPoint cuando todos los actores combatibles relevantes tengan `ActorCombatHitRegion`;
- eliminar tests cuya única finalidad sea mantener ese contrato reemplazado.

Mantener `ActorLocomotionCollider` si sigue siendo necesario para NavMesh/collision/avoidance/collapse. La cápsula técnica invisible no equivale a un actor legacy capsule-only.

## 7. Fase 8 revisada

### 8A — Aim Bias Evidence

Sin cambiar gameplay: instrumentar aim NPC real y medir `aimPoint`, source, spread, direction, hit collider, hit point, BodyRegion/miss. Comparar locomotion center con proposed human center-mass bajo mismas condiciones/seeds.

### 8B — Generic Target-side Primary Aim Point

Sólo si 8A confirma que el seam actual contribuye al problema: introducir el punto genérico del target y dejar de usar locomotion center para firearm aim normal.

### 8C — Controlled Before/After

Repetir exactamente la muestra de 8A con mismos seeds/condiciones. No retunear spread, damage ni anatomy. Resolver `ISSUE-0008` sólo si la distribución queda explicada y el sesgo anómalo desaparece o se demuestra otra causa.

### 8D — Accuracy Simplification Review

Revisar uno por uno Focus, distance, target/shooter movement, burst y weapon spread. No implementar cambios por defecto; cambiar sólo lo que la evidencia muestre redundante/incorrecto.

### 8E — Legacy Migration/Cleanup

Migrar perfiles/fixtures que todavía dependan del actor capsule-only y luego retirar fallback visual/anatómico reemplazado.

## 8. Lo que no se implementa por esta investigación

- Behavior Trees, GOAP o Utility AI general;
- aim-point scoring/weak-point system;
- headshot AI o body-part selection aleatoria;
- full ballistics, drop, drag, wind o projectile travel sólo para arreglar accuracy;
- morale/suppression/stance/breathing/weapon-skill frameworks;
- attack-method framework para animales/máquinas antes del primer consumidor real;
- machine/vehicle damage receiver genérico antes de existir un target de ese tipo.

## 9. Principio de bounded engineering

Esta investigación no autoriza una nueva reescritura general de IA. El objetivo es quitar del Encounter la responsabilidad claramente mal ubicada de adivinar el center mass del target y luego medir si el modelo de error existente ya es suficiente.

Si el target-point fix resuelve la distribución, no continuar "mejorando accuracy" por inercia.
