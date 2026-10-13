using UnityEngine;
using UnityEngine.UI;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "MainMenuUI")]
public sealed class MainMenuUI : MonoBehaviour
{
    public FrontendMenuNavigator navigator;
    public Button newRunButton;
    public Button settingsButton;
    public Button creditsButton;
    public Button quitButton;

    void OnEnable()
    {
        RunSaveMenuUI.Install(this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevModePanelUI.InstallMainMenu(this);
#endif
        if (newRunButton) newRunButton.onClick.AddListener(ShowRunSetup);
        if (settingsButton) settingsButton.onClick.AddListener(ShowSettings);
        if (creditsButton) creditsButton.onClick.AddListener(ShowCredits);
        if (quitButton) quitButton.onClick.AddListener(Quit);
    }

    void OnDisable()
    {
        if (newRunButton) newRunButton.onClick.RemoveListener(ShowRunSetup);
        if (settingsButton) settingsButton.onClick.RemoveListener(ShowSettings);
        if (creditsButton) creditsButton.onClick.RemoveListener(ShowCredits);
        if (quitButton) quitButton.onClick.RemoveListener(Quit);
    }

    public void ShowRunSetup() => navigator?.ShowRunSetup();
    public void ShowSettings() => navigator?.ShowSettings();
    public void ShowCredits() => navigator?.ShowCredits();
    public void Quit() => GameFlowService.Instance?.Quit();
}
