using System;
using System.Collections.Generic;
using UnityEngine;

public class CardManager : Singleton<CardManager>
{
    public const int HAND_SIZE = 8;
    public const int BASE_ARTIFACT_CAPACITY = 5;

    [Header("References")]
    public Field handField;
    public CardView cardPrefab;
    public Canvas dragCanvas;

    [Header("Build Progression Catalogs")]
    public List<RelicData> relicCatalog = new();
    public List<CardEnhancementData> enhancementCatalog = new();

    [Header("Runtime State")]
    public int gold;
    public HashSet<string> relics = new();
    public List<RelicData> ownedArtifacts = new();
    public CardView selectedCard;

    readonly CardCollection _cards = new();
    readonly HashSet<CardView> _trackedViews = new();
    readonly List<CardView> _selectedCards = new();
    readonly List<CardData> _generatedDefinitions = new();
    readonly Dictionary<RelicData, ArtifactRuntimeInstance> _artifactInstances = new();
    CardView _activeDragCard;
    int _nextCardId = 1;

    public IReadOnlyList<CardInstance> ownedCards => _cards.OwnedCards;
    public IReadOnlyList<CardInstance> deck => _cards.Deck;
    public IReadOnlyList<CardInstance> hand => _cards.Hand;
    public IReadOnlyList<CardInstance> discardPile => _cards.DiscardPile;
    public int HandCount => _cards.HandCount;
    public IReadOnlyList<CardView> SelectedCards => _selectedCards;
    public CardView ActiveDragCard => _activeDragCard;
    public int ArtifactCapacity => BASE_ARTIFACT_CAPACITY;
    public int ArtifactSlotsUsed
    {
        get
        {
            int count = 0;
            foreach (var artifact in ownedArtifacts)
                if (artifact != null && artifact.OccupiesCapacitySlot) count++;
            return count;
        }
    }

    public event Action<int> OnGoldChanged;
    public event Action OnDeckChanged;
    public event Action OnBuildChanged;
    public event Action<CardView> OnCardSelected;

    public void Configure(
        Field field,
        Canvas canvas,
        CardView prefab,
        RelicData[] artifacts,
        CardEnhancementData[] enhancements = null)
    {
        handField = field;
        dragCanvas = canvas;
        cardPrefab = prefab;

        relicCatalog.Clear();
        if (artifacts != null)
            relicCatalog.AddRange(artifacts);

        enhancementCatalog.Clear();
        if (enhancements != null)
            enhancementCatalog.AddRange(enhancements);
    }

    public void ConfigureRandom(IRandomSource random)
    {
        _cards.ConfigureRandom(random);
    }

    public bool HasRelic(string id) => relics.Contains(id);
    public bool HasArtifact(RelicData artifact) => artifact != null && HasRelic(artifact.id);

    public string GetArtifactAcquisitionUnavailableReason(RelicData artifact)
    {
        if (artifact == null || string.IsNullOrWhiteSpace(artifact.id)) return "Artifact unavailable";
        if (HasArtifact(artifact)) return "Already owned";
        return artifact.OccupiesCapacitySlot && ArtifactSlotsUsed >= ArtifactCapacity
            ? "Artifact Capacity Full" : string.Empty;
    }

    public bool BuyRelic(RelicData relic) => BuyArtifact(relic);

    public bool BuyArtifact(RelicData artifact) => BuyArtifact(artifact, artifact != null ? artifact.price : -1);

    public bool BuyArtifact(RelicData artifact, int price)
    {
        if (price < 0 || !string.IsNullOrEmpty(GetArtifactAcquisitionUnavailableReason(artifact))) return false;
        if (!SpendGold(price)) return false;
        relics.Add(artifact.id);
        ownedArtifacts.Add(artifact);
        var instance = new ArtifactRuntimeInstance(artifact);
        _artifactInstances.Add(artifact, instance);
        var run = RunManager.Instance;
        GameplayEffectResolver.InitializeNodeCounters(new GameplayEffectSource(instance),
            run != null ? run.CompletedNodeCount : 0);
        run?.RefreshCurrentMapEffects();
        OnBuildChanged?.Invoke();
        return true;
    }

    public ArtifactRuntimeInstance GetArtifactInstance(RelicData artifact)
    {
        if (artifact == null || !ownedArtifacts.Contains(artifact)) return null;
        if (!_artifactInstances.TryGetValue(artifact, out var instance))
            _artifactInstances.Add(artifact, instance = new ArtifactRuntimeInstance(artifact));
        return instance;
    }

    public void ResetArtifactEncounterEffectState()
    {
        foreach (var artifact in ownedArtifacts)
            GetArtifactInstance(artifact)?.State.ClearEncounter();
        foreach (var card in ownedCards)
            card?.EffectState.ClearEncounter();
    }

    public CardInstance FindOwnedCard(int cardId)
    {
        foreach (var card in _cards.OwnedCards)
            if (card.Id == cardId)
                return card;
        return null;
    }

