using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StoryPanel : MonoBehaviour
{
    private enum StoryPage
    {
        None = 0,
        Acts = 1,
        Worlds = 2,
        Encounters = 3,
        PlaceholderWorld = 4,
    }

    private const string RuntimeRootName = "StoryRuntimeBody";
    private const string CurrencyTextName = "CurrencyText";
    private const string BreadcrumbTextName = "BreadcrumbText";
    private const string InfoTextName = "InfoText";
    private const string ScrollRootName = "ScrollRoot";
    private const string PopupRootName = "UnlockPopup";
    private const string PopupTextName = "PopupText";
    private const string PopupButtonName = "PopupCloseButton";
    private const string DialogueRootName = "DialogueOverlay";
    private const string DialogueBoxName = "DialogueBox";
    private const string DialoguePortraitName = "DialoguePortrait";
    private const string DialogueSpeakerName = "DialogueSpeaker";
    private const string DialogueBodyName = "DialogueBody";
    private const string DialogueAdvanceButtonName = "DialogueAdvanceButton";
    private const string TitleTextName = "TitleText";
    private const string BackButtonName = "BackButton";

    [Header("Runtime UI Refs")]
    [SerializeField] private Text CurrencyText;
    [SerializeField] private Text BreadcrumbText;
    [SerializeField] private Text InfoText;
    [SerializeField] private ScrollRect StoryScrollRect;
    [SerializeField] private RectTransform StoryContent;
    [SerializeField] private GameObject PopupRoot;
    [SerializeField] private Text PopupText;
    [SerializeField] private Button PopupCloseButton;
    [SerializeField] private GameObject DialogueRoot;
    [SerializeField] private Image DialoguePortraitImage;
    [SerializeField] private Text DialogueSpeakerText;
    [SerializeField] private Text DialogueBodyText;
    [SerializeField] private Button DialogueAdvanceButton;

    private readonly List<Button> _entryButtons = new List<Button>();
    private readonly Dictionary<string, Sprite> _portraitCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private StoryPage _currentPage = StoryPage.None;
    private string _selectedActId = string.Empty;
    private string _selectedWorldId = string.Empty;
    private StorySceneDef _activeScene;
    private int _activeSceneLineIndex = -1;
    private StoryEncounterDef _pendingEncounterAfterDialogue;
    private bool _launchBattleAfterDialogue;
    private bool _runtimeUiBuilt;
    private static Font _runtimeFont;

    private void OnEnable()
    {
        OpenStoryMode(resetToActsIfNeeded: true);
    }

    public void RefreshAfterBattleReturn()
    {
        OpenStoryMode(resetToActsIfNeeded: true);
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
        if (DialogueRoot != null && DialogueRoot.activeSelf && DialogueAdvanceButton != null && DialogueAdvanceButton.gameObject.activeInHierarchy)
        {
            return DialogueAdvanceButton.gameObject;
        }

        if (PopupRoot != null && PopupRoot.activeSelf && PopupCloseButton != null && PopupCloseButton.gameObject.activeInHierarchy)
        {
            return PopupCloseButton.gameObject;
        }

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

    private void OpenStoryMode(bool resetToActsIfNeeded)
    {
        ProgressionManager.Instance.LoadOrCreate();
        StoryDatabase.Instance.Reload();
        EnsureRuntimeUi();
        RefreshCurrency();
        SetTitleText("STORY MODE");

        if (!TryRestoreStoryReturnContext() && (resetToActsIfNeeded || _currentPage == StoryPage.None))
        {
            _currentPage = StoryPage.Acts;
            _selectedActId = string.Empty;
            _selectedWorldId = string.Empty;
        }

        RenderCurrentPage();
        if (!TryOpenPendingStoryScene())
        {
            ShowPendingRewardPopupIfAny();
        }
    }

    private bool TryRestoreStoryReturnContext()
    {
        GameSessionContext session = GameSessionContext.Instance;
        if (session == null || !session.ReturnToStoryModeAfterBattle)
        {
            return false;
        }

        StoryActDef act = StoryDatabase.Instance.GetAct(session.ReturnStoryActId);
        StoryWorldDef world = StoryDatabase.Instance.GetWorld(session.ReturnStoryWorldId);
        session.ClearStoryReturnState(clearPendingRewardLines: false, clearPendingScene: false);

        if (act != null && IsActUnlocked(act))
        {
            _selectedActId = act.id;

            if (world != null && string.Equals(world.parentActId, act.id, StringComparison.Ordinal) && IsWorldUnlocked(world))
            {
                _selectedWorldId = world.id;
                _currentPage = world.isAuthored ? StoryPage.Encounters : StoryPage.PlaceholderWorld;
                return true;
            }

            _selectedWorldId = string.Empty;
            _currentPage = StoryPage.Worlds;
            return true;
        }

        _currentPage = StoryPage.Acts;
        _selectedActId = string.Empty;
        _selectedWorldId = string.Empty;
        return true;
    }

    private void RenderCurrentPage()
    {
        ClearEntries();

        switch (_currentPage)
        {
            case StoryPage.Worlds:
                RenderWorldPage();
                break;

            case StoryPage.Encounters:
                RenderEncounterPage();
                break;

            case StoryPage.PlaceholderWorld:
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
        _currentPage = StoryPage.Acts;
        _selectedActId = string.Empty;
        _selectedWorldId = string.Empty;

        BreadcrumbText.text = "Select an act.";
        SetInfoText("Acts 2 and 3 unlock after clearing the prior act.");

        IReadOnlyList<StoryActDef> acts = StoryDatabase.Instance.Acts;
        if (acts == null || acts.Count == 0)
        {
            SetInfoText("No story acts found.");
            return;
        }

        for (int index = 0; index < acts.Count; index++)
        {
            StoryActDef act = acts[index];
            if (act == null)
            {
                continue;
            }

            bool unlocked = IsActUnlocked(act);
            string subtitle = unlocked
                ? $"{Mathf.Max(0, act.WorldCount)} world{(act.WorldCount == 1 ? string.Empty : "s")}"
                : "Locked";

            AddEntryButton(
                objectName: $"ActButton_{act.id}",
                title: string.IsNullOrWhiteSpace(act.title) ? act.id : act.title,
                subtitle: subtitle,
                interactable: unlocked,
                color: unlocked ? new Color(0.14f, 0.31f, 0.45f, 0.96f) : new Color(0.2f, 0.2f, 0.24f, 0.9f),
                onClick: () => OpenWorldPage(act.id));
        }
    }

    private void OpenWorldPage(string actId)
    {
        StoryActDef act = StoryDatabase.Instance.GetAct(actId);
        if (act == null || !IsActUnlocked(act))
        {
            SetInfoText("That act is still locked.");
            return;
        }

        _currentPage = StoryPage.Worlds;
        _selectedActId = act.id;
        _selectedWorldId = string.Empty;
        RenderCurrentPage();
    }

    private void RenderWorldPage()
    {
        StoryActDef act = StoryDatabase.Instance.GetAct(_selectedActId);
        if (act == null)
        {
            RenderActPage();
            return;
        }

        BreadcrumbText.text = string.IsNullOrWhiteSpace(act.title) ? act.id : act.title;
        SetInfoText("Locked worlds stay hidden until unlocked. Placeholder worlds open as unavailable.");

        if (act.worlds == null || act.worlds.Length == 0)
        {
            SetInfoText("No worlds authored for this act.");
            return;
        }

        for (int index = 0; index < act.worlds.Length; index++)
        {
            StoryWorldDef world = act.worlds[index];
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
                subtitle = $"{world.EncounterCount} fights • Cleared";
            }
            else
            {
                subtitle = $"{world.EncounterCount} fights";
            }

            Color color = !unlocked
                ? new Color(0.2f, 0.2f, 0.24f, 0.9f)
                : world.isAuthored
                    ? new Color(0.18f, 0.27f, 0.36f, 0.96f)
                    : new Color(0.29f, 0.25f, 0.14f, 0.96f);

            AddEntryButton(
                objectName: $"WorldButton_{world.id}",
                title: title,
                subtitle: subtitle,
                interactable: unlocked,
                color: color,
                onClick: () => OpenWorld(world));
        }
    }

    private void OpenWorld(StoryWorldDef world)
    {
        if (world == null || !IsWorldUnlocked(world))
        {
            SetInfoText("That world is still locked.");
            return;
        }

        _selectedActId = world.parentActId ?? string.Empty;
        _selectedWorldId = world.id;
        _currentPage = world.isAuthored ? StoryPage.Encounters : StoryPage.PlaceholderWorld;
        RenderCurrentPage();
    }

    private void RenderEncounterPage()
    {
        StoryWorldDef world = StoryDatabase.Instance.GetWorld(_selectedWorldId);
        StoryActDef act = StoryDatabase.Instance.GetAct(_selectedActId);
        if (world == null || act == null)
        {
            RenderActPage();
            return;
        }

        BreadcrumbText.text = $"{SafeTitle(act.title, act.id)} / {SafeTitle(world.title, world.id)}";
        SetInfoText(world.EncounterCount == 0
            ? "No encounters authored for this world."
            : "Defeat the current encounter to reveal the next one.");

        if (world.encounters == null || world.encounters.Length == 0)
        {
            return;
        }

        for (int index = 0; index < world.encounters.Length; index++)
        {
            StoryEncounterDef encounter = world.encounters[index];
            if (encounter == null)
            {
                continue;
            }

            bool completed = ProgressionManager.Instance.IsStoryCompleted(encounter.id);
            bool unlocked = IsEncounterUnlocked(encounter);
            bool revealed = completed || unlocked;
            bool playable = unlocked && encounter.IsPlayable;

            string title = revealed ? SafeTitle(encounter.title, encounter.id) : "????";
            string subtitle = BuildEncounterSubtitle(encounter, completed, unlocked);
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
                objectName: $"EncounterButton_{encounter.id}",
                title: title,
                subtitle: subtitle,
                interactable: revealed,
                color: color,
                onClick: () => OnEncounterSelected(encounter));
        }
    }

    private void RenderPlaceholderWorldPage()
    {
        StoryWorldDef world = StoryDatabase.Instance.GetWorld(_selectedWorldId);
        StoryActDef act = StoryDatabase.Instance.GetAct(_selectedActId);
        if (world == null || act == null)
        {
            RenderActPage();
            return;
        }

        BreadcrumbText.text = $"{SafeTitle(act.title, act.id)} / {SafeTitle(world.title, world.id)}";
        SetInfoText(string.IsNullOrWhiteSpace(world.placeholderMessage)
            ? "This world has not been authored yet."
            : world.placeholderMessage.Trim());
    }

    private void OnEncounterSelected(StoryEncounterDef encounter)
    {
        if (encounter == null)
        {
            return;
        }

        if (!IsEncounterUnlocked(encounter))
        {
            SetInfoText("That encounter is still locked.");
            return;
        }

        if (!encounter.IsPlayable)
        {
            SetInfoText("This duel is not authored yet.");
            return;
        }

        if (TryOpenScene(encounter.preDuelSceneId, encounter, launchBattleOnComplete: true))
        {
            return;
        }

        StartStoryDuel(encounter);
    }

    public void StartStoryDuel(StoryEncounterDef encounter)
    {
        if (encounter == null)
        {
            SetInfoText("Invalid encounter.");
            return;
        }

        if (!TryResolveEnemyDeck(encounter, out DeckData enemyDeck, out string error))
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
            SessionMode.Story,
            encounter.id,
            encounter.rewardCurrency,
            encounter.rewardPromoCardId,
            promoOneTime: true,
            storyActId: encounter.parentActId,
            storyWorldId: encounter.parentWorldId);

        StartCoroutine(SinglePlayerBattleLoader.LoadBattleSceneAdditiveCoroutine());
    }

    private bool TryResolveEnemyDeck(StoryEncounterDef encounter, out DeckData enemyDeck, out string error)
    {
        enemyDeck = null;
        error = string.Empty;

        if (encounter == null)
        {
            error = "Invalid encounter.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(encounter.enemyDeckCode))
        {
            enemyDeck = new DeckData(encounter.enemyDeckCode);
            return true;
        }

        if (encounter.enemyCardIds != null && encounter.enemyCardIds.Length > 0)
        {
            if (ShopService.TryBuildDeckByCardIds(SafeTitle(encounter.title, encounter.id), encounter.enemyCardIds, out enemyDeck, out error))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                error = "This duel is not authored yet.";
            }

            return false;
        }

        if (!string.IsNullOrWhiteSpace(encounter.enemyProductId))
        {
            ShopCatalogDatabase.Instance.Reload();
            if (ShopService.TryBuildStructureDeckByProductId(encounter.enemyProductId, out enemyDeck, out error))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                error = "This duel is not authored yet.";
            }

            return false;
        }

        error = "This duel is not authored yet.";
        return false;
    }

    private bool IsActUnlocked(StoryActDef act)
    {
        return act != null && AreRequirementsMet(act.prereqEncounterIds, act.prereqKeyIds);
    }

    private bool IsWorldUnlocked(StoryWorldDef world)
    {
        if (world == null)
        {
            return false;
        }

        StoryActDef act = StoryDatabase.Instance.GetAct(world.parentActId);
        return act != null &&
               IsActUnlocked(act) &&
               AreRequirementsMet(world.prereqEncounterIds, world.prereqKeyIds);
    }

    private bool IsEncounterUnlocked(StoryEncounterDef encounter)
    {
        if (encounter == null)
        {
            return false;
        }

        StoryWorldDef world = StoryDatabase.Instance.GetWorld(encounter.parentWorldId);
        return world != null &&
               IsWorldUnlocked(world) &&
               AreRequirementsMet(encounter.prereqEncounterIds, null);
    }

    private bool IsWorldCleared(StoryWorldDef world)
    {
        if (world?.encounters == null || world.encounters.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < world.encounters.Length; index++)
        {
            StoryEncounterDef encounter = world.encounters[index];
            if (encounter == null || !ProgressionManager.Instance.IsStoryCompleted(encounter.id))
            {
                return false;
            }
        }

        return true;
    }

    private bool AreRequirementsMet(string[] prereqEncounterIds, string[] prereqKeyIds)
    {
        if (prereqEncounterIds != null)
        {
            for (int index = 0; index < prereqEncounterIds.Length; index++)
            {
                string prereqId = prereqEncounterIds[index];
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

        if (prereqKeyIds != null)
        {
            for (int index = 0; index < prereqKeyIds.Length; index++)
            {
                string keyId = prereqKeyIds[index];
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

    private void ShowPendingRewardPopupIfAny()
    {
        if (DialogueRoot != null && DialogueRoot.activeSelf)
        {
            return;
        }

        if (PopupRoot == null || PopupText == null)
        {
            return;
        }

        List<string> lines = GameSessionContext.Instance.ConsumePendingStoryRewardLines();
        if (lines == null || lines.Count == 0)
        {
            PopupRoot.SetActive(false);
            return;
        }

        PopupText.text = string.Join("\n", lines);
        PopupRoot.SetActive(true);
    }

    private bool TryOpenPendingStoryScene()
    {
        GameSessionContext session = GameSessionContext.Instance;
        if (session == null)
        {
            return false;
        }

        string sceneId = session.ConsumePendingStorySceneId();
        if (string.IsNullOrWhiteSpace(sceneId))
        {
            return false;
        }

        return TryOpenScene(sceneId, null, launchBattleOnComplete: false);
    }

    private bool TryOpenScene(string sceneId, StoryEncounterDef encounterAfterScene, bool launchBattleOnComplete)
    {
        if (string.IsNullOrWhiteSpace(sceneId))
        {
            return false;
        }

        StorySceneDef scene = StoryDatabase.Instance.GetScene(sceneId);
        if (scene == null || scene.lines == null || scene.lines.Length == 0)
        {
            return false;
        }

        _activeScene = scene;
        _activeSceneLineIndex = 0;
        _pendingEncounterAfterDialogue = encounterAfterScene;
        _launchBattleAfterDialogue = launchBattleOnComplete;

        ShowDialogueLine();
        return true;
    }

    private void ShowDialogueLine()
    {
        if (_activeScene == null || _activeScene.lines == null || _activeScene.lines.Length == 0)
        {
            CompleteDialogueScene();
            return;
        }

        if (_activeSceneLineIndex < 0 || _activeSceneLineIndex >= _activeScene.lines.Length)
        {
            CompleteDialogueScene();
            return;
        }

        EnsureRuntimeUi();
        if (DialogueRoot == null || DialogueSpeakerText == null || DialogueBodyText == null || DialogueAdvanceButton == null)
        {
            CompleteDialogueScene();
            return;
        }

        StorySceneLineDef line = _activeScene.lines[_activeSceneLineIndex];
        string speaker = ResolveSceneSpeaker(_activeScene, line);
        string portraitId = ResolveScenePortraitId(_activeScene, line);

        DialogueSpeakerText.text = speaker;
        DialogueBodyText.text = line != null ? (line.text ?? string.Empty).Trim() : string.Empty;

        if (DialoguePortraitImage != null)
        {
            Sprite portrait = LoadPortraitSprite(portraitId);
            DialoguePortraitImage.sprite = portrait;
            DialoguePortraitImage.enabled = portrait != null;
        }

        SetButtonLabel(DialogueAdvanceButton, _activeSceneLineIndex >= _activeScene.lines.Length - 1
            ? (_launchBattleAfterDialogue ? "DUEL" : "CONTINUE")
            : "NEXT");

        DialogueRoot.SetActive(true);
        if (PopupRoot != null)
        {
            PopupRoot.SetActive(false);
        }

        UpdateBackButton();
    }

    private void AdvanceDialogueScene()
    {
        if (_activeScene == null)
        {
            return;
        }

        _activeSceneLineIndex++;
        if (_activeSceneLineIndex >= (_activeScene.lines?.Length ?? 0))
        {
            CompleteDialogueScene();
            return;
        }

        ShowDialogueLine();
    }

    private void CompleteDialogueScene()
    {
        StoryEncounterDef encounterToLaunch = _pendingEncounterAfterDialogue;
        bool launchBattle = _launchBattleAfterDialogue;

        _activeScene = null;
        _activeSceneLineIndex = -1;
        _pendingEncounterAfterDialogue = null;
        _launchBattleAfterDialogue = false;

        if (DialogueRoot != null)
        {
            DialogueRoot.SetActive(false);
        }

        UpdateBackButton();

        if (launchBattle && encounterToLaunch != null)
        {
            StartStoryDuel(encounterToLaunch);
            return;
        }

        ShowPendingRewardPopupIfAny();
    }

    private static string ResolveSceneSpeaker(StorySceneDef scene, StorySceneLineDef line)
    {
        if (line != null && !string.IsNullOrWhiteSpace(line.speaker))
        {
            return line.speaker.Trim();
        }

        if (scene != null && !string.IsNullOrWhiteSpace(scene.speaker))
        {
            return scene.speaker.Trim();
        }

        return string.Empty;
    }

    private static string ResolveScenePortraitId(StorySceneDef scene, StorySceneLineDef line)
    {
        if (line != null && !string.IsNullOrWhiteSpace(line.portraitId))
        {
            return line.portraitId.Trim();
        }

        if (scene != null && !string.IsNullOrWhiteSpace(scene.portraitId))
        {
            return scene.portraitId.Trim();
        }

        return string.Empty;
    }

    private Sprite LoadPortraitSprite(string portraitId)
    {
        if (string.IsNullOrWhiteSpace(portraitId))
        {
            return null;
        }

        if (_portraitCache.TryGetValue(portraitId, out Sprite cached))
        {
            return cached;
        }

        string portraitDirectory = StreamingAssetsUtility.GetStreamingAssetPath("Textures/StoryPortraits", false);
        if (string.IsNullOrWhiteSpace(portraitDirectory))
        {
            return null;
        }

        string portraitPath = Path.Combine(portraitDirectory, $"{portraitId}.png");
        if (!File.Exists(portraitPath))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(portraitPath);
        Texture2D texture = StreamingAssetsUtility.BinaryToTexture(bytes);
        if (texture == null)
        {
            return null;
        }

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        _portraitCache[portraitId] = sprite;
        return sprite;
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
            case StoryPage.Worlds:
                backButton.onClick.AddListener(() =>
                {
                    _currentPage = StoryPage.Acts;
                    _selectedActId = string.Empty;
                    _selectedWorldId = string.Empty;
                    RenderCurrentPage();
                });
                SetButtonLabel(backButton, "BACK");
                break;

            case StoryPage.Encounters:
            case StoryPage.PlaceholderWorld:
                backButton.onClick.AddListener(() =>
                {
                    _currentPage = StoryPage.Worlds;
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

        backButton.interactable = DialogueRoot == null || !DialogueRoot.activeSelf;
    }

    private void EnsureRuntimeUi()
    {
        if (_runtimeUiBuilt &&
            CurrencyText != null &&
            BreadcrumbText != null &&
            InfoText != null &&
            StoryScrollRect != null &&
            StoryContent != null &&
            PopupRoot != null &&
            PopupText != null &&
            PopupCloseButton != null &&
            DialogueRoot != null &&
            DialogueSpeakerText != null &&
            DialogueBodyText != null &&
            DialogueAdvanceButton != null)
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

        StoryScrollRect = scrollRoot.GetComponent<ScrollRect>();
        if (StoryScrollRect == null)
        {
            StoryScrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        }

        StoryScrollRect.horizontal = false;
        StoryScrollRect.vertical = true;
        StoryScrollRect.scrollSensitivity = 24f;

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

        StoryContent = FindOrCreateRectTransform(viewport, "Content");
        StoryContent.anchorMin = new Vector2(0f, 1f);
        StoryContent.anchorMax = new Vector2(1f, 1f);
        StoryContent.pivot = new Vector2(0.5f, 1f);
        StoryContent.anchoredPosition = Vector2.zero;
        StoryContent.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup grid = StoryContent.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = StoryContent.gameObject.AddComponent<GridLayoutGroup>();
        }

        grid.cellSize = new Vector2(320f, 132f);
        grid.spacing = new Vector2(18f, 18f);
        grid.padding = new RectOffset(18, 18, 18, 18);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;

        ContentSizeFitter contentFitter = StoryContent.GetComponent<ContentSizeFitter>();
        if (contentFitter == null)
        {
            contentFitter = StoryContent.gameObject.AddComponent<ContentSizeFitter>();
        }

        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        StoryScrollRect.viewport = viewport;
        StoryScrollRect.content = StoryContent;

        PopupRoot = FindOrCreateRectTransform(runtimeRoot, PopupRootName).gameObject;
        RectTransform popupRect = PopupRoot.GetComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.anchoredPosition = Vector2.zero;
        popupRect.sizeDelta = new Vector2(720f, 320f);

        Image popupBackground = PopupRoot.GetComponent<Image>();
        if (popupBackground == null)
        {
            popupBackground = PopupRoot.AddComponent<Image>();
        }

        popupBackground.color = new Color(0.06f, 0.08f, 0.13f, 0.96f);
        popupBackground.raycastTarget = true;

        PopupText = FindOrCreateText(popupRect, PopupTextName, 28, TextAnchor.MiddleCenter, FontStyle.Bold);
        RectTransform popupTextRect = PopupText.rectTransform;
        popupTextRect.anchorMin = new Vector2(0f, 0f);
        popupTextRect.anchorMax = new Vector2(1f, 1f);
        popupTextRect.offsetMin = new Vector2(30f, 88f);
        popupTextRect.offsetMax = new Vector2(-30f, -32f);

        PopupCloseButton = FindOrCreateButton(popupRect, PopupButtonName, "OK");
        RectTransform popupButtonRect = PopupCloseButton.GetComponent<RectTransform>();
        popupButtonRect.anchorMin = new Vector2(0.5f, 0f);
        popupButtonRect.anchorMax = new Vector2(0.5f, 0f);
        popupButtonRect.pivot = new Vector2(0.5f, 0f);
        popupButtonRect.anchoredPosition = new Vector2(0f, 24f);
        popupButtonRect.sizeDelta = new Vector2(220f, 64f);
        PopupCloseButton.onClick.RemoveAllListeners();
        PopupCloseButton.onClick.AddListener(() => PopupRoot.SetActive(false));

        PopupRoot.SetActive(false);

        DialogueRoot = FindOrCreateRectTransform(runtimeRoot, DialogueRootName).gameObject;
        RectTransform dialogueRootRect = DialogueRoot.GetComponent<RectTransform>();
        dialogueRootRect.anchorMin = Vector2.zero;
        dialogueRootRect.anchorMax = Vector2.one;
        dialogueRootRect.offsetMin = Vector2.zero;
        dialogueRootRect.offsetMax = Vector2.zero;

        Image dialogueOverlay = DialogueRoot.GetComponent<Image>();
        if (dialogueOverlay == null)
        {
            dialogueOverlay = DialogueRoot.AddComponent<Image>();
        }

        dialogueOverlay.color = new Color(0.02f, 0.03f, 0.06f, 0.88f);
        dialogueOverlay.raycastTarget = true;

        RectTransform dialogueBox = FindOrCreateRectTransform(dialogueRootRect, DialogueBoxName);
        dialogueBox.anchorMin = new Vector2(0.04f, 0.06f);
        dialogueBox.anchorMax = new Vector2(0.96f, 0.92f);
        dialogueBox.offsetMin = Vector2.zero;
        dialogueBox.offsetMax = Vector2.zero;

        Image dialogueBoxBackground = dialogueBox.GetComponent<Image>();
        if (dialogueBoxBackground == null)
        {
            dialogueBoxBackground = dialogueBox.gameObject.AddComponent<Image>();
        }

        dialogueBoxBackground.color = new Color(0.08f, 0.1f, 0.15f, 0.95f);
        dialogueBoxBackground.raycastTarget = true;

        DialoguePortraitImage = FindOrCreateImage(dialogueBox, DialoguePortraitName);
        RectTransform dialoguePortraitRect = DialoguePortraitImage.rectTransform;
        dialoguePortraitRect.anchorMin = new Vector2(0f, 0f);
        dialoguePortraitRect.anchorMax = new Vector2(0f, 1f);
        dialoguePortraitRect.pivot = new Vector2(0f, 0.5f);
        dialoguePortraitRect.anchoredPosition = new Vector2(28f, 0f);
        dialoguePortraitRect.sizeDelta = new Vector2(320f, 0f);
        DialoguePortraitImage.preserveAspect = true;
        DialoguePortraitImage.color = Color.white;

        RectTransform textPanel = FindOrCreateRectTransform(dialogueBox, "DialogueTextPanel");
        textPanel.anchorMin = new Vector2(0f, 0f);
        textPanel.anchorMax = new Vector2(1f, 1f);
        textPanel.offsetMin = new Vector2(380f, 28f);
        textPanel.offsetMax = new Vector2(-28f, -28f);

        DialogueSpeakerText = FindOrCreateText(textPanel, DialogueSpeakerName, 30, TextAnchor.UpperLeft, FontStyle.Bold);
        RectTransform speakerRect = DialogueSpeakerText.rectTransform;
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(0f, 1f);
        speakerRect.offsetMin = new Vector2(0f, -56f);
        speakerRect.offsetMax = new Vector2(0f, 0f);

        DialogueBodyText = FindOrCreateText(textPanel, DialogueBodyName, 28, TextAnchor.UpperLeft, FontStyle.Normal);
        RectTransform bodyRect = DialogueBodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(0f, 90f);
        bodyRect.offsetMax = new Vector2(0f, -74f);

        DialogueAdvanceButton = FindOrCreateButton(textPanel, DialogueAdvanceButtonName, "NEXT");
        RectTransform advanceRect = DialogueAdvanceButton.GetComponent<RectTransform>();
        advanceRect.anchorMin = new Vector2(1f, 0f);
        advanceRect.anchorMax = new Vector2(1f, 0f);
        advanceRect.pivot = new Vector2(1f, 0f);
        advanceRect.anchoredPosition = new Vector2(0f, 0f);
        advanceRect.sizeDelta = new Vector2(220f, 64f);
        DialogueAdvanceButton.onClick.RemoveAllListeners();
        DialogueAdvanceButton.onClick.AddListener(AdvanceDialogueScene);

        DialogueRoot.SetActive(false);
        _runtimeUiBuilt = true;
    }

    private void ClearEntries()
    {
        _entryButtons.Clear();

        if (StoryContent == null)
        {
            return;
        }

        for (int index = StoryContent.childCount - 1; index >= 0; index--)
        {
            Destroy(StoryContent.GetChild(index).gameObject);
        }
    }

    private void AddEntryButton(string objectName, string title, string subtitle, bool interactable, Color color, Action onClick)
    {
        if (StoryContent == null)
        {
            return;
        }

        Button button = CreateButton(StoryContent, objectName, string.Empty);
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
        if (StoryScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            StoryScrollRect.verticalNormalizedPosition = 1f;
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

    private static string BuildEncounterSubtitle(StoryEncounterDef encounter, bool completed, bool unlocked)
    {
        string roleLabel;
        switch (encounter != null ? encounter.Role : StoryEncounterRole.Standard)
        {
            case StoryEncounterRole.Qualifier:
                roleLabel = "Qualifier";
                break;

            case StoryEncounterRole.Gatekeeper:
                roleLabel = "Gatekeeper";
                break;

            case StoryEncounterRole.Champion:
                roleLabel = "Champion";
                break;

            default:
                roleLabel = "Encounter";
                break;
        }

        if (completed)
        {
            return $"{roleLabel} • Cleared";
        }

        if (!unlocked)
        {
            return "Locked";
        }

        if (encounter != null && !encounter.IsPlayable)
        {
            return $"{roleLabel} • Coming Soon";
        }

        return $"{roleLabel} • Ready";
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

    private static Image FindOrCreateImage(Transform parent, string name)
    {
        Transform existing = FindChildRecursive(parent, name);
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        if (image == null)
        {
            GameObject imageObject = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(Image));
            if (existing == null)
            {
                imageObject.transform.SetParent(parent, false);
            }

            image = imageObject.GetComponent<Image>();
            if (image == null)
            {
                image = imageObject.AddComponent<Image>();
            }
        }

        image.name = name;
        image.raycastTarget = false;
        return image;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        return ConfigureButton(buttonObject.GetComponent<Button>(), label);
    }

    private static Button FindOrCreateButton(Transform parent, string name, string label)
    {
        Transform existing = FindChildRecursive(parent, name);
        Button button = existing != null ? existing.GetComponent<Button>() : null;
        if (button == null)
        {
            GameObject buttonObject = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            if (existing == null)
            {
                buttonObject.transform.SetParent(parent, false);
            }

            button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                button = buttonObject.AddComponent<Button>();
            }
        }

        return ConfigureButton(button, label);
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
