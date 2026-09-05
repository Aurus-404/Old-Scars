# Blood Trails R0 — URP blood mark rendering proof

Fecha: 2026-09-05. Estado: **PASS**, primitive gráfica únicamente.

## Alcance y resultado

Unity `6000.4.6f1`, URP `17.4.0`, Windows Editor batchmode con Direct3D 11 y Radeon RX 560. `PC_RPAsset` es el pipeline global y de Quality PC/Standalone; su renderer por defecto sigue siendo `PC_Renderer`, Forward+ (`m_RenderingMode=2`). Se añadió una única `DecalRendererFeature` activa, manteniendo SSAO y todos sus parámetros.

La prueba carga el **WorldRuntime real** desde una WorldSession temporal, seed `941413001`, Small/High, mediante las autoridades existentes. Fuerza el backend Unity Terrain sólo para este proceso diagnóstico. No guarda escenas ni cambia preferencias de terrain del usuario. El store temporal no es un save del usuario.

**Proceed with Blood Trails V1**, en cuanto a viabilidad de esta primitive en el renderer PC comprobado. R0 no implementa ni valida emisión, integración médica, pooling, trails, AI tracking, persistence, puddles ni spray. Tampoco establece arte final, coste de cientos de decals, otros GPUs/APIs, build de Player o backend volumétrico.

## Configuración serializada

- Feature: `Blood Trails R0 Decal`, activa, técnica **Automatic** (`0`), distancia máxima `50`, decal layers OFF.
- Técnica efectiva observada durante los renders: **DBuffer**. Automatic utiliza Albedo/Normal/MAOS; los parámetros serializados son `dBufferSettings.surfaceData=2`, `screenSpaceSettings.normalBlend=0` (este último no es el path utilizado).
- Un material `Assets/_OldScars/Art/BloodTrailsR0/BloodMarkR0.mat`, shader oficial del paquete `Shader Graphs/Decal` (`Packages/com.unity.render-pipelines.universal/Shaders/Decal.shadergraph`). GPU instancing ON; `Normal_Blend=0`.
- `Base_Map`: textura técnica RGBA de 128×128, roja, irregular, exterior transparente, alpha máximo 0.92, Clamp, sin compresión. Generada mediante Texture2D/PNG, sin arte final.
- Un `DecalProjector` efímero, material compartido, volumen `2×2×0.30`, pivot cero, distancia `50`; eje de proyección orientado contra la normal. Se reutiliza secuencialmente en los tres casos, sin emitter ni pool.

La feature se creó con `ScriptableObject.CreateInstance`, `SerializedObject`, `AssetDatabase.AddObjectToAsset` y `SaveAssets`. El mapa de features usa los local IDs reales obtenidos por `TryGetGUIDAndLocalFileIdentifier`. **No se editó YAML manualmente ni se inventaron GUIDs/fileIDs.**

Diff adicional revisado de la serialización Unity: renderer asset v2→v3; prepass mask hereda opaque mask (todos los layers); formatos de profundidad en default; referencia XR nula; reformateo de referencias. Los antiguos campos serializados de shader/blue noise de SSAO desaparecen porque URP 17.4 resuelve esos recursos mediante sus `ScreenSpaceAmbientOcclusionPersistentResources` / `DynamicResources`. SSAO conserva su subasset, estado activo y settings; el diagnostic comprueba además que preparó su material durante rendering.

## Evidencia visual automática

Pipeline no tenía Editor accesible (`unity status --json`: `STATUS_NO_INSTANCES`). Se usó batchmode **con GPU**, sin `-nographics`, sin `ScreenCapture`, sin controlar una GUI del usuario.

`RenderPipeline.SubmitRenderRequest` + `UniversalRenderPipeline.SingleCameraRequest` renderiza a RenderTexture 512×512; `ReadPixels` obtiene los PNG. Se inspeccionaron las capturas reales. Cada fase espera ocho renders; el mundo se congela temporalmente a timeScale=0 para comparar imágenes estables.

El primer intento comprobó `IsValid` antes de inicializar rendering y falló sin producir evidencia visual. La versión final valida el proyector **después** de las solicitudes URP. El siguiente intento sí renderizó DBuffer y pasó; la ejecución final añade validación de recursos SSAO y detección inmediata de errores durante cada solicitud.

| Caso | Superficie | Píxeles cambiados OFF→ON | Fuera de profundidad vs OFF |
| --- | --- | ---: | ---: |
| Exterior | Terrain/TerrainCollider materializado por WorldRuntime; punto (5,74.67,5), normal aprox. (0.02,1,0) | 13247 | 0 |
| Piso opaco | Cube/Lit, fixture efímera de piso 7×0.3×7 dentro de WorldRuntime | 13320 | 0 |
| Inclinado | El mismo piso a 30°, normal (-0.50,0.87,0) | 13273 | 0 |

