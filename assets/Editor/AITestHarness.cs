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

    class ScenarioSuite
    {
        public string Name;
        public string Description;
        public List<ScenarioCase> Cases = new List<ScenarioCase>();
    }

    [MenuItem("Build/DCGO/Run AI Shadow Harness")]
    public static void Run()
    {
        GreedyShadowBrain brain = new GreedyShadowBrain();
        List<ScenarioSuite> suites = BuildSuites();
        int failures = 0;
        int totalCases = 0;

        foreach (ScenarioSuite suite in suites)
        {
            int suiteFailures = 0;
            Debug.Log($"AITestHarness SUITE: {suite.Name} ({suite.Cases.Count}) - {suite.Description}");

            foreach (ScenarioCase scenario in suite.Cases)
            {
                totalCases++;
                AIChosenAction action = EvaluateScenario(brain, scenario);
                bool passed = scenario.Assertion != null && scenario.Assertion(action);

                if (passed)
                {
                    Debug.Log($"AITestHarness PASS [{suite.Name}]: {scenario.Name} -> {action.ToCompactString()}");
                }
                else
                {
                    failures++;
                    suiteFailures++;
                    Debug.LogError($"AITestHarness FAIL [{suite.Name}]: {scenario.Name} -> {action.ToCompactString()}");
                }
            }

            if (suiteFailures == 0)
            {
                Debug.Log($"AITestHarness SUITE PASS: {suite.Name}");
            }
            else
            {
                Debug.LogError($"AITestHarness SUITE FAIL: {suite.Name} ({suiteFailures} failing case(s))");
            }
        }

        if (failures > 0)
        {
            throw new Exception($"AITestHarness failed with {failures} failing scenario(s).");
        }

        Debug.Log($"AITestHarness passed {totalCases} scenario(s) across {suites.Count} suite(s).");
    }

    static AIChosenAction EvaluateScenario(GreedyShadowBrain brain, ScenarioCase scenario)
    {
        switch (scenario.Snapshot.DecisionType)
        {
            case AIChosenAction.AIDecisionType.Mulligan:
                return brain.DecideMulligan(scenario.Snapshot);

            case AIChosenAction.AIDecisionType.Breeding:
                return brain.DecideBreeding(scenario.Snapshot);

            default:
                return brain.DecideMainPhase(scenario.Snapshot, scenario.Candidates);
        }
    }

    static List<ScenarioSuite> BuildSuites()
    {
        return new List<ScenarioSuite>
        {
            BuildCloseGameSuite(),
            BuildConvertAdvantageSuite(),
            BuildCrackbackSuite(),
            BuildStabilizeSuite(),
            BuildSetupDisciplineSuite(),
            BuildAttackSequencingSuite(),
            BuildOpeningDisciplineSuite(),
        };
    }

    static ScenarioSuite BuildCloseGameSuite()
    {
        return new ScenarioSuite
        {
            Name = "Close Game",
            Description = "Prioritize lethal and close-game pressure over incremental value.",
            Cases = new List<ScenarioCase>
            {
                new ScenarioCase
                {
                    Name = "Take lethal over incidental board clear",
                    Snapshot = MainSnapshot(selfSecurity: 3, oppSecurity: 1, selfThreats: 1, oppThreats: 1),
                    Candidates = new List<AIMainPhaseCandidate>
                    {
                        AttackSecurityCandidate("Attack security with Rookie", 0, stackCount: 1, attackIntent: AIAttackIntent.CloseGame, likelySafeAttack: true),
                        AttackDigimonCandidate("Attack blocker with Rookie", 0, 0, sourceDp: 6000, targetDp: 5000, stackCount: 1, targetIsBlocker: true),
                    },
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackSecurity,
                },
                new ScenarioCase
                {
                    Name = "Prioritize close-game pressure over incremental value",
                    Snapshot = MainSnapshot(selfSecurity: 3, oppSecurity: 2, selfThreats: 4, oppThreats: 1, opponentHasBlocker: true),
                    Candidates = new List<AIMainPhaseCandidate>
                    {
                        AttackSecurityCandidate("Attack security with Attacker 0", 0, stackCount: 1, likelySafeAttack: true),
                        AttackSecurityCandidate("Attack security with Attacker 1", 1, stackCount: 1, likelySafeAttack: true),
                        AttackSecurityCandidate("Attack security with Attacker 2", 2, stackCount: 2, likelySafeAttack: true, unlocksAdditionalPressure: true),
                        AttackSecurityCandidate("Attack security with Attacker 3", 3, stackCount: 2, likelySafeAttack: true, unlocksAdditionalPressure: true),
                        DigivolveCandidate("Digivolve instead of closing", 21, projectedMemory: 2, cost: 2, targetLevel: 4, sourceLevel: 5),
                    },
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackSecurity,
                },
            },
        };
    }

    static ScenarioSuite BuildConvertAdvantageSuite()
    {
        return new ScenarioSuite
        {
            Name = "Convert Advantage",
            Description = "Turn existing lead and board control into meaningful pressure instead of drifting into more setup.",
            Cases = new List<ScenarioCase>
            {
                new ScenarioCase
                {
                    Name = "Convert advantage by clearing blocker that unlocks pressure",
                    Snapshot = MainSnapshot(selfSecurity: 4, oppSecurity: 2, selfThreats: 1, oppThreats: 1, opponentHasBlocker: true),
                    Candidates = new List<AIMainPhaseCandidate>
                    {
                        AttackSecurityCandidate("Attack security with Champion", 0, stackCount: 2, likelySafeAttack: true),
                        AttackDigimonCandidate("Attack blocker with Champion", 0, 0, sourceDp: 8000, targetDp: 5000, stackCount: 2, targetIsBlocker: true, unlocksAdditionalPressure: true),
                    },
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackDigimon,
                },
                new ScenarioCase
                {
                    Name = "Convert established lead instead of adding more slow setup",
                    Snapshot = MainSnapshot(
                        selfSecurity: 4,
                        oppSecurity: 2,
                        selfThreats: 3,
                        oppThreats: 0,
                        breedingLevel: 6,
                        breedingStack: 4,
                        breedingCanMove: true,
                        selfHasMemorySetter: true),
                    Candidates = new List<AIMainPhaseCandidate>
                    {
                        AttackSecurityCandidate("Convert lead into pressure", 0, stackCount: 2, likelySafeAttack: true, unlocksAdditionalPressure: true),
                        PlayCandidate("Play extra utility tamer", 33, projectedMemory: 3, cost: 3, playIntent: AIPlayIntent.UtilityTamer),
                        DigivolveCandidate("Digivolve even deeper", 34, projectedMemory: 2, cost: 2, targetLevel: 6, sourceLevel: 5),
                    },
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackSecurity,
                },
            },
        };
    }

    static ScenarioSuite BuildCrackbackSuite()
    {
        return new ScenarioSuite
        {
            Name = "Respect Crackback",
            Description = "Avoid handing the opponent a dangerous tempo window when visible punish is available.",
            Cases = new List<ScenarioCase>
            {
                new ScenarioCase
                {
                    Name = "Promote mature L6 when no clear visible punish exists",
                    Snapshot = BreedingSnapshot(canHatch: false, canMove: true, selfSecurity: 3, oppSecurity: 4, breedingLevel: 6, breedingStack: 4, oppThreats: 1, selfThreats: 1),
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.MoveOut,
                },
                new ScenarioCase
                {
                    Name = "Respect dangerous crackback over slow setup",
                    Snapshot = MainSnapshot(
                        selfSecurity: 3,
                        oppSecurity: 4,
                        selfThreats: 1,
                        oppThreats: 1,
                        opponentBreedingLevel: 6,
                        opponentBreedingStack: 4,
                        opponentBreedingCanMove: true),
                    Candidates = new List<AIMainPhaseCandidate>
                    {
                        PlayCandidate("Play memory setter", 31, projectedMemory: 3, cost: 3, playIntent: AIPlayIntent.MemorySetter),
                        AttackDigimonCandidate("Attack opposing body", 0, 0, sourceDp: 7000, targetDp: 5000, stackCount: 2),
                    },
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackDigimon,
                },
                new ScenarioCase
                {
                    Name = "Respect visible punish by keeping premium breeding hidden",
                    Snapshot = BreedingSnapshot(canHatch: false, canMove: true, selfSecurity: 2, oppSecurity: 4, breedingLevel: 6, breedingStack: 4, oppThreats: 3, selfThreats: 0),
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.StayHidden,
                },
            },
        };
    }

    static ScenarioSuite BuildStabilizeSuite()
    {
        return new ScenarioSuite
        {
            Name = "Stabilize",
            Description = "When under pressure, prefer actions that contest the race and protect the next turn.",
            Cases = new List<ScenarioCase>
            {
                new ScenarioCase
                {
                    Name = "Stabilize under pressure by contesting board",
                    Snapshot = MainSnapshot(selfSecurity: 1, oppSecurity: 4, selfThreats: 1, oppThreats: 3),
                    Candidates = new List<AIMainPhaseCandidate>
                    {
                        AttackDigimonCandidate("Attack opposing threat", 0, 0, sourceDp: 7000, targetDp: 5000, stackCount: 2),
                        PlayCandidate("Play setup rookie", 12, projectedMemory: 3, cost: 3, playIntent: AIPlayIntent.BodyDevelopment),
                    },
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackDigimon,
                },
            },
        };
    }

    static ScenarioSuite BuildSetupDisciplineSuite()
    {
        return new ScenarioSuite
        {
            Name = "Setup Discipline",
            Description = "Develop when the window is safe, but sharply downweight redundant or low-impact setup once sufficiency is reached.",
            Cases = new List<ScenarioCase>
            {
                new ScenarioCase
                {
                    Name = "Prefer efficient line over flashy overextension",
                    Snapshot = MainSnapshot(selfSecurity: 4, oppSecurity: 4, selfThreats: 1, oppThreats: 1),
                    Candidates = new List<AIMainPhaseCandidate>
                    {
                        PlayCandidate("Play expensive body", 10, projectedMemory: 6, cost: 6, playIntent: AIPlayIntent.Finisher),
                        DigivolveCandidate("Digivolve cheaply", 11, projectedMemory: 1, cost: 1, targetLevel: 4, sourceLevel: 5),
                    },
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.Digivolve,
                },
                new ScenarioCase
                {
                    Name = "Prefer efficient development when the window is safe",
                    Snapshot = MainSnapshot(selfSecurity: 5, oppSecurity: 4, selfThreats: 1, oppThreats: 0, breedingLevel: 4, breedingStack: 2),
                    Candidates = new List<AIMainPhaseCandidate>
                    {
                        AttackSecurityCandidate("Attack security with Rookie", 0, stackCount: 1, likelySafeAttack: true),
                        DigivolveCandidate("Digivolve into Ultimate", 20, projectedMemory: 2, cost: 2, targetLevel: 4, sourceLevel: 5),
                    },
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.Digivolve,
                },
                new ScenarioCase
                {
                    Name = "Avoid redundant setup after sufficiency is reached",
                    Snapshot = MainSnapshot(
                        selfSecurity: 4,
                        oppSecurity: 3,
                        selfThreats: 2,
                        oppThreats: 1,
                        breedingLevel: 6,
                        breedingStack: 4,
                        breedingCanMove: true,
                        selfHasMemorySetter: true),
                    Candidates = new List<AIMainPhaseCandidate>
                    {
                        PlayCandidate("Play another memory setter", 32, projectedMemory: 3, cost: 3, playIntent: AIPlayIntent.MemorySetter),
                        AttackSecurityCandidate("Attack security with established board", 0, stackCount: 2, likelySafeAttack: true, unlocksAdditionalPressure: true),
                    },
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackSecurity,
                },
            },
        };
    }

    static ScenarioSuite BuildAttackSequencingSuite()
    {
        return new ScenarioSuite
        {
            Name = "Attack Sequencing",
            Description = "Use expendable attackers first and preserve premium stacks for higher-value pressure.",
            Cases = new List<ScenarioCase>
            {
                new ScenarioCase
                {
                    Name = "Use expendable attacker first when pressure outcome is equal",
                    Snapshot = MainSnapshot(selfSecurity: 2, oppSecurity: 2, selfThreats: 2, oppThreats: 1),
                    Candidates = new List<AIMainPhaseCandidate>
                    {
                        AttackSecurityCandidate("Attack security with premium stack", 0, stackCount: 5, likelySafeAttack: true, attackerValueTier: AIAttackerValueTier.High),
                        AttackSecurityCandidate("Attack security with expendable attacker", 1, stackCount: 1, likelySafeAttack: true, attackerValueTier: AIAttackerValueTier.Low),
                    },
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackSecurity && action.SourcePermanentIndex == 1,
                },
                new ScenarioCase
                {
                    Name = "Use expendable attacker for low-value clear when result is equivalent",
                    Snapshot = MainSnapshot(selfSecurity: 3, oppSecurity: 2, selfThreats: 2, oppThreats: 1),
                    Candidates = new List<AIMainPhaseCandidate>
                    {
                        AttackDigimonCandidate("Premium stack clears low-value body", 0, 0, sourceDp: 11000, targetDp: 4000, stackCount: 5, attackerValueTier: AIAttackerValueTier.High),
                        AttackDigimonCandidate("Expendable attacker clears same body", 1, 0, sourceDp: 6000, targetDp: 4000, stackCount: 1, attackerValueTier: AIAttackerValueTier.Low),
                    },
                    Assertion = action => action.ActionKind == AIChosenAction.AIActionKind.AttackDigimon && action.SourcePermanentIndex == 1,
                },
            },
        };
    }

    static ScenarioSuite BuildOpeningDisciplineSuite()
    {
        return new ScenarioSuite
        {
            Name = "Opening Discipline",
            Description = "Keep structurally functional openers and throw back dead, top-heavy hands.",
            Cases = new List<ScenarioCase>
            {
                new ScenarioCase
                {
                    Name = "Keep structurally functional opener",
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
                    Name = "Mulligan structurally dead opener",
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
            },
        };
    }

    static AISnapshot MainSnapshot(
        int selfSecurity,
        int oppSecurity,
        int selfThreats,
        int oppThreats,
        bool opponentHasBlocker = false,
        int breedingLevel = 0,
        int breedingStack = 0,
        bool breedingCanMove = false,
        int opponentBreedingLevel = 0,
        int opponentBreedingStack = 0,
        bool opponentBreedingCanMove = false,
        bool selfHasMemorySetter = false)
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

        if (selfHasMemorySetter)
        {
            snapshot.Self.BattlePermanents.Add(new AISnapshotPermanentView
            {
                Name = "Memory Setter",
                IsTamer = true,
                LikelyMemorySetter = true,
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
                CanMove = breedingCanMove,
            });
        }

        if (opponentBreedingLevel > 0)
        {
            snapshot.Opponent.BreedingPermanents.Add(new AISnapshotPermanentView
            {
                Name = "Opponent Breeding",
                IsDigimon = true,
                Level = opponentBreedingLevel,
                StackCount = opponentBreedingStack,
                InBreeding = true,
                CanMove = opponentBreedingCanMove,
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
            CanMove = canMove,
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

    static AIMainPhaseCandidate AttackSecurityCandidate(
        string summary,
        int sourcePermanentIndex,
        int stackCount,
        bool likelySafeAttack = true,
        AIAttackIntent attackIntent = AIAttackIntent.Pressure,
        bool unlocksAdditionalPressure = false,
        AIAttackerValueTier attackerValueTier = AIAttackerValueTier.Low,
        int immediateSecurityPressure = 1)
    {
        return new AIMainPhaseCandidate
        {
            ActionType = AIMainPhaseActionType.AttackSecurity,
            Summary = summary,
            SourcePermanentIndex = sourcePermanentIndex,
            SourceStackCount = stackCount,
            ImmediateSecurityPressure = immediateSecurityPressure,
            AttackIntent = attackIntent,
            AttackerValueTier = NormalizeAttackerValueTier(attackerValueTier, stackCount, 0, 0),
            LikelySafeAttack = likelySafeAttack,
            UnlocksAdditionalPressure = unlocksAdditionalPressure,
        };
    }

    static AIMainPhaseCandidate AttackDigimonCandidate(
        string summary,
        int sourcePermanentIndex,
        int attackTargetPermanentIndex,
        int sourceDp,
        int targetDp,
        int stackCount = 1,
        bool targetIsBlocker = false,
        bool likelySafeAttack = true,
        bool unlocksAdditionalPressure = false,
        AIAttackIntent attackIntent = AIAttackIntent.None,
        AIAttackerValueTier attackerValueTier = AIAttackerValueTier.Low)
    {
        return new AIMainPhaseCandidate
        {
            ActionType = AIMainPhaseActionType.AttackDigimon,
            Summary = summary,
            SourcePermanentIndex = sourcePermanentIndex,
            AttackTargetPermanentIndex = attackTargetPermanentIndex,
            SourceDP = sourceDp,
            TargetDP = targetDp,
            SourceStackCount = stackCount,
            TargetIsBlocker = targetIsBlocker,
            AttackIntent = attackIntent == AIAttackIntent.None
                ? targetIsBlocker ? AIAttackIntent.ClearBlocker : AIAttackIntent.RemoveThreat
                : attackIntent,
            AttackerValueTier = NormalizeAttackerValueTier(attackerValueTier, stackCount, sourceDp, 0),
            LikelySafeAttack = likelySafeAttack && sourceDp >= targetDp,
            UnlocksAdditionalPressure = unlocksAdditionalPressure,
        };
    }

    static AIMainPhaseCandidate PlayCandidate(string summary, int cardIndex, int projectedMemory, int cost, AIPlayIntent playIntent = AIPlayIntent.Unknown)
    {
        return new AIMainPhaseCandidate
        {
            ActionType = AIMainPhaseActionType.Play,
            Summary = summary,
            CardIndex = cardIndex,
            ProjectedMemory = projectedMemory,
            MemoryCost = cost,
            SourceCardKind = DetermineCardKind(playIntent),
            SourceLevel = DetermineSourceLevel(playIntent),
            PlayIntent = playIntent,
            EffectIntent = DetermineEffectIntent(playIntent),
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

    static CardKind DetermineCardKind(AIPlayIntent playIntent)
    {
        switch (playIntent)
        {
            case AIPlayIntent.MemorySetter:
            case AIPlayIntent.UtilityTamer:
                return CardKind.Tamer;

            case AIPlayIntent.DrawFilterOption:
            case AIPlayIntent.TempoOption:
            case AIPlayIntent.RemovalOption:
            case AIPlayIntent.ProtectionOption:
                return CardKind.Option;

            default:
                return CardKind.Digimon;
        }
    }

    static int DetermineSourceLevel(AIPlayIntent playIntent)
    {
        switch (playIntent)
        {
            case AIPlayIntent.Finisher:
                return 6;

            case AIPlayIntent.BodyDevelopment:
            case AIPlayIntent.Floodgate:
                return 3;

            default:
                return 0;
        }
    }

    static AIEffectIntent DetermineEffectIntent(AIPlayIntent playIntent)
    {
        switch (playIntent)
        {
            case AIPlayIntent.DrawFilterOption:
                return AIEffectIntent.DrawFilter;

            case AIPlayIntent.TempoOption:
                return AIEffectIntent.Tempo;

            case AIPlayIntent.RemovalOption:
                return AIEffectIntent.Removal;

            case AIPlayIntent.ProtectionOption:
                return AIEffectIntent.Protection;

            case AIPlayIntent.Floodgate:
                return AIEffectIntent.Floodgate;

            case AIPlayIntent.MemorySetter:
            case AIPlayIntent.UtilityTamer:
                return AIEffectIntent.Utility;

            default:
                return AIEffectIntent.Unknown;
        }
    }

    static AIAttackerValueTier NormalizeAttackerValueTier(AIAttackerValueTier configuredTier, int stackCount, int sourceDp, int sourceLevel)
    {
        if (configuredTier != AIAttackerValueTier.Low)
        {
            return configuredTier;
        }

        if (stackCount >= 4 || sourceDp >= 10000 || sourceLevel >= 6)
        {
            return AIAttackerValueTier.High;
        }

        if (stackCount >= 2 || sourceDp >= 7000 || sourceLevel >= 5)
        {
            return AIAttackerValueTier.Medium;
        }

        return AIAttackerValueTier.Low;
    }
}
#endif