    public bool ApplyEnhancement(int cardId, CardEnhancementData enhancement)
    {
        var card = FindOwnedCard(cardId);
        if (card == null || !card.TryApplyEnhancement(enhancement)) return false;
        RefreshTrackedView(card);
        OnDeckChanged?.Invoke();
        OnBuildChanged?.Invoke();
        return true;
    }

    public bool BuyEnhancement(int cardId, CardEnhancementData enhancement, int price)
    {
        var card = FindOwnedCard(cardId);
        if (card == null || card.Enhancement != null || enhancement == null || price < 0 || gold < price)
            return false;
        if (!SpendGold(price)) return false;
        if (card.TryApplyEnhancement(enhancement))
        {
            RefreshTrackedView(card);
            OnDeckChanged?.Invoke();
            OnBuildChanged?.Invoke();
            return true;
        }

        AddGold(price);
        return false;
    }

    public void BuildDeck()
    {
        ClearCardState();

        var initialCards = new List<CardInstance>(40);
        foreach (CardData.Suit suit in Enum.GetValues(typeof(CardData.Suit)))
        {
            for (int rankValue = (int)CardData.Rank.Ace; rankValue <= (int)CardData.Rank.Ten; rankValue++)
            {
                var definition = CardData.Create(suit, (CardData.Rank)rankValue);
                _generatedDefinitions.Add(definition);
                initialCards.Add(new CardInstance(definition, _nextCardId++));
            }
        }

        _cards.Initialize(initialCards);
        _cards.ShuffleDeck();
        OnDeckChanged?.Invoke();
        OnCardSelected?.Invoke(null);
    }

    public CardInstance AddBossReward(CardData sourceCard)
    {
        if (sourceCard == null || !sourceCard.IsFaceCard)
            return null;

        foreach (var ownedCard in _cards.OwnedCards)
        {
            if (ownedCard.Suit == sourceCard.suit && ownedCard.Rank == sourceCard.rank)
                return null;
        }

        var definition = CardData.Create(sourceCard.suit, sourceCard.rank);
        var reward = new CardInstance(definition, _nextCardId);
        if (!_cards.AddOwnedToDeck(reward))
        {
            DestroyRuntimeObject(definition);
            return null;
        }

        _generatedDefinitions.Add(definition);
        _nextCardId++;
        _cards.ShuffleDeck();
        OnDeckChanged?.Invoke();
        return reward;
    }

    public void ShuffleDeck()
    {
        _cards.ShuffleDeck();
        OnDeckChanged?.Invoke();
    }

    public int DrawToHand(int count)
    {
        int previousHandCount = _cards.HandCount;
        int drawn = _cards.DrawToHand(count, HAND_SIZE);
        if (drawn <= 0) return 0;

        if (CanCreateViews())
        {
            for (int i = previousHandCount; i < _cards.HandCount; i++)
                CreateView(_cards.Hand[i]);
        }

        OnDeckChanged?.Invoke();
        return drawn;
    }

    public void DealHand() => DrawToHand(HAND_SIZE);
    public void RefillHand() => DrawToHand(HAND_SIZE);

    public int ReturnRandomDiscardToDeck(int count)
    {
        int moved = _cards.ReturnRandomDiscardToDeck(count);
        if (moved > 0)
            OnDeckChanged?.Invoke();
        return moved;
    }

    public bool TryDiscard(CardView card)
    {
        if (card == null) return false;
        return TryDiscardCards(new[] { card });
    }

    public bool TryDiscardCards(IReadOnlyList<CardView> cards)
    {
        if (cards == null || cards.Count == 0) return false;

        var uniqueViews = new HashSet<CardView>();
        var instances = new List<CardInstance>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            var view = cards[i];
            if (view == null || view.data == null || !_trackedViews.Contains(view) ||
                !uniqueViews.Add(view) || !_cards.ContainsInHand(view.data))
                return false;
            instances.Add(view.data);
        }

        if (!_cards.TryDiscard(instances)) return false;

        for (int i = 0; i < cards.Count; i++)
        {
            var view = cards[i];
            _selectedCards.Remove(view);
            view.SetSelectionOrder(0);
            DetachAndDestroyView(view);
        }

