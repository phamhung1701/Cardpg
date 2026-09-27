#if UNITY_EDITOR
using UnityEngine;

// Editor-only state shared by the Development EditorWindow and guarded gameplay hooks.
// Delete this file, the DevMode Editor scripts, and their guarded call sites to remove the feature.
public static class DevModeRuntime
{
    const string Prefix = "CardPG.DevMode.";
    const string EnabledKey = Prefix + "Enabled";
    const string InfiniteHealthKey = Prefix + "InfiniteHealth";
    const string InfiniteMoneyKey = Prefix + "InfiniteMoney";
    const string UnlimitedShopOffersKey = Prefix + "UnlimitedShopOffers";

    public static bool Enabled => PlayerPrefs.GetInt(EnabledKey, 0) != 0;
    public static bool InfiniteHealth => Enabled && PlayerPrefs.GetInt(InfiniteHealthKey, 1) != 0;
    public static bool InfiniteMoney => Enabled && PlayerPrefs.GetInt(InfiniteMoneyKey, 1) != 0;
    public static bool UnlimitedShopOffers => Enabled && PlayerPrefs.GetInt(UnlimitedShopOffersKey, 1) != 0;

    public static void Configure(bool enabled, bool infiniteHealth, bool infiniteMoney,
        bool unlimitedShopOffers)
    {
        PlayerPrefs.SetInt(EnabledKey, enabled ? 1 : 0);
        PlayerPrefs.SetInt(InfiniteHealthKey, infiniteHealth ? 1 : 0);
        PlayerPrefs.SetInt(InfiniteMoneyKey, infiniteMoney ? 1 : 0);
        PlayerPrefs.SetInt(UnlimitedShopOffersKey, unlimitedShopOffers ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void Disable() => Configure(false, false, false, false);
}
#endif
