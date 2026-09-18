using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7AInteractionTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;
    Canvas _dragCanvas;
    CardView _cardPrefab;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        CreateComponent<UnityEngine.EventSystems.EventSystem>("Phase7A EventSystem");
        _field = CreateHandField();
        _dragCanvas = CreateComponent<Canvas>("Phase7A Drag Canvas");
        _dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _cardPrefab = CreateCardView("Phase7A Card Prefab");

        _cards = CreateComponent<CardManager>("Phase7A CardManager");
        _cards.Configure(_field, _dragCanvas, _cardPrefab, System.Array.Empty<RelicData>(), System.Array.Empty<CardEnhancementData>());
        _cards.BuildDeck();
        _cards.DealHand();

        _combat = CreateComponent<CombatManager>("Phase7A CombatManager");
        _combat.ConfigurePlayer(30);
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
    public void PlayerTurnSelection_ReplacesPreviousCard_AndClickingSelectedCardDeselects()
    {
        var enemy = CreateEnemyRuntime("Selection Target", 30, 4);
        _combat.StartEnemy(enemy);
        var views = HandViews();

        _cards.ToggleCardSelection(views[2]);
        Assert.That(_cards.SelectedCards, Is.EqualTo(new[] { views[2] }));

        _cards.ToggleCardSelection(views[0]);
        Assert.That(_cards.SelectedCards, Is.EqualTo(new[] { views[0] }));
        Assert.That(views[2].IsSelected, Is.False);
        Assert.That(views[0].IsSelected, Is.True);

        _cards.ToggleCardSelection(views[0]);
        Assert.That(_cards.SelectedCards, Is.Empty);
        Assert.That(views[0].IsSelected, Is.False);
    }

    [Test]
    public void DefenseSelection_AllowsFinalOverblock_BlocksFurtherAdds_AndReopensAfterDeselect()
    {
        var enemy = CreateEnemyRuntime("Defense Selection", 50, 20);
        _combat.StartEnemy(enemy);
        Assert.That(_combat.TryPlayCards(new[] { HandViews()[0] }, enemy), Is.True);

        var defenseArtifact = ScriptableObject.CreateInstance<RelicData>();
        defenseArtifact.id = "selection-defense";
        defenseArtifact.displayName = "Selection Defense";
        defenseArtifact.damageMultiplier = 1;
        defenseArtifact.defenseBonus = 3;
        _created.Add(defenseArtifact);
        _cards.ownedArtifacts.Add(defenseArtifact);

        var candidates = HandViews()
            .OrderBy(view => _combat.CalculateCardDefense(view.data))
            .ToList();
        var first = candidates[0];
        var crossing = candidates[^1];
        var extra = candidates[1];
        int firstDefense = _combat.CalculateCardDefense(first.data);
        int crossingDefense = _combat.CalculateCardDefense(crossing.data);
        _combat.pendingDamage = firstDefense + crossingDefense - 1;

        _cards.ToggleCardSelection(first);
        Assert.That(_combat.CalculateSelectedDefense(_cards.SelectedCards), Is.LessThan(_combat.pendingDamage));

        _cards.ToggleCardSelection(crossing);
        Assert.That(_combat.CalculateSelectedDefense(_cards.SelectedCards), Is.GreaterThanOrEqualTo(_combat.pendingDamage));
        int countAtRequiredBlock = _cards.SelectedCards.Count;

        _cards.ToggleCardSelection(extra);
        Assert.That(_cards.SelectedCards.Count, Is.EqualTo(countAtRequiredBlock), "Additional unselected cards must be rejected once enough block is selected.");

        _cards.ToggleCardSelection(crossing);
        Assert.That(_combat.CalculateSelectedDefense(_cards.SelectedCards), Is.LessThan(_combat.pendingDamage));
        _cards.ToggleCardSelection(extra);
        Assert.That(_cards.SelectedCards, Does.Contain(extra));
    }

    [Test]
    public void EnemyDropTarget_ForwardsContextualAttackAndDefenseActions()
    {
        var enemy = CreateEnemyRuntime("Drop Target", 30, 5);
        _combat.StartEnemy(enemy);
        var card = HandViews()[0];
        int expectedDamage = _combat.CalculateCardAttackDamage(card.data);
        _cards.SelectOnlyCard(card);
        _cards.BeginCardDrag(card);

        var targetObject = CreateGameObject("Enemy Drop Target", typeof(RectTransform), typeof(Image));
        var display = targetObject.AddComponent<EnemyDisplayUI>();
        var target = targetObject.AddComponent<CardActionDropTarget>();
        target.enemyDisplay = display;
        target.feedbackGraphic = targetObject.GetComponent<Image>();

        Assert.That(target.CanAccept(card), Is.True);
        Assert.That(target.TryCommit(card), Is.True);
        Assert.That(enemy.currentHp, Is.EqualTo(enemy.maxHp - expectedDamage));
        Assert.That(_cards.HandCount, Is.EqualTo(7));
        Assert.That(_cards.discardPile.Count, Is.EqualTo(1));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));

        var defender = HandViews()[0];
        int pendingBefore = _combat.pendingDamage;
        int defense = _combat.CalculateCardDefense(defender.data);
        _cards.ToggleCardSelection(defender);
        _cards.BeginCardDrag(defender);

        Assert.That(target.CanAccept(defender), Is.True);
        Assert.That(target.TryCommit(defender), Is.True);
        Assert.That(_cards.discardPile.Count, Is.EqualTo(2));
        Assert.That(_combat.pendingDamage, Is.EqualTo(Mathf.Max(0, pendingBefore - defense)));
    }

    [Test]
    public void MultiCardDefense_IsAtomic_ConsumesEachCardOnce_AndSupportsPartialThenFullBlock()
    {
        var enemy = CreateEnemyRuntime("Defense Target", 50, 20);
        _combat.StartEnemy(enemy);
        var attackCard = HandViews()[0];
        Assert.That(_combat.TryPlayCards(new[] { attackCard }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));

        var remaining = HandViews();
        var firstSet = new[] { remaining[0], remaining[1] };
        int firstDefense = firstSet.Sum(view => _combat.CalculateCardDefense(view.data));
        int startingDamage = _combat.pendingDamage;

        Assert.That(_combat.TryDefendWithCards(firstSet), Is.True);
        Assert.That(_combat.pendingDamage, Is.EqualTo(Mathf.Max(0, startingDamage - firstDefense)));
        Assert.That(_cards.discardPile.Count, Is.EqualTo(3));

        if (_combat.currentState == GameState.EnemyAttacking)
        {
            var secondSet = HandViews().ToArray();
            int beforeSecond = _cards.discardPile.Count;
            Assert.That(_combat.TryDefendWithCards(secondSet), Is.True);
            Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
            Assert.That(_combat.pendingDamage, Is.Zero);
            Assert.That(_cards.discardPile.Count, Is.EqualTo(beforeSecond + secondSet.Length));
        }

        Assert.That(_cards.ownedCards.Count, Is.EqualTo(_cards.deck.Count + _cards.hand.Count + _cards.discardPile.Count));
    }

    [Test]
    public void InvalidBatchDefense_DoesNotPartiallyConsumeCards()
    {
        var enemy = CreateEnemyRuntime("Invalid Defense", 50, 10);
        _combat.StartEnemy(enemy);
        Assert.That(_combat.TryPlayCards(new[] { HandViews()[0] }, enemy), Is.True);

        var valid = HandViews()[0];
        var foreignObject = CreateGameObject("Foreign Card", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var foreign = foreignObject.AddComponent<CardView>();
        foreign.data = valid.data;
        int handBefore = _cards.HandCount;
        int discardBefore = _cards.discardPile.Count;

        Assert.That(_combat.TryDefendWithCards(new[] { valid, foreign }), Is.False);
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore));
        Assert.That(_cards.discardPile.Count, Is.EqualTo(discardBefore));
        Assert.That(_combat.pendingDamage, Is.EqualTo(10));
    }

    [Test]
    public void HandVisualDrag_UsesPlaceholderAndCancelRestoresOriginalOrder()
    {
        var views = HandViews();
        var dragged = views[3];
        int originalIndex = dragged.transform.GetSiblingIndex();
        int holderChildren = _field.cardsHolder.childCount;

        var slot = _field.BeginVisualDrag(dragged);
        dragged.transform.SetParent(_dragCanvas.transform, true);

        Assert.That(slot, Is.Not.Null);
        Assert.That(_field.cardsHolder.childCount, Is.EqualTo(holderChildren), "The placeholder must preserve hand layout on pickup.");
        Assert.That(slot.transform.GetSiblingIndex(), Is.EqualTo(originalIndex));

        _field.EndVisualDrag(dragged, false);
        Assert.That(dragged.transform.parent, Is.SameAs(_field.cardsHolder));
        Assert.That(dragged.transform.GetSiblingIndex(), Is.EqualTo(originalIndex));
        Assert.That(_field.cardsHolder.childCount, Is.EqualTo(holderChildren));
    }

    [Test]
    public void CancelAndReset_ClearTransientSelectionWithoutCorruptingZones()
    {
        var enemy = CreateEnemyRuntime("Reset Target", 30, 4);
        _combat.StartEnemy(enemy);
        var views = HandViews();
        _cards.ToggleCardSelection(views[0]);
        _cards.ToggleCardSelection(views[1]);
        int handBefore = _cards.HandCount;

        _cards.CancelCardInteractions();
        Assert.That(_cards.SelectedCards, Is.Empty);
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore));
        Assert.That(_cards.discardPile, Is.Empty);

        _cards.ToggleCardSelection(views[2]);
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {
            button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, views[2].transform.position)
        };
        views[2].OnBeginDrag(pointer);
        Assert.That(_cards.ActiveDragCard, Is.SameAs(views[2]));

        _cards.Reset();
        Assert.That(_cards.SelectedCards, Is.Empty);
        Assert.That(_cards.ownedCards, Is.Empty);
        Assert.That(_cards.hand, Is.Empty);
        Assert.That(_field.cardsHolder.childCount, Is.Zero);
    }

    List<CardView> HandViews()
    {
        var views = new List<CardView>();
        for (int i = 0; i < _field.cardsHolder.childCount; i++)
            if (_field.cardsHolder.GetChild(i).TryGetComponent<CardView>(out var view))
                views.Add(view);
        return views;
    }

    Field CreateHandField()
    {
        var canvas = CreateComponent<Canvas>("Phase7A Hand Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("Hand Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)holder.transform;
        rect.sizeDelta = new Vector2(1200f, 220f);
        var layout = holder.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var field = holder.AddComponent<Field>();
        field.cardsHolder = rect;
        return field;
    }

    CardView CreateCardView(string name)
    {
        var gameObject = CreateGameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        var rect = (RectTransform)gameObject.transform;
        rect.sizeDelta = new Vector2(132f, 184f);
        var layout = gameObject.GetComponent<LayoutElement>();
        layout.minWidth = 112f;
        layout.minHeight = 156f;
        layout.preferredWidth = 132f;
        layout.preferredHeight = 184f;
        var view = gameObject.AddComponent<CardView>();
        view.face = gameObject.GetComponent<Image>();
        view.canvasGroup = gameObject.GetComponent<CanvasGroup>();
        return view;
    }

    EnemyRuntime CreateEnemyRuntime(string name, int health, int attack)
    {
        var definition = EnemyTypeData.Create(name, health, attack, 0);
        _created.Add(definition);
        return new EnemyRuntime(definition);
    }

    T CreateComponent<T>(string name) where T : Component
    {
        return CreateGameObject(name).AddComponent<T>();
    }

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var gameObject = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }
}
