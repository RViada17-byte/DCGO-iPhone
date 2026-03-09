using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LegacyAIBrain : IAIBrain
{
    public string Name => "Legacy";

    public AIChosenAction DecideMulligan(AISnapshot snapshot)
    {
        bool redraw = snapshot != null && snapshot.Self.KnownHandCards.Count(card => card.IsDigimon && card.Level == 3) == 0;

        return AIChosenAction.Create(
            AIChosenAction.AIDecisionType.Mulligan,
            redraw ? AIChosenAction.AIActionKind.Mulligan : AIChosenAction.AIActionKind.KeepHand,
            redraw ? "Mulligan hand (no level 3 Digimon)" : "Keep hand",
            AITurnGoal.ValueSetup,
            new AIActionScore
            {
                ActionSummary = redraw ? "Mulligan" : "Keep Hand",
                TotalScore = redraw ? 1f : 0f,
            });
    }

    public AIChosenAction DecideBreeding(AISnapshot snapshot)
    {
        if (snapshot == null)
        {
            return AIChosenAction.Create(AIChosenAction.AIDecisionType.Breeding, AIChosenAction.AIActionKind.StayHidden, "Stay hidden");
        }

        if (snapshot.Self.CanHatch)
        {
            return AIChosenAction.Create(
                AIChosenAction.AIDecisionType.Breeding,
                AIChosenAction.AIActionKind.Hatch,
                "Hatch Digi-Egg",
                AITurnGoal.BuildStack,
                new AIActionScore
                {
                    ActionSummary = "Hatch Digi-Egg",
                    TotalScore = 1f,
                });
        }

        if (snapshot.Self.CanMove && RandomUtility.IsSucceedProbability(0.85f))
        {
            return AIChosenAction.Create(
                AIChosenAction.AIDecisionType.Breeding,
                AIChosenAction.AIActionKind.MoveOut,
                "Move out from breeding",
                AITurnGoal.ValueSetup,
                new AIActionScore
                {
                    ActionSummary = "Move Out",
                    TotalScore = 1f,
                });
        }

        return AIChosenAction.Create(
            AIChosenAction.AIDecisionType.Breeding,
            AIChosenAction.AIActionKind.StayHidden,
            "Stay hidden in breeding",
            AITurnGoal.BuildStack,
            new AIActionScore
            {
                ActionSummary = "Stay Hidden",
                TotalScore = 1f,
            });
    }

    public AIChosenAction DecideMainPhase(AISnapshot snapshot, IReadOnlyList<AIMainPhaseCandidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return CreateEndTurn();
        }

        if (!RandomUtility.IsSucceedProbability(0.99f))
        {
            return CreateEndTurn();
        }

        List<AIMainPhaseCandidate> candidateList = candidates.ToList();

        List<AIMainPhaseCandidate> attackCandidates = candidateList
            .Where(candidate => candidate.ActionType == AIMainPhaseActionType.AttackSecurity || candidate.ActionType == AIMainPhaseActionType.AttackDigimon)
            .ToList();

        if (attackCandidates.Count > 0)
        {
            List<int> attackerIds = attackCandidates
                .Select(candidate => candidate.SourcePermanentIndex)
                .Distinct()
                .ToList();

            int selectedAttackerIndex = attackerIds[UnityEngine.Random.Range(0, attackerIds.Count)];
            List<AIMainPhaseCandidate> attackerCandidates = attackCandidates
                .Where(candidate => candidate.SourcePermanentIndex == selectedAttackerIndex)
                .ToList();

            List<AIMainPhaseCandidate> defenderCandidates = attackerCandidates
                .Where(candidate => candidate.ActionType == AIMainPhaseActionType.AttackDigimon && candidate.SourceDP >= candidate.TargetDP)
                .ToList();

            bool isSecurityAttack = true;

            if (defenderCandidates.Count > 0 && RandomUtility.IsSucceedProbability(0.5f))
            {
                isSecurityAttack = false;
            }

            if (snapshot != null && snapshot.Opponent.SecurityCount <= 1)
            {
                isSecurityAttack = true;
            }

            if (!isSecurityAttack && defenderCandidates.Count > 0)
            {
                AIMainPhaseCandidate chosenDefense = defenderCandidates[UnityEngine.Random.Range(0, defenderCandidates.Count)];
                return AIChosenAction.FromCandidate(chosenDefense, AITurnGoal.ValueSetup, CreateLegacyScore(chosenDefense.Summary), new List<AIActionScore>());
            }

            AIMainPhaseCandidate chosenSecurity = attackerCandidates.FirstOrDefault(candidate => candidate.ActionType == AIMainPhaseActionType.AttackSecurity);
            if (chosenSecurity != null)
            {
                return AIChosenAction.FromCandidate(chosenSecurity, AITurnGoal.ValueSetup, CreateLegacyScore(chosenSecurity.Summary), new List<AIActionScore>());
            }
        }

        List<AIMainPhaseCandidate> playCandidates = candidateList
            .Where(candidate => candidate.ActionType == AIMainPhaseActionType.Play || candidate.ActionType == AIMainPhaseActionType.Digivolve)
            .ToList();

        List<IGrouping<int, AIMainPhaseCandidate>> groupedPlayCandidates = playCandidates
            .GroupBy(candidate => candidate.CardIndex)
            .OrderBy(group => Array.IndexOf(DataBase.cardKinds, group.First().SourceCardKind))
            .ThenBy(group => group.Any(candidate => candidate.TargetsOccupiedFrame) ? 0 : 1)
            .ToList();

        foreach (IGrouping<int, AIMainPhaseCandidate> group in groupedPlayCandidates)
        {
            if (!RandomUtility.IsSucceedProbability(0.99f))
            {
                continue;
            }

            List<AIMainPhaseCandidate> weightedCandidates = new List<AIMainPhaseCandidate>();

            foreach (AIMainPhaseCandidate candidate in group)
            {
                int weight = candidate.TargetsOccupiedFrame ? 4 : 1;

                for (int i = 0; i < weight; i++)
                {
                    weightedCandidates.Add(candidate);
                }
            }

            if (weightedCandidates.Count == 0)
            {
                continue;
            }

            AIMainPhaseCandidate chosenCandidate = weightedCandidates[UnityEngine.Random.Range(0, weightedCandidates.Count)];
            return AIChosenAction.FromCandidate(chosenCandidate, AITurnGoal.ValueSetup, CreateLegacyScore(chosenCandidate.Summary), new List<AIActionScore>());
        }

        return CreateEndTurn();
    }

    static AIActionScore CreateLegacyScore(string summary)
    {
        AIActionScore score = new AIActionScore
        {
            ActionSummary = summary,
            TotalScore = 0f,
        };
        score.Breakdown.Add("legacy");
        return score;
    }

    static AIChosenAction CreateEndTurn()
    {
        return AIChosenAction.Create(
            AIChosenAction.AIDecisionType.MainPhase,
            AIChosenAction.AIActionKind.EndTurn,
            "End Turn",
            AITurnGoal.MemoryChoke,
            CreateLegacyScore("End Turn"));
    }
}
