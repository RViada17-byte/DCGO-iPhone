using System.Collections.Generic;

public interface IAIBrain
{
    string Name { get; }

    AIChosenAction DecideMulligan(AISnapshot snapshot);
    AIChosenAction DecideBreeding(AISnapshot snapshot);
    AIChosenAction DecideMainPhase(AISnapshot snapshot, IReadOnlyList<AIMainPhaseCandidate> candidates);
}
