using System;

public enum ShopOfferKind
{
    Artifact,
    Enhancement,
    Investment,
    Guidance,
    Consumable
}

[Serializable]
public sealed class ShopOffer
{
    public ShopOfferKind kind;
    public RelicData artifact;
    public CardEnhancementData enhancement;
    public ConsumableData consumable;
    public int slot;
    public int price;

    public string StableId => kind switch
    {
        ShopOfferKind.Artifact => $"artifact:{artifact?.id}",
        ShopOfferKind.Enhancement => $"enhancement:{slot}:{enhancement?.id}",
        ShopOfferKind.Investment => "investment",
        ShopOfferKind.Guidance => "guidance",
        ShopOfferKind.Consumable => $"consumable:{consumable?.id}",
        _ => "unknown"
    };

    public string DisplayName => kind switch
    {
        ShopOfferKind.Artifact => artifact != null ? artifact.displayName : "Unknown Artifact",
        ShopOfferKind.Enhancement => enhancement != null ? enhancement.displayName : "Unknown Enhancement",
        ShopOfferKind.Investment => "Investment",
        ShopOfferKind.Guidance => "Guidance",
        ShopOfferKind.Consumable => consumable != null ? consumable.displayName : "Unknown Consumable",
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
            return $"Choose an owned card to enhance: {effect}";
        }
        if (kind == ShopOfferKind.Consumable)
            return consumable != null ? consumable.description : string.Empty;
        if (kind == ShopOfferKind.Investment)
            return "Invest 5 Gold. At the next Shop, there is a 50% chance to receive 15 Gold; otherwise receive nothing.";
        if (kind == ShopOfferKind.Guidance)
            return "Reveal one reachable hidden Event or Risk node on the current map.";
        return string.Empty;
    }
}