        RefreshSelectionPresentation();
        OnDeckChanged?.Invoke();
        return true;
    }

    public void NotifyDeckChanged() => OnDeckChanged?.Invoke();

    public void CancelCardInteractions()
    {
        ClearSelection();
        if (_activeDragCard != null)
            _activeDragCard.CancelActiveDrag();
        _activeDragCard = null;

        foreach (var view in _trackedViews)
            if (view != null)
                view.ForceToIdle();
    }

    public void SelectCard(CardView card) => ToggleCardSelection(card);

    public void ToggleCardSelection(CardView card)
    {
        if (!CanSelect(card)) return;

        if (_selectedCards.Remove(card))
        {
            card.SetSelectionOrder(0);
            RefreshSelectionPresentation();
            return;
        }

        var combat = CombatManager.Instance;
        if (combat == null || !combat.CanAddCardToSelection(_selectedCards, card))
            return;

        if (combat.ShouldReplaceSelectionOnAdd &&
            !combat.CanPairSelection(_selectedCards, card) &&
            !combat.CanExtendSameRankSelection(_selectedCards, card))
            ClearSelection(false);

        _selectedCards.Add(card);
        RefreshSelectionPresentation();
    }

    public void SelectOnlyCard(CardView card)
    {
        if (!CanSelect(card)) return;
        if (_selectedCards.Count == 1 && _selectedCards[0] == card) return;

        var combat = CombatManager.Instance;
        if (combat == null || !combat.CanAddCardToSelection(System.Array.Empty<CardView>(), card))
            return;

        ClearSelection(false);
        _selectedCards.Add(card);
        RefreshSelectionPresentation();
    }

    public bool PrepareDragSelection(CardView card)
    {
        if (!CanSelect(card)) return false;
        if (_selectedCards.Contains(card)) return true;

        ToggleCardSelection(card);
        return _selectedCards.Contains(card);
    }

    public IReadOnlyList<CardView> GetSelectedCardsSnapshot()
    {
        return new List<CardView>(_selectedCards);
    }

    public void BeginCardDrag(CardView card)
    {
        if (card != null && _trackedViews.Contains(card))
            _activeDragCard = card;
    }

    public void EndCardDrag(CardView card)
    {
        if (_activeDragCard == card)
            _activeDragCard = null;
    }

    public void DeselectCard() => ClearSelection();

    public void ClearSelection() => ClearSelection(true);

    public void AddGold(int amount)
    {
        gold += amount;
        OnGoldChanged?.Invoke(gold);
    }

    public bool SpendGold(int amount)
    {
        if (amount < 0 || gold < amount) return false;
        gold -= amount;
        OnGoldChanged?.Invoke(gold);
        return true;
    }

    public void Reset()
    {
        ClearCardState();
        gold = 0;
        relics.Clear();
        ownedArtifacts.Clear();
        _artifactInstances.Clear();

        OnGoldChanged?.Invoke(gold);
        OnDeckChanged?.Invoke();
        OnBuildChanged?.Invoke();
        OnCardSelected?.Invoke(null);
    }

    protected override void OnDestroy()
    {
        ClearCardState();
        base.OnDestroy();
    }

    bool CanCreateViews() => handField != null && handField.cardsHolder != null && cardPrefab != null && dragCanvas != null;

    void CreateView(CardInstance cardInstance)
    {
        var view = Instantiate(cardPrefab);
        _trackedViews.Add(view);
        handField.AddCard(view, cardInstance);
    }

    void ClearCardState()
    {
        if (_activeDragCard != null)
            _activeDragCard.CancelActiveDrag();
        _activeDragCard = null;
        ClearSelection(false);

        if (_trackedViews.Count > 0)
        {
            var views = new List<CardView>(_trackedViews);
            foreach (var view in views)
                DetachAndDestroyView(view);
        }
        _trackedViews.Clear();

        _cards.Clear();
        DestroyGeneratedDefinitions();
        _nextCardId = 1;
    }

    bool CanSelect(CardView card)
    {
        return !GameplayInputGate.IsBlocked && card != null && card.data != null &&
            _trackedViews.Contains(card) && _cards.ContainsInHand(card.data);
    }

    void ClearSelection(bool notify)
    {
        for (int i = 0; i < _selectedCards.Count; i++)
            if (_selectedCards[i] != null)
                _selectedCards[i].SetSelectionOrder(0);

        _selectedCards.Clear();
        selectedCard = null;
        if (notify)
            OnCardSelected?.Invoke(null);
    }

    void RefreshSelectionPresentation()
    {
        for (int i = 0; i < _selectedCards.Count; i++)
            if (_selectedCards[i] != null)
                _selectedCards[i].SetSelectionOrder(i + 1);

        selectedCard = _selectedCards.Count > 0 ? _selectedCards[^1] : null;
        OnCardSelected?.Invoke(selectedCard);
    }

    void RefreshTrackedView(CardInstance card)
    {
        foreach (var view in _trackedViews)
        {
            if (view != null && ReferenceEquals(view.data, card))
            {
                view.Setup(card);
                return;
            }
        }
    }

    void DetachAndDestroyView(CardView view)
    {
        _trackedViews.Remove(view);
        if (view == null) return;

        view.homeField = null;
        view.transform.SetParent(null, false);
        view.gameObject.SetActive(false);
        DestroyRuntimeObject(view.gameObject);
    }

    void DestroyGeneratedDefinitions()
    {
        foreach (var definition in _generatedDefinitions)
        {
            if (definition != null)
                DestroyRuntimeObject(definition);
        }
        _generatedDefinitions.Clear();
    }

    static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }
}
