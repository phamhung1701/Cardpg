using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameState { Idle, PlayerTurn, EnemyAttacking, GameOver, GameWon }

public class CombatManager : Singleton<CombatManager>
{
    [Header("Critical Hits")]
    [SerializeField, Range(0f, 100f)] float criticalChancePercent = 25f;
    IRandomSource _criticalRandom;

    [Header("Runtime State")]
    public GameState currentState = GameState.Idle;
    public EnemyRuntime currentEnemy;
    public int pendingDamage;

    readonly List<EnemyRuntime> _enemies = new();
    readonly List<int> _pendingAttacks = new();
    readonly List<EnemyRuntime> _pendingAttackSources = new();
    readonly Queue<PlayerTurnGrant> _queuedPlayerTurnGrants = new();
    int _attackCountAtStart;
    int _completedPlayerTurnCount;
    public int PendingAttackCount => _pendingAttacks.Count;
    public int BlockedAttackCount => _attackCountAtStart - _pendingAttacks.Count;
    readonly HashSet<EnemyRuntime> _processedEnemyDeaths = new();
    readonly CombatResolver _resolver = new();
    readonly CombatReactionQueue _reactions = new();
    int _earnedGoldReward;
    long _nextActionId = 1;
    int _nextReactiveHitIndex;
    int _playerTurnNumber;
    public int QueuedExtraPlayerTurns => _queuedPlayerTurnGrants.Count;
    public int CompletedPlayerTurnCount => _completedPlayerTurnCount;
    bool _isResolvingAction;
    bool _reactionFaulted;
    CombatActionContext _activeAction;
    CombatReactionPhase? _processingReactionPhase;
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
    public event Action<DamageResult> OnDamageResolved;
    public event Action<string> OnCombatLog;

    public bool IsResolvingAction => _isResolvingAction;
    public CombatActionContext ActiveAction => _activeAction;

    bool _encounterResolved;

    readonly struct DefenseAssignment
    {
        public CardInstance BlockingCard { get; }
        public int CardOrder { get; }
        public int AttackIndex { get; }

        public DefenseAssignment(CardInstance blockingCard, int cardOrder, int attackIndex)
        {
            BlockingCard = blockingCard;
            CardOrder = cardOrder;
            AttackIndex = attackIndex;
        }
    }

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
        if (_reactionFaulted || (currentState != GameState.Idle && currentState != GameState.GameWon)) return;

