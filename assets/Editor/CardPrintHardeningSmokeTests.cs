using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CardPrintHardeningSmokeTests
{
    const string CardAssetRoot = "Assets/CardBaseEntity";

    [MenuItem("DCGO/Card Prints/Run Hardening Smoke Tests")]
    public static void RunAll()
    {
        SmokeTestSession session = new SmokeTestSession();
        session.Run();
    }

    sealed class SmokeTestSession
    {
        readonly List<string> _results = new List<string>();
        readonly List<FileBackup> _backups = new List<FileBackup>();

        ContinuousController _controller;
        GameObject _controllerObject;

        public void Run()
        {
            BackupSaveFiles();

            try
            {
                CreateController();

                TestLegacySaveMigration();
                TestFallbackWithoutOverwrite();
                TestAltPrintGameplayOwnership();
                TestBasePrintDoesNotUnlockSiblingPrints();
                TestDeckEditingPreservesStoredPrintRefs();
                TestBattleDeckSelectionGate();
                TestCanonicalOnlyPackAndRewardSemantics();
                TestSwappingPrintsKeepsGameplayIdentity();

                Debug.Log("[CardPrintHardeningSmokeTests] All smoke tests passed.\n" + string.Join("\n", _results));
            }
            finally
            {
                ResetRuntimeState();
                if (_controllerObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(_controllerObject);
                }
                ContinuousController.instance = null;

                RestoreSaveFiles();
            }
        }

        void TestLegacySaveMigration()
        {
            WriteCanonicalSaveJson(
@"{
  ""SaveVersion"": 1,
  ""LastSavedUtc"": """",
  ""LegacyProfileMigrated"": true,
  ""LegacyControllerMigrated"": true,
  ""Profile"": {
    ""SaveVersion"": 1,
    ""Currency"": 777,
    ""UnlockedCardIds"": [""BT1-003""],
    ""OwnedPrintIds"": [],
    ""PurchasedProductIds"": [],
    ""CompletedStoryNodeIds"": [],
    ""EarnedStoryKeyIds"": [],
    ""CompletedDuelBoardIds"": [],
    ""ClaimedPromoCardIds"": [],
    ""FirstRunGrantsApplied"": false
  },
  ""Controller"": {
    ""PlayerName"": ""Player"",
    ""WinCount"": 0,
    ""Decks"": [
      {
        ""DeckId"": ""legacy-alt-deck"",
        ""DeckName"": ""Legacy Alt Deck"",
        ""KeyCardId"": 136,
        ""SortValue"": 0,
        ""MainDeckCardIds"": [136, 137],
        ""DigitamaDeckCardIds"": []
      }
    ],
    ""StarterDeckKeys"": [],
    ""AutoEffectOrder"": false,
    ""AutoDeckBottomOrder"": false,
    ""AutoDeckTopOrder"": false,
    ""AutoMinDigivolutionCost"": false,
    ""AutoMaxCardCount"": false,
    ""AutoHatch"": false,
    ""ReverseOpponentsCards"": false,
    ""TurnSuspendedCards"": true,
    ""CheckBeforeEndingSelection"": true,
    ""SuspendedCardsDirectionIsLeft"": true,
    ""ShowBackgroundParticle"": false,
    ""BgmVolume"": 0.5,
    ""SeVolume"": 0.5,
    ""ServerRegion"": ""us"",
    ""Language"": ""ENG""
  }
}");

            GameSaveData parsedLegacySave = LoadCanonicalSaveDirect();
            AssertTrue(
                parsedLegacySave?.Profile?.UnlockedCardIds?.Contains("BT1-003") == true,
                "Legacy smoke-test fixture should deserialize BT1-003 into UnlockedCardIds.");

            ReloadSystems();

            CEntity_Base canonicalPrint = CardPrintCatalog.GetCanonicalPrint("BT1-003");
            AssertTrue(canonicalPrint != null, "Smoke test expected BT1-003 canonical print to resolve after controller setup.");

            PlayerProfileData savedProfile = GameSaveManager.LoadOrCreateProfileData();
            PlayerProfileData profile = ProgressionManager.Instance.CurrentProfileData;
            string expectedCanonicalPrintId = canonicalPrint.EffectivePrintID;
            AssertTrue(
                savedProfile.OwnedPrintIds.Contains(expectedCanonicalPrintId),
                $"Legacy profile migration should materialize canonical owned print IDs. Saved owned prints: {string.Join(",", savedProfile.OwnedPrintIds)} | Runtime owned prints: {string.Join(",", profile.OwnedPrintIds)}");

            GameSaveControllerData controllerData = GameSaveManager.GetControllerData(_controller);
            GameSaveDeckData migratedDeck = controllerData.Decks.FirstOrDefault(deck => deck.DeckId == "legacy-alt-deck");
            AssertTrue(
                migratedDeck != null,
                $"Legacy controller migration should retain deck 'legacy-alt-deck'. Actual decks: {string.Join(",", controllerData.Decks.Select(deck => deck?.DeckId ?? "<null>"))}");
            AssertEqual("BT1-003_P1", migratedDeck.KeyCardRef.PrintId, "Legacy key card index should migrate through alias lookup.");
            AssertEqual(
                "BT1-003_P1,BT1-003_P2",
                string.Join(",", migratedDeck.MainDeckCardRefs.Select(cardRef => cardRef.PrintId)),
                "Legacy deck card indexes should migrate to stored CardPrintRefs.");

            _controller.LoadDeckLists();
            DeckData loadedDeck = _controller.DeckDatas.FirstOrDefault(deck => deck.DeckID == "legacy-alt-deck");
            AssertTrue(
                loadedDeck != null,
                $"Legacy migrated deck should load into controller deck list. Actual decks: {string.Join(",", _controller.DeckDatas.Select(deck => deck?.DeckID ?? "<null>"))}");
            AssertEqual("BT1-003_P1", loadedDeck.GetStoredMainDeckRefs()[0].PrintId, "Loaded deck should preserve migrated stored print refs.");

            _results.Add("PASS 1-2: legacy save/profile migration populated OwnedPrintIds and CardPrintRefs.");
        }

        void TestFallbackWithoutOverwrite()
        {
            LoadOwnedPrints("BT1-003");

            DeckData unownedAltDeck = new DeckData(string.Empty, "fallback-unowned");
            unownedAltDeck.SetStoredDeckRefs(
                new[] { new CardPrintRef("BT1-003", "BT1-003_P3") },
                Array.Empty<CardPrintRef>(),
                new CardPrintRef("BT1-003", "BT1-003_P3"));

            AssertEqual("BT1-003", unownedAltDeck.DeckCards().Single().EffectivePrintID, "Unowned saved alt print should render as canonical fallback.");
            AssertEqual("BT1-003_P3", unownedAltDeck.GetStoredMainDeckRefs().Single().PrintId, "Unowned saved alt print should remain stored.");

            DeckData missingPrintDeck = new DeckData(string.Empty, "fallback-missing");
            missingPrintDeck.SetStoredDeckRefs(
                new[] { new CardPrintRef("BT1-003", "BT1-003_P99") },
                Array.Empty<CardPrintRef>(),
                new CardPrintRef("BT1-003", "BT1-003_P99"));

            AssertEqual("BT1-003", missingPrintDeck.DeckCards().Single().EffectivePrintID, "Missing saved print should render as canonical fallback.");
            AssertEqual("BT1-003_P99", missingPrintDeck.GetStoredMainDeckRefs().Single().PrintId, "Missing saved print should remain stored.");

            _results.Add("PASS 3: missing and unowned saved PrintIDs fall back without overwriting stored preference.");
        }

        void TestAltPrintGameplayOwnership()
        {
            LoadOwnedPrints("BT1-003_P1");

            AssertTrue(ProgressionManager.Instance.OwnsPrint("BT1-003_P1"), "Owned alt print should remain directly owned.");
            AssertTrue(ProgressionManager.Instance.IsCardUnlocked("BT1-003"), "Owning any print should grant gameplay ownership for the CardID.");

            _results.Add("PASS 4: owning an alt print grants gameplay ownership for the CardID.");
        }

        void TestBasePrintDoesNotUnlockSiblingPrints()
        {
            LoadOwnedPrints("BT1-003");

            AssertTrue(ProgressionManager.Instance.IsCardUnlocked("BT1-003"), "Owning the canonical print should still grant gameplay ownership.");
            AssertFalse(ProgressionManager.Instance.OwnsPrint("BT1-003_P1"), "Owning the canonical print must not unlock sibling prints.");

            _results.Add("PASS 5: owning the base print does not unlock sibling prints.");
        }

        void TestDeckEditingPreservesStoredPrintRefs()
        {
            CEntity_Base altMainDeckPrint = FindAltMainDeckPrint();
            LoadOwnedPrints(altMainDeckPrint.EffectivePrintID);

            DeckData deck = new DeckData(string.Empty, "edit-ref");
            deck.SetDeckCardsFromResolvedCards(Array.Empty<CEntity_Base>(), Array.Empty<CEntity_Base>(), null);
            deck.AddCard(altMainDeckPrint);

            AssertEqual(altMainDeckPrint.EffectivePrintID, deck.GetStoredMainDeckRefs().Single().PrintId, "Adding a print should persist its PrintID.");

            DeckData modified = deck.ModifiedDeckData();
            AssertEqual(altMainDeckPrint.EffectivePrintID, modified.GetStoredMainDeckRefs().Single().PrintId, "ModifiedDeckData should not round-trip through CardIndex.");

            modified.RemoveCard(altMainDeckPrint);
            AssertTrue(modified.GetStoredMainDeckRefs().Count == 0, "Removing a print should clear the stored print ref.");

            _results.Add("PASS 6: deck add/remove/edit flows preserve print refs without CardIndex round-trips.");
        }

        void TestBattleDeckSelectionGate()
        {
            DeckData validDeck = GenerateValidDeck();
            validDeck.DeckID = "smoke-battle";

            LoadOwnedPrints(validDeck.AllDeckCards().Select(card => card.EffectivePrintID).Distinct().ToArray());
            _controller.DeckDatas = new List<DeckData> { validDeck };

            MethodInfo getSelectableDecks = typeof(SinglePlayerWorldDuelLauncher).GetMethod("GetSelectableDecks", BindingFlags.NonPublic | BindingFlags.Static);
            List<DeckData> selectableDecks = (List<DeckData>)getSelectableDecks.Invoke(null, new object[] { _controller });

            AssertTrue(selectableDecks.Any(deck => deck.DeckID == validDeck.DeckID), "Valid unlocked decks should still pass the world duel selection gate.");

            _results.Add("PASS 7: battle deck selection still accepts valid unlocked decks.");
        }

        void TestCanonicalOnlyPackAndRewardSemantics()
        {
            LoadOwnedPrints();

            List<PackPullResult> pulls = PackService.OpenPack("BT1");
            AssertTrue(pulls.Count > 0, "PackService should still open packs.");
            AssertTrue(
                pulls.All(pull => pull.Card == CardPrintCatalog.GetCanonicalPrint(pull.Card.CardID)),
                "PackService should still return canonical-only prints in V1.");

            string rewardCardId = pulls[0].Card.CardID;
            ProgressionManager.Instance.UnlockCanonicalPrint(rewardCardId, saveImmediately: false);

            CEntity_Base canonicalPrint = CardPrintCatalog.GetCanonicalPrint(rewardCardId);
            AssertTrue(ProgressionManager.Instance.OwnsPrint(canonicalPrint.EffectivePrintID), "Canonical reward unlock should own the canonical print.");

            CEntity_Base siblingAlt = CardPrintCatalog.GetPrints(rewardCardId).FirstOrDefault(card => card.EffectivePrintID != canonicalPrint.EffectivePrintID);
            if (siblingAlt != null)
            {
                AssertFalse(ProgressionManager.Instance.OwnsPrint(siblingAlt.EffectivePrintID), "Canonical reward unlock must not unlock sibling alt prints.");
            }

            _results.Add("PASS 8: pack/reward flow remains canonical-only.");
        }

        void TestSwappingPrintsKeepsGameplayIdentity()
        {
            (CEntity_Base canonicalPrint, CEntity_Base altPrint) = FindComparablePrintPair();
            LoadOwnedPrints(canonicalPrint.EffectivePrintID, altPrint.EffectivePrintID);

            DeckData altDeck = new DeckData(string.Empty, "swap-print");
            altDeck.SetStoredDeckRefs(
                new[] { CardPrintRef.FromCard(altPrint) },
                Array.Empty<CardPrintRef>(),
                CardPrintRef.FromCard(altPrint));

            AssertEqual(canonicalPrint.CardID, altDeck.DeckCards().Single().CardID, "Swapping prints must not change CardID gameplay identity.");
            AssertEqual(canonicalPrint.CardEffectClassName, altDeck.DeckCards().Single().CardEffectClassName, "Swapping prints must not change effect wiring identity.");
            AssertEqual(altPrint.EffectivePrintID, altDeck.GetStoredMainDeckRefs().Single().PrintId, "Stored print preference should remain the chosen alt print.");

            _results.Add("PASS 9: swapping prints does not change gameplay identity.");
        }

        void CreateController()
        {
            _controllerObject = new GameObject("CardPrintSmokeController");
            _controller = _controllerObject.AddComponent<ContinuousController>();
            ContinuousController.instance = _controller;
            _controller.CardList = AssetDatabase.FindAssets("t:CEntity_Base", new[] { CardAssetRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<CEntity_Base>(path))
                .Where(card => card != null)
                .OrderBy(card => card.CardIndex)
                .ToArray();
            _controller.SortedCardList = _controller.CardList
                .Where(card => card != null)
                .OrderBy(card => card.CardIndex)
                .ToArray();
            _controller.DeckDatas = new List<DeckData>();
            CardPrintCatalog.ResetCache();
        }

        DeckData GenerateValidDeck()
        {
            MethodInfo generator = typeof(ContinuousController).GetMethod("GenerateDemoDeckFromSets", BindingFlags.NonPublic | BindingFlags.Instance);
            DeckData deck = (DeckData)generator.Invoke(_controller, new object[] { "Smoke Demo", new HashSet<string>() });
            AssertTrue(deck != null && deck.IsValidDeckData(), "A valid smoke-test deck should be generated.");
            return deck;
        }

        CEntity_Base FindAltMainDeckPrint()
        {
            CEntity_Base altPrint = _controller.CardList
                .Where(card => card != null && card.cardKind != CardKind.DigiEgg)
                .GroupBy(card => card.CardID)
                .Select(group => new
                {
                    Canonical = CardPrintCatalog.GetCanonicalPrint(group.Key),
                    Alt = group.FirstOrDefault(card => CardPrintCatalog.GetCanonicalPrint(group.Key) != null && card.EffectivePrintID != CardPrintCatalog.GetCanonicalPrint(group.Key).EffectivePrintID),
                })
                .Where(entry => entry.Canonical != null && entry.Alt != null)
                .Select(entry => entry.Alt)
                .FirstOrDefault();

            AssertTrue(altPrint != null, "Expected at least one alternate main-deck print for smoke tests.");
            return altPrint;
        }

        (CEntity_Base canonicalPrint, CEntity_Base altPrint) FindComparablePrintPair()
        {
            foreach (IGrouping<string, CEntity_Base> group in _controller.CardList
                .Where(card => card != null)
                .GroupBy(card => card.CardID))
            {
                CEntity_Base canonicalPrint = CardPrintCatalog.GetCanonicalPrint(group.Key);
                CEntity_Base altPrint = group.FirstOrDefault(card => canonicalPrint != null && card.EffectivePrintID != canonicalPrint.EffectivePrintID);
                if (canonicalPrint != null && altPrint != null)
                {
                    return (canonicalPrint, altPrint);
                }
            }

            throw new InvalidOperationException("Expected at least one print group with an alternate print.");
        }

        void LoadOwnedPrints(params string[] ownedPrintIds)
        {
            WriteCanonicalSave(new GameSaveData
            {
                SaveVersion = GameSaveManager.CurrentSaveVersion,
                LegacyProfileMigrated = true,
                LegacyControllerMigrated = true,
                Profile = new PlayerProfileData
                {
                    SaveVersion = 4,
                    Currency = 1000,
                    OwnedPrintIds = ownedPrintIds?.ToList() ?? new List<string>(),
                    UnlockedCardIds = new List<string>(),
                },
                Controller = new GameSaveControllerData(),
            });

            ReloadSystems();
        }

        void ReloadSystems()
        {
            ResetRuntimeState();
            _controller.DeckDatas = new List<DeckData>();
            CardPrintCatalog.ResetCache();
            GameSaveManager.GetControllerData(_controller);
            ProgressionManager.Instance.LoadOrCreate();
        }

        void BackupSaveFiles()
        {
            foreach (string path in EnumerateSavePaths())
            {
                string backupPath = path + ".card-print-hardening-backup";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                if (File.Exists(path))
                {
                    File.Move(path, backupPath);
                    _backups.Add(new FileBackup(path, backupPath, true));
                }
                else
                {
                    _backups.Add(new FileBackup(path, backupPath, false));
                }
            }
        }

        void RestoreSaveFiles()
        {
            foreach (FileBackup backup in _backups)
            {
                if (File.Exists(backup.Path))
                {
                    File.Delete(backup.Path);
                }

                if (!backup.Existed)
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(backup.Path) ?? Directory.GetCurrentDirectory());
                if (File.Exists(backup.BackupPath))
                {
                    File.Move(backup.BackupPath, backup.Path);
                }
            }
        }

        void WriteCanonicalSave(GameSaveData saveData)
        {
            foreach (string path in EnumerateSavePaths())
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(GameSaveManager.CanonicalSavePath) ?? Directory.GetCurrentDirectory());
            string json = JsonUtility.ToJson(saveData, true);
            WriteCanonicalSaveJson(json);
        }

        void WriteCanonicalSaveJson(string json)
        {
            foreach (string path in EnumerateSavePaths())
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(GameSaveManager.CanonicalSavePath) ?? Directory.GetCurrentDirectory());
            File.WriteAllText(GameSaveManager.CanonicalSavePath, json);
        }

        IEnumerable<string> EnumerateSavePaths()
        {
            string canonical = GameSaveManager.CanonicalSavePath;
            yield return canonical;
            yield return canonical + ".bak";
            yield return canonical + ".tmp";
            yield return Path.Combine(Application.persistentDataPath, "profile.json");
        }

        void ResetRuntimeState()
        {
            CardPrintCatalog.ResetCache();

            Type gameSaveManagerType = typeof(GameSaveManager);
            SetStaticField(gameSaveManagerType, "_cachedSave", null);
            SetStaticField(gameSaveManagerType, "_isLoaded", false);
            SetStaticField(gameSaveManagerType, "_lastSuccessfulSaveUtc", string.Empty);

            ProgressionManager existingProgressionManager = ProgressionManager.LoadedInstance;
            if (existingProgressionManager != null)
            {
                UnityEngine.Object.DestroyImmediate(existingProgressionManager.gameObject);
            }

            SetStaticField(typeof(ProgressionManager), "_instance", null);
        }

        GameSaveData LoadCanonicalSaveDirect()
        {
            MethodInfo loader = typeof(GameSaveManager).GetMethod("TryLoadCanonicalSave", BindingFlags.NonPublic | BindingFlags.Static);
            object[] args = { GameSaveManager.CanonicalSavePath, null, null };
            bool loaded = (bool)loader.Invoke(null, args);
            AssertTrue(loaded, "Expected direct canonical save parse to succeed for the smoke-test fixture.");
            return (GameSaveData)args[1];
        }

        static void SetStaticField(Type type, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
            {
                throw new InvalidOperationException($"Missing static field {type.FullName}.{fieldName}");
            }

            field.SetValue(null, value);
        }

        static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        static void AssertFalse(bool condition, string message)
        {
            AssertTrue(!condition, message);
        }

        static void AssertEqual(string expected, string actual, string message)
        {
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
            }
        }
    }

    readonly struct FileBackup
    {
        public FileBackup(string path, string backupPath, bool existed)
        {
            Path = path;
            BackupPath = backupPath;
            Existed = existed;
        }

        public string Path { get; }
        public string BackupPath { get; }
        public bool Existed { get; }
    }
}
