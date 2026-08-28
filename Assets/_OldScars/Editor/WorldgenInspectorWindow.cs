using System;
using System.IO;
using OldScars.Core.World;
using UnityEditor;
using UnityEngine;

namespace OldScars.EditorTools
{
    /// <summary>
    /// Permanent diagnostic-only inspector for the committed logical passes.
    /// It never publishes a WorldSession or becomes a generation authority.
    /// </summary>
    public sealed class WorldgenInspectorWindow : EditorWindow
    {
        private const int PanelSize = 256;
        private string seedText = "8675309123456789";
        private WorldSizePreset size = WorldSizePreset.Large;
        private LandCoveragePreset coverage = LandCoveragePreset.High;
        private Texture2D preview;
        private string status;
        private Vector2 scroll;

        [MenuItem("Old Scars/Worldgen Inspector")]
        public static void Open()
        {
            var window = GetWindow<WorldgenInspectorWindow>();
            window.titleContent = new GUIContent("Worldgen Inspector");
            window.minSize = new Vector2(760f, 620f);
            window.Show();
        }

        private void OnDisable()
        {
            if (preview != null) DestroyImmediate(preview);
            preview = null;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("OLD SCARS — WORLDGEN INSPECTOR", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Diagnostic preview only. Panels: Elevation | Landforms | Thermal | Moisture | Primary Environment; " +
                "Gradient/Suitability | Water/Coast | Drainage/Basins | Ecotone Transition | Human Infrastructure. " +
                "Human roads are global macro truth; optional sector markers are not road endpoints.",
                MessageType.Info);
            seedText = EditorGUILayout.TextField("Signed 64-bit Seed", seedText);
            size = (WorldSizePreset)EditorGUILayout.EnumPopup("World Size", size);
            coverage = (LandCoveragePreset)EditorGUILayout.EnumPopup("Land Coverage", coverage);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Preview", GUILayout.Height(30f)))
                GeneratePreview();
            GUI.enabled = preview != null;
            if (GUILayout.Button("Export PNG…", GUILayout.Height(30f)))
                ExportInteractive();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, MessageType.None);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (preview != null)
            {
                float width = Mathf.Min(position.width - 30f, preview.width);
                float height = width * preview.height / preview.width;
                GUILayout.Label(preview, GUILayout.Width(width), GUILayout.Height(height));
            }
            EditorGUILayout.EndScrollView();
        }

        private void GeneratePreview()
        {
            if (!WorldSeed.TryParse(seedText, out WorldSeed seed, out string seedError))
            {
                status = "Invalid seed: " + seedError + ".";
                return;
            }
            string temporary = Path.Combine(
                Path.GetTempPath(), "OldScars_WorldgenInspector_" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                Generate(seed, temporary, out MacroWorldPlan plan,
                    out MacroGeographyPlan geography, out MacroWaterPlan water,
                    out MacroClimatePlan climate,
                    out MacroEnvironmentPlan environment,
                    out WorldGameplayQualityAnalysis quality, out SectorId starter,
                    out MacroHumanGeographyPlan human);
                byte[] bytes = File.ReadAllBytes(temporary);
                if (preview != null) DestroyImmediate(preview);
                preview = new Texture2D(2, 2, TextureFormat.RGB24, false, true)
                {
                    name = "OldScarsWorldgenInspectorPreview"
                };
                if (!preview.LoadImage(bytes, true))
                    throw new InvalidOperationException("Unity could not decode the exported preview PNG.");
                status = "Generated " + size + "/" + coverage +
                         " | plan " + plan.CanonicalHash.Substring(0, 12) +
                         " | geography " + geography.CanonicalHash.Substring(0, 12) +
                         " | water " + water.CanonicalHash.Substring(0, 12) +
                         " | climate " + climate.CanonicalHash.Substring(0, 12) +
                         " | environment " + environment.CanonicalHash.Substring(0, 12) +
                         " | moisture " + climate.PrevailingMoistureDirection +
                         " | human " + human.CanonicalHash.Substring(0, 12) +
                         " | hubs " + human.RegionalHubCount + "/" + human.LocalHubCount +
                         " | roads " + human.PrimaryRoadCount + "/" + human.SecondaryRoadCount +
                         " | starters " + quality.SuitableStarterCandidateCount +
                         " | selected " + starter.Canonical;
            }
            catch (Exception exception)
            {
                status = "Preview failed: " + exception.Message;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private void ExportInteractive()
        {
            if (!WorldSeed.TryParse(seedText, out WorldSeed seed, out string seedError))
            {
                status = "Invalid seed: " + seedError + ".";
                return;
            }
            string path = EditorUtility.SaveFilePanel(
                "Export Old Scars Worldgen Preview", string.Empty,
                "OldScars_Worldgen_" + size + "_" + coverage + ".png", "png");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                Generate(seed, path, out _, out _, out _, out _, out _, out _, out _, out _);
                status = "Exported preview: " + path;
            }
            catch (Exception exception)
            {
                status = "Export failed: " + exception.Message;
            }
        }

        private void Generate(
            WorldSeed seed,
            string outputPath,
            out MacroWorldPlan plan,
            out MacroGeographyPlan geography,
            out MacroWaterPlan water,
            out MacroClimatePlan climate,
            out MacroEnvironmentPlan environment,
            out WorldGameplayQualityAnalysis quality,
            out SectorId starter,
            out MacroHumanGeographyPlan human)
        {
            var context = new WorldGenerationContext(
                seed, GeneratorVersion.Parse(WorldSessionBootstrap.CurrentGeneratorVersion));
            if (!MacroWorldPlanGenerator.TryGenerate(
                    context, WorldGenerationSettings.ResolvePreset(size), out plan, out string planError))
                throw new InvalidOperationException(planError);
            if (!MacroGeographyGenerator.TryGenerate(
                    context, plan, out geography, out string geographyError))
                throw new InvalidOperationException(geographyError);
            if (!MacroWaterGenerator.TryGenerate(
                    plan, geography, coverage, out water, out string waterError))
                throw new InvalidOperationException(waterError);
            if (!MacroClimateGenerator.TryGenerate(
                    context, plan, geography, water,
                    out climate, out string climateError))
                throw new InvalidOperationException(climateError);
            if (!MacroEnvironmentGenerator.TryGenerate(
                    context, climate, water,
                    out environment, out string environmentError))
                throw new InvalidOperationException(environmentError);
            if (!WorldGameplayQualityAnalyzer.TryAnalyze(
                    plan, geography, water, out quality, out string qualityError))
                throw new InvalidOperationException(qualityError);
            if (!WorldStarterSectorSelector.TrySelect(quality, out starter, out string starterError))
                throw new InvalidOperationException(starterError);
            if (!MacroHumanGeographyGenerator.TryGenerate(
                    context, plan, geography, water, quality, starter,
                    out human, out string humanError))
                throw new InvalidOperationException(humanError);
            MacroGeographyPreviewExporter.Export(
                plan, geography, water, climate, environment, quality, human, starter,
                outputPath, PanelSize, PanelSize, true);
        }
    }
}
