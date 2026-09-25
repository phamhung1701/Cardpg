using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase7DMapVisibilityTests
{
    [Test]
    public void DistantElitesRemainRevealedWithoutExposingHiddenEventsOrRisk()
    {
        for (int mapIndex = 0; mapIndex < 12; mapIndex++)
        {
            var nodes = RunMapGenerator.Generate(new RunRandomContext("elite-visibility"), mapIndex, null);
            var elites = nodes.Where(node => node.kind == MapNodeType.Elite).ToArray();

            Assert.That(elites.Length, Is.GreaterThan(0));
            Assert.That(elites.All(node => node.col > 0 && node.revealed && !node.hidden), Is.True);
            Assert.That(nodes.Where(node => node.hidden).All(node => !node.revealed), Is.True);
            Assert.That(nodes.Where(node => node.kind == MapNodeType.Shop && node.col > 0)
                .All(node => !node.revealed), Is.True, "This slice reveals only distant Elites.");
        }
    }

    [Test]
    public void CombatAndEliteMapLabels_DoNotRevealEncounterEnemyIdentity()
    {
        var gameObject = new GameObject("Phase7D Label RunManager");
        try
        {
            var run = gameObject.AddComponent<RunManager>();
            var combat = new PathNode { kind = MapNodeType.Combat, contentId = "Goblin" };
            var elite = new PathNode { kind = MapNodeType.Elite, contentId = "Knight" };

            Assert.That(run.GetNodeLabel(combat), Is.EqualTo("Combat"));
            Assert.That(run.GetNodeLabel(elite), Is.EqualTo("Elite"));
            var headingMethod = typeof(PathScreenUI).GetMethod("GetNodeHeading", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(headingMethod, Is.Not.Null);
            Assert.That((string)headingMethod.Invoke(null, new object[] { combat, run }), Is.EqualTo("Combat"));
            Assert.That((string)headingMethod.Invoke(null, new object[] { elite, run }), Is.EqualTo("Elite"));
            Assert.That(run.GetNodeDesc(combat), Is.EqualTo("Normal encounter"));
            Assert.That(run.GetNodeDesc(elite), Is.EqualTo("Hard fight • better gold"));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void InaccessibleEliteKeepsIdentifyingColorRatherThanUnknownRouteColor()
    {
        var method = typeof(PathScreenUI).GetMethod("GetNodeColor", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        var nodes = RunMapGenerator.Generate(new RunRandomContext("elite-colors"), 0, null);
        var elite = nodes.First(node => node.kind == MapNodeType.Elite);
        var unknown = nodes.First(node => node.kind == MapNodeType.Combat && node.col > 0);

        Assert.That(elite.accessible, Is.False);
        Assert.That((Color)method.Invoke(null, new object[] { elite }), Is.EqualTo(new Color(0.64f, 0.3f, 0.26f)));
        Assert.That((Color)method.Invoke(null, new object[] { unknown }), Is.EqualTo(new Color(0.18f, 0.21f, 0.27f)));
    }
}
