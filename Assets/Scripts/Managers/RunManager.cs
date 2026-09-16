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

    public int CurrentCycle => bossIndex + 1;
    public int BossTotal => bossDeck.Count;

    public void StartRun()
    {
        CardManager.Instance.Reset();
        CombatManager.Instance.Reset();
        CardManager.Instance.BuildDeck();
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
        return EnemyTypeData.Create($"{card.RankName} of {card.SuitName}", hp, atk, gold);
    }

    void NextCycle()
    {
        GeneratePath();
        OnCycleStarted?.Invoke($"Boss {CurrentCycle} / {BossTotal}");
        OnShowPathScreen?.Invoke();
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
        var node = FindNode(id);
        if (node == null || !node.accessible) return;

        node.completed = true;
        node.accessible = false;
        node.revealed = true;

        foreach (var n in currentPath)
            if (n.col == node.col && n.id != node.id)
                n.accessible = false;

        foreach (var nextId in node.next)
        {
            var next = FindNode(nextId);
            if (next != null) next.accessible = true;
        }

        OnHidePathScreen?.Invoke();
        StartEncounter(node);
    }

    void StartEncounter(PathNode node)
    {
        var enemy = node.type == "boss"
            ? new EnemyRuntime(bossDeck[bossIndex])
            : new EnemyRuntime(GetEnemyType(node.type));
        CombatManager.Instance.StartEnemy(enemy);
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
        "boss"   => $"Boss: {bossDeck[bossIndex].enemyName}",
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
        foreach (var n in currentPath)
        {
            if (n.type == "boss" && n.completed)
            {
                bossIndex++;
                if (bossIndex >= bossDeck.Count)
                {
                    OnRunCompleted?.Invoke();
                    return;
                }
                NextCycle();
                return;
            }
        }
        OnShowPathScreen?.Invoke();
    }

    public void Reset()
    {
        bossDeck.Clear();
        bossIndex = 0;
        currentPath.Clear();
    }
}
