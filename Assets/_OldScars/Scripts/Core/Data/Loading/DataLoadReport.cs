using System.Collections.Generic;
using UnityEngine;

namespace OldScars.Core.Data.Loading
{
    /// <summary>
    /// Accumulates loading and validation problems.
    ///
    /// This avoids silent failures: bad JSON, duplicate IDs and broken references
    /// are reported clearly before gameplay systems start using data.
    /// </summary>
    public sealed class DataLoadReport
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();

        public bool HasErrors => errors.Count > 0;
        public int ErrorCount => errors.Count;
        public int WarningCount => warnings.Count;
        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;

        public void Error(string message)
        {
            errors.Add(message);
            Debug.LogError($"[OldScars/Data] ERROR: {message}");
        }

        public void Warning(string message)
        {
            warnings.Add(message);
            Debug.LogWarning($"[OldScars/Data] WARNING: {message}");
        }

        public void LogSummary()
        {
            if (ErrorCount == 0 && WarningCount == 0)
            {
                Debug.Log("[OldScars/Data] Load OK — 0 errors, 0 warnings.");
                return;
            }

            if (ErrorCount > 0)
                Debug.LogError($"[OldScars/Data] Load FAILED — {ErrorCount} error(s), {WarningCount} warning(s).");
            else
                Debug.LogWarning($"[OldScars/Data] Load OK with {WarningCount} warning(s).");
        }
    }
}
