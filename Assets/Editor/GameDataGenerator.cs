using System.IO;
using System.Linq;
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

        ConfigureSpecialArtifact(CreateArtifact("TravelerPackCommon", "rel_120", "Traveler’s Pack", "PACK",
            "Increase backpack capacity from 3 slots to 5. Consumables remain non-stacking.", 20),
            "rel_120", "Common", 1, "", ArtifactSpecialRule.TravelerPackCommon);
        ConfigureSpecialArtifact(CreateArtifact("TravelerPackRare", "rel_121", "Traveler’s Pack", "PACK+",
            "Keep 5 backpack slots and stack up to 3 identical consumables per slot. Individual charges are preserved and partially used items are consumed first.", 30),
            "rel_121", "Rare", 2, "rel_120", ArtifactSpecialRule.TravelerPackRare);
        ConfigureSpecialArtifact(CreateArtifact("PreparationManual", "rel_123", "Preparation Manual", "BLOCK",
            "Start each encounter with 2 Block per distinct consumable type carried. Block is consumed before Shield and expires after the encounter.", 20),
            "rel_123", "Common", 1, "", ArtifactSpecialRule.PreparationManual);
        ConfigureSpecialArtifact(CreateArtifact("ScavengersPouch", "rel_117", "Scavenger’s Pouch", "DRAW",
            "The first successful consumable use each encounter draws 1 card.", 20),
            "rel_117", "Common", 1, "", ArtifactSpecialRule.ScavengersPouch);
        ConfigureSpecialArtifact(CreateArtifact("ScavengersSatchel", "rel_129", "Scavenger’s Satchel", "LOOT",
            "Every 3 Combat, Elite, or Boss victories offers 1 random non-enhancement consumable to claim, replace, or decline.", 30),
            "rel_129", "Rare", 1, "", ArtifactSpecialRule.ScavengersSatchel);
        ConfigureSpecialArtifact(CreateArtifact("CashbackToken", "rel_125", "Cashback Token", "15%",
            "Refund 15% of actual Gold spent on paid Shop items, rounded to nearest with ties up. Free purchases and this Token’s own purchase grant no refund.", 20),
            "rel_125", "Common", 1, "", ArtifactSpecialRule.CashbackToken);
        ConfigureSpecialArtifact(CreateArtifact("GoldenVault", "rel_127", "Golden Vault", "VAULT",
            "After Boss Gold rewards settle, gain 10% of the resulting unspent Gold, rounded to nearest with ties up.", 30),
            "rel_127", "Rare", 1, "", ArtifactSpecialRule.GoldenVault);
        ConfigureSpecialArtifact(CreateArtifact("MerchantsBadge", "rel_124", "Merchant’s Badge", "SALE",
            "Paid Artifact, Enhancement, and Consumable Shop items cost 20% less, rounded to nearest with ties up.", 20),
            "rel_124", "Common", 1, "", ArtifactSpecialRule.MerchantsBadge);
        ConfigureSpecialArtifact(CreateArtifact("MerchantsGift", "rel_131", "Merchant’s Gift", "GIFT",
            "Every 3 paid Shop item purchases offers 1 random non-enhancement consumable to claim, replace, or decline.", 30),
            "rel_131", "Rare", 1, "", ArtifactSpecialRule.MerchantsGift);
        ConfigureSpecialArtifact(CreateArtifact("AlchemistsKit", "rel_130", "Alchemist’s Kit", "ALCH",
            "At each new map, choose 1 of 3 cached enhancement consumables. Enhancement consumables never enter enemy drops.", 30),
            "rel_130", "Rare", 1, "", ArtifactSpecialRule.AlchemistsKit);
        ConfigureSpecialArtifact(CreateArtifact("FieldMedicsKit", "rel_122", "Field Medic’s Kit", "MEDIC",
            "The first successful consumable use each encounter also heals 3 HP.", 20),
            "rel_122", "Common", 1, "", ArtifactSpecialRule.FieldMedicsKit);

        ConfigureSpecialArtifact(CreateArtifact("RetaliationEpic", "rel_111", "Retaliation Emblem", "RETAL+",
            "Keep Rare counterattacks. A counterattack kill permanently adds +1 counterattack damage.", 30),
            "rel_111", "Epic", 3, "rel_022", ArtifactSpecialRule.RetaliationEpic);
        ConfigureSpecialArtifact(CreateArtifact("CrownOfEndurance", "rel_139", "Crown of Endurance", "CROWN",
            "Boss kills permanently grant +3 max HP; grant +3 additional max HP if you lost no HP during that encounter.", 20),
            "rel_139", "Common", 1, "", ArtifactSpecialRule.CrownOfEndurance);
        ConfigureSpecialArtifact(CreateArtifact("KingslayersMark", "rel_138", "Kingslayer’s Mark", "KING",
            "Against a Boss, manually play 3 different ranks to empower your next attack by +1. Spend one charge per empowered attack.", 20),
            "rel_138", "Common", 1, "", ArtifactSpecialRule.KingslayersMark);
        ConfigureSpecialArtifact(CreateArtifact("AceRare", "rel_114", "Ace Emblem", "ACE+",
            "Keep Common critical chance. Once per encounter, a critical Ace pair returns one physical Ace to your hand.", 30),
            "rel_114", "Rare", 2, "rel_014", ArtifactSpecialRule.AceRare);
        ConfigureSpecialArtifact(CreateArtifact("AceEpic", "rel_115", "Ace Emblem", "ACE++",
            "Keep Rare behavior. An Ace paired with a Face card is guaranteed to critically strike.", 30),
            "rel_115", "Epic", 3, "rel_114", ArtifactSpecialRule.AceEpic);
        ConfigureSpecialArtifact(CreateArtifact("DiamondEpic", "rel_108", "Diamond Emblem", "♦+",
            "Keep Rare draw 2. Every third manually played Diamond lets you choose one discard card to draw.", 30),
            "rel_108", "Epic", 3, "rel_027", ArtifactSpecialRule.DiamondEpic);
        ConfigureSpecialArtifact(CreateArtifact("TrophyRack", "rel_137", "Trophy Rack", "TROPHY",
            "Each Elite kill permanently adds +1 damage to your opening attack against Bosses.", 20),
            "rel_137", "Common", 1, "", ArtifactSpecialRule.TrophyRack);
        ConfigureSpecialArtifact(CreateArtifact("HuntersLedger", "rel_119", "Hunter’s Ledger", "HUNT",
            "Each Elite kill permanently adds +1 damage to the first attack of each encounter.", 20),
            "rel_119", "Common", 1, "", ArtifactSpecialRule.HuntersLedger);
        ConfigureSpecialArtifact(CreateArtifact("HammerRare", "rel_140", "Hammer", "HAMMER",
            "Every 3 completed nodes, choose one of 3 Common Enhancements and an eligible card. If none are eligible, retry or decline.", 30),
            "rel_140", "Rare", 2, "rel_009", ArtifactSpecialRule.Hammer);
        ConfigureSpecialArtifact(CreateArtifact("ArsenalEpic", "rel_030", "Arsenal Emblem", "ARSENAL+",
            "Face cards in hand grant diminishing hand capacity: floor(1 + 1/2 + … + 1/n). Does not inherit earlier tiers.", 30),
            "rel_030", "Epic", 3, "rel_029", ArtifactSpecialRule.ArsenalEpic);
        ConfigureSpecialArtifact(CreateArtifact("HeartEpic", "rel_105", "Heart Emblem", "♥+",
            "Keep Rare healing and Gold. Overhealing builds up to 10 Vitality at 1:1; the next Heart attack spends all Vitality for damage.", 30),
            "rel_105", "Epic", 3, "rel_026", ArtifactSpecialRule.HeartEpic);
        ConfigureSpecialArtifact(CreateArtifact("MimicEpic", "rel_116", "Mimic Emblem", "MIMIC+",
            "Keep base behavior. Held cards with matching ranks trigger their in-hand effects one additional time.", 30),
            "rel_116", "Epic", 2, "rel_017", ArtifactSpecialRule.MimicEpic);
        ConfigureSpecialArtifact(CreateArtifact("DwarfRare", "rel_112", "Dwarf Emblem", "LOW+",
            "Keep Common bonus action. Playing a rank below Five prepares +1 damage for the next higher-rank attack.", 30),
            "rel_112", "Rare", 2, "rel_013", ArtifactSpecialRule.DwarfRare);
        ConfigureSpecialArtifact(CreateArtifact("DwarfEpic", "rel_113", "Dwarf Emblem", "LOW++",
            "Keep Rare behavior. Killing with the prepared higher-rank attack permanently gives its low-rank source card +1 attack.", 30),
            "rel_113", "Epic", 3, "rel_112", ArtifactSpecialRule.DwarfEpic);
        ConfigureSpecialArtifact(CreateArtifact("HiddenTrail", "rel_135", "Hidden Trail", "TRAIL",
            "Once per map before entry, reroll one reachable hidden node to a different hidden type. Never changes Bosses, nodes, or routes.", 20),
            "rel_135", "Common", 1, "", ArtifactSpecialRule.HiddenTrail);
        ConfigureSpecialArtifact(CreateArtifact("WanderersBoots", "rel_133", "Wanderer’s Boots", "BOOTS",
            "After visiting 3 distinct node types on a map, heal 3 HP and gain 3 Gold once per map.", 20),
            "rel_133", "Common", 1, "", ArtifactSpecialRule.WanderersBoots);
        ConfigureSpecialArtifact(CreateArtifact("WarpathBanner", "rel_134", "Warpath Banner", "WARPATH",
            "Consecutive Combat/Elite/Boss wins add +2 starting Block to the next combat, capped at 6. Noncombat nodes reset the streak.", 20),
            "rel_134", "Common", 1, "", ArtifactSpecialRule.WarpathBanner);
        ConfigureSpecialArtifact(CreateArtifact("HandsEpic", "rel_109", "Hands Emblem", "HANDS+",
            "Keep Rare behavior. Allow four matching ranks; each selected card’s authored on-play effects trigger twice, without replaying attacks or actions.", 30),
            "rel_109", "Epic", 3, "rel_020", ArtifactSpecialRule.HandsEpic);
        ConfigureSpecialArtifact(CreateArtifact("BalancersScale", "rel_118", "Balancer’s Scale", "SCALE",
            "Alternate manually played low-rank and Face cards. Each correct alternation adds +1 damage; repeating a group resets the bonus.", 20),
            "rel_118", "Common", 1, "", ArtifactSpecialRule.BalancersScale);
        ConfigureSpecialArtifact(CreateArtifact("SpadeRare", "rel_106", "Spade Emblem", "♠+",
            "Keep doubled Block. One Spade that fully blocks an attack draws 1 card.", 30),
            "rel_106", "Rare", 2, "rel_004", ArtifactSpecialRule.SpadeRare);
        ConfigureSpecialArtifact(CreateArtifact("SpadeEpic", "rel_107", "Spade Emblem", "♠++",
            "Keep Rare behavior. Unused Spade Block adds +1 damage to your next attack.", 30),
            "rel_107", "Epic", 3, "rel_106", ArtifactSpecialRule.SpadeEpic);
        CopyUpgradeBaseData("rel_114", "rel_014");
        CopyUpgradeBaseData("rel_115", "rel_114");
        CopyUpgradeBaseData("rel_108", "rel_027");
        CopyUpgradeBaseData("rel_105", "rel_026");
        CopyUpgradeBaseData("rel_116", "rel_017");
        CopyUpgradeBaseData("rel_112", "rel_013");
        CopyUpgradeBaseData("rel_113", "rel_112");
        CopyUpgradeBaseData("rel_109", "rel_020");
        CopyUpgradeBaseData("rel_106", "rel_004");
        CopyUpgradeBaseData("rel_107", "rel_106");
        CreateMasterThief();
        Debug.Log("Artifact assets created or updated.");
    }

    [MenuItem("Game/Generate Enhancement Assets")]
    public static void GenerateEnhancements()
    {
        EnsureFolder(EnhancementDir);
        ConfigureCoreEnhancement(CreateEnhancement("Sharpened", "sharpened", "Sharpened", "ATK", "This card deals +3 attack damage", 12),
            "enh_001", GameplayEffectKind.FlatAttack, GameplayEffectTrigger.AttackCalculated, 3);
        ConfigureCoreEnhancement(CreateEnhancement("Reinforced", "reinforced", "Hardened", "DEF", "This card blocks +3 damage", 12),
            "enh_002", GameplayEffectKind.FlatBlock, GameplayEffectTrigger.DefenseCalculated, 3);
        ConfigureCoreEnhancement(CreateEnhancement("Mending", "mending", "Mending", "HP", "Heal 2 HP at each player-turn start while this card is in hand", 15),
            "enh_003", GameplayEffectKind.Heal, GameplayEffectTrigger.PlayerTurnStart, 2);
        ConfigureCoreEnhancement(CreateEnhancement("Quickdraw", "quickdraw", "Quickdraw", "DRAW", "Draw 1 card when played", 18),
            "enh_004", GameplayEffectKind.Draw, GameplayEffectTrigger.CardCommitted, 1);
        ConfigureCoreEnhancement(CreateEnhancement("DoubleStrike", "double_strike", "Double Strike", "2×", "This card hits the chosen enemy twice, dealing its full resolved damage on each hit.", 20),
            "enh_005", GameplayEffectKind.DoubleStrike, GameplayEffectTrigger.AttackCalculated, 1);
        Debug.Log("Enhancement assets created or updated.");
    }

    [MenuItem("Game/Generate Enemy Assets")]
    public static void GenerateEnemies()
    {
        EnsureFolder(EnemyDir);
        CreateEnemy("Thief", "Thief", 12, 1, 15, fleeAfterPlayerTurns: 2);
        CreateEnemy("Goblin", "Goblin", 3, 2, 3, encounterCount: 3);
        CreateEnemy("Knight", "Knight", 16, 4, 10);
        // Temporary 0 Gold rewards until the economy values in the workbook are approved.
        CreateEnemy("Shieldbearer", "Shieldbearer", 10, 3, 0);
        CreateEnemy("Brute", "Brute", 18, 3, 0);
        CreateEnemy("Duelist", "Duelist", 14, 4, 0);
        CreateEnemy("WarDrummer", "War Drummer", 8, 1, 0);
        CreateEnemy("GoblinCaptain", "Goblin Captain", 16, 3, 0);
        CreateEnemy("RoyalKnight", "Royal Knight", 32, 5, 0);
        CreateMasterThief();
        ConfigureEnemyAbility("MasterThief", "MasterThief", "Pickpocket & Escape",
            "After the first enemy attack, steal up to 8 Gold; flee after the player’s fourth action unless defeated.",
            EnemyAbilityEffect.MasterThief, 8);
        ConfigureEnemyAbility("Shieldbearer", "StartingShield", "Starting Shield",
            "Begins each encounter with one Shield.", EnemyAbilityEffect.StartWithShield, 1);
        ConfigureEnemyAbility("Brute", "ChargedAttack", "Charged Attack",
            "Alternates normal attacks with announced double-damage attacks.",
            EnemyAbilityEffect.AlternateChargedAttack, 1);
        ConfigureEnemyAbility("Duelist", "DuelistSuitCall", "Suit Call",
            "At each player-turn start, announce a seeded suit. The first attack with that suit deals 50% damage, not immunity.",
            EnemyAbilityEffect.SuitCall, 1);
        ConfigureEnemyAbility("WarDrummer", "WarDrum", "War Drum",
            "After each enemy response, grant each ally +1 ATK for this encounter, up to +3. Cannot buff self; death stops growth; bonus is fixed and not map-scaled.",
            EnemyAbilityEffect.WarDrum, 1);
        ConfigureEnemyAbility("GoblinCaptain", "Captaincy", "Captaincy",
            "Two Goblin allies gain +1 ATK while this Captain lives. This bonus is not map-scaled.",
            EnemyAbilityEffect.Captaincy, 1);
        ConfigureEnemyAbility("RoyalKnight", "RoyalGuard", "Royal Guard",
            "Alternates +1 Shield and a normal attack, then a telegraphed double-damage attack.",
            EnemyAbilityEffect.RoyalGuard, 1);
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

    static void CopyUpgradeBaseData(string upgradeId, string predecessorId)
    {
        var assets = AssetDatabase.FindAssets("t:RelicData", new[] { ArtifactDir })
            .Select(guid => AssetDatabase.LoadAssetAtPath<RelicData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(artifact => artifact != null).ToArray();
        var upgrade = assets.FirstOrDefault(artifact => artifact.canonicalId == upgradeId);
        var predecessor = assets.FirstOrDefault(artifact => artifact.canonicalId == predecessorId);
        if (upgrade == null || predecessor == null) return;
        upgrade.effects = predecessor.effects != null ? (GameplayEffectDefinition[])predecessor.effects.Clone() : System.Array.Empty<GameplayEffectDefinition>();
        upgrade.ruleModifiers = predecessor.ruleModifiers?.Where(rule => rule != null).Select(rule => new GameplayRuleModifierData
        {
            ruleId = rule.ruleId,
            kind = rule.kind,
            extraTurn = rule.extraTurn,
            sameRankMultiCard = rule.sameRankMultiCard
        }).ToArray() ?? System.Array.Empty<GameplayRuleModifierData>();
        if (upgradeId == "rel_109")
            foreach (var rule in upgrade.ruleModifiers)
                if (rule.kind == GameplayRuleModifierKind.SameRankMultiCard)
                    rule.sameRankMultiCard.maximumCards = 4;
        upgrade.restrictToSuit = predecessor.restrictToSuit;
        upgrade.affectedSuit = predecessor.affectedSuit;
        upgrade.requiresEnhancedCard = predecessor.requiresEnhancedCard;
        upgrade.damageMultiplier = predecessor.damageMultiplier;
        upgrade.flatDamageBonus = predecessor.flatDamageBonus;
        upgrade.defenseBonus = predecessor.defenseBonus;
        upgrade.reduceEnemyAttackByCardValue = predecessor.reduceEnemyAttackByCardValue;
        upgrade.recycleDiscardByCardValue = predecessor.recycleDiscardByCardValue;
        upgrade.drawByCardValue = predecessor.drawByCardValue;
        upgrade.healAfterVictory = predecessor.healAfterVictory;
        upgrade.bonusGold = predecessor.bonusGold;
        EditorUtility.SetDirty(upgrade);
    }

    static void ConfigureSpecialArtifact(RelicData artifact, string canonicalId, string rarity,
        int tier, string upgradeFromId, ArtifactSpecialRule specialRule)
    {
        artifact.canonicalId = canonicalId;
        artifact.rarity = rarity;
        artifact.tier = tier;
        artifact.upgradeFromId = upgradeFromId;
        artifact.specialRule = specialRule;
        artifact.effects = System.Array.Empty<GameplayEffectDefinition>();
        artifact.ruleModifiers = System.Array.Empty<GameplayRuleModifierData>();
        EditorUtility.SetDirty(artifact);
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

    static void CreateMasterThief()
    {
        const string path = "Assets/Data/Enemies/MasterThief.asset";
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyTypeData>(path);
        if (enemy == null)
        {
            enemy = ScriptableObject.CreateInstance<EnemyTypeData>();
            AssetDatabase.CreateAsset(enemy, path);
        }
        enemy.enemyName = "Master Thief";
        enemy.maxHp = 24;
        enemy.baseAttack = 4;
        enemy.goldReward = 20; // Owner-approved normal Elite reward; stolen Gold is refunded separately on defeat.
        enemy.encounterCount = 1;
        enemy.fleeAfterPlayerTurns = 4;
        EditorUtility.SetDirty(enemy);
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

    static void ConfigureEnemyAbility(string enemyAssetName, string abilityAssetName,
        string displayName, string description, EnemyAbilityEffect effect, int amount)
    {
        string folder = $"{EnemyDir}/Abilities";
        EnsureFolder(folder);
        string path = $"{folder}/{abilityAssetName}.asset";
        var ability = AssetDatabase.LoadAssetAtPath<EnemyAbility>(path);
        if (ability == null)
        {
            ability = ScriptableObject.CreateInstance<EnemyAbility>();
            AssetDatabase.CreateAsset(ability, path);
        }
        ability.id = abilityAssetName;
        ability.displayName = displayName;
        ability.description = description;
        ability.effect = effect;
        ability.amount = amount;
        EditorUtility.SetDirty(ability);

        var enemy = AssetDatabase.LoadAssetAtPath<EnemyTypeData>($"{EnemyDir}/{enemyAssetName}.asset");
        if (enemy == null) return;
        enemy.abilities = new[] { ability };
        EditorUtility.SetDirty(enemy);
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
