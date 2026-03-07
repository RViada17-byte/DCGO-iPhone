using System;

[Serializable]
public class DuelBoardDuelDef
{
    public string id;
    public string title;
    public string enemyDeckCode;
    public string[] enemyCardIds;
    public string enemyProductId;
    public int rewardCurrency;
    public string rewardPromoCardId;
    public bool promoOneTime = true;
    public string[] prereqBoardDuelIds;
    public string[] prereqStoryNodeIds;
    public string[] prereqStoryKeyIds;

    [NonSerialized] public string parentActId;
    [NonSerialized] public string parentWorldId;
    [NonSerialized] public int orderIndex;

    public bool IsPlayable =>
        !string.IsNullOrWhiteSpace(enemyDeckCode) ||
        (enemyCardIds != null && enemyCardIds.Length > 0) ||
        !string.IsNullOrWhiteSpace(enemyProductId);
}

[Serializable]
public class DuelBoardWorldDef
{
    public string id;
    public string title;
    public bool isAuthored = true;
    public string placeholderMessage;
    public string[] prereqStoryNodeIds;
    public string[] prereqStoryKeyIds;
    public DuelBoardDuelDef[] duels;

    [NonSerialized] public string parentActId;
    [NonSerialized] public int orderIndex;

    public int DuelCount => duels?.Length ?? 0;
}

[Serializable]
public class DuelBoardActDef
{
    public string id;
    public string title;
    public string[] prereqStoryNodeIds;
    public string[] prereqStoryKeyIds;
    public DuelBoardWorldDef[] worlds;

    [NonSerialized] public int orderIndex;

    public int WorldCount => worlds?.Length ?? 0;
}

[Serializable]
public class DuelBoardDatabaseDef
{
    public DuelBoardActDef[] acts;
    public DuelBoardDuelDef[] duels;
}
