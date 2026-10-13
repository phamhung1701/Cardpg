using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class GearUpgradeResolverTests
{
    readonly List<Object> _created = new();
    GearData Make(string id, int tier, string parent = null)
    {
        var item = ScriptableObject.CreateInstance<GearData>();
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
        Assert.That(GearUpgradeResolver.CanReplace(common, rare, new[] { common }, new[] { common, rare }, out var reason), Is.True, reason);
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
        Assert.That(GearUpgradeResolver.CanReplace(parent, missing ? rare : epic, owned, catalog, out _), Is.False);
    }

    [Test] public void RejectsDuplicateCanonicalIdsAndCycles()
    {
        var a = Make("a", 1, "b");
        var b = Make("b", 2, "a");
        Assert.That(GearUpgradeResolver.ValidateCatalog(new[] { a, b }), Is.Not.Empty);
        Assert.That(GearUpgradeResolver.ValidateCatalog(new[] { Make("c", 1), Make("c", 2) }), Does.Contain("Duplicate"));
    }
}
