using System.Collections.Generic;
using System.Linq;

public class AIChosenAction
{
    public enum AIDecisionType
    {
        Mulligan,
        Breeding,
        MainPhase,
    }

    public enum AIActionKind
    {
        None,
        KeepHand,
        Mulligan,
        Hatch,
        MoveOut,
        StayHidden,
        EndTurn,
        AttackSecurity,
        AttackDigimon,
        Play,
        Digivolve,
        Jogress,
        Burst,
        AppFusion,
        UseFieldEffect,
        UseHandEffect,
        UseTrashEffect,
    }

    public AIDecisionType DecisionType { get; private set; } = AIDecisionType.MainPhase;
    public AIActionKind ActionKind { get; private set; } = AIActionKind.None;
    public AITurnGoal Goal { get; private set; } = AITurnGoal.ValueSetup;
    public string GoalReason { get; private set; } = "";
    public string Summary { get; private set; } = "None";
    public AIActionScore Score { get; private set; } = new AIActionScore();
    public List<AIActionScore> TopAlternatives { get; private set; } = new List<AIActionScore>();
    public int CardIndex { get; private set; } = -1;
    public int SourcePermanentIndex { get; private set; } = -1;
    public int TargetFrameID { get; private set; } = -1;
    public int AttackTargetPermanentIndex { get; private set; } = -1;
    public int SkillIndex { get; private set; } = -1;
    public int[] JogressEvoRootsFrameIDs { get; private set; } = new int[0];
    public int BurstTamerFrameID { get; private set; } = -1;
    public int[] AppFusionFrameIDs { get; private set; } = new int[0];
    public bool DownstreamResolutionNotControlled { get; private set; } = false;
    public AIActionFingerprint Fingerprint { get; private set; } = null;

    public static AIChosenAction Create(
        AIDecisionType decisionType,
        AIActionKind actionKind,
        string summary,
        AITurnGoal goal = AITurnGoal.ValueSetup,
        AIActionScore score = null,
        List<AIActionScore> topAlternatives = null,
        int cardIndex = -1,
        int sourcePermanentIndex = -1,
        int targetFrameId = -1,
        int attackTargetPermanentIndex = -1,
        int skillIndex = -1,
        int[] jogressEvoRootsFrameIDs = null,
        int burstTamerFrameID = -1,
        int[] appFusionFrameIDs = null,
        bool downstreamResolutionNotControlled = false,
        string goalReason = "")
    {
        int[] normalizedJogressRoots = CopyArray(jogressEvoRootsFrameIDs);
        int[] normalizedAppFusionFrameIds = CopyArray(appFusionFrameIDs);

        return new AIChosenAction
        {
            DecisionType = decisionType,
            ActionKind = actionKind,
            Summary = summary,
            Goal = goal,
            GoalReason = goalReason ?? "",
            Score = score ?? new AIActionScore { ActionSummary = summary },
            TopAlternatives = topAlternatives ?? new List<AIActionScore>(),
            CardIndex = cardIndex,
            SourcePermanentIndex = sourcePermanentIndex,
            TargetFrameID = targetFrameId,
            AttackTargetPermanentIndex = attackTargetPermanentIndex,
            SkillIndex = skillIndex,
            JogressEvoRootsFrameIDs = normalizedJogressRoots,
            BurstTamerFrameID = burstTamerFrameID,
            AppFusionFrameIDs = normalizedAppFusionFrameIds,
            DownstreamResolutionNotControlled = downstreamResolutionNotControlled,
            Fingerprint = AIActionFingerprint.Create(
                decisionType,
                actionKind,
                cardIndex,
                sourcePermanentIndex,
                targetFrameId,
                attackTargetPermanentIndex,
                skillIndex,
                normalizedJogressRoots,
                burstTamerFrameID,
                normalizedAppFusionFrameIds),
        };
    }

    public static AIChosenAction FromCandidate(
        AIMainPhaseCandidate candidate,
        AITurnGoal goal,
        AIActionScore score,
        List<AIActionScore> topAlternatives,
        string goalReason = "")
    {
        if (candidate == null)
        {
            return Create(AIDecisionType.MainPhase, AIActionKind.EndTurn, "End Turn", goal, score, topAlternatives, goalReason: goalReason);
        }

        return Create(
            AIDecisionType.MainPhase,
            candidate.ToActionKind(),
            candidate.Summary,
            goal,
            score,
            topAlternatives,
            candidate.CardIndex,
            candidate.SourcePermanentIndex,
            candidate.TargetFrameID,
            candidate.AttackTargetPermanentIndex,
            candidate.SkillIndex,
            candidate.JogressEvoRootsFrameIDs,
            candidate.BurstTamerFrameID,
            candidate.AppFusionFrameIDs,
            candidate.DownstreamResolutionNotControlled,
            goalReason);
    }

    public bool Matches(AIChosenAction other)
    {
        return AIActionFingerprint.AreEquivalent(Fingerprint, other != null ? other.Fingerprint : null);
    }

    public string ToCompactString()
    {
        return Fingerprint != null ? Fingerprint.ToNormalizedString() : $"{ActionKind}:{Summary}";
    }

    static int[] CopyArray(int[] values)
    {
        if (values == null || values.Length == 0)
        {
            return new int[0];
        }

        int[] copy = new int[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            copy[i] = values[i];
        }

        return copy;
    }
}
