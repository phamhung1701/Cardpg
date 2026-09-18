using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presentation-only drop target. It validates and forwards a card intent to CombatManager;
/// authoritative card and combat mutations remain in the model/managers.
/// </summary>
public class CardActionDropTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public EnemyDisplayUI enemyDisplay;
    public Graphic feedbackGraphic;
    public TMP_Text guidanceLabel;

    [Header("Feedback")]
    public Color validHoverColor = new(0.32f, 0.7f, 0.44f, 1f);
    public Color invalidHoverColor = new(0.72f, 0.3f, 0.3f, 1f);

    Color _baseColor;
    bool _hasBaseColor;

    void Awake()
    {
        ResolveReferences();
        CaptureBaseColor();
    }

    void OnEnable()
    {
        ResolveReferences();
        CaptureBaseColor();
        ResetFeedback();
    }

    void OnDisable() => ResetFeedback();

    public bool CanAccept(CardView draggedCard)
    {
        if (GameplayInputGate.IsBlocked || draggedCard == null) return false;
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        if (cards == null || combat == null || cards.ActiveDragCard != draggedCard) return false;

        var selection = cards.GetSelectedCardsSnapshot();
        var target = ResolveEnemyTarget();
        return combat.currentState switch
        {
            GameState.PlayerTurn => combat.CanPlayCards(selection, target),
            GameState.EnemyAttacking => combat.CanDefendWithCards(selection, target),
            _ => false
        };
    }

    public bool TryCommit(CardView draggedCard)
    {
        if (!CanAccept(draggedCard))
        {
            ShowFeedback(false);
            return false;
        }

        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        var selection = cards.GetSelectedCardsSnapshot();
        var target = ResolveEnemyTarget();
        bool committed = combat.currentState switch
        {
            GameState.PlayerTurn => combat.TryPlayCards(selection, target),
            GameState.EnemyAttacking => combat.TryDefendWithCards(selection, target),
            _ => false
        };
        ResetFeedback();
        return committed;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var draggedCard = CardManager.Instance?.ActiveDragCard;
        if (draggedCard != null)
            ShowFeedback(CanAccept(draggedCard));
    }

    public void OnPointerExit(PointerEventData eventData) => ResetFeedback();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameplayInputGate.IsBlocked || eventData.button != PointerEventData.InputButton.Left) return;
        CombatManager.Instance?.SelectEnemyTarget(ResolveEnemyTarget());
    }

    void ResolveReferences()
    {
        if (feedbackGraphic == null)
            feedbackGraphic = GetComponent<Graphic>();
        if (enemyDisplay == null)
            enemyDisplay = GetComponent<EnemyDisplayUI>();
        if (guidanceLabel == null)
            guidanceLabel = GetComponentInChildren<TMP_Text>(true);
    }

    EnemyRuntime ResolveEnemyTarget()
    {
        return enemyDisplay != null ? enemyDisplay.DisplayedEnemy : null;
    }

    void CaptureBaseColor()
    {
        if (feedbackGraphic == null || _hasBaseColor) return;
        _baseColor = feedbackGraphic.color;
        _hasBaseColor = true;
    }

    void ShowFeedback(bool valid)
    {
        if (feedbackGraphic != null)
            feedbackGraphic.color = valid ? validHoverColor : invalidHoverColor;
        if (guidanceLabel != null)
        {
            var combat = CombatManager.Instance;
            guidanceLabel.text = valid
                ? combat != null && combat.currentState == GameState.EnemyAttacking
                    ? "RELEASE TO DEFEND"
                    : "RELEASE TO ATTACK"
                : "INVALID CARD ACTION";
        }
    }

    public void ResetFeedback()
    {
        if (feedbackGraphic != null && _hasBaseColor)
            feedbackGraphic.color = _baseColor;
        enemyDisplay?.RefreshStateGuidance();
    }
}
