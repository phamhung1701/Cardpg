using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class HandPresentationTests
{
    readonly List<Object> _created = new();
    RectTransform _holder;
    Field _field;

    [SetUp]
    public void SetUp()
    {
        var canvas = Create("Presentation Test Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _holder = Create("Presentation Test Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Field))
            .GetComponent<RectTransform>();
        _holder.SetParent(canvas.transform, false);
        _holder.sizeDelta = new Vector2(560f, 220f);
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
    public void TickPresentation_ConvergesWithoutAddCardTeleportingToTarget()
    {
        var card = Add(CardData.Suit.Hearts, CardData.Rank.Ace);
        Vector3 start = card.transform.localPosition;
        Assert.That(Vector3.Distance(start, card.LayoutTargetPosition), Is.GreaterThan(0.01f));

        for (int i = 0; i < 240; i++) card.TickPresentation(1f / 60f);

        Assert.That(Vector3.Distance(card.transform.localPosition, card.LayoutTargetPosition), Is.LessThan(0.1f));
        Assert.That(card.transform.localScale.x, Is.EqualTo(card.LayoutTargetScale).Within(0.001f));
    }

    [Test]
    public void HoverAndSelection_AreIndependentAndSelectionRetainsClickState()
    {
        var card = Add(CardData.Suit.Clubs, CardData.Rank.Five);
        card.OnPointerEnter(null);
        Assert.That(card.IsHovered, Is.True);
        card.SetSelected(true);
        for (int i = 0; i < 240; i++) card.TickPresentation(1f / 60f);
        Assert.That(card.IsHovered, Is.True);
        Assert.That(card.CurrentState, Is.EqualTo(CardView.State.Click));
        float selectedRaise = card.visualRoot.localPosition.y;
        card.SetSelected(false);
        for (int i = 0; i < 240; i++) card.TickPresentation(1f / 60f);
        Assert.That(card.IsHovered, Is.True);
        Assert.That(card.CurrentState, Is.EqualTo(CardView.State.Hover));
        Assert.That(card.visualRoot.localPosition.y, Is.LessThan(selectedRaise));
        card.OnPointerExit(null);
        Assert.That(card.IsHovered, Is.False);
    }

    [Test]
    public void RaisedArtwork_RemainsRaycastableAlongsideStableBaseFootprint()
    {
        var card = Add(CardData.Suit.Hearts, CardData.Rank.Two);
        card.OnPointerEnter(null);
        for (int i = 0; i < 60; i++) card.TickPresentation(1f / 60f);
        Assert.That(card.visualRoot.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(card.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(card.visualRoot.GetComponent<GraphicRaycaster>(), Is.Not.Null);
    }

    [Test]
    public void LargeHand_ProducesBoundedHolderLocalTargets()
    {
        var cards = new List<CardView>();
        for (int i = 0; i < 24; i++)
            cards.Add(Add(CardData.Suit.Spades, (CardData.Rank)(i % 13)));

        foreach (var card in cards)
        {
            Assert.That(Mathf.Abs(card.LayoutTargetPosition.x), Is.LessThanOrEqualTo(_holder.rect.width * 0.5f + 0.1f));
            Assert.That(float.IsNaN(card.LayoutTargetPosition.x), Is.False);
        }
    }

    [Test]
    public void SiblingManipulation_DoesNotChangeExplicitPresentationTargets()
    {
        var a = Add(CardData.Suit.Hearts, CardData.Rank.Two);
        var b = Add(CardData.Suit.Hearts, CardData.Rank.Three);
        var c = Add(CardData.Suit.Hearts, CardData.Rank.Four);
        Vector3 aTarget = a.LayoutTargetPosition;
        Vector3 bTarget = b.LayoutTargetPosition;
        Vector3 cTarget = c.LayoutTargetPosition;

        a.transform.SetAsLastSibling();
        _field.RefreshLayout();

        Assert.That(a.LayoutTargetPosition, Is.EqualTo(aTarget));
        Assert.That(b.LayoutTargetPosition, Is.EqualTo(bTarget));
        Assert.That(c.LayoutTargetPosition, Is.EqualTo(cTarget));
    }

    [Test]
    public void DragOutsideHand_ClosesReservedGapAndCancelRestoresCard()
    {
        var left = Add(CardData.Suit.Hearts, CardData.Rank.Two);
        var middle = Add(CardData.Suit.Hearts, CardData.Rank.Three);
        var right = Add(CardData.Suit.Hearts, CardData.Rank.Four);
        float reservedGap = right.LayoutTargetPosition.x - left.LayoutTargetPosition.x;
        var slot = _field.BeginVisualDrag(middle);
        Assert.That(slot, Is.Not.Null);

        var dragCanvas = Create("Outside Drag Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        middle.transform.SetParent(dragCanvas.transform, true);
        _field.UpdateVisualDrag(middle, new Vector2(-200f, -200f), null);
        float excludedGap = right.LayoutTargetPosition.x - left.LayoutTargetPosition.x;
        Assert.That(Mathf.Abs(excludedGap), Is.LessThan(Mathf.Abs(reservedGap)));

        _field.EndVisualDrag(middle, false);
        Assert.That(middle.transform.parent, Is.SameAs(_holder));
        Assert.That(middle.index, Is.EqualTo(1));
        Assert.That(_field.CardCount, Is.EqualTo(3));
    }

    CardView Add(CardData.Suit suit, CardData.Rank rank)
    {
        var go = Create("Presentation Test Card", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(CardView));
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(136f, 194f);
        var element = go.GetComponent<LayoutElement>();
        element.preferredWidth = 136f;
        element.preferredHeight = 194f;
        var definition = CardData.Create(suit, rank);
        _created.Add(definition);
        var card = go.GetComponent<CardView>();
        var visual = Create("Visual", typeof(RectTransform), typeof(Image));
        visual.GetComponent<Image>().raycastTarget = false; // Match the authored card artwork default.
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
