using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class AIMainPhaseCandidateBuilder
{
    struct SemanticSignals
    {
        public bool HasRemoval;
        public bool HasDrawFilter;
        public bool HasTempo;
        public bool HasProtection;
        public bool HasFloodgate;
    }

    public static List<AIMainPhaseCandidate> Build(GameContext gameContext, Player player)
    {
        List<AIMainPhaseCandidate> candidates = new List<AIMainPhaseCandidate>();

        if (gameContext == null || player == null)
        {
            return candidates;
        }

        List<Permanent> yourField = player.GetFieldPermanents();
        List<Permanent> enemyField = player.Enemy != null ? player.Enemy.GetFieldPermanents() : new List<Permanent>();

        candidates.Add(new AIMainPhaseCandidate
        {
            ActionType = AIMainPhaseActionType.EndTurn,
            Summary = "End Turn",
            ProjectedMemory = gameContext.Memory,
        });

        for (int permanentIndex = 0; permanentIndex < yourField.Count; permanentIndex++)
        {
            Permanent permanent = yourField[permanentIndex];
            if (permanent == null || permanent.TopCard == null)
            {
                continue;
            }

            AddFieldEffectCandidates(candidates, permanent, permanentIndex, gameContext);
            AddAttackCandidates(candidates, permanent, permanentIndex, yourField, enemyField, gameContext);
        }

        AddHandPlayCandidates(candidates, player, gameContext);
        AddHandSkillCandidates(candidates, player.HandCards, SelectCardEffect.Root.Hand, AIMainPhaseActionType.UseHandEffect, gameContext);
        AddHandSkillCandidates(candidates, player.TrashCards, SelectCardEffect.Root.Trash, AIMainPhaseActionType.UseTrashEffect, gameContext);

        return candidates
            .GroupBy(Signature)
            .Select(group => group.First())
            .ToList();
    }

    public static List<AIMainPhaseCandidate> BuildForProjectedMoveOut(GameContext gameContext, Player player)
    {
        List<AIMainPhaseCandidate> candidates = Build(gameContext, player);

        if (gameContext == null || player == null)
        {
            return candidates;
        }

        Permanent breedingPermanent = player.GetBreedingAreaPermanents()
            .FirstOrDefault(permanent => permanent != null && permanent.TopCard != null && permanent.CanMove);

        if (breedingPermanent == null)
        {
            return candidates;
        }

        List<Permanent> yourField = player.GetFieldPermanents();
        List<Permanent> enemyField = player.Enemy != null ? player.Enemy.GetFieldPermanents() : new List<Permanent>();
        AddProjectedMoveOutAttackCandidates(candidates, breedingPermanent, yourField, enemyField, gameContext);

        return candidates
            .GroupBy(Signature)
            .Select(group => group.First())
            .ToList();
    }

    static void AddAttackCandidates(List<AIMainPhaseCandidate> candidates, Permanent attacker, int attackerIndex, List<Permanent> yourField, List<Permanent> enemyField, GameContext gameContext)
    {
        if (!attacker.CanAttack(null))
        {
            return;
        }

        AIAttackerValueTier attackerValueTier = DetermineAttackerValueTier(attacker);
        int visibleBlockerCount = enemyField.Count(permanent => permanent != null && permanent.TopCard != null && permanent.HasBlocker);
        int otherPressureAttackers = CountOtherSecurityAttackers(yourField, attacker);
        bool hasHigherValueFollowUp = HasHigherValueFollowUpAttacker(yourField, attacker, attackerValueTier);
        int opponentSecurityCount = attacker.TopCard != null && attacker.TopCard.Owner != null && attacker.TopCard.Owner.Enemy != null
            ? attacker.TopCard.Owner.Enemy.SecurityCards.Count
            : 0;

        if (attacker.CanAttackTargetDigimon(null, null))
        {
            bool closeGameAttack = opponentSecurityCount <= 1;
            bool unlocksAdditionalPressure = otherPressureAttackers > 0 && (attackerValueTier == AIAttackerValueTier.Low || hasHigherValueFollowUp);
            candidates.Add(new AIMainPhaseCandidate
            {
                ActionType = AIMainPhaseActionType.AttackSecurity,
                Summary = $"Attack security with {attacker.TopCard.BaseENGCardNameFromEntity}",
                SourceName = attacker.TopCard.BaseENGCardNameFromEntity,
                SourcePermanentIndex = attackerIndex,
                SourceLevel = NormalizeLevel(attacker.Level),
                SourceDP = attacker.DP,
                SourceStackCount = attacker.StackCards.Count,
                SourceHasBlocker = attacker.HasBlocker,
                SourceIsSuspended = attacker.IsSuspended,
                SourceInBreeding = attacker.PermanentFrame != null && attacker.PermanentFrame.isBreedingAreaFrame(),
                ProjectedMemory = gameContext.Memory,
                ImmediateSecurityPressure = 1,
                AttackIntent = DetermineSecurityAttackIntent(closeGameAttack, attackerValueTier, otherPressureAttackers, hasHigherValueFollowUp),
                AttackerValueTier = attackerValueTier,
                LikelySafeAttack = IsLikelySafeSecurityAttack(closeGameAttack, visibleBlockerCount, attackerValueTier, attacker),
                UnlocksAdditionalPressure = unlocksAdditionalPressure,
            });
        }

        for (int defenderIndex = 0; defenderIndex < enemyField.Count; defenderIndex++)
        {
            Permanent defender = enemyField[defenderIndex];
            if (defender == null || defender.TopCard == null)
            {
                continue;
            }

            if (!attacker.CanAttackTargetDigimon(defender, null))
            {
                continue;
            }

            bool unlocksAdditionalPressure = defender.HasBlocker && otherPressureAttackers > 0;
            bool likelySafeAttack = IsLikelySafeDigimonAttack(attacker, defender);

            candidates.Add(new AIMainPhaseCandidate
            {
                ActionType = AIMainPhaseActionType.AttackDigimon,
                Summary = $"Attack {defender.TopCard.BaseENGCardNameFromEntity} with {attacker.TopCard.BaseENGCardNameFromEntity}",
                SourceName = attacker.TopCard.BaseENGCardNameFromEntity,
                TargetName = defender.TopCard.BaseENGCardNameFromEntity,
                SourcePermanentIndex = attackerIndex,
                AttackTargetPermanentIndex = defenderIndex,
                SourceLevel = NormalizeLevel(attacker.Level),
                SourceDP = attacker.DP,
                SourceStackCount = attacker.StackCards.Count,
                SourceHasBlocker = attacker.HasBlocker,
                SourceIsSuspended = attacker.IsSuspended,
                SourceInBreeding = attacker.PermanentFrame != null && attacker.PermanentFrame.isBreedingAreaFrame(),
                TargetLevel = NormalizeLevel(defender.Level),
                TargetDP = defender.DP,
                TargetIsBlocker = defender.HasBlocker,
                ProjectedMemory = gameContext.Memory,
                AttackIntent = DetermineDigimonAttackIntent(defender, attacker, likelySafeAttack, unlocksAdditionalPressure, opponentSecurityCount),
                AttackerValueTier = attackerValueTier,
                LikelySafeAttack = likelySafeAttack,
                UnlocksAdditionalPressure = unlocksAdditionalPressure,
            });
        }
    }

    static void AddProjectedMoveOutAttackCandidates(List<AIMainPhaseCandidate> candidates, Permanent attacker, List<Permanent> yourField, List<Permanent> enemyField, GameContext gameContext)
    {
        if (!CanProjectedMoveOutAttack(attacker, gameContext))
        {
            return;
        }

        int attackerIndex = yourField.IndexOf(attacker);
        if (attackerIndex < 0)
        {
            attackerIndex = yourField.Count;
        }

        AIAttackerValueTier attackerValueTier = DetermineAttackerValueTier(attacker);
        int visibleBlockerCount = enemyField.Count(permanent => permanent != null && permanent.TopCard != null && permanent.HasBlocker);
        int otherPressureAttackers = CountOtherSecurityAttackers(yourField, attacker);
        bool hasHigherValueFollowUp = HasHigherValueFollowUpAttacker(yourField, attacker, attackerValueTier);
        int opponentSecurityCount = attacker.TopCard != null && attacker.TopCard.Owner != null && attacker.TopCard.Owner.Enemy != null
            ? attacker.TopCard.Owner.Enemy.SecurityCards.Count
            : 0;

        bool closeGameAttack = opponentSecurityCount <= 1;
        bool unlocksAdditionalPressure = otherPressureAttackers > 0 && (attackerValueTier == AIAttackerValueTier.Low || hasHigherValueFollowUp);
        candidates.Add(new AIMainPhaseCandidate
        {
            ActionType = AIMainPhaseActionType.AttackSecurity,
            Summary = $"Move out then attack security with {attacker.TopCard.BaseENGCardNameFromEntity}",
            SourceName = attacker.TopCard.BaseENGCardNameFromEntity,
            SourcePermanentIndex = attackerIndex,
            SourceLevel = NormalizeLevel(attacker.Level),
            SourceDP = attacker.DP,
            SourceStackCount = attacker.StackCards.Count,
            SourceHasBlocker = attacker.HasBlocker,
            SourceIsSuspended = attacker.IsSuspended,
            SourceInBreeding = false,
            ProjectedMemory = gameContext.Memory,
            ImmediateSecurityPressure = 1,
            AttackIntent = DetermineSecurityAttackIntent(closeGameAttack, attackerValueTier, otherPressureAttackers, hasHigherValueFollowUp),
            AttackerValueTier = attackerValueTier,
            LikelySafeAttack = IsLikelySafeSecurityAttack(closeGameAttack, visibleBlockerCount, attackerValueTier, attacker),
            UnlocksAdditionalPressure = unlocksAdditionalPressure,
        });

        for (int defenderIndex = 0; defenderIndex < enemyField.Count; defenderIndex++)
        {
            Permanent defender = enemyField[defenderIndex];
            if (!CanProjectedMoveOutAttackTargetDigimon(attacker, defender, gameContext))
            {
                continue;
            }

            bool projectedUnlocksAdditionalPressure = defender.HasBlocker && otherPressureAttackers > 0;
            bool likelySafeAttack = IsLikelySafeDigimonAttack(attacker, defender);

            candidates.Add(new AIMainPhaseCandidate
            {
                ActionType = AIMainPhaseActionType.AttackDigimon,
                Summary = $"Move out then attack {defender.TopCard.BaseENGCardNameFromEntity} with {attacker.TopCard.BaseENGCardNameFromEntity}",
                SourceName = attacker.TopCard.BaseENGCardNameFromEntity,
                TargetName = defender.TopCard.BaseENGCardNameFromEntity,
                SourcePermanentIndex = attackerIndex,
                AttackTargetPermanentIndex = defenderIndex,
                SourceLevel = NormalizeLevel(attacker.Level),
                SourceDP = attacker.DP,
                SourceStackCount = attacker.StackCards.Count,
                SourceHasBlocker = attacker.HasBlocker,
                SourceIsSuspended = attacker.IsSuspended,
                SourceInBreeding = false,
                TargetLevel = NormalizeLevel(defender.Level),
                TargetDP = defender.DP,
                TargetIsBlocker = defender.HasBlocker,
                ProjectedMemory = gameContext.Memory,
                AttackIntent = DetermineDigimonAttackIntent(defender, attacker, likelySafeAttack, projectedUnlocksAdditionalPressure, opponentSecurityCount),
                AttackerValueTier = attackerValueTier,
                LikelySafeAttack = likelySafeAttack,
                UnlocksAdditionalPressure = projectedUnlocksAdditionalPressure,
            });
        }
    }

    static void AddFieldEffectCandidates(List<AIMainPhaseCandidate> candidates, Permanent permanent, int permanentIndex, GameContext gameContext)
    {
        List<ICardEffect> effects = permanent.EffectList(EffectTiming.OnDeclaration);
        for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
        {
            ICardEffect effect = effects[effectIndex];
            if (!(effect is ActivateICardEffect) || !effect.CanUse(null))
            {
                continue;
            }

            candidates.Add(new AIMainPhaseCandidate
            {
                ActionType = AIMainPhaseActionType.UseFieldEffect,
                Summary = $"Use field effect {effect.EffectName} from {permanent.TopCard.BaseENGCardNameFromEntity}",
                SourceName = permanent.TopCard.BaseENGCardNameFromEntity,
                SourcePermanentIndex = permanentIndex,
                SkillIndex = effectIndex,
                SourceLevel = NormalizeLevel(permanent.Level),
                SourceDP = permanent.DP,
                SourceStackCount = permanent.StackCards.Count,
                ProjectedMemory = gameContext.Memory,
                EffectIntent = DetermineEffectIntent(permanent.TopCard, effect),
                DownstreamResolutionNotControlled = true,
            });
        }
    }

    static void AddHandPlayCandidates(List<AIMainPhaseCandidate> candidates, Player player, GameContext gameContext)
    {
        foreach (CardSource card in player.HandCards)
        {
            if (card == null)
            {
                continue;
            }

            if (card.IsOption && card.CanPlayFromHandDuringMainPhase)
            {
                int optionCost = card.PayingCost(SelectCardEffect.Root.Hand, null, checkAvailability: true);
                candidates.Add(new AIMainPhaseCandidate
                {
                    ActionType = AIMainPhaseActionType.Play,
                    Summary = $"Play option {card.BaseENGCardNameFromEntity}",
                    SourceName = card.BaseENGCardNameFromEntity,
                    CardIndex = card.CardIndex,
                    SourceCardKind = card.CardKind,
                    SourceLevel = card.HasLevel ? card.Level : 0,
                    MemoryCost = optionCost,
                    ProjectedMemory = player.ExpectedMemory(optionCost),
                    PlayIntent = DeterminePlayIntent(card),
                    EffectIntent = DetermineEffectIntent(card, null),
                    DownstreamResolutionNotControlled = true,
                });
            }

            if (card.IsPermanent)
            {
                AddNormalPlayCandidates(candidates, player, card);
                AddJogressCandidates(candidates, player, card);
                AddBurstCandidates(candidates, player, card);
                AddAppFusionCandidates(candidates, player, card);
            }
        }
    }

    static void AddNormalPlayCandidates(List<AIMainPhaseCandidate> candidates, Player player, CardSource card)
    {
        bool addedEmptyFrameCandidate = false;

        foreach (FieldCardFrame frame in player.fieldCardFrames)
        {
            if (!card.CanPlayCardTargetFrame(frame, true, null))
            {
                continue;
            }

            Permanent targetPermanent = frame.GetFramePermanent();
            int targetFrameId = frame.FrameID;
            bool targetsOccupiedFrame = targetPermanent != null;

            if (!targetsOccupiedFrame)
            {
                if (addedEmptyFrameCandidate)
                {
                    continue;
                }

                FieldCardFrame preferredFrame = card.PreferredFrame();
                if (preferredFrame != null)
                {
                    targetFrameId = preferredFrame.FrameID;
                }

                addedEmptyFrameCandidate = true;
            }

            int cost = card.PayingCost(SelectCardEffect.Root.Hand, new List<Permanent> { targetPermanent }, checkAvailability: true);

            candidates.Add(new AIMainPhaseCandidate
            {
                ActionType = targetsOccupiedFrame ? AIMainPhaseActionType.Digivolve : AIMainPhaseActionType.Play,
                Summary = targetsOccupiedFrame
                    ? $"Digivolve {card.BaseENGCardNameFromEntity} onto {targetPermanent.TopCard.BaseENGCardNameFromEntity}"
                    : $"Play {card.BaseENGCardNameFromEntity}",
                SourceName = card.BaseENGCardNameFromEntity,
                TargetName = targetPermanent != null && targetPermanent.TopCard != null ? targetPermanent.TopCard.BaseENGCardNameFromEntity : "",
                CardIndex = card.CardIndex,
                TargetFrameID = targetFrameId,
                SourceCardKind = card.CardKind,
                SourceLevel = card.HasLevel ? card.Level : 0,
                TargetLevel = targetPermanent != null ? NormalizeLevel(targetPermanent.Level) : 0,
                TargetDP = targetPermanent != null ? targetPermanent.DP : 0,
                TargetsOccupiedFrame = targetsOccupiedFrame,
                MemoryCost = cost,
                ProjectedMemory = player.ExpectedMemory(cost),
                PlayIntent = targetsOccupiedFrame ? AIPlayIntent.Unknown : DeterminePlayIntent(card),
            });
        }
    }

    static void AddJogressCandidates(List<AIMainPhaseCandidate> candidates, Player player, CardSource card)
    {
        if (!card.CanPlayJogress(true) || card.jogressCondition == null)
        {
            return;
        }

        List<Permanent> battleDigimon = player.GetBattleAreaDigimons();
        for (int leftIndex = 0; leftIndex < battleDigimon.Count; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1; rightIndex < battleDigimon.Count; rightIndex++)
            {
                Permanent left = battleDigimon[leftIndex];
                Permanent right = battleDigimon[rightIndex];
                if (left == null || right == null || left.PermanentFrame == null || right.PermanentFrame == null)
                {
                    continue;
                }

                int cost = TryGetJogressCost(card, left, right);
                if (cost < 0)
                {
                    continue;
                }

                int[] roots = new[] { left.PermanentFrame.FrameID, right.PermanentFrame.FrameID };
                Array.Sort(roots);

                candidates.Add(new AIMainPhaseCandidate
                {
                    ActionType = AIMainPhaseActionType.Jogress,
                    Summary = $"Jogress {card.BaseENGCardNameFromEntity} using {left.TopCard.BaseENGCardNameFromEntity} + {right.TopCard.BaseENGCardNameFromEntity}",
                    SourceName = card.BaseENGCardNameFromEntity,
                    TargetName = $"{left.TopCard.BaseENGCardNameFromEntity}/{right.TopCard.BaseENGCardNameFromEntity}",
                    CardIndex = card.CardIndex,
                    TargetFrameID = roots[0],
                    JogressEvoRootsFrameIDs = roots,
                    SourceCardKind = card.CardKind,
                    SourceLevel = card.HasLevel ? card.Level : 0,
                    TargetLevel = Mathf.Max(NormalizeLevel(left.Level), NormalizeLevel(right.Level)),
                    MemoryCost = cost,
                    ProjectedMemory = player.ExpectedMemory(cost),
                });
            }
        }
    }

    static void AddBurstCandidates(List<AIMainPhaseCandidate> candidates, Player player, CardSource card)
    {
        if (!card.CanPlayBurst(true) || card.burstDigivolutionCondition == null)
        {
            return;
        }

        List<Permanent> availableTargets = new List<Permanent>();
        availableTargets.AddRange(player.GetBattleAreaDigimons());
        availableTargets.AddRange(player.GetBreedingAreaPermanents());

        foreach (Permanent targetPermanent in availableTargets)
        {
            if (targetPermanent == null || targetPermanent.PermanentFrame == null)
            {
                continue;
            }

            if (!card.CanBurstDigivolutionFromTargetPermanent(targetPermanent, true))
            {
                continue;
            }

            Permanent tamer = player.GetBattleAreaPermanents()
                .FirstOrDefault(permanent => permanent != targetPermanent
                    && card.burstDigivolutionCondition.tamerCondition != null
                    && card.burstDigivolutionCondition.tamerCondition(permanent)
                    && !permanent.CannotReturnToHand(null));

            if (tamer == null || tamer.PermanentFrame == null)
            {
                continue;
            }

            int cost = card.GetChangedCostItselef(card.burstDigivolutionCondition.cost, SelectCardEffect.Root.Hand, new List<Permanent> { targetPermanent }, checkAvailability: true);

            candidates.Add(new AIMainPhaseCandidate
            {
                ActionType = AIMainPhaseActionType.Burst,
                Summary = $"Burst digivolve {card.BaseENGCardNameFromEntity} onto {targetPermanent.TopCard.BaseENGCardNameFromEntity}",
                SourceName = card.BaseENGCardNameFromEntity,
                TargetName = targetPermanent.TopCard.BaseENGCardNameFromEntity,
                CardIndex = card.CardIndex,
                TargetFrameID = targetPermanent.PermanentFrame.FrameID,
                BurstTamerFrameID = tamer.PermanentFrame.FrameID,
                SourceCardKind = card.CardKind,
                SourceLevel = card.HasLevel ? card.Level : 0,
                TargetLevel = NormalizeLevel(targetPermanent.Level),
                MemoryCost = cost,
                ProjectedMemory = player.ExpectedMemory(cost),
            });
        }
    }

    static void AddAppFusionCandidates(List<AIMainPhaseCandidate> candidates, Player player, CardSource card)
    {
        if (card.appFusionCondition == null)
        {
            return;
        }

        foreach (Permanent targetPermanent in player.GetFieldPermanents())
        {
            if (targetPermanent == null || targetPermanent.PermanentFrame == null)
            {
                continue;
            }

            if (!card.CanAppFusionFromTargetPermanent(targetPermanent, true))
            {
                continue;
            }

            CardSource linkCard = targetPermanent.LinkedCards.FirstOrDefault(link => card.appFusionCondition.linkedCondition != null && card.appFusionCondition.linkedCondition(targetPermanent, link));
            if (linkCard == null)
            {
                continue;
            }

            int cost = card.GetChangedCostItselef(card.appFusionCondition.cost, SelectCardEffect.Root.Hand, new List<Permanent> { targetPermanent }, checkAvailability: true);

            candidates.Add(new AIMainPhaseCandidate
            {
                ActionType = AIMainPhaseActionType.AppFusion,
                Summary = $"App Fusion {card.BaseENGCardNameFromEntity} onto {targetPermanent.TopCard.BaseENGCardNameFromEntity}",
                SourceName = card.BaseENGCardNameFromEntity,
                TargetName = targetPermanent.TopCard.BaseENGCardNameFromEntity,
                CardIndex = card.CardIndex,
                TargetFrameID = targetPermanent.PermanentFrame.FrameID,
                AppFusionFrameIDs = new[] { targetPermanent.PermanentFrame.FrameID, targetPermanent.LinkedCards.IndexOf(linkCard) },
                SourceCardKind = card.CardKind,
                SourceLevel = card.HasLevel ? card.Level : 0,
                TargetLevel = NormalizeLevel(targetPermanent.Level),
                MemoryCost = cost,
                ProjectedMemory = player.ExpectedMemory(cost),
            });
        }
    }

    static void AddHandSkillCandidates(List<AIMainPhaseCandidate> candidates, List<CardSource> cards, SelectCardEffect.Root root, AIMainPhaseActionType actionType, GameContext gameContext)
    {
        foreach (CardSource card in cards)
        {
            if (card == null || !card.CanDeclareSkill)
            {
                continue;
            }

            List<ICardEffect> effects = card.EffectList(EffectTiming.OnDeclaration);
            for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
            {
                ICardEffect effect = effects[effectIndex];
                if (!(effect is ActivateICardEffect) || !effect.CanUse(null))
                {
                    continue;
                }

                int cost = 0;
                if (card.HasUseCost || root != SelectCardEffect.Root.Hand)
                {
                    cost = card.PayingCost(root, null, checkAvailability: true);
                }

                candidates.Add(new AIMainPhaseCandidate
                {
                    ActionType = actionType,
                    Summary = $"Use {root} effect {effect.EffectName} from {card.BaseENGCardNameFromEntity}",
                    SourceName = card.BaseENGCardNameFromEntity,
                    CardIndex = card.CardIndex,
                    SkillIndex = effectIndex,
                    SourceCardKind = card.CardKind,
                    SourceLevel = card.HasLevel ? card.Level : 0,
                    MemoryCost = cost,
                    ProjectedMemory = card.Owner != null ? card.Owner.ExpectedMemory(cost) : gameContext.Memory,
                    EffectIntent = DetermineEffectIntent(card, effect),
                    DownstreamResolutionNotControlled = true,
                });
            }
        }
    }

    static int TryGetJogressCost(CardSource card, Permanent left, Permanent right)
    {
        int bestCost = int.MaxValue;

        foreach (JogressCondition condition in card.jogressCondition)
        {
            if (condition == null || condition.elements == null || condition.elements.Length != 2)
            {
                continue;
            }

            if (MatchesJogressCondition(condition, left, right))
            {
                int cost = card.GetChangedCostItselef(condition.cost, SelectCardEffect.Root.Hand, new List<Permanent> { left, right }, checkAvailability: true);
                bestCost = Mathf.Min(bestCost, cost);
            }

            if (MatchesJogressCondition(condition, right, left))
            {
                int cost = card.GetChangedCostItselef(condition.cost, SelectCardEffect.Root.Hand, new List<Permanent> { right, left }, checkAvailability: true);
                bestCost = Mathf.Min(bestCost, cost);
            }
        }

        return bestCost == int.MaxValue ? -1 : bestCost;
    }

    static bool MatchesJogressCondition(JogressCondition condition, Permanent first, Permanent second)
    {
        return condition.elements[0] != null
            && condition.elements[1] != null
            && condition.elements[0].EvoRootCondition != null
            && condition.elements[1].EvoRootCondition != null
            && condition.elements[0].EvoRootCondition(first)
            && condition.elements[1].EvoRootCondition(second);
    }

    static int NormalizeLevel(int level)
    {
        return level > 1000 ? 0 : level;
    }

    static AIAttackerValueTier DetermineAttackerValueTier(Permanent attacker)
    {
        if (attacker == null)
        {
            return AIAttackerValueTier.Low;
        }

        int level = NormalizeLevel(attacker.Level);
        int stackCount = attacker.StackCards != null ? attacker.StackCards.Count : 0;
        int dp = attacker.DP;

        if (stackCount >= 4 || level >= 6 || dp >= 10000)
        {
            return AIAttackerValueTier.High;
        }

        if (stackCount >= 2 || level >= 5 || dp >= 7000 || attacker.HasBlocker)
        {
            return AIAttackerValueTier.Medium;
        }

        return AIAttackerValueTier.Low;
    }

    static int CountOtherSecurityAttackers(List<Permanent> yourField, Permanent attacker)
    {
        if (yourField == null)
        {
            return 0;
        }

        return yourField.Count(permanent =>
            permanent != null
            && permanent != attacker
            && permanent.TopCard != null
            && permanent.CanAttack(null)
            && permanent.CanAttackTargetDigimon(null, null));
    }

    static bool HasHigherValueFollowUpAttacker(List<Permanent> yourField, Permanent attacker, AIAttackerValueTier attackerValueTier)
    {
        if (yourField == null)
        {
            return false;
        }

        return yourField.Any(permanent =>
            permanent != null
            && permanent != attacker
            && permanent.TopCard != null
            && permanent.CanAttack(null)
            && permanent.CanAttackTargetDigimon(null, null)
            && DetermineAttackerValueTier(permanent) > attackerValueTier);
    }

    static AIAttackIntent DetermineSecurityAttackIntent(bool closeGameAttack, AIAttackerValueTier attackerValueTier, int otherPressureAttackers, bool hasHigherValueFollowUp)
    {
        if (closeGameAttack)
        {
            return AIAttackIntent.CloseGame;
        }

        if (otherPressureAttackers > 0 && (attackerValueTier == AIAttackerValueTier.Low || hasHigherValueFollowUp))
        {
            return AIAttackIntent.Probe;
        }

        return AIAttackIntent.Pressure;
    }

    static bool IsLikelySafeSecurityAttack(bool closeGameAttack, int visibleBlockerCount, AIAttackerValueTier attackerValueTier, Permanent attacker)
    {
        if (closeGameAttack && visibleBlockerCount == 0)
        {
            return true;
        }

        if (visibleBlockerCount > 0)
        {
            return false;
        }

        int level = attacker != null ? NormalizeLevel(attacker.Level) : 0;
        int stackCount = attacker != null && attacker.StackCards != null ? attacker.StackCards.Count : 0;
        return attackerValueTier == AIAttackerValueTier.Low || (level <= 4 && stackCount <= 2);
    }

    static bool IsLikelySafeDigimonAttack(Permanent attacker, Permanent defender)
    {
        return attacker != null
            && defender != null
            && attacker.DP >= defender.DP;
    }

    static bool CanProjectedMoveOutAttack(Permanent attacker, GameContext gameContext)
    {
        if (attacker == null
            || attacker.TopCard == null
            || !attacker.IsDigimon
            || attacker.IsSuspended
            || !attacker.CanSuspend
            || gameContext == null
            || attacker.TopCard.Owner != gameContext.TurnPlayer)
        {
            return false;
        }

        if (GManager.instance != null && GManager.instance.attackProcess != null && GManager.instance.attackProcess.IsAttacking)
        {
            return false;
        }

        int currentTurnCount = GManager.instance != null && GManager.instance.turnStateMachine != null
            ? GManager.instance.turnStateMachine.TurnCount
            : -1;

        return attacker.EnterFieldTurnCount != currentTurnCount || attacker.HasRush;
    }

    static bool CanProjectedMoveOutAttackTargetDigimon(Permanent attacker, Permanent defender, GameContext gameContext)
    {
        return CanProjectedMoveOutAttack(attacker, gameContext)
            && defender != null
            && defender.TopCard != null
            && defender.TopCard.Owner == attacker.TopCard.Owner.Enemy
            && defender.IsDigimon
            && defender.TopCard.Owner.GetBattleAreaPermanents().Contains(defender)
            && defender.IsSuspended;
    }

    static AIAttackIntent DetermineDigimonAttackIntent(Permanent defender, Permanent attacker, bool likelySafeAttack, bool unlocksAdditionalPressure, int opponentSecurityCount)
    {
        if (defender == null)
        {
            return AIAttackIntent.RemoveThreat;
        }

        if (defender.HasBlocker && unlocksAdditionalPressure)
        {
            return opponentSecurityCount <= 1 ? AIAttackIntent.CloseGame : AIAttackIntent.ClearBlocker;
        }

        int defenderLevel = NormalizeLevel(defender.Level);
        int defenderStackCount = defender.StackCards != null ? defender.StackCards.Count : 0;
        int attackerLevel = attacker != null ? NormalizeLevel(attacker.Level) : 0;

        if (likelySafeAttack && (defender.HasBlocker || defenderLevel >= attackerLevel || defender.DP >= 7000 || defenderStackCount >= 3))
        {
            return AIAttackIntent.FavorableTrade;
        }

        return AIAttackIntent.RemoveThreat;
    }

    static AIPlayIntent DeterminePlayIntent(CardSource card)
    {
        if (card == null)
        {
            return AIPlayIntent.Unknown;
        }

        SemanticSignals signals = CollectCardSemanticSignals(card);
        bool hasOnPlayEffect = card.HasOnPlayEffect;
        bool hasWhenDigivolvingEffect = card.HasWhenDigivolvingEffect;
        bool hasInheritedEffect = card.HasInheritedEffect;
        bool hasStartTurnEffect = HasLightweightEffect(card, EffectTiming.OnStartTurn);
        bool hasStartMainPhaseEffect = HasLightweightEffect(card, EffectTiming.OnStartMainPhase);
        bool hasPassiveEffect = HasLightweightEffect(card, EffectTiming.None);
        bool hasOptionUseEffect = HasLightweightEffect(card, EffectTiming.OnUseOption) || HasLightweightEffect(card, EffectTiming.OptionSkill);
        int playCost = card.BasePlayCostFromEntity;
        int level = card.HasLevel ? card.Level : 0;
        bool cheapDigimon = card.IsDigimon && ((level > 0 && level <= 4) || playCost <= 4);
        bool premiumDigimon = card.IsDigimon && (level >= 6 || playCost >= 7);
        bool lowCostTamer = card.IsTamer && playCost <= 4;
        bool lowCostPassiveDigimon = card.IsDigimon
            && level > 0
            && level <= 4
            && playCost <= 4
            && hasPassiveEffect
            && !hasOnPlayEffect
            && !hasWhenDigivolvingEffect
            && !hasInheritedEffect;
        bool likelyMemorySetter = card.IsTamer
            && lowCostTamer
            && !hasOnPlayEffect
            && !card.CanDeclareSkill
            && (hasStartTurnEffect || hasStartMainPhaseEffect);

        if (card.IsOption)
        {
            if (signals.HasRemoval || playCost >= 6 || card.OverflowMemory > 0)
            {
                return AIPlayIntent.RemovalOption;
            }

            if (signals.HasProtection)
            {
                return AIPlayIntent.ProtectionOption;
            }

            if (signals.HasDrawFilter)
            {
                return AIPlayIntent.DrawFilterOption;
            }

            if (signals.HasFloodgate)
            {
                return AIPlayIntent.Floodgate;
            }

            if (signals.HasTempo || hasOptionUseEffect)
            {
                return AIPlayIntent.TempoOption;
            }

            return AIPlayIntent.TempoOption;
        }

        if (card.IsTamer)
        {
            if (likelyMemorySetter)
            {
                return AIPlayIntent.MemorySetter;
            }

            if (hasOnPlayEffect || card.CanDeclareSkill || hasStartTurnEffect || hasStartMainPhaseEffect || hasPassiveEffect)
            {
                return AIPlayIntent.UtilityTamer;
            }

            return AIPlayIntent.UtilityTamer;
        }

        if (card.IsDigimon)
        {
            if (premiumDigimon || (level >= 5 && playCost >= 5 && hasOnPlayEffect))
            {
                return AIPlayIntent.Finisher;
            }

            if (signals.HasFloodgate || lowCostPassiveDigimon)
            {
                return AIPlayIntent.Floodgate;
            }

            if (cheapDigimon
                || hasOnPlayEffect
                || hasInheritedEffect
                || hasWhenDigivolvingEffect
                || signals.HasTempo
                || signals.HasProtection
                || signals.HasDrawFilter)
            {
                return AIPlayIntent.BodyDevelopment;
            }

            return AIPlayIntent.BodyDevelopment;
        }

        return AIPlayIntent.Unknown;
    }

    static AIEffectIntent DetermineEffectIntent(CardSource sourceCard, ICardEffect effect)
    {
        SemanticSignals signals = new SemanticSignals();
        ApplyEffectSemantics(ref signals, effect);

        if (!signals.HasRemoval
            && !signals.HasDrawFilter
            && !signals.HasTempo
            && !signals.HasProtection
            && !signals.HasFloodgate)
        {
            signals = CollectCardSemanticSignals(sourceCard);
        }

        if (signals.HasRemoval)
        {
            return AIEffectIntent.Removal;
        }

        if (signals.HasProtection)
        {
            return AIEffectIntent.Protection;
        }

        if (signals.HasDrawFilter)
        {
            return AIEffectIntent.DrawFilter;
        }

        if (signals.HasFloodgate)
        {
            return AIEffectIntent.Floodgate;
        }

        if (signals.HasTempo)
        {
            return AIEffectIntent.Tempo;
        }

        return sourceCard != null ? AIEffectIntent.Utility : AIEffectIntent.Unknown;
    }

    static SemanticSignals CollectCardSemanticSignals(CardSource card)
    {
        SemanticSignals signals = new SemanticSignals();
        if (card == null)
        {
            return signals;
        }

        ApplyEffectSemantics(ref signals, card.EffectList(EffectTiming.OnDeclaration));
        ApplyEffectSemantics(ref signals, card.EffectList(EffectTiming.OnUseOption));
        ApplyEffectSemantics(ref signals, card.EffectList(EffectTiming.OptionSkill));
        ApplyEffectSemantics(ref signals, card.EffectList(EffectTiming.OnEnterFieldAnyone));
        ApplyEffectSemantics(ref signals, card.EffectList(EffectTiming.OnStartTurn));
        ApplyEffectSemantics(ref signals, card.EffectList(EffectTiming.OnStartMainPhase));
        ApplyEffectSemantics(ref signals, card.EffectList(EffectTiming.None));
        return signals;
    }

    static void ApplyEffectSemantics(ref SemanticSignals signals, IEnumerable<ICardEffect> effects)
    {
        if (effects == null)
        {
            return;
        }

        foreach (ICardEffect effect in effects)
        {
            ApplyEffectSemantics(ref signals, effect);
        }
    }

    static void ApplyEffectSemantics(ref SemanticSignals signals, ICardEffect effect)
    {
        if (effect == null || effect.IsInheritedEffect || effect.IsSecurityEffect)
        {
            return;
        }

        string semanticText = BuildSemanticText(effect);

        if (effect is ICannotAddMemoryEffect
            || effect is ICanNotPlayCardEffect
            || effect is ICanNotDigivolveEffect
            || effect is ICanNotMoveEffect
            || effect is ICanNotPutFieldEffect
            || effect is ICannotReduceCostEffect
            || effect is ICannotIgnoreDigivolutionConditionEffect
            || effect is ICannotAddSecurityEffect
            || semanticText.Contains("cannotaddmemory")
            || semanticText.Contains("cannotplay")
            || semanticText.Contains("cannotdigivolve")
            || semanticText.Contains("cannotmove")
            || semanticText.Contains("cannotputfield")
            || semanticText.Contains("cannotreducecost")
            || semanticText.Contains("cannotaddsecurity"))
        {
            signals.HasFloodgate = true;
        }

        if (effect is ICanNotBeDestroyedEffect
            || effect is ICanNotBeDestroyedByBattleEffect
            || effect is ICanNotBeDestroyedBySkillEffect
            || effect is ICanNotBeRemovedEffect
            || effect is IImmuneFromDPMinusEffect
            || effect is IImmuneFromDeDigivolveEffect
            || effect is IImmuneFromStackTrashingEffect
            || effect is ICanNotAffectedEffect
            || semanticText.Contains("evade")
            || semanticText.Contains("barrier")
            || semanticText.Contains("immune")
            || semanticText.Contains("cannotbedestroyed")
            || semanticText.Contains("cannotberemoved")
            || semanticText.Contains("cannotaffected"))
        {
            signals.HasProtection = true;
        }

        if (effect is IChangeDPDeleteEffectMaxDPEffect
            || (effect is IChangeDPEffect changeDPEffect && changeDPEffect.IsMinusDP())
            || effect is ICannotReturnToHandEffect
            || effect is ICannotReturnToLibraryEffect
            || semanticText.Contains("delete")
            || semanticText.Contains("dedigivolve")
            || semanticText.Contains("returntohand")
            || semanticText.Contains("returntolibrary")
            || semanticText.Contains("bounce"))
        {
            signals.HasRemoval = true;
        }

        if (effect is IRushEffect
            || effect is IRebootEffect
            || effect is IBlockerEffect
            || effect is ICanNotUnsuspendEffect
            || effect is ICanNotSuspendEffect
            || effect is ICanNotAttackTargetDefendingPermanentEffect
            || effect is ICannotBlockEffect
            || effect is IChangeSAttackEffect
            || (effect is IChangeDPEffect dpEffect && !dpEffect.IsMinusDP())
            || semanticText.Contains("rush")
            || semanticText.Contains("blitz")
            || semanticText.Contains("blocker")
            || semanticText.Contains("reboot")
            || semanticText.Contains("suspend")
            || semanticText.Contains("unsuspend")
            || semanticText.Contains("cannotblock")
            || semanticText.Contains("cannotattack")
            || semanticText.Contains("changesattack"))
        {
            signals.HasTempo = true;
        }

        if (semanticText.Contains("draw")
            || semanticText.Contains("search")
            || semanticText.Contains("reveal")
            || semanticText.Contains("addhand")
            || semanticText.Contains("librarytop")
            || semanticText.Contains("librarybottom")
            || semanticText.Contains("decktop")
            || semanticText.Contains("deckbottom")
            || semanticText.Contains("look"))
        {
            signals.HasDrawFilter = true;
        }
    }

    static string BuildSemanticText(ICardEffect effect)
    {
        if (effect == null)
        {
            return "";
        }

        string raw = string.Concat(
            effect.GetType().Name ?? "",
            "|",
            effect.EffectName ?? "",
            "|",
            effect.HashString ?? "");

        char[] buffer = new char[raw.Length];
        int count = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (char.IsLetterOrDigit(c))
            {
                buffer[count++] = char.ToLowerInvariant(c);
            }
        }

        return count > 0 ? new string(buffer, 0, count) : "";
    }

    static bool HasLightweightEffect(CardSource card, EffectTiming timing)
    {
        return card != null
            && card.EffectList(timing).Any(cardEffect => cardEffect != null
                && !cardEffect.IsInheritedEffect
                && !cardEffect.IsSecurityEffect);
    }

    static string Signature(AIMainPhaseCandidate candidate)
    {
        return $"{candidate.ActionType}|{candidate.CardIndex}|{candidate.SourcePermanentIndex}|{candidate.TargetFrameID}|{candidate.AttackTargetPermanentIndex}|{candidate.SkillIndex}|{string.Join(",", candidate.JogressEvoRootsFrameIDs)}|{candidate.BurstTamerFrameID}|{string.Join(",", candidate.AppFusionFrameIDs)}";
    }
}
