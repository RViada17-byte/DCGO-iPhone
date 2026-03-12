public class AITraceEntry
{
    public AIChosenAction.AIDecisionType DecisionType { get; set; } = AIChosenAction.AIDecisionType.MainPhase;
    public string StateKey { get; set; } = "";
    public string SnapshotSummary { get; set; } = "";
    public AITurnGoal Goal { get; set; } = AITurnGoal.ValueSetup;
    public string GoalReason { get; set; } = "";
    public AIChosenAction LegacyAction { get; set; } = null;
    public AIChosenAction GreedyAction { get; set; } = null;
    public bool Mismatch { get; set; } = false;
    public bool Unsupported { get; set; } = false;
    public bool Unresolved { get; set; } = false;
    public float EvaluationElapsedMs { get; set; } = 0f;
    public string FailureReason { get; set; } = "";
}
