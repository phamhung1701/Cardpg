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

    readonly List<EnemyRuntime> _enemies = new();
    int _earnedGoldReward;
    public IReadOnlyList<EnemyRuntime> Enemies => _enemies;
    public int TotalEnemyAttack
    {
        get
        {
            int total = 0;
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i] != null && !_enemies[i].IsDefeated)
                    total += Mathf.Max(0, _enemies[i].currentAttack);
            return total;
        }
    }

    public PlayerRuntime player { get; } = new();

    int _configuredPlayerMaxHealth = PlayerRuntime.DefaultMaxHealth;

    public event Action<GameState> OnStateChanged;
    public event Action OnEnemyChanged;
    public event Action OnEnemiesChanged;
    public event Action OnEnemyHpChanged;
    public event Action<int> OnPendingDamageChanged;
    public event Action<int, int> OnPlayerHealthChanged;
    public event Action<EncounterResult> OnEncounterResult;
    public event Action<string> OnCombatLog;

    bool _encounterResolved;

    public void ConfigurePlayer(int maxHealth)
    {
        _configuredPlayerMaxHealth = maxHealth;
        player.Configure(maxHealth);
        player.Reset();
        NotifyPlayerHealthChanged();
    }

    public void StartEnemy(EnemyRuntime enemy)
    {
        if (enemy == null) throw new ArgumentNullException(nameof(enemy));
        StartEncounter(new[] { enemy });
    }

    public void StartEncounter(IReadOnlyList<EnemyRuntime> enemies)
    {
        if (enemies == null || enemies.Count == 0)
            throw new ArgumentException("An encounter requires at least one enemy.", nameof(enemies));
        if (currentState != GameState.Idle && currentState != GameState.GameWon) return;

        UnsubscribeFromEnemies();
        CardManager.Instance?.CancelCardInteractions();
        _enemies.Clear();
        var uniqueEnemies = new HashSet<EnemyRuntime>();
        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy == null || !uniqueEnemies.Add(enemy))
                throw new ArgumentException("Encounter enemies must be non-null unique instances.", nameof(enemies));
            _enemies.Add(enemy);
        }

        currentEnemy = _enemies[0];
        _earnedGoldReward = 0;
        _encounterResolved = false;
        SetPendingDamage(0, true);
        for (int i = 0; i < _enemies.Count; i++)
        {
            _enemies[i].OnHpChanged += HandleEnemyHpChanged;
            _enemies[i].OnAttackChanged += HandleEnemyHpChanged;
            _enemies[i].NotifyEncounterStarted(this);
        }

        int drawn = CardManager.Instance.DrawToHand(1);
        OnEnemiesChanged?.Invoke();
        OnEnemyChanged?.Invoke();
        SetState(GameState.PlayerTurn);
        Log(_enemies.Count == 1
            ? $"Enemy appears: {_enemies[0].DisplayName} (HP: {_enemies[0].maxHp}, ATK: {_enemies[0].currentAttack})"
            : $"Enemy group appears: {string.Join(", ", _enemies.ConvertAll(enemy => $"{enemy.DisplayName} {enemy.currentHp} HP/{enemy.currentAttack} ATK"))}");
        if (drawn > 0)
            Log($"Drew {drawn} card for the encounter.");
    }

    public bool SelectEnemyTarget(EnemyRuntime enemy)
    {
        if (enemy == null || enemy.IsDefeated || !_enemies.Contains(enemy)) return false;
        if (ReferenceEquals(currentEnemy, enemy)) return true;
        currentEnemy = enemy;
        OnEnemyChanged?.Invoke();
        return true;
    }

    public bool ShouldReplaceSelectionOnAdd => currentState == GameState.PlayerTurn;

    public bool CanAddCardToSelection(IReadOnlyList<CardView> selectedCards, CardView candidate)
    {
        if (GameplayInputGate.IsBlocked || candidate?.data == null ||
            !AreValidHandViews(new[] { candidate }))
            return false;

        return currentState switch
        {
            GameState.PlayerTurn => true,
            GameState.EnemyAttacking => pendingDamage > 0 &&
                CalculateSelectedDefense(selectedCards) < pendingDamage,
            _ => false
        };
    }

    public int CalculateSelectedDefense(IReadOnlyList<CardView> cards)
    {
        if (cards == null) return 0;
        int total = 0;
        for (int i = 0; i < cards.Count; i++)
            if (cards[i]?.data != null)
                total += CalculateCardDefense(cards[i].data);
        return total;
    }

    public void PlayCard(CardView card)
    {
        if (card != null)
            TryPlayCards(new[] { card }, currentEnemy);
    }

    public bool CanPlayCards(IReadOnlyList<CardView> cards, EnemyRuntime target)
    {
        return !GameplayInputGate.IsBlocked && currentState == GameState.PlayerTurn &&
            target != null && !target.IsDefeated && _enemies.Contains(target) && !_encounterResolved &&
            cards != null && cards.Count == 1 && AreValidHandViews(cards);
    }

    public bool TryPlayCards(IReadOnlyList<CardView> cards, EnemyRuntime target)
    {
        if (!CanPlayCards(cards, target)) return false;

        var card = cards[0];
        var data = card.data;
        int damage = CalculateCardAttackDamage(data);

        var cm = CardManager.Instance;
        if (!cm.TryDiscardCards(cards)) return false;

        foreach (var artifact in cm.ownedArtifacts)
        {
            if (artifact == null || !artifact.Matches(data)) continue;
            if (artifact.reduceEnemyAttackByCardValue)
            {
                int reduce = data.BaseAttackValue;
                target.ReduceAttack(reduce);
                Log($"{artifact.displayName}: Enemy ATK reduced by {reduce} to {target.currentAttack}!");
            }

            if (artifact.recycleDiscardByCardValue)
            {
                int returned = cm.ReturnRandomDiscardToDeck(data.BaseAttackValue);
                if (returned > 0) Log($"{artifact.displayName}: {returned} cards returned to deck!");
            }

            if (artifact.drawByCardValue)
            {
                int drawn = cm.DrawToHand(data.BaseAttackValue);
                if (drawn > 0) Log($"{artifact.displayName}: Drew {drawn} cards!");
            }
        }

        if (data.Enhancement != null)
        {
            if (data.Enhancement.healOnPlay > 0)
            {
                int healed = HealPlayer(data.Enhancement.healOnPlay);
                if (healed > 0) Log($"{data.Enhancement.displayName}: Healed {healed} HP.");
            }

            if (data.Enhancement.drawOnPlay > 0)
            {
                int drawn = cm.DrawToHand(data.Enhancement.drawOnPlay);
                if (drawn > 0) Log($"{data.Enhancement.displayName}: Drew {drawn} card(s).");
            }
        }

        int dealtDamage = target.TakeCardDamage(damage, this);
        Log($"Played {data.DisplayName} on {target.DisplayName} for {dealtDamage} damage! (HP: {target.currentHp}/{target.maxHp})");
        target.NotifyPlayerCardResolved(this);

        if (target.IsDefeated)
            HandleEnemyDefeated(target);

        CompletePlayerTurnForEnemies();
        if (_enemies.Count == 0)
        {
            ResolveEncounter(EncounterResult.Victory);
            return true;
        }

        BeginEnemyAttack();
        return true;
    }

    public void DiscardCard(CardView card)
    {
        if (card != null)
            TryDefendWithCards(new[] { card });
    }

    public bool CanDefendWithCards(IReadOnlyList<CardView> cards)
    {
        return CanDefendWithCards(cards, currentEnemy);
    }

    public bool CanDefendWithCards(IReadOnlyList<CardView> cards, EnemyRuntime target)
    {
        return !GameplayInputGate.IsBlocked && currentState == GameState.EnemyAttacking &&
            target != null && !target.IsDefeated && _enemies.Contains(target) && !_encounterResolved &&
            cards != null && cards.Count > 0 && AreValidHandViews(cards);
    }

    public bool TryDefendWithCards(IReadOnlyList<CardView> cards)
    {
        return TryDefendWithCards(cards, currentEnemy);
    }

    public bool TryDefendWithCards(IReadOnlyList<CardView> cards, EnemyRuntime target)
    {
        if (!CanDefendWithCards(cards, target)) return false;

        int totalDefense = CalculateSelectedDefense(cards);
        var names = new List<string>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
            names.Add(cards[i].data.DisplayName);

        if (!CardManager.Instance.TryDiscardCards(cards)) return false;

        SetPendingDamage(pendingDamage - totalDefense);
        Log($"Defended with {string.Join(", ", names)} (-{totalDefense}). Remaining: {pendingDamage}");

        if (pendingDamage <= 0)
        {
            SetState(GameState.PlayerTurn);
            Log("Defended! Your turn.");
        }
        return true;
    }

    public void TakeRemainingDamage()
    {
        if (GameplayInputGate.IsBlocked || currentState != GameState.EnemyAttacking || _enemies.Count == 0 ||
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
        if (GameplayInputGate.IsBlocked || currentState != GameState.PlayerTurn || _enemies.Count == 0 ||
            _encounterResolved || HandCount() != 0)
            return;

        int damage = TotalEnemyAttack;
        ApplyPlayerDamage(damage, "Recovery attack dealt");
        if (_encounterResolved) return;

        int drawn = CardManager.Instance.DrawToHand(1);
        Log(drawn > 0 ? "Recovered and drew 1 card." : "Recovered, but no card could be drawn.");
    }

    void BeginEnemyAttack()
    {
        SetPendingDamage(TotalEnemyAttack);
        if (pendingDamage <= 0)
        {
            Log("Enemy attack is 0! Your turn.");
            return;
        }

        SetState(GameState.EnemyAttacking);
        string breakdown = string.Join(" + ", _enemies.ConvertAll(enemy => $"{enemy.DisplayName} {Mathf.Max(0, enemy.currentAttack)}"));
        Log($"Surviving enemies attack for {pendingDamage} ({breakdown})! Discard to defend or take the remaining damage.");
    }

    void CompletePlayerTurnForEnemies()
    {
        for (int i = 0; i < _enemies.Count; i++)
            _enemies[i].NotifyPlayerTurnCompleted();

        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            if (!enemy.ShouldFlee) continue;
            Log($"{enemy.DisplayName} fled after {enemy.PlayerTurnsCompleted} player turns. No gold awarded.");
            RemoveEnemy(enemy);
        }
    }

    void HandleEnemyDefeated(EnemyRuntime enemy)
    {
        if (enemy == null || !_enemies.Contains(enemy)) return;
        _earnedGoldReward += Mathf.Max(0, enemy.GoldReward);
        RemoveEnemy(enemy);
        int drawn = CardManager.Instance.DrawToHand(1);
        Log($"{enemy.DisplayName} defeated! {enemy.GoldReward}g secured.{(drawn > 0 ? " Drew 1 card." : string.Empty)}");
    }

    void RemoveEnemy(EnemyRuntime enemy)
    {
        if (enemy == null || !_enemies.Remove(enemy)) return;
        enemy.OnHpChanged -= HandleEnemyHpChanged;
        enemy.OnAttackChanged -= HandleEnemyHpChanged;
        if (ReferenceEquals(currentEnemy, enemy))
            currentEnemy = _enemies.Count > 0 ? _enemies[0] : null;
        OnEnemiesChanged?.Invoke();
        OnEnemyChanged?.Invoke();
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
        CardManager.Instance?.CancelCardInteractions();
        UnsubscribeFromEnemies();
        SetPendingDamage(0);

        if (result == EncounterResult.Victory)
        {
            var cards = CardManager.Instance;
            int reward = _earnedGoldReward;
            int victoryHeal = 0;
            foreach (var artifact in cards.ownedArtifacts)
            {
                if (artifact == null) continue;
                reward += artifact.bonusGold;
                victoryHeal += artifact.healAfterVictory;
            }

            cards.AddGold(reward);
            if (victoryHeal > 0)
            {
                int healed = HealPlayer(victoryHeal);
                if (healed > 0) Log($"Artifacts restored {healed} HP after victory.");
            }
            SetState(GameState.GameWon);
            Log($"Encounter cleared! +{reward}g");
        }
        else
        {
            SetState(GameState.GameOver);
            Log("Player defeated! Game Over!");
        }

        OnEncounterResult?.Invoke(result);
    }

    public int HealPlayer(int amount)
    {
        int healed = player.Heal(amount);
        if (healed > 0) NotifyPlayerHealthChanged();
        return healed;
    }

    public int IncreasePlayerMaxHealth(int amount)
    {
        int gained = player.IncreaseMaxHealth(amount);
        if (gained > 0) NotifyPlayerHealthChanged();
        return gained;
    }

    public bool CanTakeRunDamage(int amount)
    {
        return amount <= 0 || player.currentHealth > amount;
    }

    public int TakeRunDamage(int amount)
    {
        if (!CanTakeRunDamage(amount)) return 0;
        int dealt = player.TakeDamage(amount);
        if (dealt > 0) NotifyPlayerHealthChanged();
        return dealt;
    }

    public int CalculateCardAttackDamage(CardInstance card)
    {
        if (card == null) return 0;

        int multiplier = 1;
        int flatBonus = 0;
        var cards = CardManager.Instance;
        if (cards != null)
        {
            foreach (var artifact in cards.ownedArtifacts)
            {
                if (artifact == null || !artifact.Matches(card)) continue;
                multiplier = Mathf.Max(1, multiplier * Mathf.Max(1, artifact.damageMultiplier));
                flatBonus += artifact.flatDamageBonus;
            }
        }
        return Mathf.Max(0, card.AttackValue * multiplier + flatBonus);
    }

    public int CalculateCardDefense(CardInstance card)
    {
        if (card == null) return 0;

        int defense = card.DefenseValue;
        var cards = CardManager.Instance;
        if (cards != null)
        {
            foreach (var artifact in cards.ownedArtifacts)
                if (artifact != null && artifact.Matches(card))
                    defense += artifact.defenseBonus;
        }
        return Mathf.Max(0, defense);
    }

    bool AreValidHandViews(IReadOnlyList<CardView> cards)
    {
        var manager = CardManager.Instance;
        if (manager == null) return false;

        var uniqueCards = new HashSet<CardInstance>();
        for (int i = 0; i < cards.Count; i++)
        {
            var view = cards[i];
            if (view == null || view.data == null || !uniqueCards.Add(view.data))
                return false;

            bool found = false;
            for (int handIndex = 0; handIndex < manager.hand.Count; handIndex++)
            {
                if (!ReferenceEquals(manager.hand[handIndex], view.data)) continue;
                found = true;
                break;
            }
            if (!found) return false;
        }
        return true;
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

    void UnsubscribeFromEnemies()
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (enemy == null) continue;
            enemy.OnHpChanged -= HandleEnemyHpChanged;
            enemy.OnAttackChanged -= HandleEnemyHpChanged;
        }
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
        UnsubscribeFromEnemies();
        _enemies.Clear();
        currentEnemy = null;
        _earnedGoldReward = 0;
        _encounterResolved = false;
        player.Configure(_configuredPlayerMaxHealth);
        player.Reset();
        SetPendingDamage(0, true);
        SetState(GameState.Idle);
        NotifyPlayerHealthChanged();
        OnEnemiesChanged?.Invoke();
        OnEnemyChanged?.Invoke();
        OnCombatLog?.Invoke(string.Empty);
    }
}
