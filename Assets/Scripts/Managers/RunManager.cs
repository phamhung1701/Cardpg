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

    readonly List<EnemyTypeData> _generatedEnemyTypes = new();
    readonly List<ShopOffer> _activeShopOffers = new();
    readonly List<ShopOffer> _activeUpgradeOffers = new();
    readonly HashSet<ShopOfferKind> _usedSpecialShopOffers = new();
    readonly HashSet<string> _purchasedConsumablesInShop = new(StringComparer.Ordinal);
    readonly HashSet<int> _consumableDropResolvedContexts = new();
    PathNode _activeNode;
    RunEventDefinition _activeEvent;
    CombatManager _subscribedCombatManager;
    RunRandomContext _randomContext;
    int _completedNodeCount;
    int _pendingInvestments;

    public RunTimingStatistics Timing { get; } = new();

    public int CurrentCycle => BossTotal > 0 ? Mathf.Min(bossIndex + 1, BossTotal) : 0;
    public int BossTotal => bossDeck.Count;
    public bool IsRunCompleted { get; private set; }
    public string RunSeed => runSeed;
    public int CurrentMapIndex => bossIndex;
    public int CompletedNodeCount => _completedNodeCount;
    public int PendingInvestmentCount => _pendingInvestments;
    public PathNode ActiveNode => _activeNode;
    public RunEventDefinition ActiveEvent => _activeEvent;
    public EnemyTypeData CurrentBoss => bossIndex >= 0 && bossIndex < bossDeck.Count
        ? bossDeck[bossIndex]
        : null;

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
        var (hp, atk, gold) = card.rank switch
        {
            CardData.Rank.Jack => (20, 10, 10),
            CardData.Rank.Queen => (30, 15, 15),
            _ => (40, 20, 20),
        };
        var boss = EnemyTypeData.Create($"{card.RankName} of {card.SuitName}", hp, atk, gold);
        boss.sourceCard = card;
        var ability = contentCatalog != null ? contentCatalog.GetBossAbility(card.suit) : null;
        boss.abilities = ability != null ? new[] { ability } : Array.Empty<EnemyAbility>();
        _generatedEnemyTypes.Add(boss);
        return boss;
    }

    void NextCycle()
    {
        GenerateAndAnnounceCycle();
        OnShowPathScreen?.Invoke();
    }

    void GenerateAndAnnounceCycle()
    {
        GeneratePath(CurrentMapIndex);
        OnCycleStarted?.Invoke($"Map {CurrentCycle} / {BossTotal}");
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
            MapNodeType.Elite => CreateEliteType(FindEnemy(node.contentId)),
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

        if (node.kind == MapNodeType.Combat && ShouldAddGoblinAlly(node, enemyType))
        {
            var goblin = GetNormalEnemies().FirstOrDefault(enemy => enemy != null && enemy.enemyName == "Goblin");
            if (goblin != null)
            {
                var allyStats = EnemyMapScaling.Scale(goblin.maxHp, goblin.baseAttack, node.mapIndex);
                enemies.Add(new EnemyRuntime(goblin, startingHp: allyStats.hp, startingAttack: allyStats.attack));
            }
        }

        CombatManager.Instance.StartEncounter(enemies);
    }

    bool ShouldAddGoblinAlly(PathNode node, EnemyTypeData enemyType)
    {
        if (enemyType.enemyName == "Shieldbearer") return true;
        if (enemyType.enemyName != "Knight" || node.mapIndex < 3) return false;
        var random = (_randomContext ?? new RunRandomContext(runSeed)).CreateStream(
            "knight-weak-ally", unchecked(node.mapIndex * 7919 ^ node.id));
        return random.NextInt(0, 2) == 0;
    }

    /// <summary>Preview and spawn use the same scaled base stats; elite bonuses are applied afterwards.</summary>
    public (int hp, int attack) GetEncounterStats(PathNode node)
    {
        if (node == null) return (0, 0);
        var source = node.kind == MapNodeType.Boss ? CurrentBoss : FindEnemy(node.contentId);
        if (source == null) return (0, 0);
        var stats = EnemyMapScaling.Scale(source.maxHp, source.baseAttack, node.mapIndex);
        return node.kind == MapNodeType.Elite
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
                ? $"Normal encounter • primary enemy {stats.hp} HP / {stats.attack} ATK"
                : "Normal encounter",
            MapNodeType.Elite => showStats
                ? $"Hard fight • better gold • {stats.hp} HP / {stats.attack} ATK"
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

    public IReadOnlyList<ShopOffer> GetCurrentShopOffers() => _activeShopOffers;
    public IReadOnlyList<ShopOffer> GetCurrentUpgradeOffers() => _activeUpgradeOffers;

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
        else if (offer.kind == ShopOfferKind.Enhancement)
        {
            if (offer.enhancement == null) return "Enhancement unavailable";
            if (!cards.ownedCards.Any(card => card != null && card.Enhancement == null))
                return "No eligible cards";
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
            if (offer.kind == ShopOfferKind.Guidance && !CanRevealEligibleHiddenNode())
                return "No eligible hidden node to reveal";
        }
        else return "Offer unavailable";

        return cards.CanAfford(offer.price)
            ? string.Empty
            : $"Need {offer.price}g (have {cards.gold}g)";
    }

    public string GetEnhancementTargetUnavailableReason(int cardId)
    {
        var card = CardManager.Instance != null ? CardManager.Instance.FindOwnedCard(cardId) : null;
        if (card == null) return "Target card unavailable";
        return card.Enhancement != null ? "Target already enhanced" : string.Empty;
    }

    // Artifact purchases need no target. Enhancement purchases require an explicit card ID.
    public bool PurchaseShopOffer(string stableId)
    {
        if (GameplayInputGate.IsBlocked || !CanPurchaseShopOffer(stableId)) return false;
        var offer = _activeShopOffers.First(candidate => candidate.StableId == stableId);
        var cards = CardManager.Instance;
        if (offer.kind == ShopOfferKind.Artifact) return cards.BuyArtifact(offer.artifact, offer.price);
        if (offer.kind == ShopOfferKind.Consumable)
        {
            if (!cards.BuyConsumable(offer.consumable, offer.price)) return false;
            _purchasedConsumablesInShop.Add(offer.consumable.id);
            return true;
        }
        if (offer.kind == ShopOfferKind.Investment)
        {
            if (!cards.SpendGold(offer.price)) return false;
            _pendingInvestments++;
            _usedSpecialShopOffers.Add(offer.kind);
            return true;
        }
        if (offer.kind == ShopOfferKind.Guidance)
        {
            if (!CanRevealEligibleHiddenNode() || !cards.SpendGold(offer.price)) return false;
            if (!TryRevealHiddenNodeFromEvent())
            {
                cards.AddGold(offer.price);
                return false;
            }
            _usedSpecialShopOffers.Add(offer.kind);
            return true;
        }
        return false;
    }

    public bool PurchaseShopEnhancement(string stableId, int cardId)
    {
        if (GameplayInputGate.IsBlocked || !CanPurchaseShopOffer(stableId) ||
            !string.IsNullOrEmpty(GetEnhancementTargetUnavailableReason(cardId))) return false;
        var offer = _activeShopOffers.First(candidate => candidate.StableId == stableId);
        return offer.kind == ShopOfferKind.Enhancement &&
            CardManager.Instance.BuyEnhancement(cardId, offer.enhancement, offer.price);
    }

    // No implicit target may commit an Upgrade reward.
    public bool ChooseUpgradeOffer(int offerIndex) => false;

    public bool ChooseUpgradeOffer(int offerIndex, int cardId)
    {
        if (GameplayInputGate.IsBlocked) return false;
        if (_activeNode == null || _activeNode.completed || _activeNode.kind != MapNodeType.Upgrade ||
            offerIndex < 0 || offerIndex >= _activeUpgradeOffers.Count ||
            !string.IsNullOrEmpty(GetEnhancementTargetUnavailableReason(cardId)))
            return false;

        var offer = _activeUpgradeOffers[offerIndex];
        if (offer.kind != ShopOfferKind.Enhancement || offer.enhancement == null ||
            !CardManager.Instance.ApplyEnhancement(cardId, offer.enhancement))
            return false;

        OnHideUpgrade?.Invoke();
        _activeUpgradeOffers.Clear();
        CompleteActiveNode();
        ShowPathAfterNode();
        return true;
    }

    void ResolvePendingInvestments(PathNode shopNode)
    {
        if (_pendingInvestments <= 0 || shopNode == null || CardManager.Instance == null) return;
        if (_randomContext == null) _randomContext = new RunRandomContext(runSeed);
        int context = unchecked(shopNode.mapIndex * 7919 ^ shopNode.id);
        var random = _randomContext.CreateStream("shop-investment", context);
        int successfulReturns = 0;
        for (int i = 0; i < _pendingInvestments; i++)
            if (random.NextInt(0, 2) == 0) successfulReturns++;
        _pendingInvestments = 0;
        if (successfulReturns > 0) CardManager.Instance.AddGold(successfulReturns * 15);
    }

    void PrepareShopOffers(int maximumOffers = 4)
    {
        _activeShopOffers.Clear();
        _usedSpecialShopOffers.Clear();
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
            .Where(artifact => artifact != null && !cards.HasArtifact(artifact) &&
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
                price = artifact.price
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
                price = artifact.price
            });
        }
        AddConsumableOffers(cards, context);
        AddSpecialShopOffers();
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
            price = consumable.price
        });
    }

    void AddSpecialShopOffers()
    {
        _activeShopOffers.Add(new ShopOffer { kind = ShopOfferKind.Investment, price = 5 });
        _activeShopOffers.Add(new ShopOffer { kind = ShopOfferKind.Guidance, price = 5 });
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void PrepareAllDevShopOffers(CardManager cards)
    {
        foreach (var artifact in cards.relicCatalog
            .Where(value => value != null && !cards.HasArtifact(value) &&
                string.IsNullOrEmpty(cards.GetArtifactAcquisitionUnavailableReason(value)))
            .OrderBy(value => value.id, StringComparer.Ordinal))
        {
            _activeShopOffers.Add(new ShopOffer
            {
                kind = ShopOfferKind.Artifact,
                artifact = artifact,
                price = artifact.price
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
                price = enhancement.price
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
                price = free ? 0 : enhancement.price
            });
        }
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

        var choice = _activeEvent.choices[choiceIndex];
        if (choice == null) return "Choice unavailable";
        if (choice.interaction == RunEventChoiceInteraction.ChooseEnhancementTarget ||
            choice.interaction == RunEventChoiceInteraction.DiscardTwoForRandomEnhancement)
        {
            var cards = CardManager.Instance;
            if (cards == null || !cards.ownedCards.Any(card => card != null && card.Enhancement == null))
                return "No eligible cards to enhance";
            if (!GetEventEnhancements().Any()) return "No enhancements available";
            if (choice.interaction == RunEventChoiceInteraction.DiscardTwoForRandomEnhancement &&
                cards.hand.Count < 2)
                return "Need at least 2 cards in hand";
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
        IsActiveEventChoice(choiceIndex, RunEventChoiceInteraction.ChooseEnhancementTarget)
            ? GetEventEnhancements()
            : Array.Empty<CardEnhancementData>();

    public bool ChooseEventEnhancementTarget(int choiceIndex, int cardId, CardEnhancementData enhancement)
    {
        if (GameplayInputGate.IsBlocked || !IsActiveEventChoice(choiceIndex,
                RunEventChoiceInteraction.ChooseEnhancementTarget) || enhancement == null ||
            !GetEventEnhancements().Contains(enhancement) ||
            !string.IsNullOrEmpty(GetEnhancementTargetUnavailableReason(cardId))) return false;

        if (!CardManager.Instance.ApplyEnhancement(cardId, enhancement)) return false;
        CompleteEventChoice();
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
        _activeNode != null && !_activeNode.completed;

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
        TryGrantConsumableDropForNode(_activeNode);
        CompleteActiveNode();
        if (wasBoss)
            HandleBossVictory();
        else
            ShowPathAfterNode();
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
            return cards.AddConsumable(candidates[i]);
        }
        return false;
    }

    void CompleteActiveNode()
    {
        var resolvedNode = _activeNode;
        _activeNode = null;
        if (resolvedNode == null || resolvedNode.completed) return;

        resolvedNode.completed = true;
        _completedNodeCount++;
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
    }

    void ShowPathAfterNode()
    {
        if (!IsRunCompleted)
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

        bool finalMap = bossIndex + 1 >= bossDeck.Count;
        Timing.CompleteCurrentMap(!finalMap);
        bossIndex++;
        if (bossIndex >= bossDeck.Count)
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
        OnHideShop?.Invoke();
        OnHideEvent?.Invoke();
        OnHideUpgrade?.Invoke();

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
        _pendingInvestments = 0;
        _usedSpecialShopOffers.Clear();
        _purchasedConsumablesInShop.Clear();
        _consumableDropResolvedContexts.Clear();
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
