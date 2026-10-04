using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7DMapVisibilityTests
{
    [Test]
    public void DistantElitesRemainRevealedWithoutExposingHiddenEventsOrRisk()
    {
        int totalElites = 0;
        for (int mapIndex = 0; mapIndex < 12; mapIndex++)
        {
            var nodes = RunMapGenerator.Generate(new RunRandomContext("elite-visibility"), mapIndex, null);
            var elites = nodes.Where(node => node.kind == MapNodeType.Elite).ToArray();

            Assert.That(elites.Length, Is.InRange(0, 2));
            totalElites += elites.Length;
            Assert.That(elites.All(node => node.col > 0 && node.revealed && !node.hidden), Is.True);
            Assert.That(nodes.Where(node => node.hidden).All(node => !node.revealed), Is.True);
            Assert.That(nodes.Where(node => node.kind == MapNodeType.Shop && node.col > 0)
                .All(node => !node.revealed), Is.True, "This slice reveals only distant Elites.");
        }
        Assert.That(totalElites, Is.GreaterThan(0), "The visibility assertions must include actual Elite nodes.");
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
    public void PathNodeButtonUI_UsesCompactLabelsAndKeepsUnknownNodesUnknown()
    {
        var root = new GameObject("Path node label test");
        var labelObject = new GameObject("Label", typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform);
        try
        {
            var runObject = new GameObject("Path node run");
            try
            {
                var run = runObject.AddComponent<RunManager>();
                var ui = root.AddComponent<PathNodeButtonUI>();
                var label = labelObject.GetComponent<TextMeshProUGUI>();

                ui.SetNode(new PathNode { kind = MapNodeType.Combat, revealed = true }, run);
                Assert.That(label.text, Is.EqualTo("COMBAT"));
                ui.SetNode(new PathNode { kind = MapNodeType.Elite, revealed = true }, run);
                Assert.That(label.text, Is.EqualTo("ELITE"));
                ui.SetNode(new PathNode { kind = MapNodeType.Shop, revealed = true }, run);
                Assert.That(label.text, Is.EqualTo("SHOP"));
                ui.SetNode(new PathNode { kind = MapNodeType.Event, revealed = true }, run);
                Assert.That(label.text, Is.EqualTo("?"));
                ui.SetNode(new PathNode { kind = MapNodeType.Risk, revealed = true }, run);
                Assert.That(label.text, Is.EqualTo("?"));
                ui.SetNode(new PathNode { kind = MapNodeType.Combat, revealed = false }, run);
                Assert.That(label.text, Is.EqualTo("?"));
            }
            finally
            {
                Object.DestroyImmediate(runObject);
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PathNodeButtonUI_PreservesBossIdentityWithoutDescription()
    {
        var runObject = new GameObject("Path node boss label test");
        var root = new GameObject("Path node boss button");
        var labelObject = new GameObject("Label", typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform);
        try
        {
            var run = runObject.AddComponent<RunManager>();
            var ui = root.AddComponent<PathNodeButtonUI>();
            var node = new PathNode { kind = MapNodeType.Boss, revealed = true };
            ui.SetNode(node, run);
            Assert.That(labelObject.GetComponent<TextMeshProUGUI>().text, Is.EqualTo("BOSS"));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(runObject);
        }
    }

    [Test]
    public void MapVisibilityToggle_RequiresAwaitingRouteAndUnblockedInput()
    {
        GameplayInputGate.Clear();
        var runObject = new GameObject("Map toggle run");
        var root = new GameObject("Map toggle canvas", typeof(RectTransform), typeof(Canvas));
        root.SetActive(false);
        var panelObject = new GameObject("PathPanel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        panelObject.transform.SetParent(root.transform, false);
        var ui = root.AddComponent<PathScreenUI>();
        SetField(ui, "panel", panelObject);
        try
        {
            var run = runObject.AddComponent<RunManager>();
            root.SetActive(true);
            InvokePrivate(ui, "EnsureMapVisibilityControl");
            var control = root.transform.Find("MapVisibilityButton");
            Assert.That(control, Is.Not.Null);

            GameplayInputGate.Set(GameplayInputBlockReason.ConsumableReward, true);
            InvokePrivate(ui, "ShowImmediately");
            Assert.That(control.gameObject.activeSelf, Is.True,
                "A temporary reward gate must not permanently disable the route-map control.");
            GameplayInputGate.Clear();
            InvokePrivate(ui, "ToggleMapVisibility");
            Assert.That(panelObject.activeSelf, Is.False,
                "The map must remain closeable once the temporary reward gate clears.");
            InvokePrivate(ui, "ToggleMapVisibility");
            Assert.That(panelObject.activeSelf, Is.True);

            InvokePrivate(ui, "ToggleMapVisibility");
            Assert.That(panelObject.activeSelf, Is.False);

            GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, true);
            InvokePrivate(ui, "ToggleMapVisibility");
            Assert.That(panelObject.activeSelf, Is.False, "A blocked UI state must not reopen the map.");
            GameplayInputGate.Clear();

            InvokePrivate(ui, "ToggleMapVisibility");
            Assert.That(panelObject.activeSelf, Is.True);

            typeof(RunManager).GetProperty(nameof(RunManager.IsRunCompleted))
                .GetSetMethod(true).Invoke(run, new object[] { true });
            InvokePrivate(ui, "ToggleMapVisibility");
            Assert.That(panelObject.activeSelf, Is.True, "A completed run must not toggle the route map.");

            InvokePrivate(ui, "HandleRunCompleted");
            Assert.That(control.gameObject.activeSelf, Is.False, "The map toggle must be hidden for final victory.");
        }
        finally
        {
            GameplayInputGate.Clear();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(runObject);
        }
    }

    [Test]
    public void MapVisibilityToggle_IsHiddenWhenMapFlowIsCancelledOrReset()
    {
        GameplayInputGate.Clear();
        var runObject = new GameObject("Map lifecycle run");
        var root = new GameObject("Map lifecycle canvas", typeof(RectTransform), typeof(Canvas));
        root.SetActive(false);
        var panelObject = new GameObject("PathPanel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        panelObject.transform.SetParent(root.transform, false);
        var ui = root.AddComponent<PathScreenUI>();
        SetField(ui, "panel", panelObject);
        try
        {
            runObject.AddComponent<RunManager>();
            root.SetActive(true);
            InvokePrivate(ui, "EnsureMapVisibilityControl");
            var control = root.transform.Find("MapVisibilityButton");

            InvokePrivate(ui, "ShowImmediately");
            Assert.That(control.gameObject.activeSelf, Is.True);
            InvokePrivate(ui, "HandleRunStarted", "new-seed");
            Assert.That(control.gameObject.activeSelf, Is.False);

            InvokePrivate(ui, "ShowImmediately");
            Assert.That(control.gameObject.activeSelf, Is.True);
            InvokePrivate(ui, "HandlePresentationCancelled");
            Assert.That(control.gameObject.activeSelf, Is.False);

            InvokePrivate(ui, "ShowImmediately");
            Assert.That(control.gameObject.activeSelf, Is.True);
            InvokePrivate(ui, "OnDisable");
            Assert.That(control.gameObject.activeSelf, Is.False);
        }
        finally
        {
            GameplayInputGate.Clear();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(runObject);
        }
    }

    static void SetField(object target, string name, object value)
    {
        typeof(PathScreenUI).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    static void InvokePrivate(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing callback {name}.");
        method.Invoke(target, args);
    }

    [Test]
    public void InaccessibleEliteKeepsIdentifyingColorRatherThanUnknownRouteColor()
    {
        var method = typeof(PathScreenUI).GetMethod("GetNodeColor", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        var elite = new PathNode { kind = MapNodeType.Elite, revealed = true, accessible = false };
        var unknown = new PathNode { kind = MapNodeType.Combat, revealed = false, accessible = false };

        Assert.That(elite.accessible, Is.False);
        Assert.That((Color)method.Invoke(null, new object[] { elite }), Is.EqualTo(new Color(0.64f, 0.3f, 0.26f)));
        Assert.That((Color)method.Invoke(null, new object[] { unknown }), Is.EqualTo(new Color(0.18f, 0.21f, 0.27f)));
    }
}
