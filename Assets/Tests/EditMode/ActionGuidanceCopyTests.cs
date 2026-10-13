using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class ActionGuidanceCopyTests
{
    readonly List<Object> _objects = new();
    CardManager _cards;
    CombatManager _combat;
    ActionButtonsUI _actions;
    TMP_Text _selection;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _cards = Make<CardManager>("Copy Audit Cards");
        _cards.Configure(null, null, null, System.Array.Empty<RelicData>());
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = Make<CombatManager>("Copy Audit Combat");
        _combat.ConfigurePlayer(30);
        var enemyType = EnemyTypeData.Create("Copy Audit Enemy", 100, 3, 0);
        _objects.Add(enemyType);
        _combat.StartEnemy(new EnemyRuntime(enemyType));
        _actions = Make<ActionButtonsUI>("Copy Audit Actions");
        _selection = Make<TextMeshProUGUI>("Copy Audit Selection");
        _actions.selectionLabel = _selection;
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        _cards?.CancelCardInteractions();
        foreach (var item in _objects.AsEnumerable().Reverse())
            if (item) Object.DestroyImmediate(item);
    }

    [Test]
    public void AttackAndDefensePromptsDoNotRepeatAdjacentButtonLabels()
    {
        Refresh();
        Assert.That(_selection.text, Is.EqualTo("Select a card to attack, or drag it to an enemy"));

        typeof(CombatManager).GetMethod("BeginEnemyAttack", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_combat, null);
        Refresh();
        Assert.That(_selection.text, Is.EqualTo("Block attacks with cards, or take the remaining damage"));
    }

    [Test]
    public void TargetingPromptsStayActionableAndExplicitAboutCancel()
    {
        _cards.BeginEnemyTargeting(_ => { }, _ => true);
        Refresh();
        Assert.That(_selection.text, Is.EqualTo("Click a highlighted enemy to target it · Back or Esc cancels"));

        _cards.CancelCardInteractions();
        _cards.BeginHandEnhancementTargeting(_ => { });
        Refresh();
        Assert.That(_selection.text, Is.EqualTo("Select highlighted cards · Confirm to apply, Back to cancel"));
    }

    void Refresh() => typeof(ActionButtonsUI).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(_actions, null);

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _objects.Add(go);
        return go.AddComponent<T>();
    }
}
