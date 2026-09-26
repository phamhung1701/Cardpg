using System.IO;
using UnityEditor;
using UnityEngine;

public static class GameDataGenerator
{
    const string ArtifactDir = "Assets/Data/Relics";
    const string EnhancementDir = "Assets/Data/Enhancements";
    const string EnemyDir = "Assets/Data/Enemies";

    [MenuItem("Game/Generate Data Assets")]
    public static void GenerateAll()
    {
        GenerateArtifacts();
        GenerateEnhancements();
        GenerateEnemies();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Game data assets generated.");
    }

    [MenuItem("Game/Generate Artifact Assets")]
    public static void GenerateArtifacts()
    {
        EnsureFolder(ArtifactDir);

        var club = CreateArtifact("ClubPower", "club_power", "Club Emblem", "♣", "Club cards deal double damage", 15);
        ConfigureCoreArtifact(club, "rel_001", GameplayEffectKind.AttackMultiplier,
            GameplayEffectTrigger.AttackCalculated, 2, CardData.Suit.Clubs);

        var spade = CreateArtifact("SpadePower", "spade_power", "Spade Emblem", "♠", "Spade cards block double after card Enhancement block bonuses", 15);
        ConfigureCoreArtifact(spade, "rel_004", GameplayEffectKind.BlockMultiplier,
            GameplayEffectTrigger.DefenseCalculated, 2, CardData.Suit.Spades);

        var heart = CreateArtifact("HeartPower", "heart_power", "Heart Emblem", "♥", "Playing a Heart card heals 1 HP", 20);
        ConfigureCoreArtifact(heart, "rel_002", GameplayEffectKind.Heal,
            GameplayEffectTrigger.CardCommitted, 1, CardData.Suit.Hearts);

        var diamond = CreateArtifact("DiamondPower", "diamond_power", "Diamond Emblem", "♦", "Playing a Diamond card draws up to 2 cards", 20);
        ConfigureCoreArtifact(diamond, "rel_003", GameplayEffectKind.Draw,
            GameplayEffectTrigger.CardCommitted, 2, CardData.Suit.Diamonds);

        // Temporary prototype prices; the spreadsheet leaves prices blank.
        ConfigureTypedArtifact(CreateArtifact("LeatherArmor", "rel_005", "Leather Armor", "ARMOR",
            "Reduce each incoming combat damage instance by 3", 15), "rel_005", "Common",
            new GameplayEffectDefinition { kind = GameplayEffectKind.IncomingCombatDamageReduction,
                trigger = GameplayEffectTrigger.IncomingDamageCalculated, amount = 3 });
        ConfigureTypedArtifact(CreateArtifact("Dagger", "rel_006", "Dagger", "DAGGER",
            "Attacks gain +3 damage, then total action damage is capped at 10 before criticals", 15),
            "rel_006", "Common",
            new GameplayEffectDefinition { kind = GameplayEffectKind.FlatActionDamage,
                trigger = GameplayEffectTrigger.ActionDamageCalculated, amount = 3 },
            new GameplayEffectDefinition { kind = GameplayEffectKind.ActionDamageCap,
                trigger = GameplayEffectTrigger.ActionDamageCalculated, amount = 10 });
        ConfigureTypedArtifact(CreateArtifact("Tome", "rel_007", "Tome", "TOME",
            "Draw 1 additional card after the baseline encounter draw", 18), "rel_007", "Common",
            new GameplayEffectDefinition { kind = GameplayEffectKind.Draw,
                trigger = GameplayEffectTrigger.EncounterStart, amount = 1 });
        ConfigureTypedArtifact(CreateArtifact("Sword", "rel_023", "Sword", "SWORD",
            "Played attacking cards with qualifying attack above 8 gain +10 damage", 25),
            "rel_023", "Rare",
            new GameplayEffectDefinition { kind = GameplayEffectKind.FlatAttack,
                trigger = GameplayEffectTrigger.AttackCalculated, amount = 10,
                conditions = new[] { new GameplayEffectCondition
                { kind = GameplayConditionKind.AttackValueGreaterThan, value = 8 } } });

        var artisan = CreateArtifact("ArtisanTools", "artisan_tools", "Artisan Tools", "ENH", "Enhanced cards deal +3 damage", 25);
        artisan.requiresEnhancedCard = true;
        artisan.flatDamageBonus = 3;

        var sleeves = CreateArtifact("ReinforcedSleeves", "reinforced_sleeves", "Reinforced Sleeves", "BLOCK", "Enhanced cards block +3 damage", 22);
        sleeves.requiresEnhancedCard = true;
        sleeves.defenseBonus = 3;

        CreateArtifact("VictoryDraught", "victory_draught", "Victory Draught", "HEAL", "Heal 3 HP after each victory", 24).healAfterVictory = 3;
        CreateArtifact("GoldenCompass", "golden_compass", "Golden Compass", "GOLD", "Gain +4 gold after each victory", 20).bonusGold = 4;
        Debug.Log("Artifact assets created or updated.");
    }

