using System;
using UnityEngine;

/// <summary>Presentation crosswalk only. Does not rename definitions or change Headliner rewards.</summary>
[CreateAssetMenu(menuName = "Dealbreak/UI/Combat Portrait Catalog")]
public sealed class CombatPortraitCatalog : ScriptableObject
{
    [Serializable] public struct EnemyPortrait { public EnemyTypeData definition; public Sprite portrait; public string displayName; }
    [Serializable] public struct HeadlinerPortrait { public CardData.Rank rank; public CardData.Suit suit; public Sprite portrait; }
    public EnemyPortrait[] enemies = Array.Empty<EnemyPortrait>();
    public HeadlinerPortrait[] headliners = Array.Empty<HeadlinerPortrait>();

    public Sprite Resolve(EnemyTypeData definition)
    {
        if (definition == null) return null;
        if (definition.portraitSprite != null) return definition.portraitSprite;
        foreach (var entry in enemies)
            if (entry.definition == definition) return entry.portrait;
        var card = definition.sourceCard;
        if (card != null && card.IsFaceCard)
            foreach (var entry in headliners)
                if (entry.rank == card.rank && entry.suit == card.suit) return entry.portrait;
        return null;
    }
    public string DisplayName(EnemyRuntime enemy)
    {
        if(enemy==null)return string.Empty;
        foreach(var entry in enemies)
            if(entry.definition==enemy.type)
            {
                string name=!string.IsNullOrEmpty(entry.displayName)?entry.displayName:entry.portrait?entry.portrait.texture.name:null;
                if(!string.IsNullOrEmpty(name))return enemy.DisplayName.Replace(enemy.type.enemyName,name);
            }
        return enemy.DisplayName;
    }
}
