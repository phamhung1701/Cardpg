using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class PathNode
{
    public int id;
    public string type;
    public MapNodeType kind;
    public string contentId;
    public int mapIndex;
    public float col, row;
    public List<int> next = new();
    public bool completed, accessible, revealed, hidden;
}

public class RunManager : Singleton<RunManager>
{
    public const int FirstMapBossHp = 20;
    [Header("Enemy Type Assets")]
    public EnemyTypeData thiefType;
    public EnemyTypeData goblinType;
    public EnemyTypeData knightType;

    [Header("Phase 4 Content")]
    public RunContentCatalog contentCatalog;

    [Header("Runtime State")]
    [SerializeField] string runSeed;
    public List<EnemyTypeData> bossDeck = new();
    public int bossIndex;
    public List<PathNode> currentPath = new();

    public event Action OnShowPathScreen;
    public event Action OnHidePathScreen;
    public event Action OnShowShop;
    public event Action OnHideShop;
    public event Action<RunEventDefinition> OnShowEvent;
    public event Action OnHideEvent;
    public event Action OnShowUpgrade;
    public event Action OnHideUpgrade;
    public event Action OnRunCompleted;
    public event Action<string> OnRunStarted;
    public event Action<string> OnCycleStarted;
    public event Action OnMapRevealChanged;
    public event Action<CardInstance> OnBossRewardGranted;
    public event Action OnShopOffersChanged;
    public event Action<string> OnInvestmentResult;
    public event Action OnConsumableRewardChanged;
    public event Action<string, string, IReadOnlyList<CardInstance>, Action<int>> OnShowArtifactDiscardChoice;

    readonly List<EnemyTypeData> _generatedEnemyTypes = new();
    readonly List<EnemyAbility> _generatedEnemyAbilities = new();
    readonly List<ShopOffer> _activeShopOffers = new();
    readonly List<ShopOffer> _activeUpgradeOffers = new();
    readonly HashSet<ShopOfferKind> _usedSpecialShopOffers = new();
    readonly HashSet<string> _purchasedConsumablesInShop = new(StringComparer.Ordinal);
    readonly HashSet<int> _consumableDropResolvedContexts = new();
    readonly Queue<ConsumableRewardOffer> _pendingConsumableRewards = new();
    readonly HashSet<string> _resolvedConsumableRewardContexts = new(StringComparer.Ordinal);
    readonly Dictionary<int, ConsumableData[]> _alchemistChoicesByMap = new();
    readonly List<CardEnhancementData> _pendingHammerEnhancements = new();
    RunEventDefinition _pendingHammerEvent;
    bool _pendingHammerReward;
    bool _hammerRetryPending;
    RunEventDefinition _pendingBloodRitualEvent;
    RelicData _pendingBloodRitualArtifact;
    RelicData[] _pendingBloodRitualReplacements = Array.Empty<RelicData>();
    int _pendingBloodRitualCost;
    bool _pendingBloodRitualSetMaxHealth;
    bool _pendingBloodRitualReward;
    readonly HashSet<MapNodeType> _visitedNodeTypesCurrentMap = new();
    int _visitedNodeTypesMapIndex = -1;
    PathNode _activeNode;
    RunEventDefinition _activeEvent;
    CombatManager _subscribedCombatManager;
    RunRandomContext _randomContext;
    int _completedNodeCount;
    readonly List<int> _pendingInvestmentStakes = new();
    int _paidItemPurchaseCount;
    int _combatWinCount;
    int _merchantGiftRewardCount;
    int _satchelRewardCount;
    bool _madeSuccessfulPurchaseInCurrentShop;
    RunContractState _activeContract;
    RunEventDefinition _pendingContractSelectionEvent;
    int _pendingContractPrice;

    public RunTimingStatistics Timing { get; } = new();

    public int CurrentCycle => BossTotal > 0 ? Math.Max(1, bossIndex == int.MaxValue ? int.MaxValue : bossIndex + 1) : 0;
    public int BossTotal => bossDeck.Count;
    public bool IsInfiniteMode { get; private set; }
    public bool IsRunCompleted { get; private set; }
    public bool CanContinueInfiniteMode => IsRunCompleted && !IsInfiniteMode && bossDeck.Count == 12 && bossIndex == 12;
    public string RunSeed => runSeed;
    public int CurrentMapIndex => bossIndex;
    public int CompletedNodeCount => _completedNodeCount;
    public int PendingInvestmentCount => _pendingInvestmentStakes.Count;
    public int PaidItemPurchaseCount => _paidItemPurchaseCount;
    public int CombatWinCount => _combatWinCount;
    public RunContractState ActiveContract => _activeContract;
    public ConsumableRewardOffer PendingConsumableReward =>
        _pendingConsumableRewards.Count > 0 ? _pendingConsumableRewards.Peek() : null;

    public bool RequestArtifactDiscardChoice(string title, string description,
        IReadOnlyList<CardInstance> choices, Action<int> onResolved)
    {
        if (OnShowArtifactDiscardChoice == null || choices == null || choices.Count == 0 || onResolved == null)
            return false;
        bool resolved = false;
        GameplayInputGate.Set(GameplayInputBlockReason.ArtifactChoice, true);
        OnShowArtifactDiscardChoice.Invoke(title, description, choices, cardId =>
        {
            if (resolved) return;
            resolved = true;
            GameplayInputGate.Set(GameplayInputBlockReason.ArtifactChoice, false);
            onResolved(cardId);
        });
        return true;
    }

