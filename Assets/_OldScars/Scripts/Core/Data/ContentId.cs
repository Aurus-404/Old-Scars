using System;

namespace OldScars.Core.Data
{
    /// <summary>
    /// Canonical identity for a globally registered content definition.
    ///
    /// Content IDs use namespace:local_id. Local contract IDs, tags, runtime
    /// instance IDs and persistent scene IDs are intentionally different domains.
    /// </summary>
    public readonly struct ContentId : IEquatable<ContentId>
    {
        public const string CoreNamespace = "core";

        private ContentId(string contentNamespace, string localId)
        {
            Namespace = contentNamespace;
            LocalId = localId;
        }

        public string Namespace { get; }
        public string LocalId { get; }
        public string Canonical => Namespace + ":" + LocalId;

        public static bool TryParse(string raw, out ContentId contentId, out string error)
        {
            contentId = default;
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "value is null, empty or whitespace";
                return false;
            }

            if (!string.Equals(raw, raw.Trim(), StringComparison.Ordinal))
            {
                error = "leading or trailing whitespace is not allowed";
                return false;
            }

            int separator = raw.IndexOf(':');
            if (separator < 0)
            {
                error = "missing namespace separator ':'; expected namespace:local_id";
                return false;
            }

            if (raw.IndexOf(':', separator + 1) >= 0)
            {
                error = "multiple namespace separators are not allowed; expected exactly one ':'";
                return false;
            }

            string contentNamespace = raw.Substring(0, separator);
            string localId = raw.Substring(separator + 1);
            if (!TryValidateSegment(contentNamespace, "namespace", out error) ||
                !TryValidateSegment(localId, "local ID", out error))
                return false;

            contentId = new ContentId(contentNamespace, localId);
            return true;
        }

        /// <summary>
        /// Resolves a canonical ID or, only when explicitly enabled, an
        /// unqualified legacy ID against the supplied namespace.
        /// </summary>
        public static bool TryResolve(
            string raw,
            string legacyNamespace,
            bool allowLegacyUnqualified,
            out ContentId contentId,
            out bool usedLegacyCompatibility,
            out string error)
        {
            usedLegacyCompatibility = false;
            if (string.IsNullOrWhiteSpace(raw) ||
                (raw != null && !string.Equals(raw, raw.Trim(), StringComparison.Ordinal)) ||
                (raw != null && raw.IndexOf(':') >= 0))
                return TryParse(raw, out contentId, out error);

            contentId = default;
            error = null;
            if (!allowLegacyUnqualified)
            {
                error = "unqualified legacy IDs are not allowed in this context; expected namespace:local_id";
                return false;
            }

            if (!TryValidateSegment(legacyNamespace, "legacy namespace", out error) ||
                !TryValidateSegment(raw, "legacy local ID", out error))
                return false;

            contentId = new ContentId(legacyNamespace, raw);
            usedLegacyCompatibility = true;
            return true;
        }

        public static bool TryResolveLegacyCore(
            string raw,
            out ContentId contentId,
            out bool usedLegacyCompatibility,
            out string error)
        {
            return TryResolve(
                raw,
                CoreNamespace,
                true,
                out contentId,
                out usedLegacyCompatibility,
                out error);
        }

        /// <summary>
        /// Transitional resolver for EquipmentSlot references authored before
        /// namespaces and before hand_right replaced the historical right_hand.
        /// This semantic alias is intentionally narrower than ContentId parsing.
        /// </summary>
        public static bool TryResolveLegacyCoreEquipmentSlot(
            string raw,
            out ContentId contentId,
            out bool usedLegacyCompatibility,
            out string error)
        {
            if (string.Equals(raw, "right_hand", StringComparison.Ordinal))
            {
                usedLegacyCompatibility = true;
                return TryParse(CoreNamespace + ":hand_right", out contentId, out error);
            }

            return TryResolveLegacyCore(raw, out contentId, out usedLegacyCompatibility, out error);
        }

        public static bool TryValidateLocalId(string value, out string error)
        {
            return TryValidateSegment(value, "local ID", out error);
        }

        public static bool TryValidateNamespace(string value, out string error)
        {
            return TryValidateSegment(value, "namespace", out error);
        }

        private static bool TryValidateSegment(string value, string label, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                error = label + " is null, empty or whitespace";
                return false;
            }

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                error = label + " has leading or trailing whitespace";
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool valid = character >= 'a' && character <= 'z' ||
                             character >= '0' && character <= '9' ||
                             character == '_';
                if (valid)
                    continue;

                error = $"{label} contains invalid character '{character}' at position {index}; " +
                        "only lowercase ASCII letters, digits and '_' are allowed";
                return false;
            }

            return true;
        }

        public bool Equals(ContentId other)
        {
            return string.Equals(Namespace, other.Namespace, StringComparison.Ordinal) &&
                   string.Equals(LocalId, other.LocalId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ContentId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Namespace != null ? StringComparer.Ordinal.GetHashCode(Namespace) : 0) * 397) ^
                       (LocalId != null ? StringComparer.Ordinal.GetHashCode(LocalId) : 0);
            }
        }

        public override string ToString()
        {
            return Canonical;
        }

        public static bool operator ==(ContentId left, ContentId right) => left.Equals(right);
        public static bool operator !=(ContentId left, ContentId right) => !left.Equals(right);
    }
}
