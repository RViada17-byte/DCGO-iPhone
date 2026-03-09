using System;
using System.Linq;

public sealed class AIActionFingerprint : IEquatable<AIActionFingerprint>
{
    public AIChosenAction.AIDecisionType DecisionType { get; private set; }
    public AIChosenAction.AIActionKind ActionKind { get; private set; }
    public int CardIndex { get; private set; }
    public int SourcePermanentIndex { get; private set; }
    public int TargetFrameID { get; private set; }
    public int AttackTargetPermanentIndex { get; private set; }
    public int SkillIndex { get; private set; }
    public int[] JogressEvoRootsFrameIDs { get; private set; } = new int[0];
    public int BurstTamerFrameID { get; private set; }
    public int[] AppFusionFrameIDs { get; private set; } = new int[0];

    public static AIActionFingerprint Create(
        AIChosenAction.AIDecisionType decisionType,
        AIChosenAction.AIActionKind actionKind,
        int cardIndex = -1,
        int sourcePermanentIndex = -1,
        int targetFrameId = -1,
        int attackTargetPermanentIndex = -1,
        int skillIndex = -1,
        int[] jogressEvoRootsFrameIDs = null,
        int burstTamerFrameID = -1,
        int[] appFusionFrameIDs = null)
    {
        int[] normalizedJogressRoots = NormalizeJogressRoots(jogressEvoRootsFrameIDs);
        int[] normalizedAppFusionFrameIds = CopyArray(appFusionFrameIDs);
        int canonicalTargetFrameId = NormalizeTargetFrameId(actionKind, targetFrameId, normalizedJogressRoots, normalizedAppFusionFrameIds);

        return new AIActionFingerprint
        {
            DecisionType = decisionType,
            ActionKind = actionKind,
            CardIndex = cardIndex,
            SourcePermanentIndex = sourcePermanentIndex,
            TargetFrameID = canonicalTargetFrameId,
            AttackTargetPermanentIndex = attackTargetPermanentIndex,
            SkillIndex = skillIndex,
            JogressEvoRootsFrameIDs = normalizedJogressRoots,
            BurstTamerFrameID = burstTamerFrameID,
            AppFusionFrameIDs = normalizedAppFusionFrameIds,
        };
    }

    public static AIActionFingerprint FromChosenAction(AIChosenAction action)
    {
        if (action == null)
        {
            return null;
        }

        return Create(
            action.DecisionType,
            action.ActionKind,
            action.CardIndex,
            action.SourcePermanentIndex,
            action.TargetFrameID,
            action.AttackTargetPermanentIndex,
            action.SkillIndex,
            action.JogressEvoRootsFrameIDs,
            action.BurstTamerFrameID,
            action.AppFusionFrameIDs);
    }

    public static bool AreEquivalent(AIActionFingerprint left, AIActionFingerprint right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        return left.Equals(right);
    }

    public string ToNormalizedString()
    {
        return $"{DecisionType}|{ActionKind}|card={CardIndex}|source={SourcePermanentIndex}|target={TargetFrameID}|attack={AttackTargetPermanentIndex}|skill={SkillIndex}|jogress=[{string.Join(",", JogressEvoRootsFrameIDs)}]|burst={BurstTamerFrameID}|app=[{string.Join(",", AppFusionFrameIDs)}]";
    }

    public bool Equals(AIActionFingerprint other)
    {
        if (other == null)
        {
            return false;
        }

        return DecisionType == other.DecisionType
            && ActionKind == other.ActionKind
            && CardIndex == other.CardIndex
            && SourcePermanentIndex == other.SourcePermanentIndex
            && TargetFrameID == other.TargetFrameID
            && AttackTargetPermanentIndex == other.AttackTargetPermanentIndex
            && SkillIndex == other.SkillIndex
            && BurstTamerFrameID == other.BurstTamerFrameID
            && JogressEvoRootsFrameIDs.SequenceEqual(other.JogressEvoRootsFrameIDs)
            && AppFusionFrameIDs.SequenceEqual(other.AppFusionFrameIDs);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as AIActionFingerprint);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)DecisionType;
            hash = (hash * 397) ^ (int)ActionKind;
            hash = (hash * 397) ^ CardIndex;
            hash = (hash * 397) ^ SourcePermanentIndex;
            hash = (hash * 397) ^ TargetFrameID;
            hash = (hash * 397) ^ AttackTargetPermanentIndex;
            hash = (hash * 397) ^ SkillIndex;
            hash = (hash * 397) ^ BurstTamerFrameID;

            for (int i = 0; i < JogressEvoRootsFrameIDs.Length; i++)
            {
                hash = (hash * 397) ^ JogressEvoRootsFrameIDs[i];
            }

            for (int i = 0; i < AppFusionFrameIDs.Length; i++)
            {
                hash = (hash * 397) ^ AppFusionFrameIDs[i];
            }

            return hash;
        }
    }

    static int[] NormalizeJogressRoots(int[] values)
    {
        int[] copy = CopyArray(values);
        Array.Sort(copy);
        return copy;
    }

    static int NormalizeTargetFrameId(AIChosenAction.AIActionKind actionKind, int targetFrameId, int[] jogressRoots, int[] appFusionFrameIds)
    {
        if (actionKind == AIChosenAction.AIActionKind.Jogress && jogressRoots.Length > 0)
        {
            return jogressRoots[0];
        }

        if (actionKind == AIChosenAction.AIActionKind.AppFusion && appFusionFrameIds.Length > 0)
        {
            return appFusionFrameIds[0];
        }

        return targetFrameId;
    }

    static int[] CopyArray(int[] values)
    {
        if (values == null || values.Length == 0)
        {
            return new int[0];
        }

        int[] copy = new int[values.Length];
        Array.Copy(values, copy, values.Length);
        return copy;
    }
}
