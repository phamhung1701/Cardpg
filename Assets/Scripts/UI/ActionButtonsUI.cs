using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActionButtonsUI : MonoBehaviour
{
    [Header("References")]
    public Button playButton;
    public Button takeDamageButton;
    public Button recoverButton;
    public TMP_Text playerHealthLabel;
    public TMP_Text turnStateLabel;
    public TMP_Text pendingDamageLabel;
    public TMP_Text selectionLabel;
    public TMP_Text combatLogLabel;

    TMP_Text _primaryActionLabel;

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
        }

        _primaryActionLabel = playButton != null ? playButton.GetComponentInChildren<TMP_Text>(true) : null;

        if (playButton) playButton.onClick.AddListener(OnPrimaryActionClicked);
        if (takeDamageButton) takeDamageButton.onClick.AddListener(OnTakeDamageClicked);
        if (recoverButton) recoverButton.onClick.AddListener(OnRecoverClicked);

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
        }

        if (playButton) playButton.onClick.RemoveListener(OnPrimaryActionClicked);
        if (takeDamageButton) takeDamageButton.onClick.RemoveListener(OnTakeDamageClicked);
        if (recoverButton) recoverButton.onClick.RemoveListener(OnRecoverClicked);
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

        if (playButton) playButton.interactable = isDefense ? canDefend : canPlay;
        if (_primaryActionLabel)
        {
            _primaryActionLabel.text = combat.currentState switch
            {
                GameState.PlayerTurn => "PLAY CARD",
                GameState.EnemyAttacking when selectedCount > 0 => $"DEFEND WITH {selectedCount} CARD{(selectedCount == 1 ? string.Empty : "S")}",
                GameState.EnemyAttacking => "SELECT CARDS TO DEFEND",
                _ => "CARD ACTION"
            };
        }
        if (takeDamageButton) takeDamageButton.interactable = canTakeDamage;
        if (recoverButton) recoverButton.interactable = canRecover;

        if (playerHealthLabel)
            playerHealthLabel.text = $"PLAYER HP  {combat.player.currentHealth}/{combat.player.maxHealth}";

        if (turnStateLabel)
        {
            turnStateLabel.text = combat.currentState switch
            {
                GameState.PlayerTurn => "PLAYER TURN",
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
            pendingDamageLabel.text = showDamage ? $"INCOMING DAMAGE  {combat.pendingDamage}" : string.Empty;
        }

        if (selectionLabel)
        {
            selectionLabel.text = combat.currentState switch
            {
                GameState.PlayerTurn when cards.HandCount == 0 =>
                    $"Hand empty: Recover takes {combat.TotalEnemyAttack} damage, then draws 1 card",
                GameState.PlayerTurn when hasSelected =>
                    $"Selected: {cards.selectedCard.data.DisplayName}  •  Press Play or drag to the enemy",
                GameState.PlayerTurn => "Select a card, then press Play or drag it to the enemy",
                GameState.EnemyAttacking when hasSelected =>
                    $"{selectedCount} selected  •  Blocks {GetSelectedDefense(cards)} of {combat.pendingDamage} incoming damage",
                GameState.EnemyAttacking => "Select card(s) to defend, drag them to the enemy, or Take Damage",
                GameState.GameWon => "Encounter cleared — choose the next route",
                GameState.GameOver => "The run has ended",
                _ => "Choose an available route on the map"
            };
        }
    }

    static int GetSelectedDefense(CardManager cards)
    {
        if (cards == null) return 0;
        return CombatManager.Instance != null
            ? CombatManager.Instance.CalculateSelectedDefense(cards.SelectedCards)
            : 0;
    }

    void OnPrimaryActionClicked()
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        if (cards == null || combat == null) return;

        var selection = cards.GetSelectedCardsSnapshot();
        if (combat.currentState == GameState.PlayerTurn)
            combat.TryPlayCards(selection, combat.currentEnemy);
        else if (combat.currentState == GameState.EnemyAttacking)
            combat.TryDefendWithCards(selection);
    }

    void OnPlayClicked() => OnPrimaryActionClicked();
    void OnDiscardClicked() => OnPrimaryActionClicked();

    void OnTakeDamageClicked() => CombatManager.Instance?.TakeRemainingDamage();
    void OnRecoverClicked() => CombatManager.Instance?.Recover();
}
