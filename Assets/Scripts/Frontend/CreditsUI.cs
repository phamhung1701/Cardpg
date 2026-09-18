using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CreditsUI : MonoBehaviour
{
    public FrontendMenuNavigator navigator;
    public TMP_Text bodyLabel;
    public Button backButton;
    [TextArea(6, 14)] public string creditsText =
        "CARDPG\n\nPrototype design and development\n\nBuilt with Unity\n\nInspired by card-based roguelikes and tabletop card combat.";

    void OnEnable()
    {
        if (bodyLabel) bodyLabel.text = creditsText;
        if (backButton) backButton.onClick.AddListener(Back);
    }

    void OnDisable()
    {
        if (backButton) backButton.onClick.RemoveListener(Back);
    }

    public void Back() => navigator?.ShowHome();
}
