using System.Text.RegularExpressions;
using UnityEngine;

namespace OldScars.Core.Identity
{
    [DisallowMultipleComponent]
    public sealed class PersistentSceneObjectId : MonoBehaviour
    {
        private static readonly Regex ValidId = new Regex(
            "^[a-z0-9]+(?:_[a-z0-9]+)*$",
            RegexOptions.CultureInvariant);

        [SerializeField] private string persistentId;

        public string PersistentId => persistentId;

        public static bool IsValidFormat(string value)
        {
            return !string.IsNullOrEmpty(value) && ValidId.IsMatch(value);
        }
    }
}
