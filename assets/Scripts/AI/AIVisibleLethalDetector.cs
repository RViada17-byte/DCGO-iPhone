using System.Collections.Generic;
using System.Linq;

public class AIVisibleLethalPlan
{
    public bool RequiresRaise { get; set; } = false;
    public int RequiredDamage { get; set; } = 0;
    public HashSet<string> FirstStepSignatures { get; private set; } = new HashSet<string>();
    public List<string> FirstStepSummaries { get; private set; } = new List<string>();

    public string Reason
    {
        get
        {
            string steps = FirstStepSummaries.Count == 0
                ? "none"
                : string.Join(" | ", FirstStepSummaries.Take(3));
            string prefix = RequiresRaise ? "visible lethal after raise" : "visible lethal on board";
            return $"{prefix}: {steps}";
        }
    }

    public bool IsFirstStep(AIMainPhaseCandidate candidate)
    {
        return candidate != null && FirstStepSignatures.Contains(Signature(candidate));
    }

    public void AddFirstStep(AIMainPhaseCandidate candidate)
    {
        if (candidate == null)
        {
            return;
        }

        string signature = Signature(candidate);
        if (FirstStepSignatures.Add(signature))
        {
            FirstStepSummaries.Add(candidate.Summary);
            FirstStepSummaries.Sort(System.StringComparer.Ordinal);
        }
    }

    static string Signature(AIMainPhaseCandidate candidate)
    {
        return $"{candidate.ActionType}|{candidate.CardIndex}|{candidate.SourcePermanentIndex}|{candidate.TargetFrameID}|{candidate.AttackTargetPermanentIndex}|{candidate.SkillIndex}|{string.Join(",", candidate.JogressEvoRootsFrameIDs)}|{candidate.BurstTamerFrameID}|{string.Join(",", candidate.AppFusionFrameIDs)}";
    }
}

public static class AIVisibleLethalDetector
{
    class SearchState
    {
        public List<int> AttackerIds = new List<int>();
        public Dictionary<int, AIMainPhaseCandidate> SecurityByAttacker = new Dictionary<int, AIMainPhaseCandidate>();
        public Dictionary<int, List<int>> ClearTargetsByAttacker = new Dictionary<int, List<int>>();
        public List<int> ActiveBlockerIds = new List<int>();
    }

