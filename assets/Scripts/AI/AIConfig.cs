public class AIConfig
{
    public AIEngineVersion EngineVersion { get; private set; }
    public AIRolloutMode RolloutMode { get; private set; }

    public bool IsShadowEnabled => RolloutMode == AIRolloutMode.ShadowOnly;

    public AIConfig(AIEngineVersion engineVersion, AIRolloutMode rolloutMode)
    {
        EngineVersion = engineVersion;
        RolloutMode = rolloutMode;
    }

    public static AIConfig CreateDefault(bool isAiMatch, bool isOfflineBotMatch)
    {
        if (isAiMatch && isOfflineBotMatch)
        {
            return new AIConfig(AIEngineVersion.Legacy, AIRolloutMode.ShadowOnly);
        }

        return new AIConfig(AIEngineVersion.Legacy, AIRolloutMode.Disabled);
    }
}
