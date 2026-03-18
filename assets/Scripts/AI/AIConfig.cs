public class AIConfig
{
    public AIEngineVersion EngineVersion { get; private set; }
    public AIRolloutMode RolloutMode { get; private set; }

    public bool IsShadowEnabled => RolloutMode == AIRolloutMode.ShadowOnly;
    public bool IsLegacyPrimary => EngineVersion == AIEngineVersion.Legacy;
    public bool IsGreedyPrimary => EngineVersion == AIEngineVersion.GreedyShadow;
    public bool IsLegacyShadowComparisonEnabled => IsLegacyPrimary && IsShadowEnabled;

    public AIConfig(AIEngineVersion engineVersion, AIRolloutMode rolloutMode)
    {
        EngineVersion = engineVersion;
        RolloutMode = rolloutMode;
    }

    public static AIConfig CreateDefault(bool isAiMatch, bool isOfflineBotMatch)
    {
        if (isAiMatch && isOfflineBotMatch)
        {
            return new AIConfig(AIEngineVersion.GreedyShadow, AIRolloutMode.Disabled);
        }

        return new AIConfig(AIEngineVersion.Legacy, AIRolloutMode.Disabled);
    }
}
