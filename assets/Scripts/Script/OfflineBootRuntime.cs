using UnityEngine;

public static class OfflineBootRuntime
{
#if DCGO_OFFLINE_BOOT
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ConfigureOfflineBootMode()
    {
        BootstrapConfig.SetMode(GameMode.OfflineLocal);
        BootstrapConfig.ConfigureOfflineDuel("ST1 Demo", "ST2 Demo", true);
        Debug.Log("[OfflineBootRuntime] Enabled offline local boot mode.");
    }
#endif
}
