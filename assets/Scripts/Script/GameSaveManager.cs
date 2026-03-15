using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
using UnityEngine.iOS;
#endif

[Serializable]
public class GameSaveData
{
    public int SaveVersion = GameSaveManager.CurrentSaveVersion;
    public string LastSavedUtc = string.Empty;
    public bool LegacyProfileMigrated = false;
    public bool LegacyControllerMigrated = false;
    public PlayerProfileData Profile = new PlayerProfileData();
    public GameSaveControllerData Controller = new GameSaveControllerData();
}

[Serializable]
public class GameSaveControllerData
{
    public string PlayerName = "Player";
    public int WinCount = 0;
    public List<GameSaveDeckData> Decks = new List<GameSaveDeckData>();
    public List<string> StarterDeckKeys = new List<string>();
    public bool AutoEffectOrder = false;
    public bool AutoDeckBottomOrder = false;
    public bool AutoDeckTopOrder = false;
    public bool AutoMinDigivolutionCost = false;
    public bool AutoMaxCardCount = false;
    public bool AutoHatch = false;
    public bool ReverseOpponentsCards = false;
    public bool TurnSuspendedCards = true;
    public bool CheckBeforeEndingSelection = true;
    public bool SuspendedCardsDirectionIsLeft = true;
    public bool ShowBackgroundParticle = false;
    public float BgmVolume = 0.5f;
    public float SeVolume = 0.5f;
    public string ServerRegion = "us";
    public string Language = global::Language.ENG.ToString();
}

[Serializable]
public class GameSaveDeckData
{
    public string DeckId = string.Empty;
    public string DeckName = "NewDeck";
    public int KeyCardId = -1;
    public CardPrintRef KeyCardRef = new CardPrintRef();
    public int SortValue = 0;
    public List<int> MainDeckCardIds = new List<int>();
    public List<int> DigitamaDeckCardIds = new List<int>();
    public List<CardPrintRef> MainDeckCardRefs = new List<CardPrintRef>();
    public List<CardPrintRef> DigitamaDeckCardRefs = new List<CardPrintRef>();
}

public static class GameSaveManager
{
    public const int CurrentSaveVersion = 2;

    const string CanonicalSaveFileName = "dcgo-save.json";
    const string LegacyProfileFileName = "profile.json";
    const string BackupExtension = ".bak";
    const string TempExtension = ".tmp";
    const string LogPrefix = "[GameSave]";

    static GameSaveData _cachedSave;
    static bool _isLoaded;
    static string _lastSuccessfulSaveUtc = string.Empty;

    public static string CanonicalSavePath => Path.GetFullPath(Path.Combine(Application.persistentDataPath, CanonicalSaveFileName));

    public static bool CanonicalSaveExists => File.Exists(CanonicalSavePath);

    public static string LastSuccessfulSaveUtc
    {
        get
        {
            if (!string.IsNullOrEmpty(_lastSuccessfulSaveUtc))
            {
                return _lastSuccessfulSaveUtc;
            }

            return !string.IsNullOrEmpty(_cachedSave?.LastSavedUtc)
                ? _cachedSave.LastSavedUtc
                : "never";
        }
    }

    public static PlayerProfileData LoadOrCreateProfileData()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.LoadOrCreateProfileData");

