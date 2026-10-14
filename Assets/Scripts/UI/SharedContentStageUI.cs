using UnityEngine;

/// <summary>One unframed encounter region within the persistent run shell.</summary>
public sealed class SharedContentStageUI : MonoBehaviour
{
    public RectTransform combat, market, map, incident;
    public Vector2 stageMin = new(.18f, .18f);
    public Vector2 stageMax = new(.88f, .89f);

    public void ApplyLayout()
    {
        foreach (var region in new[] { combat, market, map, incident })
            if (region) DealbreakUIStyle.Place(region, stageMin.x, stageMin.y, stageMax.x, stageMax.y);
    }
}
