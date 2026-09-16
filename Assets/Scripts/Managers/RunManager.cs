using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PathNode
{
    public int id;
    public string type;
    public float col, row;
    public List<int> next = new();
    public bool completed, accessible, revealed;
}

public class RunManager : Singleton<RunManager>
{
    [Header("Enemy Type Assets")]
    public EnemyTypeData thiefType;
    public EnemyTypeData goblinType;
    public EnemyTypeData knightType;

    [Header("Runtime State")]
    public List<EnemyTypeData> bossDeck = new();
    public int bossIndex;
    public List<PathNode> currentPath = new();

    public event Action OnShowPathScreen;
    public event Action OnHidePathScreen;
    public event Action OnRunCompleted;
    public event Action<string> OnCycleStarted;
    public event Action<CardInstance> OnBossRewardGranted;

    PathNode _activeNode;
    CombatManager _subscribedCombatManager;

    public int CurrentCycle => BossTotal > 0 ? Mathf.Min(bossIndex + 1, BossTotal) : 0;
    public int BossTotal => bossDeck.Count;
    public bool IsRunCompleted { get; private set; }
    public PathNode ActiveNode => _activeNode;
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

    public void StartRun()
    {
        if (thiefType == null || goblinType == null || knightType == null ||
            CardManager.Instance == null || CombatManager.Instance == null)
        {
            Debug.LogError("RunManager: run configuration is incomplete.", this);
            return;
        }

        SubscribeToCombatResults();
        CombatManager.Instance.Reset();
        CardManager.Instance.Reset();
        Reset();
        CardManager.Instance.BuildDeck();
        CardManager.Instance.DealHand();
        BuildBossDeck();
        NextCycle();
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
            rankBosses.Shuffle();
            bossDeck.AddRange(rankBosses);
        }
    }

    EnemyTypeData CreateBossType(CardData card)
    {
        var (hp, atk, gold) = card.rank switch
        {
            CardData.Rank.Jack  => (20, 10, 10),
            CardData.Rank.Queen => (30, 15, 15),
            _                    => (40, 20, 20),
        };
        var boss = EnemyTypeData.Create($"{card.RankName} of {card.SuitName}", hp, atk, gold);
        boss.sourceCard = card;
        return boss;
    }

    void NextCycle()
    {
        GenerateAndAnnounceCycle();
        OnShowPathScreen?.Invoke();
    }

    void GenerateAndAnnounceCycle()
    {
        GeneratePath();
        OnCycleStarted?.Invoke($"Boss {CurrentCycle} / {BossTotal}");
    }

    void GeneratePath()
    {
        int routeCount = UnityEngine.Random.Range(3, 5);
        var lengths = new List<int>();
        for (int i = 0; i < routeCount; i++) lengths.Add(i + 1);
        lengths.Shuffle();

        currentPath.Clear();
        int nextId = 0;

        float bossRow = (routeCount - 1) / 2f;
        var boss = MakeNode(nextId++, "boss", routeCount, bossRow, accessible: false);
        currentPath.Add(boss);

        for (int lane = 0; lane < routeCount; lane++)
        {
            PathNode prev = null;
            for (int step = 0; step < lengths[lane]; step++)
            {
                var node = MakeNode(nextId++, RandomRegularType(), step, lane, accessible: step == 0);
                if (prev != null) prev.next.Add(node.id);
                prev = node;
                currentPath.Add(node);
            }
            prev.next.Add(boss.id);
        }
    }

    PathNode MakeNode(int id, string type, float col, float row, bool accessible)
    {
        return new PathNode
        {
            id = id,
            type = type,
            col = col,
            row = row,
            accessible = accessible,
            revealed = false,
            completed = false
        };
    }

    string RandomRegularType()
    {
        string[] types = { "thief", "goblin", "knight" };
        return types[UnityEngine.Random.Range(0, types.Length)];
    }

    public void OnPathChosen(int id)
    {
        if (_activeNode != null || IsRunCompleted) return;

        var node = FindNode(id);
        if (node == null || !node.accessible || node.completed) return;

        node.accessible = false;
        node.revealed = true;

        foreach (var n in currentPath)
            if (n.col == node.col && n.id != node.id)
                n.accessible = false;

        _activeNode = node;
        OnHidePathScreen?.Invoke();
        StartEncounter(node);
    }

    void StartEncounter(PathNode node)
    {
        var enemyType = node.type == "boss" ? CurrentBoss : GetEnemyType(node.type);
        if (enemyType == null)
        {
            Debug.LogError("RunManager: cannot start encounter without an enemy definition.", this);
            _activeNode = null;
            return;
        }

        CombatManager.Instance.StartEnemy(new EnemyRuntime(enemyType));
    }

    EnemyTypeData GetEnemyType(string type) => type switch
    {
        "thief"  => thiefType,
        "goblin" => goblinType,
        _        => knightType,
    };

    PathNode FindNode(int id)
    {
        foreach (var n in currentPath)
            if (n.id == id) return n;
        return null;
    }

    public string GetNodeLabel(PathNode node) => node.type switch
    {
        "thief"  => "Thief",
        "goblin" => "Goblin",
        "knight" => "Knight",
        "boss"   => CurrentBoss != null ? $"Boss: {CurrentBoss.enemyName}" : "Boss",
        _        => "???"
    };

    public string GetNodeDesc(PathNode node) => node.type switch
    {
        "thief"  => "Easy fight",
        "goblin" => "Medium fight",
        "knight" => "Hard fight",
        "boss"   => "Boss fight!",
        _        => "???"
    };

    public void OnShopDone()
    {
        if (!IsRunCompleted)
            OnShowPathScreen?.Invoke();
    }

    void HandleEncounterResult(EncounterResult result)
    {
        if (_activeNode == null) return;

        var resolvedNode = _activeNode;
        _activeNode = null;

        if (result != EncounterResult.Victory || resolvedNode.completed)
            return;

        resolvedNode.completed = true;
        foreach (var nextId in resolvedNode.next)
        {
            var next = FindNode(nextId);
            if (next != null)
                next.accessible = true;
        }

        if (resolvedNode.type == "boss")
            HandleBossVictory();
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

        bossIndex++;
        if (bossIndex >= bossDeck.Count)
        {
            IsRunCompleted = true;
            OnRunCompleted?.Invoke();
            return;
        }

        GenerateAndAnnounceCycle();
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

    public void Reset()
    {
        _activeNode = null;
        IsRunCompleted = false;

        foreach (var boss in bossDeck)
        {
            if (boss == null) continue;

            var sourceCard = boss.sourceCard;
            boss.sourceCard = null;
            DestroyRuntimeObject(boss);
            if (sourceCard != null)
                DestroyRuntimeObject(sourceCard);
        }

        bossDeck.Clear();
        bossIndex = 0;
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
