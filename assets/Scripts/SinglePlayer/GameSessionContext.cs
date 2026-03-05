using System;
using UnityEngine;

public enum SessionMode
{
    None,
    Story,
    DuelistBoard,
}

public class GameSessionContext : MonoBehaviour
{
    private static GameSessionContext _instance;

    public static GameSessionContext Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindObjectOfType<GameSessionContext>();
            if (_instance != null)
            {
                return _instance;
            }

            GameObject runtimeObject = new GameObject(nameof(GameSessionContext));
            DontDestroyOnLoad(runtimeObject);
            _instance = runtimeObject.AddComponent<GameSessionContext>();
            return _instance;
        }
    }

    public SessionMode Mode = SessionMode.None;
    public string SessionId = string.Empty;
    public string ContentId = string.Empty;
    public int RewardCurrencyOnWin;
    public string RewardPromoCardIdOnWin = string.Empty;
    public bool RewardPromoOneTime = true;
    public bool RewardsGranted { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        _ = Instance;
    }

    public void StartSession(
        SessionMode mode,
        string contentId,
        int rewardCurrency,
        string promoCardId,
        bool promoOneTime)
    {
        Mode = mode;
        SessionId = Guid.NewGuid().ToString("N");
        ContentId = contentId ?? string.Empty;
        RewardCurrencyOnWin = rewardCurrency;
        RewardPromoCardIdOnWin = promoCardId ?? string.Empty;
        RewardPromoOneTime = promoOneTime;
        RewardsGranted = false;
    }

    public void ClearSession()
    {
        Mode = SessionMode.None;
        SessionId = string.Empty;
        ContentId = string.Empty;
        RewardCurrencyOnWin = 0;
        RewardPromoCardIdOnWin = string.Empty;
        RewardPromoOneTime = true;
        RewardsGranted = false;
    }

    public void MarkRewardsGranted()
    {
        RewardsGranted = true;
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
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
