using System.IO;
using OldScars.Core.Data;
using OldScars.Core.Data.Loading;
using OldScars.Core.Data.Validation;
using UnityEngine;

namespace OldScars.Core
{
    /// <summary>
    /// Scene entry point for the Milestone 1 data layer.
    /// Place this component on a GameObject in the boot scene.
    ///
    /// Responsibilities:
    /// - Locate StreamingAssets/Mods.
    /// - Load all JSON definitions through GameDataLoader.
    /// - Validate the loaded definitions through DataValidator.
    /// - Expose GameDatabase, TagRegistry and validated content provenance.
    ///
    /// This class does not create gameplay entities, inventories, combat, loot,
    /// save data, or UI. It only prepares immutable definition data.
    /// </summary>
    public sealed class GameDataManager : MonoBehaviour
    {
        public static GameDataManager Instance { get; private set; }

        [Header("Data Loading")]
        [SerializeField] private string modsFolderName = "Mods";
        [SerializeField] private bool haltOnDataErrors = true;
        [SerializeField] private bool dontDestroyOnLoad = true;

        public bool IsReady { get; private set; }
        public GameDatabase Database { get; private set; }
        public TagRegistry Tags { get; private set; }
        public DataLoadReport Report { get; private set; }
        public LoadedContentSet LoadedContentSet { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            LoadGameData();
        }

        [ContextMenu("Reload Game Data")]
        public void LoadGameData()
        {
            IsReady = false;
            LoadedContentSet = null;
            Report = new DataLoadReport();

            string modsRootPath = Path.Combine(Application.streamingAssetsPath, modsFolderName);
            Debug.Log($"[GameDataManager] Mods path: {modsRootPath}");

            var loader = new GameDataLoader(modsRootPath, Report);
            loader.LoadAll();

            Database = loader.Database;
            Tags = loader.Tags;

            var validator = new DataValidator(Database, Tags, Report);
            validator.Validate();

            LoadedContentSet validatedContentSet = null;
            if (!Report.HasErrors)
                loader.TryBuildLoadedContentSet(out validatedContentSet);

            Report.LogSummary();

            if (Report.HasErrors)
            {
                if (haltOnDataErrors)
                {
                    Debug.LogError("[GameDataManager] CoreDataSystem failed. Fix data errors before gameplay starts.");
                    return;
                }

                Debug.LogWarning("[GameDataManager] CoreDataSystem has errors, but haltOnDataErrors is disabled.");
            }

            LoadedContentSet = validatedContentSet;
            IsReady = true;
            if (LoadedContentSet != null)
            {
                Debug.Log(
                    "[GameDataManager] CoreDataSystem ready. Loaded content set provenance: " +
                    LoadedContentSet.ProvenanceFingerprint);
            }
            else
            {
                Debug.LogWarning(
                    "[GameDataManager] CoreDataSystem continued because haltOnDataErrors is disabled; " +
                    "no validated LoadedContentSet was published.");
            }
        }
    }
}
