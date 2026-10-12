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
            Assert.That(first.Where(n => !n.isShopCheckpoint && n.col < 2).Any(n => n.kind == MapNodeType.Shop), Is.False);
            Assert.That(first.Any(n => n.col >= 2 && n.kind == MapNodeType.Shop), Is.True);
            Assert.That(first.Select(n => (n.id, n.col, n.row, n.kind)),
                Is.EqualTo(replay.Select(n => (n.id, n.col, n.row, n.kind))));
            Assert.That(first.Select(n => string.Join(",", n.next)),
                Is.EqualTo(replay.Select(n => string.Join(",", n.next))));
        }
    }

    [Test]
    public void EveryMapHasTwoMandatoryZeroDepthCheckpoints()
    {
        for (int index = 0; index < 12; index++)
        {
            var map = RunMapGenerator.Generate(new RunRandomContext("mandatory-shops"), index, null);
            var shops = map.Where(n => n.kind == MapNodeType.Shop).OrderBy(n => n.col).ToArray();
            Assert.That(shops.Length, Is.EqualTo(2));
            Assert.That(shops.All(n => n.isShopCheckpoint && n.revealed), Is.True);
            Assert.That(shops.Select(n => n.col), Is.EqualTo(new[] { 1.5f, 4.5f }));
            Assert.That(map.Count(n => !n.isShopCheckpoint && n.kind != MapNodeType.Boss), Is.EqualTo(15));
            foreach (var node in map.Where(n => n.col == 1 || n.col == 4))
                Assert.That(node.next, Is.EqualTo(new[] { shops[node.col == 1 ? 0 : 1].id }));
            Assert.That(shops[1].next, Is.EqualTo(new[] { map.Single(n => n.kind == MapNodeType.Boss).id }));
        }
    }
}
