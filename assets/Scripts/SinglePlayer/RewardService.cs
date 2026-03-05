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
            progressionManager.AddCurrency(session.RewardCurrencyOnWin);

            if (!string.IsNullOrWhiteSpace(session.RewardPromoCardIdOnWin))
            {
                bool shouldGrantPromo = !session.RewardPromoOneTime ||
                                        !progressionManager.HasClaimedPromo(session.RewardPromoCardIdOnWin);

                if (shouldGrantPromo)
                {
                    progressionManager.UnlockCard(session.RewardPromoCardIdOnWin);
                    progressionManager.MarkPromoClaimed(session.RewardPromoCardIdOnWin);
                }
            }

            if (session.Mode == SessionMode.Story)
            {
                progressionManager.MarkStoryCompleted(session.ContentId);
            }
            else if (session.Mode == SessionMode.DuelistBoard)
            {
                progressionManager.MarkBoardCompleted(session.ContentId);
            }

            progressionManager.Save();
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
}