    public static AIVisibleLethalPlan FindVisibleLethal(AISnapshot snapshot, IReadOnlyList<AIMainPhaseCandidate> candidates, bool requiresRaise = false)
    {
        if (snapshot == null || candidates == null || candidates.Count == 0)
        {
            return null;
        }

        SearchState state = BuildSearchState(snapshot, candidates);
        if (state.AttackerIds.Count == 0)
        {
            return null;
        }

        int requiredDamage = snapshot.Opponent.SecurityCount + 1;
        if (requiredDamage <= 0)
        {
            requiredDamage = 1;
        }

        ulong activeBlockerMask = MaskForCount(state.ActiveBlockerIds.Count);
        AIVisibleLethalPlan plan = new AIVisibleLethalPlan
        {
            RequiresRaise = requiresRaise,
            RequiredDamage = requiredDamage,
        };

        List<AIMainPhaseCandidate> firstStepCandidates = candidates
            .Where(candidate => candidate != null
                && candidate.SourcePermanentIndex >= 0
                && (candidate.ActionType == AIMainPhaseActionType.AttackSecurity
                    || (candidate.ActionType == AIMainPhaseActionType.AttackDigimon && candidate.TargetIsBlocker)))
            .OrderBy(candidate => candidate.Summary)
            .ToList();

        foreach (AIMainPhaseCandidate candidate in firstStepCandidates)
        {
            int attackerLocalIndex = state.AttackerIds.IndexOf(candidate.SourcePermanentIndex);
            if (attackerLocalIndex < 0)
            {
                continue;
            }

            ulong usedMask = 1UL << attackerLocalIndex;
            bool enablesLethal = false;

            if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
            {
                if (activeBlockerMask == 0UL)
                {
                    enablesLethal = CanFinishLethal(
                        requiredDamage - SecurityDamage(candidate),
                        usedMask,
                        activeBlockerMask,
                        state,
                        new Dictionary<string, bool>());
                }
                else
                {
                    for (int blockerLocalIndex = 0; blockerLocalIndex < state.ActiveBlockerIds.Count; blockerLocalIndex++)
                    {
                        ulong nextBlockerMask = activeBlockerMask & ~(1UL << blockerLocalIndex);
                        if (CanFinishLethal(requiredDamage, usedMask, nextBlockerMask, state, new Dictionary<string, bool>()))
                        {
                            enablesLethal = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                int blockerLocalIndex = state.ActiveBlockerIds.IndexOf(candidate.AttackTargetPermanentIndex);
                if (blockerLocalIndex >= 0)
                {
                    ulong nextBlockerMask = activeBlockerMask & ~(1UL << blockerLocalIndex);
                    enablesLethal = CanFinishLethal(requiredDamage, usedMask, nextBlockerMask, state, new Dictionary<string, bool>());
                }
            }

            if (enablesLethal)
            {
                plan.AddFirstStep(candidate);
            }
        }

        return plan.FirstStepSignatures.Count > 0 ? plan : null;
    }

    static SearchState BuildSearchState(AISnapshot snapshot, IReadOnlyList<AIMainPhaseCandidate> candidates)
    {
        SearchState state = new SearchState();

        for (int i = 0; i < snapshot.Opponent.BattlePermanents.Count; i++)
        {
            AISnapshotPermanentView permanent = snapshot.Opponent.BattlePermanents[i];
            if (permanent != null && permanent.HasBlocker && !permanent.IsSuspended)
            {
                state.ActiveBlockerIds.Add(i);
            }
        }

        List<AIMainPhaseCandidate> securityCandidates = candidates
            .Where(candidate => candidate != null
                && candidate.ActionType == AIMainPhaseActionType.AttackSecurity
                && candidate.SourcePermanentIndex >= 0)
            .GroupBy(candidate => candidate.SourcePermanentIndex)
            .Select(group => group
                .OrderByDescending(candidate => SecurityDamage(candidate))
                .ThenByDescending(candidate => candidate.LikelySafeAttack)
                .ThenByDescending(candidate => candidate.UnlocksAdditionalPressure)
                .ThenBy(candidate => candidate.Summary)
                .First())
            .ToList();

        foreach (AIMainPhaseCandidate candidate in securityCandidates)
        {
            state.SecurityByAttacker[candidate.SourcePermanentIndex] = candidate;
        }

        foreach (AIMainPhaseCandidate candidate in candidates)
        {
            if (candidate == null
                || candidate.ActionType != AIMainPhaseActionType.AttackDigimon
                || !candidate.TargetIsBlocker
                || candidate.SourcePermanentIndex < 0
                || !state.ActiveBlockerIds.Contains(candidate.AttackTargetPermanentIndex))
            {
                continue;
            }

            if (!state.ClearTargetsByAttacker.TryGetValue(candidate.SourcePermanentIndex, out List<int> targets))
            {
                targets = new List<int>();
                state.ClearTargetsByAttacker[candidate.SourcePermanentIndex] = targets;
            }

            if (!targets.Contains(candidate.AttackTargetPermanentIndex))
            {
                targets.Add(candidate.AttackTargetPermanentIndex);
            }
        }

        state.AttackerIds = state.SecurityByAttacker.Keys
            .Concat(state.ClearTargetsByAttacker.Keys)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        return state;
    }

    static bool CanFinishLethal(
        int remainingDamage,
        ulong usedAttackerMask,
        ulong activeBlockerMask,
        SearchState state,
        Dictionary<string, bool> memo)
    {
        if (remainingDamage <= 0)
        {
            return true;
        }

        string memoKey = $"{remainingDamage}|{usedAttackerMask}|{activeBlockerMask}";
        if (memo.TryGetValue(memoKey, out bool cached))
        {
            return cached;
        }

        for (int attackerLocalIndex = 0; attackerLocalIndex < state.AttackerIds.Count; attackerLocalIndex++)
        {
            ulong attackerBit = 1UL << attackerLocalIndex;
            if ((usedAttackerMask & attackerBit) != 0UL)
            {
                continue;
            }

            int attackerId = state.AttackerIds[attackerLocalIndex];

            if (state.SecurityByAttacker.TryGetValue(attackerId, out AIMainPhaseCandidate securityCandidate))
            {
                if (activeBlockerMask == 0UL)
                {
                    if (CanFinishLethal(
                        remainingDamage - SecurityDamage(securityCandidate),
                        usedAttackerMask | attackerBit,
                        activeBlockerMask,
                        state,
                        memo))
                    {
                        memo[memoKey] = true;
                        return true;
                    }
                }
                else
                {
                    for (int blockerLocalIndex = 0; blockerLocalIndex < state.ActiveBlockerIds.Count; blockerLocalIndex++)
                    {
                        ulong blockerBit = 1UL << blockerLocalIndex;
                        if ((activeBlockerMask & blockerBit) == 0UL)
                        {
                            continue;
                        }

                        if (CanFinishLethal(
                            remainingDamage,
                            usedAttackerMask | attackerBit,
                            activeBlockerMask & ~blockerBit,
                            state,
                            memo))
                        {
                            memo[memoKey] = true;
                            return true;
                        }
                    }
                }
            }

            if (!state.ClearTargetsByAttacker.TryGetValue(attackerId, out List<int> clearTargets))
            {
                continue;
            }

            for (int i = 0; i < clearTargets.Count; i++)
            {
                int blockerId = clearTargets[i];
                int blockerLocalIndex = state.ActiveBlockerIds.IndexOf(blockerId);
                if (blockerLocalIndex < 0)
                {
                    continue;
                }

                ulong blockerBit = 1UL << blockerLocalIndex;
                if ((activeBlockerMask & blockerBit) == 0UL)
                {
                    continue;
                }

                if (CanFinishLethal(
                    remainingDamage,
                    usedAttackerMask | attackerBit,
                    activeBlockerMask & ~blockerBit,
                    state,
                    memo))
                {
                    memo[memoKey] = true;
                    return true;
                }
            }
        }

        memo[memoKey] = false;
        return false;
    }

    static int SecurityDamage(AIMainPhaseCandidate candidate)
    {
        return candidate != null && candidate.ImmediateSecurityPressure > 0
            ? candidate.ImmediateSecurityPressure
            : 1;
    }

    static ulong MaskForCount(int count)
    {
        if (count <= 0)
        {
            return 0UL;
        }

        if (count >= 64)
        {
            return ulong.MaxValue;
        }

        return (1UL << count) - 1UL;
    }
}
