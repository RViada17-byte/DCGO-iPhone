using System;

[Serializable]
public class DuelBoardDuelDef
{
    public string id;
    public string title;
    public string enemyDeckCode;
    public int rewardCurrency;
    public string rewardPromoCardId;
    public bool promoOneTime;
    public string[] prereqStoryNodeIds;
}

[Serializable]
public class DuelBoardDatabaseDef
{
    public DuelBoardDuelDef[] duels;
}
