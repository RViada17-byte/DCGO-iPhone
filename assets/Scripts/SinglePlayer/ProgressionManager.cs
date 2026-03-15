using System;
using System.Collections.Generic;
using UnityEngine;

public class ProgressionManager : MonoBehaviour
{
    private const int CurrentSaveVersion = 4;
    private const int NewProfileStartingCurrency = 1000;

    private static ProgressionManager _instance;
    private PlayerProfileData _profile;
    private bool _isLoaded;
    private HashSet<string> _ownedPrintLookupCache;
    private HashSet<string> _unlockedCardIdLookupCache;
    private bool _legacyUnlockedCardMigrationVerified;

    public static ProgressionManager Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindObjectOfType<ProgressionManager>();
            if (_instance != null)
            {
                return _instance;
            }

            GameObject runtimeObject = new GameObject(nameof(ProgressionManager));
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(runtimeObject);
            }
            _instance = runtimeObject.AddComponent<ProgressionManager>();
            return _instance;
        }
    }

    public static ProgressionManager LoadedInstance => _instance;

    public PlayerProfileData CurrentProfileData => _profile;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        _ = Instance;
    }

    public void LoadOrCreate()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("ProgressionManager.LoadOrCreate");

        try
        {
            if (_isLoaded && _profile != null)
            {
                EnsureCollectionsInitialized();
                return;
            }

            _profile = GameSaveManager.LoadOrCreateProfileData();
            _legacyUnlockedCardMigrationVerified = false;
            EnsureCollectionsInitialized();
            bool migrated = MigrateProfileIfNeeded();
            _isLoaded = true;
            InvalidateOwnedPrintLookupCache();
            if (migrated || !GameSaveManager.CanonicalSaveExists)
            {
                Save(migrated ? "profile migration" : "initial canonical save");
            }
        }
        finally
        {
            perfScope.SetItemCount("ownedPrints", _profile?.OwnedPrintIds?.Count ?? 0);
            perfScope.SetItemCount("unlockedCardIds", _profile?.UnlockedCardIds?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    public void Save(string reason = null)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("ProgressionManager.Save");

        try
        {
            EnsureLoaded();
            EnsureCollectionsInitialized();
            MigrateLegacyUnlockedCardsToOwnedPrints();
            InvalidateOwnedPrintLookupCache();
            _profile.SaveVersion = CurrentSaveVersion;
            // Profile saves must not capture controller state. The controller owns its
            // own save cadence, and capturing it here can overwrite deck data before the
            // runtime deck list has been loaded.
            GameSaveManager.SaveAll(_profile, null, reason ?? "profile save");
        }
        finally
        {
            perfScope.SetItemCount("ownedPrints", _profile?.OwnedPrintIds?.Count ?? 0);
            perfScope.SetItemCount("unlockedCardIds", _profile?.UnlockedCardIds?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    public int GetCurrency()
    {
        EnsureLoaded();
        return _profile.Currency;
    }

    public void AddCurrency(int amount, bool saveImmediately = true)
    {
        EnsureLoaded();
        if (amount == 0)
        {
            return;
        }

        _profile.Currency += amount;
        if (_profile.Currency < 0)
        {
            _profile.Currency = 0;
        }

        if (saveImmediately)
        {
            Save();
        }
    }

    public bool TrySpendCurrency(int amount, bool saveImmediately = true)
    {
        EnsureLoaded();

        if (amount < 0)
        {
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        if (_profile.Currency < amount)
        {
            return false;
        }

        _profile.Currency -= amount;
        if (saveImmediately)
        {
            Save();
        }
        return true;
    }

    public bool IsCardUnlocked(string cardId)
    {
        EnsureLoaded();
        return HasUnlockedCard(cardId);
    }

    public void UnlockCard(string cardId, bool saveImmediately = true)
    {
        if (UnlockCanonicalPrint(cardId, saveImmediately: false) && saveImmediately)
        {
            Save();
        }
    }

    public void UnlockCards(IEnumerable<string> cardIds, bool saveImmediately = true)
    {
        UnlockCanonicalPrints(cardIds, saveImmediately);
    }

    public bool OwnsPrint(string printId)
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("ProgressionManager.OwnsPrint");

        try
        {
            EnsureLoaded();
            return HasOwnedPrint(printId);
        }
        finally
        {
            perfScope.SetItemCount("ownedPrints", _profile?.OwnedPrintIds?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    public bool UnlockPrint(string printId, bool saveImmediately = true)
    {
        EnsureLoaded();
        bool changed = TryAddUnique(_profile.OwnedPrintIds, CardPrintCatalog.NormalizeStoredPrintId(printId));
        if (changed)
        {
            InvalidateOwnedPrintLookupCache();
        }
        if (changed && saveImmediately)
        {
            Save();
        }

        return changed;
    }

    public void UnlockPrints(IEnumerable<string> printIds, bool saveImmediately = true)
    {
        EnsureLoaded();
        if (printIds == null)
        {
            return;
        }

        bool changed = false;
        foreach (string printId in printIds)
        {
            changed |= TryAddUnique(_profile.OwnedPrintIds, CardPrintCatalog.NormalizeStoredPrintId(printId));
        }

        if (changed)
        {
            InvalidateOwnedPrintLookupCache();
        }

        if (changed && saveImmediately)
        {
            Save();
        }
    }

    public bool UnlockCanonicalPrint(string cardId, bool saveImmediately = true)
    {
        EnsureLoaded();
        CEntity_Base canonicalPrint = CardPrintCatalog.GetCanonicalPrint(cardId);
        if (canonicalPrint == null)
        {
            return false;
        }

        return UnlockPrint(canonicalPrint.EffectivePrintID, saveImmediately);
    }

    public void UnlockCanonicalPrints(IEnumerable<string> cardIds, bool saveImmediately = true)
    {
        EnsureLoaded();
        if (cardIds == null)
        {
            return;
        }

        bool changed = false;
        foreach (string cardId in cardIds)
        {
            CEntity_Base canonicalPrint = CardPrintCatalog.GetCanonicalPrint(cardId);
            if (canonicalPrint == null)
            {
                continue;
            }

            changed |= TryAddUnique(_profile.OwnedPrintIds, canonicalPrint.EffectivePrintID);
        }

        if (changed)
        {
            InvalidateOwnedPrintLookupCache();
        }

        if (changed && saveImmediately)
        {
            Save();
        }
    }

    public HashSet<string> GetUnlockedCardIdSetSnapshot()
    {
        EnsureLoaded();
        return new HashSet<string>(GetUnlockedCardIdLookupCache(), StringComparer.OrdinalIgnoreCase);
    }

    public HashSet<string> GetOwnedPrintIdSetSnapshot()
    {
        EnsureLoaded();
        return new HashSet<string>(GetOwnedPrintLookupCache(), StringComparer.OrdinalIgnoreCase);
    }

    public bool HasPurchasedProduct(string productId)
    {
        EnsureLoaded();
        return ContainsValue(_profile.PurchasedProductIds, productId);
    }

    public void MarkProductPurchased(string productId, bool saveImmediately = true)
    {
        EnsureLoaded();
        if (TryAddUnique(_profile.PurchasedProductIds, productId))
        {
            if (saveImmediately)
            {
                Save();
            }
        }
    }

    public bool IsStoryCompleted(string nodeId)
    {
        EnsureLoaded();
        return ContainsValue(_profile.CompletedStoryNodeIds, nodeId);
    }

    public bool MarkStoryCompleted(string nodeId, bool saveImmediately = true)
    {
        EnsureLoaded();
        if (TryAddUnique(_profile.CompletedStoryNodeIds, nodeId))
        {
            if (saveImmediately)
            {
                Save();
            }

            return true;
        }

        return false;
    }

    public bool HasStoryKey(string keyId)
    {
        EnsureLoaded();
        return ContainsValue(_profile.EarnedStoryKeyIds, keyId);
    }

    public bool EarnStoryKey(string keyId, bool saveImmediately = true)
    {
        EnsureLoaded();
        if (TryAddUnique(_profile.EarnedStoryKeyIds, keyId))
        {
            if (saveImmediately)
            {
                Save();
            }

            return true;
        }

        return false;
    }

    public HashSet<string> GetEarnedStoryKeyIdSetSnapshot()
    {
        EnsureLoaded();
        return new HashSet<string>(_profile.EarnedStoryKeyIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
    }

    public bool IsBoardCompleted(string duelId)
    {
        EnsureLoaded();
        return ContainsValue(_profile.CompletedDuelBoardIds, duelId);
    }

    public bool MarkBoardCompleted(string duelId, bool saveImmediately = true)
    {
        EnsureLoaded();
        if (TryAddUnique(_profile.CompletedDuelBoardIds, duelId))
        {
            if (saveImmediately)
            {
                Save();
            }

            return true;
        }

        return false;
    }

    public bool HasClaimedPromo(string cardId)
    {
        EnsureLoaded();
        return ContainsValue(_profile.ClaimedPromoCardIds, cardId);
    }

    public bool MarkPromoClaimed(string cardId, bool saveImmediately = true)
    {
        EnsureLoaded();
        if (TryAddUnique(_profile.ClaimedPromoCardIds, cardId))
        {
            if (saveImmediately)
            {
                Save();
            }

            return true;
        }

        return false;
    }

    public void ResetProfileForDev()
    {
        _profile = new PlayerProfileData
        {
            SaveVersion = CurrentSaveVersion,
            Currency = NewProfileStartingCurrency,
        };
        _legacyUnlockedCardMigrationVerified = false;
        EnsureCollectionsInitialized();
        InvalidateOwnedPrintLookupCache();
        _isLoaded = true;
        Save();
    }

    private static bool ContainsValue(List<string> values, string value)
    {
        if (values == null)
        {
            return false;
        }

        string normalized = NormalizeValue(value);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        for (int index = 0; index < values.Count; index++)
        {
            if (NormalizeValue(values[index]) == normalized)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryAddUnique(List<string> values, string value)
    {
        if (values == null)
        {
            return false;
        }

        string normalized = NormalizeValue(value);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        for (int index = 0; index < values.Count; index++)
        {
            if (NormalizeValue(values[index]) == normalized)
            {
                values[index] = normalized;
                return false;
            }
        }

        values.Add(normalized);
        return true;
    }

    private void EnsureLoaded()
    {
        if (_isLoaded && _profile != null)
        {
            EnsureCollectionsInitialized();
            if (MigrateLegacyUnlockedCardsToOwnedPrints())
            {
                InvalidateOwnedPrintLookupCache();
            }
            return;
        }

        LoadOrCreate();
    }

    private bool TryReloadProfileFromDisk()
    {
        if (!GameSaveManager.TryReloadProfileData(out PlayerProfileData loaded) || loaded == null)
        {
            return false;
        }

        _profile = loaded;
        _legacyUnlockedCardMigrationVerified = false;
        EnsureCollectionsInitialized();
        bool migrated = MigrateProfileIfNeeded();
        _isLoaded = true;
        InvalidateOwnedPrintLookupCache();
        if (migrated)
        {
            Save("profile reload migration");
        }
        return true;
    }

    private void EnsureCollectionsInitialized()
    {
        if (_profile == null)
        {
            _profile = new PlayerProfileData();
        }

        if (_profile.SaveVersion <= 0)
        {
            _profile.SaveVersion = 1;
        }

        _profile.UnlockedCardIds ??= new List<string>();
        _profile.OwnedPrintIds ??= new List<string>();
        _profile.PurchasedProductIds ??= new List<string>();
        _profile.CompletedStoryNodeIds ??= new List<string>();
        _profile.EarnedStoryKeyIds ??= new List<string>();
        _profile.CompletedDuelBoardIds ??= new List<string>();
        _profile.ClaimedPromoCardIds ??= new List<string>();

        NormalizeValues(_profile.UnlockedCardIds);
        NormalizeValues(_profile.OwnedPrintIds);
        NormalizeValues(_profile.PurchasedProductIds);
        NormalizeValues(_profile.CompletedStoryNodeIds);
        NormalizeValues(_profile.EarnedStoryKeyIds);
        NormalizeValues(_profile.CompletedDuelBoardIds);
        NormalizeValues(_profile.ClaimedPromoCardIds);
    }

    private bool MigrateProfileIfNeeded()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("ProgressionManager.MigrateProfileIfNeeded");

        try
        {
            EnsureCollectionsInitialized();

            bool changed = MigrateLegacyUnlockedCardsToOwnedPrints();

            if (_profile.SaveVersion < CurrentSaveVersion)
            {
                _profile.SaveVersion = CurrentSaveVersion;
                changed = true;
            }

            return changed;
        }
        finally
        {
            perfScope.SetItemCount("ownedPrints", _profile?.OwnedPrintIds?.Count ?? 0);
            perfScope.SetItemCount("unlockedCardIds", _profile?.UnlockedCardIds?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    private void Awake()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("ProgressionManager.Awake");

        try
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
            LoadOrCreate();
        }
        finally
        {
            perfScope.SetItemCount("ownedPrints", _profile?.OwnedPrintIds?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private static void NormalizeValues(List<string> values)
    {
        if (values == null)
        {
            return;
        }

        for (int index = values.Count - 1; index >= 0; index--)
        {
            string normalized = NormalizeValue(values[index]);
            if (string.IsNullOrEmpty(normalized))
            {
                values.RemoveAt(index);
                continue;
            }

            values[index] = normalized;
        }

        for (int index = values.Count - 1; index >= 0; index--)
        {
            string normalized = values[index];
            for (int innerIndex = index - 1; innerIndex >= 0; innerIndex--)
            {
                if (values[innerIndex] == normalized)
                {
                    values.RemoveAt(index);
                    break;
                }
            }
        }
    }

    private static string NormalizeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().Replace("_", "-").ToUpperInvariant();
    }

    private bool HasUnlockedCard(string cardId)
    {
        string normalizedCardId = CardPrintCatalog.NormalizeCardId(cardId);
        if (string.IsNullOrEmpty(normalizedCardId))
        {
            return false;
        }

        return GetUnlockedCardIdLookupCache().Contains(normalizedCardId);
    }

    private bool HasOwnedPrint(string printId)
    {
        string normalizedPrintLookup = CardPrintCatalog.NormalizeLookupCode(printId);
        if (string.IsNullOrEmpty(normalizedPrintLookup))
        {
            return false;
        }

        HashSet<string> ownedPrintLookup = GetOwnedPrintLookupCache();
        bool contains = ownedPrintLookup.Contains(normalizedPrintLookup);
        if (contains)
        {
            StartupPerfTrace.RecordOwnedPrintLookupCacheHit();
        }
        else
        {
            StartupPerfTrace.RecordOwnedPrintLookupCacheMiss();
        }

        return contains;
    }

    private HashSet<string> BuildOwnedPrintIdSetSnapshot()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("ProgressionManager.BuildOwnedPrintIdSetSnapshot");
        HashSet<string> ownedPrints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (_profile?.OwnedPrintIds != null)
            {
                foreach (string printId in _profile.OwnedPrintIds)
                {
                    string normalizedPrintLookup = CardPrintCatalog.NormalizeLookupCode(printId);
                    if (!string.IsNullOrEmpty(normalizedPrintLookup))
                    {
                        ownedPrints.Add(normalizedPrintLookup);
                    }
                }
            }

            return ownedPrints;
        }
        finally
        {
            perfScope.SetItemCount("ownedPrints", ownedPrints.Count);
            perfScope.Dispose();
        }
    }

    private HashSet<string> BuildUnlockedCardIdSetSnapshot()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("ProgressionManager.BuildUnlockedCardIdSetSnapshot");
        HashSet<string> unlockedCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (string ownedPrintId in GetOwnedPrintLookupCache())
            {
                CEntity_Base print = CardPrintCatalog.ResolveCardOrPrint(ownedPrintId, preferCanonical: false);
                if (print == null)
                {
                    continue;
                }

                string normalizedCardId = CardPrintCatalog.NormalizeCardId(print.CardID);
                if (!string.IsNullOrEmpty(normalizedCardId))
                {
                    unlockedCardIds.Add(normalizedCardId);
                }
            }

            return unlockedCardIds;
        }
        finally
        {
            perfScope.SetItemCount("unlockedCardIds", unlockedCardIds.Count);
            perfScope.Dispose();
        }
    }

    private bool MigrateLegacyUnlockedCardsToOwnedPrints()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("ProgressionManager.MigrateLegacyUnlockedCardsToOwnedPrints");

        try
        {
            if (_legacyUnlockedCardMigrationVerified)
            {
                return false;
            }

            if (_profile?.UnlockedCardIds == null || _profile.UnlockedCardIds.Count == 0)
            {
                _legacyUnlockedCardMigrationVerified = true;
                return false;
            }

            bool changed = false;
            foreach (string legacyCardId in _profile.UnlockedCardIds)
            {
                CEntity_Base canonicalPrint = CardPrintCatalog.GetCanonicalPrint(legacyCardId);
                if (canonicalPrint == null)
                {
                    continue;
                }

                changed |= TryAddUnique(_profile.OwnedPrintIds, canonicalPrint.EffectivePrintID);
            }

            if (changed)
            {
                InvalidateOwnedPrintLookupCache();
            }

            _legacyUnlockedCardMigrationVerified = true;
            return changed;
        }
        finally
        {
            perfScope.SetItemCount("ownedPrints", _profile?.OwnedPrintIds?.Count ?? 0);
            perfScope.SetItemCount("unlockedCardIds", _profile?.UnlockedCardIds?.Count ?? 0);
            perfScope.Dispose();
        }
    }

    private void InvalidateOwnedPrintLookupCache()
    {
        _ownedPrintLookupCache = null;
        _unlockedCardIdLookupCache = null;
    }

    private HashSet<string> GetOwnedPrintLookupCache()
    {
        if (_ownedPrintLookupCache != null)
        {
            return _ownedPrintLookupCache;
        }

        _ownedPrintLookupCache = BuildOwnedPrintIdSetSnapshot();
        StartupPerfTrace.RecordOwnedPrintLookupCacheBuild(_ownedPrintLookupCache.Count);
        return _ownedPrintLookupCache;
    }

    private HashSet<string> GetUnlockedCardIdLookupCache()
    {
        if (_unlockedCardIdLookupCache != null)
        {
            return _unlockedCardIdLookupCache;
        }

        _unlockedCardIdLookupCache = BuildUnlockedCardIdSetSnapshot();
        return _unlockedCardIdLookupCache;
    }
}
