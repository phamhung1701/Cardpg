using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class HandFanTests
{
    readonly List<Object> _created = new();
    Field _field;
    RectTransform _holder;

    [SetUp]
    public void SetUp()
    {
        var canvas = Create("Hand Test Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _holder = Create("Hand Test Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Field))
            .GetComponent<RectTransform>();
        _holder.SetParent(canvas.transform, false);
        _holder.sizeDelta = new Vector2(560f, 220f);
        var layout = _holder.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        _field = _holder.GetComponent<Field>();
        _field.cardsHolder = _holder;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void Reflow_CentersAndCompressesSpacing_AsCardsAreAdded()
    {
        var a = Add(CardData.Suit.Spades, CardData.Rank.King);
        var b = Add(CardData.Suit.Hearts, CardData.Rank.Ace);
        var c = Add(CardData.Suit.Diamonds, CardData.Rank.Three);
        Assert.That(_holder.GetComponent<HorizontalLayoutGroup>().spacing, Is.EqualTo(-18f).Within(0.01f));
        Assert.That((a.transform.localPosition.x + c.transform.localPosition.x) * 0.5f,
            Is.EqualTo(0f).Within(0.1f));
        for (int i = 0; i < 4; i++) Add(CardData.Suit.Clubs, CardData.Rank.Four);
        Assert.That(_holder.GetComponent<HorizontalLayoutGroup>().spacing, Is.LessThan(0f));
        Assert.That(_field.CardCount, Is.EqualTo(7));
        Assert.That(b.transform.localPosition.x, Is.LessThan(c.transform.localPosition.x));
    }

    [Test]
    public void Sorting_ChangesOnlyPresentation_AndPreservesSelection()
    {
        var king = Add(CardData.Suit.Spades, CardData.Rank.King);
        var diamond = Add(CardData.Suit.Diamonds, CardData.Rank.Ace);
        var heart = Add(CardData.Suit.Hearts, CardData.Rank.Ten);
        var modelOrder = new[] { king.data, diamond.data, heart.data };
        heart.SetSelected(true);

        _field.SortByRank();
        Assert.That(_holder.GetChild(0), Is.SameAs(diamond.transform));
        Assert.That(_holder.GetChild(1), Is.SameAs(heart.transform));
        Assert.That(_holder.GetChild(2), Is.SameAs(king.transform));
        _field.SortBySuit();
        Assert.That(_holder.GetChild(0), Is.SameAs(heart.transform));
        Assert.That(_holder.GetChild(1), Is.SameAs(diamond.transform));
        Assert.That(_holder.GetChild(2), Is.SameAs(king.transform));
        Assert.That(heart.IsSelected, Is.True);
        Assert.That(king.data, Is.SameAs(modelOrder[0]));
        Assert.That(diamond.data, Is.SameAs(modelOrder[1]));
        Assert.That(heart.data, Is.SameAs(modelOrder[2]));
    }

    [Test]
    public void HoverAndSelection_DrawOverNeighbors_WithoutReorderingOrLosingRaycasters()
    {
        var left = Add(CardData.Suit.Hearts, CardData.Rank.Two);
        var middle = Add(CardData.Suit.Hearts, CardData.Rank.Three);
        var right = Add(CardData.Suit.Hearts, CardData.Rank.Four);
        Assert.That(middle.visualRoot.GetComponent<GraphicRaycaster>(), Is.Not.Null);
        middle.OnPointerEnter(null);
        Assert.That(middle.visualRoot.GetComponent<Canvas>().sortingOrder,
            Is.GreaterThan(right.visualRoot.GetComponent<Canvas>().sortingOrder));
        right.SetSelected(true);
        Assert.That(right.visualRoot.GetComponent<Canvas>().sortingOrder,
            Is.GreaterThan(middle.visualRoot.GetComponent<Canvas>().sortingOrder));
        Assert.That(_holder.GetChild(0), Is.SameAs(left.transform));
        Assert.That(_holder.GetChild(1), Is.SameAs(middle.transform));
        Assert.That(_holder.GetChild(2), Is.SameAs(right.transform));
        Assert.That(right.visualRoot.GetComponent<GraphicRaycaster>(), Is.Not.Null);
    }

    [Test]
    public void DragPlaceholder_RequiresCrossingNeighborCenterPlusThreshold()
    {
        var left = Add(CardData.Suit.Hearts, CardData.Rank.Ace);
        var middle = Add(CardData.Suit.Clubs, CardData.Rank.Two);
        var right = Add(CardData.Suit.Spades, CardData.Rank.Three);
        var slot = _field.BeginVisualDrag(middle);
        var dragCanvas = Create("Drag Test Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        middle.transform.SetParent(dragCanvas.transform, true);
        _field.RefreshLayout();
        float center = RectTransformUtility.WorldToScreenPoint(null, right.transform.position).x;
        float y = RectTransformUtility.WorldToScreenPoint(null, right.transform.position).y;
        _field.UpdateVisualDrag(middle, new Vector2(center + _field.reorderThreshold - 1f, y), null);
        Assert.That(slot.transform.GetSiblingIndex(), Is.EqualTo(1));
        _field.UpdateVisualDrag(middle, new Vector2(center + _field.reorderThreshold + 1f, y), null);
        Assert.That(slot.transform.GetSiblingIndex(), Is.EqualTo(2));
        _field.EndVisualDrag(middle, true);
        Assert.That(_holder.GetChild(0), Is.SameAs(left.transform));
        Assert.That(_holder.GetChild(2), Is.SameAs(middle.transform));
    }

    CardView Add(CardData.Suit suit, CardData.Rank rank)
    {
        var go = Create("Test Card", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(CardView));
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(136f, 194f);
        var element = go.GetComponent<LayoutElement>();
        element.preferredWidth = 136f;
        element.preferredHeight = 194f;
        var definition = CardData.Create(suit, rank);
        _created.Add(definition);
        var card = go.GetComponent<CardView>();
        var visual = Create("Visual", typeof(RectTransform), typeof(Image));
        visual.transform.SetParent(go.transform, false);
        card.visualRoot = (RectTransform)visual.transform;
        card.visualRoot.sizeDelta = rect.sizeDelta;
        _field.AddCard(card, new CardInstance(definition, _created.Count));
        return card;
    }

    GameObject Create(string name, params System.Type[] types)
    {
        var go = new GameObject(name, types);
        _created.Add(go);
        return go;
    }
}
