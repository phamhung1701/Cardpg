using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActionButtonsUI : MonoBehaviour
{
    [Header("References")]
    public Button playButton;
    public Button discardButton;
    public TMP_Text statusLabel;

    void OnEnable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnStateChanged += HandleStateChanged;
            CombatManager.Instance.OnPendingDamageChanged += HandlePendingDamage;
            CombatManager.Instance.OnCombatLog += HandleCombatLog;
        }
        if (CardManager.Instance != null)
            CardManager.Instance.OnCardSelected += HandleSelectionChanged;

        if (playButton) playButton.onClick.AddListener(OnPlayClicked);
        if (discardButton) discardButton.onClick.AddListener(OnDiscardClicked);
    }

    void OnDisable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnStateChanged -= HandleStateChanged;
            CombatManager.Instance.OnPendingDamageChanged -= HandlePendingDamage;
            CombatManager.Instance.OnCombatLog -= HandleCombatLog;
        }
        if (CardManager.Instance != null)
            CardManager.Instance.OnCardSelected -= HandleSelectionChanged;

        if (playButton) playButton.onClick.RemoveListener(OnPlayClicked);
        if (discardButton) discardButton.onClick.RemoveListener(OnDiscardClicked);
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

        if (playButton) playButton.interactable = canPlay;
        if (discardButton) discardButton.interactable = canDiscard;
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
            CombatManager.Instance.DiscardCard(card);
    }
}
