using TMPro;
using UnityEngine;

/// <summary>Applies the compact, non-spoiling label used by route-map nodes.</summary>
public sealed class PathNodeButtonUI : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    public void SetNode(PathNode node, RunManager run)
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label == null) return;

        if (node == null || run == null)
        {
            label.text = "?";
            return;
        }

        if (!node.revealed && !node.completed)
        {
            label.text = "?";
            return;
        }

        label.text = node.kind switch
        {
            MapNodeType.Combat => "COMBAT",
            MapNodeType.Elite => "ELITE",
            MapNodeType.Shop => "SHOP",
            MapNodeType.Event => "?",
            MapNodeType.Upgrade => "UPGRADE",
            MapNodeType.Risk => "?",
            MapNodeType.Boss => run.GetNodeLabel(node).ToUpperInvariant(),
            _ => "?"
        };
    }
}
