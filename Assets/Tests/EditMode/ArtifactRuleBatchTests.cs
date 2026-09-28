using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ArtifactRuleBatchTests
{
    readonly List<UnityEngine.Object> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    RelicData _cheater, _hands, _dwarf, _dagger, _healingLight, _handsRare, _royal;
    CardEnhancementData _mending, _sharpened;
    Field _field;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        var canvas = Make<Canvas>("Rule Batch Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = new GameObject("Rule Batch Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _objects.Add(holder);
        holder.transform.SetParent(canvas.transform, false);
        _field = holder.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holder.transform;
        var drag = Make<Canvas>("Rule Batch Drag Canvas");
        drag.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = new GameObject("Rule Batch Card Prefab", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        _objects.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = Make<CardManager>("Rule Batch Cards");
        _cheater = Artifact("rel_011", "Cheater Emblem",
            Rule("cheater_first_turn", GameplayRuleModifierKind.ExtraTurn,
                new ExtraTurnRuleDefinition { trigger = ExtraTurnRuleTrigger.FirstPlayerTurnCompleted, oncePerEncounter = true }));
        _hands = Artifact("rel_012", "Hands Emblem",
            Rule("hands_same_rank", GameplayRuleModifierKind.SameRankMultiCard,
                sameRank: new SameRankMultiCardRuleDefinition { requireMatchingRank = true, maximumCards = 3 }));
        _hands.effects = new[] { new GameplayEffectDefinition
        {
            kind = GameplayEffectKind.ActionDamageCap,
            trigger = GameplayEffectTrigger.ActionDamageCalculated,
            amount = 10,
            conditions = new[] { new GameplayEffectCondition
            {
                kind = GameplayConditionKind.HandsMultiCardAction, expected = true
            } }
        } };
        _dwarf = Artifact("rel_013", "Dwarf Emblem",
            Rule("dwarf_low_rank_turn", GameplayRuleModifierKind.ExtraTurn,
                new ExtraTurnRuleDefinition { trigger = ExtraTurnRuleTrigger.QualifyingPlayedActionCompleted,
                    requireAllPlayedCardsBelowRank = true, exclusiveRank = CardData.Rank.Five }));
        _dagger = AssetDatabaseArtifact("rel_006");
        _healingLight = AssetDatabaseArtifact("rel_010");
        _cards.Configure(_field, drag, prefab,
            new[] { _cheater, _hands, _dwarf, _dagger, _healingLight,
                AssetDatabaseArtifact("rel_005"), AssetDatabaseArtifact("rel_007"),
                AssetDatabaseArtifact("rel_023"),
                AssetDatabaseArtifact("rel_001"), AssetDatabaseArtifact("rel_002"),
                AssetDatabaseArtifact("rel_003"), AssetDatabaseArtifact("rel_004"),
                AssetDatabaseArtifact("rel_020"), AssetDatabaseArtifact("rel_100") },
            Array.Empty<CardEnhancementData>());
        _cards.ConfigureRandom(new DeterministicRandom(41));
        _combat = Make<CombatManager>("Rule Batch Combat");
        _combat.ConfigurePlayer(30);
        _combat.ConfigureCriticalRandom(new CountingRandom());
        _run = Make<RunManager>("Rule Batch Run");
        _run.thiefType = Enemy("Thief", 50, 3);
        _run.goblinType = Enemy("Goblin", 50, 3);
        _run.knightType = Enemy("Knight", 50, 3);
        _run.StartRunWithSeed("RULE-BATCH-SEED");
        _handsRare = AssetDatabaseArtifact("rel_020");
        _royal = AssetDatabaseArtifact("rel_100");
        _mending = AssetDatabaseEnhancement("enh_003");
        _sharpened = AssetDatabaseEnhancement("enh_001");
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        foreach (var go in _objects.AsEnumerable().Reverse())
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        foreach (var asset in _assets)
            if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
    }

    [Test]
    public void CanonicalRuleAssets_PersistTypedDefinitionsAndGameplaySettings()
    {
        var cheater = AssetDatabaseArtifact("rel_011");
        var hands = AssetDatabaseArtifact("rel_012");
        var dwarf = AssetDatabaseArtifact("rel_013");
        Assert.That(cheater.displayName, Is.EqualTo("Cheater Emblem"));
        Assert.That(cheater.ruleModifiers, Has.Length.EqualTo(1));
        Assert.That(cheater.ruleModifiers[0].kind, Is.EqualTo(GameplayRuleModifierKind.ExtraTurn));
        Assert.That(cheater.ruleModifiers[0].extraTurn.trigger, Is.EqualTo(ExtraTurnRuleTrigger.FirstPlayerTurnCompleted));
        Assert.That(cheater.ruleModifiers[0].extraTurn.oncePerEncounter, Is.True);
        Assert.That(hands.ruleModifiers[0].kind, Is.EqualTo(GameplayRuleModifierKind.SameRankMultiCard));
        Assert.That(hands.ruleModifiers[0].sameRankMultiCard.maximumCards, Is.EqualTo(3));
        Assert.That(hands.effects.Single(e=>e.kind==GameplayEffectKind.ActionDamageCap).amount, Is.EqualTo(10));
        Assert.That(hands.effects.Single(e=>e.kind==GameplayEffectKind.ActionDamageCap).conditions.Single().kind,
            Is.EqualTo(GameplayConditionKind.HandsMultiCardAction));
        Assert.That(dwarf.ruleModifiers[0].extraTurn.trigger,
            Is.EqualTo(ExtraTurnRuleTrigger.QualifyingPlayedActionCompleted));
        Assert.That(dwarf.ruleModifiers[0].extraTurn.requireAllPlayedCardsBelowRank, Is.True);
        Assert.That(dwarf.ruleModifiers[0].extraTurn.exclusiveRank, Is.EqualTo(CardData.Rank.Five));
        foreach (var a in new[]{cheater,hands,dwarf}) Assert.That(a.tier, Is.EqualTo(1));
    }

    [Test]
    public void Cheater_OnlyGrantsAfterFirstCompletedAction_AndUsesNormalTurnStart()
    {
        Buy(_cheater);
        Buy(_healingLight);
        var mendingCard = FindView(c => c.Rank != CardData.Rank.Ace);
        Assert.That(_cards.ApplyEnhancement(mendingCard.data.Id, _mending), Is.True);
        _combat.TakeRunDamage(10);
        var enemy = StartTarget(100, 3);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(24));
        Assert.That(_combat.QueuedExtraPlayerTurns, Is.Zero);
        var firstAction = FindView(c => c.Rank == CardData.Rank.Two, mendingCard.data);
        Assert.That(_combat.TryPlayCards(new[] { firstAction }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(_combat.QueuedExtraPlayerTurns, Is.Zero);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(28), "The Cheater turn starts the normal Light+Mending lifecycle.");
        var nextAction = FindView(c => c.Rank == CardData.Rank.Six, mendingCard.data);
        Assert.That(_combat.TryPlayCards(new[] { nextAction }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(28), "Cheater does not retrigger after its first player turn.");
    }

    [Test]
    public void Dwarf_QualifiesBelowFiveAndCanGrantAgainOnLaterExtraTurns()
    {
        Buy(_dwarf);
        var enemy = StartTarget(100, 3);
        var two = FindView(c => c.Rank == CardData.Rank.Two);
        Assert.That(_combat.TryPlayCards(new[] { two }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        var four = FindView(c => c.Rank == CardData.Rank.Four);
        Assert.That(_combat.TryPlayCards(new[] { four }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn), "A later qualifying extra turn can grant another.");
        var five = FindView(c => c.Rank == CardData.Rank.Five);
        Assert.That(_combat.TryPlayCards(new[] { five }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking), "Rank five is not below five.");
        Assert.That(_combat.QueuedExtraPlayerTurns, Is.Zero);
    }

    [Test]
    public void Dwarf_MixedAcePairQualifiesOnlyWhenEveryCommittedRankIsLow()
    {
        Buy(_dwarf);
        var enemy = StartTarget(200, 1);
        _combat.CriticalChancePercent = 0;
        var ace = FindView(c => c.Rank == CardData.Rank.Ace);
        var four = FindView(c => c.Rank == CardData.Rank.Four, ace.data);
        Assert.That(_combat.TryPlayCards(new[] { ace, four }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        _combat.Reset();
        enemy = StartTarget(200, 1);
        _combat.CriticalChancePercent = 0;
        ace = FindView(c => c.Rank == CardData.Rank.Ace);
        var five = FindView(c => c.Rank == CardData.Rank.Five, ace.data);
        Assert.That(_combat.TryPlayCards(new[] { ace, five }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
    }

    [Test]
    public void Dwarf_LowRankHandsSetQualifiesOnceForTheCompletedAction()
    {
        Buy(_hands);
        Buy(_dwarf);
        var set = EnsureRankInHand(CardData.Rank.Three, 3);
        var enemy = StartTarget(300, 2);
        Assert.That(_combat.TryPlayCards(set, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(_combat.QueuedExtraPlayerTurns, Is.Zero, "The one qualifying Dwarf grant has already entered PlayerTurn.");
    }

    [Test]
    public void CombinedCheaterAndDwarf_QueueTwoOrderedTurns_ThenResumeEnemyFlow()
    {
        Buy(_cheater);
        Buy(_dwarf);
        var grants = new List<string>();
        _combat.OnCombatLog += message => { if (message.Contains("grants an extra player turn")) grants.Add(message); };
        var enemy = StartTarget(100, 3);
        var low = FindView(c => c.Rank == CardData.Rank.Four);
        Assert.That(_dwarf.ruleModifiers[0].kind, Is.EqualTo(GameplayRuleModifierKind.ExtraTurn));
        Assert.That(_dwarf.ruleModifiers[0].extraTurn.trigger,
            Is.EqualTo(ExtraTurnRuleTrigger.QualifyingPlayedActionCompleted));
        Assert.That(_cheater.ruleModifiers[0].extraTurn.trigger,
            Is.EqualTo(ExtraTurnRuleTrigger.FirstPlayerTurnCompleted));
        Assert.That(_cards.ownedArtifacts, Is.EqualTo(new[] { _cheater, _dwarf }));
        Assert.That(low.data.Rank, Is.LessThan(CardData.Rank.Five));
        Assert.That(_combat.TryPlayCards(new[] { low }, enemy), Is.True);
        Assert.That(_combat.CompletedPlayerTurnCount, Is.EqualTo(1));
        Assert.That(_combat.QueuedExtraPlayerTurns, Is.EqualTo(1));
        CollectionAssert.AreEqual(new[] {
            "Cheater Emblem grants an extra player turn."
        }, grants);
        Assert.That(_combat.QueuedExtraPlayerTurns, Is.EqualTo(1), "Cheater's turn is entered first; Dwarf's independent grant remains queued.");
        var high = FindView(c => c.Rank == CardData.Rank.Seven, low.data);
        Assert.That(_combat.TryPlayCards(new[] { high }, enemy), Is.True);
        CollectionAssert.AreEqual(new[] {
            "Cheater Emblem grants an extra player turn.",
            "Dwarf Emblem grants an extra player turn."
        }, grants);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(_combat.QueuedExtraPlayerTurns, Is.Zero);
        var anotherHigh = FindView(c => c.Rank == CardData.Rank.Eight, high.data);
        Assert.That(_combat.TryPlayCards(new[] { anotherHigh }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(grants.Count, Is.EqualTo(2), "The high-rank follow-up did not retrigger Dwarf.");
    }

    [Test]
    public void CheaterOncePerEncounterStateResetsOnNewEncounterAndCombatReset()
    {
        Buy(_cheater);
        var enemy = StartTarget(200, 1);
        Assert.That(_combat.TryPlayCards(new[] { FindView(c => c.Rank == CardData.Rank.Two) }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        // End the second player turn normally, then start a fresh encounter without resetting the run/artifacts.
        Assert.That(_combat.TryPlayCards(new[] { FindView(c => c.Rank == CardData.Rank.Eight) }, enemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        _combat.Reset();
        var nextEnemy = StartTarget(200, 1);
        Assert.That(_combat.TryPlayCards(new[] { FindView(c => c.Rank == CardData.Rank.Three) }, nextEnemy), Is.True);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn), "Encounter start cleared Cheater's once flag.");
    }

    [Test]
    public void HandsRare_ResolvesCardsInSelectionOrderAndFinishesCommittedEffectsAfterTargetDies()
    {
        Buy(_hands);
        Buy(_handsRare);
        CollectionAssert.Contains(_cards.ownedArtifacts, _handsRare);
        CollectionAssert.DoesNotContain(_cards.ownedArtifacts, _hands);

        var group = EnsureRankInHand(CardData.Rank.Six, 3);
        for (int i = 0; i < group.Length; i++)
        {
            var onPlay = ScriptableObject.CreateInstance<CardEnhancementData>();
            onPlay.displayName = $"Committed Heal {i + 1}";
            onPlay.healOnPlay = i + 1;
            _assets.Add(onPlay);
            Assert.That(group[i].data.TryApplyEnhancement(onPlay), Is.True);
        }

        _combat.player.TakeDamage(20);
        var first = new EnemyRuntime(Type("Low-HP Target", 1, 1));
        var second = new EnemyRuntime(Type("Untouched Target", 200, 1));
        _combat.StartEncounter(new[] { first, second });
        var damageResults = new List<DamageResult>();
        var encounterResults = new List<EncounterResult>();
        var healLogs = new List<string>();
        _combat.OnDamageResolved += damageResults.Add;
        _combat.OnEncounterResult += encounterResults.Add;
        _combat.OnCombatLog += message => { if (message.Contains(": Healed ")) healLogs.Add(message); };
        var committedIds = group.Select(view => view.data.Id).ToArray();
        var selectionOrder = group.Select(view => view.data.Id).ToArray();

        Assert.That(_combat.TryPlayCards(group, first), Is.True);
        Assert.That(first.IsDefeated, Is.True);
        Assert.That(second.currentHp, Is.EqualTo(200), "Remaining committed cards must not retarget the surviving enemy.");
        Assert.That(damageResults, Has.Count.EqualTo(1), "Only the first card's attack reached the 1-HP target.");
        Assert.That(damageResults[0].Request.HitIndex, Is.EqualTo(0));
        Assert.That(group.All(view => _cards.discardPile.Contains(view.data)), Is.True,
            "All committed cards are disposed even though the first one defeated the target.");
        Assert.That(committedIds.OrderBy(id => id), Is.EqualTo(selectionOrder.OrderBy(id => id)));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(16), "Every committed card's on-play effect resolves exactly once.");
        CollectionAssert.AreEqual(new[]
        {
            "Committed Heal 1: Healed 1 HP.",
            "Committed Heal 2: Healed 2 HP.",
            "Committed Heal 3: Healed 3 HP."
        }, healLogs, "Effects resolve in exact selection order even after the first card kills the target.");
        Assert.That(_combat.Enemies, Has.Count.EqualTo(1));
        Assert.That(_combat.Enemies[0], Is.SameAs(second));
        Assert.That(_combat.CompletedPlayerTurnCount, Is.EqualTo(1), "The whole group spends one player action.");
        Assert.That(encounterResults, Is.Empty, "The surviving enemy prevents premature victory.");
    }

    [Test]
    public void RoyalFamily_InvalidMixedSuitSelectionConsumesNothing()
    {
        Buy(_royal);
        AddFaceCard(CardData.Suit.Clubs, CardData.Rank.Jack);
        AddFaceCard(CardData.Suit.Spades, CardData.Rank.Queen);
        AddFaceCard(CardData.Suit.Diamonds, CardData.Rank.King);
        var cards = new List<CardView>();
        cards.Add(FindView(c => c.Rank == CardData.Rank.Ten && c.Suit == CardData.Suit.Hearts));
        cards.Add(FindView(c => c.Rank == CardData.Rank.Jack && c.Suit == CardData.Suit.Clubs,
            cards.Select(view => view.data).ToArray()));
        cards.Add(FindView(c => c.Rank == CardData.Rank.Queen && c.Suit == CardData.Suit.Spades,
            cards.Select(view => view.data).ToArray()));
        cards.Add(FindView(c => c.Rank == CardData.Rank.King && c.Suit == CardData.Suit.Diamonds,
            cards.Select(view => view.data).ToArray()));
        cards.Add(FindView(c => c.Rank == CardData.Rank.Ace && c.Suit == CardData.Suit.Hearts,
            cards.Select(view => view.data).ToArray()));
        var target = StartTarget(100, 1);
        int handCount = _cards.HandCount;
        int gold = _cards.gold;
        int damageEvents = 0;
        _combat.OnDamageResolved += _ => damageEvents++;

        Assert.That(_combat.CanPlayCards(cards, target), Is.False);
        Assert.That(_combat.TryPlayCards(cards, target), Is.False);
        Assert.That(_cards.HandCount, Is.EqualTo(handCount));
        Assert.That(_cards.gold, Is.EqualTo(gold));
        Assert.That(target.currentHp, Is.EqualTo(100));
        Assert.That(damageEvents, Is.Zero);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
    }

    [Test]
    public void RoyalFamily_WildSuitDefeatsBossThroughOneActionAndAwardsVictoryOnce()
    {
        Buy(_royal);
        AddFaceCard(CardData.Suit.Hearts, CardData.Rank.Jack);
        AddFaceCard(CardData.Suit.Spades, CardData.Rank.Queen);
        AddFaceCard(CardData.Suit.Hearts, CardData.Rank.King);
        var royalCards = new List<CardView>();
        royalCards.Add(FindView(c => c.Rank == CardData.Rank.Ten && c.Suit == CardData.Suit.Hearts));
        royalCards.Add(FindView(c => c.Rank == CardData.Rank.Jack && c.Suit == CardData.Suit.Hearts,
            royalCards.Select(view => view.data).ToArray()));
        royalCards.Add(FindView(c => c.Rank == CardData.Rank.Queen && c.Suit == CardData.Suit.Spades,
            royalCards.Select(view => view.data).ToArray()));
        royalCards.Add(FindView(c => c.Rank == CardData.Rank.King && c.Suit == CardData.Suit.Hearts,
            royalCards.Select(view => view.data).ToArray()));
        royalCards.Add(FindView(c => c.Rank == CardData.Rank.Ace && c.Suit == CardData.Suit.Hearts,
            royalCards.Select(view => view.data).ToArray()));
        var royal = royalCards.ToArray();
        var onPlay = ScriptableObject.CreateInstance<CardEnhancementData>();
        onPlay.displayName = "Royal Heal";
        onPlay.healOnPlay = 1;
        _assets.Add(onPlay);
        var wildHeal = ScriptableObject.CreateInstance<CardEnhancementData>();
        wildHeal.displayName = "Wild Royal";
        wildHeal.effects = new[]
        {
            new GameplayEffectDefinition { kind = GameplayEffectKind.WildSuit },
            new GameplayEffectDefinition { kind = GameplayEffectKind.Heal,
                trigger = GameplayEffectTrigger.CardCommitted, amount = 1 }
        };
        _assets.Add(wildHeal);
        for (int i = 0; i < royal.Length; i++)
            Assert.That(royal[i].data.TryApplyEnhancement(i == 2 ? wildHeal : onPlay), Is.True);

        var type = Type("Boss God", int.MaxValue, 9);
        type.goldReward = 13;
        var boss = new EnemyRuntime(type);
        _combat.StartEnemy(boss);
        _combat.player.TakeDamage(10);
        var damageResults = new List<DamageResult>();
        var encounterResults = new List<EncounterResult>();
        var playLogs = new List<string>();
        _combat.OnDamageResolved += damageResults.Add;
        _combat.OnEncounterResult += encounterResults.Add;
        _combat.OnCombatLog += message => { if (message.StartsWith("Played ")) playLogs.Add(message); };
        var random = new CountingRandom();
        _combat.ConfigureCriticalRandom(random);
        _combat.CriticalChancePercent = 100f;
        foreach (var view in royal) _cards.ToggleCardSelection(view);
        Assert.That(_cards.SelectedCards, Has.Count.EqualTo(5));
        Assert.That(_combat.CanPlayCards(_cards.SelectedCards, boss), Is.True);
        int[] committedIds = royal.Select(view => view.data.Id).ToArray();

        Assert.That(_combat.TryPlayCards(_cards.GetSelectedCardsSnapshot(), boss), Is.True);
        Assert.That(boss.IsDefeated, Is.True, "Instant defeat also applies to boss-scale HP.");
        Assert.That(damageResults, Is.Empty, "Instant defeat is not represented by synthetic numeric damage.");
        Assert.That(encounterResults, Is.EqualTo(new[] { EncounterResult.Victory }));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.GameWon));
        Assert.That(_cards.gold, Is.EqualTo(13));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(25), "All five on-play heals resolve before the defeat outcome.");
        Assert.That(random.Calls, Is.Zero, "The instant-defeat action does not spend a critical roll.");
        Assert.That(playLogs, Has.Count.EqualTo(1), "The group is logged as one action.");
        Assert.That(committedIds.All(id => _cards.discardPile.Any(card => card.Id == id)), Is.True);
        Assert.That(_combat.TryPlayCards(royal, boss), Is.False, "A resolved encounter cannot award victory twice.");
        Assert.That(encounterResults, Has.Count.EqualTo(1));
        Assert.That(_cards.gold, Is.EqualTo(13));
    }

    void AddFaceCard(CardData.Suit suit, CardData.Rank rank)
    {
        var source = CardData.Create(suit, rank);
        _assets.Add(source);
        Assert.That(_cards.AddBossReward(source), Is.Not.Null);
    }

    [Test]
    public void Hands_SelectionRequiresRuleAllowsTwoThreeSameRankAndRejectsFourthOrMismatch()
    {
        var different = Views().Take(2).ToArray();
        var selectionTarget = StartTarget(500, 1);
        _cards.ToggleCardSelection(different[0]);
        _cards.ToggleCardSelection(different[1]);
        Assert.That(_cards.SelectedCards, Has.Count.EqualTo(1), "Without Hands, an ordinary click replaces the single selection.");
        Buy(_hands);
        _cards.ClearSelection();
        var group = EnsureRankInHand(CardData.Rank.Six, 4);
        for (int i = 0; i < 3; i++) _cards.ToggleCardSelection(group[i]);
        Assert.That(_cards.SelectedCards, Has.Count.EqualTo(3));
        _cards.ToggleCardSelection(group[3]);
        Assert.That(_cards.SelectedCards, Has.Count.EqualTo(3), "A fourth card is rejected.");
        _cards.ClearSelection();
        _cards.ToggleCardSelection(group[0]);
        _cards.ToggleCardSelection(group[1]);
        var mismatch = FindView(c => c.Rank == CardData.Rank.Seven, group[0].data, group[1].data);
        _cards.ToggleCardSelection(mismatch);
        Assert.That(_cards.SelectedCards, Has.Count.EqualTo(2));
        Assert.That(_cards.SelectedCards.All(v => v.data.Rank == CardData.Rank.Six), Is.True);
    }

    [Test]
    public void Hands_CapsAggregatedTwoAndThreeCardActions_WithoutCappingSingleCards()
    {
        Buy(_hands);
        var group = EnsureRankInHand(CardData.Rank.Eight, 3);
        var target = StartTarget(500, 1);
        Assert.That(_combat.TryPlayCards(group.Take(2).ToArray(), target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(490));
        // New encounter, preserve Artifact ownership; an ordinary single face card is not Hands-capped.
        _combat.Reset();
        target = StartTarget(500, 1);
        var kingSource = CardData.Create(CardData.Suit.Hearts, CardData.Rank.King);
        _assets.Add(kingSource);
        Assert.That(_cards.AddBossReward(kingSource), Is.Not.Null);
        var king = FindView(c => c.Rank == CardData.Rank.King);
        int singleDamage = _combat.CalculateCardAttackDamage(king.data);
        Assert.That(_combat.TryPlayCards(new[] { king }, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(500 - singleDamage));
    }

    [Test]
    public void Hands_ThreeCardAceRankActionIsNotAcePairCritical_AndDaggerCapComposes()
    {
        Buy(_hands);
        Buy(_dagger);
        var eights = EnsureRankInHand(CardData.Rank.Eight, 3);
        var random = new CountingRandom();
        _combat.ConfigureCriticalRandom(random);
        _combat.CriticalChancePercent = 100;
        var target = StartTarget(500, 1);
        Assert.That(_combat.TryPlayCards(eights, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(490), "Hands and Dagger caps combine to one 10-point cap, no crit on three cards.");
        Assert.That(random.Calls, Is.Zero);

        _combat.Reset();
        var aces = EnsureRankInHand(CardData.Rank.Ace, 3);
        random = new CountingRandom();
        _combat.ConfigureCriticalRandom(random);
        _combat.CriticalChancePercent = 100;
        target = StartTarget(500, 1);
        Assert.That(_combat.TryPlayCards(aces, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(494), "A three-card Ace-rank Hands action is not a two-card Ace pair and receives no crit.");
        Assert.That(random.Calls, Is.Zero);
    }

    [Test]
    public void AcePairStillUsesExistingCriticalWithHandsOwnedAndDoesNotUseHandsCap()
    {
        Buy(_hands);
        Buy(AssetDatabaseArtifact("rel_001"));
        var aceClub = FindView(c => c.Rank == CardData.Rank.Ace && c.Suit == CardData.Suit.Clubs);
        var aceDiamond = FindView(c => c.Rank == CardData.Rank.Ace && c.Suit == CardData.Suit.Diamonds, aceClub.data);
        Assert.That(_cards.ApplyEnhancement(aceClub.data.Id, _sharpened), Is.True);
        Assert.That(_cards.ApplyEnhancement(aceDiamond.data.Id, _sharpened), Is.True);
        Assert.That(_combat.CalculateCardAttackDamage(aceClub.data), Is.EqualTo(8));
        Assert.That(_combat.CalculateCardAttackDamage(aceDiamond.data), Is.EqualTo(4));
        var pair = new[] { aceClub, aceDiamond };
        var random = new CountingRandom();
        _combat.ConfigureCriticalRandom(random);
        _combat.CriticalChancePercent = 100;
        var target = StartTarget(500, 1);
        Assert.That(_combat.TryPlayCards(pair, target), Is.True);
        Assert.That(target.currentHp, Is.EqualTo(476), "Ace pair: Club (1+3)×2 + Diamond (1+3) = 12, then ×2; Hands cap does not apply.");
        Assert.That(random.Calls, Is.Zero, "Guaranteed crit short-circuits the existing seeded RNG draw.");
    }

    [Test]
    public void RestartClearsRuntimeTurnGrantsAndArtifactOwnership()
    {
        Buy(_cheater);
        Buy(_dwarf);
        var enemy = StartTarget(500, 2);
        _combat.TryPlayCards(new[] { FindView(c => c.Rank == CardData.Rank.Four) }, enemy);
        Assert.That(_combat.QueuedExtraPlayerTurns, Is.EqualTo(1));
        _run.RestartRun();
        Assert.That(_combat.QueuedExtraPlayerTurns, Is.Zero);
        Assert.That(_cards.ownedArtifacts, Is.Empty);
        Assert.That(_cards.ownedCards, Has.Count.EqualTo(40));
    }

    void Buy(RelicData artifact) => Assert.That(_cards.BuyArtifact(artifact, 0), Is.True);

    RelicData Artifact(string id, string name, GameplayRuleModifierData rule)
    {
        var asset = ScriptableObject.CreateInstance<RelicData>();
        asset.id = id;
        asset.canonicalId = id;
        asset.displayName = name;
        asset.rarity = "Common";
        asset.tier = 1;
        asset.price = 0;
        asset.effects = Array.Empty<GameplayEffectDefinition>();
        asset.ruleModifiers = new[] { rule };
        _assets.Add(asset);
        return asset;
    }

    GameplayRuleModifierData Rule(string id, GameplayRuleModifierKind kind,
        ExtraTurnRuleDefinition extraTurn = default, SameRankMultiCardRuleDefinition sameRank = default)
    {
        return new GameplayRuleModifierData
        {
            ruleId = id,
            kind = kind,
            extraTurn = extraTurn,
            sameRankMultiCard = sameRank
        };
    }

    RelicData AssetDatabaseArtifact(string canonicalId) => AssetDatabase.FindAssets("t:RelicData",
            new[] { "Assets/Data/Relics" })
        .Select(guid => AssetDatabase.LoadAssetAtPath<RelicData>(AssetDatabase.GUIDToAssetPath(guid)))
        .Single(asset => asset.canonicalId == canonicalId);

    CardEnhancementData AssetDatabaseEnhancement(string canonicalId) => AssetDatabase.FindAssets("t:CardEnhancementData",
            new[] { "Assets/Data/Enhancements" })
        .Select(guid => AssetDatabase.LoadAssetAtPath<CardEnhancementData>(AssetDatabase.GUIDToAssetPath(guid)))
        .Single(asset => asset.canonicalId == canonicalId);

    EnemyRuntime StartTarget(int hp = 200, int attack = 1)
    {
        _combat.StartEnemy(new EnemyRuntime(Type("Rule Target", hp, attack)));
        return _combat.currentEnemy;
    }

    EnemyTypeData Enemy(string name, int hp, int attack)
    {
        var type = EnemyTypeData.Create(name, hp, attack, 0);
        type.name = name;
        _assets.Add(type);
        return type;
    }

    CardView[] Views() => _field.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(v => v.data != null && _cards.hand.Contains(v.data)).ToArray();

    CardView FindView(Func<CardInstance, bool> predicate, params CardInstance[] preserve) =>
        EnsureInHand(predicate, 1, preserve)[0];

    CardView[] EnsureRankInHand(CardData.Rank rank, int count)
    {
        var result = new List<CardView>();
        for (int i = 0; i < count; i++)
            result.Add(FindView(c => c.Rank == rank, result.Select(v => v.data).ToArray()));
        return result.ToArray();
    }

    CardView[] EnsureInHand(Func<CardInstance, bool> predicate, int count,
        params CardInstance[] preserve)
    {
        var found = new List<CardView>();
        for (int pass = 0; pass < 120 && found.Count < count; pass++)
        {
            foreach (var view in Views().Where(v => predicate(v.data) &&
                !preserve.Contains(v.data) && found.All(existing => existing.data != v.data)))
            {
                found.Add(view);
                if (found.Count == count) break;
            }
            if (found.Count >= count) break;
            var discard = Views().FirstOrDefault(v => !preserve.Contains(v.data) &&
                found.All(existing => existing.data != v.data));
            if (discard == null) break;
            _cards.TryDiscard(discard);
            _cards.DrawToHand(1);
        }
        Assert.That(found, Has.Count.EqualTo(count), "Required cards could not be found in hand.");
        return found.Take(count).ToArray();
    }

    EnemyTypeData Type(string name, int hp, int attack)
    {
        var type = EnemyTypeData.Create(name, hp, attack, 0);
        _assets.Add(type);
        return type;
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _objects.Add(go);
        return go.AddComponent<T>();
    }

    void CompleteNodes(int count)
    {
        var active = typeof(RunManager).GetField("_activeNode", BindingFlags.Instance | BindingFlags.NonPublic);
        var complete = typeof(RunManager).GetMethod("CompleteActiveNode", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 0; i < count; i++)
        {
            var node = _run.currentPath.First(n => !n.completed);
            active.SetValue(_run, node);
            complete.Invoke(_run, null);
        }
    }

    sealed class CountingRandom : IRandomSource
    {
        public int Calls { get; private set; }
        public int NextInt(int minInclusive, int maxExclusive) => throw new NotImplementedException();
        public float NextFloat() { Calls++; return 0f; }
    }
}
