using System;

public enum ShopOfferKind
{
    Artifact,
    Enhancement,
    Investment,
    Guidance,
    Consumable,
    Contract
}

[Serializable]
public sealed class ShopOffer
{
    public ShopOfferKind kind;
    public RelicData artifact;
    public CardEnhancementData enhancement;
    public ConsumableData consumable;
    public int slot;
    public int basePrice;
    public int price;
    public int investmentStake;

    public string StableId => kind switch
    {
        ShopOfferKind.Artifact => $"artifact:{artifact?.id}",
        ShopOfferKind.Enhancement => $"enhancement:{slot}:{enhancement?.id}",
        ShopOfferKind.Investment => "investment",
        ShopOfferKind.Guidance => "guidance",
        ShopOfferKind.Consumable => $"consumable:{consumable?.id}",
        ShopOfferKind.Contract => "contract:quest",
        _ => "unknown"
    };

    public string DisplayName => kind switch
    {
        ShopOfferKind.Artifact => artifact != null ? artifact.displayName : "Unknown Artifact",
        ShopOfferKind.Enhancement => enhancement != null ? enhancement.displayName : "Unknown Enhancement",
        ShopOfferKind.Investment => "Investment",
        ShopOfferKind.Guidance => "Guidance",
        ShopOfferKind.Consumable => consumable != null ? consumable.displayName : "Unknown Consumable",
        ShopOfferKind.Contract => "Quest Contract",
        _ => "Unknown Offer"
    };

    public string Icon => kind switch
    {
        ShopOfferKind.Artifact => artifact != null ? artifact.icon : string.Empty,
        ShopOfferKind.Enhancement => enhancement != null ? enhancement.icon : string.Empty,
        ShopOfferKind.Investment => "GOLD",
        ShopOfferKind.Guidance => "MAP",
        ShopOfferKind.Consumable => consumable != null ? consumable.icon : string.Empty,
        _ => string.Empty
    };

    public string Description(CardManager cards)
    {
        if (kind == ShopOfferKind.Artifact)
            return artifact != null ? artifact.description : string.Empty;
        if (kind == ShopOfferKind.Enhancement)
        {
            string effect = enhancement != null ? enhancement.description : string.Empty;
            return $"Choose a card in your hand to enhance: {effect}";
        }
        if (kind == ShopOfferKind.Consumable)
            return consumable != null ? consumable.description : string.Empty;
        if (kind == ShopOfferKind.Investment)
            return $"Invest a positive amount of Gold (up to your current Gold). At the next Shop, a seeded 50% chance pays 3× your stake; otherwise you lose the stake.";
        if (kind == ShopOfferKind.Guidance)
            return "Reveal one reachable hidden Event or Risk node on the current map.";
        if (kind == ShopOfferKind.Contract)
            return "Pay 20 Gold, then choose one run-local objective. Complete it for a reward; failure costs 5 HP.";
        return string.Empty;
    }
}
