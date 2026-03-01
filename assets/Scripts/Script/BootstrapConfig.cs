public enum GameMode
{
    Online,
    OfflineLocal,
}

public static class BootstrapConfig
{
    public static GameMode Mode { get; private set; } = GameMode.Online;
    public static string OfflinePlayerDeckSelector { get; private set; } = "";
    public static string OfflineOpponentDeckSelector { get; private set; } = "";
    public static bool AutoStartOfflineDuel { get; private set; } = false;
    public static bool ShowLegacyBattleModeChooser { get; private set; } = false;

    public static bool IsOfflineLocal => Mode == GameMode.OfflineLocal;
    public static bool HasOfflineDeckOverrides =>
        !string.IsNullOrWhiteSpace(OfflinePlayerDeckSelector) ||
        !string.IsNullOrWhiteSpace(OfflineOpponentDeckSelector);

    public static void SetMode(GameMode mode)
    {
        Mode = mode;
    }

    public static void ConfigureOfflineDuel(string playerDeckSelector, string opponentDeckSelector, bool autoStartDuel)
    {
        OfflinePlayerDeckSelector = playerDeckSelector ?? "";
        OfflineOpponentDeckSelector = opponentDeckSelector ?? "";
        AutoStartOfflineDuel = autoStartDuel;
    }

    public static void SetShowLegacyBattleModeChooser(bool showLegacy)
    {
        ShowLegacyBattleModeChooser = showLegacy;
    }

    public static void ClearOfflineDuelConfig()
    {
        OfflinePlayerDeckSelector = "";
        OfflineOpponentDeckSelector = "";
        AutoStartOfflineDuel = false;
    }
}
