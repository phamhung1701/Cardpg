using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EnemyDisplayUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
    bool _isDisplayingAuthoredPortrait;
    Sprite _placeholderSprite;
    Color _placeholderColor;
    Image.Type _placeholderImageType;
    bool _placeholderPreserveAspect;
    TMP_Text _portraitGlyph;
    Vector2 _portraitAnchorMin;
    Vector2 _portraitAnchorMax;
    Vector2 _portraitAnchoredPosition;
    Vector2 _portraitSizeDelta;
    Coroutine _defeatCue;
    Vector3 _preDefeatScale;
    Color _preDefeatPortraitColor;
    CanvasGroup _defeatCanvasGroup;
    bool _enemyTargeting;
    bool _enemyTargetEligible;
    bool _enemyPointerHovered;
    CanvasGroup _targetCanvasGroup;
    Outline _targetOutline;
    Vector3 _baseTargetScale;

    public EnemyRuntime DisplayedEnemy => _hasExplicitBinding
        ? _boundEnemy
        : CombatManager.Instance != null ? CombatManager.Instance.currentEnemy : null;

    void Awake()
    {
        _baseTargetScale = transform.localScale;
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

    public void SetEnemyTargeting(bool targeting, bool eligible)
    {
        _enemyTargeting = targeting;
        _enemyTargetEligible = targeting && eligible;
        if (_enemyTargeting && _targetCanvasGroup == null)
            _targetCanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        if (_enemyTargeting && portrait != null && _targetOutline == null)
        {
            _targetOutline = portrait.GetComponent<Outline>() ?? portrait.gameObject.AddComponent<Outline>();
            _targetOutline.effectDistance = new Vector2(3f, -3f);
        }
        RefreshEnemyTargetPresentation();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _enemyPointerHovered = true;
        RefreshEnemyTargetPresentation();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _enemyPointerHovered = false;
        RefreshEnemyTargetPresentation();
    }

    void RefreshEnemyTargetPresentation()
    {
        if (!_enemyTargeting)
        {
            if (_targetCanvasGroup != null) _targetCanvasGroup.alpha = 1f;
            if (_targetOutline != null) _targetOutline.enabled = false;
            if (_baseTargetScale != Vector3.zero) transform.localScale = _baseTargetScale;
            return;
        }
        if (_targetCanvasGroup != null) _targetCanvasGroup.alpha = _enemyTargetEligible ? 1f : 0.38f;
        if (_targetOutline != null)
        {
            _targetOutline.enabled = _enemyTargetEligible;
            _targetOutline.effectColor = _enemyPointerHovered
                ? new Color(1f, 0.86f, 0.32f, 1f)
                : new Color(0.36f, 1f, 0.52f, 1f);
        }
        float scale = _enemyTargetEligible ? (_enemyPointerHovered ? 1.07f : 1.035f) : 0.96f;
        transform.localScale = _baseTargetScale * scale;
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
            CombatManager.Instance.OnPendingDamageChanged += HandlePendingDamageChanged;
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
            CombatManager.Instance.OnPendingDamageChanged -= HandlePendingDamageChanged;
        }
    }

    public bool IsDefeatCuePlaying => _defeatCue != null;

    public void PlayDefeatCue()
    {
        if (!isActiveAndEnabled) return;
        CancelDefeatCue();
        _preDefeatScale = transform.localScale;
        _preDefeatPortraitColor = portrait != null ? portrait.color : Color.white;
        _defeatCanvasGroup = GetComponent<CanvasGroup>();
        if (_defeatCanvasGroup == null) _defeatCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        _defeatCue = StartCoroutine(DefeatCueRoutine());
    }

    public void CancelDefeatCue()
    {
        if (_defeatCue != null) StopCoroutine(_defeatCue);
        _defeatCue = null;
        if (_preDefeatScale != Vector3.zero) transform.localScale = _preDefeatScale;
        if (portrait != null && _preDefeatScale != Vector3.zero) portrait.color = _preDefeatPortraitColor;
        if (_defeatCanvasGroup != null) _defeatCanvasGroup.alpha = 1f;
    }

    System.Collections.IEnumerator DefeatCueRoutine()
    {
        var rect = (RectTransform)transform;
        Vector3 initialScale = _preDefeatScale;
        float elapsed = 0f;
        if (portrait != null) portrait.color = new Color(1f, 0.35f, 0.2f, 1f);
        // Brief warm impact flash, then a compact fade/shrink; presentation only.
        if (portrait != null) portrait.color = new Color(1f, 0.35f, 0.2f, 1f);
        yield return new WaitForSeconds(0.09f);
        while (elapsed < 0.39f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.39f);
            float eased = t * t * (3f - 2f * t);
            rect.localScale = Vector3.LerpUnclamped(initialScale, initialScale * 0.72f, eased);
            if (_defeatCanvasGroup != null) _defeatCanvasGroup.alpha = 1f - t;
            yield return null;
        }
        _defeatCue = null;
    }

    void RefreshDisplay()
    {
        var enemy = DisplayedEnemy;
        bool hasEnemy = enemy != null;

        if (nameText) nameText.text = hasEnemy ? enemy.DisplayName.ToUpperInvariant() : "AWAITING ENCOUNTER";
        if (hpText) hpText.text = hasEnemy ? $"HP  {enemy.currentHp}/{enemy.maxHp}" : "HP  —";
        if (atkText) atkText.text = hasEnemy ? $"ATTACK  {enemy.EffectiveAttack}" : "ATTACK  —";
        if (healthBar)
        {
            healthBar.minValue = 0;
            healthBar.maxValue = hasEnemy ? Mathf.Max(1, enemy.maxHp) : 1;
            healthBar.value = hasEnemy ? enemy.currentHp : 0;
        }

        RefreshPortrait(enemy);
        RefreshStateGuidance();
    }

    void RefreshPortrait(EnemyRuntime enemy)
    {
        if (!portrait) return;

        CardData bossCard = enemy?.type?.sourceCard;
        Sprite bossSprite = bossCard != null && bossCard.IsFaceCard
            ? CardManager.Instance?.cardPrefab?.ResolveCardSprite(bossCard)
            : null;
        Sprite enemySprite = enemy?.type ? enemy.type.portraitSprite : null;
        _isDisplayingBossCard = bossSprite != null;
        _isDisplayingAuthoredPortrait = !_isDisplayingBossCard && enemySprite != null;

        var rect = portrait.rectTransform;
        if (_isDisplayingBossCard || _isDisplayingAuthoredPortrait)
        {
            portrait.sprite = _isDisplayingBossCard ? bossSprite : enemySprite;
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
            _portraitGlyph.gameObject.SetActive(!_isDisplayingBossCard && !_isDisplayingAuthoredPortrait);
    }

    void RefreshHp() => RefreshDisplay();
    void HandlePendingDamageChanged(int _) => RefreshStateGuidance();

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
            if (enemy != null && state == GameState.EnemyAttacking)
            {
                int incoming = CombatManager.Instance != null
                    ? CombatManager.Instance.GetPendingAttackDamage(enemy) : 0;
                guidance += incoming > 0 ? $"\nINCOMING: {incoming} DAMAGE" : "\nBLOCKED";
            }
            if (enemy != null && state == GameState.PlayerTurn && enemy.HasDoubleStrike)
            {
                int budget = enemy.ResponseAttack + (enemy.HasOppression
                    ? Mathf.Max(0, (CardManager.Instance?.HandCount ?? 0) - 3) * 2 : 0);
                guidance += $"\nDouble Strike next: 2 hits, {Mathf.FloorToInt(budget * 0.6f + 0.5f)} damage each";
            }
            else if (enemy != null && state == GameState.EnemyAttacking && enemy.HasDoubleStrike)
                guidance = $"Double Strike incoming: 2 hits, {Mathf.FloorToInt(enemy.PreparedResponseBudget * 0.6f + 0.5f)} damage each";
            if (enemy != null && enemy.HasSuitCall)
                guidance += $"\nCalled suit: {enemy.AnnouncedSuit}{(enemy.DuelistCallConsumed ? " (used)" : " (first matching hit -50%)")}";
            if (enemy != null && enemy.HasSilence)
                guidance += "\nSilence active: held-card effects disabled";
            if (enemy != null && enemy.HasAbilityEffect(EnemyAbilityEffect.Silence))
                guidance += "\nSilence inactive at/below 50% HP";
            if (enemy != null && enemy.HasWithering)
                guidance += "\nWithering active: player healing prevented";
            else if (enemy != null && enemy.HasAbilityEffect(EnemyAbilityEffect.Withering))
                guidance += "\nWithering inactive at/below 50% HP";
            if (enemy != null && enemy.HasOppression && state == GameState.PlayerTurn)
                guidance += $"\nOppression: +{Mathf.Max(0, (CardManager.Instance?.HandCount ?? 0) - 3) * 2} next response damage";
            if (enemy != null && state == GameState.PlayerTurn && enemy.NextAttackIsCharged)
                guidance += $"\nWarning: charged attack next ({enemy.EffectiveAttack * 2} damage)";
            else if (enemy != null && state == GameState.EnemyAttacking && enemy.CurrentResponseIsCharged)
                guidance = $"Charged attack incoming ({enemy.ResponseAttack} damage)";
            if (enemy != null && state == GameState.PlayerTurn && enemy.HasRoyalGuard && !enemy.NextAttackIsCharged)
                guidance += $"\nNext: +1 Shield and {enemy.EffectiveAttack} damage";
            if (enemy != null && enemy.ShieldCharges > 0)
                guidance += $"\nShield: {enemy.ShieldCharges}";
            if (enemy != null && enemy.CaptainAttackBonus > 0)
                guidance += "\nCaptain alive: +1 ATK";
            string abilities = enemy?.AbilitySummary;
            statusText.text = string.IsNullOrEmpty(abilities)
                ? guidance
                : $"{guidance}\nTrait: {abilities}";
        }

        if (portrait)
        {
            if (_isDisplayingBossCard || _isDisplayingAuthoredPortrait)
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
