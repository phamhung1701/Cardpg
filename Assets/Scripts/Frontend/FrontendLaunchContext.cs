public enum MainMenuDestination
{
    Home,
    RunSetup
}

public static class FrontendLaunchContext
{
    static string _pendingRunSeed;
    static bool _hasPendingRun;
    static MainMenuDestination _pendingMenuDestination;
    static bool _hasPendingMenuDestination;

    public static void RequestRun(string seed)
    {
        _pendingRunSeed = RunRandomContext.NormalizeSeed(seed);
        _hasPendingRun = true;
    }

    public static bool TryConsumeRunSeed(out string seed)
    {
        seed = _pendingRunSeed;
        bool hadRequest = _hasPendingRun;
        _pendingRunSeed = null;
        _hasPendingRun = false;
        return hadRequest;
    }

    public static void RequestMainMenu(MainMenuDestination destination)
    {
        _pendingMenuDestination = destination;
        _hasPendingMenuDestination = true;
    }

    public static MainMenuDestination ConsumeMainMenuDestination()
    {
        var destination = _hasPendingMenuDestination ? _pendingMenuDestination : MainMenuDestination.Home;
        _hasPendingMenuDestination = false;
        _pendingMenuDestination = MainMenuDestination.Home;
        return destination;
    }

    public static void Clear()
    {
        _pendingRunSeed = null;
        _hasPendingRun = false;
        _pendingMenuDestination = MainMenuDestination.Home;
        _hasPendingMenuDestination = false;
    }
}
