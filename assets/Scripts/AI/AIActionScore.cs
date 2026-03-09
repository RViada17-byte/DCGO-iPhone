using System.Collections.Generic;

public class AIActionScore
{
    public string ActionSummary { get; set; } = "";
    public float TotalScore { get; set; } = 0f;
    public List<string> Breakdown { get; private set; } = new List<string>();
    public bool DownstreamResolutionNotControlled { get; set; } = false;

    public string BreakdownSummary
    {
        get
        {
            if (Breakdown.Count == 0)
            {
                return "none";
            }

            return string.Join(", ", Breakdown);
        }
    }
}