        UnsubscribeFromEnemies();
        CardManager.Instance?.CancelCardInteractions();
        _enemies.Clear();
        _pendingAttacks.Clear();
        _pendingAttackSources.Clear();
        _attackCountAtStart = 0;
        _queuedPlayerTurnGrants.Clear();
        _completedPlayerTurnCount = 0;
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
        _processedEnemyDeaths.Clear();
        _reactions.Clear();
        _playerTurnNumber = 0;
        _encounterResolved = false;
        player.ResetShield();
        CardManager.Instance?.ResetArtifactEncounterEffectState();
        SetPendingDamage(0, true);
        var encounterAction = CreateAction(CombatActionOrigin.Legacy);
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            enemy.ResetShield();
            enemy.OnHpChanged += HandleEnemyHpChanged;
            enemy.OnAttackChanged += HandleEnemyHpChanged;
            EnqueueEncounterStartedAbilities(encounterAction, enemy, i);
        }
        ProcessReactions(CombatReactionPhase.EncounterStarted);
        if (_reactionFaulted) return;

        int drawn = CardManager.Instance.DrawToHand(1);
        GameplayEffectResolver.EnqueueEncounterStart(
            new EncounterStartEffectContext(encounterAction, player, this, CardManager.Instance,
                _enemies, drawn), _reactions);
        ProcessReactions(CombatReactionPhase.EncounterReady);
        if (_reactionFaulted) return;

        OnEnemiesChanged?.Invoke();
        OnEnemyChanged?.Invoke();
        BeginPlayerTurn();
        if (_reactionFaulted) return;
        Log(_enemies.Count == 1
            ? $"Enemy appears: {_enemies[0].DisplayName} (HP: {_enemies[0].maxHp}, ATK: {_enemies[0].currentAttack})"
            : $"Enemy group appears: {string.Join(", ", _enemies.ConvertAll(enemy => $"{enemy.DisplayName} {enemy.currentHp} HP/{enemy.currentAttack} ATK"))}");
        if (drawn > 0)
            Log($"Drew {drawn} card for the encounter.");
    }

    public bool SelectEnemyTarget(EnemyRuntime enemy)
    {
        if (enemy == null || enemy.IsDefeated || _reactionFaulted || !_enemies.Contains(enemy)) return false;
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
            GameState.PlayerTurn => CanExtendPlayerSelection(selectedCards, candidate),
            GameState.EnemyAttacking => CanAddDefenseCard(selectedCards, candidate),
            _ => false
        };
    }

    bool CanExtendPlayerSelection(IReadOnlyList<CardView> selectedCards, CardView candidate)
    {
        int count = selectedCards?.Count ?? 0;
        if (count == 0) return true;
        if (count >= 3) return false;
        if (count == 1)
        {
            if (CanPairSelection(selectedCards, candidate)) return true;
            // With one selected card, a second click either creates a legal pair/Hands set
            // or replaces the ordinary single-card selection, matching existing click UX.
            return true;
        }
        return GameplayEffectResolver.CanContinueSameRankSelection(selectedCards, candidate,
            CardManager.Instance);
    }

    public bool CanExtendSameRankSelection(IReadOnlyList<CardView> selectedCards, CardView candidate) =>
        GameplayEffectResolver.CanContinueSameRankSelection(selectedCards, candidate, CardManager.Instance);

    public bool CanPairSelection(IReadOnlyList<CardView> selectedCards, CardView candidate)
    {
        return currentState == GameState.PlayerTurn && selectedCards != null && selectedCards.Count == 1 &&
            selectedCards[0]?.data != null && candidate?.data != null &&
            (selectedCards[0].data.Rank == CardData.Rank.Ace || candidate.data.Rank == CardData.Rank.Ace);
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

    public bool TryPreviewDefense(IReadOnlyList<CardView> cards, out int blockedCount, out int remainingDamage)
    {
        blockedCount = 0;
        remainingDamage = pendingDamage;
        if (cards == null || cards.Count == 0 || !AreValidHandViews(cards)) return false;
        var matched = MatchDefenseAssignments(cards);
        if (matched == null) return false;
        blockedCount = matched.Count;
        for (int i = 0; i < matched.Count; i++)
            remainingDamage -= _pendingAttacks[matched[i].AttackIndex];
        return true;
    }

    bool CanAddDefenseCard(IReadOnlyList<CardView> selectedCards, CardView candidate)
    {
        if (pendingDamage <= 0 || currentState != GameState.EnemyAttacking) return false;
        var proposed = new List<CardView>((selectedCards?.Count ?? 0) + 1);
        if (selectedCards != null) proposed.AddRange(selectedCards);
        proposed.Add(candidate);
        return AreValidHandViews(proposed) && MatchDefenseAssignments(proposed) != null;
    }

    // Strongest cards are assigned to the strongest remaining attack they can fully cover.
    // Equal values keep their original hand/encounter order for deterministic ties.
    List<DefenseAssignment> MatchDefenseAssignments(IReadOnlyList<CardView> cards)
    {
        if (cards.Count > _pendingAttacks.Count) return null;
        var cardOrder = new List<int>(cards.Count);
        for (int i = 0; i < cards.Count; i++) cardOrder.Add(i);
        cardOrder.Sort((a, b) =>
        {
            int comparison = CalculateCardDefense(cards[b].data).CompareTo(CalculateCardDefense(cards[a].data));
            return comparison != 0 ? comparison : a.CompareTo(b);
        });
        var attackOrder = new List<int>(_pendingAttacks.Count);
        for (int i = 0; i < _pendingAttacks.Count; i++) attackOrder.Add(i);
        attackOrder.Sort((a, b) =>
        {
            int comparison = _pendingAttacks[b].CompareTo(_pendingAttacks[a]);
            return comparison != 0 ? comparison : a.CompareTo(b);
        });

        var matched = new List<DefenseAssignment>(cards.Count);
        foreach (int cardIndex in cardOrder)
        {
            int defense = CalculateCardDefense(cards[cardIndex].data);
            int attackIndex = attackOrder.FindIndex(index => _pendingAttacks[index] <= defense);
            if (attackIndex < 0) return null;
            matched.Add(new DefenseAssignment(cards[cardIndex].data, cardIndex, attackOrder[attackIndex]));
            attackOrder.RemoveAt(attackIndex);
        }
        return matched;
    }

    public void PlayCard(CardView card)
    {
        if (card != null)
            TryPlayCards(new[] { card }, currentEnemy);
    }

    public float CriticalChancePercent
    {
        get => criticalChancePercent;
        set => criticalChancePercent = Mathf.Clamp(value, 0f, 100f);
    }

    public void ConfigureCriticalRandom(IRandomSource random) => _criticalRandom = random;

    public bool CanPlayCards(IReadOnlyList<CardView> cards, EnemyRuntime target)
    {
        return !GameplayInputGate.IsBlocked && !_reactionFaulted && !_isResolvingAction && currentState == GameState.PlayerTurn &&
            target != null && !target.IsDefeated && _enemies.Contains(target) && !_encounterResolved &&
            AreValidHandViews(cards) && GameplayEffectResolver.CanPlaySelection(cards, target, CardManager.Instance);
    }

    public bool TryPlayCards(IReadOnlyList<CardView> cards, EnemyRuntime target)
    {
        return TryPlayCards(cards, target, 1);
    }

    public bool TryPlayCards(IReadOnlyList<CardView> cards, EnemyRuntime target, int hitCount)
    {
        if (hitCount <= 0 || !CanPlayCards(cards, target)) return false;

        var cardInstances = new CardInstance[cards.Count];
        int aggregatedDamage = 0;
        var names = new List<string>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            cardInstances[i] = cards[i].data;
            aggregatedDamage += CalculateCardAttackDamage(cardInstances[i]);
            names.Add(cardInstances[i].DisplayName);
        }
        bool critical = false;
        var cm = CardManager.Instance;
        bool isAcePair = cards.Count == 2 && GameplayEffectResolver.CanPlayAsAcePair(cards);
        bool hasCriticalChanceOverride = GameplayEffectResolver.TryGetCriticalChanceOverride(
            cardInstances, cm, out float criticalChanceOverride);
        float actionCriticalChance = hasCriticalChanceOverride
            ? criticalChanceOverride : criticalChancePercent;

        _isResolvingAction = true;
        _reactions.Clear();
        _activeAction = CreateAction(
            CombatActionOrigin.PlayerCard,
            sourcePlayer: player,
            card: cardInstances[0],
            targetEnemy: target,
            hitCount: hitCount);
        int damagePerHit = GameplayEffectResolver.CalculateActionDamage(
            new ActionDamageEffectContext(_activeAction, cardInstances, target, cm, aggregatedDamage,
                GameplayEffectResolver.IsHandsMultiCardAction(cardInstances, cm)),
            out int actionDamageCap);
        _nextReactiveHitIndex = hitCount;

        try
        {
            if (!cm.TryDiscardCards(cards)) return false;

            var handSnapshot = new List<CardInstance>(cm.hand);
            GameplayEffectResolver.EnqueueAttackCommitted(
                new AttackCommittedEffectContext(_activeAction, cardInstances, handSnapshot,
                    target, this, cm), _reactions);
            bool reactionFailed = !ProcessReactions(CombatReactionPhase.AttackCommitted);

            if (!reactionFailed && (isAcePair || hasCriticalChanceOverride))
            {
                if (actionCriticalChance >= 100f) critical = true;
                else if (actionCriticalChance > 0f && _criticalRandom != null)
                    critical = _criticalRandom.NextFloat() * 100f < actionCriticalChance;
            }
            if (critical) damagePerHit *= 2;
            int remainingActionDamage = actionDamageCap < 0 ? -1 :
                (int)Math.Min((long)actionDamageCap * (critical ? 2 : 1), int.MaxValue);

            for (int cardIndex = 0; cardIndex < cards.Count && !reactionFailed; cardIndex++)
            {
                EnqueueCardCommittedEffects(_activeAction, cardInstances[cardIndex], target, cm, cardIndex);
                reactionFailed = !ProcessReactions(CombatReactionPhase.CardCommitted);
            }

            int totalHpLost = 0;
            int resolvedHits = 0;
            for (int hitIndex = 0; hitIndex < hitCount && !reactionFailed; hitIndex++)
            {
                if (target.IsDefeated || !_enemies.Contains(target)) break;
                int requestedDamage = remainingActionDamage < 0
                    ? damagePerHit : Math.Min(damagePerHit, remainingActionDamage);
                if (requestedDamage <= 0 && remainingActionDamage == 0) break;
                if (remainingActionDamage >= 0) remainingActionDamage -= requestedDamage;

                var request = new DamageRequest(
                    _activeAction,
                    hitIndex,
                    CombatDamageOrigin.Card,
                    player,
                    target,
                    requestedDamage);
                var result = ResolveDamage(request);
                totalHpLost += result.ActualHpLost;
                resolvedHits++;
                if (!target.IsDefeated)
                    EnqueueIncomingHitResolvedAbilities(_activeAction, target, result);
                reactionFailed = !ProcessReactions(CombatReactionPhase.HitResolved);
                if (_reactionFaulted) return true;
                if (player.IsDefeated)
                {
                    ResolveEncounter(EncounterResult.Defeat);
                    return true;
                }

                if (target.IsDefeated)
                {
                    HandleEnemyDefeated(target);
                    break;
                }
            }

            if (hitCount == 1)
                Log($"Played {string.Join(" + ", names)} on {target.DisplayName}{(critical ? " (CRITICAL)" : string.Empty)} for {totalHpLost} damage! (HP: {target.currentHp}/{target.maxHp})");
            else
                Log($"Played {string.Join(" + ", names)} on {target.DisplayName}{(critical ? " (CRITICAL)" : string.Empty)}: {resolvedHits}/{hitCount} hits dealt {totalHpLost} damage. (HP: {target.currentHp}/{target.maxHp})");

            if (!reactionFailed && !target.IsDefeated && _enemies.Contains(target))
            {
                EnqueuePlayerCardResolvedAbilities(_activeAction, target);
                reactionFailed = !ProcessReactions(CombatReactionPhase.CardResolved);
            }

            if (_reactionFaulted) return true;

            CompletePlayerTurnForEnemies();
            if (_enemies.Count == 0)
            {
                _queuedPlayerTurnGrants.Clear();
                ResolveEncounter(EncounterResult.Victory);
                return true;
            }

            _completedPlayerTurnCount++;
            GameplayEffectResolver.EnqueueCompletedActionTurnGrants(
                new CompletedPlayerActionContext(_activeAction, player, this, cm,
                    cardInstances, _completedPlayerTurnCount), _queuedPlayerTurnGrants);
            if (BeginGrantedPlayerTurn()) return true;
            BeginEnemyAttack();
            return true;
        }
        finally
        {
            _activeAction = null;
            _isResolvingAction = false;
            _reactions.Clear();
        }
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
        return !GameplayInputGate.IsBlocked && !_reactionFaulted && !_isResolvingAction && currentState == GameState.EnemyAttacking &&
            target != null && !target.IsDefeated && _enemies.Contains(target) && !_encounterResolved &&
            cards != null && cards.Count > 0 && AreValidHandViews(cards) && MatchDefenseAssignments(cards) != null;
    }

    public bool TryDefendWithCards(IReadOnlyList<CardView> cards)
    {
        return TryDefendWithCards(cards, currentEnemy);
    }

    public bool TryDefendWithCards(IReadOnlyList<CardView> cards, EnemyRuntime target)
    {
        if (!CanDefendWithCards(cards, target)) return false;

        var assignments = MatchDefenseAssignments(cards);
        if (assignments == null) return false;
        var names = new List<string>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
            names.Add(cards[i].data.DisplayName);

        _isResolvingAction = true;
        _reactions.Clear();
        _activeAction = CreateAction(CombatActionOrigin.PlayerDefense, sourcePlayer: player);
        _nextReactiveHitIndex = 0;
        try
        {
            var cardManager = CardManager.Instance;
            if (cardManager == null || !cardManager.TryDiscardCards(cards)) return false;

            assignments.Sort((left, right) => left.AttackIndex.CompareTo(right.AttackIndex));
            int blockedDamage = 0;
            for (int blockOrder = 0; blockOrder < assignments.Count; blockOrder++)
            {
                var assignment = assignments[blockOrder];
                int attackIndex = assignment.AttackIndex;
                int attackDamage = _pendingAttacks[attackIndex];
                var attacker = _pendingAttackSources[attackIndex];
                blockedDamage += attackDamage;
                GameplayEffectResolver.EnqueueAttackBlocked(
                    new AttackBlockedEffectContext(_activeAction, assignment.BlockingCard, attacker,
                        attackDamage, blockOrder, this, cardManager), _reactions);
            }

            for (int i = assignments.Count - 1; i >= 0; i--)
            {
                int attackIndex = assignments[i].AttackIndex;
                _pendingAttacks.RemoveAt(attackIndex);
                _pendingAttackSources.RemoveAt(attackIndex);
            }
            SetPendingDamage(pendingDamage - blockedDamage);
            Log($"Defended with {string.Join(", ", names)}: blocked {assignments.Count} attack(s) ({blockedDamage} damage). Remaining: {pendingDamage}");

            bool reactionsCompleted = ProcessReactions(CombatReactionPhase.AttackBlocked);
            if (reactionsCompleted)
                ProcessReactions(CombatReactionPhase.HitResolved);
            if (_reactionFaulted) return true;
            if (!_encounterResolved && _enemies.Count == 0)
                ResolveEncounter(EncounterResult.Victory);
            if (_encounterResolved) return true;

            if (pendingDamage <= 0)
            {
                BeginPlayerTurn();
                Log("Defended! Your turn.");
            }
            return true;
        }
        finally
        {
            _activeAction = null;
            _isResolvingAction = false;
            _reactions.Clear();
        }
    }

    public void TakeRemainingDamage()
    {
        if (GameplayInputGate.IsBlocked || _isResolvingAction || currentState != GameState.EnemyAttacking ||
            _enemies.Count == 0 || _encounterResolved)
            return;

        int damage = pendingDamage;
        _pendingAttacks.Clear();
        _pendingAttackSources.Clear();
        SetPendingDamage(0);
        ResolvePlayerDamageAction(
            CombatActionOrigin.EnemyRetaliation,
            CombatDamageOrigin.EnemyAggregate,
            damage,
            "Took",
            allowShield: true);

        if (!_encounterResolved && !_reactionFaulted)
        {
            BeginPlayerTurn();
            Log("Your turn.");
        }
    }

    public void Recover()
    {
        if (_reactionFaulted || GameplayInputGate.IsBlocked || _isResolvingAction || currentState != GameState.PlayerTurn ||
            _enemies.Count == 0 || _encounterResolved || HandCount() != 0)
            return;

        int damage = TotalEnemyAttack;
        // Temporary Phase 7B.5 policy: Recover is combat damage and may consume one Shield charge.
        ResolvePlayerDamageAction(
            CombatActionOrigin.Recovery,
            CombatDamageOrigin.Recovery,
            damage,
            "Recovery attack dealt",
            allowShield: true);
        if (_encounterResolved || _reactionFaulted) return;

        int drawn = CardManager.Instance.DrawToHand(1);
        Log(drawn > 0 ? "Recovered and drew 1 card." : "Recovered, but no card could be drawn.");
    }

    void BeginEnemyAttack()
    {
        _pendingAttacks.Clear();
        _pendingAttackSources.Clear();
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (enemy != null && !enemy.IsDefeated && enemy.currentAttack > 0)
            {
                _pendingAttacks.Add(enemy.currentAttack);
                _pendingAttackSources.Add(enemy);
            }
        }
        _attackCountAtStart = _pendingAttacks.Count;
        int total = 0;
        for (int i = 0; i < _pendingAttacks.Count; i++) total += _pendingAttacks[i];
        SetPendingDamage(total);
        if (pendingDamage <= 0)
        {
            BeginPlayerTurn();
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
        if (enemy == null || !_enemies.Contains(enemy) || !_processedEnemyDeaths.Add(enemy)) return;
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

    DamageResult ResolveDamage(DamageRequest request)
    {
        var result = _resolver.Resolve(request, this);
        if (ReferenceEquals(request.Target, player))
            NotifyPlayerHealthChanged();
        OnDamageResolved?.Invoke(result);
        return result;
    }

    void ResolvePlayerDamageAction(
        CombatActionOrigin actionOrigin,
        CombatDamageOrigin damageOrigin,
        int amount,
        string logPrefix,
        bool allowShield)
    {
        _isResolvingAction = true;
        _reactions.Clear();
        _activeAction = CreateAction(actionOrigin);
        _nextReactiveHitIndex = 1;
        try
        {
            var request = new DamageRequest(
                _activeAction,
                0,
                damageOrigin,
                null,
                player,
                amount,
                allowShield);
            var result = ResolveDamage(request);
            ProcessReactions(CombatReactionPhase.HitResolved);
            Log($"{logPrefix} {result.ActualHpLost} damage. (HP: {player.currentHealth}/{player.maxHealth})");

            if (_reactionFaulted) return;
            if (player.IsDefeated)
                ResolveEncounter(EncounterResult.Defeat);
        }
        finally
        {
            _activeAction = null;
            _isResolvingAction = false;
            _reactions.Clear();
        }
    }

    public int GrantPlayerShield(int charges)
    {
        return player.GainShield(charges);
    }

    public int GrantEnemyShield(EnemyRuntime enemy, int charges)
    {
        return enemy != null && _enemies.Contains(enemy) ? enemy.GainShield(charges) : 0;
    }

    public bool QueueReactiveDamage(
        ICombatDamageTarget source,
        ICombatDamageTarget target,
        int amount,
        int sourceOrder,
        int handlerOrder,
        int priority = 0,
        CombatReactionSourceCategory sourceCategory = CombatReactionSourceCategory.Core)
    {
        if (!_isResolvingAction || _activeAction == null || target == null ||
            (_processingReactionPhase != CombatReactionPhase.HitResolved &&
             _processingReactionPhase != CombatReactionPhase.AttackBlocked))
            return false;
        var phase = _processingReactionPhase.Value;
        int hitIndex = _nextReactiveHitIndex++;
        return _reactions.Enqueue(
            _activeAction.ActionId,
            hitIndex,
            phase,
            sourceCategory,
            sourceOrder,
            handlerOrder,
            () =>
            {
                var request = new DamageRequest(
                    _activeAction,
                    hitIndex,
                    CombatDamageOrigin.Reactive,
                    source,
                    target,
                    amount);
                var result = ResolveDamage(request);
                if (target is EnemyRuntime enemy && !enemy.IsDefeated)
                    EnqueueIncomingHitResolvedAbilities(_activeAction, enemy, result);
                if (target is EnemyRuntime defeatedEnemy && defeatedEnemy.IsDefeated)
                    HandleEnemyDefeated(defeatedEnemy);
            },
            priority);
    }

    void EnqueueEncounterStartedAbilities(CombatActionContext action, EnemyRuntime enemy, int enemyOrder)
    {
        var abilities = enemy?.type?.abilities;
        if (abilities == null) return;
        for (int abilityIndex = 0; abilityIndex < abilities.Length; abilityIndex++)
        {
            var ability = abilities[abilityIndex];
            if (ability == null) continue;
            int capturedIndex = abilityIndex;
            _reactions.Enqueue(
                action.ActionId,
                -1,
                CombatReactionPhase.EncounterStarted,
                CombatReactionSourceCategory.EnemyAbility,
                enemyOrder,
                capturedIndex,
                () => ability.OnEncounterStarted(enemy, this));
        }
    }

    void EnqueueCardCommittedEffects(
        CombatActionContext action,
        CardInstance card,
        EnemyRuntime target,
        CardManager cards,
        int cardOrder)
    {
        GameplayEffectResolver.EnqueueCardCommitted(
            new CardCommittedEffectContext(action, card, target, this, cards, cardOrder), _reactions);
    }

    bool BeginGrantedPlayerTurn()
    {
        if (_queuedPlayerTurnGrants.Count == 0) return false;
        var grant = _queuedPlayerTurnGrants.Dequeue();
        Log($"{grant.SourceName} grants an extra player turn.");
        BeginPlayerTurn();
        return true;
    }

    void BeginPlayerTurn()
    {
        if (_reactionFaulted) return;
        SetState(GameState.PlayerTurn);
        var cards = CardManager.Instance;
        if (cards == null || _encounterResolved) return;
        // Separate bounded phase so a zero-attack turn cannot disturb the action's hit queue.
        var turnQueue = new CombatReactionQueue();
        var context = new PlayerTurnStartEffectContext(CreateAction(CombatActionOrigin.Legacy),
            player, this, cards, ++_playerTurnNumber);
        GameplayEffectResolver.EnqueuePlayerTurnStart(context, turnQueue);
        if (!turnQueue.ProcessPhase(CombatReactionPhase.PlayerTurnStarted,
            () => player.IsDefeated || _encounterResolved))
        {
            HandleReactionFault(CombatReactionPhase.PlayerTurnStarted);
            return;
        }
    }

    internal void LogEffect(string message) => Log(message);

    void EnqueueIncomingHitResolvedAbilities(
        CombatActionContext action,
        EnemyRuntime target,
        DamageResult result)
    {
        var abilities = target?.type?.abilities;
        if (abilities == null) return;
        int enemyOrder = _enemies.IndexOf(target);
        for (int abilityIndex = 0; abilityIndex < abilities.Length; abilityIndex++)
        {
            var ability = abilities[abilityIndex];
            if (ability == null) continue;
            int capturedIndex = abilityIndex;
            _reactions.Enqueue(
                action.ActionId,
                result.Request.HitIndex,
                CombatReactionPhase.HitResolved,
                CombatReactionSourceCategory.EnemyAbility,
                enemyOrder,
                capturedIndex,
                () =>
                {
                    if (target.IsDefeated || !_enemies.Contains(target)) return;
                    ability.OnIncomingHitResolved(target, this, result);
                });
        }
    }

    void EnqueuePlayerCardResolvedAbilities(CombatActionContext action, EnemyRuntime target)
    {
        var abilities = target?.type?.abilities;
        if (abilities == null) return;
        int enemyOrder = _enemies.IndexOf(target);
        for (int abilityIndex = 0; abilityIndex < abilities.Length; abilityIndex++)
        {
            var ability = abilities[abilityIndex];
            if (ability == null) continue;
            int capturedIndex = abilityIndex;
            _reactions.Enqueue(
                action.ActionId,
                -1,
                CombatReactionPhase.CardResolved,
                CombatReactionSourceCategory.EnemyAbility,
                enemyOrder,
                capturedIndex,
                () =>
                {
                    if (target.IsDefeated || !_enemies.Contains(target)) return;
                    ability.OnPlayerCardResolved(target, this);
                });
        }
    }

    bool ProcessReactions(CombatReactionPhase phase)
    {
        _processingReactionPhase = phase;
        try
        {
            bool completed = _reactions.ProcessPhase(
                phase,
                () => player.IsDefeated || _encounterResolved);
            if (!completed)
            {
                Log($"Reaction processing halted during {phase}; remaining action effects were skipped.");
                HandleReactionFault(phase);
            }
            return completed;
        }
        finally
        {
            _processingReactionPhase = null;
        }
    }

    void HandleReactionFault(CombatReactionPhase phase)
    {
        if (_reactionFaulted) return;
        _reactionFaulted = true;
        _queuedPlayerTurnGrants.Clear();
        CardManager.Instance?.CancelCardInteractions();
        SetState(GameState.GameOver);
        Log($"Encounter halted after reaction processing failed during {phase}. No victory or rewards will be granted.");
    }

    CombatActionContext CreateAction(
        CombatActionOrigin origin,
        PlayerRuntime sourcePlayer = null,
        EnemyRuntime sourceEnemy = null,
        CardInstance card = null,
        EnemyRuntime targetEnemy = null,
        int hitCount = 1)
    {
        return new CombatActionContext(
            _nextActionId++,
            origin,
            sourcePlayer,
            sourceEnemy,
            card,
            targetEnemy,
            hitCount);
    }

    void ResolveEncounter(EncounterResult result)
    {
        if (_encounterResolved || _reactionFaulted) return;

        _encounterResolved = true;
        CardManager.Instance?.CancelCardInteractions();
        UnsubscribeFromEnemies();
        _pendingAttacks.Clear();
        _pendingAttackSources.Clear();
        _attackCountAtStart = 0;
        _queuedPlayerTurnGrants.Clear();
        SetPendingDamage(0);

        if (result == EncounterResult.Victory)
        {
            var cards = CardManager.Instance;
            int reward = _earnedGoldReward;
            var (bonusGold, victoryHeal) = GameplayEffectResolver.EncounterWon(cards);
            reward += bonusGold;

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

    public int CalculateCardAttackDamage(CardInstance card) =>
        GameplayEffectResolver.CalculateAttack(card, CardManager.Instance);

    public int CalculateCardDefense(CardInstance card) =>
        GameplayEffectResolver.CalculateBlock(card, CardManager.Instance);

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
        _pendingAttacks.Clear();
        _pendingAttackSources.Clear();
        _attackCountAtStart = 0;
        currentEnemy = null;
        _earnedGoldReward = 0;
        _processedEnemyDeaths.Clear();
        _reactions.Clear();
        _completedPlayerTurnCount = 0;
        _queuedPlayerTurnGrants.Clear();
        _nextActionId = 1;
        _nextReactiveHitIndex = 0;
        _playerTurnNumber = 0;
        _activeAction = null;
        _isResolvingAction = false;
        _encounterResolved = false;
        _reactionFaulted = false;
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
