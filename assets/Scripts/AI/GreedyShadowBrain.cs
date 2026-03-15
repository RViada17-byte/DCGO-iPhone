using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GreedyShadowBrain : IAIBrain
{
    class GoalSelection
    {
        public AITurnGoal Goal = AITurnGoal.ValueSetup;
        public string Reason = "";
    }

    class ScoredCandidate
    {
        public AIMainPhaseCandidate Candidate;
        public AIActionScore Score;
    }

    public string Name => "GreedyShadow";

    public AIChosenAction DecideMulligan(AISnapshot snapshot)
    {
        AIActionScore keepScore = ScoreKeepHand(snapshot);
        AIActionScore redrawScore = ScoreRedraw(snapshot, keepScore.TotalScore);
        bool redraw = redrawScore.TotalScore > keepScore.TotalScore;

        List<AIActionScore> alternatives = new List<AIActionScore>
        {
            redraw ? keepScore : redrawScore,
        };

        return AIChosenAction.Create(
            AIChosenAction.AIDecisionType.Mulligan,
            redraw ? AIChosenAction.AIActionKind.Mulligan : AIChosenAction.AIActionKind.KeepHand,
            redraw ? "Mulligan hand" : "Keep hand",
            AITurnGoal.ValueSetup,
            redraw ? redrawScore : keepScore,
            alternatives,
            goalReason: redraw
                ? "opener lacks enough early curve support"
                : "opener already has workable early curve");
    }

    public AIChosenAction DecideBreeding(AISnapshot snapshot, GameContext gameContext = null, Player player = null)
    {
        List<Tuple<AIChosenAction, AIActionScore>> options = new List<Tuple<AIChosenAction, AIActionScore>>();

        if (snapshot != null && snapshot.Self.CanHatch)
        {
            AIActionScore hatchScore = CreateScore("Hatch Digi-Egg");
            AddScore(hatchScore, 120f, "build stack");
            AddScore(hatchScore, 20f, "always hatch when empty");
            options.Add(Tuple.Create(
                AIChosenAction.Create(AIChosenAction.AIDecisionType.Breeding, AIChosenAction.AIActionKind.Hatch, "Hatch Digi-Egg", AITurnGoal.BuildStack, hatchScore, goalReason: "breeding area is empty and hatch is available"),
                hatchScore));
        }

        if (snapshot != null && snapshot.Self.CanMove)
        {
            AIVisibleLethalPlan boardLethalPlan = null;
            AIVisibleLethalPlan raiseLethalPlan = null;
            if (gameContext != null && player != null)
            {
                List<AIMainPhaseCandidate> boardCandidates = AIMainPhaseCandidateBuilder.Build(gameContext, player);
                List<AIMainPhaseCandidate> projectedCandidates = AIMainPhaseCandidateBuilder.BuildForProjectedMoveOut(gameContext, player);
                boardLethalPlan = AIVisibleLethalDetector.FindVisibleLethal(snapshot, boardCandidates);
                raiseLethalPlan = AIVisibleLethalDetector.FindVisibleLethal(snapshot, projectedCandidates, requiresRaise: true);
            }

            if (boardLethalPlan == null && raiseLethalPlan != null)
            {
                AIActionScore lethalMoveScore = CreateScore("Move out from breeding");
                AddScore(lethalMoveScore, 120000f, "raise reveals visible lethal");
                AddScore(lethalMoveScore, 500f, raiseLethalPlan.Reason);

                AIActionScore lethalStayScore = CreateScore("Stay hidden in breeding");
                AddScore(lethalStayScore, -120000f, "stay hidden gives up visible lethal");

                return AIChosenAction.Create(
                    AIChosenAction.AIDecisionType.Breeding,
                    AIChosenAction.AIActionKind.MoveOut,
                    "Move out from breeding",
                    AITurnGoal.CloseGame,
                    lethalMoveScore,
                    new List<AIActionScore> { lethalStayScore },
                    goalReason: raiseLethalPlan.Reason);
            }

            int breedingValue = snapshot.Self.BreedingPermanents.Count > 0
                ? snapshot.Self.BreedingPermanents[0].StackCount + snapshot.Self.BreedingPermanents[0].Level
                : 0;

            AIActionScore moveScore = CreateScore("Move out from breeding");
            AddScore(moveScore, 60f, "develop board");
            AddScore(moveScore, snapshot.SelfThreatCount <= snapshot.OpponentThreatCount ? 20f : 50f, "pressure");
            AddScore(moveScore, breedingValue >= 8 ? -50f : 10f, "premium stack caution");

            AIActionScore stayScore = CreateScore("Stay hidden in breeding");
            AddScore(stayScore, breedingValue >= 8 ? 140f : 50f, "protect breeding stack");
            AddScore(stayScore, snapshot.Self.SecurityCount <= 3 && snapshot.OpponentThreatCount > snapshot.SelfThreatCount ? 80f : -10f, "respect losing race");

            options.Add(Tuple.Create(
                AIChosenAction.Create(AIChosenAction.AIDecisionType.Breeding, AIChosenAction.AIActionKind.MoveOut, "Move out from breeding", AITurnGoal.ValueSetup, moveScore, goalReason: "board pressure matters more than hiding the breeding stack"),
                moveScore));
            options.Add(Tuple.Create(
                AIChosenAction.Create(AIChosenAction.AIDecisionType.Breeding, AIChosenAction.AIActionKind.StayHidden, "Stay hidden in breeding", AITurnGoal.BuildStack, stayScore, goalReason: "breeding stack is valuable enough to protect this turn"),
                stayScore));
        }

        if (options.Count == 0)
        {
            AIActionScore stayHidden = CreateScore("Stay hidden in breeding");
            return AIChosenAction.Create(AIChosenAction.AIDecisionType.Breeding, AIChosenAction.AIActionKind.StayHidden, "Stay hidden in breeding", AITurnGoal.BuildStack, stayHidden, goalReason: "no breeding action is available");
        }

        Tuple<AIChosenAction, AIActionScore> best = options
            .OrderByDescending(option => option.Item2.TotalScore)
            .ThenBy(option => option.Item1.Summary)
            .First();

        List<AIActionScore> alternatives = options
            .Where(option => option != best)
            .Select(option => option.Item2)
            .Take(3)
            .ToList();

        return AIChosenAction.Create(
            best.Item1.DecisionType,
            best.Item1.ActionKind,
            best.Item1.Summary,
            best.Item1.Goal,
            best.Item2,
            alternatives,
            goalReason: best.Item1.GoalReason);
    }

    public AIChosenAction DecideMainPhase(AISnapshot snapshot, IReadOnlyList<AIMainPhaseCandidate> candidates)
    {
        List<AIMainPhaseCandidate> candidateList = candidates != null
            ? candidates.ToList()
            : new List<AIMainPhaseCandidate>();

        if (candidateList.Count == 0)
        {
            AIActionScore endTurnScore = CreateScore("End Turn");
            return AIChosenAction.Create(AIChosenAction.AIDecisionType.MainPhase, AIChosenAction.AIActionKind.EndTurn, "End Turn", AITurnGoal.MemoryChoke, endTurnScore, goalReason: "no legal main-phase candidate exists");
        }

        AIVisibleLethalPlan visibleLethalPlan = AIVisibleLethalDetector.FindVisibleLethal(snapshot, candidateList);
        GoalSelection goalSelection = SelectGoal(snapshot, candidateList, visibleLethalPlan);
        AITurnGoal goal = goalSelection.Goal;
        List<ScoredCandidate> scoredCandidates = candidateList
            .Select(candidate => new ScoredCandidate
            {
                Candidate = candidate,
                Score = ScoreCandidate(snapshot, goal, candidate, candidateList, visibleLethalPlan),
            })
            .OrderByDescending(result => result.Score.TotalScore)
            .ThenBy(result => result.Candidate.Summary)
            .ToList();

        ScoredCandidate best = scoredCandidates.First();
        List<AIActionScore> topAlternatives = scoredCandidates
            .Skip(1)
            .Take(3)
            .Select(result => result.Score)
            .ToList();

        return AIChosenAction.FromCandidate(best.Candidate, goal, best.Score, topAlternatives, goalSelection.Reason);
    }

    static GoalSelection SelectGoal(AISnapshot snapshot, List<AIMainPhaseCandidate> candidates, AIVisibleLethalPlan visibleLethalPlan)
    {
        if (snapshot == null)
        {
            return Goal(AITurnGoal.ValueSetup, "snapshot missing, default to generic value setup");
        }

        if (visibleLethalPlan != null)
        {
            return Goal(AITurnGoal.CloseGame, visibleLethalPlan.Reason);
        }

        bool hasSecurityPressure = candidates.Any(candidate => candidate.ActionType == AIMainPhaseActionType.AttackSecurity);
        bool hasRemoval = candidates.Any(candidate => candidate.ActionType == AIMainPhaseActionType.AttackDigimon || candidate.ActionType == AIMainPhaseActionType.UseFieldEffect || candidate.ActionType == AIMainPhaseActionType.UseHandEffect || candidate.ActionType == AIMainPhaseActionType.UseTrashEffect);
        bool hasDevelopment = candidates.Any(candidate => candidate.ActionType == AIMainPhaseActionType.Play || candidate.ActionType == AIMainPhaseActionType.Digivolve || candidate.ActionType == AIMainPhaseActionType.Jogress || candidate.ActionType == AIMainPhaseActionType.Burst || candidate.ActionType == AIMainPhaseActionType.AppFusion);
        bool hasStackDevelopment = candidates.Any(candidate => candidate.ActionType == AIMainPhaseActionType.Digivolve || candidate.ActionType == AIMainPhaseActionType.Jogress || candidate.ActionType == AIMainPhaseActionType.Burst || candidate.ActionType == AIMainPhaseActionType.AppFusion);
        bool hasMemoryChokeLine = candidates.Any(candidate => candidate.ActionType != AIMainPhaseActionType.EndTurn && Mathf.Abs(candidate.ProjectedMemory) <= 1);
        bool meaningfulBreedingStack = HasMeaningfulBreedingStack(snapshot);
        bool behindBoardRace = IsBehindBoardRace(snapshot);
        bool hasStrongSafePressure = HasStrongSafePressure(snapshot, candidates);

        if (snapshot.Opponent.SecurityCount <= 1 && hasSecurityPressure)
        {
            return Goal(AITurnGoal.CloseGame, "opponent is at one security and a security attack can close the game");
        }

        if (snapshot.Race.ShouldStabilize)
        {
            return Goal(AITurnGoal.Stabilize, "race summary says we are behind and need to stabilize");
        }

        if (snapshot.Race.ShouldConvertPressure && hasRemoval && snapshot.Opponent.BlockerCount > 0)
        {
            return Goal(AITurnGoal.TempoClear, "existing pressure is real but a blocker or obstacle must be cleared first");
        }

        if (snapshot.Self.BreedingPermanents.Count > 0
            && hasDevelopment
            && hasStackDevelopment
            && meaningfulBreedingStack
            && snapshot.Race.SafeToDevelop
            && !hasStrongSafePressure
            && !behindBoardRace)
        {
            return Goal(AITurnGoal.BuildStack, "valuable breeding stack, safe development window, no strong safe pressure line, and board race is not behind");
        }

        if (hasRemoval && (snapshot.Opponent.BattlePermanents.Count > snapshot.Self.BattlePermanents.Count || snapshot.Race.BoardValueDelta < 0 || behindBoardRace))
        {
            return Goal(AITurnGoal.TempoClear, "board race is behind and removal is available to recover tempo");
        }

        if (hasMemoryChokeLine && !snapshot.Race.ShouldConvertPressure)
        {
            return Goal(AITurnGoal.MemoryChoke, "no urgent pressure conversion and a tight memory line is available");
        }

        if (snapshot.Race.ShouldConvertPressure && hasSecurityPressure)
        {
            return Goal(AITurnGoal.ValueSetup, "board pressure is already favorable, so use a generic value line instead of defaulting into stack building");
        }

        return Goal(AITurnGoal.ValueSetup, "default value setup fallback after no higher-priority goal condition fired");
    }

    static AIActionScore ScoreCandidate(AISnapshot snapshot, AITurnGoal goal, AIMainPhaseCandidate candidate, IReadOnlyList<AIMainPhaseCandidate> candidates, AIVisibleLethalPlan visibleLethalPlan)
    {
        AIActionScore score = CreateScore(candidate.Summary);
        score.DownstreamResolutionNotControlled = candidate.DownstreamResolutionNotControlled;

        ApplyHardRules(snapshot, goal, candidate, score, visibleLethalPlan);
        ApplyGoalBias(goal, candidate, score);
        ApplyGenericHeuristics(snapshot, candidate, score);
        ApplyRaceHeuristics(snapshot, candidate, score);
        ApplyAttackOrderHeuristics(snapshot, goal, candidate, candidates, score);

        if (candidate.DownstreamResolutionNotControlled)
        {
            AddScore(score, -15f, "downstream unresolved");
        }

        return score;
    }

    static void ApplyAttackOrderHeuristics(AISnapshot snapshot, AITurnGoal goal, AIMainPhaseCandidate candidate, IReadOnlyList<AIMainPhaseCandidate> candidates, AIActionScore score)
    {
        if (snapshot == null
            || candidate == null
            || candidates == null
            || (candidate.ActionType != AIMainPhaseActionType.AttackSecurity && candidate.ActionType != AIMainPhaseActionType.AttackDigimon))
        {
            return;
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
        {
            bool cheaperSecurityAlternative = HasCheaperSecurityPressureAlternative(candidate, candidates);

            if (IsPremiumAttackCandidate(candidate)
                && cheaperSecurityAlternative
                && snapshot.Opponent.SecurityCount > 1
                && !candidate.UnlocksAdditionalPressure)
            {
                AddScore(score, -85f, "save premium stack for bigger swing");
            }

            if (!IsPremiumAttackCandidate(candidate)
                && HasHigherValueSecurityFollowUp(candidate, candidates)
                && candidate.LikelySafeAttack)
            {
                AddScore(score, 60f, "probe with expendable attacker first");
            }
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon)
        {
            bool blockerClearUnlocksPressure = DoesBlockerClearUnlockMeaningfulPressure(snapshot, candidate, candidates);

            if (candidate.TargetIsBlocker)
            {
                if (blockerClearUnlocksPressure)
                {
                    AddScore(score, 70f, "blocker clear unlocks meaningful pressure");
                }
                else if (goal != AITurnGoal.TempoClear && goal != AITurnGoal.Stabilize)
                {
                    AddScore(score, -75f, "blocker clear without pressure payoff");
                }
            }

            if (IsPremiumAttackCandidate(candidate)
                && IsLowValueAttackTarget(candidate)
                && HasCheaperEquivalentAttack(candidate, candidates))
            {
                AddScore(score, -90f, "cheaper attacker handles same target");
            }

            if (!IsPremiumAttackCandidate(candidate)
                && IsLowValueAttackTarget(candidate)
                && HasHigherValueEquivalentAttack(candidate, candidates)
                && candidate.LikelySafeAttack)
            {
                AddScore(score, 50f, "preserve premium stack for later");
            }
        }
    }

    static void ApplyHardRules(AISnapshot snapshot, AITurnGoal goal, AIMainPhaseCandidate candidate, AIActionScore score, AIVisibleLethalPlan visibleLethalPlan)
    {
        if (snapshot == null)
        {
            return;
        }

        bool opponentHasBlocker = snapshot.Opponent.BlockerCount > 0;

        if (visibleLethalPlan != null)
        {
            if (visibleLethalPlan.IsFirstStep(candidate))
            {
                AddScore(score, 90000f, "visible lethal first step");
            }
            else if (IsNonLethalSetupValueAction(candidate))
            {
                AddScore(score, -12000f, "visible lethal exists, do not set up");
            }
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity && snapshot.Opponent.SecurityCount <= 1)
        {
            AddScore(score, 100000f, "take lethal");
        }

        if (goal == AITurnGoal.CloseGame && opponentHasBlocker && candidate.ActionType == AIMainPhaseActionType.AttackDigimon && candidate.TargetIsBlocker)
        {
            AddScore(score, 25000f, "clear blocker before lethal");
        }

        if (goal == AITurnGoal.Stabilize && candidate.ActionType == AIMainPhaseActionType.EndTurn)
        {
            AddScore(score, -5000f, "do not pass losing race");
        }

        if (ShouldProtectPremiumStack(snapshot, candidate))
        {
            AddScore(score, -120f, "protect premium stack from visible risk");
        }
    }

    static bool IsNonLethalSetupValueAction(AIMainPhaseCandidate candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        return candidate.ActionType == AIMainPhaseActionType.EndTurn
            || candidate.ActionType == AIMainPhaseActionType.Play
            || candidate.ActionType == AIMainPhaseActionType.Digivolve
            || candidate.ActionType == AIMainPhaseActionType.Jogress
            || candidate.ActionType == AIMainPhaseActionType.Burst
            || candidate.ActionType == AIMainPhaseActionType.AppFusion
            || candidate.ActionType == AIMainPhaseActionType.UseFieldEffect
            || candidate.ActionType == AIMainPhaseActionType.UseHandEffect
            || candidate.ActionType == AIMainPhaseActionType.UseTrashEffect;
    }

    static void ApplyGoalBias(AITurnGoal goal, AIMainPhaseCandidate candidate, AIActionScore score)
    {
        switch (goal)
        {
            case AITurnGoal.CloseGame:
                if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
                {
                    AddScore(score, 600f, "close game");
                }
                break;

            case AITurnGoal.Stabilize:
                if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon || candidate.ActionType == AIMainPhaseActionType.UseFieldEffect || candidate.ActionType == AIMainPhaseActionType.UseHandEffect || candidate.ActionType == AIMainPhaseActionType.UseTrashEffect)
                {
                    AddScore(score, 220f, "stabilize");
                }
                break;

            case AITurnGoal.BuildStack:
                if (candidate.ActionType == AIMainPhaseActionType.Digivolve || candidate.ActionType == AIMainPhaseActionType.Jogress || candidate.ActionType == AIMainPhaseActionType.Burst || candidate.ActionType == AIMainPhaseActionType.AppFusion)
                {
                    AddScore(score, 240f, "build stack");
                }
                else if (candidate.ActionType == AIMainPhaseActionType.Play)
                {
                    AddScore(score, 140f, "develop stack");
                }
                break;

            case AITurnGoal.TempoClear:
                if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon)
                {
                    AddScore(score, 200f, "tempo clear");
                }
                break;

            case AITurnGoal.MemoryChoke:
                ApplyMemoryChokeBias(candidate, score);
                break;

            default:
                if (candidate.ActionType == AIMainPhaseActionType.Play || candidate.ActionType == AIMainPhaseActionType.Digivolve)
                {
                    AddScore(score, 110f, "value setup");
                }
                break;
        }
    }

    static void ApplyGenericHeuristics(AISnapshot snapshot, AIMainPhaseCandidate candidate, AIActionScore score)
    {
        if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
        {
            AddScore(score, 180f * Mathf.Max(1, candidate.ImmediateSecurityPressure), "security pressure");
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon)
        {
            AddScore(score, candidate.SourceDP >= candidate.TargetDP ? 180f : -180f, "battle favorability");

            if (candidate.TargetIsBlocker)
            {
                AddScore(score, 150f, "remove blocker");
            }
        }

        if (candidate.ActionType == AIMainPhaseActionType.Play)
        {
            ApplyPlayHeuristics(snapshot, candidate, score);
        }

        if (candidate.ActionType == AIMainPhaseActionType.Digivolve || candidate.ActionType == AIMainPhaseActionType.Jogress || candidate.ActionType == AIMainPhaseActionType.Burst || candidate.ActionType == AIMainPhaseActionType.AppFusion)
        {
            AddScore(score, 150f, "stack progression");
            AddScore(score, Mathf.Max(0f, (candidate.SourceLevel - candidate.TargetLevel) * 18f), "level progression");
        }

        if (candidate.ActionType == AIMainPhaseActionType.UseFieldEffect || candidate.ActionType == AIMainPhaseActionType.UseHandEffect || candidate.ActionType == AIMainPhaseActionType.UseTrashEffect)
        {
            AddScore(score, 80f, "effect value");
        }

        if (candidate.ActionType == AIMainPhaseActionType.EndTurn)
        {
            AddScore(score, -90f, "pass turn");
        }

        AddScore(score, -12f * Mathf.Abs(candidate.ProjectedMemory), "memory handed away");

        if (Mathf.Abs(candidate.ProjectedMemory) <= 1 && candidate.ActionType != AIMainPhaseActionType.EndTurn)
        {
            AddScore(score, 18f, "tight memory");
        }

        if ((candidate.ActionType == AIMainPhaseActionType.AttackSecurity || candidate.ActionType == AIMainPhaseActionType.AttackDigimon) && candidate.SourceStackCount >= 4)
        {
            AddScore(score, -45f, "premium stack caution");
        }

        if ((candidate.ActionType == AIMainPhaseActionType.AttackSecurity || candidate.ActionType == AIMainPhaseActionType.AttackDigimon) && candidate.SourceDP > 0 && candidate.TargetDP > 0 && candidate.TargetDP >= candidate.SourceDP)
        {
            AddScore(score, -75f, "avoid bad visible trade");
        }

        if (snapshot != null && snapshot.SelfThreatCount == 0 && (candidate.ActionType == AIMainPhaseActionType.Play || candidate.ActionType == AIMainPhaseActionType.Digivolve))
        {
            AddScore(score, 50f, "create next-turn threat");
        }
    }

    static void ApplyRaceHeuristics(AISnapshot snapshot, AIMainPhaseCandidate candidate, AIActionScore score)
    {
        if (snapshot == null || snapshot.Race == null)
        {
            return;
        }

        if (snapshot.Race.ShouldConvertPressure)
        {
            if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
            {
                AddScore(score, 130f, "convert pressure");

                if (candidate.LikelySafeAttack)
                {
                    AddScore(score, 75f, "safe pressure while ahead");
                }

                if (candidate.UnlocksAdditionalPressure)
                {
                    AddScore(score, 90f, "pressure sequencing unlock");
                }

                if (snapshot.Opponent.SecurityCount <= 2)
                {
                    AddScore(score, 95f, "convert tempo into near-lethal");
                }
            }

            if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon && (candidate.TargetIsBlocker || candidate.UnlocksAdditionalPressure))
            {
                AddScore(score, 125f, "clear path for pressure");

                if (candidate.LikelySafeAttack && snapshot.Opponent.SecurityCount <= 2)
                {
                    AddScore(score, 55f, "tempo clear enables near-lethal");
                }
            }

            if (candidate.ActionType == AIMainPhaseActionType.Play && IsSlowTamerSetupIntent(candidate.PlayIntent))
            {
                AddScore(score, -130f, "too slow while ahead");
            }

            if ((candidate.ActionType == AIMainPhaseActionType.Digivolve
                    || candidate.ActionType == AIMainPhaseActionType.Jogress
                    || candidate.ActionType == AIMainPhaseActionType.Burst
                    || candidate.ActionType == AIMainPhaseActionType.AppFusion)
                && snapshot.Opponent.SecurityCount <= 3)
            {
                AddScore(score, -60f, "stacking instead of converting pressure");
            }
        }

        if (snapshot.Race.ShouldStabilize)
        {
            if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
            {
                AddScore(score, candidate.LikelySafeAttack && snapshot.Opponent.SecurityCount <= 2 ? -90f : -180f, "pressure is secondary to stabilizing");
            }

            if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon)
            {
                AddScore(score, candidate.LikelySafeAttack ? 140f : 40f, "stabilize board");

                if (candidate.TargetIsBlocker)
                {
                    AddScore(score, 40f, "remove blocker while stabilizing");
                }
            }

            if (candidate.ActionType == AIMainPhaseActionType.UseFieldEffect
                || candidate.ActionType == AIMainPhaseActionType.UseHandEffect
                || candidate.ActionType == AIMainPhaseActionType.UseTrashEffect)
            {
                AddScore(score, 90f, "interaction while stabilizing");
            }

            if (candidate.ActionType == AIMainPhaseActionType.Play && IsSlowTamerSetupIntent(candidate.PlayIntent))
            {
                AddScore(score, -150f, "slow setup while behind");
            }
        }

        if (snapshot.Race.SafeToDevelop)
        {
            if (candidate.ActionType == AIMainPhaseActionType.Play)
            {
                ApplySafeDevelopmentPlayBias(candidate, score);
            }

            if (candidate.ActionType == AIMainPhaseActionType.Digivolve
                || candidate.ActionType == AIMainPhaseActionType.Jogress
                || candidate.ActionType == AIMainPhaseActionType.Burst
                || candidate.ActionType == AIMainPhaseActionType.AppFusion)
            {
                AddScore(score, 70f, "safe development window");
            }
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity && candidate.LikelySafeAttack)
        {
            if (snapshot.Race.SecurityDelta >= 0 && snapshot.Race.BoardValueDelta >= 0)
            {
                AddScore(score, 40f, "safe pressure with board lead");
            }
            else if (snapshot.Self.SecurityCount <= snapshot.Opponent.SecurityCount)
            {
                AddScore(score, 30f, "safe pressure while racing");
            }
        }
    }

    static GoalSelection Goal(AITurnGoal goal, string reason)
    {
        return new GoalSelection
        {
            Goal = goal,
            Reason = reason ?? "",
        };
    }

    static bool HasMeaningfulBreedingStack(AISnapshot snapshot)
    {
        if (snapshot == null || snapshot.Self == null || snapshot.Self.BreedingPermanents.Count == 0)
        {
            return false;
        }

        AISnapshotPermanentView breeding = snapshot.Self.BreedingPermanents[0];
        if (breeding == null)
        {
            return false;
        }

        return snapshot.Self.BreedingValueScore >= 8
            || breeding.StackCount >= 2
            || breeding.Level >= 5;
    }

    static bool IsBehindBoardRace(AISnapshot snapshot)
    {
        if (snapshot == null || snapshot.Race == null)
        {
            return false;
        }

        return snapshot.Race.BoardValueDelta <= -2
            || snapshot.Race.CounterPressureDelta < 0
            || snapshot.Opponent.BattleDigimonCount > snapshot.Self.BattleDigimonCount + snapshot.Self.BlockerCount;
    }

    static bool HasStrongSafePressure(AISnapshot snapshot, List<AIMainPhaseCandidate> candidates)
    {
        if (snapshot == null || candidates == null)
        {
            return false;
        }

        if (snapshot.Race != null && snapshot.Race.ShouldConvertPressure)
        {
            return true;
        }

        bool hasSafeSecurityPressure = candidates.Any(candidate =>
            candidate.ActionType == AIMainPhaseActionType.AttackSecurity
            && (candidate.AttackIntent == AIAttackIntent.CloseGame
                || (candidate.LikelySafeAttack
                    && (candidate.UnlocksAdditionalPressure
                        || candidate.AttackerValueTier != AIAttackerValueTier.Low
                        || snapshot.Opponent.SecurityCount <= 3))));

        if (hasSafeSecurityPressure)
        {
            return true;
        }

        return candidates.Any(candidate =>
            candidate.ActionType == AIMainPhaseActionType.AttackDigimon
            && candidate.TargetIsBlocker
            && candidate.LikelySafeAttack
            && candidate.UnlocksAdditionalPressure
            && snapshot.Opponent.SecurityCount <= 3);
    }

    static bool HasCheaperSecurityPressureAlternative(AIMainPhaseCandidate candidate, IReadOnlyList<AIMainPhaseCandidate> candidates)
    {
        int candidateValue = AttackOrderValue(candidate);

        for (int i = 0; i < candidates.Count; i++)
        {
            AIMainPhaseCandidate other = candidates[i];
            if (other == null
                || other == candidate
                || other.ActionType != AIMainPhaseActionType.AttackSecurity
                || other.SourcePermanentIndex == candidate.SourcePermanentIndex)
            {
                continue;
            }

            if (AttackOrderValue(other) + 3 <= candidateValue
                && other.ImmediateSecurityPressure >= candidate.ImmediateSecurityPressure
                && (other.LikelySafeAttack || !candidate.LikelySafeAttack || other.UnlocksAdditionalPressure))
            {
                return true;
            }
        }

        return false;
    }

    static bool HasHigherValueSecurityFollowUp(AIMainPhaseCandidate candidate, IReadOnlyList<AIMainPhaseCandidate> candidates)
    {
        int candidateValue = AttackOrderValue(candidate);

        for (int i = 0; i < candidates.Count; i++)
        {
            AIMainPhaseCandidate other = candidates[i];
            if (other == null
                || other == candidate
                || other.ActionType != AIMainPhaseActionType.AttackSecurity
                || other.SourcePermanentIndex == candidate.SourcePermanentIndex)
            {
                continue;
            }

            if (AttackOrderValue(other) >= candidateValue + 4)
            {
                return true;
            }
        }

        return false;
    }

    static bool DoesBlockerClearUnlockMeaningfulPressure(AISnapshot snapshot, AIMainPhaseCandidate candidate, IReadOnlyList<AIMainPhaseCandidate> candidates)
    {
        if (snapshot == null || candidate == null || !candidate.TargetIsBlocker || !candidate.UnlocksAdditionalPressure)
        {
            return false;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            AIMainPhaseCandidate other = candidates[i];
            if (other == null
                || other == candidate
                || other.ActionType != AIMainPhaseActionType.AttackSecurity
                || other.SourcePermanentIndex == candidate.SourcePermanentIndex)
            {
                continue;
            }

            if (other.LikelySafeAttack
                || other.AttackerValueTier >= AIAttackerValueTier.Medium
                || other.UnlocksAdditionalPressure
                || snapshot.Opponent.SecurityCount <= 2)
            {
                return true;
            }
        }

        return false;
    }

    static bool HasCheaperEquivalentAttack(AIMainPhaseCandidate candidate, IReadOnlyList<AIMainPhaseCandidate> candidates)
    {
        int candidateValue = AttackOrderValue(candidate);

        for (int i = 0; i < candidates.Count; i++)
        {
            AIMainPhaseCandidate other = candidates[i];
            if (!IsEquivalentAttackTarget(candidate, other) || other.SourcePermanentIndex == candidate.SourcePermanentIndex)
            {
                continue;
            }

            if (AttackOrderValue(other) + 3 <= candidateValue
                && (other.LikelySafeAttack || other.SourceDP >= candidate.TargetDP))
            {
                return true;
            }
        }

        return false;
    }

    static bool HasHigherValueEquivalentAttack(AIMainPhaseCandidate candidate, IReadOnlyList<AIMainPhaseCandidate> candidates)
    {
        int candidateValue = AttackOrderValue(candidate);

        for (int i = 0; i < candidates.Count; i++)
        {
            AIMainPhaseCandidate other = candidates[i];
            if (!IsEquivalentAttackTarget(candidate, other) || other.SourcePermanentIndex == candidate.SourcePermanentIndex)
            {
                continue;
            }

            if (AttackOrderValue(other) >= candidateValue + 4)
            {
                return true;
            }
        }

        return false;
    }

    static bool IsEquivalentAttackTarget(AIMainPhaseCandidate candidate, AIMainPhaseCandidate other)
    {
        return candidate != null
            && other != null
            && candidate.ActionType == AIMainPhaseActionType.AttackDigimon
            && other.ActionType == AIMainPhaseActionType.AttackDigimon
            && candidate.AttackTargetPermanentIndex >= 0
            && candidate.AttackTargetPermanentIndex == other.AttackTargetPermanentIndex;
    }

    static bool IsLowValueAttackTarget(AIMainPhaseCandidate candidate)
    {
        return candidate != null
            && !candidate.TargetIsBlocker
            && candidate.TargetDP > 0
            && candidate.TargetDP <= 5000
            && candidate.TargetLevel <= 4;
    }

    static bool IsPremiumAttackCandidate(AIMainPhaseCandidate candidate)
    {
        return candidate != null
            && (candidate.AttackerValueTier == AIAttackerValueTier.High
                || candidate.SourceStackCount >= 4
                || candidate.SourceLevel >= 6
                || candidate.SourceDP >= 10000);
    }

    static int AttackOrderValue(AIMainPhaseCandidate candidate)
    {
        if (candidate == null)
        {
            return 0;
        }

        int tierValue = 0;
        switch (candidate.AttackerValueTier)
        {
            case AIAttackerValueTier.High:
                tierValue = 8;
                break;
            case AIAttackerValueTier.Medium:
                tierValue = 4;
                break;
        }

        return tierValue
            + (candidate.SourceStackCount * 2)
            + Mathf.Min(candidate.SourceLevel, 6)
            + (candidate.SourceDP / 4000)
            + (candidate.SourceHasBlocker ? 1 : 0);
    }

    static bool ShouldProtectPremiumStack(AISnapshot snapshot, AIMainPhaseCandidate candidate)
    {
        if (snapshot == null
            || snapshot.Race == null
            || candidate == null
            || (candidate.ActionType != AIMainPhaseActionType.AttackSecurity && candidate.ActionType != AIMainPhaseActionType.AttackDigimon)
            || candidate.SourceStackCount < 4)
        {
            return false;
        }

        if (candidate.LikelySafeAttack && candidate.UnlocksAdditionalPressure)
        {
            return false;
        }

        bool losingRace = snapshot.Self.SecurityCount <= snapshot.Opponent.SecurityCount || snapshot.Race.ShouldStabilize;
        bool visibleTradeRisk = candidate.ActionType == AIMainPhaseActionType.AttackDigimon && candidate.TargetDP >= candidate.SourceDP;
        bool blockerWall = candidate.ActionType == AIMainPhaseActionType.AttackSecurity && snapshot.Opponent.BlockerCount > 0;

        return losingRace && (visibleTradeRisk || blockerWall || !candidate.LikelySafeAttack);
    }

    static void ApplyMemoryChokeBias(AIMainPhaseCandidate candidate, AIActionScore score)
    {
        if (candidate == null)
        {
            return;
        }

        float actionFactor = GetMemoryChokeActionFactor(candidate);
        if (actionFactor <= 0f)
        {
            return;
        }

        float projectedMemoryMagnitude = Mathf.Abs(candidate.ProjectedMemory);
        float chokeBias = Mathf.Max(0f, 24f - (projectedMemoryMagnitude * 8f)) * actionFactor;
        AddScore(score, chokeBias, "memory choke");
    }

    static float GetMemoryChokeActionFactor(AIMainPhaseCandidate candidate)
    {
        switch (candidate.ActionType)
        {
            case AIMainPhaseActionType.AttackSecurity:
            case AIMainPhaseActionType.AttackDigimon:
            case AIMainPhaseActionType.Digivolve:
            case AIMainPhaseActionType.Jogress:
            case AIMainPhaseActionType.Burst:
            case AIMainPhaseActionType.AppFusion:
                return 1f;

            case AIMainPhaseActionType.UseFieldEffect:
            case AIMainPhaseActionType.UseHandEffect:
            case AIMainPhaseActionType.UseTrashEffect:
                return 0.85f;

            case AIMainPhaseActionType.Play:
                switch (candidate.PlayIntent)
                {
                    case AIPlayIntent.MemorySetter:
                        return 0.45f;
                    case AIPlayIntent.UtilityTamer:
                        return 0.55f;
                    case AIPlayIntent.BodyDevelopment:
                        return 0.8f;
                    case AIPlayIntent.TempoOption:
                        return 0.9f;
                    case AIPlayIntent.RemovalOption:
                        return 0.95f;
                    case AIPlayIntent.Floodgate:
                        return 0.75f;
                    case AIPlayIntent.Finisher:
                        return 0.85f;
                    case AIPlayIntent.Unknown:
                    default:
                        return 0.35f;
                }

            case AIMainPhaseActionType.EndTurn:
                return 0f;

            default:
                return 0.5f;
        }
    }

    static AIActionScore ScoreKeepHand(AISnapshot snapshot)
    {
        AIActionScore score = CreateScore("Keep hand");

        if (snapshot == null)
        {
            return score;
        }

        int level3Count = snapshot.Self.KnownHandCards.Count(card => card.IsDigimon && card.Level == 3);
        int level4PlusCount = snapshot.Self.KnownHandCards.Count(card => card.IsDigimon && card.Level >= 4);
        int lowCurveCount = snapshot.Self.KnownHandCards.Count(card => card.IsDigimon && card.Level <= 4);

        AddScore(score, level3Count * 120f, "level 3 access");
        AddScore(score, lowCurveCount * 45f, "early curve");
        AddScore(score, level4PlusCount * 15f, "follow-up");

        if (level3Count == 0)
        {
            AddScore(score, -260f, "no level 3");
        }

        if (lowCurveCount <= 1)
        {
            AddScore(score, -120f, "top heavy");
        }

        return score;
    }

    static AIActionScore ScoreRedraw(AISnapshot snapshot, float keepScore)
    {
        AIActionScore score = CreateScore("Mulligan");
        AddScore(score, 160f, "fresh hand");
        AddScore(score, -keepScore * 0.5f, "give up current hand");
        return score;
    }

    static AIActionScore CreateScore(string actionSummary)
    {
        return new AIActionScore
        {
            ActionSummary = actionSummary,
            TotalScore = 0f,
        };
    }

    static void AddScore(AIActionScore score, float delta, string reason)
    {
        if (Mathf.Abs(delta) < 0.01f)
        {
            return;
        }

        score.TotalScore += delta;
        score.Breakdown.Add($"{(delta >= 0f ? "+" : "")}{delta:0} {reason}");
    }

    static void ApplyPlayHeuristics(AISnapshot snapshot, AIMainPhaseCandidate candidate, AIActionScore score)
    {
        bool hasBoardPresence = snapshot != null && (snapshot.Self.BattleDigimonCount >= 2 || snapshot.Self.BattleTamerCount >= 1 || snapshot.Self.ReadyDigimonCount >= 1);
        bool midgameRace = IsMidgameRace(snapshot);
        bool pressuredState = snapshot != null && snapshot.Race != null && (snapshot.Race.ShouldConvertPressure || snapshot.Race.ShouldStabilize);

        switch (candidate.PlayIntent)
        {
            case AIPlayIntent.BodyDevelopment:
                AddScore(score, 100f, "body development");
                if (snapshot != null && snapshot.Self.BattleDigimonCount == 0)
                {
                    AddScore(score, 55f, "establish first body");
                }
                else if (snapshot != null && snapshot.Self.BattleDigimonCount == 1)
                {
                    AddScore(score, 25f, "broaden board");
                }

                if (snapshot != null && snapshot.Race != null && snapshot.Race.ShouldConvertPressure && hasBoardPresence)
                {
                    AddScore(score, -35f, "extra body is less urgent than converting pressure");
                }
                break;

            case AIPlayIntent.MemorySetter:
                AddScore(score, 62f, "memory setter");
                if (snapshot != null && snapshot.Self.BattleTamerCount == 0)
                {
                    AddScore(score, 24f, "first tamer slot");
                }
                if (hasBoardPresence)
                {
                    AddScore(score, -38f, "board already established");
                }
                if (midgameRace)
                {
                    AddScore(score, -42f, "midgame setup tax");
                }
                if (pressuredState)
                {
                    AddScore(score, -72f, "race state punishes slow tamer");
                }
                ApplySlowSetupSuppression(snapshot, candidate, score);
                break;

            case AIPlayIntent.UtilityTamer:
                AddScore(score, 58f, "utility tamer");
                if (!hasBoardPresence)
                {
                    AddScore(score, 18f, "early utility setup");
                }
                if (hasBoardPresence)
                {
                    AddScore(score, -32f, "board already established");
                }
                if (midgameRace)
                {
                    AddScore(score, -36f, "midgame setup tax");
                }
                if (pressuredState)
                {
                    AddScore(score, -64f, "race state punishes slow tamer");
                }
                ApplySlowSetupSuppression(snapshot, candidate, score);
                break;

            case AIPlayIntent.TempoOption:
                AddScore(score, 84f, "tempo option");
                if (snapshot != null && snapshot.Race != null && (snapshot.Race.ShouldConvertPressure || snapshot.Race.ShouldStabilize))
                {
                    AddScore(score, 34f, "interactive option fits race");
                }
                break;

            case AIPlayIntent.RemovalOption:
                AddScore(score, 92f, "removal option");
                if (snapshot != null && snapshot.Opponent.BattleDigimonCount > 0)
                {
                    AddScore(score, 46f, "opponent has board to punish");
                }
                if (snapshot != null && snapshot.Race != null && snapshot.Race.ShouldStabilize)
                {
                    AddScore(score, 52f, "removal helps stabilize");
                }
                else if (snapshot != null && snapshot.Race != null && snapshot.Race.ShouldConvertPressure)
                {
                    AddScore(score, 32f, "removal clears path for pressure");
                }
                break;

            case AIPlayIntent.Floodgate:
                AddScore(score, 76f, "floodgate body");
                if (snapshot != null && snapshot.Opponent.ImmediatePressureScore >= 2)
                {
                    AddScore(score, 26f, "disrupt active race");
                }
                if (hasBoardPresence && snapshot != null && snapshot.Race != null && !snapshot.Race.ShouldStabilize)
                {
                    AddScore(score, -20f, "extra floodgate is less urgent on board");
                }
                break;

            case AIPlayIntent.Finisher:
                AddScore(score, 52f, "finisher body");
                if (snapshot != null && snapshot.Opponent.SecurityCount <= 2)
                {
                    AddScore(score, 78f, "threatens closeout");
                }
                if (snapshot != null && snapshot.Race != null && snapshot.Race.ShouldConvertPressure)
                {
                    AddScore(score, 36f, "finisher converts board lead");
                }
                if (snapshot != null && snapshot.Race != null && snapshot.Race.ShouldStabilize)
                {
                    AddScore(score, -28f, "expensive finisher is clunky while stabilizing");
                }
                break;

            case AIPlayIntent.Unknown:
            default:
                AddScore(score, 42f, "unknown play");
                break;
        }
    }

    static void ApplySafeDevelopmentPlayBias(AIMainPhaseCandidate candidate, AIActionScore score)
    {
        switch (candidate.PlayIntent)
        {
            case AIPlayIntent.MemorySetter:
                AddScore(score, 88f, "safe setup window");
                break;

            case AIPlayIntent.UtilityTamer:
                AddScore(score, 70f, "safe setup window");
                break;

            case AIPlayIntent.BodyDevelopment:
                AddScore(score, 42f, "safe body development");
                break;

            case AIPlayIntent.Floodgate:
                AddScore(score, 46f, "safe disruptive development");
                break;

            case AIPlayIntent.Finisher:
                AddScore(score, 24f, "safe premium development");
                break;

            case AIPlayIntent.RemovalOption:
            case AIPlayIntent.TempoOption:
                AddScore(score, 20f, "safe value development");
                break;

            case AIPlayIntent.Unknown:
            default:
                AddScore(score, 18f, "safe value development");
                break;
        }
    }

    static bool IsSlowTamerSetupIntent(AIPlayIntent playIntent)
    {
        return playIntent == AIPlayIntent.MemorySetter
            || playIntent == AIPlayIntent.UtilityTamer;
    }

    static void ApplySlowSetupSuppression(AISnapshot snapshot, AIMainPhaseCandidate candidate, AIActionScore score)
    {
        if (snapshot == null || candidate == null || !IsSlowTamerSetupIntent(candidate.PlayIntent))
        {
            return;
        }

        bool earlyMemorySetterWindow =
            candidate.PlayIntent == AIPlayIntent.MemorySetter
            && snapshot.TurnCount <= 3
            && snapshot.Self.BattleDigimonCount == 0
            && snapshot.Self.BattleTamerCount == 0
            && snapshot.Self.ImmediatePressureScore == 0
            && (snapshot.Race == null || !snapshot.Race.ShouldStabilize);

        if (earlyMemorySetterWindow)
        {
            AddScore(score, 28f, "early memory setter window");
            return;
        }

        bool activeRaceState =
            snapshot.Race != null
            && (snapshot.Race.ShouldConvertPressure
                || snapshot.Race.ShouldStabilize
                || (snapshot.TurnCount >= 4 && (snapshot.Race.ImmediatePressureDelta != 0 || snapshot.Race.CounterPressureDelta != 0)));

        bool meaningfulBoardPresence =
            snapshot.Self.ReadyDigimonCount >= 1
            || snapshot.Self.BattleDigimonCount >= 2
            || snapshot.Self.BoardValueScore >= 10;

        bool pressureLinesAvailable =
            snapshot.Self.ImmediatePressureScore >= 2
            || (snapshot.Self.ReadyDigimonCount >= 1 && snapshot.Opponent.SecurityCount <= 3)
            || (snapshot.Race != null && snapshot.Race.ShouldConvertPressure);

        if (activeRaceState && pressureLinesAvailable)
        {
            AddScore(
                score,
                candidate.PlayIntent == AIPlayIntent.MemorySetter ? -90f : -80f,
                "active race favors pressure over setup");
        }

        if (meaningfulBoardPresence && pressureLinesAvailable)
        {
            AddScore(
                score,
                candidate.PlayIntent == AIPlayIntent.MemorySetter ? -65f : -55f,
                "existing board already supports pressure");
        }
    }

    static bool IsMidgameRace(AISnapshot snapshot)
    {
        if (snapshot == null)
        {
            return false;
        }

        return snapshot.TurnCount >= 4
            || snapshot.Self.BattleDigimonCount + snapshot.Opponent.BattleDigimonCount >= 3
            || (snapshot.Race != null && (snapshot.Race.BoardValueDelta != 0 || snapshot.Race.ImmediatePressureDelta != 0));
    }
}
