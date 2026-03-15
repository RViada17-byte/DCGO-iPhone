using System.Collections.Generic;

public static class RewardService
{
    private static Player _latestWinner;

    public static void SetLatestWinner(Player winner)
    {
        _latestWinner = winner;
    }

    public static void GrantWinRewardsIfEligible()
    {
        GameSessionContext session = GameSessionContext.Instance;
        if (session.Mode == SessionMode.None)
        {
            return;
        }

        if (session.RewardsGranted)
        {
            return;
        }

        Player winner = ResolveWinner();
        bool playerWon = GManager.instance != null && winner == GManager.instance.You;

        if (playerWon)
        {
            ProgressionManager progressionManager = ProgressionManager.Instance;
            progressionManager.AddCurrency(session.RewardCurrencyOnWin, saveImmediately: false);

            if (!string.IsNullOrWhiteSpace(session.RewardPromoCardIdOnWin))
            {
                bool shouldGrantPromo = !session.RewardPromoOneTime ||
                                        !progressionManager.HasClaimedPromo(session.RewardPromoCardIdOnWin);

                if (shouldGrantPromo)
                {
                    progressionManager.UnlockCanonicalPrint(session.RewardPromoCardIdOnWin, saveImmediately: false);
                    progressionManager.MarkPromoClaimed(session.RewardPromoCardIdOnWin, saveImmediately: false);
                }
            }

            if (session.Mode == SessionMode.Story)
            {
                bool firstClear = progressionManager.MarkStoryCompleted(session.ContentId, saveImmediately: false);
                ApplyStoryUnlockGrants(session, progressionManager, firstClear);
            }
            else if (session.Mode == SessionMode.DuelistBoard)
            {
                progressionManager.MarkBoardCompleted(session.ContentId, saveImmediately: false);
            }

            progressionManager.Save("duel reward commit");
        }

        session.MarkRewardsGranted();
        _latestWinner = null;
    }

    private static Player ResolveWinner()
    {
        if (_latestWinner != null)
        {
            return _latestWinner;
        }

        if (GManager.instance?.turnStateMachine?.gameContext?.Players == null)
        {
            return null;
        }

        foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players)
        {
            if (player != null && player.IsLose && player.Enemy != null)
            {
                return player.Enemy;
            }
        }

        return null;
    }

    private static void ApplyStoryUnlockGrants(GameSessionContext session, ProgressionManager progressionManager, bool firstClear)
    {
        if (!firstClear || session == null || progressionManager == null)
        {
            return;
        }

        StoryEncounterDef encounter = StoryDatabase.Instance.GetEncounter(session.ContentId);
        if (encounter != null && !string.IsNullOrWhiteSpace(encounter.postWinSceneId))
        {
            session.SetPendingStoryScene(encounter.postWinSceneId);
        }

        if (encounter?.unlockGrants == null || encounter.unlockGrants.Length == 0)
        {
            return;
        }

        List<string> summaryLines = new List<string>();
        for (int index = 0; index < encounter.unlockGrants.Length; index++)
        {
            StoryUnlockGrantDef grant = encounter.unlockGrants[index];
            if (grant == null)
            {
                continue;
            }

            string message = BuildGrantMessage(grant);
            switch (grant.Kind)
            {
                case StoryUnlockGrantKind.StoryKey:
                    if (progressionManager.EarnStoryKey(grant.id, saveImmediately: false) && !string.IsNullOrWhiteSpace(message))
                    {
                        summaryLines.Add(message);
                    }
                    break;

                case StoryUnlockGrantKind.ShopSet:
                case StoryUnlockGrantKind.WorldUnlock:
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        summaryLines.Add(message);
                    }
                    break;
            }
        }

        session.SetPendingStoryRewardLines(summaryLines);
    }

    private static string BuildGrantMessage(StoryUnlockGrantDef grant)
    {
        if (grant == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(grant.message))
        {
            return grant.message.Trim();
        }

        string title = string.IsNullOrWhiteSpace(grant.title) ? grant.id : grant.title;
        switch (grant.Kind)
        {
            case StoryUnlockGrantKind.ShopSet:
                return string.IsNullOrWhiteSpace(title) ? string.Empty : $"Shop unlocked: {title}";

            case StoryUnlockGrantKind.StoryKey:
                return string.IsNullOrWhiteSpace(title) ? string.Empty : $"Key obtained: {title}";

            case StoryUnlockGrantKind.WorldUnlock:
                return string.IsNullOrWhiteSpace(title) ? string.Empty : $"World unlocked: {title}";

            default:
                return string.IsNullOrWhiteSpace(title) ? string.Empty : title;
        }
    }
}
