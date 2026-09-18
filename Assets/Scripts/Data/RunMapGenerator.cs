using System.Collections.Generic;
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
                bool hidden = kind == MapNodeType.Risk ||
                    (kind == MapNodeType.Event && contentRandom.NextInt(0, 2) == 0);
                var node = new PathNode
                {
                    id = nextId++,
                    type = ToLegacyType(kind),
                    kind = kind,
                    col = col,
                    row = row,
                    accessible = col == 0,
                    revealed = col == 0 && !hidden,
                    hidden = hidden,
                    completed = false,
                    mapIndex = mapIndex,
                    contentId = PickContentId(kind, catalog, fallbackEnemies, contentRandom)
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

                int diagonal = layoutRandom.NextInt(0, 2) == 0 ? row - 1 : row + 1;
                if (diagonal >= 0 && diagonal < RowCount)
                    AddUnique(node.next, columns[col + 1][diagonal].id);
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

        return nodes;
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
        RunContentCatalog catalog,
        EnemyTypeData[] fallbackEnemies,
        IRandomSource random)
    {
        if (kind == MapNodeType.Combat || kind == MapNodeType.Elite)
        {
            var enemies = catalog != null && catalog.normalEnemies != null && catalog.normalEnemies.Length > 0
                ? catalog.normalEnemies
                : fallbackEnemies;
            if (enemies == null || enemies.Length == 0) return string.Empty;
            var enemy = enemies[random.NextInt(0, enemies.Length)];
            return enemy != null ? enemy.name : string.Empty;
        }

        if (kind == MapNodeType.Event || kind == MapNodeType.Upgrade || kind == MapNodeType.Risk)
        {
            var events = catalog != null ? catalog.GetEvents(kind) : null;
            if (events == null || events.Length == 0) return string.Empty;
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