    [MenuItem("Game/Generate Enhancement Assets")]
    public static void GenerateEnhancements()
    {
        EnsureFolder(EnhancementDir);
        ConfigureCoreEnhancement(CreateEnhancement("Sharpened", "sharpened", "Sharpened", "ATK", "+3 attack damage", 12),
            "enh_001", GameplayEffectKind.FlatAttack, GameplayEffectTrigger.AttackCalculated, 3);
        ConfigureCoreEnhancement(CreateEnhancement("Reinforced", "reinforced", "Hardened", "DEF", "+3 defensive block", 12),
            "enh_002", GameplayEffectKind.FlatBlock, GameplayEffectTrigger.DefenseCalculated, 3);
        ConfigureCoreEnhancement(CreateEnhancement("Mending", "mending", "Mending", "HP", "Heal 2 HP at each player-turn start while this card is in hand", 15),
            "enh_003", GameplayEffectKind.Heal, GameplayEffectTrigger.PlayerTurnStart, 2);
        ConfigureCoreEnhancement(CreateEnhancement("Quickdraw", "quickdraw", "Quickdraw", "DRAW", "Draw 1 card when played", 18),
            "enh_004", GameplayEffectKind.Draw, GameplayEffectTrigger.CardCommitted, 1);
        Debug.Log("Enhancement assets created or updated.");
    }

    [MenuItem("Game/Generate Enemy Assets")]
    public static void GenerateEnemies()
    {
        EnsureFolder(EnemyDir);
        CreateEnemy("Thief", "Thief", 12, 1, 15, fleeAfterPlayerTurns: 2);
        CreateEnemy("Goblin", "Goblin", 3, 2, 3, encounterCount: 3);
        CreateEnemy("Knight", "Knight", 25, 8, 10);
        Debug.Log("Enemy assets created.");
    }

    static RelicData CreateArtifact(string name, string id, string displayName, string icon, string description, int price)
    {
        string path = $"{ArtifactDir}/{name}.asset";
        var artifact = AssetDatabase.LoadAssetAtPath<RelicData>(path);
        if (artifact == null)
        {
            artifact = ScriptableObject.CreateInstance<RelicData>();
            AssetDatabase.CreateAsset(artifact, path);
        }

        artifact.id = id;
        artifact.displayName = displayName;
        artifact.icon = icon;
        artifact.description = description;
        artifact.price = price;
        artifact.restrictToSuit = false;
        artifact.requiresEnhancedCard = false;
        artifact.damageMultiplier = 1;
        artifact.flatDamageBonus = 0;
        artifact.defenseBonus = 0;
        artifact.reduceEnemyAttackByCardValue = false;
        artifact.recycleDiscardByCardValue = false;
        artifact.drawByCardValue = false;
        artifact.healAfterVictory = 0;
        artifact.bonusGold = 0;
        EditorUtility.SetDirty(artifact);
        return artifact;
    }

    static CardEnhancementData CreateEnhancement(string name, string id, string displayName, string icon, string description, int price)
    {
        string path = $"{EnhancementDir}/{name}.asset";
        var enhancement = AssetDatabase.LoadAssetAtPath<CardEnhancementData>(path);
        if (enhancement == null)
        {
            enhancement = ScriptableObject.CreateInstance<CardEnhancementData>();
            AssetDatabase.CreateAsset(enhancement, path);
        }

        enhancement.id = id;
        enhancement.displayName = displayName;
        enhancement.icon = icon;
        enhancement.description = description;
        enhancement.price = price;
        EditorUtility.SetDirty(enhancement);
        return enhancement;
    }

    static void ConfigureTypedArtifact(RelicData artifact, string canonicalId, string rarity,
        params GameplayEffectDefinition[] effects)
    {
        artifact.canonicalId = canonicalId;
        artifact.rarity = rarity;
        artifact.tier = 1;
        artifact.upgradeFromId = "";
        artifact.effects = effects;
        artifact.ruleModifiers = System.Array.Empty<GameplayRuleModifierData>();
        EditorUtility.SetDirty(artifact);
    }

    static void ConfigureCoreArtifact(RelicData artifact, string canonicalId,
        GameplayEffectKind kind, GameplayEffectTrigger trigger, int amount, CardData.Suit suit)
    {
        artifact.canonicalId = canonicalId;
        artifact.rarity = "Common";
        artifact.tier = 1;
        artifact.upgradeFromId = "";
        artifact.restrictToSuit = true;
        artifact.affectedSuit = suit;
        artifact.effects = new[] { new GameplayEffectDefinition
        {
            kind = kind, trigger = trigger, amount = amount,
            conditions = new[] { new GameplayEffectCondition
            {
                kind = GameplayConditionKind.CardSuit, suit = suit
            } }
        } };
        EditorUtility.SetDirty(artifact);
    }

    static void ConfigureCoreEnhancement(CardEnhancementData enhancement, string canonicalId,
        GameplayEffectKind kind, GameplayEffectTrigger trigger, int amount)
    {
        enhancement.canonicalId = canonicalId;
        enhancement.rarity = "Common";
        enhancement.tier = 1;
        enhancement.upgradeFromId = "";
        enhancement.attackBonus = 0;
        enhancement.defenseBonus = 0;
        enhancement.healOnPlay = 0;
        enhancement.drawOnPlay = 0;
        enhancement.effects = new[] { new GameplayEffectDefinition
        {
            kind = kind, trigger = trigger, amount = amount
        } };
        EditorUtility.SetDirty(enhancement);
    }

    static void CreateEnemy(
        string name,
        string displayName,
        int hp,
        int atk,
        int gold,
        int encounterCount = 1,
        int fleeAfterPlayerTurns = 0)
    {
        var path = $"{EnemyDir}/{name}.asset";
        if (File.Exists(path)) return;
        var enemy = ScriptableObject.CreateInstance<EnemyTypeData>();
        enemy.enemyName = displayName;
        enemy.maxHp = hp;
        enemy.baseAttack = atk;
        enemy.goldReward = gold;
        enemy.encounterCount = encounterCount;
        enemy.fleeAfterPlayerTurns = fleeAfterPlayerTurns;
        AssetDatabase.CreateAsset(enemy, path);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(parent));
        AssetDatabase.CreateFolder(parent, folder);
    }
}
