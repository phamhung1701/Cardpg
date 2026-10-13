using System;

public enum ShopOfferKind
{
    Artifact,
    Enhancement,
    Investment,
    Guidance,
    Consumable,
    Contract,
    Supply
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
    public int supplyQuantity = 1;
    public bool purchased;
    public int generation;
    public bool IsService => kind is ShopOfferKind.Investment or ShopOfferKind.Guidance or ShopOfferKind.Contract or ShopOfferKind.Supply;

    public string StableId => generation == 0 ? BaseStableId : $"refresh:{generation}:{BaseStableId}";
    string BaseStableId => kind switch
    {
        ShopOfferKind.Artifact => $"artifact:{artifact?.id}",
        ShopOfferKind.Enhancement => $"enhancement:{slot}:{enhancement?.id}",
        ShopOfferKind.Investment => "investment",
        ShopOfferKind.Guidance => "guidance",
        ShopOfferKind.Consumable => $"consumable:{consumable?.id}",
        ShopOfferKind.Contract => "contract:quest",
        ShopOfferKind.Supply => "supply",
        _ => "unknown"
    };

    public string DisplayName => kind switch
    {
        ShopOfferKind.Artifact => artifact != null ? artifact.displayName : "Unknown Gear",
        ShopOfferKind.Enhancement => enhancement != null ? enhancement.displayName : "Unknown Mod",
        ShopOfferKind.Investment => "Stake",
        ShopOfferKind.Guidance => "Intel",
        ShopOfferKind.Consumable => consumable != null ? consumable.displayName : "Unknown Trick",
        ShopOfferKind.Contract => "Contract",
        ShopOfferKind.Supply => "Supply",
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
            return "Invest Credits now; at the next Black Market, the investment may return more Credits or return nothing.";
        if (kind == ShopOfferKind.Guidance)
            return "Reveal 1 random hidden node on the current district.";
        if (kind == ShopOfferKind.Supply)
            return "Draw cards from the existing Draw Pile for 3 Credits each, up to your hand limit. Once per Black Market; no recycling.";
        if (kind == ShopOfferKind.Contract)
            return "Choose: win 2 encounters within 12 districts for 6 Credits; defeat a reachable Enforcer before leaving this district for an eligible Rare Gear or heal 5 HP if none is collectible; or win an encounter with zero actual HP loss for 10 Credits. Contract is run-local and uses no Gear slot. One active Contract maximum; failure settles once for up to 5 HP, never below 1.";
        return string.Empty;
    }
}
