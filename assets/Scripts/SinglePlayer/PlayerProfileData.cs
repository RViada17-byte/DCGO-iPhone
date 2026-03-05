using System;
using System.Collections.Generic;

[Serializable]
public class PlayerProfileData
{
    public int SaveVersion = 1;
    public int Currency = 1000;
    public List<string> UnlockedCardIds = new List<string>();
    public List<string> CompletedStoryNodeIds = new List<string>();
    public List<string> CompletedDuelBoardIds = new List<string>();
    public List<string> ClaimedPromoCardIds = new List<string>();
    public bool FirstRunGrantsApplied = false;
}
