using System;

public enum ShopOfferKind
{
    Artifact,
    Enhancement
}

[Serializable]
public sealed class ShopOffer
{
    public ShopOfferKind kind;
    public RelicData artifact;
    public CardEnhancementData enhancement;
    public int slot;
    public int price;

    public string StableId => kind == ShopOfferKind.Artifact
        ? $"artifact:{artifact?.id}"
        : $"enhancement:{slot}:{enhancement?.id}";

    public string DisplayName => kind == ShopOfferKind.Artifact
        ? artifact != null ? artifact.displayName : "Unknown Artifact"
        : enhancement != null ? enhancement.displayName : "Unknown Enhancement";

    public string Icon => kind == ShopOfferKind.Artifact
        ? artifact != null ? artifact.icon : string.Empty
        : enhancement != null ? enhancement.icon : string.Empty;

    public string Description(CardManager cards)
    {
        if (kind == ShopOfferKind.Artifact)
            return artifact != null ? artifact.description : string.Empty;

        string effect = enhancement != null ? enhancement.description : string.Empty;
        return $"Choose an owned card to enhance: {effect}";
    }
}
