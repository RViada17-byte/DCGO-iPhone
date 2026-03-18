using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GreedyShadowBrain : IAIBrain
{
    enum PostureKind
    {
        CloseGame,
        Stabilize,
        Race,
        ConvertAdvantage,
        Develop,
        Choke,
    }

    class PostureProfile
    {
        public float CloseGameWeight = 0f;
        public float StabilizeWeight = 0f;
        public float RaceWeight = 0f;
        public float ConvertAdvantageWeight = 0f;
        public float DevelopWeight = 0f;
        public float ChokeWeight = 0f;
        public PostureKind PrimaryKind = PostureKind.Develop;
        public AITurnGoal DebugGoal = AITurnGoal.ValueSetup;
        public string Reason = "";

        public float Weight(PostureKind kind)
        {
            switch (kind)
            {
                case PostureKind.CloseGame:
                    return CloseGameWeight;
                case PostureKind.Stabilize:
                    return StabilizeWeight;
                case PostureKind.Race:
                    return RaceWeight;
                case PostureKind.ConvertAdvantage:
                    return ConvertAdvantageWeight;
                case PostureKind.Develop:
                    return DevelopWeight;
                case PostureKind.Choke:
                    return ChokeWeight;
                default:
                    return 0f;
            }
        }
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
            List<AIMainPhaseCandidate> boardCandidates = null;
            List<AIMainPhaseCandidate> projectedCandidates = null;
            AIVisibleLethalPlan boardLethalPlan = null;
            AIVisibleLethalPlan raiseLethalPlan = null;
            if (gameContext != null && player != null)
            {
                boardCandidates = AIMainPhaseCandidateBuilder.Build(gameContext, player);
                projectedCandidates = AIMainPhaseCandidateBuilder.BuildForProjectedMoveOut(gameContext, player);
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

            if (ShouldDefaultRaiseMatureLevelSix(snapshot))
            {
                bool visiblePunish = HasClearVisibleRaisePunish(snapshot);
                bool meaningfulMoveOutValue = MoveOutCreatesMeaningfulImmediateValue(snapshot, projectedCandidates);

                if (!visiblePunish || meaningfulMoveOutValue)
                {
                    AIActionScore matureRaiseScore = CreateScore("Move out from breeding");
                    AddScore(matureRaiseScore, 90000f, "mature L6 should not idle in breeding");
                    if (meaningfulMoveOutValue)
                    {
                        AddScore(matureRaiseScore, 350f, "moving out creates immediate pressure or tempo");
                    }

                    AIActionScore matureStayScore = CreateScore("Stay hidden in breeding");
                    AddScore(matureStayScore, -90000f, "staying hidden delays an online L6 without enough payoff");
                    if (visiblePunish)
                    {
                        AddScore(matureStayScore, 250f, "visible punish exists, but not enough to outweigh active L6 pressure");
                    }

                    return AIChosenAction.Create(
                        AIChosenAction.AIDecisionType.Breeding,
                        AIChosenAction.AIActionKind.MoveOut,
                        "Move out from breeding",
                        meaningfulMoveOutValue ? AITurnGoal.ValueSetup : AITurnGoal.BuildStack,
                        matureRaiseScore,
                        new List<AIActionScore> { matureStayScore },
                        goalReason: meaningfulMoveOutValue
                            ? "mature L6 can convert into immediate pressure or tempo now"
                            : "mature L6 should be promoted unless a clear visible punish says otherwise");
                }

                AIActionScore protectedStayScore = CreateScore("Stay hidden in breeding");
                AddScore(protectedStayScore, 90000f, "clear visible punish and low immediate payoff justify hiding mature L6");

                AIActionScore protectedMoveScore = CreateScore("Move out from breeding");
                AddScore(protectedMoveScore, -90000f, "raising mature L6 opens a visible punish without enough immediate value");

                return AIChosenAction.Create(
                    AIChosenAction.AIDecisionType.Breeding,
                    AIChosenAction.AIActionKind.StayHidden,
                    "Stay hidden in breeding",
                    AITurnGoal.Stabilize,
                    protectedStayScore,
                    new List<AIActionScore> { protectedMoveScore },
                    goalReason: "visible punish is strong and raising does not create enough immediate value");
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
        PostureProfile postureProfile = BuildPostureProfile(snapshot, candidateList, visibleLethalPlan);
        AITurnGoal goal = postureProfile.DebugGoal;
        List<ScoredCandidate> scoredCandidates = candidateList
            .Select(candidate => new ScoredCandidate
            {
                Candidate = candidate,
                Score = ScoreCandidate(snapshot, postureProfile, candidate, candidateList, visibleLethalPlan),
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

        return AIChosenAction.FromCandidate(best.Candidate, goal, best.Score, topAlternatives, postureProfile.Reason);
    }

    static PostureProfile BuildPostureProfile(AISnapshot snapshot, List<AIMainPhaseCandidate> candidates, AIVisibleLethalPlan visibleLethalPlan)
    {
        if (snapshot == null)
        {
            return FinalizePostureProfile(
                new PostureProfile
                {
                    DevelopWeight = 0.45f,
                    ChokeWeight = 0.15f,
                    Reason = "snapshot missing, default posture favors generic development",
                },
                snapshot,
                candidates);
        }

        if (visibleLethalPlan != null)
        {
            return FinalizePostureProfile(
                new PostureProfile
                {
                    CloseGameWeight = 1f,
                    Reason = visibleLethalPlan.Reason,
                },
                snapshot,
                candidates);
        }

        bool hasSecurityPressure = candidates.Any(candidate => candidate.ActionType == AIMainPhaseActionType.AttackSecurity);
        bool hasRemoval = candidates.Any(IsRemovalLikeCandidate);
        bool hasDevelopment = candidates.Any(candidate => candidate.ActionType == AIMainPhaseActionType.Play || candidate.ActionType == AIMainPhaseActionType.Digivolve || candidate.ActionType == AIMainPhaseActionType.Jogress || candidate.ActionType == AIMainPhaseActionType.Burst || candidate.ActionType == AIMainPhaseActionType.AppFusion);
        bool hasStackDevelopment = candidates.Any(candidate => candidate.ActionType == AIMainPhaseActionType.Digivolve || candidate.ActionType == AIMainPhaseActionType.Jogress || candidate.ActionType == AIMainPhaseActionType.Burst || candidate.ActionType == AIMainPhaseActionType.AppFusion);
        bool hasMemoryChokeLine = candidates.Any(candidate => candidate.ActionType != AIMainPhaseActionType.EndTurn && Mathf.Abs(candidate.ProjectedMemory) <= 1);
        bool meaningfulBreedingStack = HasMeaningfulBreedingStack(snapshot);
        bool behindBoardRace = IsBehindBoardRace(snapshot);
        bool hasStrongSafePressure = HasStrongSafePressure(snapshot, candidates);
        bool activeRaceState =
            snapshot.Race != null
            && (snapshot.Race.ShouldConvertPressure
                || snapshot.Race.ShouldStabilize
                || snapshot.Race.OpponentCanPunishSlowTurn
                || (snapshot.TurnCount >= 4 && (snapshot.Race.ImmediatePressureDelta != 0 || snapshot.Race.CounterPressureDelta != 0)));

        PostureProfile profile = new PostureProfile();

        float closeWeight = 0f;
        if (snapshot.Opponent.SecurityCount <= 1 && hasSecurityPressure)
        {
            closeWeight = 0.92f;
        }
        else if (snapshot.Opponent.SecurityCount == 2 && hasStrongSafePressure)
        {
            closeWeight = 0.48f;
        }
        else if (snapshot.Opponent.SecurityCount <= 3 && hasSecurityPressure && snapshot.Self.PremiumThreatCount > 0)
        {
            closeWeight = 0.26f;
        }
        profile.CloseGameWeight = Mathf.Clamp01(closeWeight);

        float stabilizeWeight = 0f;
        if (snapshot.Race.ShouldStabilize)
        {
            stabilizeWeight += 0.9f;
        }
        if (snapshot.Race.OpponentHasDangerousCrackback)
        {
            stabilizeWeight += 0.28f;
        }
        if (snapshot.Self.SecurityCount <= 3 && behindBoardRace)
        {
            stabilizeWeight += 0.2f;
        }
        profile.StabilizeWeight = Mathf.Clamp01(stabilizeWeight);

        float raceWeight = 0.08f;
        if (activeRaceState)
        {
            raceWeight += 0.34f;
        }
        if (snapshot.Self.SecurityCount <= snapshot.Opponent.SecurityCount && hasSecurityPressure)
        {
            raceWeight += 0.2f;
        }
        if (snapshot.Race.ImmediatePressureDelta != 0 || snapshot.Race.CounterPressureDelta != 0)
        {
            raceWeight += 0.14f;
        }
        if (snapshot.Race.OpponentCanPunishSlowTurn)
        {
            raceWeight += 0.1f;
        }
        profile.RaceWeight = Mathf.Clamp01(raceWeight);

        float convertWeight = 0.04f;
        if (snapshot.Race.ShouldConvertPressure)
        {
            convertWeight += 0.78f;
        }
        if (snapshot.Race.HasBoardAdvantage && hasStrongSafePressure)
        {
            convertWeight += 0.2f;
        }
        if (snapshot.Opponent.SecurityCount <= 3 && hasSecurityPressure)
        {
            convertWeight += 0.08f;
        }
        if (snapshot.Self.DevelopmentSufficient && snapshot.Self.HasEnoughBoardToConvert)
        {
            convertWeight += 0.14f;
        }
        profile.ConvertAdvantageWeight = Mathf.Clamp01(convertWeight);

        float developWeight = 0.08f;
        if (snapshot.Race.SafeToDevelop)
        {
            developWeight += 0.72f;
        }
        if (meaningfulBreedingStack && hasDevelopment && hasStackDevelopment && !behindBoardRace)
        {
            developWeight += 0.16f;
        }
        if (snapshot.TurnCount <= 3 && !snapshot.Race.OpponentCanPunishSlowTurn)
        {
            developWeight += 0.1f;
        }
        if (hasStrongSafePressure)
        {
            developWeight -= 0.18f;
        }
        if (snapshot.Self.DevelopmentSufficient)
        {
            developWeight -= 0.28f;
        }
        if (snapshot.Self.HasExistingMemorySetter)
        {
            developWeight -= 0.08f;
        }
        profile.DevelopWeight = Mathf.Clamp01(developWeight);

        float opponentMemoryUsePotential = EvaluateOpponentMemoryUsePotential(snapshot);
        float chokeWeight = 0f;
        if (hasMemoryChokeLine)
        {
            chokeWeight += 0.16f;
        }
        if (!snapshot.Race.ShouldConvertPressure && !snapshot.Race.ShouldStabilize)
        {
            chokeWeight += 0.08f;
        }
        if (snapshot.Race.HasBoardAdvantage)
        {
            chokeWeight += 0.08f;
        }
        if (snapshot.Self.DevelopmentSufficient || snapshot.Self.HasEnoughBoardToConvert || snapshot.Self.HasExistingMemorySetter)
        {
            chokeWeight += 0.12f;
        }
        if (opponentMemoryUsePotential >= 3f)
        {
            chokeWeight += 0.16f;
        }
        else if (opponentMemoryUsePotential >= 1.5f)
        {
            chokeWeight += 0.08f;
        }
        if (hasStrongSafePressure)
        {
            chokeWeight -= 0.18f;
        }
        if (activeRaceState)
        {
            chokeWeight -= 0.14f;
        }
        if (snapshot.Race.ShouldConvertPressure)
        {
            chokeWeight -= 0.22f;
        }
        if (snapshot.Race.OpponentCanPunishSlowTurn)
        {
            chokeWeight -= 0.12f;
        }
        if (snapshot.Race.ShouldStabilize)
        {
            chokeWeight -= 0.4f;
        }
        if (behindBoardRace)
        {
            chokeWeight -= 0.08f;
        }
        profile.ChokeWeight = Mathf.Clamp01(chokeWeight);

        string baseReason =
            profile.StabilizeWeight >= 0.7f ? "visible state strongly favors stabilization" :
            profile.ConvertAdvantageWeight >= 0.7f ? "board lead and safe pressure favor converting advantage now" :
            profile.DevelopWeight >= 0.7f ? "board and race state leave room to develop" :
            profile.CloseGameWeight >= 0.7f ? "public state points toward closing pressure" :
            profile.RaceWeight >= 0.55f ? "tempo race is active and slow drift is risky" :
            profile.ChokeWeight >= 0.35f ? "tight memory line is relevant but secondary" :
            "posture is mixed, so score across pressure, development, and safety";

        profile.Reason = baseReason;
        return FinalizePostureProfile(profile, snapshot, candidates);
    }

    static AIActionScore ScoreCandidate(AISnapshot snapshot, PostureProfile postureProfile, AIMainPhaseCandidate candidate, IReadOnlyList<AIMainPhaseCandidate> candidates, AIVisibleLethalPlan visibleLethalPlan)
    {
        AIActionScore score = CreateScore(candidate.Summary);
        score.DownstreamResolutionNotControlled = candidate.DownstreamResolutionNotControlled;

        ApplyHardRules(snapshot, postureProfile, candidate, score, visibleLethalPlan);
        ApplyPostureBias(postureProfile, candidate, score);
        ApplyGenericHeuristics(snapshot, candidate, score);
        ApplyRaceHeuristics(snapshot, postureProfile, candidate, score);
        ApplyAntiLethalHeuristics(snapshot, candidate, score);
        ApplyAttackOrderHeuristics(snapshot, postureProfile, candidate, candidates, score);
        ApplyContinuationHeuristics(snapshot, postureProfile, candidate, candidates, score);
        ApplyMemoryChokeBias(snapshot, postureProfile, candidate, candidates, score);

        if (candidate.DownstreamResolutionNotControlled)
        {
            AddScore(score, -15f, "downstream unresolved");
        }

        return score;
    }

    static void ApplyAttackOrderHeuristics(AISnapshot snapshot, PostureProfile postureProfile, AIMainPhaseCandidate candidate, IReadOnlyList<AIMainPhaseCandidate> candidates, AIActionScore score)
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
            bool tempoSensitivePosture = postureProfile != null
                && (postureProfile.StabilizeWeight >= 0.45f
                    || postureProfile.RaceWeight >= 0.45f
                    || postureProfile.ConvertAdvantageWeight >= 0.45f
                    || postureProfile.CloseGameWeight >= 0.45f);

            if (candidate.TargetIsBlocker)
            {
                if (blockerClearUnlocksPressure)
                {
                    AddScore(score, 70f, "blocker clear unlocks meaningful pressure");
                }
                else if (!tempoSensitivePosture)
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

    static void ApplyContinuationHeuristics(AISnapshot snapshot, PostureProfile postureProfile, AIMainPhaseCandidate candidate, IReadOnlyList<AIMainPhaseCandidate> candidates, AIActionScore score)
    {
        if (snapshot == null || candidate == null || candidates == null)
        {
            return;
        }

        int remainingPressureAttackers = CountRemainingPressureAttackers(candidate, candidates);
        int continuationPressure = EstimateContinuationPressure(snapshot, candidate, remainingPressureAttackers);
        float exposureRisk = EstimateContinuationExposureRisk(snapshot, candidate, remainingPressureAttackers);
        bool strandsPressure = StrandsFuturePressure(snapshot, candidate, remainingPressureAttackers, continuationPressure);
        bool activeTempoRace = snapshot.Race != null && (snapshot.Race.ShouldConvertPressure || snapshot.Race.ShouldStabilize || snapshot.Race.OpponentCanPunishSlowTurn);

        if (continuationPressure > 0)
        {
            AddScore(score, 24f * Mathf.Min(continuationPressure, 3), "continuation pressure");
        }

        if (remainingPressureAttackers > 0)
        {
            AddScore(score, 16f * Mathf.Min(remainingPressureAttackers, 2), "keeps follow-up attackers");
        }

        if (candidate.UnlocksAdditionalPressure)
        {
            AddScore(score, 42f, "unlocks future pressure");
        }

        if (strandsPressure)
        {
            AddScore(score, -58f, "strands future pressure");
        }

        if (exposureRisk > 0f)
        {
            AddScore(score, -exposureRisk, "weak continuation exposure");
        }

        if (Mathf.Abs(candidate.ProjectedMemory) >= 3
            && continuationPressure <= 0
            && !candidate.UnlocksAdditionalPressure
            && !CreatesBoardThreat(candidate)
            && !IsInteractiveEffectCandidate(candidate)
            && !IsInteractivePlayIntent(candidate.PlayIntent))
        {
            AddScore(score, -46f, "passes tempo without meaningful continuation");
        }

        if (activeTempoRace
            && continuationPressure <= 0
            && (IsLowImpactSetupCandidate(candidate) || IsLowValueAttackCandidate(candidate)))
        {
            AddScore(score, -52f, "low-impact line leaves weak continuation");
        }

        if (snapshot.Self.DevelopmentSufficient
            && continuationPressure <= 0
            && IsLowImpactSetupCandidate(candidate))
        {
            AddScore(score, -64f, "redundant setup leaves no pressure");
        }
    }

    static void ApplyHardRules(AISnapshot snapshot, PostureProfile postureProfile, AIMainPhaseCandidate candidate, AIActionScore score, AIVisibleLethalPlan visibleLethalPlan)
    {
        if (snapshot == null)
        {
            return;
        }

        bool opponentHasBlocker = snapshot.Opponent.BlockerCount > 0;
        bool closePosture = postureProfile != null && postureProfile.CloseGameWeight >= 0.55f;
        bool stabilizePosture = postureProfile != null && postureProfile.StabilizeWeight >= 0.55f;

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

        if (closePosture && opponentHasBlocker && candidate.ActionType == AIMainPhaseActionType.AttackDigimon && candidate.TargetIsBlocker)
        {
            AddScore(score, 25000f, "clear blocker before lethal");
        }

        if (stabilizePosture && candidate.ActionType == AIMainPhaseActionType.EndTurn)
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

    static void ApplyPostureBias(PostureProfile postureProfile, AIMainPhaseCandidate candidate, AIActionScore score)
    {
        if (postureProfile == null || candidate == null)
        {
            return;
        }

        float closeWeight = postureProfile.CloseGameWeight;
        if (closeWeight > 0f)
        {
            if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
            {
                AddScore(score, 600f * closeWeight, "close posture");
            }
            else if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon && (candidate.TargetIsBlocker || candidate.UnlocksAdditionalPressure))
            {
                AddScore(score, 170f * closeWeight, "close posture clears path");
            }
        }

        float stabilizeWeight = postureProfile.StabilizeWeight;
        if (stabilizeWeight > 0f)
        {
            if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon
                || IsInteractiveEffectCandidate(candidate))
            {
                AddScore(score, 220f * stabilizeWeight, "stabilize posture");
            }
            else if (candidate.ActionType == AIMainPhaseActionType.Play && IsSlowTamerSetupIntent(candidate.PlayIntent))
            {
                AddScore(score, -90f * stabilizeWeight, "stabilize posture rejects slow setup");
            }
        }

        float raceWeight = postureProfile.RaceWeight;
        if (raceWeight > 0f)
        {
            if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
            {
                AddScore(score, 120f * raceWeight, "race posture");
            }
            else if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon && (candidate.LikelySafeAttack || candidate.TargetIsBlocker))
            {
                AddScore(score, 105f * raceWeight, "race posture contests tempo");
            }
            else if (candidate.ActionType == AIMainPhaseActionType.Play && IsSlowTamerSetupIntent(candidate.PlayIntent))
            {
                AddScore(score, -95f * raceWeight, "race posture rejects slow setup");
            }
        }

        float convertWeight = postureProfile.ConvertAdvantageWeight;
        if (convertWeight > 0f)
        {
            if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
            {
                AddScore(score, 150f * convertWeight, "convert advantage posture");
            }
            else if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon && (candidate.TargetIsBlocker || candidate.UnlocksAdditionalPressure))
            {
                AddScore(score, 130f * convertWeight, "convert advantage posture");
            }
            else if (candidate.ActionType == AIMainPhaseActionType.Play && IsSlowTamerSetupIntent(candidate.PlayIntent))
            {
                AddScore(score, -100f * convertWeight, "convert advantage rejects setup");
            }
        }

        float developWeight = postureProfile.DevelopWeight;
        if (developWeight > 0f)
        {
            if (candidate.ActionType == AIMainPhaseActionType.Digivolve
                || candidate.ActionType == AIMainPhaseActionType.Jogress
                || candidate.ActionType == AIMainPhaseActionType.Burst
                || candidate.ActionType == AIMainPhaseActionType.AppFusion)
            {
                AddScore(score, 220f * developWeight, "develop posture");
            }
            else if (candidate.ActionType == AIMainPhaseActionType.Play)
            {
                AddScore(score, 120f * developWeight, "develop posture");
            }
            else if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity && !candidate.UnlocksAdditionalPressure)
            {
                AddScore(score, -45f * developWeight, "develop posture delays low-payoff swing");
            }
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

        if (IsUseEffectCandidate(candidate))
        {
            ApplyEffectHeuristics(snapshot, candidate, score);
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

        if (snapshot != null
            && snapshot.SelfThreatCount == 0
            && (candidate.ActionType == AIMainPhaseActionType.Digivolve
                || candidate.ActionType == AIMainPhaseActionType.Jogress
                || candidate.ActionType == AIMainPhaseActionType.Burst
                || candidate.ActionType == AIMainPhaseActionType.AppFusion
                || (candidate.ActionType == AIMainPhaseActionType.Play && CreatesBoardThreat(candidate))))
        {
            AddScore(score, 50f, "create next-turn threat");
        }
    }

    static void ApplyRaceHeuristics(AISnapshot snapshot, PostureProfile postureProfile, AIMainPhaseCandidate candidate, AIActionScore score)
    {
        if (snapshot == null || snapshot.Race == null || postureProfile == null)
        {
            return;
        }

        float convertWeight = postureProfile.ConvertAdvantageWeight;
        if (convertWeight > 0.05f)
        {
            if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
            {
                AddScore(score, 130f * convertWeight, "convert pressure");

                if (candidate.LikelySafeAttack)
                {
                    AddScore(score, 75f * convertWeight, "safe pressure while ahead");
                }

                if (candidate.UnlocksAdditionalPressure)
                {
                    AddScore(score, 90f * convertWeight, "pressure sequencing unlock");
                }

                if (snapshot.Opponent.SecurityCount <= 2)
                {
                    AddScore(score, 95f * convertWeight, "convert tempo into near-lethal");
                }
            }

            if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon && (candidate.TargetIsBlocker || candidate.UnlocksAdditionalPressure))
            {
                AddScore(score, 125f * convertWeight, "clear path for pressure");

                if (candidate.LikelySafeAttack && snapshot.Opponent.SecurityCount <= 2)
                {
                    AddScore(score, 55f * convertWeight, "tempo clear enables near-lethal");
                }
            }

            if (candidate.ActionType == AIMainPhaseActionType.Play && IsSlowTamerSetupIntent(candidate.PlayIntent))
            {
                AddScore(score, -130f * convertWeight, "too slow while ahead");
            }

            if ((candidate.ActionType == AIMainPhaseActionType.Digivolve
                    || candidate.ActionType == AIMainPhaseActionType.Jogress
                    || candidate.ActionType == AIMainPhaseActionType.Burst
                    || candidate.ActionType == AIMainPhaseActionType.AppFusion)
                && snapshot.Opponent.SecurityCount <= 3)
            {
                AddScore(score, -60f * convertWeight, "stacking instead of converting pressure");
            }
        }

        float stabilizeWeight = postureProfile.StabilizeWeight;
        if (stabilizeWeight > 0.05f)
        {
            if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
            {
                AddScore(score, (candidate.LikelySafeAttack && snapshot.Opponent.SecurityCount <= 2 ? -90f : -180f) * stabilizeWeight, "pressure is secondary to stabilizing");
            }

            if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon)
            {
                AddScore(score, (candidate.LikelySafeAttack ? 140f : 40f) * stabilizeWeight, "stabilize board");

                if (candidate.TargetIsBlocker)
                {
                    AddScore(score, 40f * stabilizeWeight, "remove blocker while stabilizing");
                }
            }

            if (IsInteractiveEffectCandidate(candidate))
            {
                AddScore(score, 90f * stabilizeWeight, "interaction while stabilizing");
            }

            if (candidate.ActionType == AIMainPhaseActionType.Play && IsSlowTamerSetupIntent(candidate.PlayIntent))
            {
                AddScore(score, -150f * stabilizeWeight, "slow setup while behind");
            }
        }

        float developWeight = postureProfile.DevelopWeight;
        if (developWeight > 0.05f)
        {
            if (candidate.ActionType == AIMainPhaseActionType.Play)
            {
                ApplySafeDevelopmentPlayBias(candidate, score, developWeight);
            }

            if (candidate.ActionType == AIMainPhaseActionType.Digivolve
                || candidate.ActionType == AIMainPhaseActionType.Jogress
                || candidate.ActionType == AIMainPhaseActionType.Burst
                || candidate.ActionType == AIMainPhaseActionType.AppFusion)
            {
                AddScore(score, 70f * developWeight, "safe development window");
            }
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity && candidate.LikelySafeAttack)
        {
            if (snapshot.Race.SecurityDelta >= 0 && snapshot.Race.BoardValueDelta >= 0 && convertWeight > 0.05f)
            {
                AddScore(score, 40f * convertWeight, "safe pressure with board lead");
            }
            else if (snapshot.Self.SecurityCount <= snapshot.Opponent.SecurityCount && postureProfile.RaceWeight > 0.05f)
            {
                AddScore(score, 30f * postureProfile.RaceWeight, "safe pressure while racing");
            }
        }
    }

    static void ApplyAntiLethalHeuristics(AISnapshot snapshot, AIMainPhaseCandidate candidate, AIActionScore score)
    {
        if (snapshot == null || snapshot.Race == null || candidate == null)
        {
            return;
        }

        if (!snapshot.Race.OpponentCanPunishSlowTurn)
        {
            return;
        }

        float dangerScale = Mathf.Clamp(snapshot.Race.OpponentVisibleCrackbackScore - snapshot.Race.SelfDefensiveBuffer + 1, 0, 4);
        bool dangerousTempoWindow = snapshot.Race.OpponentHasDangerousCrackback;
        bool handsAwayTempo = Mathf.Abs(candidate.ProjectedMemory) >= 2;

        if (candidate.ActionType == AIMainPhaseActionType.EndTurn && dangerousTempoWindow)
        {
            AddScore(score, -140f, "pass gives opponent crackback window");
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity && candidate.LikelySafeAttack && dangerousTempoWindow)
        {
            AddScore(score, 35f + (10f * dangerScale), "push pressure before crackback");
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon
            && dangerousTempoWindow
            && (candidate.TargetIsBlocker || candidate.LikelySafeAttack))
        {
            AddScore(score, 30f + (8f * dangerScale), "contest crackback board");
        }

        if (candidate.ActionType == AIMainPhaseActionType.Play)
        {
            if (IsSlowTamerSetupIntent(candidate.PlayIntent))
            {
                float penalty = 65f + (18f * dangerScale);
                if (dangerousTempoWindow)
                {
                    penalty += 35f;
                }
                if (handsAwayTempo)
                {
                    penalty += 18f;
                }

                AddScore(score, -penalty, "visible crackback punishes slow setup");
            }
            else if (handsAwayTempo && candidate.PlayIntent == AIPlayIntent.DrawFilterOption)
            {
                AddScore(score, -(42f + (10f * dangerScale)), "slow draw/filter opens crackback");
            }
            else if (handsAwayTempo && candidate.PlayIntent == AIPlayIntent.BodyDevelopment && dangerousTempoWindow)
            {
                AddScore(score, -(35f + (10f * dangerScale)), "slow body development opens crackback");
            }
            else if (dangerousTempoWindow && (candidate.PlayIntent == AIPlayIntent.RemovalOption || candidate.PlayIntent == AIPlayIntent.TempoOption || candidate.PlayIntent == AIPlayIntent.ProtectionOption || candidate.PlayIntent == AIPlayIntent.Floodgate))
            {
                AddScore(score, 24f + (8f * dangerScale), "interactive play helps respect crackback");
            }
        }

        if (IsUseEffectCandidate(candidate))
        {
            switch (candidate.EffectIntent)
            {
                case AIEffectIntent.DrawFilter:
                case AIEffectIntent.Utility:
                    if (dangerousTempoWindow)
                    {
                        AddScore(score, -(40f + (10f * dangerScale) + (handsAwayTempo ? 15f : 0f)), "slow effect opens crackback");
                    }
                    break;

                case AIEffectIntent.Removal:
                case AIEffectIntent.Tempo:
                case AIEffectIntent.Protection:
                case AIEffectIntent.Floodgate:
                    AddScore(score, 20f + (6f * dangerScale), "interactive effect respects crackback");
                    break;
            }
        }

        if ((candidate.ActionType == AIMainPhaseActionType.Digivolve
                || candidate.ActionType == AIMainPhaseActionType.Jogress
                || candidate.ActionType == AIMainPhaseActionType.Burst
                || candidate.ActionType == AIMainPhaseActionType.AppFusion)
            && handsAwayTempo
            && dangerousTempoWindow
            && !candidate.UnlocksAdditionalPressure)
        {
            AddScore(score, -(30f + (12f * dangerScale)), "slow development opens crackback");
        }
    }

    static PostureProfile FinalizePostureProfile(PostureProfile profile, AISnapshot snapshot, List<AIMainPhaseCandidate> candidates)
    {
        if (profile == null)
        {
            profile = new PostureProfile();
        }

        profile.CloseGameWeight = Mathf.Clamp01(profile.CloseGameWeight);
        profile.StabilizeWeight = Mathf.Clamp01(profile.StabilizeWeight);
        profile.RaceWeight = Mathf.Clamp01(profile.RaceWeight);
        profile.ConvertAdvantageWeight = Mathf.Clamp01(profile.ConvertAdvantageWeight);
        profile.DevelopWeight = Mathf.Clamp01(profile.DevelopWeight);
        profile.ChokeWeight = Mathf.Clamp01(profile.ChokeWeight);
        profile.PrimaryKind = DeterminePrimaryPostureKind(profile);
        profile.DebugGoal = MapDebugGoal(profile.PrimaryKind, snapshot, candidates);
        profile.Reason = BuildPostureReason(profile, snapshot, candidates);
        return profile;
    }

    static PostureKind DeterminePrimaryPostureKind(PostureProfile profile)
    {
        PostureKind[] priority = new[]
        {
            PostureKind.CloseGame,
            PostureKind.Stabilize,
            PostureKind.ConvertAdvantage,
            PostureKind.Race,
            PostureKind.Develop,
            PostureKind.Choke,
        };

        PostureKind best = PostureKind.Develop;
        float bestWeight = -1f;

        for (int i = 0; i < priority.Length; i++)
        {
            PostureKind kind = priority[i];
            float weight = profile.Weight(kind);
            if (weight > bestWeight + 0.001f)
            {
                best = kind;
                bestWeight = weight;
            }
        }

        return best;
    }

    static AITurnGoal MapDebugGoal(PostureKind primaryKind, AISnapshot snapshot, List<AIMainPhaseCandidate> candidates)
    {
        bool hasRemoval = candidates != null && candidates.Any(IsRemovalLikeCandidate);

        switch (primaryKind)
        {
            case PostureKind.CloseGame:
                return AITurnGoal.CloseGame;
            case PostureKind.Stabilize:
                return AITurnGoal.Stabilize;
            case PostureKind.Race:
                return hasRemoval ? AITurnGoal.TempoClear : AITurnGoal.ValueSetup;
            case PostureKind.ConvertAdvantage:
                return snapshot != null && snapshot.Opponent.BlockerCount > 0 && hasRemoval
                    ? AITurnGoal.TempoClear
                    : AITurnGoal.ValueSetup;
            case PostureKind.Develop:
                return HasMeaningfulBreedingStack(snapshot) ? AITurnGoal.BuildStack : AITurnGoal.ValueSetup;
            case PostureKind.Choke:
                return AITurnGoal.MemoryChoke;
            default:
                return AITurnGoal.ValueSetup;
        }
    }

    static string BuildPostureReason(PostureProfile profile, AISnapshot snapshot, List<AIMainPhaseCandidate> candidates)
    {
        string primaryText = $"primary={profile.PrimaryKind}";
        string weights =
            $"close={profile.CloseGameWeight:0.00} " +
            $"stab={profile.StabilizeWeight:0.00} " +
            $"race={profile.RaceWeight:0.00} " +
            $"convert={profile.ConvertAdvantageWeight:0.00} " +
            $"develop={profile.DevelopWeight:0.00} " +
            $"choke={profile.ChokeWeight:0.00}";

        if (string.IsNullOrEmpty(profile.Reason))
        {
            return $"{primaryText}; {weights}";
        }

        return $"{primaryText}; {weights}; {profile.Reason}";
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

    static bool ShouldDefaultRaiseMatureLevelSix(AISnapshot snapshot)
    {
        if (snapshot == null
            || snapshot.Self == null
            || !snapshot.Self.CanMove
            || snapshot.Self.BreedingPermanents.Count == 0)
        {
            return false;
        }

        AISnapshotPermanentView breeding = snapshot.Self.BreedingPermanents[0];
        return breeding != null
            && breeding.IsDigimon
            && breeding.Level >= 6;
    }

    static bool HasClearVisibleRaisePunish(AISnapshot snapshot)
    {
        if (snapshot == null || snapshot.Race == null)
        {
            return false;
        }

        int visibleNextTurnPressure = snapshot.Race.OpponentVisibleNextTurnPressure;
        int selfBuffer = snapshot.Self.SecurityCount + snapshot.Self.BlockerCount + snapshot.Self.CounterPressureScore;
        bool lowSecurity = snapshot.Self.SecurityCount <= 2;
        bool badlyBehindBoard = snapshot.Race.BoardValueDelta <= -4 && snapshot.Race.CounterPressureDelta < 0;

        return snapshot.Race.OpponentHasDangerousCrackback
            || snapshot.Race.ShouldStabilize
            || (lowSecurity && visibleNextTurnPressure >= selfBuffer)
            || (lowSecurity && snapshot.Opponent.BattleDigimonCount >= snapshot.Self.BattleDigimonCount + 2)
            || badlyBehindBoard;
    }

    static bool MoveOutCreatesMeaningfulImmediateValue(AISnapshot snapshot, IReadOnlyList<AIMainPhaseCandidate> projectedCandidates)
    {
        if (projectedCandidates != null && projectedCandidates.Count > 0)
        {
            bool hasVisiblePressureAttack = projectedCandidates.Any(candidate =>
                candidate.ActionType == AIMainPhaseActionType.AttackSecurity
                && (candidate.LikelySafeAttack
                    || candidate.AttackIntent == AIAttackIntent.CloseGame
                    || candidate.UnlocksAdditionalPressure));

            bool hasTempoConversionLine = projectedCandidates.Any(candidate =>
                candidate.ActionType == AIMainPhaseActionType.AttackDigimon
                && ((candidate.TargetIsBlocker && candidate.UnlocksAdditionalPressure)
                    || (candidate.LikelySafeAttack
                        && (candidate.AttackIntent == AIAttackIntent.ClearBlocker
                            || candidate.AttackIntent == AIAttackIntent.RemoveThreat
                            || candidate.AttackIntent == AIAttackIntent.FavorableTrade))));

            if (hasVisiblePressureAttack || hasTempoConversionLine)
            {
                return true;
            }
        }

        if (snapshot == null)
        {
            return false;
        }

        bool lowOpponentSecurity = snapshot.Opponent.SecurityCount <= 3;
        bool alreadyHasBoardToConvert = snapshot.Self.HasEnoughBoardToConvert || snapshot.Self.HasEnoughAttackersToPressure;
        bool convertPosture = snapshot.Race != null && (snapshot.Race.ShouldConvertPressure || snapshot.Race.ImmediatePressureDelta >= 0);
        bool activeBoardPresence = snapshot.Self.BattleDigimonCount > 0 || snapshot.Self.PremiumThreatCount > 0;

        return lowOpponentSecurity
            || alreadyHasBoardToConvert
            || convertPosture
            || activeBoardPresence;
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

    static bool HasStrongSafePressure(AISnapshot snapshot, IReadOnlyList<AIMainPhaseCandidate> candidates)
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

    static float EvaluateOpponentMemoryUsePotential(AISnapshot snapshot)
    {
        if (snapshot == null || snapshot.Opponent == null)
        {
            return 0f;
        }

        float score = 0f;

        if (snapshot.Opponent.HandCount >= 5)
        {
            score += 1f;
        }
        else if (snapshot.Opponent.HandCount >= 3)
        {
            score += 0.5f;
        }

        if (snapshot.Opponent.MaxMemoryCost >= 6)
        {
            score += 1f;
        }
        else if (snapshot.Opponent.MaxMemoryCost >= 4)
        {
            score += 0.5f;
        }

        if (snapshot.Opponent.BattleDigimonCount >= 2)
        {
            score += 0.75f;
        }
        else if (snapshot.Opponent.BattleDigimonCount >= 1)
        {
            score += 0.35f;
        }

        if (snapshot.Opponent.HasOnlineBreedingStack || snapshot.Opponent.VisibleBreedingPressureScore >= 2)
        {
            score += 0.9f;
        }

        if (snapshot.Opponent.PremiumThreatCount > 0)
        {
            score += 0.45f;
        }

        if (snapshot.Opponent.BattleTamerCount > 0)
        {
            score += 0.25f;
        }

        return score;
    }

    static bool SupportsVisiblePressureWhileChoking(AISnapshot snapshot, AIMainPhaseCandidate candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
        {
            return candidate.LikelySafeAttack
                || candidate.UnlocksAdditionalPressure
                || (snapshot != null && snapshot.Opponent.SecurityCount <= 2);
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon)
        {
            return candidate.TargetIsBlocker
                || candidate.UnlocksAdditionalPressure
                || (candidate.LikelySafeAttack && snapshot != null && snapshot.Opponent.SecurityCount <= 2);
        }

        if (candidate.ActionType == AIMainPhaseActionType.Play)
        {
            return candidate.PlayIntent == AIPlayIntent.RemovalOption
                || candidate.PlayIntent == AIPlayIntent.TempoOption
                || candidate.PlayIntent == AIPlayIntent.ProtectionOption
                || candidate.PlayIntent == AIPlayIntent.Finisher
                || candidate.PlayIntent == AIPlayIntent.Floodgate;
        }

        if (candidate.ActionType == AIMainPhaseActionType.Digivolve
            || candidate.ActionType == AIMainPhaseActionType.Jogress
            || candidate.ActionType == AIMainPhaseActionType.Burst
            || candidate.ActionType == AIMainPhaseActionType.AppFusion)
        {
            return candidate.SourceLevel >= 6
                || (snapshot != null && snapshot.Opponent.SecurityCount <= 2);
        }

        if (IsUseEffectCandidate(candidate))
        {
            return candidate.EffectIntent == AIEffectIntent.Removal
                || candidate.EffectIntent == AIEffectIntent.Tempo
                || candidate.EffectIntent == AIEffectIntent.Protection
                || candidate.EffectIntent == AIEffectIntent.Floodgate;
        }

        return false;
    }

    static int CountRemainingPressureAttackers(AIMainPhaseCandidate candidate, IReadOnlyList<AIMainPhaseCandidate> candidates)
    {
        HashSet<int> attackerIds = new HashSet<int>();
        if (candidate == null || candidates == null)
        {
            return 0;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            AIMainPhaseCandidate other = candidates[i];
            if (other == null
                || other == candidate
                || other.ActionType != AIMainPhaseActionType.AttackSecurity
                || other.SourcePermanentIndex < 0)
            {
                continue;
            }

            if ((candidate.ActionType == AIMainPhaseActionType.AttackSecurity || candidate.ActionType == AIMainPhaseActionType.AttackDigimon)
                && other.SourcePermanentIndex == candidate.SourcePermanentIndex)
            {
                continue;
            }

            attackerIds.Add(other.SourcePermanentIndex);
        }

        return attackerIds.Count;
    }

    static int EstimateContinuationPressure(AISnapshot snapshot, AIMainPhaseCandidate candidate, int remainingPressureAttackers)
    {
        int pressure = remainingPressureAttackers;
        if (candidate == null)
        {
            return pressure;
        }

        switch (candidate.ActionType)
        {
            case AIMainPhaseActionType.AttackSecurity:
                if (candidate.UnlocksAdditionalPressure)
                {
                    pressure += 1;
                }
                if (candidate.LikelySafeAttack && IsPremiumAttackCandidate(candidate))
                {
                    pressure += 1;
                }
                break;

            case AIMainPhaseActionType.AttackDigimon:
                if (candidate.TargetIsBlocker && candidate.UnlocksAdditionalPressure)
                {
                    pressure += 1;
                }
                else if (candidate.LikelySafeAttack && snapshot != null && snapshot.Opponent.SecurityCount <= 2)
                {
                    pressure += 1;
                }
                break;

            case AIMainPhaseActionType.Play:
                switch (candidate.PlayIntent)
                {
                    case AIPlayIntent.BodyDevelopment:
                    case AIPlayIntent.Floodgate:
                        pressure += 1;
                        break;

                    case AIPlayIntent.Finisher:
                        pressure += snapshot != null && snapshot.Opponent.SecurityCount <= 2 ? 2 : 1;
                        break;

                    case AIPlayIntent.RemovalOption:
                    case AIPlayIntent.TempoOption:
                    case AIPlayIntent.ProtectionOption:
                        if (snapshot != null && snapshot.Opponent.SecurityCount <= 3)
                        {
                            pressure += 1;
                        }
                        break;
                }
                break;

            case AIMainPhaseActionType.Digivolve:
            case AIMainPhaseActionType.Jogress:
            case AIMainPhaseActionType.Burst:
            case AIMainPhaseActionType.AppFusion:
                if (snapshot != null && snapshot.Race != null && snapshot.Race.SafeToDevelop)
                {
                    pressure += 1;
                }
                if (candidate.SourceLevel >= 6 && snapshot != null && snapshot.Opponent.SecurityCount <= 2)
                {
                    pressure += 1;
                }
                break;

            case AIMainPhaseActionType.UseFieldEffect:
            case AIMainPhaseActionType.UseHandEffect:
            case AIMainPhaseActionType.UseTrashEffect:
                switch (candidate.EffectIntent)
                {
                    case AIEffectIntent.Removal:
                    case AIEffectIntent.Tempo:
                    case AIEffectIntent.Protection:
                    case AIEffectIntent.Floodgate:
                        pressure += 1;
                        break;
                }
                break;
        }

        return pressure;
    }

    static float EstimateContinuationExposureRisk(AISnapshot snapshot, AIMainPhaseCandidate candidate, int remainingPressureAttackers)
    {
        if (snapshot == null || candidate == null)
        {
            return 0f;
        }

        float risk = 0f;

        if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
        {
            if (!candidate.LikelySafeAttack)
            {
                risk += IsPremiumAttackCandidate(candidate) ? 72f : 38f;
            }
            else if (IsPremiumAttackCandidate(candidate)
                && remainingPressureAttackers == 0
                && snapshot.Opponent.SecurityCount > 1
                && !candidate.UnlocksAdditionalPressure)
            {
                risk += 18f;
            }
        }
        else if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon)
        {
            if (!candidate.LikelySafeAttack)
            {
                risk += candidate.TargetDP > candidate.SourceDP ? 64f : 32f;
            }

            if (!candidate.TargetIsBlocker && IsLowValueAttackTarget(candidate) && !candidate.UnlocksAdditionalPressure)
            {
                risk += 24f;
            }
        }
        else if (candidate.ActionType == AIMainPhaseActionType.Play)
        {
            if (IsLowImpactSetupCandidate(candidate) && snapshot.Race != null && snapshot.Race.OpponentCanPunishSlowTurn)
            {
                risk += 32f;
            }

            if (IsLowImpactSetupCandidate(candidate) && snapshot.Self.DevelopmentSufficient)
            {
                risk += 24f;
            }
        }
        else if (IsUseEffectCandidate(candidate))
        {
            if ((candidate.EffectIntent == AIEffectIntent.DrawFilter || candidate.EffectIntent == AIEffectIntent.Utility)
                && snapshot.Race != null
                && snapshot.Race.OpponentCanPunishSlowTurn)
            {
                risk += 28f;
            }
        }

        return risk;
    }

    static bool StrandsFuturePressure(AISnapshot snapshot, AIMainPhaseCandidate candidate, int remainingPressureAttackers, int continuationPressure)
    {
        if (snapshot == null || candidate == null)
        {
            return false;
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
        {
            return remainingPressureAttackers == 0
                && !candidate.UnlocksAdditionalPressure
                && snapshot.Opponent.SecurityCount > 1
                && (!candidate.LikelySafeAttack || candidate.AttackerValueTier == AIAttackerValueTier.Low);
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon)
        {
            return remainingPressureAttackers == 0
                && !candidate.TargetIsBlocker
                && !candidate.UnlocksAdditionalPressure
                && IsLowValueAttackTarget(candidate);
        }

        if (IsLowImpactSetupCandidate(candidate))
        {
            return continuationPressure <= 0
                && (snapshot.Self.HasEnoughAttackersToPressure
                    || (snapshot.Race != null && snapshot.Race.ShouldConvertPressure));
        }

        if ((candidate.ActionType == AIMainPhaseActionType.Digivolve
                || candidate.ActionType == AIMainPhaseActionType.Jogress
                || candidate.ActionType == AIMainPhaseActionType.Burst
                || candidate.ActionType == AIMainPhaseActionType.AppFusion)
            && continuationPressure <= 0
            && snapshot.Race != null
            && snapshot.Race.ShouldConvertPressure)
        {
            return true;
        }

        return false;
    }

    static bool IsLowImpactSetupCandidate(AIMainPhaseCandidate candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.ActionType == AIMainPhaseActionType.Play)
        {
            return candidate.PlayIntent == AIPlayIntent.MemorySetter
                || candidate.PlayIntent == AIPlayIntent.UtilityTamer
                || candidate.PlayIntent == AIPlayIntent.DrawFilterOption;
        }

        return IsUseEffectCandidate(candidate)
            && (candidate.EffectIntent == AIEffectIntent.DrawFilter || candidate.EffectIntent == AIEffectIntent.Utility);
    }

    static bool IsInteractivePlayIntent(AIPlayIntent playIntent)
    {
        return playIntent == AIPlayIntent.RemovalOption
            || playIntent == AIPlayIntent.TempoOption
            || playIntent == AIPlayIntent.ProtectionOption
            || playIntent == AIPlayIntent.Floodgate;
    }

    static bool IsLowValueAttackCandidate(AIMainPhaseCandidate candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackSecurity)
        {
            return candidate.ImmediateSecurityPressure <= 1
                && !candidate.UnlocksAdditionalPressure
                && !candidate.LikelySafeAttack;
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon)
        {
            return !candidate.TargetIsBlocker
                && !candidate.UnlocksAdditionalPressure
                && IsLowValueAttackTarget(candidate);
        }

        return false;
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

    static void ApplyMemoryChokeBias(AISnapshot snapshot, PostureProfile postureProfile, AIMainPhaseCandidate candidate, IReadOnlyList<AIMainPhaseCandidate> candidates, AIActionScore score)
    {
        if (snapshot == null
            || postureProfile == null
            || candidate == null
            || candidates == null
            || postureProfile.ChokeWeight <= 0f)
        {
            return;
        }

        float actionFactor = GetMemoryChokeActionFactor(candidate);
        if (actionFactor <= 0f)
        {
            return;
        }

        float projectedMemoryMagnitude = Mathf.Abs(candidate.ProjectedMemory);
        float baseBias = Mathf.Max(0f, 22f - (projectedMemoryMagnitude * 8f));
        if (baseBias <= 0f)
        {
            return;
        }

        float contextScale = postureProfile.ChokeWeight;
        float opponentMemoryUsePotential = EvaluateOpponentMemoryUsePotential(snapshot);
        bool strongVisiblePressureExists = HasStrongSafePressure(snapshot, candidates);
        bool behindBoardRace = IsBehindBoardRace(snapshot);
        bool preservesPressure = SupportsVisiblePressureWhileChoking(snapshot, candidate);

        if (snapshot.Race != null && snapshot.Race.HasBoardAdvantage)
        {
            contextScale *= 1.15f;
        }

        if (snapshot.Self.DevelopmentSufficient || snapshot.Self.HasEnoughBoardToConvert || snapshot.Self.HasExistingMemorySetter)
        {
            contextScale *= 1.2f;
        }

        if (opponentMemoryUsePotential >= 3f)
        {
            contextScale *= 1.25f;
        }
        else if (opponentMemoryUsePotential < 1.5f)
        {
            contextScale *= 0.72f;
        }

        if (snapshot.Race != null && snapshot.Race.ShouldStabilize)
        {
            contextScale *= 0.08f;
        }
        else if (snapshot.Race != null && snapshot.Race.ShouldConvertPressure)
        {
            contextScale *= preservesPressure ? 0.45f : 0.16f;
        }
        else if (postureProfile.RaceWeight >= 0.45f || (snapshot.Race != null && snapshot.Race.OpponentCanPunishSlowTurn))
        {
            contextScale *= preservesPressure ? 0.65f : 0.3f;
        }

        if (behindBoardRace || snapshot.Self.SecurityCount < snapshot.Opponent.SecurityCount)
        {
            contextScale *= 0.6f;
        }

        if (strongVisiblePressureExists && !preservesPressure)
        {
            contextScale *= 0.18f;
        }

        if (projectedMemoryMagnitude > 0f && opponentMemoryUsePotential >= 2f)
        {
            contextScale *= 1.08f;
        }

        float chokeBias = baseBias * actionFactor * contextScale;
        if (chokeBias < 1f)
        {
            return;
        }

        string reason =
            strongVisiblePressureExists && !preservesPressure ? "memory choke deprioritized behind stronger pressure" :
            snapshot.Race != null && snapshot.Race.ShouldStabilize ? "memory choke heavily reduced while stabilizing" :
            snapshot.Race != null && (snapshot.Race.ShouldConvertPressure || postureProfile.RaceWeight >= 0.45f) ? "memory choke reduced in active race" :
            opponentMemoryUsePotential >= 3f && (snapshot.Self.DevelopmentSufficient || snapshot.Race.HasBoardAdvantage) ? "contextual memory choke punishes opponent spend" :
            "contextual memory choke";

        AddScore(score, chokeBias, reason);
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
                switch (candidate.EffectIntent)
                {
                    case AIEffectIntent.Removal:
                        return 0.95f;
                    case AIEffectIntent.Tempo:
                        return 0.9f;
                    case AIEffectIntent.Protection:
                        return 0.75f;
                    case AIEffectIntent.Floodgate:
                        return 0.7f;
                    case AIEffectIntent.DrawFilter:
                        return 0.5f;
                    case AIEffectIntent.Utility:
                        return 0.45f;
                    case AIEffectIntent.Unknown:
                    default:
                        return 0.35f;
                }

            case AIMainPhaseActionType.Play:
                switch (candidate.PlayIntent)
                {
                    case AIPlayIntent.MemorySetter:
                        return 0.45f;
                    case AIPlayIntent.UtilityTamer:
                        return 0.55f;
                    case AIPlayIntent.BodyDevelopment:
                        return 0.8f;
                    case AIPlayIntent.DrawFilterOption:
                        return 0.55f;
                    case AIPlayIntent.TempoOption:
                        return 0.9f;
                    case AIPlayIntent.RemovalOption:
                        return 0.95f;
                    case AIPlayIntent.ProtectionOption:
                        return 0.8f;
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

    static bool IsUseEffectCandidate(AIMainPhaseCandidate candidate)
    {
        return candidate != null
            && (candidate.ActionType == AIMainPhaseActionType.UseFieldEffect
                || candidate.ActionType == AIMainPhaseActionType.UseHandEffect
                || candidate.ActionType == AIMainPhaseActionType.UseTrashEffect);
    }

    static bool IsInteractiveEffectCandidate(AIMainPhaseCandidate candidate)
    {
        if (!IsUseEffectCandidate(candidate))
        {
            return false;
        }

        switch (candidate.EffectIntent)
        {
            case AIEffectIntent.Removal:
            case AIEffectIntent.Tempo:
            case AIEffectIntent.Protection:
            case AIEffectIntent.Floodgate:
                return true;
            default:
                return false;
        }
    }

    static bool IsRemovalLikeCandidate(AIMainPhaseCandidate candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.ActionType == AIMainPhaseActionType.AttackDigimon)
        {
            return true;
        }

        if (candidate.ActionType == AIMainPhaseActionType.Play)
        {
            return candidate.PlayIntent == AIPlayIntent.RemovalOption;
        }

        return IsUseEffectCandidate(candidate)
            && (candidate.EffectIntent == AIEffectIntent.Removal || candidate.EffectIntent == AIEffectIntent.Tempo);
    }

    static bool CreatesBoardThreat(AIMainPhaseCandidate candidate)
    {
        if (candidate == null || candidate.ActionType != AIMainPhaseActionType.Play)
        {
            return false;
        }

        return candidate.PlayIntent == AIPlayIntent.BodyDevelopment
            || candidate.PlayIntent == AIPlayIntent.Floodgate
            || candidate.PlayIntent == AIPlayIntent.Finisher;
    }

    static void ApplyEffectHeuristics(AISnapshot snapshot, AIMainPhaseCandidate candidate, AIActionScore score)
    {
        switch (candidate.EffectIntent)
        {
            case AIEffectIntent.Removal:
                AddScore(score, 94f, "removal effect");
                if (snapshot != null && snapshot.Opponent.BattleDigimonCount > 0)
                {
                    AddScore(score, 42f, "opponent has board to answer");
                }
                if (snapshot != null && snapshot.Race != null && snapshot.Race.ShouldStabilize)
                {
                    AddScore(score, 50f, "removal helps stabilize");
                }
                else if (snapshot != null && snapshot.Race != null && snapshot.Race.ShouldConvertPressure)
                {
                    AddScore(score, 28f, "removal clears path for pressure");
                }
                break;

            case AIEffectIntent.DrawFilter:
                AddScore(score, 70f, "draw/filter effect");
                if (snapshot != null && snapshot.TurnCount <= 3 && !IsMidgameRace(snapshot))
                {
                    AddScore(score, 20f, "early card smoothing");
                }
                if (snapshot != null && snapshot.Self.DevelopmentSufficient)
                {
                    AddScore(score, -34f, "setup is already sufficient");
                }
                if (snapshot != null && snapshot.Race != null && (snapshot.Race.ShouldConvertPressure || snapshot.Race.ShouldStabilize))
                {
                    AddScore(score, -42f, "race state punishes slow value effect");
                }
                break;

            case AIEffectIntent.Tempo:
                AddScore(score, 88f, "tempo effect");
                if (snapshot != null && snapshot.Race != null && (snapshot.Race.ShouldConvertPressure || snapshot.Race.ShouldStabilize))
                {
                    AddScore(score, 34f, "tempo effect fits race");
                }
                break;

            case AIEffectIntent.Protection:
                AddScore(score, 82f, "protection effect");
                if (snapshot != null && (snapshot.Self.BattleDigimonCount > 0 || snapshot.Self.HasOnlineBreedingStack || HasMeaningfulBreedingStack(snapshot)))
                {
                    AddScore(score, 28f, "protects active pressure");
                }
                if (snapshot != null && snapshot.Race != null && (snapshot.Race.ShouldConvertPressure || snapshot.Race.ShouldStabilize))
                {
                    AddScore(score, 20f, "protection supports live race");
                }
                if (snapshot != null && snapshot.Self.BattleDigimonCount == 0 && !snapshot.Self.HasOnlineBreedingStack && !HasMeaningfulBreedingStack(snapshot))
                {
                    AddScore(score, -26f, "little board to protect");
                }
                break;

            case AIEffectIntent.Floodgate:
                AddScore(score, 80f, "floodgate effect");
                if (snapshot != null && (snapshot.Opponent.ImmediatePressureScore >= 2 || (snapshot.Race != null && snapshot.Race.OpponentHasDangerousCrackback)))
                {
                    AddScore(score, 24f, "floodgate disrupts active race");
                }
                break;

            case AIEffectIntent.Utility:
                AddScore(score, 58f, "utility effect");
                break;

            case AIEffectIntent.Unknown:
            default:
                AddScore(score, 42f, "unknown effect");
                break;
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
        bool developmentSufficient = snapshot != null && snapshot.Self.DevelopmentSufficient;

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
                if (developmentSufficient && snapshot.Self.HasEnoughBoardToConvert)
                {
                    AddScore(score, -75f, "board already has enough bodies to convert");
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
                if (snapshot != null && snapshot.Self.HasExistingMemorySetter)
                {
                    AddScore(score, -170f, "existing memory setter already online");
                }
                if (developmentSufficient)
                {
                    AddScore(score, -145f, "development already sufficient");
                }
                if (snapshot != null && snapshot.Self.HasEnoughBoardToConvert)
                {
                    AddScore(score, -85f, "board already ready to convert");
                }
                if (snapshot != null && snapshot.Self.HasEnoughAttackersToPressure)
                {
                    AddScore(score, -70f, "attackers already online");
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
                if (developmentSufficient)
                {
                    AddScore(score, -125f, "development already sufficient");
                }
                if (snapshot != null && snapshot.Self.HasEnoughBoardToConvert)
                {
                    AddScore(score, -70f, "board already ready to convert");
                }
                if (snapshot != null && snapshot.Self.HasEnoughAttackersToPressure)
                {
                    AddScore(score, -55f, "attackers already online");
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

            case AIPlayIntent.DrawFilterOption:
                AddScore(score, 72f, "draw/filter option");
                if (!midgameRace && snapshot != null && snapshot.TurnCount <= 3)
                {
                    AddScore(score, 24f, "early smoothing window");
                }
                if (pressuredState)
                {
                    AddScore(score, -48f, "race state punishes slow draw/filter");
                }
                if (developmentSufficient)
                {
                    AddScore(score, -36f, "setup is already sufficient");
                }
                break;

            case AIPlayIntent.ProtectionOption:
                AddScore(score, 80f, "protection option");
                if (snapshot != null && (snapshot.Self.BattleDigimonCount > 0 || snapshot.Self.HasOnlineBreedingStack))
                {
                    AddScore(score, 30f, "protect active threat");
                }
                if (snapshot != null && snapshot.Race != null && (snapshot.Race.ShouldConvertPressure || snapshot.Race.ShouldStabilize))
                {
                    AddScore(score, 24f, "protection supports live race");
                }
                if (snapshot != null && snapshot.Self.BattleDigimonCount == 0 && !snapshot.Self.HasOnlineBreedingStack)
                {
                    AddScore(score, -28f, "little board to protect");
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

    static void ApplySafeDevelopmentPlayBias(AIMainPhaseCandidate candidate, AIActionScore score, float scale = 1f)
    {
        if (candidate == null || scale <= 0f)
        {
            return;
        }

        switch (candidate.PlayIntent)
        {
            case AIPlayIntent.MemorySetter:
                AddScore(score, 88f * scale, "safe setup window");
                break;

            case AIPlayIntent.UtilityTamer:
                AddScore(score, 70f * scale, "safe setup window");
                break;

            case AIPlayIntent.BodyDevelopment:
                AddScore(score, 42f * scale, "safe body development");
                break;

            case AIPlayIntent.DrawFilterOption:
                AddScore(score, 28f * scale, "safe card smoothing");
                break;

            case AIPlayIntent.Floodgate:
                AddScore(score, 46f * scale, "safe disruptive development");
                break;

            case AIPlayIntent.Finisher:
                AddScore(score, 24f * scale, "safe premium development");
                break;

            case AIPlayIntent.ProtectionOption:
                AddScore(score, 24f * scale, "safe protection setup");
                break;

            case AIPlayIntent.RemovalOption:
            case AIPlayIntent.TempoOption:
                AddScore(score, 20f * scale, "safe value development");
                break;

            case AIPlayIntent.Unknown:
            default:
                AddScore(score, 18f * scale, "safe value development");
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

        if (snapshot.Self.DevelopmentSufficient)
        {
            AddScore(
                score,
                candidate.PlayIntent == AIPlayIntent.MemorySetter ? -120f : -100f,
                "development sufficiency reduces setup need");
        }

        if (snapshot.Self.HasOnlineBreedingStack)
        {
            AddScore(
                score,
                candidate.PlayIntent == AIPlayIntent.MemorySetter ? -60f : -50f,
                "online breeding stack reduces setup need");
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
