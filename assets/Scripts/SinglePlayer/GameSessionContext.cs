using System;
using System.Collections.Generic;
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
    public bool ReturnToStoryModeAfterBattle { get; private set; }
    public string ReturnStoryActId { get; private set; } = string.Empty;
    public string ReturnStoryWorldId { get; private set; } = string.Empty;
    public bool ReturnToDuelistBoardAfterBattle { get; private set; }
    public string ReturnBoardActId { get; private set; } = string.Empty;
    public string ReturnBoardWorldId { get; private set; } = string.Empty;
    public string PendingStorySceneId { get; private set; } = string.Empty;

    private readonly List<string> _pendingStoryRewardLines = new List<string>();

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
        bool promoOneTime,
        string storyActId = null,
        string storyWorldId = null,
        string boardActId = null,
        string boardWorldId = null)
    {
        Mode = mode;
        SessionId = Guid.NewGuid().ToString("N");
        ContentId = contentId ?? string.Empty;
        RewardCurrencyOnWin = rewardCurrency;
        RewardPromoCardIdOnWin = promoCardId ?? string.Empty;
        RewardPromoOneTime = promoOneTime;
        RewardsGranted = false;

        if (mode == SessionMode.Story)
        {
            ReturnToStoryModeAfterBattle = true;
            ReturnStoryActId = storyActId ?? string.Empty;
            ReturnStoryWorldId = storyWorldId ?? string.Empty;
            ClearDuelistBoardReturnState();
        }
        else if (mode == SessionMode.DuelistBoard)
        {
            ReturnToDuelistBoardAfterBattle = true;
            ReturnBoardActId = boardActId ?? string.Empty;
            ReturnBoardWorldId = boardWorldId ?? string.Empty;
            ClearStoryReturnState(clearPendingRewardLines: true);
        }
        else
        {
            ClearStoryReturnState(clearPendingRewardLines: true);
            ClearDuelistBoardReturnState();
        }

        _pendingStoryRewardLines.Clear();
        PendingStorySceneId = string.Empty;
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

    public void SetPendingStoryRewardLines(IEnumerable<string> lines)
    {
        _pendingStoryRewardLines.Clear();

        if (lines == null)
        {
            return;
        }

        foreach (string line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                _pendingStoryRewardLines.Add(line.Trim());
            }
        }
    }

    public List<string> ConsumePendingStoryRewardLines()
    {
        List<string> lines = new List<string>(_pendingStoryRewardLines);
        _pendingStoryRewardLines.Clear();
        return lines;
    }

    public void SetPendingStoryScene(string sceneId)
    {
        PendingStorySceneId = sceneId ?? string.Empty;
    }

    public string ConsumePendingStorySceneId()
    {
        string sceneId = PendingStorySceneId;
        PendingStorySceneId = string.Empty;
        return sceneId;
    }

    public void ClearStoryReturnState(bool clearPendingRewardLines = true, bool clearPendingScene = true)
    {
        ReturnToStoryModeAfterBattle = false;
        ReturnStoryActId = string.Empty;
        ReturnStoryWorldId = string.Empty;

        if (clearPendingRewardLines)
        {
            _pendingStoryRewardLines.Clear();
        }

        if (clearPendingScene)
        {
            PendingStorySceneId = string.Empty;
        }
    }

    public void ClearDuelistBoardReturnState()
    {
        ReturnToDuelistBoardAfterBattle = false;
        ReturnBoardActId = string.Empty;
        ReturnBoardWorldId = string.Empty;
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
