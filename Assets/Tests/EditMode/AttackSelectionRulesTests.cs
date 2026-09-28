using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class AttackSelectionRulesTests
{
    readonly System.Collections.Generic.List<UnityEngine.Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var item in _created)
            if (item != null) Object.DestroyImmediate(item);
        _created.Clear();
    }

    [Test]
    public void RoyalFamily_IsValidInAnyOrderAndRequiresExactlyUniqueRoyalRanks()
    {
        var cards = Royal(CardData.Suit.Hearts);
        Assert.That(AttackSelectionRules.IsRoyalFamily(cards), Is.True);
        Assert.That(AttackSelectionRules.IsRoyalFamily(cards.Reverse().ToArray()), Is.True);
        Assert.That(AttackSelectionRules.IsRoyalFamilyPartial(cards.Take(4).ToArray()), Is.True);
        Assert.That(AttackSelectionRules.IsRoyalFamily(cards.Take(3).Concat(new[] { cards[0] }).ToArray()), Is.False);
        var mixedSuit = Royal(CardData.Suit.Hearts);
        mixedSuit[0] = Make(CardData.Suit.Clubs, CardData.Rank.Ten);
        Assert.That(AttackSelectionRules.IsRoyalFamily(mixedSuit), Is.False);
    }

    [Test]
    public void RoyalFamily_WildSuitMayCompleteOneEffectiveSuit_ButNotWildRank()
    {
        var cards = Royal(CardData.Suit.Hearts);
        var wild = ScriptableObject.CreateInstance<CardEnhancementData>();
        _created.Add(wild);
        wild.effects = new[] { new GameplayEffectDefinition { kind = GameplayEffectKind.WildSuit } };
        cards[0] = Make(CardData.Suit.Spades, CardData.Rank.Ten);
        Assert.That(cards[0].TryApplyEnhancement(wild), Is.True);
        Assert.That(AttackSelectionRules.IsRoyalFamily(cards), Is.True);
        cards[0] = Make(CardData.Suit.Hearts, CardData.Rank.Nine);
        Assert.That(cards[0].TryApplyEnhancement(wild), Is.True);
        Assert.That(AttackSelectionRules.IsRoyalFamily(cards), Is.False);
    }

    CardInstance[] Royal(CardData.Suit suit) => new[]
    {
        Make(suit, CardData.Rank.Ten), Make(suit, CardData.Rank.Jack),
        Make(suit, CardData.Rank.Queen), Make(suit, CardData.Rank.King),
        Make(suit, CardData.Rank.Ace)
    };

    CardInstance Make(CardData.Suit suit, CardData.Rank rank)
    {
        var definition = CardData.Create(suit, rank);
        _created.Add(definition);
        return new CardInstance(definition, (int)rank + (int)suit * 20 + 1);
    }
}
