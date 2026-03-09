public class AIMainPhaseCandidate
{
    public AIMainPhaseActionType ActionType { get; set; } = AIMainPhaseActionType.EndTurn;
    public string Summary { get; set; } = "End Turn";
    public string SourceName { get; set; } = "";
    public string TargetName { get; set; } = "";
    public CardKind SourceCardKind { get; set; } = CardKind.Option;
    public int CardIndex { get; set; } = -1;
    public int SourcePermanentIndex { get; set; } = -1;
    public int TargetFrameID { get; set; } = -1;
    public int AttackTargetPermanentIndex { get; set; } = -1;
    public int SkillIndex { get; set; } = -1;
    public int[] JogressEvoRootsFrameIDs { get; set; } = new int[0];
    public int BurstTamerFrameID { get; set; } = -1;
    public int[] AppFusionFrameIDs { get; set; } = new int[0];
    public int SourceLevel { get; set; } = 0;
    public int SourceDP { get; set; } = 0;
    public int SourceStackCount { get; set; } = 0;
    public int TargetLevel { get; set; } = 0;
    public int TargetDP { get; set; } = 0;
    public bool TargetIsBlocker { get; set; } = false;
    public bool SourceHasBlocker { get; set; } = false;
    public bool SourceIsSuspended { get; set; } = false;
    public bool SourceInBreeding { get; set; } = false;
    public bool TargetsOccupiedFrame { get; set; } = false;
    public bool DownstreamResolutionNotControlled { get; set; } = false;
    public int MemoryCost { get; set; } = 0;
    public int ProjectedMemory { get; set; } = 0;
    public int ImmediateSecurityPressure { get; set; } = 0;
    public AIPlayIntent PlayIntent { get; set; } = AIPlayIntent.Unknown;
    public AIAttackIntent AttackIntent { get; set; } = AIAttackIntent.None;
    public AIAttackerValueTier AttackerValueTier { get; set; } = AIAttackerValueTier.Low;
    public bool LikelySafeAttack { get; set; } = false;
    public bool UnlocksAdditionalPressure { get; set; } = false;

    public AIChosenAction.AIActionKind ToActionKind()
    {
        switch (ActionType)
        {
            case AIMainPhaseActionType.AttackSecurity:
                return AIChosenAction.AIActionKind.AttackSecurity;
            case AIMainPhaseActionType.AttackDigimon:
                return AIChosenAction.AIActionKind.AttackDigimon;
            case AIMainPhaseActionType.Play:
                return AIChosenAction.AIActionKind.Play;
            case AIMainPhaseActionType.Digivolve:
                return AIChosenAction.AIActionKind.Digivolve;
            case AIMainPhaseActionType.Jogress:
                return AIChosenAction.AIActionKind.Jogress;
            case AIMainPhaseActionType.Burst:
                return AIChosenAction.AIActionKind.Burst;
            case AIMainPhaseActionType.AppFusion:
                return AIChosenAction.AIActionKind.AppFusion;
            case AIMainPhaseActionType.UseFieldEffect:
                return AIChosenAction.AIActionKind.UseFieldEffect;
            case AIMainPhaseActionType.UseHandEffect:
                return AIChosenAction.AIActionKind.UseHandEffect;
            case AIMainPhaseActionType.UseTrashEffect:
                return AIChosenAction.AIActionKind.UseTrashEffect;
            default:
                return AIChosenAction.AIActionKind.EndTurn;
        }
    }
}
