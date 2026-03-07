using System;

[Serializable]
public class StoryUnlockGrantDef
{
    public string kind;
    public string id;
    public string title;
    public string message;

    public StoryUnlockGrantKind Kind => StoryUnlockGrantKindUtility.Parse(kind);
}

[Serializable]
public class StoryEncounterDef
{
    public string id;
    public string title;
    public string role;
    public string enemyDeckCode;
    public string[] enemyCardIds;
    public string enemyProductId;
    public int rewardCurrency;
    public string rewardPromoCardId;
    public string[] prereqEncounterIds;
    public StoryUnlockGrantDef[] unlockGrants;
    public string preDuelSceneId;
    public string postWinSceneId;

    [NonSerialized] public string parentActId;
    [NonSerialized] public string parentWorldId;
    [NonSerialized] public int orderIndex;

    public StoryEncounterRole Role => StoryEncounterRoleUtility.Parse(role);
    public bool IsPlayable =>
        !string.IsNullOrWhiteSpace(enemyDeckCode) ||
        (enemyCardIds != null && enemyCardIds.Length > 0) ||
        !string.IsNullOrWhiteSpace(enemyProductId);
}

[Serializable]
public class StorySceneLineDef
{
    public string speaker;
    public string portraitId;
    public string text;
}

[Serializable]
public class StorySceneDef
{
    public string id;
    public string title;
    public string speaker;
    public string portraitId;
    public StorySceneLineDef[] lines;
}

[Serializable]
public class StoryWorldDef
{
    public string id;
    public string title;
    public bool isAuthored = true;
    public string placeholderMessage;
    public string[] prereqEncounterIds;
    public string[] prereqKeyIds;
    public StoryEncounterDef[] encounters;

    [NonSerialized] public string parentActId;
    [NonSerialized] public int orderIndex;

    public int EncounterCount => encounters?.Length ?? 0;
}

[Serializable]
public class StoryActDef
{
    public string id;
    public string title;
    public string[] prereqEncounterIds;
    public string[] prereqKeyIds;
    public StoryWorldDef[] worlds;

    [NonSerialized] public int orderIndex;

    public int WorldCount => worlds?.Length ?? 0;
}

[Serializable]
public class StoryNodeDef
{
    public string id;
    public string title;
    public string enemyDeckCode;
    public int rewardCurrency;
    public string rewardPromoCardId;
    public string[] prereqNodeIds;
}

[Serializable]
public class StoryDatabaseDef
{
    public StoryActDef[] acts;
    public StorySceneDef[] scenes;
    public StoryNodeDef[] nodes;
}

public enum StoryEncounterRole
{
    Standard = 0,
    Qualifier = 1,
    Gatekeeper = 2,
    Champion = 3,
}

public enum StoryUnlockGrantKind
{
    Unknown = 0,
    ShopSet = 1,
    StoryKey = 2,
    WorldUnlock = 3,
}

public static class StoryEncounterRoleUtility
{
    public static StoryEncounterRole Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return StoryEncounterRole.Standard;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "qualifier":
                return StoryEncounterRole.Qualifier;

            case "gatekeeper":
                return StoryEncounterRole.Gatekeeper;

            case "champion":
                return StoryEncounterRole.Champion;

            default:
                return StoryEncounterRole.Standard;
        }
    }
}

public static class StoryUnlockGrantKindUtility
{
    public static StoryUnlockGrantKind Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return StoryUnlockGrantKind.Unknown;
        }

        string normalized = value.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "shopset":
            case "shop_set":
            case "shop-set":
                return StoryUnlockGrantKind.ShopSet;

            case "storykey":
            case "story_key":
            case "story-key":
            case "key":
                return StoryUnlockGrantKind.StoryKey;

            case "worldunlock":
            case "world_unlock":
            case "world-unlock":
            case "world":
                return StoryUnlockGrantKind.WorldUnlock;

            default:
                return StoryUnlockGrantKind.Unknown;
        }
    }
}
