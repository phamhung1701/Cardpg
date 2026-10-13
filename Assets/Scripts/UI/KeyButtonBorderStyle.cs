using UnityEngine;
using UnityEngine.UI;

/// <summary>Persistent high-contrast outline for primary and confirmation actions.</summary>
public static class KeyButtonBorderStyle
{
    public static readonly Color BorderColor = new(1f, 0.78f, 0.28f, 1f);
    public static readonly Vector2 BorderOffset = new(2f, -2f);

    public static void Apply(Button button)
    {
        if (button == null || button.targetGraphic == null) return;
        var outline = button.targetGraphic.GetComponent<Outline>();
        if (outline == null) outline = button.targetGraphic.gameObject.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = BorderOffset;
        outline.useGraphicAlpha = false;
        outline.enabled = true;
    }
}
