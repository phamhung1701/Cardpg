using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameState { Idle, PlayerTurn, EnemyAttacking, GameOver, GameWon }

public class CombatManager : Singleton<CombatManager>
{
    [Header("Runtime State")]
    public GameState currentState = GameState.Idle;
    public EnemyRuntime currentEnemy;
    public int pendingDamage;

    public event Action<GameState> OnStateChanged;
    public event Action OnEnemyChanged;
    public event Action OnEnemyHpChanged;
    public event Action<int> OnPendingDamageChanged;
    public event Action<string> OnCombatLog;

    public void StartEnemy(EnemyRuntime enemy)
    {
        if (enemy == null) throw new ArgumentNullException(nameof(enemy));
        if (currentEnemy != null)
            currentEnemy.OnHpChanged -= HandleEnemyHpChanged;
        currentEnemy = enemy;
        pendingDamage = 0;
        OnPendingDamageChanged?.Invoke(0);
        currentEnemy.OnHpChanged += HandleEnemyHpChanged;
        CardManager.Instance.RefillHand();
        OnEnemyChanged?.Invoke();
        SetState(GameState.PlayerTurn);
        Log($"Enemy appears: {enemy.DisplayName} (HP: {enemy.maxHp}, ATK: {enemy.currentAttack})");
    }

    public void PlayCard(CardView card)
    {
        if (currentState != GameState.PlayerTurn || card?.data == null) return;

        var data = card.data;
        int damage = data.AttackValue;
        var suit = data.suit;

        var cm = CardManager.Instance;
        if (!cm.TryDiscard(card)) return;

        if (cm.HasRelic("club_power") && suit == CardData.Suit.Clubs)
        {
            damage *= 2;
            Log("Clubs: Double damage!");
        }

        if (cm.HasRelic("spade_power") && suit == CardData.Suit.Spades)
        {
            int reduce = data.AttackValue;
            currentEnemy.ReduceAttack(reduce);
            Log($"Spades: Enemy ATK reduced by {reduce} to {currentEnemy.currentAttack}!");
        }

        if (cm.HasRelic("heart_power") && suit == CardData.Suit.Hearts)
            ActivateHearts(data.AttackValue);

        if (cm.HasRelic("diamond_power") && suit == CardData.Suit.Diamonds)
            ActivateDiamonds(data.AttackValue);

        currentEnemy.TakeDamage(damage);
        Log($"Played {data.DisplayName} for {damage} damage! (HP: {currentEnemy.currentHp}/{currentEnemy.maxHp})");

        if (currentEnemy.IsDefeated)
        {
            currentEnemy.OnHpChanged -= HandleEnemyHpChanged;
            cm.AddGold(currentEnemy.GoldReward);
            SetState(GameState.GameWon);
            Log($"Enemy defeated! +{currentEnemy.GoldReward}g");
            return;
        }

        if (HandCount() == 0)
        {
            EndGameOver();
            return;
        }

        pendingDamage = Mathf.Max(0, currentEnemy.currentAttack);
        if (pendingDamage <= 0)
        {
            Log("Enemy attack is 0! Your turn.");
            return;
        }

        SetState(GameState.EnemyAttacking);
        OnPendingDamageChanged?.Invoke(pendingDamage);
        Log($"Enemy attacks for {pendingDamage}! Discard to defend!");
    }

    public void DiscardCard(CardView card)
    {
        if (currentState != GameState.EnemyAttacking || card?.data == null) return;

        var data = card.data;
        int value = data.AttackValue;
        if (!CardManager.Instance.TryDiscard(card)) return;

        pendingDamage = Mathf.Max(0, pendingDamage - value);
        OnPendingDamageChanged?.Invoke(pendingDamage);
        Log($"Discarded {data.DisplayName} (-{value}). Remaining: {pendingDamage}");

        if (HandCount() == 0)
        {
            EndGameOver();
            return;
        }

        if (pendingDamage <= 0)
        {
            SetState(GameState.PlayerTurn);
            Log("Defended! Your turn.");
        }
    }

    void ActivateHearts(int value)
    {
        int count = CardManager.Instance.ReturnRandomDiscardToDeck(value);
        if (count <= 0) return;
        Log($"Hearts: {count} cards returned to deck!");
    }

    void ActivateDiamonds(int value)
    {
        int drawn = CardManager.Instance.DrawToHand(value);
        if (drawn > 0) Log($"Diamonds: Drew {drawn} cards!");
    }

    int HandCount() => CardManager.Instance.HandCount;

    void EndGameOver()
    {
        if (currentEnemy != null)
            currentEnemy.OnHpChanged -= HandleEnemyHpChanged;
        SetState(GameState.GameOver);
        Log("No cards left! Game Over!");
    }

    void HandleEnemyHpChanged()
    {
        OnEnemyHpChanged?.Invoke();
    }

    void Log(string msg)
    {
        OnCombatLog?.Invoke(msg);
        Debug.Log($"[Combat] {msg}");
    }

    void SetState(GameState s)
    {
        currentState = s;
        OnStateChanged?.Invoke(s);
    }

    public void Reset()
    {
        if (currentEnemy != null)
            currentEnemy.OnHpChanged -= HandleEnemyHpChanged;
        currentEnemy = null;
        pendingDamage = 0;
        SetState(GameState.Idle);
        OnPendingDamageChanged?.Invoke(0);
        OnEnemyChanged?.Invoke();
        OnCombatLog?.Invoke(string.Empty);
    }
}
