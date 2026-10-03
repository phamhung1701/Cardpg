using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ActionButtonsUI : MonoBehaviour
{
    [Header("References")]
    public Button playButton;
    public Button blockButton;
    public Button rankSortButton;
    public Button suitSortButton;
    public Field handField;
    public Button takeDamageButton;
    public Button recoverButton;
    public Button[] backpackSlotButtons = new Button[CardManager.BACKPACK_CAPACITY];
    public TMP_Text playerHealthLabel;
    public TMP_Text turnStateLabel;
    public TMP_Text pendingDamageLabel;
    public TMP_Text selectionLabel;
    public TMP_Text combatLogLabel;

    TMP_Text _primaryActionLabel;
    UnityAction[] _backpackSlotListeners;
    EnhancementTargetUI _enhancementTargetUI;

    void OnEnable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnStateChanged += HandleStateChanged;
            CombatManager.Instance.OnPendingDamageChanged += HandlePendingDamage;
            CombatManager.Instance.OnPlayerHealthChanged += HandlePlayerHealthChanged;
            CombatManager.Instance.OnCombatLog += HandleCombatLog;
        }
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCardSelected += HandleSelectionChanged;
            CardManager.Instance.OnDeckChanged += HandleDeckChanged;
            CardManager.Instance.OnConsumablesChanged += HandleDeckChanged;
        }

        _primaryActionLabel = playButton != null ? playButton.GetComponentInChildren<TMP_Text>(true) : null;
        _enhancementTargetUI = GetComponent<EnhancementTargetUI>();

        if (playButton) playButton.onClick.AddListener(OnPlayClicked);
        if (blockButton) blockButton.onClick.AddListener(OnBlockClicked);
        if (rankSortButton) rankSortButton.onClick.AddListener(OnRankSortClicked);
        if (suitSortButton) suitSortButton.onClick.AddListener(OnSuitSortClicked);
        if (takeDamageButton) takeDamageButton.onClick.AddListener(OnTakeDamageClicked);
        if (recoverButton) recoverButton.onClick.AddListener(OnRecoverClicked);
        BindBackpackSlots();

        RefreshAll();
    }

    void OnDisable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnStateChanged -= HandleStateChanged;
            CombatManager.Instance.OnPendingDamageChanged -= HandlePendingDamage;
            CombatManager.Instance.OnPlayerHealthChanged -= HandlePlayerHealthChanged;
            CombatManager.Instance.OnCombatLog -= HandleCombatLog;
        }
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCardSelected -= HandleSelectionChanged;
            CardManager.Instance.OnDeckChanged -= HandleDeckChanged;
            CardManager.Instance.OnConsumablesChanged -= HandleDeckChanged;
        }

        if (playButton) playButton.onClick.RemoveListener(OnPlayClicked);
        if (blockButton) blockButton.onClick.RemoveListener(OnBlockClicked);
        if (rankSortButton) rankSortButton.onClick.RemoveListener(OnRankSortClicked);
        if (suitSortButton) suitSortButton.onClick.RemoveListener(OnSuitSortClicked);
        if (takeDamageButton) takeDamageButton.onClick.RemoveListener(OnTakeDamageClicked);
        if (recoverButton) recoverButton.onClick.RemoveListener(OnRecoverClicked);
        UnbindBackpackSlots();
    }

    void HandleStateChanged(GameState _) => RefreshAll();
    void HandlePendingDamage(int _) => RefreshAll();
    void HandleSelectionChanged(CardView _) => RefreshAll();
    void HandleDeckChanged() => RefreshAll();
    void HandlePlayerHealthChanged(int _, int __) => RefreshAll();

    void HandleCombatLog(string message)
    {
        if (combatLogLabel) combatLogLabel.text = message;
        RefreshAll();
    }

    void RefreshAll()
    {
        var combat = CombatManager.Instance;
        var cards = CardManager.Instance;
        if (combat == null || cards == null) return;

        int selectedCount = cards.SelectedCards.Count;
        bool hasSelected = selectedCount > 0;
        bool canPlay = combat.CanPlayCards(cards.SelectedCards, combat.currentEnemy);
        bool canDefend = combat.CanDefendWithCards(cards.SelectedCards);
        bool isDefense = combat.currentState == GameState.EnemyAttacking;
        bool canTakeDamage = isDefense && combat.pendingDamage > 0;
        bool canRecover = combat.currentState == GameState.PlayerTurn && cards.HandCount == 0;

        if (playButton) playButton.interactable = combat.currentState == GameState.PlayerTurn && canPlay;
        if (blockButton) blockButton.interactable = isDefense && canDefend;
        if (rankSortButton) rankSortButton.interactable = cards.HandCount > 1;
        if (suitSortButton) suitSortButton.interactable = cards.HandCount > 1;
        if (_primaryActionLabel)
        {
            _primaryActionLabel.text = combat.currentState switch
            {
                GameState.PlayerTurn when GameplayEffectResolver.IsRoyalFamilySelection(cards.SelectedCards.Select(view => view.data).ToArray(), cards) => "PLAY ROYAL FAMILY",
                GameState.PlayerTurn when selectedCount == 2 && GameplayEffectResolver.CanPlayAsAcePair(cards.SelectedCards) => "PLAY ACE PAIR",
                GameState.PlayerTurn when selectedCount >= 2 && GameplayEffectResolver.IsSameRankMultiCardAction(cards.SelectedCards, cards) => $"PLAY SAME-RANK {selectedCount}",
                GameState.PlayerTurn => "PLAY CARD",
                GameState.EnemyAttacking => "PLAY",
                _ => "CARD ACTION"
            };
        }
        if (takeDamageButton) takeDamageButton.interactable = canTakeDamage;
        if (recoverButton) recoverButton.interactable = canRecover;

        RefreshBackpackSlots(cards, combat);

        if (playerHealthLabel)
        {
            string block = combat.player.EncounterBlock > 0 ? $"  •  BLOCK {combat.player.EncounterBlock}" : string.Empty;
            string shield = combat.player.ShieldCharges > 0 ? $"  •  SHIELD {combat.player.ShieldCharges}" : string.Empty;
            playerHealthLabel.text = combat.HasInfiniteHealthForDev
                ? $"PLAYER HP  ∞{block}{shield}"
                : $"PLAYER HP  {combat.player.currentHealth}/{combat.player.maxHealth}{block}{shield}";
        }

        if (turnStateLabel)
        {
            turnStateLabel.text = combat.currentState switch
            {
                GameState.PlayerTurn => combat.IsBonusPlayerAction ? "PLAYER TURN  •  BONUS ACTION" : "PLAYER TURN",
                GameState.EnemyAttacking => "DEFENSE WINDOW",
                GameState.GameWon => "ENCOUNTER WON",
                GameState.GameOver => "RUN DEFEAT",
                _ => "CHOOSE A ROUTE"
            };
        }

        if (pendingDamageLabel)
        {
            bool showDamage = combat.currentState == GameState.EnemyAttacking && combat.pendingDamage > 0;
            pendingDamageLabel.gameObject.SetActive(showDamage);
            pendingDamageLabel.text = showDamage
                ? $"ATTACKS BLOCKED  {combat.BlockedAttackCount}  •  PENDING  {combat.PendingAttackCount}  •  REMAINING DAMAGE  {combat.pendingDamage}"
                : string.Empty;
        }

        if (selectionLabel)
        {
            selectionLabel.text = combat.currentState switch
            {
                GameState.PlayerTurn when cards.HandCount == 0 =>
                    $"Hand empty: Recover takes {combat.TotalEnemyAttack} damage, then draws 1 card",
                GameState.PlayerTurn when GameplayEffectResolver.IsRoyalFamilySelection(cards.SelectedCards.Select(view => view.data).ToArray(), cards) =>
                    "Royal Family: exact 10/J/Q/K/A in one effective suit — instant defeat. Press Play or drag to target",
                GameState.PlayerTurn when GameplayEffectResolver.IsRoyalFamilyPartialSelection(cards.SelectedCards, cards) =>
                    $"Royal Family selection ({selectedCount}/5)  •  exact 10/J/Q/K/A, one effective suit; target will be instantly defeated when complete",
                GameState.PlayerTurn when selectedCount == 2 && GameplayEffectResolver.CanPlayAsAcePair(cards.SelectedCards) =>
                    $"Ace pair: {cards.SelectedCards[0].data.DisplayName} + {cards.SelectedCards[1].data.DisplayName}  •  Press Play or drag to the enemy",
                GameState.PlayerTurn when selectedCount >= 2 && GameplayEffectResolver.IsSameRankMultiCardAction(cards.SelectedCards, cards) =>
                    $"Same-rank set ({selectedCount} cards): {cards.SelectedCards[0].data.Rank}  •  Press Play or drag to the enemy",
                GameState.PlayerTurn when hasSelected =>
                    $"Selected: {cards.selectedCard.data.DisplayName}  •  Press Play or drag to the enemy",
                GameState.PlayerTurn => "Select a card, then press Play or drag it to the enemy",
                GameState.EnemyAttacking when hasSelected && combat.TryPreviewDefense(cards.SelectedCards, out int blocked, out int remaining) =>
                    $"{selectedCount} selected  •  Blocks {blocked} of {combat.PendingAttackCount} pending attacks  •  Remaining damage {remaining}",
                GameState.EnemyAttacking when hasSelected =>
                    "Selected cards cannot each block a distinct pending attack",
                GameState.EnemyAttacking => "Select cards to Block attacks, or Take Damage",
                GameState.GameWon => "Encounter cleared — choose the next route",
                GameState.GameOver => "The run has ended",
                _ => "Choose an available route on the map"
            };
        }
    }

    void OnPrimaryActionClicked()
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        if (cards == null || combat == null) return;

        var selection = cards.GetSelectedCardsSnapshot();
        if (combat.currentState == GameState.PlayerTurn)
            TryPlaySelectedCards(cards, combat);
        else if (combat.currentState == GameState.EnemyAttacking)
        {
            var presentation = AttackCardPresentationUI.Instance;
            if (presentation != null)
                presentation.TryDefendCards(selection, combat.currentEnemy);
            else
                combat.TryDefendWithCards(selection);
        }
    }

    void OnPlayClicked()
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        if (cards != null && combat != null && combat.currentState == GameState.PlayerTurn)
            TryPlaySelectedCards(cards, combat);
    }

    static void TryPlaySelectedCards(CardManager cards, CombatManager combat)
    {
        var selected = cards.GetSelectedCardsSnapshot();
        var presentation = AttackCardPresentationUI.Instance;
        if (presentation != null)
            presentation.TryPlayCards(selected, combat.currentEnemy);
        else
            combat.TryPlayCards(selected, combat.currentEnemy);
    }

    void OnBlockClicked()
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        if (cards != null && combat != null && combat.currentState == GameState.EnemyAttacking)
        {
            var selected = cards.GetSelectedCardsSnapshot();
            var presentation = AttackCardPresentationUI.Instance;
            if (presentation != null)
                presentation.TryDefendCards(selected, combat.currentEnemy);
            else
                combat.TryDefendWithCards(selected);
        }
    }

    void OnRankSortClicked() => (handField != null ? handField : CardManager.Instance?.handField)?.SortByRank();
    void OnSuitSortClicked() => (handField != null ? handField : CardManager.Instance?.handField)?.SortBySuit();

    // Kept for existing serialized UnityEvent bindings and interaction characterization.
    void OnDiscardClicked() => OnBlockClicked();

    void OnTakeDamageClicked() => CombatManager.Instance?.TakeRemainingDamage();
    void OnRecoverClicked() => CombatManager.Instance?.Recover();

    void BindBackpackSlots()
    {
        UnbindBackpackSlots();
        if (backpackSlotButtons == null) return;
        _backpackSlotListeners = new UnityAction[backpackSlotButtons.Length];
        for (int i = 0; i < backpackSlotButtons.Length; i++)
        {
            if (!backpackSlotButtons[i]) continue;
            int slotIndex = i;
            _backpackSlotListeners[i] = () => UseBackpackSlot(slotIndex);
            backpackSlotButtons[i].onClick.AddListener(_backpackSlotListeners[i]);
        }
    }

    void UnbindBackpackSlots()
    {
        if (_backpackSlotListeners == null || backpackSlotButtons == null) return;
        int count = Mathf.Min(_backpackSlotListeners.Length, backpackSlotButtons.Length);
        for (int i = 0; i < count; i++)
            if (backpackSlotButtons[i] && _backpackSlotListeners[i] != null)
                backpackSlotButtons[i].onClick.RemoveListener(_backpackSlotListeners[i]);
        _backpackSlotListeners = null;
    }

    void UseBackpackSlot(int slotIndex)
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        var consumable = cards != null ? cards.GetConsumableAtSlot(slotIndex) : null;
        if (cards == null || combat == null || consumable == null) return;
        if (consumable.effectType == ConsumableEffectType.ApplyEnhancement)
        {
            _enhancementTargetUI?.OpenConsumableTarget(slotIndex);
            return;
        }
        if (consumable.effectType is ConsumableEffectType.DuplicateCard or
            ConsumableEffectType.DestroyCards or ConsumableEffectType.ChangeSuit)
        {
            _enhancementTargetUI?.OpenDeckMutationTarget(slotIndex);
            return;
        }
        cards.UseConsumableAtSlot(slotIndex, combat);
    }

    void RefreshBackpackSlots(CardManager cards, CombatManager combat)
    {
        if (backpackSlotButtons == null) return;
        for (int i = 0; i < backpackSlotButtons.Length; i++)
        {
            var button = backpackSlotButtons[i];
            if (!button) continue;
            bool visible = i < Mathf.Max(cards.BackpackCapacity, cards.BackpackSlotsUsed);
            button.gameObject.SetActive(visible);
            if (!visible) continue;
            var consumable = cards.GetConsumableAtSlot(i);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label)
            {
                int stackCount = cards.GetConsumableStackCountAtSlot(i);
                var activeItem = cards.GetConsumableInstanceAtSlot(i);
                label.text = consumable == null
                    ? $"EMPTY SLOT {i + 1}"
                    : $"{consumable.icon} {consumable.displayName}" +
                        (stackCount > 1 ? $" ×{stackCount}" : string.Empty) +
                        (consumable.uses > 1 && activeItem != null
                            ? $" ({activeItem.RemainingCharges}/{consumable.uses} first)" : string.Empty);
            }
            button.interactable = cards.CanUseConsumableAtSlot(i, combat) &&
                (consumable == null || consumable.effectType is not
                    (ConsumableEffectType.ApplyEnhancement or ConsumableEffectType.DuplicateCard or
                     ConsumableEffectType.DestroyCards or ConsumableEffectType.ChangeSuit) ||
                    _enhancementTargetUI != null && _enhancementTargetUI.CanOpenConsumableTarget);
        }
    }
}
