using UnityEngine;

[CreateAssetMenu(fileName = "Relic", menuName = "Game/Relic Data")]
public class RelicData : ScriptableObject
{
    public string id;
    public string displayName;
    public string icon;
    public string description;
    public int price;
}
