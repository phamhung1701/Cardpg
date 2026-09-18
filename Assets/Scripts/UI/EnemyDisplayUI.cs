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

    public EnemyRuntime DisplayedEnemy => _hasExplicitBinding
        ? _boundEnemy
        : CombatManager.Instance != null ? CombatManager.Instance.currentEnemy : null;

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
            string abilities = DisplayedEnemy?.AbilitySummary;
            statusText.text = string.IsNullOrEmpty(abilities)
                ? guidance
                : $"{guidance}\nTrait: {abilities}";
        }

        if (portrait)
        {
            bool isSelectedTarget = DisplayedEnemy != null &&
                ReferenceEquals(CombatManager.Instance?.currentEnemy, DisplayedEnemy);
            portrait.color = state == GameState.GameWon ? new Color(0.35f, 0.75f, 0.48f)
                : state == GameState.GameOver ? new Color(0.75f, 0.28f, 0.3f)
                : isSelectedTarget ? new Color(0.42f, 0.52f, 0.72f)
                : new Color(0.32f, 0.38f, 0.5f);
        }
    }
}
