using UnityEngine;

public static class OfflineBootRuntime
{
#if DCGO_OFFLINE_BOOT
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ConfigureOfflineBootMode()
    {
        BootstrapConfig.SetMode(GameMode.OfflineLocal);
        BootstrapConfig.ClearOfflineDuelConfig();
        Debug.Log("[OfflineBootRuntime] Enabled offline local boot mode.");
    }
#endif
}
