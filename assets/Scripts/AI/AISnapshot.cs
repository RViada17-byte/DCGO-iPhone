using System.Collections.Generic;
using System.Linq;

public class AISnapshot
{
    public AIChosenAction.AIDecisionType DecisionType { get; set; } = AIChosenAction.AIDecisionType.MainPhase;
    public int TurnCount { get; set; } = 0;
    public string PhaseName { get; set; } = "";
    public int Memory { get; set; } = 0;
    public string StateKey { get; set; } = "";
    public AISnapshotPlayerView Self { get; set; } = new AISnapshotPlayerView();
    public AISnapshotPlayerView Opponent { get; set; } = new AISnapshotPlayerView();
    public AISnapshotRaceSummary Race { get; set; } = new AISnapshotRaceSummary();

    public string SummaryText()
    {
        string breeding = Self.BreedingPermanents.Count > 0
            ? $"{Self.BreedingPermanents[0].Name} L{Self.BreedingPermanents[0].Level}x{Self.BreedingPermanents[0].StackCount}"
            : "none";

        string raceMode = Race.ShouldStabilize
            ? "stabilize"
            : Race.ShouldConvertPressure
                ? "convert"
                : Race.SafeToDevelop
                    ? "develop"
                    : "neutral";

        return $"phase={PhaseName} mem={Memory} selfSec={Self.SecurityCount} oppSec={Opponent.SecurityCount} hand={Self.HandCount} selfBattle={Self.BattlePermanents.Count} oppBattle={Opponent.BattlePermanents.Count} oppBlockers={Opponent.BattlePermanents.Count(permanent => permanent.HasBlocker)} race={raceMode} secΔ={Race.SecurityDelta} boardΔ={Race.BoardValueDelta} pressΔ={Race.ImmediatePressureDelta} breeding={breeding}";
    }

    public int SelfThreatCount => Self.ReadyDigimonCount;
    public int OpponentThreatCount => Opponent.ReadyDigimonCount;
}

public class AISnapshotPlayerView
{
    public string Name { get; set; } = "";
    public bool IsYou { get; set; } = false;
    public int PlayerId { get; set; } = -1;
    public int HandCount { get; set; } = 0;
    public int TrashCount { get; set; } = 0;
    public int SecurityCount { get; set; } = 0;
    public bool CanHatch { get; set; } = false;
    public bool CanMove { get; set; } = false;
    public int MaxMemoryCost { get; set; } = 0;
    public int BattleDigimonCount { get; set; } = 0;
    public int BattleTamerCount { get; set; } = 0;
    public int ReadyDigimonCount { get; set; } = 0;
    public int SuspendedDigimonCount { get; set; } = 0;
    public int BlockerCount { get; set; } = 0;
    public int LargeThreatCount { get; set; } = 0;
    public int PremiumThreatCount { get; set; } = 0;
    public int HighestBattleDP { get; set; } = 0;
    public int TotalBattleDP { get; set; } = 0;
    public int BoardValueScore { get; set; } = 0;
    public int ImmediatePressureScore { get; set; } = 0;
    public int CounterPressureScore { get; set; } = 0;
    public int BreedingValueScore { get; set; } = 0;
    public List<AISnapshotCardView> KnownHandCards { get; private set; } = new List<AISnapshotCardView>();
    public List<AISnapshotCardView> KnownTrashCards { get; private set; } = new List<AISnapshotCardView>();
    public List<AISnapshotPermanentView> BattlePermanents { get; private set; } = new List<AISnapshotPermanentView>();
    public List<AISnapshotPermanentView> BreedingPermanents { get; private set; } = new List<AISnapshotPermanentView>();
}

public class AISnapshotRaceSummary
{
    public int SecurityDelta { get; set; } = 0;
    public int BoardDigimonDelta { get; set; } = 0;
    public int BoardValueDelta { get; set; } = 0;
    public int ImmediatePressureDelta { get; set; } = 0;
    public int CounterPressureDelta { get; set; } = 0;
    public bool HasBoardAdvantage { get; set; } = false;
    public bool OpponentCanPunishSlowTurn { get; set; } = false;
    public bool SafeToDevelop { get; set; } = false;
    public bool ShouldConvertPressure { get; set; } = false;
    public bool ShouldStabilize { get; set; } = false;
}

public class AISnapshotCardView
{
    public int CardIndex { get; set; } = -1;
    public string CardID { get; set; } = "";
    public string Name { get; set; } = "";
    public CardKind CardKind { get; set; } = CardKind.Option;
    public int Level { get; set; } = 0;
    public int BasePlayCost { get; set; } = 0;
    public bool IsDigimon { get; set; } = false;
    public bool IsDigiEgg { get; set; } = false;
    public bool IsTamer { get; set; } = false;
    public bool IsOption { get; set; } = false;
    public bool CanDeclareSkill { get; set; } = false;
    public int OverflowMemory { get; set; } = 0;
}

public class AISnapshotPermanentView
{
    public int FrameID { get; set; } = -1;
    public string Name { get; set; } = "";
    public string CardID { get; set; } = "";
    public int TopCardIndex { get; set; } = -1;
    public int Level { get; set; } = 0;
    public int DP { get; set; } = 0;
    public bool IsDigimon { get; set; } = false;
    public bool IsTamer { get; set; } = false;
    public bool IsSuspended { get; set; } = false;
    public bool HasBlocker { get; set; } = false;
    public bool CanMove { get; set; } = false;
    public int StackCount { get; set; } = 0;
    public int LinkedCount { get; set; } = 0;
    public bool InBreeding { get; set; } = false;
}
