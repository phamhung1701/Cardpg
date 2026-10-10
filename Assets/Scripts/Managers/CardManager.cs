using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ConsumableTargetType { None, Card, Enemy }

public class CardManager : Singleton<CardManager>
{
    public const int HAND_SIZE = 8;
    public const int BASE_ARTIFACT_CAPACITY = 5;
    public const int BASE_BACKPACK_CAPACITY = 3;
    // Kept for existing content/tests that explicitly mean the backpack's unmodified baseline.
    public const int BACKPACK_CAPACITY = BASE_BACKPACK_CAPACITY;
    public const int TRAVELER_BACKPACK_CAPACITY = 5;
    public const int RARE_CONSUMABLES_PER_SLOT = 3;

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
    readonly List<ArtifactRuntimeInstance> _orderedArtifactInstances = new();
    readonly List<ConsumableStack> _backpack = new(BASE_BACKPACK_CAPACITY);
    CardView _activeDragCard;
    int _nextCardId = 1;
    bool _consumableUsedThisEncounter;
    Action<CardView> _handEnhancementTargetHandler;
    Func<int, bool> _handTargetEligibility;
    Action<EnemyRuntime> _enemyTargetHandler;
    Func<EnemyRuntime, bool> _enemyTargetEligibility;

    public bool IsHandEnhancementTargeting => _handEnhancementTargetHandler != null;
    public bool IsEnemyTargeting => _enemyTargetHandler != null;
    public event Action<bool> OnEnemyTargetingChanged;

