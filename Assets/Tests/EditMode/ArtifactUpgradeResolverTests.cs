using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ArtifactUpgradeResolverTests
{
    readonly List<Object> _created = new();
    RelicData Make(string id, int tier, string parent = null)
    {
        var item = ScriptableObject.CreateInstance<RelicData>();
        _created.Add(item);
        item.canonicalId = id;
        item.tier = tier;
        item.upgradeFromId = parent;
        return item;
    }

    [TearDown] public void TearDown() { foreach (var item in _created) if (item) Object.DestroyImmediate(item); _created.Clear(); }

    [Test] public void ImmediateOwnedPredecessorCanBeReplaced()
    {
        var common = Make("rel_001", 1);
        var rare = Make("rel_025", 2, "rel_001");
        Assert.That(ArtifactUpgradeResolver.CanReplace(common, rare, new[] { common }, new[] { common, rare }, out var reason), Is.True, reason);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RejectsMissingOrNonImmediatePredecessor(bool missing)
    {
        var parent = Make("rel_001", 1);
        var rare = Make("rel_025", 2, missing ? "missing" : "rel_001");
        var epic = Make("rel_034", 3, "rel_025");
        var owned = missing ? new[] { parent } : new[] { parent };
        var catalog = missing ? new[] { parent, rare } : new[] { parent, rare, epic };
        Assert.That(ArtifactUpgradeResolver.CanReplace(parent, missing ? rare : epic, owned, catalog, out _), Is.False);
    }

    [Test] public void RejectsDuplicateCanonicalIdsAndCycles()
    {
        var a = Make("a", 1, "b");
        var b = Make("b", 2, "a");
        Assert.That(ArtifactUpgradeResolver.ValidateCatalog(new[] { a, b }), Is.Not.Empty);
        Assert.That(ArtifactUpgradeResolver.ValidateCatalog(new[] { Make("c", 1), Make("c", 2) }), Does.Contain("Duplicate"));
    }
}
