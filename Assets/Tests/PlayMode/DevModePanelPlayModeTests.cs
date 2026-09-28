#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class DevModePanelPlayModeTests
{
    [UnityTest]
    public IEnumerator RuntimePanels_EnterConfigureAndGateGameplayControlsThroughCoordinator()
    {
        var objects = new List<GameObject>();
        DevModeRuntime.Disable();

        var canvasObject = new GameObject("Dev UI Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        objects.Add(canvasObject);
        var menuObject = new GameObject("Home", typeof(RectTransform)); objects.Add(menuObject); menuObject.transform.SetParent(canvasObject.transform, false);
        var newRunObject = new GameObject("New Run", typeof(RectTransform), typeof(Image), typeof(Button)); objects.Add(newRunObject); newRunObject.transform.SetParent(menuObject.transform, false);
        var mainMenu = menuObject.AddComponent<MainMenuUI>(); mainMenu.newRunButton = newRunObject.GetComponent<Button>();
        DevModePanelUI.InstallMainMenu(mainMenu);
        var entry = menuObject.transform.Find("Dev Mode Button").GetComponent<Button>();
        entry.onClick.Invoke();
        Assert.That(canvasObject.transform.Find("Dev Mode Setup").gameObject.activeSelf, Is.True, "The dynamic menu entry opens the setup panel.");
        DevModeRuntime.Configure(true, true, false, true);

        var coordinator = canvasObject.AddComponent<GameplayMenuCoordinator>();
        var dialogPanel = new GameObject("Confirmation", typeof(RectTransform), typeof(Image)); objects.Add(dialogPanel); dialogPanel.transform.SetParent(canvasObject.transform, false);
        var dialogObject = new GameObject("Confirmation Dialog"); objects.Add(dialogObject); dialogObject.transform.SetParent(canvasObject.transform, false);
        var confirmation = dialogObject.AddComponent<ConfirmationDialogUI>(); confirmation.panel = dialogPanel;
        confirmation.confirmButton = MakeButton(dialogPanel.transform, "Confirm"); confirmation.cancelButton = MakeButton(dialogPanel.transform, "Cancel");
        var pauseObject = new GameObject("Pause", typeof(RectTransform)); objects.Add(pauseObject); pauseObject.transform.SetParent(canvasObject.transform, false); pauseObject.SetActive(false);
        var pause = pauseObject.AddComponent<PauseMenuUI>(); pause.coordinator = coordinator; pause.confirmationDialog = confirmation;
        pauseObject.SetActive(true);
        yield return null;

        var devButton = canvasObject.transform.Find("DEV Button").GetComponent<Button>();
        Assert.That(devButton.gameObject.activeSelf, Is.True, "A session-enabled dev run exposes the in-game control.");
        devButton.onClick.Invoke();
        Assert.That(coordinator.IsTop(pause.GetComponent<DevModePanelUI>()), Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        canvasObject.transform.Find("Dev Mode Controls/Close Button").GetComponent<Button>().onClick.Invoke();
        Assert.That(coordinator.LayerCount, Is.Zero);
        Assert.That(Time.timeScale, Is.EqualTo(1f));

        DevModeRuntime.Disable();
        yield return null;
        Assert.That(devButton.gameObject.activeSelf, Is.False);
        foreach (var go in objects) if (go != null) Object.Destroy(go);
        DevModeRuntime.Disable();
        GameplayInputGate.Clear();
        yield return null;
    }

    static Button MakeButton(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false); return go.GetComponent<Button>();
    }
}
#endif