    public PathNode ActiveNode => _activeNode;
    public RunEventDefinition ActiveEvent => _activeEvent;
    public bool IsPendingHammerReward => _pendingHammerReward;
    public EnemyTypeData CurrentBoss => bossDeck.Count == 0 || bossIndex < 0
        ? null
        : bossDeck[bossIndex % bossDeck.Count];

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        SubscribeToCombatResults();
    }

    protected override void OnDestroy()
    {
        UnsubscribeFromCombatResults();
        Reset();
        base.OnDestroy();
    }

    void Update()
    {
        Timing.Advance(Time.deltaTime);
    }

    public void StartRun()
    {
        if (string.IsNullOrWhiteSpace(runSeed))
            runSeed = RunSeedUtility.GenerateSeed();
        StartRunWithSeed(runSeed);
    }

    public void StartNewRun()
    {
        StartRunWithSeed(RunSeedUtility.GenerateSeed());
    }

    public void RestartRun()
    {
        StartRunWithSeed(runSeed);
    }

    public void StartRunWithSeed(string seed)
    {
        if (!HasRequiredConfiguration())
        {
            Debug.LogError("RunManager: run configuration is incomplete.", this);
            return;
        }

        runSeed = RunRandomContext.NormalizeSeed(seed);
        _randomContext = new RunRandomContext(runSeed);

        SubscribeToCombatResults();
        CombatManager.Instance.Reset();
        CombatManager.Instance.ConfigureCriticalRandom(_randomContext.CreateStream("combat-critical"));
        CombatManager.Instance.ConfigureDuelistSuitRandom(_randomContext.CreateStream("duelist-suit"));
        CardManager.Instance.Reset();
        ResetRuntimeState();
        CardManager.Instance.ConfigureRandom(_randomContext.CreateStream("cards"));
        CardManager.Instance.BuildDeck();
        CardManager.Instance.DealHand();
        BuildBossDeck();
        Timing.StartNewRun(CurrentMapIndex);
        OnRunStarted?.Invoke(runSeed);
        NextCycle();
    }

    bool HasRequiredConfiguration()
    {
        bool hasFallbackEnemies = thiefType != null && goblinType != null && knightType != null;
        bool hasCatalogEnemies = contentCatalog != null && contentCatalog.normalEnemies != null &&
            contentCatalog.normalEnemies.Any(enemy => enemy != null);
        return (hasFallbackEnemies || hasCatalogEnemies) &&
            CardManager.Instance != null && CombatManager.Instance != null;
    }

    void BuildBossDeck()
    {
        bossDeck.Clear();
        bossIndex = 0;
        foreach (var rank in new[] { CardData.Rank.Jack, CardData.Rank.Queen, CardData.Rank.King })
        {
            var rankBosses = new List<EnemyTypeData>();
            foreach (CardData.Suit suit in Enum.GetValues(typeof(CardData.Suit)))
            {
                var card = CardData.Create(suit, rank);
                rankBosses.Add(CreateBossType(card));
            }
            rankBosses.Shuffle(_randomContext.CreateStream("boss-order", (int)rank));
            bossDeck.AddRange(rankBosses);
        }
    }

    EnemyTypeData CreateBossType(CardData card)
    {
        int gold = card.rank switch
        {
            CardData.Rank.Jack => 10,
            CardData.Rank.Queen => 15,
            _ => 20,
        };
        var boss = EnemyTypeData.Create($"{card.RankName} of {card.SuitName}", 30, 6, gold);
        boss.sourceCard = card;
        var random = _randomContext.CreateStream("boss-ability", (int)card.rank * 10 + (int)card.suit);
        EnemyAbility[] abilities;
        if (card.rank != CardData.Rank.King)
        {
            abilities = new[] { CreateAbility(Choose(random,
                EnemyAbilityEffect.DoubleStrike, EnemyAbilityEffect.HeavySwing,
                EnemyAbilityEffect.Desperation, EnemyAbilityEffect.Regeneration,
                EnemyAbilityEffect.Guarded, EnemyAbilityEffect.Silence,
                EnemyAbilityEffect.Withering, EnemyAbilityEffect.Oppression,
                EnemyAbilityEffect.FullCoverageBlock)) };
        }
        else
        {
            var hardCounters = new[] { EnemyAbilityEffect.Silence, EnemyAbilityEffect.Withering, EnemyAbilityEffect.Oppression };
            bool hasHardCounter = random.NextInt(0, 2) == 0;
            if (hasHardCounter)
            {
                var chosenHardCounter = Choose(random, hardCounters);
                var secondary = Choose(random, new[] { EnemyAbilityEffect.DoubleStrike,
                    EnemyAbilityEffect.HeavySwing, EnemyAbilityEffect.Desperation,
                    EnemyAbilityEffect.Regeneration, EnemyAbilityEffect.Guarded,
                    EnemyAbilityEffect.FullCoverageBlock });
                abilities = new[] { CreateAbility(chosenHardCounter), CreateAbility(secondary) };
            }
            else
            {
                abilities = new[] {
                    CreateAbility(Choose(random, EnemyAbilityEffect.Regeneration, EnemyAbilityEffect.Guarded,
                        EnemyAbilityEffect.FullCoverageBlock)),
                    CreateAbility(Choose(random, EnemyAbilityEffect.DoubleStrike,
                        EnemyAbilityEffect.HeavySwing, EnemyAbilityEffect.Desperation)) };
            }
        }
        boss.abilities = abilities;
        _generatedEnemyTypes.Add(boss);
        return boss;
    }

    static T Choose<T>(IRandomSource random, params T[] options) => options[random.NextInt(0, options.Length)];

    EnemyAbility CreateAbility(EnemyAbilityEffect effect)
    {
        var ability = ScriptableObject.CreateInstance<EnemyAbility>();
        ability.effect = effect;
        ability.id = effect.ToString();
        ability.displayName = effect switch
        {
            EnemyAbilityEffect.HeavySwing => "Heavy Swing",
            EnemyAbilityEffect.Desperation => "Desperation",
            EnemyAbilityEffect.Regeneration => "Regeneration",
            EnemyAbilityEffect.Guarded => "Guarded",
            EnemyAbilityEffect.DoubleStrike => "Double Strike",
            EnemyAbilityEffect.Silence => "Silence",
            EnemyAbilityEffect.Withering => "Withering",
            EnemyAbilityEffect.Oppression => "Oppression",
            EnemyAbilityEffect.FullCoverageBlock => "Unyielding Defense",
            _ => effect.ToString()
        };
        ability.description = effect switch
        {
            EnemyAbilityEffect.HeavySwing => "Alternates normal attacks with a 150% ATK response.",
            EnemyAbilityEffect.Desperation => "Gains 25% ATK while below 50% maximum HP.",
            EnemyAbilityEffect.Regeneration => "Heals 5% maximum HP after an enemy response, up to 3 times.",
            EnemyAbilityEffect.Guarded => "The first non-immune hit is reduced by 50%; resets after each enemy response.",
            EnemyAbilityEffect.DoubleStrike => "Attacks twice per response; each hit deals 60% of the response budget.",
            EnemyAbilityEffect.Silence => "While above 50% HP, disables held-card effects.",
            EnemyAbilityEffect.Withering => "While above 50% HP, prevents player healing.",
            EnemyAbilityEffect.Oppression => "+2 response damage per held card beyond 3.",
            EnemyAbilityEffect.FullCoverageBlock => "Each attack must be fully covered by one Block card; partial or combined blocks are rejected.",
            _ => effect.ToString()
        };
        _generatedEnemyAbilities.Add(ability);
        return ability;
    }

    public string CurrentBossDescription
    {
        get
        {
            var boss = CurrentBoss;
            if (boss == null) return string.Empty;
            string abilities = boss.abilities == null ? string.Empty :
                string.Join(" • ", boss.abilities.Where(ability => ability != null).Select(ability =>
                    string.IsNullOrEmpty(ability.description) ? ability.displayName : $"{ability.displayName}: {ability.description}"));

            int bossHp = CurrentMapIndex == 0 ? FirstMapBossHp : boss.maxHp;
            var stats = ScaleEncounterStats(bossHp, boss.baseAttack, CurrentMapIndex);
            return string.IsNullOrEmpty(abilities)
                ? $"{boss.enemyName} • {stats.hp} HP / {stats.attack} ATK"
                : $"{boss.enemyName} • {stats.hp} HP / {stats.attack} ATK • {abilities}";
        }
    }

    void NextCycle()
    {
        GenerateAndAnnounceCycle();
        OnShowPathScreen?.Invoke();
    }

    void GenerateAndAnnounceCycle()
    {
        GeneratePath(CurrentMapIndex);
        QueueAlchemistRewardForCurrentMap();
        OnCycleStarted?.Invoke(IsInfiniteMode
            ? $"Map {CurrentCycle} / Infinite"
            : $"Map {CurrentCycle} / {BossTotal}");
    }

    public IReadOnlyList<PathNode> GenerateMapForIndex(int mapIndex)
    {
        if (_randomContext == null)
            _randomContext = new RunRandomContext(runSeed);
        return RunMapGenerator.Generate(_randomContext, mapIndex, contentCatalog, GetFallbackEnemies());
    }

    void GeneratePath(int mapIndex)
    {
        currentPath.Clear();
        _visitedNodeTypesCurrentMap.Clear();
        _visitedNodeTypesMapIndex = mapIndex;
        currentPath.AddRange(GenerateMapForIndex(mapIndex));
        GameplayEffectResolver.ApplyMapReady(new MapReadyEffectContext(this, CardManager.Instance, mapIndex));
    }

    public void RefreshCurrentMapEffects()
    {
        if (_randomContext == null || currentPath.Count == 0) return;
        if (GameplayEffectResolver.ApplyMapReady(
            new MapReadyEffectContext(this, CardManager.Instance, CurrentMapIndex)))
            OnMapRevealChanged?.Invoke();
    }

    internal bool TryRerollEligibleHiddenNode()
    {
        var candidate = FindEligibleHiddenNodeToReveal();
        if (candidate == null || _randomContext == null) return false;
        MapNodeType replacement = candidate.kind == MapNodeType.Risk ? MapNodeType.Event : MapNodeType.Risk;
        var definitions = contentCatalog != null ? contentCatalog.GetEvents(replacement) : null;
        if (definitions == null || definitions.Length == 0) return false;
        var random = _randomContext.CreateStream("hidden-trail-content",
            unchecked(candidate.mapIndex * 7919 ^ candidate.id));
        var definition = definitions[random.NextInt(0, definitions.Length)];
        if (definition == null || string.IsNullOrEmpty(definition.id)) return false;
        candidate.kind = replacement;
        candidate.type = replacement.ToString().ToLowerInvariant();
        candidate.contentId = definition.id;
        candidate.hidden = true;
        candidate.revealed = false;
        return true;
    }

    // Shares Lantern's policy: reachable hidden Event/Risk only, ordered by route depth, row, then stable node ID.
    internal bool CanRevealEligibleHiddenNode() => FindEligibleHiddenNodeToReveal() != null;

    internal bool TryRevealEligibleHiddenNode()
    {
        var candidate = FindEligibleHiddenNodeToReveal();
        if (candidate == null) return false;
        candidate.revealed = true;
        return true;
    }

    internal bool TryRevealHiddenNodeFromEvent()
    {
        if (!TryRevealEligibleHiddenNode()) return false;
        OnMapRevealChanged?.Invoke();
        return true;
    }

    PathNode FindEligibleHiddenNodeToReveal()
    {
        if (currentPath.Count == 0) return null;
        var frontier = _activeNode != null && !_activeNode.completed
            ? new[] { _activeNode }
            : currentPath.Where(node => node.accessible && !node.completed)
                .OrderBy(node => node.id).ToArray();
        var pending = new Queue<PathNode>(frontier);
        var visited = new HashSet<int>();
        PathNode candidate = null;
        while (pending.Count > 0)
        {
            var node = pending.Dequeue();
            if (node == null || node.mapIndex != CurrentMapIndex || node.completed ||
                !visited.Add(node.id)) continue;
            if (node.hidden && !node.revealed &&
                (node.kind == MapNodeType.Event || node.kind == MapNodeType.Risk) &&
                (candidate == null || node.col < candidate.col ||
                    (Mathf.Approximately(node.col, candidate.col) &&
                        (node.row < candidate.row ||
                            (Mathf.Approximately(node.row, candidate.row) && node.id < candidate.id)))))
                candidate = node;
            foreach (int nextId in node.next)
            {
                var next = FindNode(nextId);
                if (next != null && !next.completed) pending.Enqueue(next);
            }
        }
        return candidate;
    }

    // These are run-authoritative node/content operations, invoked by typed effects only.
    internal bool TryGrantRandomCommonEnhancement(int completedNodeCount)
    {
        var cards = CardManager.Instance;
        if (_randomContext == null || cards == null) return false;
        var eligible = cards.ownedCards
            .Where(card => card != null && card.Enhancement == null)
            .OrderBy(card => card.Id).ToArray();
        var common = cards.enhancementCatalog
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.canonicalId) &&
                string.Equals(value.rarity, "Common", StringComparison.Ordinal))
            .OrderBy(value => value.canonicalId, StringComparer.Ordinal).ToArray();
        if (eligible.Length == 0 || common.Length == 0) return false;
        var cardRandom = _randomContext.CreateStream("artifact-common-card", completedNodeCount);
        var enhancementRandom = _randomContext.CreateStream("artifact-common-enhancement", completedNodeCount);
        var card = eligible[cardRandom.NextInt(0, eligible.Length)];
        var enhancement = common[enhancementRandom.NextInt(0, common.Length)];
        return cards.ApplyEnhancement(card.Id, enhancement);
    }

    public void OnPathChosen(int id)
    {
        if (GameplayInputGate.IsBlocked) return;
        if (_activeNode != null || IsRunCompleted) return;

        var node = FindNode(id);
        if (node == null || !node.accessible || node.completed) return;

        node.accessible = false;
        node.revealed = true;
        foreach (var other in currentPath)
            if (Mathf.Approximately(other.col, node.col) && other.id != node.id)
                other.accessible = false;

        _activeNode = node;
        OnHidePathScreen?.Invoke();

        switch (node.kind)
        {
            case MapNodeType.Combat:
            case MapNodeType.Elite:
            case MapNodeType.Boss:
                StartEncounter(node);
                break;
            case MapNodeType.Shop:
                ResolvePendingInvestments(node);
                PrepareShopOffers();
                OnShowShop?.Invoke();
                break;
            case MapNodeType.Upgrade:
                PrepareUpgradeOffers();
                if (_activeUpgradeOffers.Count == 0)
                {
                    CompleteActiveNode();
                    ShowPathAfterNode();
                }
                else
                {
                    OnShowUpgrade?.Invoke();
                }
                break;
            case MapNodeType.Event:
            case MapNodeType.Risk:
                _activeEvent = FindEvent(node.kind, node.contentId);
                if (_activeEvent == null)
                {
                    Debug.LogError($"RunManager: no event content found for node {node.id} ({node.kind}).", this);
                    CompleteActiveNode();
                    ShowPathAfterNode();
                    return;
                }
                OnShowEvent?.Invoke(_activeEvent);
                break;
        }
    }

    void StartEncounter(PathNode node)
    {
        var enemyType = node.kind switch
        {
            MapNodeType.Boss => CurrentBoss,
            MapNodeType.Elite => FindAuthoredElite(node.contentId) ?? CreateEliteType(FindEnemy(node.contentId)),
            _ => FindEnemy(node.contentId)
        };

        if (enemyType == null)
        {
            Debug.LogError("RunManager: cannot start encounter without an enemy definition.", this);
            _activeNode = null;
            return;
        }

        var stats = GetEncounterStats(node);
        int encounterCount = node.kind == MapNodeType.Combat
            ? Mathf.Max(1, enemyType.encounterCount)
            : 1;
        var enemies = new List<EnemyRuntime>(encounterCount + 1);
        for (int i = 0; i < encounterCount; i++)
            enemies.Add(new EnemyRuntime(enemyType, i + 1, encounterCount, stats.hp, stats.attack));

        bool isCaptain = node.kind == MapNodeType.Elite && IsCaptainType(enemyType);
        var allies = new List<EnemyTypeData>();
        if (node.kind == MapNodeType.Combat && enemyType.enemyName == "War Drummer")
        {
            var goblin = FindNormalEnemyExact("Goblin");
            if (goblin != null) { allies.Add(goblin); allies.Add(goblin); }
        }
        else if (isCaptain || node.kind == MapNodeType.Combat && ShouldAddGoblinAlly(node, enemyType))
        {
            var goblin = FindNormalEnemyExact("Goblin");
            int allyCount = isCaptain ? 2 : 1;
            for (int i = 0; i < allyCount && goblin != null; i++) allies.Add(goblin);
        }
        foreach (var allyType in allies)
        {
            var allyStats = ScaleEncounterStats(allyType.maxHp, allyType.baseAttack, node.mapIndex);
            int sameTypeCount = allies.Count(value => ReferenceEquals(value, allyType));
            int instance = enemies.Count(value => ReferenceEquals(value.type, allyType)) + 1;
            enemies.Add(new EnemyRuntime(allyType, instance, sameTypeCount, allyStats.hp, allyStats.attack));
        }

        CombatManager.Instance.StartEncounter(enemies, node.kind);
    }

    bool ShouldAddGoblinAlly(PathNode node, EnemyTypeData enemyType)
    {
        if (enemyType.enemyName == "Shieldbearer") return true;
        if (enemyType.enemyName != "Knight" || node.mapIndex < 3) return false;
        var random = (_randomContext ?? new RunRandomContext(runSeed)).CreateStream(
            "knight-weak-ally", unchecked(node.mapIndex * 7919 ^ node.id));
        return random.NextInt(0, 2) == 0;
    }

    static (int hp, int attack) ScaleEncounterStats(int hp, int attack, int mapIndex) =>
        mapIndex >= InfiniteRunScaling.FirstInfiniteMapIndex
            ? InfiniteRunScaling.Scale(hp, attack, mapIndex)
            : EnemyMapScaling.Scale(hp, attack, mapIndex);

    /// <summary>Preview and spawn use the same scaled base stats; elite bonuses are applied afterwards.</summary>
    public (int hp, int attack) GetEncounterStats(PathNode node)
    {
        if (node == null) return (0, 0);
        var authoredElite = node.kind == MapNodeType.Elite ? FindAuthoredElite(node.contentId) : null;
        var source = authoredElite ?? (node.kind == MapNodeType.Boss ? CurrentBoss : FindEnemy(node.contentId));
        if (source == null) return (0, 0);
        int baseHp = node.kind == MapNodeType.Boss && node.mapIndex == 0
            ? FirstMapBossHp : source.maxHp;
        var stats = ScaleEncounterStats(baseHp, source.baseAttack, node.mapIndex);
        return node.kind == MapNodeType.Elite && authoredElite == null
            ? (Mathf.CeilToInt(stats.hp * 1.5f), stats.attack + 2)
            : stats;
    }

    EnemyTypeData CreateEliteType(EnemyTypeData source)
    {
        if (source == null) return null;
        var elite = EnemyTypeData.Create(
            $"Elite {source.enemyName}",
            Mathf.CeilToInt(source.maxHp * 1.5f),
            source.baseAttack + 2,
            source.goldReward * 2);
        var ability = contentCatalog != null ? contentCatalog.eliteAbility : null;
        elite.abilities = ability != null ? new[] { ability } : Array.Empty<EnemyAbility>();
        _generatedEnemyTypes.Add(elite);
        return elite;
    }

    static bool IsCaptainType(EnemyTypeData enemy) => enemy != null && enemy.abilities != null &&
        enemy.abilities.Any(ability => ability != null && ability.effect == EnemyAbilityEffect.Captaincy);

    EnemyTypeData FindAuthoredElite(string contentId) => contentCatalog?.eliteEnemies?.FirstOrDefault(
        enemy => enemy != null && (enemy.name == contentId || enemy.enemyName == contentId));

    EnemyTypeData FindNormalEnemyExact(string enemyName) => GetNormalEnemies().FirstOrDefault(enemy =>
        enemy != null && (string.Equals(enemy.enemyName, enemyName, StringComparison.Ordinal) ||
            string.Equals(enemy.name, enemyName, StringComparison.Ordinal)));

    EnemyTypeData FindEnemy(string contentId)
    {
        foreach (var enemy in GetNormalEnemies())
        {
            if (enemy == null) continue;
            if (enemy.name == contentId || enemy.enemyName == contentId)
                return enemy;
        }
        return GetNormalEnemies().FirstOrDefault(enemy => enemy != null);
    }

    EnemyTypeData[] GetNormalEnemies()
    {
        if (contentCatalog != null && contentCatalog.normalEnemies != null &&
            contentCatalog.normalEnemies.Any(enemy => enemy != null))
            return contentCatalog.normalEnemies;
        return GetFallbackEnemies();
    }

    EnemyTypeData[] GetFallbackEnemies() => new[] { thiefType, goblinType, knightType };

    RunEventDefinition FindEvent(MapNodeType category, string contentId)
    {
        var definitions = contentCatalog != null ? contentCatalog.GetEvents(category) : null;
        if (definitions == null) return null;
        return definitions.FirstOrDefault(definition => definition != null && definition.id == contentId)
            ?? definitions.FirstOrDefault(definition => definition != null);
    }

    PathNode FindNode(int id) => currentPath.FirstOrDefault(node => node.id == id);

    public string GetNodeLabel(PathNode node)
    {
        if (node == null) return "Unknown";
        return node.kind switch
        {
            MapNodeType.Combat => "Combat",
            MapNodeType.Elite => "Elite",
            MapNodeType.Shop => "Shop",
            MapNodeType.Event => "Event",
            MapNodeType.Upgrade => "Training",
            MapNodeType.Risk => "Risk / Reward",
            MapNodeType.Boss => CurrentBoss != null ? $"Boss: {CurrentBoss.enemyName}" : "Boss",
            _ => "Unknown"
        };
    }

    public string GetNodeDesc(PathNode node)
    {
        if (node == null) return string.Empty;
        bool showStats = node.accessible && node.revealed &&
            (node.kind == MapNodeType.Combat || node.kind == MapNodeType.Elite || node.kind == MapNodeType.Boss);
        var stats = showStats ? GetEncounterStats(node) : (hp: 0, attack: 0);
        showStats &= stats.hp > 0;
        return node.kind switch
        {
            MapNodeType.Combat => showStats
                ? GetNormalEncounterPreview(node, stats)
                : "Normal encounter",
            MapNodeType.Elite => showStats
                ? GetElitePreviewDescription(node, stats)
                : "Hard fight • better gold",
            MapNodeType.Shop => "Spend gold on artifacts and card enhancements",
            MapNodeType.Event => "A choice with immediate effects",
            MapNodeType.Upgrade => "Choose a free card enhancement",
            MapNodeType.Risk => "Trade safety for a stronger reward",
            MapNodeType.Boss => showStats
                ? $"Major boss encounter • {stats.hp} HP / {stats.attack} ATK"
                : "Major boss encounter",
            _ => string.Empty
        };
    }

    string GetNormalEncounterPreview(PathNode node, (int hp, int attack) stats)
    {
        var enemy = FindEnemy(node.contentId);
        if (enemy == null) return $"Normal encounter • {stats.hp} HP / {stats.attack} ATK";
        string allyName = enemy.enemyName == "War Drummer" ? "2 Goblins" : null;
        if (allyName == null) return $"Normal encounter • primary enemy {stats.hp} HP / {stats.attack} ATK";
        var ally = FindNormalEnemyExact(allyName);
        if (ally == null) return $"Normal encounter • {enemy.enemyName} • {stats.hp} HP / {stats.attack} ATK";
        int count = enemy.enemyName == "War Drummer" ? 2 : 1;
            var allyStats = ScaleEncounterStats(ally.maxHp, ally.baseAttack, node.mapIndex);
        return $"Normal encounter • {enemy.enemyName} + {allyName} • {AddCappedStats(stats.hp, allyStats.hp, count)} HP / {AddCappedStats(stats.attack, allyStats.attack, count)} ATK total";
    }

    string GetElitePreviewDescription(PathNode node, (int hp, int attack) stats)
    {
        var authored = FindAuthoredElite(node.contentId);
        if (IsCaptainType(authored))
        {
            var goblin = GetNormalEnemies().FirstOrDefault(enemy => enemy != null && enemy.enemyName == "Goblin");
            if (goblin != null)
            {
                var ally = ScaleEncounterStats(goblin.maxHp, goblin.baseAttack, node.mapIndex);
                return $"Elite • 3 enemies • {AddCappedStats(stats.hp, ally.hp, 2)} HP / {AddCappedStats(stats.attack, AddCappedStats(ally.attack, 1, 1), 2)} ATK total";
            }
        }
        return authored != null
            ? $"Elite • 1 enemy • {stats.hp} HP / {stats.attack} ATK"
            : $"Hard fight • better gold • {stats.hp} HP / {stats.attack} ATK";
    }

    static int AddCappedStats(int primary, int secondary, int secondaryCount)
    {
        long total = (long)Math.Max(0, primary) + (long)Math.Max(0, secondary) * Math.Max(0, secondaryCount);
        return (int)Math.Min(int.MaxValue, total);
    }

    public IReadOnlyList<ShopOffer> GetCurrentShopOffers() => _activeShopOffers;
    public IReadOnlyList<ShopOffer> GetCurrentUpgradeOffers() => _activeUpgradeOffers;

    public bool ClaimPendingConsumableReward(int choiceIndex)
    {
        var reward = PendingConsumableReward;
        var cards = CardManager.Instance;
        if (reward == null || cards == null || choiceIndex < 0 || choiceIndex >= reward.Choices.Count ||
            !cards.AddConsumable(reward.Choices[choiceIndex])) return false;
        CompletePendingConsumableReward();
        return true;
    }

    public bool ReplacePendingConsumableReward(int choiceIndex, int backpackSlotIndex)
    {
        var reward = PendingConsumableReward;
        var cards = CardManager.Instance;
        if (reward == null || cards == null || choiceIndex < 0 || choiceIndex >= reward.Choices.Count ||
            !cards.ReplaceConsumableSlot(backpackSlotIndex, reward.Choices[choiceIndex])) return false;
        CompletePendingConsumableReward();
        return true;
    }

    public bool DeclinePendingConsumableReward()
    {
        if (PendingConsumableReward == null) return false;
        CompletePendingConsumableReward();
        return true;
    }

    void CompletePendingConsumableReward()
    {
        if (_pendingConsumableRewards.Count > 0) _pendingConsumableRewards.Dequeue();
        GameplayInputGate.Set(GameplayInputBlockReason.ConsumableReward, _pendingConsumableRewards.Count > 0);
        OnConsumableRewardChanged?.Invoke();
    }

    bool QueueConsumableReward(string contextId, string sourceLabel, IReadOnlyList<ConsumableData> choices)
    {
        if (string.IsNullOrWhiteSpace(contextId) || choices == null || choices.Count == 0 ||
            choices.Any(value => value == null) || _resolvedConsumableRewardContexts.Contains(contextId)) return false;
        var distinctChoices = choices.Distinct().ToArray();
        if (distinctChoices.Length == 0) return false;
        _resolvedConsumableRewardContexts.Add(contextId);
        _pendingConsumableRewards.Enqueue(new ConsumableRewardOffer(contextId, sourceLabel, distinctChoices));
        GameplayInputGate.Set(GameplayInputBlockReason.ConsumableReward, true);
        OnConsumableRewardChanged?.Invoke();
        return true;
    }

    bool QueueRandomNonEnhancementReward(string contextId, string sourceLabel, string streamName, int context)
    {
        var cards = CardManager.Instance;
        if (cards == null || _randomContext == null) return false;
        var candidates = cards.consumableCatalog
            .Where(value => value != null && value.effectType != ConsumableEffectType.ApplyEnhancement)
            .OrderBy(value => value.id, StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) return false;
        var random = _randomContext.CreateStream(streamName, context);
        return QueueConsumableReward(contextId, sourceLabel,
            new[] { candidates[random.NextInt(0, candidates.Length)] });
    }

    void QueueAlchemistRewardForCurrentMap()
    {
        var cards = CardManager.Instance;
        int mapIndex = CurrentMapIndex;
        if (cards == null || _randomContext == null ||
            !cards.HasArtifactSpecialRule(ArtifactSpecialRule.AlchemistsKit)) return;
        if (!_alchemistChoicesByMap.TryGetValue(mapIndex, out var choices))
        {
            var candidates = cards.consumableCatalog
                .Where(value => value != null && value.effectType == ConsumableEffectType.ApplyEnhancement)
                .OrderBy(value => value.id, StringComparer.Ordinal).ToList();
            if (candidates.Count == 0) return;
            candidates.Shuffle(_randomContext.CreateStream("alchemist-options", mapIndex));
            choices = candidates.Take(Mathf.Min(3, candidates.Count)).ToArray();
            _alchemistChoicesByMap.Add(mapIndex, choices);
        }
        QueueConsumableReward($"alchemist:{mapIndex}", "Alchemist’s Kit", choices);
    }

    public bool SellArtifact(long artifactInstanceId, int expectedAuthoredPrice)
    {
        if (!IsActiveShopForSale() || GameplayInputGate.IsBlocked) return false;
        var cards = CardManager.Instance;
        var instance = cards != null ? cards.GetArtifactInstanceById(artifactInstanceId) : null;
        if (instance == null || instance.Definition == null || instance.Definition.price < 0 ||
            instance.Definition.price != expectedAuthoredPrice) return false;
        int payout = expectedAuthoredPrice / 2;
        if (!cards.RemoveArtifactInstance(artifactInstanceId)) return false;
        if (payout > 0) cards.AddGold(payout);
        return true;
    }

    public bool SellConsumable(int slotIndex, ConsumableInstance expectedInstance, int expectedAuthoredPrice)
    {
        if (!IsActiveShopForSale() || GameplayInputGate.IsBlocked || expectedInstance == null) return false;
        var cards = CardManager.Instance;
        var definition = expectedInstance.Definition;
        if (cards == null || definition == null || definition.price < 0 ||
            definition.price != expectedAuthoredPrice) return false;
        int payout = expectedAuthoredPrice / 2;
        if (!cards.RemoveConsumableInstanceAtSlot(slotIndex, expectedInstance)) return false;
        if (payout > 0) cards.AddGold(payout);
        return true;
    }

    bool IsActiveShopForSale() => _activeNode != null && !_activeNode.completed &&
        _activeNode.kind == MapNodeType.Shop && CardManager.Instance != null;

    public bool CanPurchaseShopOffer(string stableId) =>
        string.IsNullOrEmpty(GetShopOfferUnavailableReason(stableId));

    public string GetShopOfferUnavailableReason(string stableId)
    {
        if (_activeNode == null || _activeNode.completed || _activeNode.kind != MapNodeType.Shop || string.IsNullOrEmpty(stableId))
            return "Offer unavailable";

        var offer = _activeShopOffers.FirstOrDefault(candidate => candidate.StableId == stableId);
        var cards = CardManager.Instance;
        if (offer == null || cards == null) return "Offer unavailable";
        if (offer.price < 0) return "Invalid price";

        if (offer.kind == ShopOfferKind.Artifact)
        {
            string reason = cards.GetArtifactAcquisitionUnavailableReason(offer.artifact);
            if (!string.IsNullOrEmpty(reason)) return reason;
        }
        else if (offer.kind == ShopOfferKind.Contract)
        {
            if (_activeContract != null || _pendingContractSelectionEvent != null)
                return "A Contract is already active";
            if (_usedSpecialShopOffers.Contains(offer.kind)) return "Already purchased this Shop visit";
            if (!_madeSuccessfulPurchaseInCurrentShop) return "Purchase another Shop item first";
        }
        else if (offer.kind == ShopOfferKind.Enhancement)
        {
            if (offer.enhancement == null) return "Enhancement unavailable";
            if (cards.hand.Count == 0) return "No cards in hand to target";
        }
        else if (offer.kind == ShopOfferKind.Consumable)
        {
            if (offer.consumable == null || !offer.consumable.shopAvailable ||
                !cards.consumableCatalog.Contains(offer.consumable)) return "Consumable unavailable";
            if (_purchasedConsumablesInShop.Contains(offer.consumable.id)) return "Already purchased this Shop visit";
            if (!cards.CanAddConsumable(offer.consumable)) return "Backpack Full";
        }
        else if (offer.kind == ShopOfferKind.Investment || offer.kind == ShopOfferKind.Guidance)
        {
            if (_usedSpecialShopOffers.Contains(offer.kind)) return "Already purchased this Shop visit";
            if (offer.kind == ShopOfferKind.Investment && (offer.investmentStake <= 0 || offer.investmentStake > cards.gold))
                return "Investment stake must be between 1g and your current Gold";
            if (offer.kind == ShopOfferKind.Guidance && !CanRevealEligibleHiddenNode())
                return "No eligible hidden node to reveal";
        }
        else return "Offer unavailable";

        int requiredGold = offer.kind == ShopOfferKind.Investment ? offer.investmentStake : offer.price;
        return cards.CanAfford(requiredGold)
            ? string.Empty
            : $"Need {requiredGold}g (have {cards.gold}g)";
    }

    public string GetEnhancementTargetUnavailableReason(int cardId)
    {
        var card = CardManager.Instance != null ? CardManager.Instance.FindOwnedCard(cardId) : null;
        if (card == null) return "Target card unavailable";
        return card.Enhancement != null ? "Target already enhanced" : string.Empty;
    }

    // Shop, Upgrade, and direct Event acquisitions deliberately target the current hand only.
    public string GetEnhancementAcquisitionTargetUnavailableReason(int cardId, CardEnhancementData enhancement = null)
    {
        var cards = CardManager.Instance;
        var card = cards != null ? cards.FindOwnedCard(cardId) : null;
        if (card == null || !cards.hand.Contains(card)) return "Target must be a card in your hand";
        return enhancement != null && ReferenceEquals(card.Enhancement, enhancement)
            ? "Card already has this Enhancement" : string.Empty;
    }

    // Artifact purchases need no target. Enhancement purchases require an explicit card ID.
    public bool PurchaseShopOffer(string stableId)
    {
        if (GameplayInputGate.IsBlocked || !CanPurchaseShopOffer(stableId)) return false;
        var offer = _activeShopOffers.First(candidate => candidate.StableId == stableId);
        var cards = CardManager.Instance;
        bool cashbackOwned = cards.HasArtifactSpecialRule(ArtifactSpecialRule.CashbackToken);
        bool giftOwned = cards.HasArtifactSpecialRule(ArtifactSpecialRule.MerchantsGift);
        int goldBefore = cards.gold;
        bool success;
        if (offer.kind == ShopOfferKind.Artifact)
            success = cards.BuyArtifact(offer.artifact, offer.price);
        else if (offer.kind == ShopOfferKind.Contract)
        {
            success = ShowContractSelection(offer.price);
        }
        else if (offer.kind == ShopOfferKind.Consumable)
        {
            success = cards.BuyConsumable(offer.consumable, offer.price);
            if (success) _purchasedConsumablesInShop.Add(offer.consumable.id);
        }
        else if (offer.kind == ShopOfferKind.Investment)
        {
            success = cards.SpendGold(offer.investmentStake);
            if (success)
            {
                _pendingInvestmentStakes.Add(offer.investmentStake);
                _usedSpecialShopOffers.Add(offer.kind);
            }
        }
        else if (offer.kind == ShopOfferKind.Guidance)
        {
            if (!CanRevealEligibleHiddenNode() || !cards.SpendGold(offer.price)) return false;
            if (!TryRevealHiddenNodeFromEvent())
            {
                cards.AddGold(offer.price);
                return false;
            }
            _usedSpecialShopOffers.Add(offer.kind);
            success = true;
        }
        else return false;
        if (!success) return false;
        if (offer.kind != ShopOfferKind.Contract && offer.price > 0)
            _madeSuccessfulPurchaseInCurrentShop = true;
        if (IsEligiblePaidItem(offer.kind))
            CompletePaidItemPurchase(goldBefore - cards.gold, cashbackOwned, giftOwned);
        RepriceActiveShopOffers();
        return true;
    }

    public bool PurchaseShopEnhancement(string stableId, int cardId, bool replaceExisting = false)
    {
        if (GameplayInputGate.IsBlocked || !CanPurchaseShopOffer(stableId)) return false;
        var offer = _activeShopOffers.First(candidate => candidate.StableId == stableId);
        if (offer.kind != ShopOfferKind.Enhancement ||
            !string.IsNullOrEmpty(GetEnhancementAcquisitionTargetUnavailableReason(cardId, offer.enhancement))) return false;
        var cards = CardManager.Instance;
        bool cashbackOwned = cards.HasArtifactSpecialRule(ArtifactSpecialRule.CashbackToken);
        bool giftOwned = cards.HasArtifactSpecialRule(ArtifactSpecialRule.MerchantsGift);
        int goldBefore = cards.gold;
        if (!cards.BuyEnhancement(cardId, offer.enhancement, offer.price, replaceExisting)) return false;
        CompletePaidItemPurchase(goldBefore - cards.gold, cashbackOwned, giftOwned);
        if (offer.price > 0)
            _madeSuccessfulPurchaseInCurrentShop = true;
        return true;
    }

    static bool IsEligiblePaidItem(ShopOfferKind kind) => kind is
        ShopOfferKind.Artifact or ShopOfferKind.Enhancement or ShopOfferKind.Consumable;

    static int RoundPercentage(int value, int percent)
    {
        if (value <= 0 || percent <= 0) return 0;
        return (int)Math.Min(int.MaxValue, ((long)value * percent + 50L) / 100L);
    }

    static int IncrementToMaximum(int value) => value < int.MaxValue ? value + 1 : int.MaxValue;

    void CompletePaidItemPurchase(int actualGoldSpent, bool cashbackOwned, bool giftOwned)
    {
        actualGoldSpent = Math.Max(0, actualGoldSpent);
        if (actualGoldSpent <= 0) return;
        var cards = CardManager.Instance;
        if (cashbackOwned && cards != null)
        {
            int refund = Math.Min(actualGoldSpent, RoundPercentage(actualGoldSpent, 15));
            if (refund > 0) cards.AddGold(refund);
        }
        if (!giftOwned) return;
        _paidItemPurchaseCount = IncrementToMaximum(_paidItemPurchaseCount);
        if (_paidItemPurchaseCount % 3 != 0) return;
        _merchantGiftRewardCount = IncrementToMaximum(_merchantGiftRewardCount);
        QueueRandomNonEnhancementReward(
            $"merchant-gift:{_merchantGiftRewardCount}", "Merchant’s Gift", "merchant-gift", _merchantGiftRewardCount);
    }

    int CalculateEffectiveShopPrice(ShopOfferKind kind, int basePrice)
    {
        basePrice = Math.Max(0, basePrice);
        var cards = CardManager.Instance;
        return IsEligiblePaidItem(kind) && cards != null &&
            cards.HasArtifactSpecialRule(ArtifactSpecialRule.MerchantsBadge)
                ? RoundPercentage(basePrice, 80) : basePrice;
    }

    void RepriceActiveShopOffers()
    {
        bool changed = false;
        for (int i = 0; i < _activeShopOffers.Count; i++)
        {
            var offer = _activeShopOffers[i];
            if (offer == null) continue;
            if (offer.basePrice == 0 && offer.price > 0) offer.basePrice = offer.price;
            int effective = CalculateEffectiveShopPrice(offer.kind, offer.basePrice);
            if (offer.price == effective) continue;
            offer.price = effective;
            changed = true;
        }
        if (changed) OnShopOffersChanged?.Invoke();
    }

    // No implicit target may commit an Upgrade reward.
    public bool ChooseUpgradeOffer(int offerIndex) => false;

    public bool ChooseUpgradeOffer(int offerIndex, int cardId, bool replaceExisting = false)
    {
        if (GameplayInputGate.IsBlocked || _activeNode == null || _activeNode.completed ||
            _activeNode.kind != MapNodeType.Upgrade || offerIndex < 0 || offerIndex >= _activeUpgradeOffers.Count)
            return false;
        var offer = _activeUpgradeOffers[offerIndex];
        if (offer == null || !string.IsNullOrEmpty(GetEnhancementAcquisitionTargetUnavailableReason(cardId, offer.enhancement)) ||
            offer.kind != ShopOfferKind.Enhancement || offer.enhancement == null ||
            !CardManager.Instance.ApplyEnhancement(cardId, offer.enhancement, replaceExisting))
            return false;

        OnHideUpgrade?.Invoke();
        _activeUpgradeOffers.Clear();
        CompleteActiveNode();
        ShowPathAfterNode();
        return true;
    }

    public bool SetInvestmentStake(string stableId, int stake)
    {
        var offer = _activeShopOffers.FirstOrDefault(candidate => candidate != null && candidate.StableId == stableId);
        var cards = CardManager.Instance;
        if (offer == null || offer.kind != ShopOfferKind.Investment || cards == null || stake <= 0 || stake > cards.gold)
            return false;
        offer.investmentStake = stake;
        return true;
    }

    void ResolvePendingInvestments(PathNode shopNode)
    {
        if (_pendingInvestmentStakes.Count == 0 || shopNode == null || CardManager.Instance == null) return;
        if (_randomContext == null) _randomContext = new RunRandomContext(runSeed);
        int context = unchecked(shopNode.mapIndex * 7919 ^ shopNode.id);
        var random = _randomContext.CreateStream("shop-investment-payout", context);
        long payout = 0;
        long totalStake = 0;
        int wins = 0;
        foreach (int stake in _pendingInvestmentStakes)
        {
            totalStake += stake;
            if (random.NextInt(0, 2) != 0) continue;
            wins++;
            payout = Math.Min(int.MaxValue, payout + (long)stake * 3L);
        }
        int losses = _pendingInvestmentStakes.Count - wins;
        _pendingInvestmentStakes.Clear();
        if (payout > 0) CardManager.Instance.AddGold((int)payout);
        OnInvestmentResult?.Invoke(
            $"Investment results: {wins} success(es), {losses} loss(es); payout {payout}g, stakes at risk {totalStake}g.");
    }

    void PrepareShopOffers(int maximumOffers = 4)
    {
        _activeShopOffers.Clear();
        _usedSpecialShopOffers.Clear();
        _madeSuccessfulPurchaseInCurrentShop = false;
        _purchasedConsumablesInShop.Clear();
        var cards = CardManager.Instance;
        if (_activeNode == null || _activeNode.kind != MapNodeType.Shop || cards == null)
            return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevModeRuntime.UnlimitedShopOffers)
        {
            PrepareAllDevShopOffers(cards);
            return;
        }
