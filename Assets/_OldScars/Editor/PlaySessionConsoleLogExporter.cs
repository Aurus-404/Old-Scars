using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldScars.EditorTools
{
    /// <summary>
    /// IMPL-0042. Captures every Unity Console message emitted during one Play session into
    /// a durable text file under the user's Documents/Unity Logs folder.
    ///
    /// The file is opened before Play begins, appended as messages arrive, survives assembly
    /// reloads through SessionState, and is finalized after Unity returns to Edit mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlaySessionConsoleLogExporter
    {
        private const string SessionActiveKey = "OldScars.PlaySessionLog.Active";
        private const string SessionPathKey = "OldScars.PlaySessionLog.Path";
        private const string SessionStartedLocalKey = "OldScars.PlaySessionLog.StartedLocal";
        private const string FilePrefix = "OldScars_Play_";
        private const string FileExtension = ".txt";
        private const int MaxRetainedLogs = 50;

        private static readonly object FileGate = new object();

        private static bool sessionActive;
        private static string currentFilePath;
        private static DateTimeOffset startedLocal;
        private static StreamWriter writer;
        private static string deferredIoError;

        static PlaySessionConsoleLogExporter()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

            Application.logMessageReceivedThreaded -= HandleLogMessage;
            Application.logMessageReceivedThreaded += HandleLogMessage;

            AssemblyReloadEvents.beforeAssemblyReload -= CloseWriterForAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += CloseWriterForAssemblyReload;

            EditorApplication.quitting -= HandleEditorQuitting;
            EditorApplication.quitting += HandleEditorQuitting;

            RestoreSessionState();
        }

        [MenuItem("Tools/Old Scars/QA/Open Unity Logs Folder")]
        private static void OpenUnityLogsFolder()
        {
            string directory = GetLogDirectory();
            Directory.CreateDirectory(directory);
            EditorUtility.RevealInFinder(directory);
        }

        [MenuItem("Tools/Old Scars/QA/Open Current Play Session Log", true)]
        private static bool ValidateOpenCurrentPlaySessionLog()
        {
            return sessionActive && !string.IsNullOrWhiteSpace(currentFilePath) && File.Exists(currentFilePath);
        }

        [MenuItem("Tools/Old Scars/QA/Open Current Play Session Log")]
        private static void OpenCurrentPlaySessionLog()
        {
            if (!ValidateOpenCurrentPlaySessionLog())
                return;

            EditorUtility.RevealInFinder(currentFilePath);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    BeginSession();
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    EndSession("Play/Stop completed");
                    break;
            }
        }

        private static void BeginSession()
        {
            if (sessionActive)
                EndSession("Previous Play session interrupted before a new session began");

            try
            {
                string directory = GetLogDirectory();
                Directory.CreateDirectory(directory);
                PruneOldLogs(directory);

                startedLocal = DateTimeOffset.Now;
                currentFilePath = CreateUniqueLogPath(directory, startedLocal);
                deferredIoError = null;
                sessionActive = true;
                PersistSessionState();

                WriteRaw(BuildHeader(startedLocal));
            }
            catch (Exception exception)
            {
                sessionActive = false;
                currentFilePath = null;
                ClearSessionState();
                Debug.LogError("[PlaySessionLogExporter] Could not start Play-session log export. " + exception.Message);
            }
        }

        private static void EndSession(string reason)
        {
            if (!sessionActive)
                return;

            DateTimeOffset endedLocal = DateTimeOffset.Now;
            try
            {
                WriteRaw(BuildFooter(endedLocal, reason));
            }
            catch (Exception exception)
            {
                deferredIoError = exception.Message;
            }

            string completedPath = currentFilePath;
            string ioError = deferredIoError;

            sessionActive = false;
            currentFilePath = null;
            deferredIoError = null;
            ClearSessionState();
            CloseWriter();

            if (!string.IsNullOrWhiteSpace(ioError))
            {
                Debug.LogWarning("[PlaySessionLogExporter] Play-session log finished with an I/O warning: " + ioError);
            }
            else if (!string.IsNullOrWhiteSpace(completedPath))
            {
                Debug.Log("[PlaySessionLogExporter] Saved Play-session log: " + completedPath);
            }
        }

        private static void HandleEditorQuitting()
        {
            if (sessionActive)
                EndSession("Unity Editor quitting");
        }

        private static void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!sessionActive || string.IsNullOrWhiteSpace(currentFilePath))
                return;

            try
            {
                var builder = new StringBuilder(256 + (condition?.Length ?? 0) + (stackTrace?.Length ?? 0));
                builder.Append('[')
                    .Append(DateTimeOffset.Now.ToString("HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
                    .Append("] [")
                    .Append(type.ToString().ToUpperInvariant())
                    .AppendLine("]");
                builder.AppendLine(condition ?? string.Empty);

                if (!string.IsNullOrWhiteSpace(stackTrace))
                {
                    builder.AppendLine("--- STACK TRACE ---");
                    builder.AppendLine(stackTrace.TrimEnd());
                }

                builder.AppendLine("---");
                WriteRaw(builder.ToString());
            }
            catch (Exception exception)
            {
                // Never emit a Unity log from inside the log callback: that could recurse.
                deferredIoError = exception.Message;
            }
        }

        private static string BuildHeader(DateTimeOffset started)
        {
            string projectPath = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string scenePath = SceneManager.GetActiveScene().path;
            string playModeOptions = EditorSettings.enterPlayModeOptionsEnabled
                ? EditorSettings.enterPlayModeOptions.ToString()
                : "Default (domain + scene reload)";

            var builder = new StringBuilder();
            builder.AppendLine("# Old Scars — Unity Play Session Console Log");
            builder.AppendLine("# IMPL-0042");
            builder.AppendLine("Started local: " + started.ToString("O", CultureInfo.InvariantCulture));
            builder.AppendLine("Started UTC:   " + started.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            builder.AppendLine("Project:       " + projectPath);
            builder.AppendLine("Unity:         " + Application.unityVersion);
            builder.AppendLine("Scene:         " + (string.IsNullOrWhiteSpace(scenePath) ? "<UNSAVED/NONE>" : scenePath));
            builder.AppendLine("Play options:  " + playModeOptions);
            builder.AppendLine("Log folder:    " + GetLogDirectory());
            builder.AppendLine(new string('=', 78));
            return builder.ToString();
        }

        private static string BuildFooter(DateTimeOffset ended, string reason)
        {
            TimeSpan duration = startedLocal == default ? TimeSpan.Zero : ended - startedLocal;
            var builder = new StringBuilder();
            builder.AppendLine(new string('=', 78));
            builder.AppendLine("Ended local:   " + ended.ToString("O", CultureInfo.InvariantCulture));
            builder.AppendLine("Ended UTC:     " + ended.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            builder.AppendLine("Duration:      " + duration.ToString("c", CultureInfo.InvariantCulture));
            builder.AppendLine("End reason:    " + reason);
            return builder.ToString();
        }

        private static void WriteRaw(string text)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(currentFilePath))
                return;

            lock (FileGate)
            {
                EnsureWriter();
                writer.Write(text);
                writer.Flush();
            }
        }

        private static void EnsureWriter()
        {
            if (writer != null)
                return;

            string directory = Path.GetDirectoryName(currentFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var stream = new FileStream(
                currentFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);
            writer = new StreamWriter(stream, new UTF8Encoding(false))
            {
                AutoFlush = true
            };
        }

        private static void CloseWriterForAssemblyReload()
        {
            CloseWriter();
        }

        private static void CloseWriter()
        {
            lock (FileGate)
            {
                if (writer == null)
                    return;

                try
                {
                    writer.Flush();
                    writer.Dispose();
                }
                finally
                {
                    writer = null;
                }
            }
        }

        private static void RestoreSessionState()
        {
            sessionActive = SessionState.GetBool(SessionActiveKey, false);
            currentFilePath = SessionState.GetString(SessionPathKey, string.Empty);

            string started = SessionState.GetString(SessionStartedLocalKey, string.Empty);
            if (!DateTimeOffset.TryParse(started, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out startedLocal))
                startedLocal = default;

            if (!sessionActive || string.IsNullOrWhiteSpace(currentFilePath))
            {
                sessionActive = false;
                currentFilePath = null;
                return;
            }

            try
            {
                EnsureWriter();
            }
            catch (Exception exception)
            {
                deferredIoError = exception.Message;
            }
        }

        private static void PersistSessionState()
        {
            SessionState.SetBool(SessionActiveKey, sessionActive);
            SessionState.SetString(SessionPathKey, currentFilePath ?? string.Empty);
            SessionState.SetString(
                SessionStartedLocalKey,
                startedLocal == default ? string.Empty : startedLocal.ToString("O", CultureInfo.InvariantCulture));
        }

        private static void ClearSessionState()
        {
            SessionState.EraseBool(SessionActiveKey);
            SessionState.EraseString(SessionPathKey);
            SessionState.EraseString(SessionStartedLocalKey);
        }

        private static string GetLogDirectory()
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documents))
                documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(documents))
                documents = Directory.GetCurrentDirectory();

            return Path.Combine(documents, "Unity Logs");
        }

        private static string CreateUniqueLogPath(string directory, DateTimeOffset started)
        {
            string stem = FilePrefix + started.ToString("yyyy-MM-dd_HH-mm-ss-fff", CultureInfo.InvariantCulture);
            string candidate = Path.Combine(directory, stem + FileExtension);
            int suffix = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(directory, stem + "_" + suffix.ToString("00", CultureInfo.InvariantCulture) + FileExtension);
                suffix++;
            }

            return candidate;
        }

        private static void PruneOldLogs(string directory)
        {
            var directoryInfo = new DirectoryInfo(directory);
            FileInfo[] logs = directoryInfo.GetFiles(FilePrefix + "*" + FileExtension, SearchOption.TopDirectoryOnly);
            Array.Sort(logs, (left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));

            for (int index = MaxRetainedLogs - 1; index < logs.Length; index++)
            {
                try
                {
                    logs[index].Delete();
                }
                catch
                {
                    // Retention cleanup is best-effort and must never block Play Mode.
                }
            }
        }
    }
}
