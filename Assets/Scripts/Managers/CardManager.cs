using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CardManager : Singleton<CardManager>
{
    public const int HAND_SIZE = 8;
    public const int BASE_ARTIFACT_CAPACITY = 5;
    public const int BACKPACK_CAPACITY = 3;

    [Header("References")]
    public Field handField;
    public CardView cardPrefab;
    public Canvas dragCanvas;

    [Header("Build Progression Catalogs")]
    public List<RelicData> relicCatalog = new();
    public List<CardEnhancementData> enhancementCatalog = new();
    public List<ConsumableData> consumableCatalog = new();

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
    readonly List<ConsumableInstance> _backpack = new(BACKPACK_CAPACITY);
    CardView _activeDragCard;
    int _nextCardId = 1;

    public IReadOnlyList<CardInstance> ownedCards => _cards.OwnedCards;
    public IReadOnlyList<CardInstance> deck => _cards.Deck;
    public IReadOnlyList<CardInstance> hand => _cards.Hand;
    public IReadOnlyList<CardInstance> discardPile => _cards.DiscardPile;
    public IReadOnlyList<ConsumableData> Backpack => _backpack.Select(slot => slot.Definition).ToArray();
    public ConsumableInstance GetConsumableInstanceAtSlot(int slotIndex) =>
        slotIndex >= 0 && slotIndex < _backpack.Count ? _backpack[slotIndex] : null;
    public int BackpackSlotsUsed => _backpack.Count;
    public bool IsBackpackFull => _backpack.Count >= BACKPACK_CAPACITY;
    public int HandCount => _cards.HandCount;
    public int HandCapacity => GameplayEffectResolver.CalculateHandCapacity(HAND_SIZE, this);
    public bool HasInfiniteMoney
    {
        get
        {
#if UNITY_EDITOR
            return DevModeRuntime.InfiniteMoney;
#else
            return false;
#endif
        }
    }
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
    public event Action OnConsumablesChanged;


    public void Configure(
        Field field,
        Canvas canvas,
        CardView prefab,
        RelicData[] artifacts,
        CardEnhancementData[] enhancements = null,
        ConsumableData[] consumables = null)
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

        consumableCatalog.Clear();
        if (consumables != null)
            consumableCatalog.AddRange(consumables);
    }

    public void ConfigureRandom(IRandomSource random) => _cards.ConfigureRandom(random);

    public CardEnhancementData FindEnhancement(string id) =>
        enhancementCatalog.Find(value => value != null && value.id == id);

    public ConsumableData FindConsumable(string id) =>
        consumableCatalog.Find(value => value != null && value.id == id);

    public ConsumableData GetConsumableAtSlot(int slotIndex) =>
        slotIndex >= 0 && slotIndex < _backpack.Count ? _backpack[slotIndex].Definition : null;

    public int GetConsumableCount(ConsumableData consumable)
    {
        if (consumable == null) return 0;
        int count = 0;
        for (int i = 0; i < _backpack.Count; i++)
            if (_backpack[i].Definition == consumable) count++;
        return count;
    }

    public bool CanAddConsumable(ConsumableData consumable, int count = 1) =>
        consumable != null && count > 0 && consumableCatalog.Contains(consumable) &&
        count <= BACKPACK_CAPACITY - _backpack.Count;

    public bool AddConsumable(ConsumableData consumable, int count = 1)
    {
        if (!CanAddConsumable(consumable, count)) return false;
        for (int i = 0; i < count; i++) _backpack.Add(new ConsumableInstance(consumable));
        OnConsumablesChanged?.Invoke();
        return true;
    }

    public bool BuyConsumable(ConsumableData consumable, int price)
    {
        if (consumable == null || !consumable.shopAvailable || !CanAddConsumable(consumable) ||
            price < 0 || !CanAfford(price)) return false;
        if (!SpendGold(price)) return false;
        return AddConsumable(consumable);
    }

    public bool CanUseConsumable(ConsumableData consumable, CombatManager combat)
    {
        if (consumable == null) return false;
        int slot = _backpack.FindIndex(value => value.Definition == consumable);
        return CanUseConsumableAtSlot(slot, combat);
    }

    public bool CanUseConsumableAtSlot(int slotIndex, CombatManager combat)
    {
        var consumable = GetConsumableAtSlot(slotIndex);
        if (consumable == null || GetConsumableInstanceAtSlot(slotIndex).RemainingCharges <= 0 ||
            GameplayInputGate.IsBlocked || combat == null || combat.player == null || combat.player.IsDefeated ||
            combat.IsResolvingAction) return false;
        bool inCombat = combat.currentState == GameState.PlayerTurn || combat.currentState == GameState.EnemyAttacking;
        bool canEdit = inCombat || combat.currentState == GameState.Idle || combat.currentState == GameState.GameWon;
        bool hasTarget = _cards.OwnedCards.Any(card => DeckMutationService.CanTarget(_cards, card, inCombat));
        return consumable.effectType switch
        {
            ConsumableEffectType.Heal => consumable.healAmount > 0 &&
                combat.player.currentHealth < combat.player.maxHealth,
            ConsumableEffectType.DirectEnemyDamage => combat.CanUseConsumableDamage(consumable.damageAmount),
            ConsumableEffectType.ApplyEnhancement => consumable.enhancementToApply != null &&
                _cards.OwnedCards.Any(card => card != null && card.Enhancement == null),
            ConsumableEffectType.DuplicateCard => canEdit && hasTarget,
            ConsumableEffectType.DestroyCards => canEdit && _cards.OwnedCards.Count > 1 && hasTarget,
            ConsumableEffectType.ChangeSuit => canEdit && _cards.OwnedCards.Any(card =>
                DeckMutationService.CanTarget(_cards, card, inCombat) && card.Suit != consumable.targetSuit),
            _ => false
        };
    }

    public bool UseConsumable(ConsumableData consumable, CombatManager combat)
    {
        int slot = _backpack.FindIndex(value => value.Definition == consumable);
        return UseConsumableAtSlot(slot, combat);
    }

    public bool UseConsumableAtSlot(int slotIndex, CombatManager combat)
    {
        if (!CanUseConsumableAtSlot(slotIndex, combat)) return false;
        var instance = _backpack[slotIndex];
        var consumable = instance.Definition;
        if (consumable.effectType is ConsumableEffectType.ApplyEnhancement or
            ConsumableEffectType.DuplicateCard or ConsumableEffectType.DestroyCards or
            ConsumableEffectType.ChangeSuit) return false;

        // Remove before effects that can complete an encounter so a same-victory drop can use the freed slot.
        _backpack.RemoveAt(slotIndex);
        bool succeeded = consumable.effectType switch
        {
            ConsumableEffectType.Heal => combat.HealPlayer(consumable.healAmount) > 0,
            ConsumableEffectType.DirectEnemyDamage => combat.TryUseConsumableDamage(consumable.damageAmount),
            _ => false
        };
        if (!succeeded)
        {
            _backpack.Insert(slotIndex, instance);
            return false;
        }
        OnConsumablesChanged?.Invoke();
        return true;
    }

    public bool UseEnhancementConsumableAtSlot(int slotIndex, int cardId)
    {
        var combat = CombatManager.Instance;
        if (!CanUseConsumableAtSlot(slotIndex, combat)) return false;
        var consumable = _backpack[slotIndex].Definition;
        if (consumable.effectType != ConsumableEffectType.ApplyEnhancement ||
            consumable.enhancementToApply == null ||
            !_cards.OwnedCards.Any(card => card != null && card.Enhancement == null)) return false;
        var card = FindOwnedCard(cardId);
        if (card == null || card.Enhancement != null ||
            !ApplyEnhancement(cardId, consumable.enhancementToApply)) return false;
        _backpack.RemoveAt(slotIndex);
        OnConsumablesChanged?.Invoke();
        return true;
    }

    public bool UseDeckMutationConsumableAtSlot(int slotIndex, IReadOnlyList<int> cardIds)
    {
        var combat = CombatManager.Instance;
        if (!CanUseConsumableAtSlot(slotIndex, combat) || cardIds == null) return false;
        var item = _backpack[slotIndex];
        var definition = item.Definition;
        if (definition.effectType is not (ConsumableEffectType.DuplicateCard or
            ConsumableEffectType.DestroyCards or ConsumableEffectType.ChangeSuit)) return false;
        bool inCombat = combat.currentState == GameState.PlayerTurn || combat.currentState == GameState.EnemyAttacking;
        var selected = new List<CardInstance>(cardIds.Count);
        var unique = new HashSet<int>();
        foreach (int id in cardIds)
        {
            var card = FindOwnedCard(id);
            if (!unique.Add(id) || !DeckMutationService.CanTarget(_cards, card, inCombat)) return false;
            selected.Add(card);
        }
        bool success;
        switch (definition.effectType)
        {
            case ConsumableEffectType.DuplicateCard:
                if (selected.Count != 1) return false;
                success = DeckMutationService.Duplicate(_cards, selected[0], _nextCardId, inCombat) != null;
                if (success) _nextCardId++;
                break;
            case ConsumableEffectType.DestroyCards:
                if (!DeckMutationService.CanDestroy(_cards, selected, inCombat)) return false;
                success = DeckMutationService.Destroy(_cards, selected, inCombat);
                if (success)
                    foreach (var card in selected) RemoveTrackedViews(card);
                break;
            default:
                if (selected.Count != 1) return false;
                success = DeckMutationService.ChangeSuit(_cards, selected[0], definition.targetSuit, inCombat);
                if (success) RefreshTrackedView(selected[0]);
                break;
        }
        if (!success) return false;
        item.SpendCharge();
        if (item.RemainingCharges == 0) _backpack.RemoveAt(slotIndex);
        OnConsumablesChanged?.Invoke();
        OnDeckChanged?.Invoke();
        OnBuildChanged?.Invoke();
        return true;
    }

    public bool CanTargetDeckMutationAtSlot(int slotIndex, int cardId)
    {
        var combat = CombatManager.Instance;
        if (!CanUseConsumableAtSlot(slotIndex, combat)) return false;
        var definition = GetConsumableAtSlot(slotIndex);
        if (definition.effectType is not (ConsumableEffectType.DuplicateCard or
            ConsumableEffectType.DestroyCards or ConsumableEffectType.ChangeSuit)) return false;
        bool inCombat = combat.currentState == GameState.PlayerTurn || combat.currentState == GameState.EnemyAttacking;
        var card = FindOwnedCard(cardId);
        return DeckMutationService.CanTarget(_cards, card, inCombat) &&
            (definition.effectType != ConsumableEffectType.ChangeSuit || card.Suit != definition.targetSuit);
    }

    void RemoveTrackedViews(CardInstance card)
    {
        var views = new List<CardView>();
        foreach (var view in _trackedViews)
            if (view != null && ReferenceEquals(view.data, card)) views.Add(view);
        foreach (var view in views)
        {
            _selectedCards.Remove(view);
            view.SetSelectionOrder(0);
            DetachAndDestroyView(view);
        }
        RefreshSelectionPresentation();
    }

    public bool HasRelic(string id) => relics.Contains(id);
    public bool HasArtifact(RelicData artifact) => artifact != null && HasRelic(artifact.id);

    public string GetArtifactAcquisitionUnavailableReason(RelicData artifact)
    {
        if (artifact == null || string.IsNullOrWhiteSpace(artifact.id)) return "Artifact unavailable";
        if (HasArtifact(artifact)) return "Already owned";
        if (artifact.tier > 1)
        {
            var predecessor = ownedArtifacts.FirstOrDefault(value => value != null &&
                value.canonicalId == artifact.upgradeFromId);
            if (predecessor == null) return "Requires immediate predecessor";
            return ArtifactUpgradeResolver.CanReplace(predecessor, artifact, ownedArtifacts, relicCatalog, out var reason)
                ? string.Empty : reason;
        }
        if (!string.IsNullOrEmpty(artifact.upgradeFromId)) return "Upgrade cannot be acquired standalone";
        return artifact.OccupiesCapacitySlot && ArtifactSlotsUsed >= ArtifactCapacity
            ? "Artifact Capacity Full" : string.Empty;
    }

    public bool BuyRelic(RelicData relic) => BuyArtifact(relic);

    public bool BuyArtifact(RelicData artifact) => BuyArtifact(artifact, artifact != null ? artifact.price : -1);

    public bool BuyArtifact(RelicData artifact, int price)
    {
        if (price < 0 || !string.IsNullOrEmpty(GetArtifactAcquisitionUnavailableReason(artifact))) return false;
        var predecessor = artifact.tier > 1
            ? ownedArtifacts.FirstOrDefault(value => value != null && value.canonicalId == artifact.upgradeFromId)
            : null;
        if (!SpendGold(price)) return false;
        if (predecessor != null)
        {
            int index = ownedArtifacts.IndexOf(predecessor);
            ownedArtifacts[index] = artifact;
            relics.Remove(predecessor.id);
            _artifactInstances.Remove(predecessor);
        }
        else ownedArtifacts.Add(artifact);
        relics.Add(artifact.id);
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

    public bool DestroyOwnedCard(CardInstance card)
    {
        if (card == null || FindOwnedCard(card.Id) != card || !_cards.RemoveOwnedCard(card)) return false;
        RemoveTrackedViews(card);
        OnDeckChanged?.Invoke();
        OnBuildChanged?.Invoke();
        return true;
    }

    public void NotifyCardInstanceChanged(CardInstance card)
    {
        if (card == null || FindOwnedCard(card.Id) != card) return;
        RefreshTrackedView(card);
        OnDeckChanged?.Invoke();
        OnBuildChanged?.Invoke();
    }

    public bool BuyEnhancement(int cardId, CardEnhancementData enhancement, int price)
    {
        var card = FindOwnedCard(cardId);
        if (card == null || card.Enhancement != null || enhancement == null || price < 0 || !CanAfford(price))
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
        int drawn = _cards.DrawToHand(count, HandCapacity);
        if (drawn <= 0) return 0;

        if (CanCreateViews())
        {
            for (int i = previousHandCount; i < _cards.HandCount; i++)
                CreateView(_cards.Hand[i]);
        }

        OnDeckChanged?.Invoke();
        return drawn;
    }

    public void DealHand() => DrawToHand(HandCapacity);
    public void RefillHand() => DrawToHand(HandCapacity);

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

    public bool TryDiscardCardsByIds(IReadOnlyList<int> cardIds)
    {
        if (cardIds == null || cardIds.Count == 0) return false;
        var views = new List<CardView>(cardIds.Count);
        var uniqueIds = new HashSet<int>();
        for (int i = 0; i < cardIds.Count; i++)
        {
            int id = cardIds[i];
            if (id <= 0 || !uniqueIds.Add(id)) return false;
            CardView found = null;
            foreach (var view in _trackedViews)
                if (view != null && view.data != null && view.data.Id == id && _cards.ContainsInHand(view.data))
                { found = view; break; }
            if (found == null) return false;
            views.Add(found);
        }
        return TryDiscardCards(views);
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
        if (HasInfiniteMoney)
        {
            OnGoldChanged?.Invoke(gold);
            return;
        }
        gold += amount;
        OnGoldChanged?.Invoke(gold);
    }

    public bool CanAfford(int amount) => amount >= 0 && (HasInfiniteMoney || gold >= amount);

    public bool SpendGold(int amount)
    {
        if (!CanAfford(amount)) return false;
        if (HasInfiniteMoney)
        {
            OnGoldChanged?.Invoke(gold);
            return true;
        }
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
        _backpack.Clear();

        OnGoldChanged?.Invoke(gold);
        OnDeckChanged?.Invoke();
        OnBuildChanged?.Invoke();
        OnConsumablesChanged?.Invoke();
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
