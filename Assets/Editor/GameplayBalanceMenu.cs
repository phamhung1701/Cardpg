using UnityEditor;
using UnityEngine;

public static class GameplayBalanceMenu
{
    const string EnableMenu = "Tools/Dealbreak/Balance/Record New Runs";

    [MenuItem(EnableMenu)]
    static void Toggle()
    {
        GameplayBalanceTelemetry.Enabled = !GameplayBalanceTelemetry.Enabled;
        Debug.Log("Balance recording for new runs: " + GameplayBalanceTelemetry.Enabled);
    }

    [MenuItem(EnableMenu, true)]
    static bool ValidateToggle()
    {
        Menu.SetChecked(EnableMenu, GameplayBalanceTelemetry.Enabled);
        return true;
    }

    [MenuItem("Tools/Dealbreak/Balance/Export Current Journal")]
    static void Export()
    {
        if (GameplayBalanceTelemetry.TryExport()) Debug.Log(GameplayBalanceTelemetry.LastExportPath);
        else Debug.LogWarning("No journal exported. Enable recording, then start a new run.");
    }

    [MenuItem("Tools/Dealbreak/Balance/Open Export Folder")]
    static void OpenFolder() => EditorUtility.RevealInFinder(GameplayBalanceTelemetry.ExportDirectory);

    [InitializeOnLoadMethod]
    static void InstallLifecycle()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) GameplayBalanceTelemetry.Finish("interrupted");
    }
}
