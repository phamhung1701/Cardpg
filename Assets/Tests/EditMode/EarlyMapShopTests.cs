using System.Linq;
using NUnit.Framework;

public sealed class EarlyMapShopTests
{
    [Test]
    public void MapOne_FirstTwoDepthsContainNoShops_AndReplayIsDeterministic()
    {
        for (int seed = 0; seed < 40; seed++)
        {
            var first = RunMapGenerator.Generate(new RunRandomContext($"early-shop-{seed}"), 0, null);
            var replay = RunMapGenerator.Generate(new RunRandomContext($"early-shop-{seed}"), 0, null);
            Assert.That(first.Where(n => n.col < 2).Any(n => n.kind == MapNodeType.Shop), Is.False);
            Assert.That(first.Any(n => n.col >= 2 && n.kind == MapNodeType.Shop), Is.True);
            Assert.That(first.Select(n => (n.id, n.col, n.row, n.kind)),
                Is.EqualTo(replay.Select(n => (n.id, n.col, n.row, n.kind))));
            Assert.That(first.Select(n => string.Join(",", n.next)),
                Is.EqualTo(replay.Select(n => string.Join(",", n.next))));
        }
    }

    [Test]
    public void LaterMapsRetainTheirSecondDepthShop()
    {
        var map = RunMapGenerator.Generate(new RunRandomContext("later-shop"), 1, null);
        Assert.That(map.Any(n => n.col == 1 && n.kind == MapNodeType.Shop), Is.True);
    }
}
