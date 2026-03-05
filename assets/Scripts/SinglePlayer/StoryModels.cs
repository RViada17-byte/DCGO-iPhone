using System;

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
    public StoryNodeDef[] nodes;
}
