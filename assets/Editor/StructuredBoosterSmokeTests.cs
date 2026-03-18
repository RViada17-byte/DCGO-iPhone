#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class StructuredBoosterSmokeTests
{
    private const string CardAssetRoot = "Assets/CardBaseEntity";
    private const int StructuredBoosterPackCount = 12;
    private const int StructuredBoosterCommonsPerPack = 6;
    private const int StructuredBoosterUncommonsPerPack = 3;
    private const int StructuredBoosterRaresPerPack = 2;
    private const int PacksPerStructuredBooster = 64;

    [MenuItem("Build/DCGO/Run Structured Booster Smoke Test")]
    public static void RunAll()
    {
        SmokeTestSession session = new SmokeTestSession();
        session.Run();
    }

    private sealed class SmokeTestSession
    {
        private readonly List<string> _results = new List<string>();
        private readonly List<FileBackup> _backups = new List<FileBackup>();

        private ContinuousController _controller;
        private GameObject _controllerObject;

        public void Run()
        {
            BackupSaveFiles();

            try
            {
                CreateController();
                LoadOwnedPrints();
                ShopCatalogDatabase.Instance.Reload();

                TestStructuredBoosterDistribution();

                Debug.Log("[StructuredBoosterSmokeTests] All smoke tests passed.\n" + string.Join("\n", _results));
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

        private void TestStructuredBoosterDistribution()
        {
            List<ShopProductDef> structuredBoosters = ShopCatalogDatabase.Instance.Products
                .Where(product => product != null && product.IsPack && IsStructuredBoosterSet(product.setId))
                .OrderBy(product => product.setId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AssertTrue(structuredBoosters.Count > 0, "Expected at least one structured booster product in the shop catalog.");

            foreach (ShopProductDef product in structuredBoosters)
            {
                bool setHasAltPrints = SetHasAltPrints(product.setId);
                bool observedAltChase = false;

                for (int packIndex = 0; packIndex < PacksPerStructuredBooster; packIndex++)
                {
                    List<PackPullResult> pulls = PackService.OpenPack(product.setId, product.packRules);
                    AssertStructuredPack(product, pulls);

                    PackPullResult chasePull = pulls[StructuredBoosterPackCount - 1];
                    if (!observedAltChase && chasePull?.Card != null && !chasePull.Card.IsCanonicalPrint)
                    {
                        AssertAltChasePresentation(product, pulls);
                        observedAltChase = true;
                    }
                }

                if (setHasAltPrints)
                {
                    AssertTrue(
                        observedAltChase,
                        $"Structured booster '{product.id}' never produced an alt-art chase over {PacksPerStructuredBooster} packs.");
                }

                _results.Add($"PASS: {product.id} held 6C/3U/2R/1 chase across {PacksPerStructuredBooster} packs.");
            }
        }

        private static void AssertStructuredPack(ShopProductDef product, IReadOnlyList<PackPullResult> pulls)
        {
            AssertTrue(pulls != null, $"Structured booster '{product?.id}' returned a null pull list.");
            AssertEqual(
                StructuredBoosterPackCount,
                pulls.Count,
                $"Structured booster '{product?.id}' should open exactly {StructuredBoosterPackCount} cards.");

            AssertSlotBand(product, pulls, 0, StructuredBoosterCommonsPerPack, Rarity.C);
            AssertSlotBand(product, pulls, StructuredBoosterCommonsPerPack, StructuredBoosterUncommonsPerPack, Rarity.U);
            AssertSlotBand(
                product,
                pulls,
                StructuredBoosterCommonsPerPack + StructuredBoosterUncommonsPerPack,
                StructuredBoosterRaresPerPack,
                Rarity.R);

            for (int index = 0; index < StructuredBoosterPackCount - 1; index++)
            {
                AssertFalse(
                    pulls[index].IsChase,
                    $"Structured booster '{product?.id}' marked slot {index + 1} as chase. Only the final slot should be chase.");
            }

            PackPullResult chasePull = pulls[StructuredBoosterPackCount - 1];
            AssertTrue(chasePull?.Card != null, $"Structured booster '{product?.id}' returned a null chase slot.");
            AssertTrue(chasePull.IsChase, $"Structured booster '{product?.id}' failed to tag the final slot as chase.");
            AssertTrue(
                IsStructuredBoosterChaseCard(chasePull.Card),
                $"Structured booster '{product?.id}' produced a non-chase card in the final slot: {DescribeCard(chasePull.Card)}.");
        }

        private static void AssertSlotBand(ShopProductDef product, IReadOnlyList<PackPullResult> pulls, int startIndex, int count, Rarity expectedRarity)
        {
            for (int offset = 0; offset < count; offset++)
            {
                int index = startIndex + offset;
                PackPullResult pull = pulls[index];
                AssertTrue(pull?.Card != null, $"Structured booster '{product?.id}' returned a null card in slot {index + 1}.");
                AssertFalse(
                    pull.IsChase,
                    $"Structured booster '{product?.id}' incorrectly tagged base slot {index + 1} as chase.");
                AssertTrue(
                    pull.Card.IsCanonicalPrint,
                    $"Structured booster '{product?.id}' slot {index + 1} should be canonical but got {DescribeCard(pull.Card)}.");
                AssertTrue(
                    pull.Card.rarity == expectedRarity,
                    $"Structured booster '{product?.id}' slot {index + 1} should be {expectedRarity} but got {DescribeCard(pull.Card)}.");
            }
        }

        private static void AssertAltChasePresentation(ShopProductDef product, IReadOnlyList<PackPullResult> pulls)
        {
            ShopPurchaseResult purchaseResult = new ShopPurchaseResult();
            for (int index = 0; index < pulls.Count; index++)
            {
                PackPullResult pull = pulls[index];
                purchaseResult.CardResults.Add(new ShopPurchaseCardResult
                {
                    CardId = pull.Card?.CardID ?? string.Empty,
                    PrintId = pull.Card?.EffectivePrintID ?? string.Empty,
                    CardName = pull.Card?.CardName_ENG ?? string.Empty,
                    Count = 1,
                    IsNew = pull.WasNew,
                    IsChase = pull.IsChase,
                });
            }

            PackOpeningResult openingResult = PackOpeningResult.FromShopPurchase(product, purchaseResult);
            AssertTrue(openingResult != null, $"Structured booster '{product?.id}' failed to build a pack opening result.");
            AssertEqual(
                StructuredBoosterPackCount,
                openingResult.Cards.Count,
                $"Structured booster '{product?.id}' pack opening result should preserve all {StructuredBoosterPackCount} cards.");

            PackOpeningResult.CardEntry chaseEntry = openingResult.Cards[StructuredBoosterPackCount - 1];
            AssertTrue(chaseEntry != null, $"Structured booster '{product?.id}' lost the chase card during opening-result conversion.");
            AssertTrue(chaseEntry.IsChase, $"Structured booster '{product?.id}' lost the chase flag during opening-result conversion.");
            AssertTrue(chaseEntry.IsAltPrint, $"Structured booster '{product?.id}' failed to recognize an alt-art chase print.");
            AssertTrue(chaseEntry.IsRare, $"Structured booster '{product?.id}' alt-art chase should receive special presentation.");
        }

        private void CreateController()
        {
            _controllerObject = new GameObject("StructuredBoosterSmokeController");
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

        private void LoadOwnedPrints(params string[] ownedPrintIds)
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

        private void ReloadSystems()
        {
            ResetRuntimeState();
            _controller.DeckDatas = new List<DeckData>();
            CardPrintCatalog.ResetCache();
            GameSaveManager.GetControllerData(_controller);
            ProgressionManager.Instance.LoadOrCreate();
        }

        private void BackupSaveFiles()
        {
            foreach (string path in EnumerateSavePaths())
            {
                string backupPath = path + ".structured-booster-smoke-backup";
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

        private void RestoreSaveFiles()
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

        private void WriteCanonicalSave(GameSaveData saveData)
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
            File.WriteAllText(GameSaveManager.CanonicalSavePath, json);
        }

        private IEnumerable<string> EnumerateSavePaths()
        {
            string canonical = GameSaveManager.CanonicalSavePath;
            yield return canonical;
            yield return canonical + ".bak";
            yield return canonical + ".tmp";
            yield return Path.Combine(Application.persistentDataPath, "profile.json");
        }

        private void ResetRuntimeState()
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

        private static void SetStaticField(Type type, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
            {
                throw new InvalidOperationException($"Missing static field {type.FullName}.{fieldName}");
            }

            field.SetValue(null, value);
        }

        private bool SetHasAltPrints(string setId)
        {
            string normalizedSetId = NormalizeCardCode(setId);
            return _controller.CardList.Any(card =>
                card != null &&
                !card.IsCanonicalPrint &&
                NormalizeCardCode(card.SetID) == normalizedSetId);
        }
    }

    private static bool IsStructuredBoosterSet(string setId)
    {
        string normalizedSetId = NormalizeCardCode(setId);
        return normalizedSetId.StartsWith("BT", StringComparison.OrdinalIgnoreCase) ||
               normalizedSetId.StartsWith("EX", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStructuredBoosterChaseCard(CEntity_Base card)
    {
        return card != null &&
               (!card.IsCanonicalPrint || card.rarity == Rarity.SR || card.rarity == Rarity.SEC);
    }

    private static string NormalizeCardCode(string cardCode)
    {
        if (string.IsNullOrWhiteSpace(cardCode))
        {
            return string.Empty;
        }

        return cardCode.Trim().Replace("_", "-").ToUpperInvariant();
    }

    private static string DescribeCard(CEntity_Base card)
    {
        if (card == null)
        {
            return "<null>";
        }

        return $"{card.CardID}/{card.EffectivePrintID}/{card.rarity}/{(card.IsCanonicalPrint ? "canonical" : "alt")}";
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool condition, string message)
    {
        AssertTrue(!condition, message);
    }

    private static void AssertEqual(int expected, int actual, string message)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
        }
    }

    private readonly struct FileBackup
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
#endif
