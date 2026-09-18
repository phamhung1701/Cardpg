using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct DisplayResolution
{
    public readonly int Width;
    public readonly int Height;

    public DisplayResolution(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public override string ToString() => $"{Width} × {Height}";
}

public readonly struct DisplaySettingsData
{
    public readonly int Width;
    public readonly int Height;
    public readonly FullScreenMode Mode;
    public readonly int VSyncCount;

    public DisplaySettingsData(int width, int height, FullScreenMode mode, int vSyncCount)
    {
        Width = width;
        Height = height;
        Mode = mode;
        VSyncCount = Mathf.Clamp(vSyncCount, 0, 1);
    }
}

public static class DisplaySettingsService
{
    const string WidthKey = "display.width";
    const string HeightKey = "display.height";
    const string ModeKey = "display.mode";
    const string VSyncKey = "display.vsync";

    public static readonly FullScreenMode[] SupportedModes =
    {
        FullScreenMode.Windowed,
        FullScreenMode.FullScreenWindow,
        FullScreenMode.ExclusiveFullScreen
    };

    public static IReadOnlyList<DisplayResolution> GetAvailableResolutions()
    {
        var values = Screen.resolutions
            .Select(value => new DisplayResolution(value.width, value.height))
            .GroupBy(value => (value.Width, value.Height))
            .Select(group => group.First())
            .OrderBy(value => value.Width)
            .ThenBy(value => value.Height)
            .ToList();

        if (!values.Any(value => value.Width == Screen.width && value.Height == Screen.height))
            values.Add(new DisplayResolution(Mathf.Max(640, Screen.width), Mathf.Max(480, Screen.height)));
        if (values.Count == 0)
            values.Add(new DisplayResolution(1280, 720));
        return values;
    }

    public static DisplaySettingsData Load()
    {
        var resolutions = GetAvailableResolutions();
        int savedWidth = PlayerPrefs.GetInt(WidthKey, Screen.width);
        int savedHeight = PlayerPrefs.GetInt(HeightKey, Screen.height);
        var resolution = resolutions.FirstOrDefault(value => value.Width == savedWidth && value.Height == savedHeight);
        if (resolution.Width <= 0 || resolution.Height <= 0)
            resolution = resolutions.FirstOrDefault(value => value.Width == Screen.width && value.Height == Screen.height);
        if (resolution.Width <= 0 || resolution.Height <= 0)
            resolution = resolutions[^1];

        int rawMode = PlayerPrefs.GetInt(ModeKey, (int)Screen.fullScreenMode);
        var mode = SupportedModes.Contains((FullScreenMode)rawMode)
            ? (FullScreenMode)rawMode
            : Screen.fullScreenMode;
        if (!SupportedModes.Contains(mode)) mode = FullScreenMode.Windowed;
        int vSync = Mathf.Clamp(PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0), 0, 1);

        var normalized = new DisplaySettingsData(resolution.Width, resolution.Height, mode, vSync);
        Save(normalized);
        return normalized;
    }

    public static void LoadAndApply() => Apply(Load());

    public static void Apply(DisplaySettingsData settings)
    {
        var resolutions = GetAvailableResolutions();
        var resolution = resolutions.FirstOrDefault(value => value.Width == settings.Width && value.Height == settings.Height);
        if (resolution.Width <= 0 || resolution.Height <= 0)
            resolution = resolutions[^1];
        var mode = SupportedModes.Contains(settings.Mode) ? settings.Mode : FullScreenMode.Windowed;
        int vSync = Mathf.Clamp(settings.VSyncCount, 0, 1);

        QualitySettings.vSyncCount = vSync;
        Screen.SetResolution(resolution.Width, resolution.Height, mode);
        Save(new DisplaySettingsData(resolution.Width, resolution.Height, mode, vSync));
    }

    static void Save(DisplaySettingsData settings)
    {
        PlayerPrefs.SetInt(WidthKey, settings.Width);
        PlayerPrefs.SetInt(HeightKey, settings.Height);
        PlayerPrefs.SetInt(ModeKey, (int)settings.Mode);
        PlayerPrefs.SetInt(VSyncKey, settings.VSyncCount);
        PlayerPrefs.Save();
    }
}
