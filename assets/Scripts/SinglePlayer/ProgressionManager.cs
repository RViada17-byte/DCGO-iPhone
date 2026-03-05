using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ProgressionManager : MonoBehaviour
{
    private const int NewProfileStartingCurrency = 10000;

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
                        _isLoaded = true;
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

    public void AddCurrency(int amount)
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

        Save();
    }

    public bool TrySpendCurrency(int amount)
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
        Save();
        return true;
    }

    public bool IsCardUnlocked(string cardId)
    {
        EnsureLoaded();
        return ContainsValue(_profile.UnlockedCardIds, cardId);
    }

    public void UnlockCard(string cardId)
    {
        EnsureLoaded();
        if (TryAddUnique(_profile.UnlockedCardIds, cardId))
        {
            Save();
        }
    }

    public void UnlockCards(IEnumerable<string> cardIds)
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
            Save();
        }
    }

    public bool IsStoryCompleted(string nodeId)
    {
        EnsureLoaded();
        return ContainsValue(_profile.CompletedStoryNodeIds, nodeId);
    }

    public void MarkStoryCompleted(string nodeId)
    {
        EnsureLoaded();
        if (TryAddUnique(_profile.CompletedStoryNodeIds, nodeId))
        {
            Save();
        }
    }

    public bool IsBoardCompleted(string duelId)
    {
        EnsureLoaded();
        return ContainsValue(_profile.CompletedDuelBoardIds, duelId);
    }

    public void MarkBoardCompleted(string duelId)
    {
        EnsureLoaded();
        if (TryAddUnique(_profile.CompletedDuelBoardIds, duelId))
        {
            Save();
        }
    }

    public bool HasClaimedPromo(string cardId)
    {
        EnsureLoaded();
        return ContainsValue(_profile.ClaimedPromoCardIds, cardId);
    }

    public void MarkPromoClaimed(string cardId)
    {
        EnsureLoaded();
        if (TryAddUnique(_profile.ClaimedPromoCardIds, cardId))
        {
            Save();
        }
    }

    public void ResetProfileForDev()
    {
        _profile = new PlayerProfileData
        {
            Currency = NewProfileStartingCurrency,
        };
        EnsureCollectionsInitialized();
        _isLoaded = true;
        Save();
    }

    private static bool ContainsValue(List<string> values, string value)
    {
        if (values == null || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return values.Contains(value);
    }

    private static bool TryAddUnique(List<string> values, string value)
    {
        if (values == null || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (values.Contains(value))
        {
            return false;
        }

        values.Add(value);
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

    private void EnsureCollectionsInitialized()
    {
        if (_profile == null)
        {
            _profile = new PlayerProfileData();
        }

        _profile.UnlockedCardIds ??= new List<string>();
        _profile.CompletedStoryNodeIds ??= new List<string>();
        _profile.CompletedDuelBoardIds ??= new List<string>();
        _profile.ClaimedPromoCardIds ??= new List<string>();
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
}
