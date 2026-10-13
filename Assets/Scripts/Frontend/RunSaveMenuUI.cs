using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RunSaveMenuUI : MonoBehaviour
{
    public Button ContinueButton { get; private set; }
    TMP_Text _status;
    public static void Install(MainMenuUI menu)
    {
        if (menu.newRunButton == null) return;
        var ui = menu.GetComponent<RunSaveMenuUI>();
        if (ui == null) { ui = menu.gameObject.AddComponent<RunSaveMenuUI>(); ui.Build(menu); }
        ui.Refresh();
    }
    void Build(MainMenuUI menu)
    {
        ContinueButton = Instantiate(menu.newRunButton, menu.newRunButton.transform.parent);
        ContinueButton.name = "ContinueRunButton";
        ContinueButton.onClick = new Button.ButtonClickedEvent();
        ContinueButton.onClick.AddListener(Continue);
        var label = ContinueButton.GetComponentInChildren<TMP_Text>(); if (label) label.text = "CONTINUE";
        var buttons = new[] { ContinueButton, menu.newRunButton, menu.settingsButton, menu.creditsButton, menu.quitButton };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (!buttons[i]) continue;
            var rect = buttons[i].GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, 110 - i * 65); rect.sizeDelta = new Vector2(430, 54);
        }
        var go = new GameObject("RunSaveStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(menu.newRunButton.transform.parent, false);
        var sr = (RectTransform)go.transform; sr.anchorMin = sr.anchorMax = new Vector2(.5f, .5f);
        sr.anchoredPosition = new Vector2(0, -225); sr.sizeDelta = new Vector2(570, 70);
        _status = go.GetComponent<TMP_Text>(); _status.fontSize = 18; _status.alignment = TextAlignmentOptions.Center; _status.raycastTarget = false;
    }
    void Continue()
    {
        if (SavePromptUI.IsOpen) return;
        GameFlowService.Instance?.ContinueRun();
        Refresh();
    }
    public void Refresh()
    {
        if (ContinueButton == null) return;
        bool valid = SaveGameService.TryRead(out var data, out var error);
        ContinueButton.interactable = valid;
        if (_status) _status.text = valid
            ? $"Saved run • Map {data.bossIndex + 1} • {data.cards.gold} Gold" + (SaveGameService.Store.RecoveredFromBackup ? "\nRecovered from backup" : string.Empty)
            : error;
    }
}
