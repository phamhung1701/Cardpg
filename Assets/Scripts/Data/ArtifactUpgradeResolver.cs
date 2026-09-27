using System;
using System.Collections.Generic;
using System.Linq;

public static class ArtifactUpgradeResolver
{
    public static string ValidateCatalog(IReadOnlyList<RelicData> catalog)
    {
        if (catalog == null) return "Artifact catalog is missing";
        var byCanonical = new Dictionary<string, RelicData>(StringComparer.Ordinal);
        foreach (var item in catalog)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.canonicalId)) continue;
            if (!byCanonical.TryAdd(item.canonicalId, item)) return $"Duplicate artifact canonicalId: {item.canonicalId}";
        }
        foreach (var item in byCanonical.Values)
        {
            if (item.tier < 1) return $"Artifact tier must be positive: {item.canonicalId}";
            if (string.IsNullOrEmpty(item.upgradeFromId)) continue;
            if (!byCanonical.TryGetValue(item.upgradeFromId, out var parent)) return $"Missing artifact parent {item.upgradeFromId} for {item.canonicalId}";
            if (parent.canonicalId == item.canonicalId || parent.tier + 1 != item.tier) return $"Invalid artifact tier link: {item.canonicalId}";
        }
        foreach (var item in byCanonical.Values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var current = item;
            while (current != null && !string.IsNullOrEmpty(current.upgradeFromId))
            {
                if (!seen.Add(current.canonicalId)) return $"Artifact upgrade cycle at {item.canonicalId}";
                byCanonical.TryGetValue(current.upgradeFromId, out current);
            }
        }
        return string.Empty;
    }

    public static bool CanReplace(RelicData predecessor, RelicData replacement,
        IReadOnlyList<RelicData> owned, IReadOnlyList<RelicData> catalog, out string reason)
    {
        reason = ValidateCatalog(catalog);
        if (!string.IsNullOrEmpty(reason)) return false;
        if (predecessor == null || replacement == null || owned == null) { reason = "Artifact unavailable"; return false; }
        if (predecessor.canonicalId != replacement.upgradeFromId || replacement.tier <= predecessor.tier)
        { reason = "Not an immediate artifact upgrade"; return false; }
        if (!owned.Contains(predecessor)) { reason = "Predecessor not owned"; return false; }
        if (owned.Any(x => x != null && x.canonicalId == replacement.canonicalId))
        { reason = "Artifact upgrade already owned"; return false; }
        reason = string.Empty;
        return true;
    }

    public static RelicData FindImmediateUpgrade(RelicData owned, IReadOnlyList<RelicData> catalog) =>
        owned == null || catalog == null ? null : catalog.FirstOrDefault(x => x != null &&
            string.Equals(x.upgradeFromId, owned.canonicalId, StringComparison.Ordinal));
}