    public IReadOnlyList<ArtifactRuntimeInstance> OwnedArtifactInstances
    {
        get { EnsureArtifactInstances(); return _orderedArtifactInstances; }
    }
    public IReadOnlyList<CardInstance> ownedCards => _cards.OwnedCards;
    public IReadOnlyList<CardInstance> deck => _cards.Deck;
    public IReadOnlyList<CardInstance> hand => _cards.Hand;
    public IReadOnlyList<CardInstance> discardPile => _cards.DiscardPile;
    public IReadOnlyList<ConsumableData> Backpack => _backpack
        .SelectMany(stack => stack.Items).Select(item => item.Definition).ToArray();
    public ConsumableInstance GetConsumableInstanceAtSlot(int slotIndex) =>
        slotIndex >= 0 && slotIndex < _backpack.Count ? _backpack[slotIndex].GetPartiallyUsedFirst() : null;
    public int GetConsumableStackCountAtSlot(int slotIndex) =>
        slotIndex >= 0 && slotIndex < _backpack.Count ? _backpack[slotIndex].Count : 0;
    public int BackpackSlotsUsed => _backpack.Count;
    public int BackpackCapacity => HasArtifactSpecialRule(ArtifactSpecialRule.TravelerPackCommon) ||
        HasArtifactSpecialRule(ArtifactSpecialRule.TravelerPackRare)
            ? TRAVELER_BACKPACK_CAPACITY : BASE_BACKPACK_CAPACITY;
    public int ConsumablesPerSlot => HasArtifactSpecialRule(ArtifactSpecialRule.TravelerPackRare)
        ? RARE_CONSUMABLES_PER_SLOT : 1;
    public int DistinctConsumableTypeCount => _backpack.Select(stack => stack.Definition).Distinct().Count();
    public bool IsBackpackFull => _backpack.Count >= BackpackCapacity;
    public int HandCount => _cards.HandCount;
    public int HandCapacity => GameplayEffectResolver.CalculateHandCapacity(HAND_SIZE, this);
    public int OverflowCapacity
    {
        get
        {
            int capacity = 0;
            for (int i = 0; i < ownedArtifacts.Count; i++)
            {
                var rule = ownedArtifacts[i]?.specialRule;
                if (rule is ArtifactSpecialRule.OverflowRare or ArtifactSpecialRule.OverflowEpic) return 3;
                if (rule == ArtifactSpecialRule.OverflowCommon) capacity = 2;
            }
            return capacity;
        }
    }
    public bool HasInfiniteMoney
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
    public event Action<ConsumableData> OnConsumableUsed;


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
            if (_backpack[i].Definition == consumable) count += _backpack[i].Count;
        return count;
    }

    public bool CanAddConsumable(ConsumableData consumable, int count = 1)
    {
        if (consumable == null || count <= 0 || !consumableCatalog.Contains(consumable) ||
            !IsBackpackWithinCurrentLimits()) return false;
        int capacity = BackpackCapacity;
        int maxPerSlot = ConsumablesPerSlot;
        var definitions = _backpack.Select(stack => stack.Definition).ToList();
        var stackCounts = _backpack.Select(stack => stack.Count).ToList();
        for (int item = 0; item < count; item++)
        {
            int stackIndex = -1;
            if (maxPerSlot > 1)
            {
                for (int i = 0; i < definitions.Count; i++)
                    if (definitions[i] == consumable && stackCounts[i] < maxPerSlot)
                    {
                        stackIndex = i;
                        break;
                    }
            }
            if (stackIndex >= 0) stackCounts[stackIndex]++;
            else if (definitions.Count < capacity)
            {
                definitions.Add(consumable);
                stackCounts.Add(1);
            }
            else return false;
        }
        return true;
    }

    public bool AddConsumable(ConsumableData consumable, int count = 1)
    {
        if (!CanAddConsumable(consumable, count)) return false;
        for (int i = 0; i < count; i++) AddConsumableInstance(new ConsumableInstance(consumable));
        OnConsumablesChanged?.Invoke();
        return true;
    }

    public bool ReplaceConsumableSlot(int slotIndex, ConsumableData replacement)
    {
        if (slotIndex < 0 || slotIndex >= _backpack.Count || replacement == null ||
            !consumableCatalog.Contains(replacement)) return false;
        var removed = _backpack[slotIndex];
        _backpack.RemoveAt(slotIndex);
        if (!CanAddConsumable(replacement))
        {
            _backpack.Insert(slotIndex, removed);
            return false;
        }
        AddConsumableInstance(new ConsumableInstance(replacement));
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

    bool IsBackpackWithinCurrentLimits()
    {
        if (_backpack.Count > BackpackCapacity) return false;
        int maxPerSlot = ConsumablesPerSlot;
        for (int i = 0; i < _backpack.Count; i++)
            if (_backpack[i].Count > maxPerSlot) return false;
        return true;
    }

    void AddConsumableInstance(ConsumableInstance instance)
    {
        if (ConsumablesPerSlot > 1)
        {
            var target = _backpack.FirstOrDefault(stack =>
                stack.Definition == instance.Definition && stack.Count < ConsumablesPerSlot);
            if (target != null)
            {
                target.Add(instance);
                return;
            }
        }
        var created = new ConsumableStack(instance.Definition);
        created.Add(instance);
        _backpack.Add(created);
    }

    public bool RemoveConsumableInstanceAtSlot(int slotIndex, ConsumableInstance expected)
    {
        if (slotIndex < 0 || slotIndex >= _backpack.Count || expected == null ||
            !_backpack[slotIndex].Items.Contains(expected)) return false;
        var stack = _backpack[slotIndex];
        if (!stack.Remove(expected)) return false;
        if (stack.Count == 0) _backpack.RemoveAt(slotIndex);
        OnConsumablesChanged?.Invoke();
        OnBuildChanged?.Invoke();
        return true;
    }

    public bool MoveConsumableSlot(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _backpack.Count || toIndex < 0 || toIndex >= BackpackCapacity ||
            fromIndex == toIndex) return false;
        var stack = _backpack[fromIndex];
        _backpack.RemoveAt(fromIndex);
        int insertionIndex = Mathf.Min(toIndex, _backpack.Count);
        if (insertionIndex == fromIndex)
        {
            _backpack.Insert(fromIndex, stack);
            return false;
        }
        _backpack.Insert(insertionIndex, stack);
        OnConsumablesChanged?.Invoke();
        return true;
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
        bool hasRuneTarget = _cards.Hand.Any(card => card != null && card.Suit != consumable.targetSuit);
        bool hasEnhancementTarget = _cards.OwnedCards.Any(card => card != null && card.Enhancement == null &&
            (!inCombat || _cards.ContainsInHand(card)));
        return consumable.effectType switch
        {
            ConsumableEffectType.Heal => consumable.healAmount > 0 &&
                combat.player.currentHealth < combat.player.maxHealth,
            ConsumableEffectType.DirectEnemyDamage => combat.Enemies.Any(enemy =>
                enemy != null && combat.CanUseConsumableDamage(consumable.damageAmount, enemy)),
            ConsumableEffectType.ApplyEnhancement => consumable.enhancementToApply != null && hasEnhancementTarget,
            ConsumableEffectType.DuplicateCard => canEdit && hasTarget,
            ConsumableEffectType.DestroyCards => canEdit && _cards.OwnedCards.Count > 1 && hasTarget,
            ConsumableEffectType.ChangeSuit => canEdit && hasRuneTarget,
            _ => false
        };
    }

    public bool UseConsumable(ConsumableData consumable, CombatManager combat)
    {
        int slot = _backpack.FindIndex(value => value.Definition == consumable);
        return UseConsumableAtSlot(slot, combat);
    }

    public bool UseConsumableAtSlot(int slotIndex, CombatManager combat, EnemyRuntime target)
    {
        if (!CanUseConsumableAtSlot(slotIndex, combat) || target == null) return false;
        var item = GetConsumableInstanceAtSlot(slotIndex);
        if (item == null || item.Definition.effectType != ConsumableEffectType.DirectEnemyDamage ||
            !combat.CanUseConsumableDamage(item.Definition.damageAmount, target)) return false;
        bool wasInEncounter = IsActiveEncounter(combat);
        if (!combat.TryUseConsumableDamage(item.Definition.damageAmount, target)) return false;
        CompleteConsumableUse(slotIndex, item, combat, wasInEncounter);
        return true;
    }

    public bool UseConsumableAtSlot(int slotIndex, CombatManager combat)
    {
        if (!CanUseConsumableAtSlot(slotIndex, combat)) return false;
        var instance = GetConsumableInstanceAtSlot(slotIndex);
        var consumable = instance.Definition;
        if (consumable.effectType is ConsumableEffectType.ApplyEnhancement or
            ConsumableEffectType.DuplicateCard or ConsumableEffectType.DestroyCards or
            ConsumableEffectType.ChangeSuit) return false;
        bool wasInEncounter = IsActiveEncounter(combat);
        bool succeeded = consumable.effectType switch
        {
            ConsumableEffectType.Heal => combat.HealPlayer(consumable.healAmount) > 0,
            ConsumableEffectType.DirectEnemyDamage => combat.TryUseConsumableDamage(consumable.damageAmount),
            _ => false
        };
        if (!succeeded) return false;
        CompleteConsumableUse(slotIndex, instance, combat, wasInEncounter);
        return true;
    }

    public bool UseEnhancementConsumableAtSlot(int slotIndex, int cardId)
    {
        var combat = CombatManager.Instance;
        if (!CanUseConsumableAtSlot(slotIndex, combat)) return false;
        var instance = GetConsumableInstanceAtSlot(slotIndex);
        var consumable = instance.Definition;
        if (consumable.effectType != ConsumableEffectType.ApplyEnhancement ||
            consumable.enhancementToApply == null || !CanTargetConsumableAtSlot(slotIndex, cardId)) return false;
        var card = FindOwnedCard(cardId);
        if (card == null || !ApplyEnhancement(cardId, consumable.enhancementToApply)) return false;
        CompleteConsumableUse(slotIndex, instance, combat, IsActiveEncounter(combat));
        return true;
    }

    public bool UseDeckMutationConsumableAtSlot(int slotIndex, IReadOnlyList<int> cardIds)
    {
        var combat = CombatManager.Instance;
        if (!CanUseConsumableAtSlot(slotIndex, combat) || cardIds == null) return false;
        var item = GetConsumableInstanceAtSlot(slotIndex);
        var definition = item.Definition;
        bool wasInEncounter = IsActiveEncounter(combat);
        if (definition.effectType is not (ConsumableEffectType.DuplicateCard or
            ConsumableEffectType.DestroyCards or ConsumableEffectType.ChangeSuit)) return false;
        bool inCombat = combat.currentState == GameState.PlayerTurn || combat.currentState == GameState.EnemyAttacking;
        bool requiresHandTarget = inCombat || definition.effectType == ConsumableEffectType.ChangeSuit;
        var selected = new List<CardInstance>(cardIds.Count);
        var unique = new HashSet<int>();
        foreach (int id in cardIds)
        {
            var card = FindOwnedCard(id);
            if (!unique.Add(id) || !DeckMutationService.CanTarget(_cards, card, requiresHandTarget)) return false;
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
            case ConsumableEffectType.ChangeSuit:
                if (selected.Count < 1 || selected.Count > 3 ||
                    !Enum.IsDefined(typeof(CardData.Suit), definition.targetSuit) ||
                    selected.Any(card => card.Suit == definition.targetSuit)) return false;
                var previousSuits = selected.Select(card => card.Suit).ToArray();
                int changedCount = 0;
                while (changedCount < selected.Count &&
                    selected[changedCount].TryChangeSuit(definition.targetSuit))
                    changedCount++;
                if (changedCount != selected.Count)
                {
                    for (int i = 0; i < changedCount; i++)
                        selected[i].TryChangeSuit(previousSuits[i]);
                    return false;
                }
                for (int i = 0; i < selected.Count; i++) RefreshTrackedView(selected[i]);
                success = true;
                break;
            default:
                return false;
        }
        if (!success) return false;
        CompleteConsumableUse(slotIndex, item, combat, wasInEncounter);
        OnDeckChanged?.Invoke();
        OnBuildChanged?.Invoke();
        return true;
    }

    public bool CanTargetConsumableAtSlot(int slotIndex, int cardId)
    {
        var combat = CombatManager.Instance;
        if (!CanUseConsumableAtSlot(slotIndex, combat)) return false;
        var item = GetConsumableAtSlot(slotIndex);
        var card = FindOwnedCard(cardId);
        if (item == null || card == null) return false;
        bool inCombat = combat != null && combat.currentState is GameState.PlayerTurn or GameState.EnemyAttacking;
        switch (item.effectType)
        {
            case ConsumableEffectType.ApplyEnhancement:
                return item.enhancementToApply != null && card.Enhancement == null &&
                    (!inCombat || _cards.ContainsInHand(card));
            case ConsumableEffectType.DuplicateCard:
            case ConsumableEffectType.DestroyCards:
            case ConsumableEffectType.ChangeSuit:
                return CanTargetDeckMutationAtSlot(slotIndex, cardId);
            default:
                return false;
        }
    }

    public int CountConsumableTargetsAtSlot(int slotIndex)
    {
        var item = GetConsumableAtSlot(slotIndex);
        if (item == null) return 0;
        int count = 0;
        foreach (var card in _cards.OwnedCards)
            if (card != null && CanTargetConsumableAtSlot(slotIndex, card.Id)) count++;
        return count;
    }

    public bool CanTargetDeckMutationAtSlot(int slotIndex, int cardId)
    {
        var combat = CombatManager.Instance;
        if (!CanUseConsumableAtSlot(slotIndex, combat)) return false;
        var definition = GetConsumableAtSlot(slotIndex);
        bool inCombat = combat.currentState == GameState.PlayerTurn || combat.currentState == GameState.EnemyAttacking;
        bool requiresHandTarget = inCombat || definition.effectType == ConsumableEffectType.ChangeSuit;
        var card = FindOwnedCard(cardId);
        return DeckMutationService.CanTarget(_cards, card, requiresHandTarget) &&
            (definition.effectType != ConsumableEffectType.ChangeSuit || card.Suit != definition.targetSuit);
    }

    static bool IsActiveEncounter(CombatManager combat) => combat != null &&
        combat.currentState is GameState.PlayerTurn or GameState.EnemyAttacking;

    void CompleteConsumableUse(int slotIndex, ConsumableInstance item, CombatManager combat, bool wasInEncounter)
    {
        if (slotIndex < 0 || slotIndex >= _backpack.Count || item == null ||
            !_backpack[slotIndex].Items.Contains(item) || !item.SpendCharge()) return;
        if (item.RemainingCharges == 0)
        {
            _backpack[slotIndex].Remove(item);
            if (_backpack[slotIndex].Count == 0) _backpack.RemoveAt(slotIndex);
        }
        OnConsumablesChanged?.Invoke();
        OnConsumableUsed?.Invoke(item.Definition);
        if (!wasInEncounter || _consumableUsedThisEncounter) return;
        _consumableUsedThisEncounter = true;
        for (int i = 0; i < ownedArtifacts.Count; i++)
        {
            var artifact = ownedArtifacts[i];
            if (artifact == null) continue;
            if (artifact.specialRule == ArtifactSpecialRule.ScavengersPouch)
                DrawToHand(1);
            else if (artifact.specialRule == ArtifactSpecialRule.FieldMedicsKit)
                combat?.HealPlayer(3);
        }
    }

    void NormalizeBackpackStacks()
    {
        if (ConsumablesPerSlot <= 1 || _backpack.Count == 0) return;
        var items = _backpack.SelectMany(stack => stack.Items).ToArray();
        _backpack.Clear();
        for (int i = 0; i < items.Length; i++) AddConsumableInstance(items[i]);
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
    public bool HasArtifactSpecialRule(ArtifactSpecialRule rule) => ownedArtifacts.Any(artifact =>
        artifact != null && artifact.specialRule == rule);

    public string GetArtifactAcquisitionUnavailableReason(RelicData artifact)
    {
        if (artifact == null || string.IsNullOrWhiteSpace(artifact.id)) return "Artifact unavailable";
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
        EnsureArtifactInstances();
        if (price < 0 || !string.IsNullOrEmpty(GetArtifactAcquisitionUnavailableReason(artifact))) return false;
        var predecessor = artifact.tier > 1
            ? ownedArtifacts.FirstOrDefault(value => value != null && value.canonicalId == artifact.upgradeFromId)
            : null;
        if (!SpendGold(price)) return false;
        ArtifactRuntimeInstance instance;
        if (predecessor != null)
        {
            int index = ownedArtifacts.IndexOf(predecessor);
            ownedArtifacts[index] = artifact;
            instance = _orderedArtifactInstances[index] = new ArtifactRuntimeInstance(artifact);
            relics.Remove(predecessor.id);
        }
        else
        {
            ownedArtifacts.Add(artifact);
            instance = new ArtifactRuntimeInstance(artifact);
            _orderedArtifactInstances.Add(instance);
        }
        relics.Add(artifact.id);
        var run = RunManager.Instance;
        GameplayEffectResolver.InitializeNodeCounters(new GameplayEffectSource(instance),
            run != null ? run.CompletedNodeCount : 0);
        if (artifact.specialRule == ArtifactSpecialRule.Hammer)
            instance.State.SetCounter(-140, run != null ? run.CompletedNodeCount : 0);
        if (artifact.specialRule is ArtifactSpecialRule.TravelerPackCommon or ArtifactSpecialRule.TravelerPackRare)
        {
            if (artifact.specialRule == ArtifactSpecialRule.TravelerPackRare) NormalizeBackpackStacks();
            OnConsumablesChanged?.Invoke();
        }
        run?.RefreshCurrentMapEffects();
        OnBuildChanged?.Invoke();
        return true;
    }

    public bool GrantArtifactReward(RelicData artifact, RelicData replacement = null)
    {
        EnsureArtifactInstances();
        if (artifact == null || string.IsNullOrWhiteSpace(artifact.id) || !relicCatalog.Contains(artifact)) return false;

        RelicData predecessor = artifact.tier > 1
            ? ownedArtifacts.FirstOrDefault(value => value != null &&
                string.Equals(value.canonicalId, artifact.upgradeFromId, StringComparison.Ordinal))
            : null;
        if (artifact.tier > 1 && predecessor == null) return false;
        if (artifact.tier <= 1 && !string.IsNullOrEmpty(artifact.upgradeFromId)) return false;
        if (replacement == null && predecessor != null) replacement = predecessor;
        if (replacement != null && (!ownedArtifacts.Contains(replacement) ||
            predecessor != null && replacement != predecessor)) return false;
        if (replacement == null && artifact.OccupiesCapacitySlot && ArtifactSlotsUsed >= ArtifactCapacity)
            return false;

        int index = replacement != null ? ownedArtifacts.IndexOf(replacement) : ownedArtifacts.Count;
        if (replacement != null)
        {
            ownedArtifacts[index] = artifact;
            _orderedArtifactInstances[index] = new ArtifactRuntimeInstance(artifact);
            relics.Remove(replacement.id);
        }
        else
        {
            ownedArtifacts.Add(artifact);
            _orderedArtifactInstances.Add(new ArtifactRuntimeInstance(artifact));
        }
        relics.Add(artifact.id);
        var instance = _orderedArtifactInstances[index];
        var run = RunManager.Instance;
        GameplayEffectResolver.InitializeNodeCounters(new GameplayEffectSource(instance),
            run != null ? run.CompletedNodeCount : 0);
        if (artifact.specialRule == ArtifactSpecialRule.Hammer)
            instance.State.SetCounter(-140, run != null ? run.CompletedNodeCount : 0);
        if (artifact.specialRule is ArtifactSpecialRule.TravelerPackCommon or ArtifactSpecialRule.TravelerPackRare)
        {
            if (artifact.specialRule == ArtifactSpecialRule.TravelerPackRare) NormalizeBackpackStacks();
            OnConsumablesChanged?.Invoke();
        }
        run?.RefreshCurrentMapEffects();
        OnBuildChanged?.Invoke();
        return true;
    }

    public ArtifactRuntimeInstance GetArtifactInstance(RelicData artifact)
    {
        EnsureArtifactInstances();
        if (artifact == null) return null;
        ArtifactRuntimeInstance match = null;
        for (int i = 0; i < _orderedArtifactInstances.Count; i++)
        {
            var instance = _orderedArtifactInstances[i];
            if (instance == null || instance.Definition != artifact) continue;
            if (match != null) return null;
            match = instance;
        }
        return match;
    }

    public ArtifactRuntimeInstance GetArtifactInstanceById(long instanceId)
    {
        EnsureArtifactInstances();
        return _orderedArtifactInstances.FirstOrDefault(value => value != null && value.Id == instanceId);
    }

    public ArtifactRuntimeInstance GetArtifactInstanceAt(int index)
    {
        EnsureArtifactInstances();
        return index >= 0 && index < _orderedArtifactInstances.Count ? _orderedArtifactInstances[index] : null;
    }

    void EnsureArtifactInstances()
    {
        // Keep compatibility with older callers/tests that directly edit ownedArtifacts.
        for (int i = _orderedArtifactInstances.Count - 1; i >= 0; i--)
            if (i >= ownedArtifacts.Count || _orderedArtifactInstances[i]?.Definition != ownedArtifacts[i])
                _orderedArtifactInstances.RemoveAt(i);
        while (_orderedArtifactInstances.Count < ownedArtifacts.Count)
        {
            var definition = ownedArtifacts[_orderedArtifactInstances.Count];
            _orderedArtifactInstances.Add(definition != null ? new ArtifactRuntimeInstance(definition) : null);
        }
    }

    public bool RemoveArtifactInstance(long instanceId)
    {
        EnsureArtifactInstances();
        int index = _orderedArtifactInstances.FindIndex(value => value != null && value.Id == instanceId);
        if (index < 0) return false;
        _orderedArtifactInstances.RemoveAt(index);
        ownedArtifacts.RemoveAt(index);
        RebuildRelicIds();
        RunManager.Instance?.RefreshCurrentMapEffects();
        OnBuildChanged?.Invoke();
        return true;
    }

    public RelicData GetArtifactMergeResult(long firstInstanceId, long secondInstanceId)
    {
        if (firstInstanceId == secondInstanceId) return null;
        int firstIndex = _orderedArtifactInstances.FindIndex(value => value != null && value.Id == firstInstanceId);
        int secondIndex = _orderedArtifactInstances.FindIndex(value => value != null && value.Id == secondInstanceId);
        if (firstIndex < 0 || secondIndex < 0 || firstIndex >= ownedArtifacts.Count ||
            secondIndex >= ownedArtifacts.Count) return null;
        var first = _orderedArtifactInstances[firstIndex];
        var second = _orderedArtifactInstances[secondIndex];
        var source = first.Definition;
        if (source == null || second.Definition == null ||
            ownedArtifacts[firstIndex] != source || ownedArtifacts[secondIndex] != second.Definition ||
            source.tier != second.Definition.tier || string.IsNullOrEmpty(source.canonicalId) ||
            source.canonicalId != second.Definition.canonicalId || source.tier < 1) return null;
        var nextCandidates = relicCatalog.Where(value => value != null &&
            value.tier == source.tier + 1 && value.upgradeFromId == source.canonicalId).ToArray();
        return nextCandidates.Length == 1 ? nextCandidates[0] : null;
    }

    public bool MergeArtifacts(long firstInstanceId, long secondInstanceId)
    {
        EnsureArtifactInstances();
        var next = GetArtifactMergeResult(firstInstanceId, secondInstanceId);
        if (next == null) return false;
        int firstIndex = _orderedArtifactInstances.FindIndex(value => value != null && value.Id == firstInstanceId);
        int secondIndex = _orderedArtifactInstances.FindIndex(value => value != null && value.Id == secondInstanceId);
        int firstPosition = Math.Min(firstIndex, secondIndex);
        int lastPosition = Math.Max(firstIndex, secondIndex);
        ownedArtifacts.RemoveAt(lastPosition);
        _orderedArtifactInstances.RemoveAt(lastPosition);
        ownedArtifacts.RemoveAt(firstPosition);
        _orderedArtifactInstances.RemoveAt(firstPosition);
        var merged = new ArtifactRuntimeInstance(next);
        ownedArtifacts.Insert(firstPosition, next);
        _orderedArtifactInstances.Insert(firstPosition, merged);
        RebuildRelicIds();
        var run = RunManager.Instance;
        GameplayEffectResolver.InitializeNodeCounters(new GameplayEffectSource(merged), run != null ? run.CompletedNodeCount : 0);
        if (next.specialRule == ArtifactSpecialRule.Hammer)
            merged.State.SetCounter(-140, run != null ? run.CompletedNodeCount : 0);
        run?.RefreshCurrentMapEffects();
        OnBuildChanged?.Invoke();
        return true;
    }

    void RebuildRelicIds()
    {
        relics.Clear();
        foreach (var artifact in ownedArtifacts)
            if (artifact != null) relics.Add(artifact.id);
    }

    public void ResetArtifactEncounterEffectState()
    {
        EnsureArtifactInstances();
        _consumableUsedThisEncounter = false;
        foreach (var artifact in _orderedArtifactInstances)
            artifact?.ResetEncounterState();
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

    public bool ApplyEnhancement(int cardId, CardEnhancementData enhancement, bool replaceExisting = false)
    {
        var card = FindOwnedCard(cardId);
        if (card == null || !card.TryApplyEnhancement(enhancement, replaceExisting)) return false;
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

    public bool BuyEnhancement(int cardId, CardEnhancementData enhancement, int price, bool replaceExisting = false)
    {
        var card = FindOwnedCard(cardId);
        if (card == null || (card.Enhancement != null && !replaceExisting) ||
            ReferenceEquals(card.Enhancement, enhancement) || enhancement == null || price < 0 || !CanAfford(price))
            return false;
        if (!SpendGold(price)) return false;
        if (card.TryApplyEnhancement(enhancement, replaceExisting))
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
        var combat = CombatManager.Instance;
        int normalCapacity = HandCapacity;
        int drawCapacity = normalCapacity + (combat != null ? combat.OverflowDrawAllowance : 0);
        int drawn = _cards.DrawToHand(count, drawCapacity);
        if (drawn <= 0) return 0;

        if (CanCreateViews())
        {
            for (int i = previousHandCount; i < _cards.HandCount; i++)
            {
                var view = CreateView(_cards.Hand[i]);
                if (i >= normalCapacity && combat != null) combat.QueueOverflowAutoPlay(view);
            }
        }

        OnDeckChanged?.Invoke();
        return drawn;
    }

    public bool TryReturnDiscardCardToHand(CardInstance card)
    {
        if (!_cards.TryReturnDiscardToHand(card, HandCapacity)) return false;
        if (CanCreateViews()) CreateView(card);
        OnDeckChanged?.Invoke();
        return true;
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

    public bool TryDiscardCards(IReadOnlyList<CardView> cards) => TryCommitHandCards(cards, false);

    public bool TryRecycleDefenseCards(IReadOnlyList<CardView> cards) => TryCommitHandCards(cards, true);

    bool TryCommitHandCards(IReadOnlyList<CardView> cards, bool recycleToDeck)
    {
        if (cards == null || cards.Count == 0) return false;

        var committedViews = new List<CardView>(cards.Count);
        var uniqueViews = new HashSet<CardView>();
        var instances = new List<CardInstance>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            var view = cards[i];
            if (view == null || view.data == null || !_trackedViews.Contains(view) ||
                !uniqueViews.Add(view) || !_cards.ContainsInHand(view.data))
                return false;
            instances.Add(view.data);
            committedViews.Add(view);
        }

        if (!(recycleToDeck ? _cards.TryRecycleHandToDeck(instances) : _cards.TryDiscard(instances))) return false;

        // Selection may be the input list; removing selected views must not shorten our iteration.
        for (int i = 0; i < committedViews.Count; i++)
        {
            var view = committedViews[i];
            _selectedCards.Remove(view);
            view.SetSelectionOrder(0);
            DetachAndDestroyView(view);
        }

        RefreshSelectionPresentation();
        OnDeckChanged?.Invoke();
        return true;
    }

    public void NotifyDeckChanged() => OnDeckChanged?.Invoke();

    public void BeginHandEnhancementTargeting(Action<CardView> onTarget) =>
        BeginHandTargeting(onTarget, null);

    public void BeginHandTargeting(Action<CardView> onTarget, Func<int, bool> isEligible)
    {
        EndEnemyTargeting();
        EndHandEnhancementTargeting();
        _handEnhancementTargetHandler = onTarget;
        _handTargetEligibility = isEligible;
        ClearSelection();
        CancelCardInteractions();
        RefreshHandTargetPresentation();
    }

    public void BeginEnemyTargeting(Action<EnemyRuntime> onTarget, Func<EnemyRuntime, bool> isEligible)
    {
        EndEnemyTargeting();
        if (IsHandEnhancementTargeting) EndHandEnhancementTargeting();
        if (onTarget == null) return;
        _enemyTargetHandler = onTarget;
        _enemyTargetEligibility = isEligible;
        if (_activeDragCard != null) _activeDragCard.CancelActiveDrag();
        _activeDragCard = null;
        OnEnemyTargetingChanged?.Invoke(true);
    }

    public bool HandleEnemyTargetClick(EnemyRuntime enemy)
    {
        if (!IsEnemyTargeting) return false;
        if (!GameplayInputGate.IsBlocked && enemy != null &&
            (_enemyTargetEligibility == null || _enemyTargetEligibility(enemy)))
            _enemyTargetHandler?.Invoke(enemy);
        return true;
    }

    public void EndEnemyTargeting()
    {
        if (!IsEnemyTargeting) return;
        _enemyTargetHandler = null;
        _enemyTargetEligibility = null;
        OnEnemyTargetingChanged?.Invoke(false);
    }

    public void EndHandEnhancementTargeting()
    {
        _handEnhancementTargetHandler = null;
        _handTargetEligibility = null;
        foreach (var view in _trackedViews)
        {
            if (view == null) continue;
            view.SetHandTargetState(false, false);
            view.SetSelected(false);
        }
        ClearSelection();
    }

    public void RefreshHandTargetPresentation()
    {
        foreach (var view in _trackedViews)
        {
            if (view == null) continue;
            bool isHand = view.data != null && _cards.ContainsInHand(view.data);
            bool targeting = IsHandEnhancementTargeting && isHand;
            bool eligible = targeting && (_handTargetEligibility == null || _handTargetEligibility(view.data.Id));
            view.SetHandTargetState(targeting, eligible);
        }
    }

    public ConsumableTargetType GetConsumableTargetTypeAtSlot(int slotIndex)
    {
        var item = GetConsumableAtSlot(slotIndex);
        if (item == null) return ConsumableTargetType.None;
        return item.effectType switch
        {
            ConsumableEffectType.DirectEnemyDamage => ConsumableTargetType.Enemy,
            ConsumableEffectType.ApplyEnhancement or ConsumableEffectType.DuplicateCard or
                ConsumableEffectType.DestroyCards or ConsumableEffectType.ChangeSuit => ConsumableTargetType.Card,
            _ => ConsumableTargetType.None
        };
    }

    public IReadOnlyList<EnemyRuntime> GetValidEnemyTargetsForConsumableAtSlot(int slotIndex)
    {
        var combat = CombatManager.Instance;
        var result = new List<EnemyRuntime>();
        var item = GetConsumableAtSlot(slotIndex);
        if (item == null || item.effectType != ConsumableEffectType.DirectEnemyDamage ||
            !CanUseConsumableAtSlot(slotIndex, combat) || combat == null) return result;
        foreach (var enemy in combat.Enemies)
            if (enemy != null && combat.CanUseConsumableDamage(item.damageAmount, enemy)) result.Add(enemy);
        return result;
    }

    public int GetConsumableEffectTargetMinimumAtSlot(int slotIndex) =>
        GetConsumableTargetTypeAtSlot(slotIndex) == ConsumableTargetType.None ? 0 : 1;

    public int GetConsumableEffectTargetMaximumAtSlot(int slotIndex)
    {
        var type = GetConsumableTargetTypeAtSlot(slotIndex);
        return type == ConsumableTargetType.Enemy ? 1 :
            type == ConsumableTargetType.Card ? GetConsumableTargetMaximum(slotIndex) : 0;
    }

    public int GetConsumableTargetMinimum(int slotIndex) =>
        GetConsumableAtSlot(slotIndex) == null ? 0 : 1;

    public int GetConsumableTargetMaximum(int slotIndex)
    {
        var item = GetConsumableAtSlot(slotIndex);
        if (item == null) return 0;
        return item.effectType switch
        {
            ConsumableEffectType.DestroyCards => 2,
            ConsumableEffectType.ChangeSuit => 3,
            ConsumableEffectType.DuplicateCard or ConsumableEffectType.ApplyEnhancement => 1,
            _ => 0
        };
    }

    public bool ConsumableRequiresHandTarget(int slotIndex)
    {
        var item = GetConsumableAtSlot(slotIndex);
        var combat = CombatManager.Instance;
        if (item == null || combat == null) return false;
        return item.effectType == ConsumableEffectType.ChangeSuit ||
            combat.currentState is GameState.PlayerTurn or GameState.EnemyAttacking;
    }

    public bool CanConfirmConsumableTargets(int slotIndex, IReadOnlyList<int> cardIds) =>
        CanConfirmConsumableTargetsAtSlot(slotIndex, cardIds);

    public bool CanConfirmConsumableTargetsAtSlot(int slotIndex, IReadOnlyList<int> cardIds)
    {
        if (cardIds == null || cardIds.Count < GetConsumableTargetMinimum(slotIndex) ||
            cardIds.Count > GetConsumableTargetMaximum(slotIndex) ||
            !CanUseConsumableAtSlot(slotIndex, CombatManager.Instance)) return false;
        var unique = new HashSet<int>();
        foreach (int id in cardIds)
            if (!unique.Add(id) || !CanTargetConsumableAtSlot(slotIndex, id)) return false;
        var item = GetConsumableAtSlot(slotIndex);
        if (item != null && item.effectType == ConsumableEffectType.DestroyCards &&
            cardIds.Count >= _cards.OwnedCards.Count) return false;
        return true;
    }

    public bool CanRouteHandEnhancementTargetClick => IsHandEnhancementTargeting &&
        (!GameplayInputGate.IsBlocked || RunManager.Instance != null && RunManager.Instance.IsPendingHammerReward &&
            GameplayInputGate.Reasons == GameplayInputBlockReason.ArtifactChoice);

    public void HandleCardClick(CardView card)
    {
        if (IsEnemyTargeting) return;
        if (IsHandEnhancementTargeting)
        {
            if (CanRouteHandEnhancementTargetClick && IsTrackedHandView(card) &&
                (_handTargetEligibility == null || _handTargetEligibility(card.data.Id)))
                _handEnhancementTargetHandler?.Invoke(card);
            return;
        }
        ToggleCardSelection(card);
    }

    bool IsTrackedHandView(CardView card) => card != null && card.data != null &&
        _trackedViews.Contains(card) && _cards.ContainsInHand(card.data);

    public void CancelCardInteractions()
    {
        EndEnemyTargeting();
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
        if (IsHandEnhancementTargeting)
        {
            if (CanRouteHandEnhancementTargetClick && IsTrackedHandView(card) &&
                (_handTargetEligibility == null || _handTargetEligibility(card.data.Id)))
                _handEnhancementTargetHandler?.Invoke(card);
            return;
        }
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
            !GameplayEffectResolver.CanContinueAttackSelection(_selectedCards, card, this))
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
        gold = (int)Math.Clamp((long)gold + amount, 0L, int.MaxValue);
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
        _orderedArtifactInstances.Clear();
        _backpack.Clear();
        _consumableUsedThisEncounter = false;

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

    CardView CreateView(CardInstance cardInstance)
    {
        var view = Instantiate(cardPrefab);
        _trackedViews.Add(view);
        handField.AddCard(view, cardInstance);
        if (IsHandEnhancementTargeting)
            view.SetHandTargetState(true, _handTargetEligibility == null || _handTargetEligibility(cardInstance.Id));
        return view;
    }

    void ClearCardState()
    {
        if (_activeDragCard != null)
            _activeDragCard.CancelActiveDrag();
        _activeDragCard = null;
        _handEnhancementTargetHandler = null;
        _handTargetEligibility = null;
        foreach (var view in _trackedViews)
            if (view != null)
            {
                view.SetHandTargetState(false, false);
                view.SetSelected(false);
            }
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
        return !IsHandEnhancementTargeting && !IsEnemyTargeting && !GameplayInputGate.IsBlocked && card != null && card.data != null &&
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
