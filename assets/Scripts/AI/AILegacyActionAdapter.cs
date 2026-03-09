using System.Collections.Generic;

public static class AILegacyActionAdapter
{
    public static AIChosenAction Normalize(AIChosenAction action)
    {
        if (action == null)
        {
            return null;
        }

        return AIChosenAction.Create(
            action.DecisionType,
            action.ActionKind,
            action.Summary,
            action.Goal,
            action.Score,
            action.TopAlternatives != null ? new List<AIActionScore>(action.TopAlternatives) : new List<AIActionScore>(),
            action.CardIndex,
            action.SourcePermanentIndex,
            action.TargetFrameID,
            action.AttackTargetPermanentIndex,
            action.SkillIndex,
            action.JogressEvoRootsFrameIDs,
            action.BurstTamerFrameID,
            action.AppFusionFrameIDs,
            action.DownstreamResolutionNotControlled);
    }

    public static AIChosenAction FromMainPhaseLiveState(
        GameContext gameContext,
        CardSource playCard,
        int targetFrameID,
        int[] jogressEvoRootsFrameIDs,
        int burstTamerFrameID,
        int[] appFusionFrameIDs,
        ICardEffect useCardEffect,
        Permanent attackingPermanent,
        Permanent defendingPermanent)
    {
        if (gameContext == null)
        {
            return CreateEndTurn();
        }

        if (playCard != null)
        {
            AIChosenAction.AIActionKind actionKind = AIChosenAction.AIActionKind.Play;
            string summary = $"Play {playCard.BaseENGCardNameFromEntity}";

            if (jogressEvoRootsFrameIDs != null && jogressEvoRootsFrameIDs.Length == 2)
            {
                actionKind = AIChosenAction.AIActionKind.Jogress;
                summary = $"Jogress {playCard.BaseENGCardNameFromEntity}";
            }
            else if (burstTamerFrameID >= 0)
            {
                actionKind = AIChosenAction.AIActionKind.Burst;
                summary = $"Burst digivolve {playCard.BaseENGCardNameFromEntity}";
            }
            else if (appFusionFrameIDs != null && appFusionFrameIDs.Length == 2)
            {
                actionKind = AIChosenAction.AIActionKind.AppFusion;
                summary = $"App Fusion {playCard.BaseENGCardNameFromEntity}";
            }
            else if (playCard.IsPermanent && targetFrameID >= 0 && targetFrameID < gameContext.TurnPlayer.fieldCardFrames.Count)
            {
                Permanent targetPermanent = gameContext.TurnPlayer.fieldCardFrames[targetFrameID].GetFramePermanent();
                if (targetPermanent != null)
                {
                    actionKind = AIChosenAction.AIActionKind.Digivolve;
                    summary = $"Digivolve {playCard.BaseENGCardNameFromEntity} onto {targetPermanent.TopCard.BaseENGCardNameFromEntity}";
                }
            }

            return AIChosenAction.Create(
                AIChosenAction.AIDecisionType.MainPhase,
                actionKind,
                summary,
                AITurnGoal.ValueSetup,
                CreateLegacyScore(summary),
                cardIndex: playCard.CardIndex,
                targetFrameId: targetFrameID,
                jogressEvoRootsFrameIDs: jogressEvoRootsFrameIDs,
                burstTamerFrameID: burstTamerFrameID,
                appFusionFrameIDs: appFusionFrameIDs);
        }

        if (useCardEffect != null)
        {
            CardSource sourceCard = useCardEffect.EffectSourceCard;
            Permanent sourcePermanent = sourceCard != null ? sourceCard.PermanentOfThisCard() : null;
            AIChosenAction.AIActionKind actionKind = AIChosenAction.AIActionKind.UseFieldEffect;
            int sourcePermanentIndex = -1;
            int skillIndex = -1;
            string summary = $"Use effect {useCardEffect.EffectName}";

            if (sourceCard != null)
            {
                summary = $"Use effect {useCardEffect.EffectName} from {sourceCard.BaseENGCardNameFromEntity}";
                skillIndex = sourceCard.EffectList(EffectTiming.OnDeclaration).IndexOf(useCardEffect);
            }

            if (sourcePermanent != null)
            {
                sourcePermanentIndex = gameContext.TurnPlayer.GetFieldPermanents().IndexOf(sourcePermanent);
                actionKind = AIChosenAction.AIActionKind.UseFieldEffect;
            }
            else if (sourceCard != null && sourceCard.Owner.HandCards.Contains(sourceCard))
            {
                actionKind = AIChosenAction.AIActionKind.UseHandEffect;
            }
            else if (sourceCard != null && sourceCard.Owner.TrashCards.Contains(sourceCard))
            {
                actionKind = AIChosenAction.AIActionKind.UseTrashEffect;
            }

            return AIChosenAction.Create(
                AIChosenAction.AIDecisionType.MainPhase,
                actionKind,
                summary,
                AITurnGoal.ValueSetup,
                CreateLegacyScore(summary),
                cardIndex: sourceCard != null ? sourceCard.CardIndex : -1,
                sourcePermanentIndex: sourcePermanentIndex,
                skillIndex: skillIndex,
                downstreamResolutionNotControlled: true);
        }

        if (attackingPermanent != null)
        {
            bool attackDigimon = defendingPermanent != null;
            string summary = attackDigimon
                ? $"Attack {defendingPermanent.TopCard.BaseENGCardNameFromEntity} with {attackingPermanent.TopCard.BaseENGCardNameFromEntity}"
                : $"Attack security with {attackingPermanent.TopCard.BaseENGCardNameFromEntity}";

            return AIChosenAction.Create(
                AIChosenAction.AIDecisionType.MainPhase,
                attackDigimon ? AIChosenAction.AIActionKind.AttackDigimon : AIChosenAction.AIActionKind.AttackSecurity,
                summary,
                AITurnGoal.ValueSetup,
                CreateLegacyScore(summary),
                sourcePermanentIndex: gameContext.TurnPlayer.GetFieldPermanents().IndexOf(attackingPermanent),
                attackTargetPermanentIndex: attackDigimon ? gameContext.NonTurnPlayer.GetFieldPermanents().IndexOf(defendingPermanent) : -1);
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
