using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class AIShadowMatchStats
{
    class DecisionStats
    {
        public int Evaluations;
        public int Mismatches;
        public int Unsupported;
        public int Unresolved;
        public float TotalElapsedMs;
        public float MaxElapsedMs;
    }

    readonly Dictionary<AIChosenAction.AIDecisionType, DecisionStats> _byDecisionType = new Dictionary<AIChosenAction.AIDecisionType, DecisionStats>();

    public int TotalEvaluations { get; private set; } = 0;
    public int TotalMismatches { get; private set; } = 0;
    public int TotalUnsupported { get; private set; } = 0;
    public int TotalUnresolved { get; private set; } = 0;
    public float TotalElapsedMs { get; private set; } = 0f;
    public float MaxElapsedMs { get; private set; } = 0f;

    public void Record(AITraceEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        DecisionStats stats = GetOrCreate(entry.DecisionType);
        stats.Evaluations++;
        stats.TotalElapsedMs += entry.EvaluationElapsedMs;
        stats.MaxElapsedMs = Mathf.Max(stats.MaxElapsedMs, entry.EvaluationElapsedMs);

        TotalEvaluations++;
        TotalElapsedMs += entry.EvaluationElapsedMs;
        MaxElapsedMs = Mathf.Max(MaxElapsedMs, entry.EvaluationElapsedMs);

        if (entry.Mismatch)
        {
            stats.Mismatches++;
            TotalMismatches++;
        }

        if (entry.Unsupported)
        {
            stats.Unsupported++;
            TotalUnsupported++;
        }

        if (entry.Unresolved)
        {
            stats.Unresolved++;
            TotalUnresolved++;
        }
    }

    public string SummaryText()
    {
        StringBuilder builder = new StringBuilder(256);
        builder.Append("total=").Append(TotalEvaluations);
        builder.Append(" mismatch=").Append(TotalMismatches);
        builder.Append(" unsupported=").Append(TotalUnsupported);
        builder.Append(" unresolved=").Append(TotalUnresolved);
        builder.Append(" avgMs=").Append(FormatAverage(TotalElapsedMs, TotalEvaluations));
        builder.Append(" maxMs=").Append(MaxElapsedMs.ToString("0.0"));
        builder.Append(" byType=\"");

        bool first = true;
        foreach (AIChosenAction.AIDecisionType decisionType in Enum.GetValues(typeof(AIChosenAction.AIDecisionType)))
        {
            if (!first)
            {
                builder.Append(" | ");
            }

            first = false;
            DecisionStats stats = GetOrCreate(decisionType);
            builder.Append(decisionType)
                .Append(":n=").Append(stats.Evaluations)
                .Append(",m=").Append(stats.Mismatches)
                .Append(",u=").Append(stats.Unsupported)
                .Append(",r=").Append(stats.Unresolved)
                .Append(",avgMs=").Append(FormatAverage(stats.TotalElapsedMs, stats.Evaluations));
        }

        builder.Append("\"");
        return builder.ToString();
    }

    static string FormatAverage(float totalElapsedMs, int count)
    {
        if (count <= 0)
        {
            return "0.0";
        }

        return (totalElapsedMs / count).ToString("0.0");
    }

    DecisionStats GetOrCreate(AIChosenAction.AIDecisionType decisionType)
    {
        if (!_byDecisionType.TryGetValue(decisionType, out DecisionStats stats))
        {
            stats = new DecisionStats();
            _byDecisionType.Add(decisionType, stats);
        }

        return stats;
    }
}
