using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyDisplayUI : MonoBehaviour
{
    [Header("References")]
    public TMP_Text nameText;
    public TMP_Text hpText;
    public TMP_Text atkText;
    public TMP_Text statusText;
    public Image portrait;
    public Slider healthBar;

    EnemyRuntime _boundEnemy;
    bool _hasExplicitBinding;
    bool _isDisplayingBossCard;
    Sprite _placeholderSprite;
    Color _placeholderColor;
    Image.Type _placeholderImageType;
    bool _placeholderPreserveAspect;
    TMP_Text _portraitGlyph;
    Vector2 _portraitAnchorMin;
    Vector2 _portraitAnchorMax;
    Vector2 _portraitAnchoredPosition;
    Vector2 _portraitSizeDelta;

    public EnemyRuntime DisplayedEnemy => _hasExplicitBinding
        ? _boundEnemy
        : CombatManager.Instance != null ? CombatManager.Instance.currentEnemy : null;

    void Awake()
    {
        if (!portrait) return;

        _placeholderSprite = portrait.sprite;
        _placeholderColor = portrait.color;
        _placeholderImageType = portrait.type;
        _placeholderPreserveAspect = portrait.preserveAspect;
        _portraitGlyph = portrait.GetComponentInChildren<TMP_Text>(true);

        var rect = portrait.rectTransform;
        _portraitAnchorMin = rect.anchorMin;
        _portraitAnchorMax = rect.anchorMax;
        _portraitAnchoredPosition = rect.anchoredPosition;
        _portraitSizeDelta = rect.sizeDelta;
    }

    public void Bind(EnemyRuntime enemy)
    {
        _hasExplicitBinding = true;
        _boundEnemy = enemy;
        RefreshDisplay();
        RefreshStateGuidance();
    }

    void OnEnable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnEnemyChanged += RefreshDisplay;
            CombatManager.Instance.OnEnemiesChanged += RefreshDisplay;
            CombatManager.Instance.OnEnemyHpChanged += RefreshHp;
            CombatManager.Instance.OnStateChanged += HandleStateChanged;
        }
        RefreshDisplay();
        HandleStateChanged(CombatManager.Instance != null ? CombatManager.Instance.currentState : GameState.Idle);
    }

    void OnDisable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnEnemyChanged -= RefreshDisplay;
            CombatManager.Instance.OnEnemiesChanged -= RefreshDisplay;
            CombatManager.Instance.OnEnemyHpChanged -= RefreshHp;
            CombatManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    void RefreshDisplay()
    {
        var enemy = DisplayedEnemy;
        bool hasEnemy = enemy != null;

        if (nameText) nameText.text = hasEnemy ? enemy.DisplayName.ToUpperInvariant() : "AWAITING ENCOUNTER";
        if (hpText) hpText.text = hasEnemy ? $"HP  {enemy.currentHp}/{enemy.maxHp}" : "HP  —";
        if (atkText) atkText.text = hasEnemy ? $"ATTACK  {enemy.currentAttack}" : "ATTACK  —";
        if (healthBar)
        {
            healthBar.minValue = 0;
            healthBar.maxValue = hasEnemy ? Mathf.Max(1, enemy.maxHp) : 1;
            healthBar.value = hasEnemy ? enemy.currentHp : 0;
        }

        RefreshPortrait(enemy);
    }

    void RefreshPortrait(EnemyRuntime enemy)
    {
        if (!portrait) return;

        CardData bossCard = enemy?.type?.sourceCard;
        Sprite bossSprite = bossCard != null && bossCard.IsFaceCard
            ? CardManager.Instance?.cardPrefab?.ResolveCardSprite(bossCard)
            : null;
        _isDisplayingBossCard = bossSprite != null;

        var rect = portrait.rectTransform;
        if (_isDisplayingBossCard)
        {
            portrait.sprite = bossSprite;
            portrait.type = Image.Type.Simple;
            portrait.preserveAspect = true;
            portrait.color = Color.white;
            rect.anchorMin = new Vector2(0.03f, 0.08f);
            rect.anchorMax = new Vector2(0.30f, 0.92f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
        else
        {
            portrait.sprite = _placeholderSprite;
            portrait.type = _placeholderImageType;
            portrait.preserveAspect = _placeholderPreserveAspect;
            portrait.color = _placeholderColor;
            rect.anchorMin = _portraitAnchorMin;
            rect.anchorMax = _portraitAnchorMax;
            rect.anchoredPosition = _portraitAnchoredPosition;
            rect.sizeDelta = _portraitSizeDelta;
        }

        if (_portraitGlyph)
            _portraitGlyph.gameObject.SetActive(!_isDisplayingBossCard);
    }

    void RefreshHp() => RefreshDisplay();

    public void RefreshStateGuidance()
    {
        HandleStateChanged(CombatManager.Instance != null ? CombatManager.Instance.currentState : GameState.Idle);
    }

    void HandleStateChanged(GameState state)
    {
        if (statusText)
        {
            string guidance = state switch
            {
                GameState.PlayerTurn => "Choose a card to attack",
                GameState.EnemyAttacking => "Enemy attack incoming",
                GameState.GameWon => "Encounter cleared",
                GameState.GameOver => "Player defeated",
                _ => "Choose your next encounter"
            };
            var enemy = DisplayedEnemy;
            if (enemy != null && state == GameState.PlayerTurn && enemy.NextAttackIsCharged)
                guidance += $"\nWarning: charged attack next ({enemy.currentAttack * 2} damage)";
            else if (enemy != null && state == GameState.EnemyAttacking && enemy.CurrentResponseIsCharged)
                guidance = $"Charged attack incoming ({enemy.ResponseAttack} damage)";
            string abilities = enemy?.AbilitySummary;
            statusText.text = string.IsNullOrEmpty(abilities)
                ? guidance
                : $"{guidance}\nTrait: {abilities}";
        }

        if (portrait)
        {
            if (_isDisplayingBossCard)
            {
                portrait.color = Color.white;
                return;
            }

            bool isSelectedTarget = DisplayedEnemy != null &&
                ReferenceEquals(CombatManager.Instance?.currentEnemy, DisplayedEnemy);
            portrait.color = state == GameState.GameWon ? new Color(0.35f, 0.75f, 0.48f)
                : state == GameState.GameOver ? new Color(0.75f, 0.28f, 0.3f)
                : isSelectedTarget ? new Color(0.42f, 0.52f, 0.72f)
                : new Color(0.32f, 0.38f, 0.5f);
        }
    }
}