#endif
        if (maximumOffers <= 0) return;

        int context = unchecked(_activeNode.mapIndex * 397 ^ _activeNode.id);
        var artifacts = cards.relicCatalog
            .Where(artifact => artifact != null &&
                string.IsNullOrEmpty(cards.GetArtifactAcquisitionUnavailableReason(artifact)))
            .OrderBy(artifact => artifact.id, StringComparer.Ordinal)
            .ToList();
        artifacts.Shuffle(_randomContext.CreateStream("shop-artifacts", context));

        int artifactSlots = Mathf.Min(2, maximumOffers);
        var artifactOffers = new List<RelicData>(artifactSlots);
        var eligibleUpgrade = artifacts.FirstOrDefault(artifact => artifact.tier > 1);
        if (eligibleUpgrade != null) artifactOffers.Add(eligibleUpgrade);
        foreach (var artifact in artifacts)
        {
            if (artifactOffers.Count >= artifactSlots) break;
            if (!artifactOffers.Contains(artifact)) artifactOffers.Add(artifact);
        }
        foreach (var artifact in artifactOffers)
        {
            _activeShopOffers.Add(new ShopOffer
            {
                kind = ShopOfferKind.Artifact,
                artifact = artifact,
                basePrice = artifact.price,
                price = CalculateEffectiveShopPrice(ShopOfferKind.Artifact, artifact.price)
            });
        }

        AddEnhancementOffers(
            _activeShopOffers,
            maximumOffers - _activeShopOffers.Count,
            "shop-enhancement-cards",
            "shop-enhancements",
            context,
            false);

        foreach (var artifact in artifacts.Where(artifact => !artifactOffers.Contains(artifact)))
        {
            if (_activeShopOffers.Count >= maximumOffers) break;
            _activeShopOffers.Add(new ShopOffer
            {
                kind = ShopOfferKind.Artifact,
                artifact = artifact,
                basePrice = artifact.price,
                price = CalculateEffectiveShopPrice(ShopOfferKind.Artifact, artifact.price)
            });
        }
        AddConsumableOffers(cards, context);
        AddSpecialShopOffers(context);
    }

    void AddConsumableOffers(CardManager cards, int context)
    {
        var consumables = cards.consumableCatalog
            .Where(value => value != null && value.shopAvailable)
            .OrderBy(value => value.id, StringComparer.Ordinal)
            .ToList();
        if (consumables.Count == 0) return;
        consumables.Shuffle(_randomContext.CreateStream("shop-consumables", context));
        var consumable = consumables[0];
        _activeShopOffers.Add(new ShopOffer
        {
            kind = ShopOfferKind.Consumable,
            consumable = consumable,
            basePrice = consumable.price,
            price = CalculateEffectiveShopPrice(ShopOfferKind.Consumable, consumable.price)
        });
    }

    void AddSpecialShopOffers(int context)
    {
        var investmentAvailability = _randomContext.CreateStream("shop-investment-availability", context);
        var guidanceAvailability = _randomContext.CreateStream("shop-guidance-availability", context);
        if (investmentAvailability.NextInt(0, 2) == 0)
            _activeShopOffers.Add(new ShopOffer { kind = ShopOfferKind.Investment, basePrice = 5, price = 5 });
        if (guidanceAvailability.NextInt(0, 2) == 0)
            _activeShopOffers.Add(new ShopOffer { kind = ShopOfferKind.Guidance, basePrice = 5, price = 5 });
        if (_activeContract == null)
            _activeShopOffers.Add(new ShopOffer { kind = ShopOfferKind.Contract, basePrice = 20, price = 20 });
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void PrepareAllDevShopOffers(CardManager cards)
    {
        foreach (var artifact in cards.relicCatalog
            .Where(value => value != null &&
                string.IsNullOrEmpty(cards.GetArtifactAcquisitionUnavailableReason(value)))
            .OrderBy(value => value.id, StringComparer.Ordinal))
        {
            _activeShopOffers.Add(new ShopOffer
            {
                kind = ShopOfferKind.Artifact,
                artifact = artifact,
                basePrice = artifact.price,
                price = CalculateEffectiveShopPrice(ShopOfferKind.Artifact, artifact.price)
            });
        }

        int slot = 0;
        foreach (var enhancement in cards.enhancementCatalog
            .Where(value => value != null)
            .OrderBy(value => value.id, StringComparer.Ordinal))
        {
            _activeShopOffers.Add(new ShopOffer
            {
                kind = ShopOfferKind.Enhancement,
                enhancement = enhancement,
                slot = slot++,
                basePrice = enhancement.price,
                price = CalculateEffectiveShopPrice(ShopOfferKind.Enhancement, enhancement.price)
            });
        }
    }
#endif

    void PrepareUpgradeOffers(int maximumOffers = 3)
    {
        _activeUpgradeOffers.Clear();
        if (_activeNode == null || _activeNode.kind != MapNodeType.Upgrade) return;
        int context = unchecked(_activeNode.mapIndex * 613 ^ _activeNode.id);
        AddEnhancementOffers(
            _activeUpgradeOffers,
            maximumOffers,
            "upgrade-cards",
            "upgrade-enhancements",
            context,
            true);
    }

    void AddEnhancementOffers(
        List<ShopOffer> destination,
        int maximumOffers,
        string cardStream,
        string enhancementStream,
        int context,
        bool free)
    {
        var cards = CardManager.Instance;
        if (cards == null || maximumOffers <= 0 || cards.enhancementCatalog.Count == 0) return;

        var targets = cards.ownedCards
            .Where(card => card != null && card.Enhancement == null)
            .OrderBy(card => card.Id)
            .ToList();
        var enhancements = cards.enhancementCatalog
            .Where(enhancement => enhancement != null)
            .OrderBy(enhancement => enhancement.id, StringComparer.Ordinal)
            .ToList();
        if (targets.Count == 0 || enhancements.Count == 0) return;

        // Keep the original independent card stream's offer-generation consumption,
        // but never bind its shuffled card to the purchased Enhancement.
        targets.Shuffle(_randomContext.CreateStream(cardStream, context));
        enhancements.Shuffle(_randomContext.CreateStream(enhancementStream, context));
        int count = Mathf.Min(maximumOffers, targets.Count);
        for (int i = 0; i < count; i++)
        {
            var enhancement = enhancements[i % enhancements.Count];
            destination.Add(new ShopOffer
            {
                kind = ShopOfferKind.Enhancement,
                enhancement = enhancement,
                slot = i,
                basePrice = free ? 0 : enhancement.price,
                price = free ? 0 : CalculateEffectiveShopPrice(ShopOfferKind.Enhancement, enhancement.price)
            });
        }
    }

    string GetBloodRitualUnavailableReason(int choiceIndex)
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        if (cards == null || combat == null) return "Ritual unavailable";
        if (choiceIndex == 0)
            return GetRitualArtifactCandidates("Legendary").Length > 0
                ? string.Empty : "No eligible Legendary Artifact reward";
        if (choiceIndex == 1)
            return combat.player.currentHealth > 15 && GetRitualArtifactCandidates("Epic").Length > 0
                ? string.Empty : combat.player.currentHealth <= 15
                    ? "Requires more than 15 HP" : "No eligible Epic Artifact reward";
        if (choiceIndex == 2)
            return combat.player.currentHealth > 5 && GetRitualUpgradeCandidates().Length > 0
                ? string.Empty : combat.player.currentHealth <= 5
                    ? "Requires more than 5 HP" : "No eligible Artifact upgrade";
        return "Choice unavailable";
    }

    RelicData[] GetRitualArtifactCandidates(string rarity)
    {
        var cards = CardManager.Instance;
        if (cards == null) return Array.Empty<RelicData>();
        return cards.relicCatalog.Where(item => item != null &&
                string.Equals(item.rarity, rarity, StringComparison.OrdinalIgnoreCase) &&
                !cards.HasArtifact(item) &&
                (item.tier <= 1 || cards.ownedArtifacts.Any(owned => owned != null &&
                    string.Equals(owned.canonicalId, item.upgradeFromId, StringComparison.Ordinal))))
            .OrderBy(item => item.canonicalId, StringComparer.Ordinal)
            .ThenBy(item => item.id, StringComparer.Ordinal).ToArray();
    }

    RelicData[] GetRitualUpgradeCandidates()
    {
        var cards = CardManager.Instance;
        if (cards == null) return Array.Empty<RelicData>();
        return cards.ownedArtifacts.Where(item => item != null)
            .Select(item => new { Owned = item, Upgrade = ArtifactUpgradeResolver.FindImmediateUpgrade(item, cards.relicCatalog) })
            .Where(pair => pair.Upgrade != null && !cards.HasArtifact(pair.Upgrade))
            .Select(pair => pair.Upgrade)
            .OrderBy(item => item.canonicalId, StringComparer.Ordinal)
            .ThenBy(item => item.id, StringComparer.Ordinal).ToArray();
    }

    bool ResolveBloodRitualOption(int choiceIndex)
    {
        if (_pendingBloodRitualReward || !string.IsNullOrEmpty(GetBloodRitualUnavailableReason(choiceIndex))) return false;
        var combat = CombatManager.Instance;
        var candidates = choiceIndex switch
        {
            0 => GetRitualArtifactCandidates("Legendary"),
            1 => GetRitualArtifactCandidates("Epic"),
            _ => GetRitualUpgradeCandidates()
        };
        if (candidates.Length == 0) return false;
        int context = unchecked((_activeNode?.mapIndex ?? CurrentMapIndex) * 7919 ^
            (_activeNode?.id ?? 0) * 397 ^ choiceIndex);
        var random = (_randomContext ?? new RunRandomContext(runSeed))
            .CreateStream("blood-ritual-reward", context);
        _pendingBloodRitualArtifact = candidates[random.NextInt(0, candidates.Length)];
        _pendingBloodRitualCost = choiceIndex == 1 ? 15 : choiceIndex == 2 ? 5 : 0;
        _pendingBloodRitualSetMaxHealth = choiceIndex == 0;
        _pendingBloodRitualReward = true;

        var cards = CardManager.Instance;
        if (cards.ArtifactSlotsUsed >= cards.ArtifactCapacity && _pendingBloodRitualArtifact.OccupiesCapacitySlot)
            return ShowBloodRitualReplacementPrompt();
        bool committed = CommitBloodRitualReward(null);
        ClearPendingBloodRitual();
        return committed;
    }

    bool ShowBloodRitualReplacementPrompt()
    {
        var cards = CardManager.Instance;
        var artifact = _pendingBloodRitualArtifact;
        if (cards == null || artifact == null) return false;
        _pendingBloodRitualReplacements = artifact.tier > 1
            ? cards.ownedArtifacts.Where(item => item != null &&
                string.Equals(item.canonicalId, artifact.upgradeFromId, StringComparison.Ordinal)).ToArray()
            : cards.ownedArtifacts.Where(item => item != null && item.OccupiesCapacitySlot).ToArray();
        if (_pendingBloodRitualReplacements.Length == 0)
        {
            ClearPendingBloodRitual();
            return false;
        }

        _pendingBloodRitualEvent = ScriptableObject.CreateInstance<RunEventDefinition>();
        _pendingBloodRitualEvent.id = $"blood-ritual-replace:{_activeNode?.id ?? 0}";
        _pendingBloodRitualEvent.category = MapNodeType.Risk;
        _pendingBloodRitualEvent.title = "BLOOD RITUAL — CLAIM YOUR REWARD";
        _pendingBloodRitualEvent.description = $"Choose an Artifact to replace with {_pendingBloodRitualArtifact.displayName}, or cancel without paying the cost.";
        _pendingBloodRitualEvent.choices = _pendingBloodRitualReplacements
            .Select(item => new RunEventChoice
            {
                label = $"Replace {item.displayName}",
                description = $"Replace it with {_pendingBloodRitualArtifact.displayName}.",
                effects = Array.Empty<RunEffect>(),
                interaction = RunEventChoiceInteraction.Immediate
            })
            .Concat(new[] { new RunEventChoice
            {
                label = "Cancel Ritual",
                description = "Keep your Artifacts. The ritual cost is not paid.",
                effects = Array.Empty<RunEffect>(),
                interaction = RunEventChoiceInteraction.Immediate
            }}).ToArray();
        _activeEvent = _pendingBloodRitualEvent;
        if (OnShowEvent == null)
        {
            _activeEvent = null;
            ClearPendingBloodRitual();
            return false;
        }
        OnShowEvent.Invoke(_activeEvent);
        return true;
    }

    bool ResolveBloodRitualReplacement(int choiceIndex)
    {
        if (!_pendingBloodRitualReward || _pendingBloodRitualEvent == null ||
            choiceIndex < 0 || choiceIndex >= _activeEvent.choices.Length) return false;
        bool cancel = choiceIndex == _pendingBloodRitualReplacements.Length;
        bool success = cancel || CommitBloodRitualReward(_pendingBloodRitualReplacements[choiceIndex]);
        if (!success) return false;
        OnHideEvent?.Invoke();
        _activeEvent = null;
        ClearPendingBloodRitual();
        CompleteActiveNode();
        ShowPathAfterNode();
        return true;
    }

    bool CommitBloodRitualReward(RelicData replacement)
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        var artifact = _pendingBloodRitualArtifact;
        if (!_pendingBloodRitualReward || cards == null || combat == null || artifact == null ||
            _pendingBloodRitualCost > 0 && !combat.CanTakeRunDamage(_pendingBloodRitualCost)) return false;
        if (!cards.GrantArtifactReward(artifact, replacement)) return false;
        if (_pendingBloodRitualSetMaxHealth)
            combat.SetPlayerMaxHealthAndClamp(5);
        else if (_pendingBloodRitualCost > 0)
            combat.TakeRunDamage(_pendingBloodRitualCost);
        return true;
    }

    void ClearPendingBloodRitual()
    {
        if (_pendingBloodRitualEvent != null) DestroyRuntimeObject(_pendingBloodRitualEvent);
        _pendingBloodRitualEvent = null;
        _pendingBloodRitualArtifact = null;
        _pendingBloodRitualReplacements = Array.Empty<RelicData>();
        _pendingBloodRitualCost = 0;
        _pendingBloodRitualSetMaxHealth = false;
        _pendingBloodRitualReward = false;
    }

    bool ShowContractSelection(int pendingPrice)
    {
        if (_activeNode == null || _activeNode.kind != MapNodeType.Shop || _activeContract != null ||
            _pendingContractSelectionEvent != null || OnShowEvent == null) return false;
        var selection = ScriptableObject.CreateInstance<RunEventDefinition>();
        selection.id = $"quest-contract-select:{_activeNode.mapIndex}:{_activeNode.id}";
        selection.category = MapNodeType.Event;
        selection.title = "QUEST CONTRACT";
        selection.description = "Choose one objective. The Contract occupies no Artifact slot; its 20 Gold fee is charged only when you confirm an objective. Cancel keeps your Gold.";
        selection.choices = new[]
        {
            new RunEventChoice { label = "Combat Contract", description = "Win 2 encounters within 12 maps. Reward: 6 Gold. Failure: lose 5 HP.", effects = Array.Empty<RunEffect>() },
            new RunEventChoice { label = "Elite Contract", description = "Defeat a reachable Elite before leaving this map. Reward: a Rare Artifact, or heal 5 HP if none is eligible. Failure: lose 5 HP.", effects = Array.Empty<RunEffect>() },
            new RunEventChoice { label = "Flawless Contract", description = "Win 1 encounter without losing HP. Reward: 10 Gold. Failure: lose 5 HP.", effects = Array.Empty<RunEffect>() },
            new RunEventChoice { label = "Cancel", description = "Return to the Shop. The Contract fee is not charged.", effects = Array.Empty<RunEffect>() }
        };
        _pendingContractPrice = Math.Max(0, pendingPrice);
        _pendingContractSelectionEvent = selection;
        _activeEvent = selection;
        OnHideShop?.Invoke();
        OnShowEvent.Invoke(selection);
        return true;
    }

    bool CanReachEliteAfterActiveNode()
    {
        if (_activeNode == null || currentPath.Count == 0) return false;
        var pending = new Queue<int>(_activeNode.next);
        var visited = new HashSet<int>();
        while (pending.Count > 0)
        {
            int id = pending.Dequeue();
            if (!visited.Add(id)) continue;
            var node = FindNode(id);
            if (node == null || node.completed || node.mapIndex != CurrentMapIndex) continue;
            if (node.kind == MapNodeType.Elite) return true;
            foreach (int next in node.next) pending.Enqueue(next);
        }
        return false;
    }

    bool ResolveContractSelection(int choiceIndex)
    {
        if (_pendingContractSelectionEvent == null || _activeContract != null ||
            choiceIndex < 0 || choiceIndex > 3 || _activeNode == null || _activeNode.kind != MapNodeType.Shop)
            return false;
        if (choiceIndex == 3)
        {
            OnHideEvent?.Invoke();
            _activeEvent = null;
            DestroyRuntimeObject(_pendingContractSelectionEvent);
            _pendingContractSelectionEvent = null;
            _pendingContractPrice = 0;
            OnShowShop?.Invoke();
            return true;
        }
        if (choiceIndex == (int)RunContractObjective.DefeatEliteThisMap && !CanReachEliteAfterActiveNode())
            return false;
        var cards = CardManager.Instance;
        if (cards == null || !cards.CanAfford(_pendingContractPrice) || !cards.SpendGold(_pendingContractPrice))
            return false;
        _usedSpecialShopOffers.Add(ShopOfferKind.Contract);
        _activeContract = new RunContractState
        {
            objective = (RunContractObjective)choiceIndex,
            startedAtMapIndex = CurrentMapIndex,
            startedAtNodeId = _activeNode.id,
            deadlineMapExclusive = (int)Math.Min(int.MaxValue, (long)CurrentMapIndex + 12L),
            stableId = $"quest:{RunRandomContext.NormalizeSeed(runSeed)}:{_activeNode.mapIndex}:{_activeNode.id}:{choiceIndex}"
        };
        OnHideEvent?.Invoke();
        _activeEvent = null;
        DestroyRuntimeObject(_pendingContractSelectionEvent);
        _pendingContractSelectionEvent = null;
        _pendingContractPrice = 0;
        CompleteActiveNode();
        ShowPathAfterNode();
        Debug.Log($"Quest Contract started: {_activeContract.ObjectiveDescription} Deadline: before Map {(long)_activeContract.deadlineMapExclusive + 1L}.", this);
        return true;
    }

    void ProgressContractForVictory(PathNode node, int actualHpLost)
    {
        var contract = _activeContract;
        if (contract == null || node == null) return;
        bool encounter = node.kind is MapNodeType.Combat or MapNodeType.Elite or MapNodeType.Boss;
        if (contract.objective == RunContractObjective.DefeatEliteThisMap &&
            node.kind == MapNodeType.Elite && node.mapIndex == contract.startedAtMapIndex &&
            (CombatManager.Instance?.EliteKillCount ?? 0) > 0)
        {
            ResolveContractSuccess();
            return;
        }
        if (contract.objective == RunContractObjective.WinCombatWithoutHpLoss && encounter && actualHpLost == 0)
        {
            ResolveContractSuccess();
            return;
        }
        if (contract.objective == RunContractObjective.WinTwoEncounters && encounter)
        {
            contract.encounterWins++;
            if (contract.encounterWins >= 2) ResolveContractSuccess();
        }
    }

    void ResolveContractSuccess()
    {
        var contract = _activeContract;
        if (contract == null) return;
        _activeContract = null;
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        switch (contract.objective)
        {
            case RunContractObjective.WinTwoEncounters:
                cards?.AddGold(6);
                break;
            case RunContractObjective.WinCombatWithoutHpLoss:
                cards?.AddGold(10);
                break;
            case RunContractObjective.DefeatEliteThisMap:
                var eligible = cards?.relicCatalog.Where(item => item != null &&
                        string.Equals(item.rarity, "Rare", StringComparison.OrdinalIgnoreCase) &&
                        !cards.HasArtifact(item) &&
                        (!item.OccupiesCapacitySlot || cards.ArtifactSlotsUsed < cards.ArtifactCapacity) &&
                        (item.tier <= 1 || cards.ownedArtifacts.Any(owned => owned != null &&
                            string.Equals(owned.canonicalId, item.upgradeFromId, StringComparison.Ordinal))) &&
                        string.IsNullOrEmpty(cards.GetArtifactAcquisitionUnavailableReason(item)))
                    .OrderBy(item => item.canonicalId, StringComparer.Ordinal)
                    .ThenBy(item => item.id, StringComparer.Ordinal).ToArray();
                if (eligible != null && eligible.Length > 0)
                {
                    int context = unchecked(contract.startedAtMapIndex * 7919 ^
                        contract.startedAtNodeId * 397 ^ (int)contract.objective * 31);
                    var random = (_randomContext ?? new RunRandomContext(runSeed)).CreateStream("quest-rare-reward", context);
                    if (!cards.GrantArtifactReward(eligible[random.NextInt(0, eligible.Length)]))
                    {
                        combat?.HealPlayer(5);
                        Debug.Log("Quest Contract reward could not be collected; healed 5 HP instead.", this);
                    }
                }
                else
                {
                    combat?.HealPlayer(5);
                    Debug.Log("Quest Contract reward: no Rare Artifact slot was available; healed 5 HP instead.", this);
                }
                break;
            default:
                break;
        }
        Debug.Log($"Quest Contract completed: {contract.objective}.", this);
    }

    void SettleContractAtMapExit()
    {
        var contract = _activeContract;
        if (contract == null) return;
        bool failed = contract.objective == RunContractObjective.DefeatEliteThisMap &&
                CurrentMapIndex >= contract.startedAtMapIndex ||
            (long)CurrentMapIndex + 1L >= contract.deadlineMapExclusive;
        if (!failed) return;
        _activeContract = null;
        var combat = CombatManager.Instance;
        int hp = combat != null ? combat.player.currentHealth : 0;
        int safeLoss = Math.Min(5, Math.Max(0, hp - 1));
        if (safeLoss > 0) combat.TakeRunDamage(safeLoss);
        Debug.Log($"Quest Contract failed ({contract.objective}); applied {safeLoss} nonlethal HP loss.", this);
    }

    public bool CanChooseEventOption(int choiceIndex)
    {
        return string.IsNullOrEmpty(GetEventOptionUnavailableReason(choiceIndex));
    }

    public string GetEventOptionUnavailableReason(int choiceIndex)
    {
        if (_activeEvent == null || _activeEvent.choices == null ||
            choiceIndex < 0 || choiceIndex >= _activeEvent.choices.Length)
            return "Choice unavailable";

        if (_activeEvent.id == "evt_100")
            return GetBloodRitualUnavailableReason(choiceIndex);
        if (_activeEvent.id.StartsWith("blood-ritual-replace:", StringComparison.Ordinal))
            return choiceIndex >= 0 && choiceIndex < _activeEvent.choices.Length
                ? string.Empty : "Choice unavailable";
        if (_activeEvent.id.StartsWith("quest-contract-select:", StringComparison.Ordinal))
        {
            if (choiceIndex == 3) return string.Empty;
            var cards = CardManager.Instance;
            if (cards == null || !cards.CanAfford(_pendingContractPrice))
                return $"Need {_pendingContractPrice}g to accept the Contract";
            return choiceIndex == (int)RunContractObjective.DefeatEliteThisMap && !CanReachEliteAfterActiveNode()
                ? "No reachable Elite remains on this map" : string.Empty;
        }
        var choice = _activeEvent.choices[choiceIndex];
        if (choice == null) return "Choice unavailable";
        if (IsHammerEvent())
            return choice.interaction is RunEventChoiceInteraction.HammerRetry or RunEventChoiceInteraction.HammerDecline
                ? string.Empty : choice.interaction == RunEventChoiceInteraction.ChooseEnhancementTarget
                    ? _pendingHammerEnhancements.Count == 3 && CardManager.Instance != null && CardManager.Instance.hand.Count > 0
                        ? string.Empty : "No cards in hand to target"
                    : "Choice unavailable";
        if (choice.interaction == RunEventChoiceInteraction.ChooseEnhancementTarget)
        {
            var cards = CardManager.Instance;
            if (cards == null || cards.hand.Count == 0) return "No cards in hand to target";
            return GetEventEnhancements().Any() ? string.Empty : "No enhancements available";
        }
        if (choice.interaction == RunEventChoiceInteraction.DiscardTwoForRandomEnhancement)
        {
            var cards = CardManager.Instance;
            if (cards == null || !cards.ownedCards.Any(card => card != null && card.Enhancement == null))
                return "No eligible cards to enhance";
            if (!GetEventEnhancements().Any()) return "No enhancements available";
            if (cards.hand.Count < 2) return "Need at least 2 cards in hand";
            return string.Empty;
        }

        if (choice.interaction == RunEventChoiceInteraction.DiscardCardsForTotalValue)
        {
            var cards = CardManager.Instance;
            if (cards == null) return "Choice unavailable";
            return HasHandCombinationForValue(cards, choice.requiredDiscardValue)
                ? string.Empty
                : $"No hand combination totals {choice.requiredDiscardValue}";
        }

        return RunEffectResolver.GetFailureReason(
            choice, CardManager.Instance, CombatManager.Instance, this);
    }

    public IReadOnlyList<CardEnhancementData> GetEventEnhancementsForChoice(int choiceIndex) =>
        IsHammerEvent() && IsActiveEventChoice(choiceIndex, RunEventChoiceInteraction.ChooseEnhancementTarget)
            ? _pendingHammerEnhancements
            : IsActiveEventChoice(choiceIndex, RunEventChoiceInteraction.ChooseEnhancementTarget)
                ? GetEventEnhancements()
                : Array.Empty<CardEnhancementData>();

    public bool ChooseEventEnhancementTarget(int choiceIndex, int cardId, CardEnhancementData enhancement,
        bool replaceExisting = false)
    {
        IReadOnlyList<CardEnhancementData> allowedEnhancements = IsHammerEvent()
            ? _pendingHammerEnhancements : GetEventEnhancements();
        if (GameplayInputGate.IsBlocked &&
                !(IsHammerEvent() && GameplayInputGate.Reasons == GameplayInputBlockReason.ArtifactChoice) ||
            !IsActiveEventChoice(choiceIndex, RunEventChoiceInteraction.ChooseEnhancementTarget) || enhancement == null ||
            !allowedEnhancements.Contains(enhancement) ||
            !string.IsNullOrEmpty(GetEnhancementAcquisitionTargetUnavailableReason(cardId, enhancement))) return false;

        if (!CardManager.Instance.ApplyEnhancement(cardId, enhancement, replaceExisting)) return false;
        if (IsHammerEvent()) CompleteHammerReward();
        else CompleteEventChoice();
        return true;
    }

    public bool ChooseEventDiscardAndReward(int choiceIndex, IReadOnlyList<int> cardIds)
    {
        var cards = CardManager.Instance;
        if (GameplayInputGate.IsBlocked || cards == null || !IsActiveEventChoice(choiceIndex,
                RunEventChoiceInteraction.DiscardTwoForRandomEnhancement) || cardIds == null ||
            cardIds.Count != 2 || cardIds[0] == cardIds[1] ||
            !string.IsNullOrEmpty(GetEventOptionUnavailableReason(choiceIndex))) return false;

        CardInstance target = null;
        foreach (var card in cards.ownedCards)
            if (card != null && card.Enhancement == null && (target == null || card.Id < target.Id))
                target = card;
        var enhancements = GetEventEnhancements();
        if (target == null || enhancements.Length == 0) return false;

        var random = CreateEventRandom(choiceIndex);
        var enhancement = enhancements[random.NextInt(0, enhancements.Length)];
        target = cards.ownedCards
            .Where(card => card != null && card.Enhancement == null)
            .OrderBy(card => card.Id)
            .ElementAt(random.NextInt(0, cards.ownedCards.Count(card => card != null && card.Enhancement == null)));

        if (!cards.TryDiscardCardsByIds(cardIds)) return false;
        if (!cards.ApplyEnhancement(target.Id, enhancement))
        {
            Debug.LogError("RunManager: random event enhancement could not be applied after validated discard.", this);
            return false;
        }
        CompleteEventChoice();
        return true;
    }

    static bool HasHandCombinationForValue(CardManager cards, int requiredValue)
    {
        if (cards == null || requiredValue <= 0) return false;
        var reachable = new bool[requiredValue + 1];
        reachable[0] = true;
        foreach (var card in cards.hand)
        {
            int value = card != null ? card.BaseAttackValue : 0;
            if (value <= 0 || value > requiredValue) continue;
            for (int total = requiredValue; total >= value; total--)
                if (reachable[total - value]) reachable[total] = true;
        }
        return reachable[requiredValue];
    }

    public bool CanChooseEventDiscardForTotalValue(int choiceIndex, IReadOnlyList<int> cardIds)
    {
        var cards = CardManager.Instance;
        if (!IsActiveEventChoice(choiceIndex, RunEventChoiceInteraction.DiscardCardsForTotalValue) ||
            cards == null || cardIds == null || cardIds.Count == 0 ||
            !string.IsNullOrEmpty(GetEventOptionUnavailableReason(choiceIndex))) return false;

        int requiredValue = _activeEvent.choices[choiceIndex].requiredDiscardValue;
        int total = 0;
        var unique = new HashSet<int>();
        for (int i = 0; i < cardIds.Count; i++)
        {
            int id = cardIds[i];
            if (!unique.Add(id)) return false;
            var card = cards.FindOwnedCard(id);
            if (card == null || !cards.hand.Contains(card)) return false;
            total += card.BaseAttackValue;
            if (total > requiredValue) return false;
        }
        return total == requiredValue;
    }

    public bool ChooseEventDiscardForTotalValue(int choiceIndex, IReadOnlyList<int> cardIds)
    {
        var cards = CardManager.Instance;
        if (GameplayInputGate.IsBlocked || cards == null ||
            !CanChooseEventDiscardForTotalValue(choiceIndex, cardIds) ||
            !cards.TryDiscardCardsByIds(cardIds)) return false;
        CompleteEventChoice();
        return true;
    }

    bool IsActiveEventChoice(int choiceIndex, RunEventChoiceInteraction interaction) =>
        _activeEvent != null && _activeEvent.choices != null && choiceIndex >= 0 &&
        choiceIndex < _activeEvent.choices.Length && _activeEvent.choices[choiceIndex] != null &&
        _activeEvent.choices[choiceIndex].interaction == interaction &&
        (IsHammerEvent() || _activeNode != null && !_activeNode.completed);

    internal bool TryGrantRandomSwampReward()
    {
        var cards = CardManager.Instance;
        if (cards == null) return false;
        var random = CreateEventRandom(0, "swamp-event-reward");
        int rewardType = random.NextInt(0, 3);
        if (rewardType == 0)
        {
            var artifacts = cards.relicCatalog
                .Where(value => value != null &&
                    string.IsNullOrEmpty(cards.GetArtifactAcquisitionUnavailableReason(value)))
                .OrderBy(value => value.canonicalId, StringComparer.Ordinal)
                .ThenBy(value => value.id, StringComparer.Ordinal).ToArray();
            if (artifacts.Length > 0)
                cards.BuyArtifact(artifacts[random.NextInt(0, artifacts.Length)], 0);
            return true;
        }

        if (rewardType == 1)
        {
            var enhancements = GetEventEnhancements();
            var eligibleCards = cards.ownedCards
                .Where(card => card != null && card.Enhancement == null)
                .OrderBy(card => card.Id).ToArray();
            if (enhancements.Length > 0 && eligibleCards.Length > 0)
            {
                var enhancement = enhancements[random.NextInt(0, enhancements.Length)];
                var card = eligibleCards[random.NextInt(0, eligibleCards.Length)];
                cards.ApplyEnhancement(card.Id, enhancement);
            }
        }
        return true;
    }

    CardEnhancementData[] GetEventEnhancements() => CardManager.Instance == null
        ? Array.Empty<CardEnhancementData>()
        : CardManager.Instance.enhancementCatalog.Where(value => value != null)
            .OrderBy(value => value.canonicalId, StringComparer.Ordinal)
            .ThenBy(value => value.id, StringComparer.Ordinal).ToArray();

    IRandomSource CreateEventRandom(int choiceIndex, string streamName = "run-event-enhancement")
    {
        if (_randomContext == null) _randomContext = new RunRandomContext(runSeed);
        int nodeId = _activeNode != null ? _activeNode.id : 0;
        int mapIndex = _activeNode != null ? _activeNode.mapIndex : 0;
        int context = unchecked(mapIndex * 7919 ^ nodeId * 397 ^ choiceIndex);
        return _randomContext.CreateStream(streamName, context);
    }

    bool IsHammerEvent() => _pendingHammerReward && _activeEvent != null &&
        ReferenceEquals(_activeEvent, _pendingHammerEvent);

    void TryOpenHammerReward()
    {
        var cards = CardManager.Instance;
        int hammerIndex = cards != null ? cards.ownedArtifacts.FindIndex(value => value != null &&
            value.specialRule == ArtifactSpecialRule.Hammer) : -1;
        var hammer = hammerIndex >= 0 ? cards.ownedArtifacts[hammerIndex] : null;
        if (cards == null || hammer == null) return;
        var instance = cards.GetArtifactInstanceAt(hammerIndex);
        if (!_pendingHammerReward)
        {
            int last = instance.State.GetCounter(-140);
            if (_completedNodeCount - last < 3) return;
            var common = cards.enhancementCatalog
                .Where(value => value != null && string.Equals(value.rarity, "Common", StringComparison.Ordinal))
                .OrderBy(value => value.canonicalId, StringComparer.Ordinal)
                .ThenBy(value => value.id, StringComparer.Ordinal).ToList();
            if (common.Count < 3) return;
            if (_randomContext == null) _randomContext = new RunRandomContext(runSeed);
            var random = _randomContext.CreateStream("hammer-common-enhancements", _completedNodeCount);
            _pendingHammerEnhancements.Clear();
            for (int i = 0; i < 3; i++)
            {
                int selected = random.NextInt(0, common.Count);
                _pendingHammerEnhancements.Add(common[selected]);
                common.RemoveAt(selected);
            }
            instance.State.SetCounter(-140, _completedNodeCount);
            _pendingHammerReward = true;
        }
        else if (!_hammerRetryPending) return;

        bool hasTarget = cards.hand.Count > 0;
        if (_hammerRetryPending && !hasTarget) return;
        _hammerRetryPending = false;
        _pendingHammerEvent = ScriptableObject.CreateInstance<RunEventDefinition>();
        _pendingHammerEvent.id = $"artifact-hammer:{_completedNodeCount}";
        _pendingHammerEvent.title = "HAMMER";
        _pendingHammerEvent.category = MapNodeType.Upgrade;
        _pendingHammerEvent.description = hasTarget
            ? "Choose one of three cached Common Enhancements, then target a card in your hand."
            : "No card is in hand to target. Retry after another node, or decline this reward.";
        _pendingHammerEvent.choices = hasTarget
            ? new[]
            {
                new RunEventChoice { label = "Choose Enhancement", description = "Select one of the three cached Common Enhancements, then click a card in your hand.", interaction = RunEventChoiceInteraction.ChooseEnhancementTarget }
            }
            : new[]
            {
                new RunEventChoice { label = "Retry Later", description = "Continue the run and retry when an eligible card is available.", interaction = RunEventChoiceInteraction.HammerRetry },
                new RunEventChoice { label = "Decline", description = "Leave the Hammer reward unused.", interaction = RunEventChoiceInteraction.HammerDecline }
            };
        _activeEvent = _pendingHammerEvent;
        GameplayInputGate.Set(GameplayInputBlockReason.ArtifactChoice, true);
        if (OnShowEvent == null)
        {
            GameplayInputGate.Set(GameplayInputBlockReason.ArtifactChoice, false);
            _activeEvent = null;
            DestroyRuntimeObject(_pendingHammerEvent);
            _pendingHammerEvent = null;
            _hammerRetryPending = true;
            return;
        }
        OnShowEvent.Invoke(_activeEvent);
    }

    bool FinishHammerEvent(bool retry)
    {
        if (!IsHammerEvent()) return false;
        OnHideEvent?.Invoke();
        _activeEvent = null;
        GameplayInputGate.Set(GameplayInputBlockReason.ArtifactChoice, false);
        DestroyRuntimeObject(_pendingHammerEvent);
        _pendingHammerEvent = null;
        if (retry)
            _hammerRetryPending = true;
        else
        {
            _pendingHammerReward = false;
            _hammerRetryPending = false;
            _pendingHammerEnhancements.Clear();
        }
        ShowPathAfterNode();
        return true;
    }

    public bool RetryHammerReward() => FinishHammerEvent(retry: true);
    public bool DeclineHammerReward() => FinishHammerEvent(retry: false);

    void CompleteHammerReward()
    {
        if (!IsHammerEvent()) return;
        FinishHammerEvent(retry: false);
    }

    void CompleteEventChoice()
    {
        OnHideEvent?.Invoke();
        _activeEvent = null;
        CompleteActiveNode();
        ShowPathAfterNode();
    }

    public bool ChooseEventOption(int choiceIndex)
    {
        if (GameplayInputGate.IsBlocked || !CanChooseEventOption(choiceIndex) ||
            _activeEvent.choices[choiceIndex].interaction != RunEventChoiceInteraction.Immediate) return false;
        if (_activeEvent.id == "evt_100")
            return ResolveBloodRitualOption(choiceIndex);
        if (_activeEvent.id.StartsWith("blood-ritual-replace:", StringComparison.Ordinal))
            return ResolveBloodRitualReplacement(choiceIndex);
        if (_activeEvent.id.StartsWith("quest-contract-select:", StringComparison.Ordinal))
            return ResolveContractSelection(choiceIndex);
        if (!RunEffectResolver.Apply(
                _activeEvent.choices[choiceIndex], CardManager.Instance, CombatManager.Instance, this))
            return false;

        CompleteEventChoice();
        return true;
    }

    public void OnShopDone()
    {
        if (GameplayInputGate.IsBlocked) return;
        if (_activeNode == null || _activeNode.kind != MapNodeType.Shop) return;
        OnHideShop?.Invoke();
        _activeShopOffers.Clear();
        CompleteActiveNode();
        ShowPathAfterNode();
    }

    void HandleEncounterResult(EncounterResult result)
    {
        if (result == EncounterResult.Defeat)
            Timing.StopRun();
        if (_activeNode == null) return;

        if (result != EncounterResult.Victory)
        {
            _activeNode = null;
            return;
        }

        bool wasBoss = _activeNode.kind == MapNodeType.Boss;
        ApplyVictoryArtifactRewards(wasBoss);
        ProgressContractForVictory(_activeNode, CombatManager.Instance?.EncounterActualHpLost ?? 0);
        TryGrantConsumableDropForNode(_activeNode);
        CompleteActiveNode();
        if (wasBoss)
            HandleBossVictory();
        else
            ShowPathAfterNode();
    }

    void ApplyVictoryArtifactRewards(bool wasBoss)
    {
        var cards = CardManager.Instance;
        if (cards == null) return;

        bool wasElite = !wasBoss && _activeNode != null && _activeNode.kind == MapNodeType.Elite &&
            (CombatManager.Instance?.EliteKillCount ?? 0) > 0;
        if (wasElite)
        {
            for (int artifactIndex = 0; artifactIndex < cards.ownedArtifacts.Count; artifactIndex++)
            {
                var artifact = cards.ownedArtifacts[artifactIndex];
                if (artifact == null) continue;
                var state = cards.GetArtifactInstanceAt(artifactIndex).State;
                if (artifact.specialRule == ArtifactSpecialRule.HuntersLedger)
                    state.SetCounter(-119, state.GetCounter(-119) + 1);
                if (artifact.specialRule == ArtifactSpecialRule.TrophyRack)
                    state.SetCounter(-137, state.GetCounter(-137) + 1);
            }
        }
        if (wasBoss)
        {
            for (int artifactIndex = 0; artifactIndex < cards.ownedArtifacts.Count; artifactIndex++)
            {
                var artifact = cards.ownedArtifacts[artifactIndex];
                if (artifact == null || artifact.specialRule != ArtifactSpecialRule.CrownOfEndurance) continue;
                int growth = 3 + ((CombatManager.Instance?.EncounterActualHpLost ?? 0) == 0 ? 3 : 0);
                CombatManager.Instance?.IncreasePlayerMaxHealth(growth, healIncrease: false);
            }
        }
        if (wasBoss && cards.HasArtifactSpecialRule(ArtifactSpecialRule.GoldenVault))
        {
            int payout = RoundPercentage(cards.gold, 10);
            if (payout > 0) cards.AddGold(payout);
        }
        if (!cards.HasArtifactSpecialRule(ArtifactSpecialRule.ScavengersSatchel)) return;
        _combatWinCount = IncrementToMaximum(_combatWinCount);
        if (_combatWinCount % 3 != 0) return;
        _satchelRewardCount = IncrementToMaximum(_satchelRewardCount);
        QueueRandomNonEnhancementReward(
            $"scavenger-satchel:{_satchelRewardCount}", "Scavenger’s Satchel",
            "scavenger-satchel", _satchelRewardCount);
    }

    internal bool TryGrantConsumableDropForNode(PathNode node)
    {
        var cards = CardManager.Instance;
        if (node == null || cards == null || _randomContext == null) return false;
        int context = unchecked(node.mapIndex * 7919 ^ node.id);
        if (!_consumableDropResolvedContexts.Add(context)) return false;
        var candidates = cards.consumableCatalog
            .Where(value => value != null && value.dropAvailable && value.dropChance > 0f &&
                value.effectType != ConsumableEffectType.ApplyEnhancement)
            .OrderBy(value => value.id, StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) return false;
        var random = _randomContext.CreateStream("consumable-drops", context);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (random.NextFloat() >= Mathf.Clamp01(candidates[i].dropChance)) continue;
            return QueueConsumableReward($"enemy-drop:{context}", "Enemy Drop", new[] { candidates[i] });
        }
        return false;
    }

    void ApplyPhase7MapCompletionEffects(PathNode node)
    {
        var cards = CardManager.Instance;
        if (cards == null || node == null) return;
        if (_visitedNodeTypesMapIndex != node.mapIndex)
        {
            _visitedNodeTypesCurrentMap.Clear();
            _visitedNodeTypesMapIndex = node.mapIndex;
        }
        _visitedNodeTypesCurrentMap.Add(node.kind);
        bool isCombatWin = node.kind is MapNodeType.Combat or MapNodeType.Elite or MapNodeType.Boss;
        var combat = CombatManager.Instance;
        for (int artifactIndex = 0; artifactIndex < cards.ownedArtifacts.Count; artifactIndex++)
        {
            var artifact = cards.ownedArtifacts[artifactIndex];
            if (artifact == null) continue;
            var state = cards.GetArtifactInstanceAt(artifactIndex).State;
            if (artifact.specialRule == ArtifactSpecialRule.WanderersBoots &&
                _visitedNodeTypesCurrentMap.Count >= 3 && state.GetCounter(-133) != node.mapIndex + 1)
            {
                state.SetCounter(-133, node.mapIndex + 1);
                combat?.HealPlayer(3);
                cards.AddGold(3);
            }
            if (artifact.specialRule == ArtifactSpecialRule.WarpathBanner)
                state.SetCounter(-1340, isCombatWin ? Math.Min(3, state.GetCounter(-1340) + 1) : 0);
        }
    }

    void CompleteActiveNode()
    {
        var resolvedNode = _activeNode;
        _activeNode = null;
        if (resolvedNode == null || resolvedNode.completed) return;

        resolvedNode.completed = true;
        _completedNodeCount = IncrementToMaximum(_completedNodeCount);
        ApplyPhase7MapCompletionEffects(resolvedNode);
        foreach (var nextId in resolvedNode.next)
        {
            var next = FindNode(nextId);
            if (next == null || next.completed) continue;
            next.accessible = true;
            if (!next.hidden) next.revealed = true;
        }
        GameplayEffectResolver.ApplyNodeCompleted(new NodeCompletedEffectContext(
            this, CardManager.Instance, resolvedNode, _completedNodeCount));
        RefreshCurrentMapEffects();
        TryOpenHammerReward();
    }

    void ShowPathAfterNode()
    {
        if (!IsRunCompleted && _activeEvent == null)
            OnShowPathScreen?.Invoke();
    }

    void HandleBossVictory()
    {
        var defeatedBoss = CurrentBoss;
        if (defeatedBoss == null)
        {
            Debug.LogError("RunManager: boss victory has no matching boss definition.", this);
            return;
        }

        var reward = CardManager.Instance.AddBossReward(defeatedBoss.sourceCard);
        OnBossRewardGranted?.Invoke(reward);

        SettleContractAtMapExit();
        if (IsInfiniteMode && bossIndex == int.MaxValue)
        {
            Timing.CompleteCurrentMap(false);
            Timing.StopRun();
            IsRunCompleted = true;
            OnRunCompleted?.Invoke();
            return;
        }
        bool finalMap = !IsInfiniteMode && bossIndex + 1 >= bossDeck.Count;
        Timing.CompleteCurrentMap(!finalMap);
        bossIndex++;
        if (!IsInfiniteMode && bossIndex >= bossDeck.Count)
        {
            Timing.StopRun();
            IsRunCompleted = true;
            OnRunCompleted?.Invoke();
            return;
        }

        NextCycle();
    }

    void SubscribeToCombatResults()
    {
        var combatManager = CombatManager.Instance;
        if (_subscribedCombatManager == combatManager) return;

        UnsubscribeFromCombatResults();
        _subscribedCombatManager = combatManager;
        if (_subscribedCombatManager != null)
            _subscribedCombatManager.OnEncounterResult += HandleEncounterResult;
    }

    void UnsubscribeFromCombatResults()
    {
        if (_subscribedCombatManager != null)
            _subscribedCombatManager.OnEncounterResult -= HandleEncounterResult;
        _subscribedCombatManager = null;
    }

    public bool ContinueInfiniteMode()
    {
        if (!CanContinueInfiniteMode || !Timing.ResumeAtNextMap()) return false;
        IsInfiniteMode = true;
        IsRunCompleted = false;
        NextCycle();
        return true;
    }

    public void AbandonRun()
    {
        Timing.StopRun();
    }

    public void Reset()
    {
        ResetRuntimeState();
        runSeed = string.Empty;
        _randomContext = null;
    }

    void ResetRuntimeState()
    {
        _activeNode = null;
        _activeEvent = null;
        _activeShopOffers.Clear();
        _activeUpgradeOffers.Clear();
        Timing.Reset();
        IsRunCompleted = false;
        IsInfiniteMode = false;
        GameplayInputGate.Set(GameplayInputBlockReason.ArtifactChoice, false);
        OnHideShop?.Invoke();
        OnHideEvent?.Invoke();
        OnHideUpgrade?.Invoke();
        GameplayInputGate.Set(GameplayInputBlockReason.ConsumableReward, false);
        GameplayInputGate.Set(GameplayInputBlockReason.ArtifactChoice, false);
        _pendingConsumableRewards.Clear();
        _pendingHammerReward = false;
        _hammerRetryPending = false;
        _pendingHammerEnhancements.Clear();
        if (_pendingHammerEvent != null) DestroyRuntimeObject(_pendingHammerEvent);
        _pendingHammerEvent = null;
        ClearPendingBloodRitual();
        OnConsumableRewardChanged?.Invoke();

        foreach (var ability in _generatedEnemyAbilities)
            if (ability != null) DestroyRuntimeObject(ability);
        _generatedEnemyAbilities.Clear();
        foreach (var enemy in _generatedEnemyTypes)
        {
            if (enemy == null) continue;
            var sourceCard = enemy.sourceCard;
            enemy.sourceCard = null;
            DestroyRuntimeObject(enemy);
            if (sourceCard != null)
                DestroyRuntimeObject(sourceCard);
        }

        _generatedEnemyTypes.Clear();
        bossDeck.Clear();
        bossIndex = 0;
        _completedNodeCount = 0;
        _pendingInvestmentStakes.Clear();
        _paidItemPurchaseCount = 0;
        _combatWinCount = 0;
        _merchantGiftRewardCount = 0;
        _satchelRewardCount = 0;
        _activeContract = null;
        _madeSuccessfulPurchaseInCurrentShop = false;
        if (_pendingContractSelectionEvent != null) DestroyRuntimeObject(_pendingContractSelectionEvent);
        _pendingContractSelectionEvent = null;
        _pendingContractPrice = 0;
        _usedSpecialShopOffers.Clear();
        _purchasedConsumablesInShop.Clear();
        _consumableDropResolvedContexts.Clear();
        _resolvedConsumableRewardContexts.Clear();
        _alchemistChoicesByMap.Clear();
        _visitedNodeTypesCurrentMap.Clear();
        _visitedNodeTypesMapIndex = -1;
        currentPath.Clear();
    }

    static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
