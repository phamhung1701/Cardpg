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
    public int cardId;
    public int price;

    public string StableId => kind == ShopOfferKind.Artifact
        ? $"artifact:{artifact?.id}"
        : $"enhancement:{cardId}:{enhancement?.id}";

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

        var card = cards != null ? cards.FindOwnedCard(cardId) : null;
        string target = card != null ? card.DisplayName : "card";
        string effect = enhancement != null ? enhancement.description : string.Empty;
        return $"Enhance {target}: {effect}";
    }
}
