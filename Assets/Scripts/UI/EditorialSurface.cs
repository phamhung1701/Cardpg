using UnityEngine;

/// <summary>Composition intent: open screens, paper content, and genuine dialogs use different framing.</summary>
public sealed class EditorialSurface : MonoBehaviour
{
    public enum Treatment { Open, Paper, Slate, Portrait, Node, Dialog }
    public Treatment treatment;
}
