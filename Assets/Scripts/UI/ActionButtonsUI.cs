using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActionButtonsUI : MonoBehaviour
{
    [Header("References")]
    public Button playButton;
    public Button discardButton;
    public Button takeDamageButton;
    public Button recoverButton;
    public TMP_Text playerHealthLabel;
    public TMP_Text statusLabel;

    void OnEnable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnStateChanged += HandleStateChanged;
            CombatManager.Instance.OnPendingDamageChanged += HandlePendingDamage;
            CombatManager.Instance.OnPlayerHealthChanged += HandlePlayerHealthChanged;
            CombatManager.Instance.OnCombatLog += HandleCombatLog;
            HandlePlayerHealthChanged(
                CombatManager.Instance.player.currentHealth,
                CombatManager.Instance.player.maxHealth);
        }
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCardSelected += HandleSelectionChanged;
            CardManager.Instance.OnDeckChanged += HandleDeckChanged;
        }

        if (playButton) playButton.onClick.AddListener(OnPlayClicked);
        if (discardButton) discardButton.onClick.AddListener(OnDiscardClicked);
        if (takeDamageButton) takeDamageButton.onClick.AddListener(OnTakeDamageClicked);
        if (recoverButton) recoverButton.onClick.AddListener(OnRecoverClicked);
        UpdateButtons();
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

        if (playButton) playButton.onClick.RemoveListener(OnPlayClicked);
        if (discardButton) discardButton.onClick.RemoveListener(OnDiscardClicked);
        if (takeDamageButton) takeDamageButton.onClick.RemoveListener(OnTakeDamageClicked);
        if (recoverButton) recoverButton.onClick.RemoveListener(OnRecoverClicked);
    }

    void HandleStateChanged(GameState state)
    {
        UpdateButtons();
    }

    void HandlePendingDamage(int damage)
    {
        UpdateButtons();
    }

    void HandleSelectionChanged(CardView card)
    {
        UpdateButtons();
    }

    void HandleDeckChanged()
    {
        UpdateButtons();
    }

    void HandlePlayerHealthChanged(int current, int max)
    {
        if (playerHealthLabel) playerHealthLabel.text = $"Player HP: {current}/{max}";
        UpdateButtons();
    }

    void HandleCombatLog(string msg)
    {
        if (statusLabel) statusLabel.text = msg;
    }

    void UpdateButtons()
    {
        var cm = CombatManager.Instance;
        var cardMgr = CardManager.Instance;
        if (cm == null || cardMgr == null) return;

        bool hasSelected = cardMgr.selectedCard != null;
        bool canPlay = cm.currentState == GameState.PlayerTurn && hasSelected;
        bool canDiscard = cm.currentState == GameState.EnemyAttacking && hasSelected;
        bool canTakeDamage = cm.currentState == GameState.EnemyAttacking && cm.pendingDamage > 0;
        bool canRecover = cm.currentState == GameState.PlayerTurn && cardMgr.HandCount == 0;

        if (playButton) playButton.interactable = canPlay;
        if (discardButton) discardButton.interactable = canDiscard;
        if (takeDamageButton) takeDamageButton.interactable = canTakeDamage;
        if (recoverButton) recoverButton.interactable = canRecover;
    }

    void OnPlayClicked()
    {
        var card = CardManager.Instance?.selectedCard;
        if (card != null)
            CombatManager.Instance.PlayCard(card);
    }

    void OnDiscardClicked()
    {
        var card = CardManager.Instance?.selectedCard;
        if (card != null)
            CombatManager.Instance?.DiscardCard(card);
    }

    void OnTakeDamageClicked()
    {
        CombatManager.Instance?.TakeRemainingDamage();
    }

    void OnRecoverClicked()
    {
        CombatManager.Instance?.Recover();
    }
}
