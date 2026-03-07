using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ProgressionManager : MonoBehaviour
{
    private const int CurrentSaveVersion = 3;
    private const int NewProfileStartingCurrency = 1000;

    private static ProgressionManager _instance;
    private PlayerProfileData _profile;
    private bool _isLoaded;

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
            DontDestroyOnLoad(runtimeObject);
            _instance = runtimeObject.AddComponent<ProgressionManager>();
            return _instance;
        }
    }

    private string SavePath => Application.persistentDataPath + "/profile.json";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        _ = Instance;
    }

    public void LoadOrCreate()
    {
        if (File.Exists(SavePath))
        {
            try
            {
                string json = File.ReadAllText(SavePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    PlayerProfileData loaded = JsonUtility.FromJson<PlayerProfileData>(json);
                    if (loaded != null)
                    {
                        _profile = loaded;
                        EnsureCollectionsInitialized();
                        bool migrated = MigrateProfileIfNeeded();
                        _isLoaded = true;
                        if (migrated)
                        {
                            Save();
                        }
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ProgressionManager: Failed to load profile at {SavePath}. Creating a new one. {exception.Message}");
            }
        }

        _profile = new PlayerProfileData
        {
            SaveVersion = CurrentSaveVersion,
            Currency = NewProfileStartingCurrency,
        };
        EnsureCollectionsInitialized();
        _isLoaded = true;
        Save();
    }

    public void Save()
    {
        EnsureLoaded();
        EnsureCollectionsInitialized();
        _profile.SaveVersion = CurrentSaveVersion;

        string directory = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(_profile, true);
        File.WriteAllText(SavePath, json);
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
        if (ContainsValue(_profile.UnlockedCardIds, cardId))
        {
            return true;
        }

        return TryReloadProfileFromDisk() && ContainsValue(_profile.UnlockedCardIds, cardId);
    }

    public void UnlockCard(string cardId, bool saveImmediately = true)
    {
        EnsureLoaded();
        if (TryAddUnique(_profile.UnlockedCardIds, cardId))
        {
            if (saveImmediately)
            {
                Save();
            }
        }
    }

    public void UnlockCards(IEnumerable<string> cardIds, bool saveImmediately = true)
    {
        EnsureLoaded();
        if (cardIds == null)
        {
            return;
        }

        bool changed = false;
        foreach (string cardId in cardIds)
        {
            changed |= TryAddUnique(_profile.UnlockedCardIds, cardId);
        }

        if (changed)
        {
            if (saveImmediately)
            {
                Save();
            }
        }
    }

    public HashSet<string> GetUnlockedCardIdSetSnapshot()
    {
        EnsureLoaded();
        return new HashSet<string>(_profile.UnlockedCardIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
    }

    public bool HasPurchasedProduct(string productId)
    {
        EnsureLoaded();
        if (ContainsValue(_profile.PurchasedProductIds, productId))
        {
            return true;
        }

        return TryReloadProfileFromDisk() && ContainsValue(_profile.PurchasedProductIds, productId);
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
        EnsureCollectionsInitialized();
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
            return;
        }

        LoadOrCreate();
    }

    private bool TryReloadProfileFromDisk()
    {
        try
        {
            if (!File.Exists(SavePath))
            {
                return false;
            }

            string json = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            PlayerProfileData loaded = JsonUtility.FromJson<PlayerProfileData>(json);
            if (loaded == null)
            {
                return false;
            }

            _profile = loaded;
            EnsureCollectionsInitialized();
            _isLoaded = true;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"ProgressionManager: Failed to reload profile at {SavePath}. {exception.Message}");
            return false;
        }
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
        _profile.PurchasedProductIds ??= new List<string>();
        _profile.CompletedStoryNodeIds ??= new List<string>();
        _profile.EarnedStoryKeyIds ??= new List<string>();
        _profile.CompletedDuelBoardIds ??= new List<string>();
        _profile.ClaimedPromoCardIds ??= new List<string>();

        NormalizeValues(_profile.UnlockedCardIds);
        NormalizeValues(_profile.PurchasedProductIds);
        NormalizeValues(_profile.CompletedStoryNodeIds);
        NormalizeValues(_profile.EarnedStoryKeyIds);
        NormalizeValues(_profile.CompletedDuelBoardIds);
        NormalizeValues(_profile.ClaimedPromoCardIds);
    }

    private bool MigrateProfileIfNeeded()
    {
        EnsureCollectionsInitialized();

        if (_profile.SaveVersion >= CurrentSaveVersion)
        {
            return false;
        }

        _profile.SaveVersion = CurrentSaveVersion;
        return true;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadOrCreate();
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
}
