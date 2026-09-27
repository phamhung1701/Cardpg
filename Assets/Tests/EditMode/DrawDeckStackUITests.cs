using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DrawDeckStackUITests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    DrawDeckStackUI _stack;
    RectTransform _root;
    Button _button;
    TMP_Text _count;
    Canvas _canvas;
    Field _field;
    CardView _cardPrefab;
    CombatManager _combat;
    EnemyTypeData _enemyType;

    [SetUp]
    public void SetUp()
    {
        _cards = Create("Cards").AddComponent<CardManager>();
        _canvas = Create("Cards Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        var holderObject = Create("Card Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Field));
        holderObject.transform.SetParent(_canvas.transform, false);
        var holder = holderObject.GetComponent<RectTransform>();
        holder.sizeDelta = new Vector2(900f, 240f);
        _field = holderObject.GetComponent<Field>();
        _field.cardsHolder = holder;
        var template = Create("Card View Template", typeof(RectTransform), typeof(Image), typeof(CardView));
        template.transform.SetParent(_canvas.transform, false);
        ((RectTransform)template.transform).anchoredPosition = new Vector2(5000f, 5000f);
        template.GetComponent<Image>().enabled = false;
        _cardPrefab = template.GetComponent<CardView>();
        _cards.Configure(_field, _canvas, _cardPrefab, System.Array.Empty<RelicData>());
        var host = Create("Deck Stack", typeof(RectTransform));
        _root = (RectTransform)host.transform;
        _root.sizeDelta = new Vector2(90f, 120f);
        _button = host.AddComponent<Button>();
        _count = Create("Count", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TMP_Text>();
        _count.transform.SetParent(_root, false);
        _stack = host.AddComponent<DrawDeckStackUI>();
        _stack.stackRoot = _root;
        _stack.deckButton = _button;
        _stack.countLabel = _count;
        _stack.Refresh();
    }

    [TearDown]
    public void TearDown()
    {
        if (_combat) _combat.Reset();
        if (_cards) _cards.Reset();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(9, 1)]
    [TestCase(10, 2)]
    [TestCase(19, 2)]
    [TestCase(20, 3)]
    [TestCase(29, 3)]
    [TestCase(30, 4)]
    [TestCase(40, 4)]
    public void DepthFollowsRemainingDeckCountBuckets(int remaining, int expectedDepth)
    {
        Assert.That(DrawDeckStackUI.GetDepthForCount(remaining), Is.EqualTo(expectedDepth));
    }

    [Test]
    public void BuildDrawAndReset_UpdateCountAndEmptyPresentationWithoutRecreatingLayers()
    {
        _cards.BuildDeck();
        Assert.That(_count.text, Is.EqualTo("40"));
        Assert.That(_stack.DisplayedCount, Is.EqualTo(40));
        Assert.That(_stack.DisplayedDepth, Is.EqualTo(4));
        Assert.That(ActiveLayers(), Is.EqualTo(4));
        Assert.That(_root.childCount, Is.EqualTo(5)); // Four generated cards plus the count label.
        var front = _root.GetChild(3).GetComponent<Image>();
        Assert.That(front, Is.Not.Null);
        Assert.That(front.raycastTarget, Is.False);

        Assert.That(_cards.DrawToHand(8), Is.EqualTo(8));
        Assert.That(_count.text, Is.EqualTo("32"));
        Assert.That(_stack.DisplayedCount, Is.EqualTo(32));
        Assert.That(_root.childCount, Is.EqualTo(5));

        _cards.Reset();
        Assert.That(_count.text, Is.EqualTo("0"));
        Assert.That(_stack.DisplayedCount, Is.Zero);
        Assert.That(_stack.DisplayedDepth, Is.Zero);
        Assert.That(ActiveLayers(), Is.EqualTo(1));
        Assert.That(front.color.a, Is.LessThan(0.3f));
        Assert.That(_button.interactable, Is.True);

        _cards.BuildDeck();
        Assert.That(_count.text, Is.EqualTo("40"));
        Assert.That(ActiveLayers(), Is.EqualTo(4));
        Assert.That(_root.childCount, Is.EqualTo(5));
        Assert.That(front.color.a, Is.EqualTo(1f));
    }

    [Test]
    public void CardLayersDoNotInterceptClicksOrAddButtonListeners()
    {
        int clicks = 0;
        _button.onClick.AddListener(() => clicks++);
        _cards.BuildDeck();
        foreach (Transform child in _root)
        {
            var image = child.GetComponent<Image>();
            if (image != null) Assert.That(image.raycastTarget, Is.False);
        }
        _button.onClick.Invoke();
        Assert.That(clicks, Is.EqualTo(1));
        _cards.Reset();
        _button.onClick.Invoke();
        Assert.That(clicks, Is.EqualTo(2));
    }

    [Test]
    public void EncounterStartDraw_ImmediatelyRefreshesStackCount()
    {
        _cards.BuildDeck();
        Assert.That(_cards.DrawToHand(3), Is.EqualTo(3));
        Assert.That(_stack.DisplayedCount, Is.EqualTo(37));

        _combat = Create("Combat").AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        _enemyType = EnemyTypeData.Create("Deck Refresh Enemy", 10, 1, 0);
        _created.Add(_enemyType);
        _combat.StartEnemy(new EnemyRuntime(_enemyType));

        Assert.That(_cards.HandCount, Is.EqualTo(4));
        Assert.That(_cards.deck.Count, Is.EqualTo(36));
        Assert.That(_stack.DisplayedCount, Is.EqualTo(36));
    }

    [Test]
    public void RecycleAndBossRewardMutations_ImmediatelyRefreshCount()
    {
        _cards.BuildDeck();
        Assert.That(_cards.DrawToHand(3), Is.EqualTo(3));
        Assert.That(_stack.DisplayedCount, Is.EqualTo(37));
        var hand = _field.cardsHolder.GetComponentsInChildren<CardView>();
        Assert.That(_cards.TryDiscardCards(new[] { hand[0], hand[1] }), Is.True);
        Assert.That(_stack.DisplayedCount, Is.EqualTo(37), "Discarding hand cards does not change the draw deck.");

        Assert.That(_cards.ReturnRandomDiscardToDeck(1), Is.EqualTo(1));
        Assert.That(_stack.DisplayedCount, Is.EqualTo(38));
        Assert.That(_stack.DisplayedDepth, Is.EqualTo(4));

        var sourceBossCard = CardData.Create(CardData.Suit.Spades, CardData.Rank.King);
        _created.Add(sourceBossCard);
        Assert.That(_cards.AddBossReward(sourceBossCard), Is.Not.Null);
        Assert.That(_stack.DisplayedCount, Is.EqualTo(39));

        _cards.Reset();
        Assert.That(_stack.DisplayedCount, Is.Zero);
        Assert.That(_stack.DisplayedDepth, Is.Zero);
        _cards.BuildDeck();
        Assert.That(_stack.DisplayedCount, Is.EqualTo(40));
    }

    int ActiveLayers()
    {
        int result = 0;
        foreach (Transform child in _root)
            if (child.GetComponent<Image>() != null && child.gameObject.activeSelf) result++;
        return result;
    }

    GameObject Create(string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        _created.Add(go);
        return go;
    }
}
