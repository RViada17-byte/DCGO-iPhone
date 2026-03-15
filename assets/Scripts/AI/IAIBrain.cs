using System.Collections.Generic;

public interface IAIBrain
{
    string Name { get; }

    AIChosenAction DecideMulligan(AISnapshot snapshot);
    AIChosenAction DecideBreeding(AISnapshot snapshot, GameContext gameContext = null, Player player = null);
    AIChosenAction DecideMainPhase(AISnapshot snapshot, IReadOnlyList<AIMainPhaseCandidate> candidates);
}