El umbral de diferencia suma diferencias RGB >30 por píxel. Se exige una cobertura entre 500 y 35000 píxeles; el control negativo mueve el proyector 1 m a lo largo de la normal y exige menos de 100 píxeles distintos. Los resultados medidos del control negativo fueron exactamente cero. Esto demuestra contribución visible acotada, con transparencia exterior a la mancha, y ausencia de proyección sobre el mismo suelo fuera del volumen.

Capturas originales (no retocadas):

| Caso | OFF | ON | Fuera de profundidad |
| --- | --- | --- | --- |
| Exterior | [OFF](Evidence/BloodTrailsR0/exterior-off.png) | [ON](Evidence/BloodTrailsR0/exterior-on.png) | [Control](Evidence/BloodTrailsR0/exterior-out-of-depth.png) |
| Opaco | [OFF](Evidence/BloodTrailsR0/opaque-floor-off.png) | [ON](Evidence/BloodTrailsR0/opaque-floor-on.png) | [Control](Evidence/BloodTrailsR0/opaque-floor-out-of-depth.png) |
| Inclinado | [OFF](Evidence/BloodTrailsR0/inclined-floor-off.png) | [ON](Evidence/BloodTrailsR0/inclined-floor-on.png) | [Control](Evidence/BloodTrailsR0/inclined-floor-out-of-depth.png) |

La cámara cercana sigue la normal para medir cobertura; la inclinación está establecida por el transform y registrada en el log, no por una vista panorámica. No hubo aceptación manual de Mauro ni prueba de una escena interior de producto; el piso es la fixture representativa autorizada.

## Reproducción y validación

Con el checkout canónico libre de otro Editor Unity abierto, ejecutar el Editor `6000.4.6f1` con estos argumentos desde el proyecto:

```text
-batchmode -projectPath "<checkout canónico>" -force-d3d11 -executeMethod OldScars.Editor.BloodTrailsR0Diagnostics.RunBatch -logFile "<checkout canónico>/Logs/BloodTrailsR0/unity.log"
```

No añadir `-quit`: el diagnostic entra a Play Mode, espera WorldRuntime, captura y termina el proceso con exit code 0/1. No ejecutar con un Editor del usuario abierto ni detenerlo para lanzar la prueba. El diagnostic es batch-only; su inicializador no ejecuta trabajo en una sesión normal.

Requerir el marker fresco `Blood Trails R0 Diagnostics: PASS`, `result.txt=PASS` y las imágenes de esa ejecución en `Logs/BloodTrailsR0`. No reutilizar una captura o PASS viejo tras un fallo. [Extracto de evidencia](Evidence/BloodTrailsR0/validation.txt).

- Compilación Runtime/Editor: PASS, sin errores CS ni warnings de R0.
- Pipeline PC y default renderer, Forward+, SSAO activo con recursos, Automatic decal, material y projector: PASS.
- WorldRuntime, los tres renders y controles negativos de profundidad: PASS.
- No se capturaron errores, exceptions ni asserts de Console durante el escenario; sin errores shader nuevos.
- Warnings fuera de R0: CS0618 en diagnostics existentes, lookup de IDs legacy, assemblies duplicados/assemblies de tests omitidos y mensajes de licensing/entitlements. No bloquean este render; no se corrigieron paquetes ni contratos ajenos.
- Revisión focalizada de diff/código y `git diff --check`: PASS. No hubo servicio dedicado de review disponible; revisión local por el integrador, sin subagentes.

## Git y archivos

Base inicial: `e4a001909b6e83c4f13aaed7ada9127876e89b10`, rama `dev`, igual a `origin/dev` tras fetch, divergencia `0/0`.

Dirty preexistente preservado byte por byte y excluido de stage/commit:

- `Assets/_OldScars/Scripts/Core/Actors/SandboxNpcObservabilityPanel.cs`
- `Assets/_OldScars/Editor/M41F6ObservabilityDiagnostics.cs`
- `Assets/_OldScars/Editor/M41F6ObservabilityDiagnostics.cs.meta`
- `ProjectSettings/ProjectSettings.asset` (runInBackground del usuario)

Archivos de R0: renderer PC; `BloodTrailsR0Diagnostics.cs` y meta; carpeta `Art/BloodTrailsR0` y meta, textura/material y sus metas; este informe, nueve PNG y extracto de validación; párrafo técnico y entrada append-only en Development Log. No cambia ninguna escena, script de gameplay/AI/Medical, package ni ProjectSettings. La publicación se limita a estos archivos con stage explícito.
