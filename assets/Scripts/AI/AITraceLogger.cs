using System.Linq;
using UnityEngine;

public static class AITraceLogger
{
    public const string Prefix = "[AI Shadow]";
    public const string SummaryPrefix = "[AI Shadow Summary]";

    public static void Log(AITraceEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        string legacy = FormatAction(entry.LegacyAction);
        string greedy = FormatAction(entry.GreedyAction);
        string goalReason = string.IsNullOrEmpty(entry.GoalReason) ? "" : $" goalReason=\"{entry.GoalReason}\"";
        string alternatives = entry.GreedyAction == null || entry.GreedyAction.TopAlternatives.Count == 0
            ? "none"
            : string.Join(" | ", entry.GreedyAction.TopAlternatives
                .Take(3)
                .Select(score => $"{score.ActionSummary} score={score.TotalScore:0} [{score.BreakdownSummary}]"));

        if (!string.IsNullOrEmpty(entry.FailureReason))
        {
            Debug.LogWarning($"{Prefix} decision={entry.DecisionType} goal={entry.Goal}{goalReason} mismatch={entry.Mismatch} unsupported={entry.Unsupported} unresolved={entry.Unresolved} evalMs={entry.EvaluationElapsedMs:0.0} snapshot=\"{entry.SnapshotSummary}\" legacy=\"{legacy}\" fallback=\"{entry.FailureReason}\"");
            return;
        }

        Debug.Log($"{Prefix} decision={entry.DecisionType} goal={entry.Goal}{goalReason} mismatch={entry.Mismatch} unsupported={entry.Unsupported} unresolved={entry.Unresolved} evalMs={entry.EvaluationElapsedMs:0.0} snapshot=\"{entry.SnapshotSummary}\" legacy=\"{legacy}\" greedy=\"{greedy}\" top=\"{alternatives}\"");
    }

    public static void LogSummary(AIShadowMatchStats stats)
    {
        if (stats == null)
        {
            return;
        }

        Debug.Log($"{SummaryPrefix} {stats.SummaryText()}");
    }

    static string FormatAction(AIChosenAction action)
    {
        if (action == null)
        {
            return "none";
        }

        string fingerprint = action.Fingerprint != null
            ? action.Fingerprint.ToNormalizedString()
            : "none";
        string scoreSummary = action.Score != null
            ? $" score={action.Score.TotalScore:0} [{action.Score.BreakdownSummary}]"
            : "";

        string downstream = action.DownstreamResolutionNotControlled ? " downstream_resolution_not_controlled" : "";

        return $"{fingerprint} summary=\"{action.Summary}\"{scoreSummary}{downstream}";
    }
}
