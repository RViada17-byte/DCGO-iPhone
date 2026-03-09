using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class AISnapshotBuilder
{
    public static AISnapshot Build(GameContext gameContext, Player focusPlayer, AIChosenAction.AIDecisionType decisionType, int turnCount)
    {
        Player opponent = focusPlayer != null ? focusPlayer.Enemy : null;

        AISnapshot snapshot = new AISnapshot
        {
            DecisionType = decisionType,
            TurnCount = turnCount,
            PhaseName = gameContext != null ? gameContext.TurnPhase.ToString() : "",
            Memory = gameContext != null ? gameContext.Memory : 0,
            Self = BuildPlayerView(focusPlayer, true),
            Opponent = BuildPlayerView(opponent, false),
        };

        RefreshDerivedSummaries(snapshot);
        snapshot.StateKey = BuildStateKey(snapshot);
        return snapshot;
    }

    public static void RefreshDerivedSummaries(AISnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        ResetBoardSummary(snapshot.Self);
        ResetBoardSummary(snapshot.Opponent);
        PopulateBoardSummary(snapshot.Self);
        PopulateBoardSummary(snapshot.Opponent);
        snapshot.Race = BuildRaceSummary(snapshot);
    }

    static AISnapshotPlayerView BuildPlayerView(Player player, bool includeHandCards)
    {
        AISnapshotPlayerView view = new AISnapshotPlayerView();

        if (player == null)
        {
            return view;
        }

        view.Name = player.PlayerName;
        view.IsYou = player.isYou;
        view.PlayerId = player.PlayerID;
        view.HandCount = player.HandCards.Count;
        view.TrashCount = player.TrashCards.Count;
        view.SecurityCount = player.SecurityCards.Count;
        view.CanHatch = player.CanHatch;
        view.CanMove = player.CanMove;
        view.MaxMemoryCost = player.MaxMemoryCost;

        if (includeHandCards)
        {
            foreach (CardSource card in player.HandCards)
            {
                view.KnownHandCards.Add(BuildCardView(card));
            }
        }

        foreach (CardSource card in player.TrashCards)
        {
            view.KnownTrashCards.Add(BuildCardView(card));
        }

        foreach (Permanent permanent in player.GetBattleAreaPermanents())
        {
            view.BattlePermanents.Add(BuildPermanentView(permanent, false));
        }

        foreach (Permanent permanent in player.GetBreedingAreaPermanents())
        {
            view.BreedingPermanents.Add(BuildPermanentView(permanent, true));
        }

        return view;
    }

    static AISnapshotCardView BuildCardView(CardSource card)
    {
        if (card == null)
        {
            return new AISnapshotCardView();
        }

        return new AISnapshotCardView
        {
            CardIndex = card.CardIndex,
            CardID = card.CardID,
            Name = card.BaseENGCardNameFromEntity,
            CardKind = card.CardKind,
            Level = card.HasLevel ? card.Level : 0,
            BasePlayCost = card.BasePlayCostFromEntity,
            IsDigimon = card.IsDigimon,
            IsDigiEgg = card.IsDigiEgg,
            IsTamer = card.IsTamer,
            IsOption = card.IsOption,
            CanDeclareSkill = card.CanDeclareSkill,
            OverflowMemory = card.OverflowMemory,
        };
    }

    static AISnapshotPermanentView BuildPermanentView(Permanent permanent, bool inBreeding)
    {
        if (permanent == null || permanent.TopCard == null)
        {
            return new AISnapshotPermanentView();
        }

        return new AISnapshotPermanentView
        {
            FrameID = permanent.PermanentFrame != null ? permanent.PermanentFrame.FrameID : -1,
            Name = permanent.TopCard.BaseENGCardNameFromEntity,
            CardID = permanent.TopCard.CardID,
            TopCardIndex = permanent.TopCard.CardIndex,
            Level = permanent.Level > 1000 ? 0 : permanent.Level,
            DP = permanent.DP,
            IsDigimon = permanent.IsDigimon,
            IsTamer = permanent.IsTamer,
            IsSuspended = permanent.IsSuspended,
            HasBlocker = permanent.HasBlocker,
            CanMove = permanent.CanMove,
            StackCount = permanent.StackCards.Count,
            LinkedCount = permanent.LinkedCards.Count,
            InBreeding = inBreeding,
        };
    }

    static void PopulateBoardSummary(AISnapshotPlayerView view)
    {
        if (view == null)
        {
            return;
        }

        foreach (AISnapshotPermanentView permanent in view.BattlePermanents)
        {
            if (permanent == null)
            {
                continue;
            }

            if (permanent.IsTamer)
            {
                view.BattleTamerCount += 1;
                view.BoardValueScore += 3;
            }

            if (!permanent.IsDigimon)
            {
                continue;
            }

            int level = NormalizeLevel(permanent.Level);
            int stackCount = permanent.StackCount;
            int dp = permanent.DP;

            view.BattleDigimonCount += 1;
            view.TotalBattleDP += dp > 0 ? dp : 0;
            view.HighestBattleDP = view.HighestBattleDP > dp ? view.HighestBattleDP : dp;

            if (permanent.IsSuspended)
            {
                view.SuspendedDigimonCount += 1;
            }
            else
            {
                view.ReadyDigimonCount += 1;
            }

            if (permanent.HasBlocker)
            {
                view.BlockerCount += 1;
            }

            if (IsLargeThreat(level, dp, stackCount))
            {
                view.LargeThreatCount += 1;
            }

            if (IsPremiumThreat(level, dp, stackCount))
            {
                view.PremiumThreatCount += 1;
            }

            view.BoardValueScore += EvaluateBoardValue(level, dp, stackCount, permanent.HasBlocker, permanent.IsSuspended);
            view.CounterPressureScore += EvaluateCounterPressure(level, dp, stackCount);
            if (!permanent.IsSuspended)
            {
                view.ImmediatePressureScore += EvaluateImmediatePressure(level, dp, stackCount);
            }
        }

        foreach (AISnapshotPermanentView permanent in view.BreedingPermanents)
        {
            if (permanent == null || !permanent.IsDigimon)
            {
                continue;
            }

            int level = NormalizeLevel(permanent.Level);
            view.BreedingValueScore += 2
                + level
                + Min(permanent.StackCount, 4)
                + Min(Max(permanent.DP, 0) / 4000, 3);
        }
    }

    static void ResetBoardSummary(AISnapshotPlayerView view)
    {
        if (view == null)
        {
            return;
        }

        view.BattleDigimonCount = 0;
        view.BattleTamerCount = 0;
        view.ReadyDigimonCount = 0;
        view.SuspendedDigimonCount = 0;
        view.BlockerCount = 0;
        view.LargeThreatCount = 0;
        view.PremiumThreatCount = 0;
        view.HighestBattleDP = 0;
        view.TotalBattleDP = 0;
        view.BoardValueScore = 0;
        view.ImmediatePressureScore = 0;
        view.CounterPressureScore = 0;
        view.BreedingValueScore = 0;
    }

    static AISnapshotRaceSummary BuildRaceSummary(AISnapshot snapshot)
    {
        AISnapshotRaceSummary race = new AISnapshotRaceSummary();

        if (snapshot == null)
        {
            return race;
        }

        race.SecurityDelta = snapshot.Self.SecurityCount - snapshot.Opponent.SecurityCount;
        race.BoardDigimonDelta = snapshot.Self.BattleDigimonCount - snapshot.Opponent.BattleDigimonCount;
        race.BoardValueDelta = snapshot.Self.BoardValueScore - snapshot.Opponent.BoardValueScore;
        race.ImmediatePressureDelta = snapshot.Self.ImmediatePressureScore - snapshot.Opponent.ImmediatePressureScore;
        race.CounterPressureDelta = snapshot.Self.CounterPressureScore - snapshot.Opponent.CounterPressureScore;
        race.HasBoardAdvantage = race.BoardValueDelta >= 4 || (race.BoardValueDelta >= 0 && race.BoardDigimonDelta > 0);

        int selfDefensiveBuffer = snapshot.Self.SecurityCount + snapshot.Self.BlockerCount + snapshot.Self.LargeThreatCount;
        race.OpponentCanPunishSlowTurn =
            snapshot.Opponent.CounterPressureScore >= selfDefensiveBuffer
            || (race.SecurityDelta < 0 && race.CounterPressureDelta < 0 && race.BoardValueDelta <= -2);

        race.ShouldStabilize =
            (snapshot.Self.SecurityCount <= 2 && snapshot.Opponent.CounterPressureScore >= selfDefensiveBuffer)
            || (snapshot.Self.SecurityCount <= 3 && race.BoardValueDelta <= -4 && race.CounterPressureDelta < 0)
            || (race.SecurityDelta <= -2 && snapshot.Opponent.CounterPressureScore > snapshot.Self.CounterPressureScore + snapshot.Self.BlockerCount);

        race.ShouldConvertPressure =
            !race.ShouldStabilize
            && snapshot.Self.ImmediatePressureScore >= 2
            && race.HasBoardAdvantage
            && (snapshot.Opponent.SecurityCount <= 3 || race.ImmediatePressureDelta >= 2 || snapshot.Self.PremiumThreatCount > snapshot.Opponent.PremiumThreatCount);

        race.SafeToDevelop =
            !race.ShouldStabilize
            && !race.ShouldConvertPressure
            && !race.OpponentCanPunishSlowTurn
            && (race.HasBoardAdvantage || snapshot.Self.BreedingValueScore >= snapshot.Opponent.BreedingValueScore || snapshot.Opponent.CounterPressureScore <= snapshot.Self.SecurityCount + snapshot.Self.BlockerCount);

        return race;
    }

    static bool IsLargeThreat(int level, int dp, int stackCount)
    {
        return level >= 5 || dp >= 7000 || stackCount >= 3;
    }

    static bool IsPremiumThreat(int level, int dp, int stackCount)
    {
        return level >= 6 || dp >= 10000 || stackCount >= 4;
    }

    static int EvaluateBoardValue(int level, int dp, int stackCount, bool hasBlocker, bool isSuspended)
    {
        int score = 4
            + level
            + Min(stackCount, 4)
            + Min(Max(dp, 0) / 3000, 4);

        if (hasBlocker)
        {
            score += 1;
        }

        if (isSuspended)
        {
            score -= 1;
        }

        return score;
    }

    static int EvaluateImmediatePressure(int level, int dp, int stackCount)
    {
        int score = 1;

        if (level >= 5 || dp >= 7000 || stackCount >= 3)
        {
            score += 1;
        }

        if (level >= 6 || dp >= 10000 || stackCount >= 4)
        {
            score += 1;
        }

        return score;
    }

    static int EvaluateCounterPressure(int level, int dp, int stackCount)
    {
        int score = 1;

        if (level >= 5 || dp >= 7000 || stackCount >= 3)
        {
            score += 1;
        }

        if (level >= 6 || dp >= 10000 || stackCount >= 4)
        {
            score += 1;
        }

        return score;
    }

    static int NormalizeLevel(int level)
    {
        return level > 1000 ? 0 : level;
    }

    static int Max(int left, int right)
    {
        return left > right ? left : right;
    }

    static int Min(int left, int right)
    {
        return left < right ? left : right;
    }

    static string BuildStateKey(AISnapshot snapshot)
    {
        StringBuilder builder = new StringBuilder(256);

        builder.Append(snapshot.DecisionType).Append('|');
        builder.Append(snapshot.TurnCount).Append('|');
        builder.Append(snapshot.PhaseName).Append('|');
        builder.Append(snapshot.Memory).Append('|');
        AppendPlayer(builder, snapshot.Self, true);
        builder.Append('|');
        AppendPlayer(builder, snapshot.Opponent, false);

        return builder.ToString();
    }

    static void AppendPlayer(StringBuilder builder, AISnapshotPlayerView player, bool includeHandCards)
    {
        builder.Append(player.PlayerId).Append(':');
        builder.Append(player.SecurityCount).Append(':');
        builder.Append(player.HandCount).Append(':');
        builder.Append(player.TrashCount).Append(':');
        builder.Append(player.CanHatch ? '1' : '0').Append(':');
        builder.Append(player.CanMove ? '1' : '0').Append(':');

        if (includeHandCards)
        {
            foreach (AISnapshotCardView card in player.KnownHandCards)
            {
                builder.Append(card.CardID).Append(',').Append(card.Level).Append(';');
            }
        }

        builder.Append('|');

        foreach (AISnapshotPermanentView permanent in player.BattlePermanents)
        {
            builder.Append(permanent.FrameID).Append(':');
            builder.Append(permanent.CardID).Append(':');
            builder.Append(permanent.Level).Append(':');
            builder.Append(permanent.DP).Append(':');
            builder.Append(permanent.IsSuspended ? '1' : '0').Append(':');
            builder.Append(permanent.HasBlocker ? '1' : '0').Append(':');
            builder.Append(permanent.StackCount).Append(';');
        }

        builder.Append('|');

        foreach (AISnapshotPermanentView permanent in player.BreedingPermanents)
        {
            builder.Append(permanent.CardID).Append(':');
            builder.Append(permanent.Level).Append(':');
            builder.Append(permanent.DP).Append(':');
            builder.Append(permanent.StackCount).Append(';');
        }
    }
}
