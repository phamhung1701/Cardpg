using System;
using UnityEngine;

public enum RunEffectType
{
    GainGold,
    LoseGold,
    Heal,
    LoseHealth,
    GainMaxHealth,
    DrawCards,
    RevealMapNode,
    GainConsumable,
    RandomArtifactEnhancementOrNothing
}

[Serializable]
public struct RunEffect
{
    public RunEffectType type;
    [Min(0)] public int amount;
    public string consumableId;
}

public enum RunEventChoiceInteraction
{
    Immediate = 0,
    ChooseEnhancementTarget = 1,
    DiscardTwoForRandomEnhancement = 2,
    DiscardCardsForTotalValue = 3
}

[Serializable]
public class RunEventChoice
{
    public string label;
    [TextArea] public string description;
    public RunEffect[] effects;
    public RunEventChoiceInteraction interaction;
    [Min(0)] public int requiredDiscardValue;
}

[CreateAssetMenu(fileName = "RunEvent", menuName = "Game/Run Event")]
public class RunEventDefinition : ScriptableObject
{
    public string id;
    public MapNodeType category = MapNodeType.Event;
    public string title;
    [TextArea] public string description;
    public RunEventChoice[] choices;
}
