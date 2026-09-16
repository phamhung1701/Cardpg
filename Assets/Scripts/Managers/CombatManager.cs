using System;
using UnityEngine;

public enum GameState { Idle, PlayerTurn, EnemyAttacking, GameOver, GameWon }

public class CombatManager : Singleton<CombatManager>
{
    [Header("Runtime State")]
    public GameState currentState = GameState.Idle;
    public EnemyRuntime currentEnemy;
    public int pendingDamage;

    public PlayerRuntime player { get; } = new();

    public event Action<GameState> OnStateChanged;
    public event Action OnEnemyChanged;
    public event Action OnEnemyHpChanged;
    public event Action<int> OnPendingDamageChanged;
    public event Action<int, int> OnPlayerHealthChanged;
    public event Action<EncounterResult> OnEncounterResult;
    public event Action<string> OnCombatLog;

    bool _encounterResolved;

    public void ConfigurePlayer(int maxHealth)
    {
        player.Configure(maxHealth);
        NotifyPlayerHealthChanged();
    }

    public void StartEnemy(EnemyRuntime enemy)
    {
        if (enemy == null) throw new ArgumentNullException(nameof(enemy));
        if (currentState != GameState.Idle && currentState != GameState.GameWon) return;

        UnsubscribeFromEnemy();
        currentEnemy = enemy;
        _encounterResolved = false;
        SetPendingDamage(0, true);
        currentEnemy.OnHpChanged += HandleEnemyHpChanged;

        int drawn = CardManager.Instance.DrawToHand(1);
        OnEnemyChanged?.Invoke();
        SetState(GameState.PlayerTurn);
        Log($"Enemy appears: {enemy.DisplayName} (HP: {enemy.maxHp}, ATK: {enemy.currentAttack})");
        if (drawn > 0)
            Log($"Drew {drawn} card for the encounter.");
    }

    public void PlayCard(CardView card)
    {
        if (currentState != GameState.PlayerTurn || currentEnemy == null ||
            _encounterResolved || card?.data == null)
            return;

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
            ResolveEncounter(EncounterResult.Victory);
            return;
        }

        BeginEnemyAttack();
    }

    public void DiscardCard(CardView card)
    {
        if (currentState != GameState.EnemyAttacking || currentEnemy == null ||
            _encounterResolved || card?.data == null)
            return;

        var data = card.data;
        int value = data.AttackValue;
        if (!CardManager.Instance.TryDiscard(card)) return;

        SetPendingDamage(pendingDamage - value);
        Log($"Discarded {data.DisplayName} (-{value}). Remaining: {pendingDamage}");

        if (pendingDamage <= 0)
        {
            SetState(GameState.PlayerTurn);
            Log("Defended! Your turn.");
        }
    }

    public void TakeRemainingDamage()
    {
        if (currentState != GameState.EnemyAttacking || currentEnemy == null ||
            _encounterResolved)
            return;

        int damage = pendingDamage;
        SetPendingDamage(0);
        ApplyPlayerDamage(damage, "Took");

        if (!_encounterResolved)
        {
            SetState(GameState.PlayerTurn);
            Log("Your turn.");
        }
    }

    public void Recover()
    {
        if (currentState != GameState.PlayerTurn || currentEnemy == null ||
            _encounterResolved || HandCount() != 0)
            return;

        int damage = Mathf.Max(0, currentEnemy.currentAttack);
        ApplyPlayerDamage(damage, "Recovery attack dealt");
        if (_encounterResolved) return;

        int drawn = CardManager.Instance.DrawToHand(1);
        Log(drawn > 0 ? "Recovered and drew 1 card." : "Recovered, but no card could be drawn.");
    }

    void BeginEnemyAttack()
    {
        SetPendingDamage(currentEnemy.currentAttack);
        if (pendingDamage <= 0)
        {
            Log("Enemy attack is 0! Your turn.");
            return;
        }

        SetState(GameState.EnemyAttacking);
        Log($"Enemy attacks for {pendingDamage}! Discard to defend or take the remaining damage.");
    }

    void ApplyPlayerDamage(int amount, string logPrefix)
    {
        int dealt = player.TakeDamage(Mathf.Max(0, amount));
        NotifyPlayerHealthChanged();
        Log($"{logPrefix} {dealt} damage. (HP: {player.currentHealth}/{player.maxHealth})");

        if (player.IsDefeated)
            ResolveEncounter(EncounterResult.Defeat);
    }

    void ResolveEncounter(EncounterResult result)
    {
        if (_encounterResolved) return;

        _encounterResolved = true;
        UnsubscribeFromEnemy();
        SetPendingDamage(0);

        if (result == EncounterResult.Victory)
        {
            int reward = currentEnemy != null ? currentEnemy.GoldReward : 0;
            CardManager.Instance.AddGold(reward);
            SetState(GameState.GameWon);
            Log($"Enemy defeated! +{reward}g");
        }
        else
        {
            SetState(GameState.GameOver);
            Log("Player defeated! Game Over!");
        }

        OnEncounterResult?.Invoke(result);
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

    void HandleEnemyHpChanged()
    {
        OnEnemyHpChanged?.Invoke();
    }

    void NotifyPlayerHealthChanged()
    {
        OnPlayerHealthChanged?.Invoke(player.currentHealth, player.maxHealth);
    }

    void SetPendingDamage(int value, bool forceNotify = false)
    {
        int nextValue = Mathf.Max(0, value);
        if (!forceNotify && pendingDamage == nextValue) return;

        pendingDamage = nextValue;
        OnPendingDamageChanged?.Invoke(pendingDamage);
    }

    void UnsubscribeFromEnemy()
    {
        if (currentEnemy != null)
            currentEnemy.OnHpChanged -= HandleEnemyHpChanged;
    }

    void Log(string msg)
    {
        OnCombatLog?.Invoke(msg);
        Debug.Log($"[Combat] {msg}");
    }

    void SetState(GameState state)
    {
        currentState = state;
        OnStateChanged?.Invoke(state);
    }

    public void Reset()
    {
        UnsubscribeFromEnemy();
        currentEnemy = null;
        _encounterResolved = false;
        player.Reset();
        SetPendingDamage(0, true);
        SetState(GameState.Idle);
        NotifyPlayerHealthChanged();
        OnEnemyChanged?.Invoke();
        OnCombatLog?.Invoke(string.Empty);
    }
}