        try
        {
            EnsureLoaded(ContinuousController.instance);
            return _cachedSave.Profile;
        }
        finally
        {
            perfScope.SetItemCount("ownedPrints", _cachedSave?.Profile?.OwnedPrintIds?.Count ?? 0);
            perfScope.SetItemCount("savedDecks", _cachedSave?.Controller?.Decks?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    public static GameSaveControllerData GetControllerData(ContinuousController controller)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.GetControllerData");

        try
        {
            EnsureLoaded(controller);
            return _cachedSave.Controller;
        }
        finally
        {
            perfScope.SetItemCount("savedDecks", _cachedSave?.Controller?.Decks?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    public static void SaveAll(PlayerProfileData profile, ContinuousController controller, string reason = null)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.SaveAll");

        try
        {
            EnsureLoaded(controller);

            if (profile != null)
            {
                _cachedSave.Profile = profile;
            }

            if (controller != null)
            {
                _cachedSave.Controller = CaptureControllerData(controller);
                _cachedSave.LegacyControllerMigrated = true;
            }

            EnsureSaveInitialized(_cachedSave);
            StartupPerfTrace.RecordSaveAll(reason, _cachedSave?.Profile?.OwnedPrintIds?.Count ?? 0, _cachedSave?.Controller?.Decks?.Count ?? 0);
            SaveCached(reason);
        }
        finally
        {
            perfScope.SetItemCount("ownedPrints", _cachedSave?.Profile?.OwnedPrintIds?.Count ?? 0);
            perfScope.SetItemCount("savedDecks", _cachedSave?.Controller?.Decks?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    public static bool TryReloadProfileData(out PlayerProfileData profile)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.TryReloadProfileData");
        profile = null;

        try
        {
            if (!TryLoadCanonicalSave(CanonicalSavePath, out GameSaveData saveData, out string error))
            {
                Debug.LogWarning($"{LogPrefix} Failed to reload canonical save at {CanonicalSavePath}. {error}");
                return false;
            }

            _cachedSave = saveData;
            EnsureSaveInitialized(_cachedSave);
            if (ContinuousController.instance != null)
            {
                MigrateUnifiedSaveIfNeeded(_cachedSave);
                MigrateProfileOwnedPrintsIfPossible(_cachedSave.Profile);
                MigrateDeckPrintRefsIfNeeded(_cachedSave.Controller);
            }
            _isLoaded = true;
            profile = _cachedSave.Profile;
            LogCanonicalSaveStatus("reload profile");
            return true;
        }
        finally
        {
            perfScope.SetItemCount("ownedPrints", _cachedSave?.Profile?.OwnedPrintIds?.Count ?? 0);
            perfScope.SetItemCount("savedDecks", _cachedSave?.Controller?.Decks?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    public static void LogCanonicalSaveStatus(string context = null)
    {
        string path = CanonicalSavePath;
        bool exists = File.Exists(path);
        long bytes = exists ? new FileInfo(path).Length : 0L;
        string saveTimestamp = LastSuccessfulSaveUtc;
        string contextLabel = string.IsNullOrWhiteSpace(context) ? "status" : context.Trim();
        Debug.Log($"{LogPrefix} {contextLabel} path={path} exists={exists} bytes={bytes} lastSaveUtc={saveTimestamp}");
    }

    public static bool HasStarterDeckGrant(string key, ContinuousController controller = null)
    {
        EnsureLoaded(controller);
        return ContainsStarterDeckKey(_cachedSave.Controller, key);
    }

    public static bool RecordStarterDeckGrant(string key)
    {
        EnsureLoaded(ContinuousController.instance);
        EnsureControllerInitialized(_cachedSave.Controller);
        return TryAddStarterDeckKey(_cachedSave.Controller, key);
    }

    static void EnsureLoaded(ContinuousController controller)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.EnsureLoaded");

        try
        {
            if (_isLoaded)
            {
                bool incrementalMigrationChanged = false;
                if (controller != null)
                {
                    incrementalMigrationChanged |= TryCompleteLegacyControllerMigration(controller);
                    incrementalMigrationChanged |= MigrateProfileOwnedPrintsIfPossible(_cachedSave.Profile);
                    incrementalMigrationChanged |= MigrateDeckPrintRefsIfNeeded(_cachedSave.Controller);
                }

                if (incrementalMigrationChanged)
                {
                    SaveCached("incremental save migration");
                }

                return;
            }

            bool loadedFromCanonical = TryLoadCanonicalSave(CanonicalSavePath, out _cachedSave, out string error);
            if (!loadedFromCanonical)
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning($"{LogPrefix} Failed to load canonical save at {CanonicalSavePath}. {error}");
                }

                _cachedSave = CreateDefaultSaveData();
            }

            EnsureSaveInitialized(_cachedSave);

            bool changed = false;
            changed |= MigrateUnifiedSaveIfNeeded(_cachedSave);

            if (!_cachedSave.LegacyProfileMigrated)
            {
                changed |= TryMigrateLegacyProfile(_cachedSave);
                _cachedSave.LegacyProfileMigrated = true;
            }

            if (controller != null)
            {
                changed |= TryCompleteLegacyControllerMigration(controller);
                changed |= MigrateProfileOwnedPrintsIfPossible(_cachedSave.Profile);
                changed |= MigrateDeckPrintRefsIfNeeded(_cachedSave.Controller);
            }

            _isLoaded = true;

            if (!loadedFromCanonical || changed)
            {
                SaveCached(loadedFromCanonical ? "save migration" : "initial canonical save");
            }
            else
            {
                LogCanonicalSaveStatus("loaded");
            }
        }
        finally
        {
            perfScope.SetItemCount("hasController", controller != null ? 1 : 0);
            perfScope.SetItemCount("ownedPrints", _cachedSave?.Profile?.OwnedPrintIds?.Count ?? 0);
            perfScope.SetItemCount("savedDecks", _cachedSave?.Controller?.Decks?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    static bool TryLoadCanonicalSave(string path, out GameSaveData saveData, out string error)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.TryLoadCanonicalSave");
        saveData = null;
        error = string.Empty;

        try
        {
            if (!File.Exists(path))
            {
                error = "Save file does not exist.";
                return false;
            }

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Save file is empty.";
                return false;
            }

            saveData = JsonUtility.FromJson<GameSaveData>(json);
            if (saveData == null)
            {
                error = "JsonUtility returned null.";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            perfScope.SetItemCount("fileExists", File.Exists(path) ? 1 : 0);
            perfScope.Dispose();
        }
    }

    static GameSaveData CreateDefaultSaveData()
    {
        GameSaveData saveData = new GameSaveData();
        EnsureSaveInitialized(saveData);
        return saveData;
    }

    static void EnsureSaveInitialized(GameSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        if (saveData.SaveVersion <= 0)
        {
            saveData.SaveVersion = CurrentSaveVersion;
        }

        saveData.Profile ??= new PlayerProfileData();
        saveData.Controller ??= new GameSaveControllerData();

        EnsureProfileInitialized(saveData.Profile);
        EnsureControllerInitialized(saveData.Controller);
    }

    static void EnsureProfileInitialized(PlayerProfileData profile)
    {
        if (profile == null)
        {
            return;
        }

        if (profile.SaveVersion <= 0)
        {
            profile.SaveVersion = 1;
        }

        profile.UnlockedCardIds ??= new List<string>();
        profile.OwnedPrintIds ??= new List<string>();
        profile.PurchasedProductIds ??= new List<string>();
        profile.CompletedStoryNodeIds ??= new List<string>();
        profile.EarnedStoryKeyIds ??= new List<string>();
        profile.CompletedDuelBoardIds ??= new List<string>();
        profile.ClaimedPromoCardIds ??= new List<string>();
    }

    static void EnsureControllerInitialized(GameSaveControllerData controllerData)
    {
        if (controllerData == null)
        {
            return;
        }

        controllerData.PlayerName = string.IsNullOrWhiteSpace(controllerData.PlayerName)
            ? "Player"
            : DeckData.ValidateDeckName(controllerData.PlayerName);
        controllerData.Decks ??= new List<GameSaveDeckData>();
        controllerData.StarterDeckKeys ??= new List<string>();

        for (int index = controllerData.Decks.Count - 1; index >= 0; index--)
        {
            GameSaveDeckData deck = controllerData.Decks[index];
            if (deck == null)
            {
                controllerData.Decks.RemoveAt(index);
                continue;
            }

            deck.DeckId = string.IsNullOrWhiteSpace(deck.DeckId) ? Guid.NewGuid().ToString("N") : deck.DeckId.Trim();
            deck.DeckName = string.IsNullOrWhiteSpace(deck.DeckName) ? "NewDeck" : DeckData.ValidateDeckName(deck.DeckName);
            deck.MainDeckCardIds ??= new List<int>();
            deck.DigitamaDeckCardIds ??= new List<int>();
            deck.MainDeckCardRefs ??= new List<CardPrintRef>();
            deck.DigitamaDeckCardRefs ??= new List<CardPrintRef>();
            deck.KeyCardRef ??= new CardPrintRef();
            deck.MainDeckCardIds = deck.MainDeckCardIds.Where(cardId => cardId > 0).ToList();
            deck.DigitamaDeckCardIds = deck.DigitamaDeckCardIds.Where(cardId => cardId > 0).ToList();
            deck.MainDeckCardRefs = NormalizeCardPrintRefs(deck.MainDeckCardRefs);
            deck.DigitamaDeckCardRefs = NormalizeCardPrintRefs(deck.DigitamaDeckCardRefs);
            deck.KeyCardRef = NormalizeCardPrintRef(deck.KeyCardRef);
            if (deck.SortValue < 0)
            {
                deck.SortValue = 0;
            }
        }

        controllerData.StarterDeckKeys = controllerData.StarterDeckKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (float.IsNaN(controllerData.BgmVolume) || float.IsInfinity(controllerData.BgmVolume))
        {
            controllerData.BgmVolume = 0.5f;
        }

        if (float.IsNaN(controllerData.SeVolume) || float.IsInfinity(controllerData.SeVolume))
        {
            controllerData.SeVolume = 0.5f;
        }

        controllerData.Language = SanitizeLanguage(controllerData.Language);
        controllerData.ServerRegion = string.IsNullOrWhiteSpace(controllerData.ServerRegion)
            ? "us"
            : controllerData.ServerRegion.Trim();
    }

    static bool MigrateUnifiedSaveIfNeeded(GameSaveData saveData)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.MigrateUnifiedSaveIfNeeded");

        try
        {
            if (saveData == null)
            {
                return false;
            }

            if (saveData.SaveVersion >= CurrentSaveVersion)
            {
                return false;
            }

            saveData.SaveVersion = CurrentSaveVersion;
            return true;
        }
        finally
        {
            perfScope.SetItemCount("saveVersion", saveData?.SaveVersion ?? 0);
            perfScope.Dispose();
        }
    }

    static bool MigrateProfileOwnedPrintsIfPossible(PlayerProfileData profile)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.MigrateProfileOwnedPrintsIfPossible");

        try
        {
            if (profile?.UnlockedCardIds == null || profile.UnlockedCardIds.Count == 0)
            {
                return false;
            }

            profile.OwnedPrintIds ??= new List<string>();

            bool changed = false;
            for (int index = 0; index < profile.UnlockedCardIds.Count; index++)
            {
                CEntity_Base canonicalPrint = CardPrintCatalog.GetCanonicalPrint(profile.UnlockedCardIds[index]);
                if (canonicalPrint == null)
                {
                    continue;
                }

                string printId = CardPrintCatalog.NormalizeStoredPrintId(canonicalPrint.EffectivePrintID);
                if (string.IsNullOrWhiteSpace(printId))
                {
                    continue;
                }

                if (profile.OwnedPrintIds.Any(existing => CardPrintCatalog.NormalizeLookupCode(existing) == CardPrintCatalog.NormalizeLookupCode(printId)))
                {
                    continue;
                }

                profile.OwnedPrintIds.Add(printId);
                changed = true;
            }

            if (changed)
            {
                profile.OwnedPrintIds = profile.OwnedPrintIds
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(CardPrintCatalog.NormalizeStoredPrintId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return changed;
        }
        finally
        {
            perfScope.SetItemCount("unlockedCardIds", profile?.UnlockedCardIds?.Count ?? 0);
            perfScope.SetItemCount("ownedPrints", profile?.OwnedPrintIds?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    static bool MigrateDeckPrintRefsIfNeeded(GameSaveControllerData controllerData)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.MigrateDeckPrintRefsIfNeeded");

        try
        {
            if (controllerData?.Decks == null || controllerData.Decks.Count == 0)
            {
                return false;
            }

            bool changed = false;
            for (int index = 0; index < controllerData.Decks.Count; index++)
            {
                GameSaveDeckData deck = controllerData.Decks[index];
                if (deck == null)
                {
                    continue;
                }

                if ((deck.MainDeckCardRefs == null || deck.MainDeckCardRefs.Count == 0) && deck.MainDeckCardIds != null && deck.MainDeckCardIds.Count > 0)
                {
                    deck.MainDeckCardRefs = deck.MainDeckCardIds
                        .Select(ResolveRefFromLegacyCardIndex)
                        .Where(cardRef => cardRef != null && !cardRef.IsEmpty)
                        .ToList();
                    changed |= deck.MainDeckCardRefs.Count > 0;
                }

                if ((deck.DigitamaDeckCardRefs == null || deck.DigitamaDeckCardRefs.Count == 0) && deck.DigitamaDeckCardIds != null && deck.DigitamaDeckCardIds.Count > 0)
                {
                    deck.DigitamaDeckCardRefs = deck.DigitamaDeckCardIds
                        .Select(ResolveRefFromLegacyCardIndex)
                        .Where(cardRef => cardRef != null && !cardRef.IsEmpty)
                        .ToList();
                    changed |= deck.DigitamaDeckCardRefs.Count > 0;
                }

                if ((deck.KeyCardRef == null || deck.KeyCardRef.IsEmpty) && deck.KeyCardId > 0)
                {
                    CardPrintRef keyCardRef = ResolveRefFromLegacyCardIndex(deck.KeyCardId);
                    if (keyCardRef != null && !keyCardRef.IsEmpty)
                    {
                        deck.KeyCardRef = keyCardRef;
                        changed = true;
                    }
                }

                deck.MainDeckCardRefs = NormalizeCardPrintRefs(deck.MainDeckCardRefs);
                deck.DigitamaDeckCardRefs = NormalizeCardPrintRefs(deck.DigitamaDeckCardRefs);
                deck.KeyCardRef = NormalizeCardPrintRef(deck.KeyCardRef);
            }

            return changed;
        }
        finally
        {
            perfScope.SetItemCount("savedDecks", controllerData?.Decks?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    static CardPrintRef ResolveRefFromLegacyCardIndex(int cardIndex)
    {
        if (cardIndex <= 0)
        {
            return new CardPrintRef();
        }

        CEntity_Base card = CardPrintCatalog.ResolveLegacyCardIndex(cardIndex)
            ?? ContinuousController.instance?.getCardEntityByCardID(cardIndex);
        return CardPrintRef.FromCard(card);
    }

    static List<CardPrintRef> NormalizeCardPrintRefs(IEnumerable<CardPrintRef> refs)
    {
        return refs?
            .Where(cardRef => cardRef != null && !cardRef.IsEmpty)
            .Select(NormalizeCardPrintRef)
            .Where(cardRef => cardRef != null && !cardRef.IsEmpty)
            .ToList() ?? new List<CardPrintRef>();
    }

    static CardPrintRef NormalizeCardPrintRef(CardPrintRef cardRef)
    {
        if (cardRef == null || cardRef.IsEmpty)
        {
            return new CardPrintRef();
        }

        return new CardPrintRef(
            CardPrintCatalog.NormalizeCardId(cardRef.CardId),
            CardPrintCatalog.NormalizeStoredPrintId(cardRef.PrintId));
    }

    static bool TryMigrateLegacyProfile(GameSaveData saveData)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.TryMigrateLegacyProfile");
        string legacyProfilePath = Path.Combine(Application.persistentDataPath, LegacyProfileFileName);

        try
        {
            if (!File.Exists(legacyProfilePath))
            {
                return false;
            }

            string json = File.ReadAllText(legacyProfilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            PlayerProfileData profile = JsonUtility.FromJson<PlayerProfileData>(json);
            if (profile == null)
            {
                return false;
            }

            saveData.Profile = profile;
            EnsureProfileInitialized(saveData.Profile);
            Debug.Log($"{LogPrefix} Migrated legacy profile path={legacyProfilePath}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{LogPrefix} Failed to migrate legacy profile path={legacyProfilePath}. {exception.Message}");
            return false;
        }
        finally
        {
            perfScope.SetItemCount("legacyProfileExists", File.Exists(legacyProfilePath) ? 1 : 0);
            perfScope.Dispose();
        }
    }

    static bool TryCompleteLegacyControllerMigration(ContinuousController controller)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.TryCompleteLegacyControllerMigration");

        try
        {
            if (controller == null)
            {
                return false;
            }

            EnsureControllerInitialized(_cachedSave.Controller);
            if (_cachedSave.LegacyControllerMigrated)
            {
                return false;
            }

            TryMigrateLegacyDecks(controller, _cachedSave.Controller);
            TryMigrateLegacySettings(_cachedSave.Controller);
            TryMigrateLegacyStarterDeckKeys(controller, _cachedSave.Controller);
            _cachedSave.LegacyControllerMigrated = true;
            return true;
        }
        finally
        {
            perfScope.SetItemCount("savedDecks", _cachedSave?.Controller?.Decks?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    static bool TryMigrateLegacyDecks(ContinuousController controller, GameSaveControllerData controllerData)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.TryMigrateLegacyDecks");
        List<DeckData> migratedDecks = new List<DeckData>();
        bool migrated = false;

        try
        {
            string legacyDeckPath = Path.Combine(Application.persistentDataPath, "Decks");
            if (Directory.Exists(legacyDeckPath))
            {
                string[] deckFiles = Directory.GetFiles(legacyDeckPath, "*.txt");
                for (int index = 0; index < deckFiles.Length; index++)
                {
                    if (TryLoadLegacyDeckFile(deckFiles[index], out DeckData deckData))
                    {
                        migratedDecks.Add(deckData);
                        migrated = true;
                    }
                }
            }

            if (!migrated && PlayerPrefs.HasKey(controller.DeckDatasPlayerPrefsKey))
            {
                try
                {
                    List<DeckData> legacyDecks = PlayerPrefsUtil.LoadList<DeckData>(controller.DeckDatasPlayerPrefsKey);
                    if (legacyDecks != null && legacyDecks.Count > 0)
                    {
                        migratedDecks.AddRange(legacyDecks.Where(deck => deck != null));
                        migrated = migratedDecks.Count > 0;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"{LogPrefix} Failed to migrate legacy PlayerPrefs deck list. {exception.Message}");
                }
            }

            if (!migrated)
            {
                return false;
            }

            controllerData.Decks = CaptureDecks(migratedDecks
                .Where(deck => deck != null)
                .OrderBy(deck => deck.DeckName)
                .ToList());
            Debug.Log($"{LogPrefix} Migrated legacy decks count={controllerData.Decks.Count} legacyPath={legacyDeckPath}");
            return true;
        }
        finally
        {
            perfScope.SetItemCount("migratedDecks", migratedDecks.Count);
            perfScope.SetItemCount("savedDecks", controllerData?.Decks?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    static bool TryLoadLegacyDeckFile(string deckPath, out DeckData deckData)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.TryLoadLegacyDeckFile");
        deckData = null;

        try
        {
            string fileName = Path.GetFileNameWithoutExtension(deckPath);
            if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains("_"))
            {
                return false;
            }

            string deckList = File.ReadAllText(deckPath);
            if (!deckList.Contains("//", StringComparison.Ordinal))
            {
                Debug.LogWarning($"{LogPrefix} Skipping malformed legacy deck path={deckPath}");
                return false;
            }

            using StreamReader reader = new StreamReader(deckPath);
            string deckNameLine = reader.ReadLine();
            string keyCardLine = reader.ReadLine();
            string sortValueLine = reader.ReadLine();

            if (string.IsNullOrEmpty(deckNameLine) || string.IsNullOrEmpty(keyCardLine) || string.IsNullOrEmpty(sortValueLine))
            {
                return false;
            }

            string deckName = deckNameLine.Replace("Name: ", "");
            int.TryParse(keyCardLine.Replace("Key Card: ", ""), out int keyCardId);
            int.TryParse(sortValueLine.Replace("Sort Index: ", ""), out int sortValue);

            string deckCode = deckList.Substring(deckList.IndexOf("//", StringComparison.Ordinal));
            List<CEntity_Base> allDeckCards = DeckCodeUtility.GetAllDeckCardsFromDeckBuilderDeckCode(deckCode);
            if (allDeckCards.Count == 0)
            {
                allDeckCards = DeckCodeUtility.GetAllDeckCardsFromTTSDeckCode(deckCode);
            }

            if (allDeckCards.Count == 0)
            {
                return false;
            }

            List<CEntity_Base> mainDeck = new List<CEntity_Base>();
            List<CEntity_Base> digitamaDeck = new List<CEntity_Base>();
            for (int index = 0; index < allDeckCards.Count; index++)
            {
                CEntity_Base card = allDeckCards[index];
                if (card == null)
                {
                    continue;
                }

                if (card.cardKind == CardKind.DigiEgg)
                {
                    digitamaDeck.Add(card);
                }
                else
                {
                    mainDeck.Add(card);
                }
            }

            string deckId = fileName.Split('_')[1];
            deckData = new DeckData(string.Empty, deckId)
            {
                DeckName = deckName,
                SortValue = Mathf.Max(0, sortValue),
            };
            CEntity_Base keyCard = keyCardId >= 0
                ? ContinuousController.instance?.getCardEntityByCardID(keyCardId)
                : mainDeck.FirstOrDefault();
            deckData.SetDeckCardsFromResolvedCards(mainDeck, digitamaDeck, keyCard);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{LogPrefix} Failed to migrate legacy deck path={deckPath}. {exception.Message}");
            return false;
        }
        finally
        {
            perfScope.SetItemCount("deckLoaded", deckData != null ? 1 : 0);
            perfScope.Dispose();
        }
    }

    static bool TryMigrateLegacySettings(GameSaveControllerData controllerData)
    {
        bool changed = false;

        if (PlayerPrefs.HasKey("PlayerName"))
        {
            controllerData.PlayerName = DeckData.ValidateDeckName(PlayerPrefs.GetString("PlayerName"));
            changed = true;
        }

        if (PlayerPrefs.HasKey("WinCount"))
        {
            controllerData.WinCount = PlayerPrefs.GetInt("WinCount");
            changed = true;
        }

        changed |= TryMigrateLegacyBool("AutoEffectOrder", ref controllerData.AutoEffectOrder);
        changed |= TryMigrateLegacyBool("AutoDeckBottomOrder", ref controllerData.AutoDeckBottomOrder);
        changed |= TryMigrateLegacyBool("AutoDeckTopOrder", ref controllerData.AutoDeckTopOrder);
        changed |= TryMigrateLegacyBool("AutoMinDigivolutionCost", ref controllerData.AutoMinDigivolutionCost);
        changed |= TryMigrateLegacyBool("AutoMaxCardCount", ref controllerData.AutoMaxCardCount);
        changed |= TryMigrateLegacyBool("AutoHatch", ref controllerData.AutoHatch);
        changed |= TryMigrateLegacyBool("ReverseOpponentsCards", ref controllerData.ReverseOpponentsCards);
        changed |= TryMigrateLegacyBool("TurnSuspendedCards", ref controllerData.TurnSuspendedCards);
        changed |= TryMigrateLegacyBool("CheckBeforeEndingSelection", ref controllerData.CheckBeforeEndingSelection);
        changed |= TryMigrateLegacyBool("SuspendedCardsDirectionIsLeft", ref controllerData.SuspendedCardsDirectionIsLeft);
        changed |= TryMigrateLegacyBool("ShowBackgroundParticle", ref controllerData.ShowBackgroundParticle);

        if (PlayerPrefs.HasKey("BGMVolume"))
        {
            controllerData.BgmVolume = PlayerPrefs.GetFloat("BGMVolume");
            changed = true;
        }

        if (PlayerPrefs.HasKey("SEVolume"))
        {
            controllerData.SeVolume = PlayerPrefs.GetFloat("SEVolume");
            changed = true;
        }

        if (PlayerPrefs.HasKey("Language"))
        {
            controllerData.Language = SanitizeLanguage(PlayerPrefs.GetString("Language", Language.ENG.ToString()));
            changed = true;
        }

        if (PlayerPrefs.HasKey("ServerRegion"))
        {
            controllerData.ServerRegion = PlayerPrefs.GetString("ServerRegion", "us");
            changed = true;
        }

        return changed;
    }

    static bool TryMigrateLegacyStarterDeckKeys(ContinuousController controller, GameSaveControllerData controllerData)
    {
        StarterDeck starterDeck = controller.GetComponent<StarterDeck>();
        if (starterDeck == null || starterDeck.starterDeckDatas == null || starterDeck.starterDeckDatas.Count == 0)
        {
            return false;
        }

        bool changed = false;
        for (int index = 0; index < starterDeck.starterDeckDatas.Count; index++)
        {
            StarterDeckData starterDeckData = starterDeck.starterDeckDatas[index];
            if (starterDeckData == null || string.IsNullOrWhiteSpace(starterDeckData.Key))
            {
                continue;
            }

            if (!PlayerPrefs.HasKey(starterDeckData.Key))
            {
                continue;
            }

            if (PlayerPrefs.GetInt(starterDeckData.Key) != 2)
            {
                continue;
            }

            changed |= TryAddStarterDeckKey(controllerData, starterDeckData.Key);
        }

        return changed;
    }

    static bool SaveCached(string reason)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.SaveCached");

        try
        {
            EnsureSaveInitialized(_cachedSave);
            _cachedSave.SaveVersion = CurrentSaveVersion;
            string savePath = CanonicalSavePath;
            string tempPath = savePath + TempExtension;
            string backupPath = savePath + BackupExtension;
            string timestamp = DateTimeOffset.UtcNow.ToString("O");
            _cachedSave.LastSavedUtc = timestamp;
            StartupPerfTrace.RecordSaveCached(reason, _cachedSave?.Profile?.OwnedPrintIds?.Count ?? 0, _cachedSave?.Controller?.Decks?.Count ?? 0);

            Directory.CreateDirectory(Path.GetDirectoryName(savePath) ?? Application.persistentDataPath);

            string json = JsonUtility.ToJson(_cachedSave, true);
            File.WriteAllText(tempPath, json);

            if (!TryLoadCanonicalSave(tempPath, out _, out string validationError))
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                Debug.LogWarning($"{LogPrefix} Save validation failed path={savePath} tempPath={tempPath} reason={reason ?? "unspecified"} error={validationError}");
                return false;
            }

            if (File.Exists(savePath))
            {
                File.Replace(tempPath, savePath, backupPath);
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            else
            {
                File.Move(tempPath, savePath);
            }

            EnsureIncludedInBackup(savePath);

            FileInfo fileInfo = new FileInfo(savePath);
            _lastSuccessfulSaveUtc = timestamp;
            Debug.Log($"{LogPrefix} Save succeeded path={savePath} bytes={fileInfo.Length} lastSaveUtc={timestamp} reason={reason ?? "unspecified"}");
            return true;
        }
        catch (Exception exception)
        {
            string tempPath = CanonicalSavePath + TempExtension;
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            Debug.LogWarning($"{LogPrefix} Save failed path={CanonicalSavePath} lastSaveUtc={LastSuccessfulSaveUtc} reason={reason ?? "unspecified"} error={exception.Message}");
            return false;
        }
        finally
        {
            perfScope.SetItemCount("ownedPrints", _cachedSave?.Profile?.OwnedPrintIds?.Count ?? 0);
            perfScope.SetItemCount("savedDecks", _cachedSave?.Controller?.Decks?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    static void EnsureIncludedInBackup(string savePath)
    {
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            Device.ResetNoBackupFlag(savePath);
            Debug.Log($"{LogPrefix} iCloud backup enabled path={savePath}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{LogPrefix} Failed to clear iOS no-backup flag path={savePath}. {exception.Message}");
        }
#endif
    }

    static GameSaveControllerData CaptureControllerData(ContinuousController controller)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.CaptureControllerData");

        try
        {
            GameSaveControllerData controllerData = new GameSaveControllerData
            {
                PlayerName = string.IsNullOrWhiteSpace(controller.PlayerName) ? "Player" : controller.PlayerName,
                WinCount = controller.WinCount,
                AutoEffectOrder = controller.autoEffectOrder,
                AutoDeckBottomOrder = controller.autoDeckBottomOrder,
                AutoDeckTopOrder = controller.autoDeckTopOrder,
                AutoMinDigivolutionCost = controller.autoMinDigivolutionCost,
                AutoMaxCardCount = controller.autoMaxCardCount,
                AutoHatch = controller.autoHatch,
                ReverseOpponentsCards = controller.reverseOpponentsCards,
                TurnSuspendedCards = controller.turnSuspendedCards,
                CheckBeforeEndingSelection = controller.checkBeforeEndingSelection,
                SuspendedCardsDirectionIsLeft = controller.suspendedCardsDirectionIsLeft,
                ShowBackgroundParticle = controller.showBackgroundParticle,
                BgmVolume = controller.BGMVolume,
                SeVolume = controller.SEVolume,
                ServerRegion = string.IsNullOrWhiteSpace(controller.serverRegion) ? "us" : controller.serverRegion,
                Language = SanitizeLanguage(controller.language.ToString()),
                Decks = CaptureDecks(controller.DeckDatas),
            };

            if (_cachedSave?.Controller?.StarterDeckKeys != null)
            {
                controllerData.StarterDeckKeys = new List<string>(_cachedSave.Controller.StarterDeckKeys);
            }

            EnsureControllerInitialized(controllerData);
            return controllerData;
        }
        finally
        {
            perfScope.SetItemCount("savedDecks", controller?.DeckDatas?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    static List<GameSaveDeckData> CaptureDecks(IEnumerable<DeckData> decks)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.CaptureDecks");
        List<GameSaveDeckData> saveDecks = new List<GameSaveDeckData>();
        try
        {
            if (decks == null)
            {
                return saveDecks;
            }

            foreach (DeckData deck in decks)
            {
                if (deck == null)
                {
                    continue;
                }

                saveDecks.Add(new GameSaveDeckData
                {
                    DeckId = string.IsNullOrWhiteSpace(deck.DeckID) ? Guid.NewGuid().ToString("N") : deck.DeckID,
                    DeckName = string.IsNullOrWhiteSpace(deck.DeckName) ? "NewDeck" : DeckData.ValidateDeckName(deck.DeckName),
                    KeyCardId = deck.KeyCardId,
                    KeyCardRef = deck.GetStoredKeyCardRef(),
                    SortValue = Mathf.Max(0, deck.SortValue),
                    MainDeckCardIds = deck.DeckCardIDs != null ? deck.DeckCardIDs.Where(cardId => cardId > 0).ToList() : new List<int>(),
                    DigitamaDeckCardIds = deck.DigitamaDeckCardIDs != null ? deck.DigitamaDeckCardIDs.Where(cardId => cardId > 0).ToList() : new List<int>(),
                    MainDeckCardRefs = deck.GetStoredMainDeckRefs(),
                    DigitamaDeckCardRefs = deck.GetStoredDigitamaDeckRefs(),
                });
            }

            return saveDecks;
        }
        finally
        {
            perfScope.SetItemCount("savedDecks", saveDecks.Count);
            perfScope.Dispose();
        }
    }

    public static List<DeckData> BuildDecksFromSaveData(IEnumerable<GameSaveDeckData> saveDecks)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("GameSaveManager.BuildDecksFromSaveData");
        List<DeckData> decks = new List<DeckData>();
        try
        {
            if (saveDecks == null)
            {
                return decks;
            }

            foreach (GameSaveDeckData saveDeck in saveDecks)
            {
                if (saveDeck == null)
                {
                    continue;
                }

                DeckData deckData = new DeckData(string.Empty, saveDeck.DeckId)
                {
                    DeckName = string.IsNullOrWhiteSpace(saveDeck.DeckName) ? "NewDeck" : DeckData.ValidateDeckName(saveDeck.DeckName),
                    SortValue = Mathf.Max(0, saveDeck.SortValue),
                };
                List<CardPrintRef> mainDeckRefs = saveDeck.MainDeckCardRefs != null && saveDeck.MainDeckCardRefs.Count > 0
                    ? NormalizeCardPrintRefs(saveDeck.MainDeckCardRefs)
                    : (saveDeck.MainDeckCardIds ?? new List<int>()).Where(cardId => cardId > 0).Select(ResolveRefFromLegacyCardIndex).Where(cardRef => !cardRef.IsEmpty).ToList();
                List<CardPrintRef> digitamaDeckRefs = saveDeck.DigitamaDeckCardRefs != null && saveDeck.DigitamaDeckCardRefs.Count > 0
                    ? NormalizeCardPrintRefs(saveDeck.DigitamaDeckCardRefs)
                    : (saveDeck.DigitamaDeckCardIds ?? new List<int>()).Where(cardId => cardId > 0).Select(ResolveRefFromLegacyCardIndex).Where(cardRef => !cardRef.IsEmpty).ToList();
                CardPrintRef keyCardRef = saveDeck.KeyCardRef != null && !saveDeck.KeyCardRef.IsEmpty
                    ? NormalizeCardPrintRef(saveDeck.KeyCardRef)
                    : ResolveRefFromLegacyCardIndex(saveDeck.KeyCardId);
                deckData.SetStoredDeckRefs(mainDeckRefs, digitamaDeckRefs, keyCardRef);
                decks.Add(deckData);
            }

            return decks;
        }
        finally
        {
            perfScope.SetItemCount("builtDecks", decks.Count);
            perfScope.Dispose();
        }
    }

    static bool ContainsStarterDeckKey(GameSaveControllerData controllerData, string key)
    {
        if (controllerData?.StarterDeckKeys == null || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        string trimmedKey = key.Trim();
        return controllerData.StarterDeckKeys.Contains(trimmedKey);
    }

    static bool TryAddStarterDeckKey(GameSaveControllerData controllerData, string key)
    {
        if (controllerData == null || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        string trimmedKey = key.Trim();
        if (controllerData.StarterDeckKeys.Contains(trimmedKey))
        {
            return false;
        }

        controllerData.StarterDeckKeys.Add(trimmedKey);
        return true;
    }

    static bool TryMigrateLegacyBool(string key, ref bool value)
    {
        if (!PlayerPrefs.HasKey(key))
        {
            return false;
        }

        value = PlayerPrefsUtil.GetBool(key, value);
        return true;
    }

    static string SanitizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return Language.ENG.ToString();
        }

        return Enum.TryParse(language.Trim(), out Language parsedLanguage)
            ? parsedLanguage.ToString()
            : Language.ENG.ToString();
    }
}
