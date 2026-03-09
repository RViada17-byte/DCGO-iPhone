#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AITestHarness
{
    class ScenarioCase
    {
        public string Name;
        public AISnapshot Snapshot;
        public List<AIMainPhaseCandidate> Candidates = new List<AIMainPhaseCandidate>();
        public Func<AIChosenAction, bool> Assertion;
    }

    [MenuItem("Build/DCGO/Run AI Shadow Harness")]
    public static void Run()
    {
        GreedyShadowBrain brain = new GreedyShadowBrain();
        List<ScenarioCase> scenarios = BuildScenarios();
        int failures = 0;

        foreach (ScenarioCase scenario in scenarios)
        {
            AIChosenAction action;

            switch (scenario.Snapshot.DecisionType)
            {
                case AIChosenAction.AIDecisionType.Mulligan:
                    action = brain.DecideMulligan(scenario.Snapshot);
                    break;

                case AIChosenAction.AIDecisionType.Breeding:
                    action = brain.DecideBreeding(scenario.Snapshot);
                    break;

                default:
                    action = brain.DecideMainPhase(scenario.Snapshot, scenario.Candidates);
                    break;
            }

            bool passed = scenario.Assertion != null && scenario.Assertion(action);
            if (passed)
            {
                Debug.Log($"AITestHarness PASS: {scenario.Name} -> {action.ToCompactString()}");
            }
            else
            {
                failures++;
                Debug.LogError($"AITestHarness FAIL: {scenario.Name} -> {action.ToCompactString()}");
            }
        }

        if (failures > 0)
        {
            throw new Exception($"AITestHarness failed with {failures} failing scenario(s).");
        }

        Debug.Log($"AITestHarness passed {scenarios.Count} scenario(s).");
    }

    static List<ScenarioCase> BuildScenarios()
    {
        return new List<ScenarioCase>
        {
            new ScenarioCase
            {
                Name = "Take lethal over board clear",
                Snapshot = MainSnapshot(selfSecurity: 3, oppSecurity: 1, selfThreats: 1, oppThreats: 1),
                Candidates = new List<AIMainPhaseCandidate>
                {
                    AttackSecurityCandidate("Attack security with Rookie", 0, stackCount: 1),
                    AttackDigimonCandidate("Attack blocker with Rookie", 0, 0, sourceDp: 6000, targetDp: 5000, targetIsBlocker: true),
                },
                Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackSecurity,
            },
            new ScenarioCase
            {
                Name = "Clear blocker before pushing damage",
                Snapshot = MainSnapshot(selfSecurity: 4, oppSecurity: 2, selfThreats: 1, oppThreats: 1, opponentHasBlocker: true),
                Candidates = new List<AIMainPhaseCandidate>
                {
                    AttackSecurityCandidate("Attack security with Champion", 0, stackCount: 2),
                    AttackDigimonCandidate("Attack blocker with Champion", 0, 0, sourceDp: 8000, targetDp: 5000, targetIsBlocker: true),
                },
                Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackDigimon,
            },
            new ScenarioCase
            {
                Name = "Do not expose premium breeding stack",
                Snapshot = BreedingSnapshot(canHatch: false, canMove: true, selfSecurity: 2, oppSecurity: 4, breedingLevel: 6, breedingStack: 4, oppThreats: 3, selfThreats: 0),
                Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.StayHidden,
            },
            new ScenarioCase
            {
                Name = "Keep functional opener",
                Snapshot = MulliganSnapshot(new List<AISnapshotCardView>
                {
                    HandCard("BT1-010", "Agumon", 3),
                    HandCard("BT1-011", "Gabumon", 3),
                    HandCard("BT1-020", "Greymon", 4),
                    HandCard("BT1-050", "Tai Kamiya", 0, isTamer: true),
                    HandCard("BT1-085", "Hammer Spark", 0, isOption: true),
                }),
                Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.KeepHand,
            },
            new ScenarioCase
            {
                Name = "Mulligan dead opener",
                Snapshot = MulliganSnapshot(new List<AISnapshotCardView>
                {
                    HandCard("BT1-030", "MetalGreymon", 5),
                    HandCard("BT1-032", "WarGreymon", 6),
                    HandCard("BT1-050", "Tai Kamiya", 0, isTamer: true),
                    HandCard("BT1-085", "Hammer Spark", 0, isOption: true),
                    HandCard("BT1-089", "Gaia Force", 0, isOption: true),
                }),
                Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.Mulligan,
            },
            new ScenarioCase
            {
                Name = "Prefer memory choke over flashy overextension",
                Snapshot = MainSnapshot(selfSecurity: 4, oppSecurity: 4, selfThreats: 1, oppThreats: 1),
                Candidates = new List<AIMainPhaseCandidate>
                {
                    PlayCandidate("Play expensive body", 10, projectedMemory: 6, cost: 6),
                    DigivolveCandidate("Digivolve cheaply", 11, projectedMemory: 1, cost: 1, targetLevel: 4, sourceLevel: 5),
                },
                Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.Digivolve,
            },
            new ScenarioCase
            {
                Name = "Stabilize when losing race",
                Snapshot = MainSnapshot(selfSecurity: 1, oppSecurity: 4, selfThreats: 1, oppThreats: 3),
                Candidates = new List<AIMainPhaseCandidate>
                {
                    AttackDigimonCandidate("Attack opposing threat", 0, 0, sourceDp: 7000, targetDp: 5000),
                    PlayCandidate("Play setup rookie", 12, projectedMemory: 3, cost: 3),
                },
                Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackDigimon,
            },
            new ScenarioCase
            {
                Name = "Choose safer attacker",
                Snapshot = MainSnapshot(selfSecurity: 2, oppSecurity: 2, selfThreats: 2, oppThreats: 1),
                Candidates = new List<AIMainPhaseCandidate>
                {
                    AttackSecurityCandidate("Attack security with premium stack", 0, stackCount: 5),
                    AttackSecurityCandidate("Attack security with expendable attacker", 1, stackCount: 1),
                },
                Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackSecurity && action.SourcePermanentIndex == 1,
            },
            new ScenarioCase
            {
                Name = "Prefer development when safe",
                Snapshot = MainSnapshot(selfSecurity: 5, oppSecurity: 4, selfThreats: 1, oppThreats: 0, breedingLevel: 4, breedingStack: 2),
                Candidates = new List<AIMainPhaseCandidate>
                {
                    AttackSecurityCandidate("Attack security with Rookie", 0, stackCount: 1),
                    DigivolveCandidate("Digivolve into Ultimate", 20, projectedMemory: 2, cost: 2, targetLevel: 4, sourceLevel: 5),
                },
                Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.Digivolve,
            },
        };
    }

    static AISnapshot MainSnapshot(int selfSecurity, int oppSecurity, int selfThreats, int oppThreats, bool opponentHasBlocker = false, int breedingLevel = 0, int breedingStack = 0)
    {
        AISnapshot snapshot = BaseSnapshot(AIChosenAction.AIDecisionType.MainPhase, selfSecurity, oppSecurity);

        for (int i = 0; i < selfThreats; i++)
        {
            snapshot.Self.BattlePermanents.Add(new AISnapshotPermanentView
            {
                Name = $"Self-{i}",
                IsDigimon = true,
                IsSuspended = false,
                StackCount = i == 0 ? 1 : 2,
                Level = 4 + i,
            });
        }

        for (int i = 0; i < oppThreats; i++)
        {
            snapshot.Opponent.BattlePermanents.Add(new AISnapshotPermanentView
            {
                Name = $"Opponent-{i}",
                IsDigimon = true,
                IsSuspended = false,
                HasBlocker = opponentHasBlocker && i == 0,
                StackCount = 2,
                Level = 4 + i,
            });
        }

        if (breedingLevel > 0)
        {
            snapshot.Self.BreedingPermanents.Add(new AISnapshotPermanentView
            {
                Name = "Breeding",
                IsDigimon = true,
                Level = breedingLevel,
                StackCount = breedingStack,
                InBreeding = true,
            });
        }

        return FinalizeSnapshot(snapshot);
    }

    static AISnapshot BreedingSnapshot(bool canHatch, bool canMove, int selfSecurity, int oppSecurity, int breedingLevel, int breedingStack, int oppThreats, int selfThreats)
    {
        AISnapshot snapshot = BaseSnapshot(AIChosenAction.AIDecisionType.Breeding, selfSecurity, oppSecurity);
        snapshot.Self.CanHatch = canHatch;
        snapshot.Self.CanMove = canMove;

        for (int i = 0; i < oppThreats; i++)
        {
            snapshot.Opponent.BattlePermanents.Add(new AISnapshotPermanentView
            {
                Name = $"Opponent-{i}",
                IsDigimon = true,
                IsSuspended = false,
                Level = 5,
                StackCount = 2,
            });
        }

        for (int i = 0; i < selfThreats; i++)
        {
            snapshot.Self.BattlePermanents.Add(new AISnapshotPermanentView
            {
                Name = $"Self-{i}",
                IsDigimon = true,
                IsSuspended = false,
                Level = 4,
                StackCount = 1,
            });
        }

        snapshot.Self.BreedingPermanents.Add(new AISnapshotPermanentView
        {
            Name = "Premium Breeding",
            IsDigimon = true,
            InBreeding = true,
            Level = breedingLevel,
            StackCount = breedingStack,
        });

        return FinalizeSnapshot(snapshot);
    }

    static AISnapshot MulliganSnapshot(List<AISnapshotCardView> hand)
    {
        AISnapshot snapshot = BaseSnapshot(AIChosenAction.AIDecisionType.Mulligan, 5, 5);
        snapshot.Self.KnownHandCards.AddRange(hand);
        snapshot.Self.HandCount = hand.Count;
        return FinalizeSnapshot(snapshot);
    }

    static AISnapshot BaseSnapshot(AIChosenAction.AIDecisionType decisionType, int selfSecurity, int oppSecurity)
    {
        AISnapshot snapshot = new AISnapshot
        {
            DecisionType = decisionType,
            TurnCount = 3,
            PhaseName = decisionType == AIChosenAction.AIDecisionType.MainPhase ? "Main" : decisionType.ToString(),
            Memory = 0,
            Self = new AISnapshotPlayerView
            {
                Name = "Bot",
                IsYou = false,
                PlayerId = 1,
                SecurityCount = selfSecurity,
                HandCount = 5,
            },
            Opponent = new AISnapshotPlayerView
            {
                Name = "Player",
                IsYou = true,
                PlayerId = 0,
                SecurityCount = oppSecurity,
            },
        };

        snapshot.StateKey = $"{decisionType}-{selfSecurity}-{oppSecurity}";
        return snapshot;
    }

    static AISnapshot FinalizeSnapshot(AISnapshot snapshot)
    {
        AISnapshotBuilder.RefreshDerivedSummaries(snapshot);
        snapshot.StateKey = $"{snapshot.DecisionType}-{snapshot.Self.SecurityCount}-{snapshot.Opponent.SecurityCount}-{snapshot.Self.BoardValueScore}-{snapshot.Opponent.BoardValueScore}";
        return snapshot;
    }

    static AISnapshotCardView HandCard(string cardId, string name, int level, bool isTamer = false, bool isOption = false)
    {
        return new AISnapshotCardView
        {
            CardID = cardId,
            Name = name,
            Level = level,
            IsDigimon = !isTamer && !isOption && level > 0,
            IsTamer = isTamer,
            IsOption = isOption,
        };
    }

    static AIMainPhaseCandidate AttackSecurityCandidate(string summary, int sourcePermanentIndex, int stackCount)
    {
        return new AIMainPhaseCandidate
        {
            ActionType = AIMainPhaseActionType.AttackSecurity,
            Summary = summary,
            SourcePermanentIndex = sourcePermanentIndex,
            SourceStackCount = stackCount,
            ImmediateSecurityPressure = 1,
        };
    }

    static AIMainPhaseCandidate AttackDigimonCandidate(string summary, int sourcePermanentIndex, int attackTargetPermanentIndex, int sourceDp, int targetDp, bool targetIsBlocker = false)
    {
        return new AIMainPhaseCandidate
        {
            ActionType = AIMainPhaseActionType.AttackDigimon,
            Summary = summary,
            SourcePermanentIndex = sourcePermanentIndex,
            AttackTargetPermanentIndex = attackTargetPermanentIndex,
            SourceDP = sourceDp,
            TargetDP = targetDp,
            TargetIsBlocker = targetIsBlocker,
        };
    }

    static AIMainPhaseCandidate PlayCandidate(string summary, int cardIndex, int projectedMemory, int cost)
    {
        return new AIMainPhaseCandidate
        {
            ActionType = AIMainPhaseActionType.Play,
            Summary = summary,
            CardIndex = cardIndex,
            ProjectedMemory = projectedMemory,
            MemoryCost = cost,
            SourceCardKind = CardKind.Digimon,
            SourceLevel = 3,
        };
    }

    static AIMainPhaseCandidate DigivolveCandidate(string summary, int cardIndex, int projectedMemory, int cost, int targetLevel, int sourceLevel)
    {
        return new AIMainPhaseCandidate
        {
            ActionType = AIMainPhaseActionType.Digivolve,
            Summary = summary,
            CardIndex = cardIndex,
            ProjectedMemory = projectedMemory,
            MemoryCost = cost,
            TargetLevel = targetLevel,
            SourceLevel = sourceLevel,
            TargetsOccupiedFrame = true,
            SourceCardKind = CardKind.Digimon,
        };
    }
}
#endif
