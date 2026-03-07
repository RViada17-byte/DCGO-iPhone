using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DuelistBoardPanel : MonoBehaviour
{
    private enum DuelBoardPage
    {
        None = 0,
        Acts = 1,
        Worlds = 2,
        Duels = 3,
        PlaceholderWorld = 4,
    }

    private const string RuntimeRootName = "DuelBoardRuntimeBody";
    private const string CurrencyTextName = "CurrencyText";
    private const string BreadcrumbTextName = "BreadcrumbText";
    private const string InfoTextName = "InfoText";
    private const string ScrollRootName = "ScrollRoot";
    private const string TitleTextName = "TitleText";
    private const string BackButtonName = "BackButton";

    [Header("Runtime UI Refs")]
    [SerializeField] private Text CurrencyText;
    [SerializeField] private Text BreadcrumbText;
    [SerializeField] private Text InfoText;
    [SerializeField] private ScrollRect DuelBoardScrollRect;
    [SerializeField] private RectTransform DuelBoardContent;

    private readonly List<Button> _entryButtons = new List<Button>();
    private DuelBoardPage _currentPage = DuelBoardPage.None;
    private string _selectedActId = string.Empty;
    private string _selectedWorldId = string.Empty;
    private bool _runtimeUiBuilt;
    private static Font _runtimeFont;

    private void OnEnable()
    {
        OpenDuelistBoard(resetToActsIfNeeded: true);
    }

    public void RefreshAfterBattleReturn()
    {
        OpenDuelistBoard(resetToActsIfNeeded: true);
    }

    public void RefreshCurrency()
    {
        if (CurrencyText == null)
        {
            return;
        }

        CurrencyText.text = "$ " + ProgressionManager.Instance.GetCurrency();
    }

    public GameObject GetPreferredSelectionTarget()
    {
        for (int index = 0; index < _entryButtons.Count; index++)
        {
            Button button = _entryButtons[index];
            if (button != null && button.gameObject.activeInHierarchy && button.interactable)
            {
                return button.gameObject;
            }
        }

        return null;
    }

    private void OpenDuelistBoard(bool resetToActsIfNeeded)
    {
        ProgressionManager.Instance.LoadOrCreate();
        DuelBoardDatabase.Instance.Reload();
        EnsureRuntimeUi();
        RefreshCurrency();
        SetTitleText("DUELIST BOARD");

        if (!TryRestoreBoardReturnContext() && (resetToActsIfNeeded || _currentPage == DuelBoardPage.None))
        {
            _currentPage = DuelBoardPage.Acts;
            _selectedActId = string.Empty;
            _selectedWorldId = string.Empty;
        }

        RenderCurrentPage();
    }

    private bool TryRestoreBoardReturnContext()
    {
        GameSessionContext session = GameSessionContext.Instance;
        if (session == null || !session.ReturnToDuelistBoardAfterBattle)
        {
            return false;
        }

        DuelBoardActDef act = DuelBoardDatabase.Instance.GetAct(session.ReturnBoardActId);
        DuelBoardWorldDef world = DuelBoardDatabase.Instance.GetWorld(session.ReturnBoardWorldId);
        session.ClearDuelistBoardReturnState();

        if (act != null && IsActUnlocked(act))
        {
            _selectedActId = act.id;

            if (world != null && string.Equals(world.parentActId, act.id, StringComparison.Ordinal) && IsWorldUnlocked(world))
            {
                _selectedWorldId = world.id;
                _currentPage = world.isAuthored ? DuelBoardPage.Duels : DuelBoardPage.PlaceholderWorld;
                return true;
            }

            _selectedWorldId = string.Empty;
            _currentPage = DuelBoardPage.Worlds;
            return true;
        }

        _currentPage = DuelBoardPage.Acts;
        _selectedActId = string.Empty;
        _selectedWorldId = string.Empty;
        return true;
    }

    private void RenderCurrentPage()
    {
        ClearEntries();

        switch (_currentPage)
        {
            case DuelBoardPage.Worlds:
                RenderWorldPage();
                break;

            case DuelBoardPage.Duels:
                RenderDuelPage();
                break;

            case DuelBoardPage.PlaceholderWorld:
                RenderPlaceholderWorldPage();
                break;

            default:
                RenderActPage();
                break;
        }

        UpdateBackButton();
        ResetScrollPosition();
    }

    private void RenderActPage()
    {
        _currentPage = DuelBoardPage.Acts;
        _selectedActId = string.Empty;
        _selectedWorldId = string.Empty;

        if (BreadcrumbText != null)
        {
            BreadcrumbText.text = "Select an act.";
        }

        SetInfoText("World unlocks follow Story Mode progression. Board clears only unlock later duelists within the same world.");

        IReadOnlyList<DuelBoardActDef> acts = DuelBoardDatabase.Instance.Acts;
        if (acts == null || acts.Count == 0)
        {
            SetInfoText("No duelist board acts found.");
            return;
        }

        for (int index = 0; index < acts.Count; index++)
        {
            DuelBoardActDef act = acts[index];
            if (act == null)
            {
                continue;
            }

            bool unlocked = IsActUnlocked(act);
            string subtitle = unlocked
                ? $"{Mathf.Max(0, act.WorldCount)} world{(act.WorldCount == 1 ? string.Empty : "s")}"
                : "Locked";

            AddEntryButton(
                objectName: $"DuelBoardActButton_{act.id}",
                title: SafeTitle(act.title, act.id),
                subtitle: subtitle,
                interactable: unlocked,
                color: unlocked ? new Color(0.14f, 0.31f, 0.45f, 0.96f) : new Color(0.2f, 0.2f, 0.24f, 0.9f),
                onClick: () => OpenWorldPage(act.id));
        }
    }

    private void OpenWorldPage(string actId)
    {
        DuelBoardActDef act = DuelBoardDatabase.Instance.GetAct(actId);
        if (act == null || !IsActUnlocked(act))
        {
            SetInfoText("That act is still locked.");
            return;
        }

        _currentPage = DuelBoardPage.Worlds;
        _selectedActId = act.id;
        _selectedWorldId = string.Empty;
        RenderCurrentPage();
    }

    private void RenderWorldPage()
    {
        DuelBoardActDef act = DuelBoardDatabase.Instance.GetAct(_selectedActId);
        if (act == null)
        {
            RenderActPage();
            return;
        }

        if (BreadcrumbText != null)
        {
            BreadcrumbText.text = SafeTitle(act.title, act.id);
        }

        SetInfoText("World access mirrors Story Mode. You can enter newly unlocked worlds even if earlier Board worlds are unfinished.");

        if (act.worlds == null || act.worlds.Length == 0)
        {
            SetInfoText("No worlds authored for this act.");
            return;
        }

        for (int index = 0; index < act.worlds.Length; index++)
        {
            DuelBoardWorldDef world = act.worlds[index];
            if (world == null)
            {
                continue;
            }

            bool unlocked = IsWorldUnlocked(world);
            bool cleared = IsWorldCleared(world);
            string title = unlocked ? SafeTitle(world.title, world.id) : "????";
            string subtitle;
            if (!unlocked)
            {
                subtitle = "Locked";
            }
            else if (!world.isAuthored)
            {
                subtitle = "Coming Soon";
            }
            else if (cleared)
            {
                subtitle = $"{world.DuelCount} duel{(world.DuelCount == 1 ? string.Empty : "s")} • Cleared";
            }
            else
            {
                subtitle = $"{world.DuelCount} duel{(world.DuelCount == 1 ? string.Empty : "s")}";
            }

            Color color = !unlocked
                ? new Color(0.2f, 0.2f, 0.24f, 0.9f)
                : world.isAuthored
                    ? new Color(0.18f, 0.27f, 0.36f, 0.96f)
                    : new Color(0.29f, 0.25f, 0.14f, 0.96f);

            AddEntryButton(
                objectName: $"DuelBoardWorldButton_{world.id}",
                title: title,
                subtitle: subtitle,
                interactable: unlocked,
                color: color,
                onClick: () => OpenWorld(world));
        }
    }

    private void OpenWorld(DuelBoardWorldDef world)
    {
        if (world == null || !IsWorldUnlocked(world))
        {
            SetInfoText("That world is still locked.");
            return;
        }

        _selectedActId = world.parentActId ?? string.Empty;
        _selectedWorldId = world.id;
        _currentPage = world.isAuthored ? DuelBoardPage.Duels : DuelBoardPage.PlaceholderWorld;
        RenderCurrentPage();
    }

    private void RenderDuelPage()
    {
        DuelBoardWorldDef world = DuelBoardDatabase.Instance.GetWorld(_selectedWorldId);
        DuelBoardActDef act = DuelBoardDatabase.Instance.GetAct(_selectedActId);
        if (world == null || act == null)
        {
            RenderActPage();
            return;
        }

        if (BreadcrumbText != null)
        {
            BreadcrumbText.text = $"{SafeTitle(act.title, act.id)} / {SafeTitle(world.title, world.id)}";
        }

        SetInfoText(world.DuelCount == 0
            ? "No duelists authored for this world."
            : "Clear the current duelist to reveal the next one. World access still comes from Story Mode.");

        if (world.duels == null || world.duels.Length == 0)
        {
            return;
        }

        for (int index = 0; index < world.duels.Length; index++)
        {
            DuelBoardDuelDef duel = world.duels[index];
            if (duel == null)
            {
                continue;
            }

            bool completed = ProgressionManager.Instance.IsBoardCompleted(duel.id);
            bool unlocked = IsDuelUnlocked(duel);
            bool revealed = completed || unlocked;
            bool playable = unlocked && duel.IsPlayable;

            string title = revealed ? SafeTitle(duel.title, duel.id) : "????";
            string subtitle = BuildDuelSubtitle(duel, completed, unlocked);
            Color color;
            if (!revealed)
            {
                color = new Color(0.2f, 0.2f, 0.24f, 0.9f);
            }
            else if (completed)
            {
                color = new Color(0.16f, 0.37f, 0.24f, 0.96f);
            }
            else if (playable)
            {
                color = new Color(0.18f, 0.32f, 0.45f, 0.96f);
            }
            else
            {
                color = new Color(0.36f, 0.24f, 0.14f, 0.96f);
            }

            AddEntryButton(
                objectName: $"DuelBoardDuelButton_{duel.id}",
                title: title,
                subtitle: subtitle,
                interactable: revealed,
                color: color,
                onClick: () => OnDuelSelected(duel));
        }
    }

    private void RenderPlaceholderWorldPage()
    {
        DuelBoardWorldDef world = DuelBoardDatabase.Instance.GetWorld(_selectedWorldId);
        DuelBoardActDef act = DuelBoardDatabase.Instance.GetAct(_selectedActId);
        if (world == null || act == null)
        {
            RenderActPage();
            return;
        }

        if (BreadcrumbText != null)
        {
            BreadcrumbText.text = $"{SafeTitle(act.title, act.id)} / {SafeTitle(world.title, world.id)}";
        }

        SetInfoText(string.IsNullOrWhiteSpace(world.placeholderMessage)
            ? "This Duelist Board world has not been authored yet."
            : world.placeholderMessage.Trim());
    }

    private void OnDuelSelected(DuelBoardDuelDef duel)
    {
        if (duel == null)
        {
            return;
        }

        if (!IsDuelUnlocked(duel))
        {
            SetInfoText("That duelist is still locked.");
            return;
        }

        if (!duel.IsPlayable)
        {
            SetInfoText("This duelist is not authored yet.");
            return;
        }

        StartBoardDuel(duel);
    }

    public void StartBoardDuel(DuelBoardDuelDef duel)
    {
        if (duel == null)
        {
            SetInfoText("Invalid board duel.");
            return;
        }

        if (!TryResolveEnemyDeck(duel, out DeckData enemyDeck, out string error))
        {
            SetInfoText(error);
            return;
        }

        if (ContinuousController.instance == null)
        {
            SetInfoText("Game controller is unavailable.");
            return;
        }

        ContinuousController.instance.isAI = true;
        ContinuousController.instance.isRandomMatch = false;
        ContinuousController.instance.EnemyDeckData = enemyDeck;

        GameSessionContext.Instance.StartSession(
            SessionMode.DuelistBoard,
            duel.id,
            duel.rewardCurrency,
            duel.rewardPromoCardId,
            duel.promoOneTime,
            boardActId: duel.parentActId,
            boardWorldId: duel.parentWorldId);

        StartCoroutine(SinglePlayerBattleLoader.LoadBattleSceneAdditiveCoroutine());
    }

    private bool TryResolveEnemyDeck(DuelBoardDuelDef duel, out DeckData enemyDeck, out string error)
    {
        enemyDeck = null;
        error = string.Empty;

        if (duel == null)
        {
            error = "Invalid board duel.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(duel.enemyDeckCode))
        {
            enemyDeck = new DeckData(duel.enemyDeckCode);
            return true;
        }

        if (duel.enemyCardIds != null && duel.enemyCardIds.Length > 0)
        {
            if (ShopService.TryBuildDeckByCardIds(SafeTitle(duel.title, duel.id), duel.enemyCardIds, out enemyDeck, out error))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                error = "This duelist is not authored yet.";
            }

            return false;
        }

        if (!string.IsNullOrWhiteSpace(duel.enemyProductId))
        {
            ShopCatalogDatabase.Instance.Reload();
            if (ShopService.TryBuildStructureDeckByProductId(duel.enemyProductId, out enemyDeck, out error))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                error = "This duelist is not authored yet.";
            }

            return false;
        }

        error = "This duelist is not authored yet.";
        return false;
    }

    private bool IsActUnlocked(DuelBoardActDef act)
    {
        return act != null && AreStoryRequirementsMet(act.prereqStoryNodeIds, act.prereqStoryKeyIds);
    }

    private bool IsWorldUnlocked(DuelBoardWorldDef world)
    {
        if (world == null)
        {
            return false;
        }

        DuelBoardActDef act = DuelBoardDatabase.Instance.GetAct(world.parentActId);
        return act != null &&
               IsActUnlocked(act) &&
               AreStoryRequirementsMet(world.prereqStoryNodeIds, world.prereqStoryKeyIds);
    }

    private bool IsDuelUnlocked(DuelBoardDuelDef duel)
    {
        if (duel == null)
        {
            return false;
        }

        DuelBoardWorldDef world = DuelBoardDatabase.Instance.GetWorld(duel.parentWorldId);
        return world != null &&
               IsWorldUnlocked(world) &&
               AreStoryRequirementsMet(duel.prereqStoryNodeIds, duel.prereqStoryKeyIds) &&
               AreBoardRequirementsMet(duel.prereqBoardDuelIds);
    }

    private bool IsWorldCleared(DuelBoardWorldDef world)
    {
        if (world?.duels == null || world.duels.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < world.duels.Length; index++)
        {
            DuelBoardDuelDef duel = world.duels[index];
            if (duel == null || !ProgressionManager.Instance.IsBoardCompleted(duel.id))
            {
                return false;
            }
        }

        return true;
    }

    private bool AreStoryRequirementsMet(string[] prereqStoryNodeIds, string[] prereqStoryKeyIds)
    {
        if (prereqStoryNodeIds != null)
        {
            for (int index = 0; index < prereqStoryNodeIds.Length; index++)
            {
                string prereqId = prereqStoryNodeIds[index];
                if (string.IsNullOrWhiteSpace(prereqId))
                {
                    continue;
                }

                if (!ProgressionManager.Instance.IsStoryCompleted(prereqId))
                {
                    return false;
                }
            }
        }

        if (prereqStoryKeyIds != null)
        {
            for (int index = 0; index < prereqStoryKeyIds.Length; index++)
            {
                string keyId = prereqStoryKeyIds[index];
                if (string.IsNullOrWhiteSpace(keyId))
                {
                    continue;
                }

                if (!ProgressionManager.Instance.HasStoryKey(keyId))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool AreBoardRequirementsMet(string[] prereqBoardDuelIds)
    {
        if (prereqBoardDuelIds == null)
        {
            return true;
        }

        for (int index = 0; index < prereqBoardDuelIds.Length; index++)
        {
            string prereqId = prereqBoardDuelIds[index];
            if (string.IsNullOrWhiteSpace(prereqId))
            {
                continue;
            }

            if (!ProgressionManager.Instance.IsBoardCompleted(prereqId))
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateBackButton()
    {
        Button backButton = FindBackButton();
        if (backButton == null)
        {
            return;
        }

        backButton.onClick.RemoveAllListeners();

        switch (_currentPage)
        {
            case DuelBoardPage.Worlds:
                backButton.onClick.AddListener(() =>
                {
                    _currentPage = DuelBoardPage.Acts;
                    _selectedActId = string.Empty;
                    _selectedWorldId = string.Empty;
                    RenderCurrentPage();
                });
                SetButtonLabel(backButton, "BACK");
                break;

            case DuelBoardPage.Duels:
            case DuelBoardPage.PlaceholderWorld:
                backButton.onClick.AddListener(() =>
                {
                    _currentPage = DuelBoardPage.Worlds;
                    _selectedWorldId = string.Empty;
                    RenderCurrentPage();
                });
                SetButtonLabel(backButton, "BACK");
                break;

            default:
                MainMenuRouter router = FindRouter();
                if (router != null)
                {
                    backButton.onClick.AddListener(router.BackToHome);
                }
                SetButtonLabel(backButton, "HOME");
                break;
        }

        backButton.interactable = true;
    }

    private void EnsureRuntimeUi()
    {
        if (_runtimeUiBuilt &&
            CurrencyText != null &&
            BreadcrumbText != null &&
            InfoText != null &&
            DuelBoardScrollRect != null &&
            DuelBoardContent != null)
        {
            return;
        }

        RectTransform rootRect = transform as RectTransform;
        if (rootRect == null)
        {
            return;
        }

        RectTransform runtimeRoot = FindOrCreateRectTransform(rootRect, RuntimeRootName);
        runtimeRoot.anchorMin = Vector2.zero;
        runtimeRoot.anchorMax = Vector2.one;
        runtimeRoot.offsetMin = new Vector2(48f, 48f);
        runtimeRoot.offsetMax = new Vector2(-48f, -210f);

        Image rootBackground = runtimeRoot.GetComponent<Image>();
        if (rootBackground == null)
        {
            rootBackground = runtimeRoot.gameObject.AddComponent<Image>();
        }

        rootBackground.color = new Color(0.05f, 0.07f, 0.11f, 0.72f);
        rootBackground.raycastTarget = true;

        CurrencyText = FindOrCreateText(runtimeRoot, CurrencyTextName, 34, TextAnchor.MiddleRight, FontStyle.Bold);
        RectTransform currencyRect = CurrencyText.rectTransform;
        currencyRect.anchorMin = new Vector2(1f, 1f);
        currencyRect.anchorMax = new Vector2(1f, 1f);
        currencyRect.pivot = new Vector2(1f, 1f);
        currencyRect.anchoredPosition = new Vector2(-20f, -24f);
        currencyRect.sizeDelta = new Vector2(260f, 44f);

        BreadcrumbText = FindOrCreateText(runtimeRoot, BreadcrumbTextName, 24, TextAnchor.UpperLeft, FontStyle.Bold);
        RectTransform breadcrumbRect = BreadcrumbText.rectTransform;
        breadcrumbRect.anchorMin = new Vector2(0f, 1f);
        breadcrumbRect.anchorMax = new Vector2(1f, 1f);
        breadcrumbRect.pivot = new Vector2(0f, 1f);
        breadcrumbRect.offsetMin = new Vector2(20f, -58f);
        breadcrumbRect.offsetMax = new Vector2(-300f, -20f);

        InfoText = FindOrCreateText(runtimeRoot, InfoTextName, 20, TextAnchor.UpperLeft, FontStyle.Normal);
        RectTransform infoRect = InfoText.rectTransform;
        infoRect.anchorMin = new Vector2(0f, 1f);
        infoRect.anchorMax = new Vector2(1f, 1f);
        infoRect.pivot = new Vector2(0f, 1f);
        infoRect.offsetMin = new Vector2(20f, -116f);
        infoRect.offsetMax = new Vector2(-20f, -66f);

        RectTransform scrollRoot = FindOrCreateRectTransform(runtimeRoot, ScrollRootName);
        scrollRoot.anchorMin = new Vector2(0f, 0f);
        scrollRoot.anchorMax = new Vector2(1f, 1f);
        scrollRoot.offsetMin = new Vector2(20f, 20f);
        scrollRoot.offsetMax = new Vector2(-20f, -132f);

        Image scrollBackground = scrollRoot.GetComponent<Image>();
        if (scrollBackground == null)
        {
            scrollBackground = scrollRoot.gameObject.AddComponent<Image>();
        }

        scrollBackground.color = new Color(0.09f, 0.11f, 0.17f, 0.88f);

        DuelBoardScrollRect = scrollRoot.GetComponent<ScrollRect>();
        if (DuelBoardScrollRect == null)
        {
            DuelBoardScrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        }

        DuelBoardScrollRect.horizontal = false;
        DuelBoardScrollRect.vertical = true;
        DuelBoardScrollRect.scrollSensitivity = 24f;

        RectTransform viewport = FindOrCreateRectTransform(scrollRoot, "Viewport");
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(8f, 8f);
        viewport.offsetMax = new Vector2(-8f, -8f);

        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage == null)
        {
            viewportImage = viewport.gameObject.AddComponent<Image>();
        }

        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);

        Mask viewportMask = viewport.GetComponent<Mask>();
        if (viewportMask == null)
        {
            viewportMask = viewport.gameObject.AddComponent<Mask>();
        }

        viewportMask.showMaskGraphic = false;

        DuelBoardContent = FindOrCreateRectTransform(viewport, "Content");
        DuelBoardContent.anchorMin = new Vector2(0f, 1f);
        DuelBoardContent.anchorMax = new Vector2(1f, 1f);
        DuelBoardContent.pivot = new Vector2(0.5f, 1f);
        DuelBoardContent.anchoredPosition = Vector2.zero;
        DuelBoardContent.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup grid = DuelBoardContent.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = DuelBoardContent.gameObject.AddComponent<GridLayoutGroup>();
        }

        grid.cellSize = new Vector2(320f, 132f);
        grid.spacing = new Vector2(18f, 18f);
        grid.padding = new RectOffset(18, 18, 18, 18);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;

        ContentSizeFitter contentFitter = DuelBoardContent.GetComponent<ContentSizeFitter>();
        if (contentFitter == null)
        {
            contentFitter = DuelBoardContent.gameObject.AddComponent<ContentSizeFitter>();
        }

        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        DuelBoardScrollRect.viewport = viewport;
        DuelBoardScrollRect.content = DuelBoardContent;
        _runtimeUiBuilt = true;
    }

    private void ClearEntries()
    {
        _entryButtons.Clear();

        if (DuelBoardContent == null)
        {
            return;
        }

        for (int index = DuelBoardContent.childCount - 1; index >= 0; index--)
        {
            Destroy(DuelBoardContent.GetChild(index).gameObject);
        }
    }

    private void AddEntryButton(string objectName, string title, string subtitle, bool interactable, Color color, Action onClick)
    {
        if (DuelBoardContent == null)
        {
            return;
        }

        Button button = CreateButton(DuelBoardContent, objectName, string.Empty);
        button.onClick.RemoveAllListeners();
        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }

        button.interactable = interactable;
        button.gameObject.SetActive(true);

        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.color = color;
        }

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.supportRichText = true;
            label.text = $"{title}\n<size=20>{subtitle}</size>";
        }

        _entryButtons.Add(button);
    }

    private void ResetScrollPosition()
    {
        if (DuelBoardScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            DuelBoardScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void SetInfoText(string message)
    {
        if (InfoText != null)
        {
            InfoText.text = message ?? string.Empty;
        }
    }

    private void SetTitleText(string title)
    {
        Transform titleTransform = FindChildRecursive(transform, TitleTextName);
        if (titleTransform == null)
        {
            return;
        }

        Text titleText = titleTransform.GetComponent<Text>();
        if (titleText != null)
        {
            titleText.text = title;
            return;
        }

        TMPro.TMP_Text tmpText = titleTransform.GetComponent<TMPro.TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = title;
        }
    }

    private Button FindBackButton()
    {
        Transform backTransform = FindChildRecursive(transform, BackButtonName);
        return backTransform != null ? backTransform.GetComponent<Button>() : null;
    }

    private MainMenuRouter FindRouter()
    {
        MainMenuRouter router = GetComponentInParent<MainMenuRouter>();
        if (router != null)
        {
            return router;
        }

        return FindObjectOfType<MainMenuRouter>();
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = label;
            return;
        }

        TMPro.TMP_Text tmpText = button.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (tmpText != null)
        {
            tmpText.text = label;
        }
    }

    private static string BuildDuelSubtitle(DuelBoardDuelDef duel, bool completed, bool unlocked)
    {
        string rewardLabel = duel != null && duel.rewardCurrency > 0
            ? $" • ${duel.rewardCurrency}"
            : string.Empty;

        if (completed)
        {
            return $"Duelist • Cleared{rewardLabel}";
        }

        if (!unlocked)
        {
            return "Locked";
        }

        if (duel != null && !duel.IsPlayable)
        {
            return $"Duelist • Coming Soon{rewardLabel}";
        }

        return $"Duelist • Ready{rewardLabel}";
    }

    private static string SafeTitle(string title, string fallback)
    {
        return string.IsNullOrWhiteSpace(title) ? fallback : title.Trim();
    }

    private static Font GetRuntimeFont()
    {
        if (_runtimeFont == null)
        {
            _runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return _runtimeFont;
    }

    private static RectTransform FindOrCreateRectTransform(Transform parent, string name)
    {
        Transform existing = FindChildRecursive(parent, name);
        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        return rectTransform;
    }

    private static Text FindOrCreateText(Transform parent, string name, int fontSize, TextAnchor alignment, FontStyle fontStyle)
    {
        Transform existing = FindChildRecursive(parent, name);
        Text text = existing != null ? existing.GetComponent<Text>() : null;
        if (text == null)
        {
            GameObject textObject = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(Text));
            if (existing == null)
            {
                textObject.transform.SetParent(parent, false);
            }

            text = textObject.GetComponent<Text>();
            if (text == null)
            {
                text = textObject.AddComponent<Text>();
            }
        }

        text.name = name;
        text.font = GetRuntimeFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        return ConfigureButton(buttonObject.GetComponent<Button>(), label);
    }

    private static Button ConfigureButton(Button button, string label)
    {
        if (button == null)
        {
            return null;
        }

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            buttonRect.localScale = Vector3.one;
            buttonRect.sizeDelta = new Vector2(320f, 132f);
        }

        LayoutElement layoutElement = button.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = button.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.preferredWidth = 320f;
        layoutElement.preferredHeight = 132f;

        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = new Color(0.18f, 0.32f, 0.45f, 0.96f);
            buttonImage.raycastTarget = true;
        }

        Text buttonText = button.GetComponentInChildren<Text>(true);
        if (buttonText == null)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(button.transform, false);
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(14f, 14f);
            textRect.offsetMax = new Vector2(-14f, -14f);
            buttonText = textObject.GetComponent<Text>();
        }

        buttonText.font = GetRuntimeFont();
        buttonText.fontSize = 28;
        buttonText.fontStyle = FontStyle.Bold;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;
        buttonText.supportRichText = true;
        buttonText.raycastTarget = false;
        buttonText.text = label;
        buttonText.horizontalOverflow = HorizontalWrapMode.Wrap;
        buttonText.verticalOverflow = VerticalWrapMode.Overflow;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        button.colors = colors;

        return button;
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (string.Equals(parent.name, name, StringComparison.Ordinal))
        {
            return parent;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            Transform found = FindChildRecursive(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
