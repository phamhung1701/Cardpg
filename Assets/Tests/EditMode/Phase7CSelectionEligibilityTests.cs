using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7CSelectionEligibilityTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;
    CardView _prefab;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        CreateGameObject("Phase7C EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var canvas = CreateComponent<Canvas>("Phase7C Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holderObject = CreateGameObject("Phase7C Hand Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holderObject.transform.SetParent(canvas.transform, false);
        _field = holderObject.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holderObject.transform;
        _prefab = CreateCardView("Phase7C Card Prefab");
        _cards = CreateComponent<CardManager>("Phase7C CardManager");
        _cards.Configure(_field, canvas, _prefab, System.Array.Empty<RelicData>());
        _cards.ConfigureRandom(new LowestIndexRandomSource());
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = CreateComponent<CombatManager>("Phase7C CombatManager");
        _combat.ConfigurePlayer(30);
        var enemyDefinition = EnemyTypeData.Create("Phase7C Target", 100, 0, 0);
        _created.Add(enemyDefinition);
        _combat.StartEnemy(new EnemyRuntime(enemyDefinition));
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void PlayerTurn_AllowsExactlyAceAndOneOtherCard_AndRejectsThird()
    {
        var views = HandViews();
        var ace = views.First(view => view.data.Rank == CardData.Rank.Ace);
        var other = views.First(view => view.data.Rank != CardData.Rank.Ace);
        var third = views.First(view => view != ace && view != other);

        _cards.ToggleCardSelection(other);
        _cards.ToggleCardSelection(ace);

        Assert.That(_cards.SelectedCards, Is.EqualTo(new[] { other, ace }));
        Assert.That(_combat.CanPlayCards(_cards.SelectedCards, _combat.currentEnemy), Is.True);
        _cards.ToggleCardSelection(third);
        Assert.That(_cards.SelectedCards, Is.EqualTo(new[] { other, ace }), "A third card must not be appended.");
    }

    [Test]
    public void PlayerTurn_NonAceAdditionPreservesReplacementSelection()
    {
        var nonAces = HandViews().Where(view => view.data.Rank != CardData.Rank.Ace).Take(2).ToArray();
        Assert.That(nonAces.Length, Is.EqualTo(2));
        _cards.ToggleCardSelection(nonAces[0]);
        _cards.ToggleCardSelection(nonAces[1]);
        Assert.That(_cards.SelectedCards, Is.EqualTo(new[] { nonAces[1] }));
    }

    [Test]
    public void PlayerTurn_AceCanBeAddedAfterNonAceAndSelectionCanBeDeselected()
    {
        var views = HandViews();
        var ace = views.First(view => view.data.Rank == CardData.Rank.Ace);
        var other = views.First(view => view.data.Rank != CardData.Rank.Ace);
        _cards.ToggleCardSelection(ace);
        _cards.ToggleCardSelection(other);
        Assert.That(_cards.SelectedCards, Is.EqualTo(new[] { ace, other }));
        _cards.ToggleCardSelection(ace);
        Assert.That(_cards.SelectedCards, Is.EqualTo(new[] { other }));
    }

    List<CardView> HandViews()
    {
        var result = new List<CardView>();
        for (int i = 0; i < _field.cardsHolder.childCount; i++)
            if (_field.cardsHolder.GetChild(i).TryGetComponent<CardView>(out var view)) result.Add(view);
        return result;
    }

    CardView CreateCardView(string name)
    {
        var go = CreateGameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        var view = go.AddComponent<CardView>();
        view.face = go.GetComponent<Image>();
        view.canvasGroup = go.GetComponent<CanvasGroup>();
        return view;
    }

    T CreateComponent<T>(string name) where T : Component => CreateGameObject(name).AddComponent<T>();

    sealed class LowestIndexRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        public float NextFloat() => 0f;
    }

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var go = components.Length == 0 ? new GameObject(name) : new GameObject(name, components);
        _created.Add(go);
        return go;
    }
}
