using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyDisplayUI : MonoBehaviour
{
    [Header("References")]
    public TMP_Text nameText;
    public TMP_Text hpText;
    public TMP_Text atkText;
    public TMP_Text statusText;
    public Image portrait;

    void OnEnable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnEnemyChanged += RefreshDisplay;
            CombatManager.Instance.OnEnemyHpChanged += RefreshHp;
            CombatManager.Instance.OnStateChanged += HandleStateChanged;
        }
    }

    void OnDisable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnEnemyChanged -= RefreshDisplay;
            CombatManager.Instance.OnEnemyHpChanged -= RefreshHp;
            CombatManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    void RefreshDisplay()
    {
        var enemy = CombatManager.Instance.currentEnemy;
        if (enemy == null) return;
        if (nameText) nameText.text = enemy.DisplayName;
        RefreshHp();
    }

    void RefreshHp()
    {
        var enemy = CombatManager.Instance.currentEnemy;
        if (enemy == null) return;
        if (hpText) hpText.text = $"HP: {enemy.currentHp}/{enemy.maxHp}";
        if (atkText) atkText.text = $"ATK: {enemy.currentAttack}";
    }

    void HandleStateChanged(GameState state)
    {
        if (statusText)
        {
            statusText.text = state switch
            {
                GameState.PlayerTurn      => "Your Turn",
                GameState.EnemyAttacking   => "Enemy Attacking!",
                GameState.GameWon          => "VICTORY!",
                GameState.GameOver         => "GAME OVER",
                _                          => ""
            };
        }
        if (portrait)
        {
            portrait.color = state == GameState.GameWon ? Color.green
                           : state == GameState.GameOver ? Color.red
                           : Color.white;
        }
    }
}
