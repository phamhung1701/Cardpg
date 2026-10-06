using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RunMapGenerator
{
    public const int RowCount = 3;
    public const int RouteColumnCount = 5;

    public static List<PathNode> Generate(
        RunRandomContext randomContext,
        int mapIndex,
        RunContentCatalog catalog,
        EnemyTypeData[] fallbackEnemies = null)
    {
        var layoutRandom = randomContext.CreateStream("map-layout", mapIndex);
        var contentRandom = randomContext.CreateStream("map-content", mapIndex);
        var nodes = new List<PathNode>();
        int nextId = 0;

        var columns = new List<PathNode>[RouteColumnCount];
        for (int col = 0; col < RouteColumnCount; col++)
        {
            columns[col] = new List<PathNode>(RowCount);
            var kinds = GetColumnKinds(col);
            kinds.Shuffle(layoutRandom);

            for (int row = 0; row < RowCount; row++)
            {
                MapNodeType kind = kinds[row];
                // Map 1 reserves its first two route depths for non-Shop encounters.
                // Replace after the layout shuffle so link/layout RNG consumption stays unchanged.
                if (mapIndex == 0 && col < 2 && kind == MapNodeType.Shop)
                    kind = MapNodeType.Combat;
                // The first two maps are the onboarding difficulty window and contain no Elites.
                if (mapIndex < 2 && kind == MapNodeType.Elite)
                    kind = MapNodeType.Combat;
                bool hidden = kind == MapNodeType.Risk ||
                    (kind == MapNodeType.Event && contentRandom.NextInt(0, 2) == 0);
                int nodeId = nextId++;
                var node = new PathNode
                {
                    id = nodeId,
                    type = ToLegacyType(kind),
                    kind = kind,
                    col = col,
                    row = row,
                    accessible = col == 0,
                    revealed = kind == MapNodeType.Elite || (col == 0 && !hidden),
                    hidden = hidden,
                    completed = false,
                    mapIndex = mapIndex,
                    contentId = PickContentId(kind, mapIndex, catalog, fallbackEnemies, contentRandom, randomContext, nodeId)
                };
                columns[col].Add(node);
                nodes.Add(node);
            }
        }

        for (int col = 0; col < RouteColumnCount - 1; col++)
        {
            for (int row = 0; row < RowCount; row++)
            {
                var node = columns[col][row];
                AddUnique(node.next, columns[col + 1][row].id);

                // Keep branch links monotone: outer lanes may merge inward, never cross in opposite directions.
                int inwardLane = row == 0 ? 1 : row == RowCount - 1 ? RowCount - 2 : row;
                if (inwardLane != row)
                    AddUnique(node.next, columns[col + 1][inwardLane].id);
            }
        }

        var boss = new PathNode
        {
            id = nextId,
            type = "boss",
            kind = MapNodeType.Boss,
            col = RouteColumnCount,
            row = 1,
            accessible = false,
            revealed = true,
            hidden = false,
            completed = false,
            mapIndex = mapIndex,
            contentId = "boss"
        };
        foreach (var node in columns[^1])
            AddUnique(node.next, boss.id);
        nodes.Add(boss);

        ApplyEliteRoutePolicy(randomContext, mapIndex, catalog, fallbackEnemies, columns);

        return nodes;
    }

    static void ApplyEliteRoutePolicy(
        RunRandomContext randomContext,
        int mapIndex,
        RunContentCatalog catalog,
        EnemyTypeData[] fallbackEnemies,
        List<PathNode>[] columns)
    {
        var eliteNodes = columns.SelectMany(column => column)
            .Where(node => node.kind == MapNodeType.Elite)
            .ToArray();
        var presenceRandom = randomContext.CreateStream("map-elite-presence", mapIndex);
        var eliteContentRandom = randomContext.CreateStream("map-elite-content", mapIndex);
        EnemyTypeData[] authoredElites = catalog != null ? catalog.eliteEnemies : null;
        var eligibleElites = authoredElites == null
            ? new List<EnemyTypeData>()
            : authoredElites.Where(enemy => enemy != null).ToList();

        foreach (var elite in eliteNodes)
        {
            if (presenceRandom.NextInt(0, 2) == 0)
            {
                elite.kind = MapNodeType.Combat;
                elite.type = ToLegacyType(MapNodeType.Combat);
                elite.contentId = PickContentId(MapNodeType.Combat, mapIndex, catalog, fallbackEnemies,
                    randomContext.CreateStream("map-elite-fallback", unchecked(mapIndex * 31 + elite.id)),
                    randomContext, elite.id);
                elite.revealed = elite.col == 0 && !elite.hidden;
            }
            else if (eligibleElites.Count > 0)
            {
                elite.contentId = eligibleElites[eliteContentRandom.NextInt(0, eligibleElites.Count)].name;
            }
        }

        // An Elite must never be the only selectable destination from its predecessor.
        for (int col = 0; col < RouteColumnCount - 1; col++)
        {
            foreach (var predecessor in columns[col])
            {
                bool hasEliteSuccessor = predecessor.next.Any(id =>
                    columns[col + 1].Any(node => node.id == id && node.kind == MapNodeType.Elite));
                bool hasNonEliteSuccessor = predecessor.next.Any(id =>
                    columns[col + 1].Any(node => node.id == id && node.kind != MapNodeType.Elite));
                if (!hasEliteSuccessor || hasNonEliteSuccessor) continue;

                var alternative = columns[col + 1].Where(node => node.kind != MapNodeType.Elite)
                    .OrderBy(node => Mathf.Abs(node.row - predecessor.row))
                    .ThenBy(node => node.row).ThenBy(node => node.id).FirstOrDefault();
                if (alternative != null) AddUnique(predecessor.next, alternative.id);
            }
        }
    }

    static List<MapNodeType> GetColumnKinds(int column) => column switch
    {
        0 => new() { MapNodeType.Combat, MapNodeType.Combat, MapNodeType.Combat },
        1 => new() { MapNodeType.Event, MapNodeType.Combat, MapNodeType.Shop },
        2 => new() { MapNodeType.Elite, MapNodeType.Combat, MapNodeType.Risk },
        3 => new() { MapNodeType.Upgrade, MapNodeType.Event, MapNodeType.Combat },
        _ => new() { MapNodeType.Elite, MapNodeType.Combat, MapNodeType.Shop }
    };

    static string PickContentId(
        MapNodeType kind,
        int mapIndex,
        RunContentCatalog catalog,
        EnemyTypeData[] fallbackEnemies,
        IRandomSource random,
        RunRandomContext randomContext,
        int nodeId)
    {
        if (kind == MapNodeType.Combat || kind == MapNodeType.Elite)
        {
            var enemies = catalog != null && catalog.normalEnemies != null && catalog.normalEnemies.Length > 0
                ? catalog.normalEnemies
                : fallbackEnemies;
            if (enemies == null || enemies.Length == 0) return string.Empty;
            var eligible = new List<EnemyTypeData>(enemies.Length);
            foreach (var enemy in enemies)
                if (enemy != null && (mapIndex >= 2 || enemy.enemyName != "Knight") &&
                    (kind != MapNodeType.Elite ||
                        (enemy.enemyName != "Shieldbearer" && enemy.enemyName != "Brute")))
                    eligible.Add(enemy);
            if (eligible.Count == 0) return string.Empty;
            return eligible[random.NextInt(0, eligible.Count)].name;
        }

        if (kind == MapNodeType.Event || kind == MapNodeType.Upgrade || kind == MapNodeType.Risk)
        {
            var events = catalog != null ? catalog.GetEvents(kind) : null;
            if (events == null || events.Length == 0) return string.Empty;
            if (kind == MapNodeType.Risk)
            {
                var ritual = events.FirstOrDefault(value => value != null && value.id == "evt_100");
                if (ritual != null && randomContext != null)
                {
                    int context = unchecked(mapIndex * 7919 ^ nodeId);
                    if (randomContext.CreateStream("blood-ritual-appearance", context).NextInt(0, 20) == 0)
                        return ritual.id;
                    events = events.Where(value => value != null && value.id != "evt_100").ToArray();
                }
            }
            if (events.Length == 0) return string.Empty;
            var definition = events[random.NextInt(0, events.Length)];
            return definition != null ? definition.id : string.Empty;
        }

        return kind.ToString().ToLowerInvariant();
    }

    static string ToLegacyType(MapNodeType kind) => kind.ToString().ToLowerInvariant();

    static void AddUnique(List<int> values, int value)
    {
        if (!values.Contains(value)) values.Add(value);
    }
}
