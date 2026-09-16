using System.IO;
using UnityEditor;
using UnityEngine;

public static class GameDataGenerator
{
    const string RelicDir = "Assets/Data/Relics";
    const string EnemyDir = "Assets/Data/Enemies";

    [MenuItem("Game/Generate Data Assets")]
    public static void GenerateAll()
    {
        GenerateRelics();
        GenerateEnemies();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Game data assets generated.");
    }

    [MenuItem("Game/Generate Relic Assets")]
    public static void GenerateRelics()
    {
        EnsureFolder(RelicDir);
        CreateRelic("ClubPower", "club_power", "Club Power", "♣", "♣ cards deal double damage", 15);
        CreateRelic("SpadePower", "spade_power", "Spade Power", "♠", "♠ cards reduce enemy ATK", 15);
        CreateRelic("HeartPower", "heart_power", "Heart Power", "♥", "♥ cards recycle discard to deck", 20);
        CreateRelic("DiamondPower", "diamond_power", "Diamond Power", "♦", "♦ cards draw extra cards", 20);
        Debug.Log("Relic assets created.");
    }

    [MenuItem("Game/Generate Enemy Assets")]
    public static void GenerateEnemies()
    {
        EnsureFolder(EnemyDir);
        CreateEnemy("Thief", "Thief", 10, 4, 5);
        CreateEnemy("Goblin", "Goblin", 15, 6, 7);
        CreateEnemy("Knight", "Knight", 25, 8, 10);
        Debug.Log("Enemy assets created.");
    }

    static void CreateRelic(string name, string id, string displayName, string icon, string desc, int price)
    {
        var path = $"{RelicDir}/{name}.asset";
        if (File.Exists(path)) return;
        var relic = ScriptableObject.CreateInstance<RelicData>();
        relic.id = id;
        relic.displayName = displayName;
        relic.icon = icon;
        relic.description = desc;
        relic.price = price;
        AssetDatabase.CreateAsset(relic, path);
    }

    static void CreateEnemy(string name, string displayName, int hp, int atk, int gold)
    {
        var path = $"{EnemyDir}/{name}.asset";
        if (File.Exists(path)) return;
        var enemy = ScriptableObject.CreateInstance<EnemyTypeData>();
        enemy.enemyName = displayName;
        enemy.maxHp = hp;
        enemy.baseAttack = atk;
        enemy.goldReward = gold;
        AssetDatabase.CreateAsset(enemy, path);
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(parent));
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
