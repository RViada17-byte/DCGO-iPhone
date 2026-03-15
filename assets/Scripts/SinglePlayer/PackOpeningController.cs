using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PackOpeningController : MonoBehaviour
{
    private enum PackOpeningState
    {
        Hidden = 0,
        FadingIn = 1,
        PackEnter = 2,
        WaitingForTap = 3,
        OpeningBurst = 4,
        FanningSlots = 5,
        RevealingCards = 6,
        BatchTransition = 7,
        Summary = 8,
        Closing = 9,
    }

    [Header("Optional Overrides")]
    [SerializeField] private PackPresentationCatalog presentationCatalog;
    [SerializeField] private int pooledCardViewCount = 6;
    [SerializeField] private LandscapeLayoutConfig landscapeLayout = LandscapeLayoutConfig.CreateDefault();

    [Header("Runtime UI Refs")]
    [SerializeField] private CanvasGroup overlayGroup;
    [SerializeField] private Image overlayPrimary;
    [SerializeField] private Image overlayTint;
    [SerializeField] private Image overlaySecondary;
    [SerializeField] private Button inputCatcherButton;
    [SerializeField] private Text hintText;
    [SerializeField] private RectTransform packRoot;
    [SerializeField] private Image packShadowImage;
    [SerializeField] private Image packGlowImage;
    [SerializeField] private Image packRimLightImage;
    [SerializeField] private Image packShineImage;
    [SerializeField] private Image packImage;
    [SerializeField] private Text packLabelText;
    [SerializeField] private Text packSubLabelText;
    [SerializeField] private RectTransform packIdentityRoot;
    [SerializeField] private Image packIdentityBackgroundImage;
    [SerializeField] private Image packIdentityLogoImage;
    [SerializeField] private Text packIdentityText;
    [SerializeField] private Image flashImage;
    [SerializeField] private Image tearImage;
    [SerializeField] private RectTransform slotsRoot;
    [SerializeField] private RectTransform burstRoot;
    [SerializeField] private RectTransform summaryRoot;
    [SerializeField] private CanvasGroup summaryCanvasGroup;
    [SerializeField] private Image summaryGlowImage;
    [SerializeField] private Image summaryBackgroundImage;
    [SerializeField] private Image summaryAccentImage;
    [SerializeField] private Text summaryRewardLabelText;
    [SerializeField] private Text summaryTitleText;
    [SerializeField] private Text summaryStatsText;
    [SerializeField] private Text summaryFeaturedLabelText;
    [SerializeField] private Text summaryBodyText;
    [SerializeField] private Image summaryHeroPackImage;
    [SerializeField] private RectTransform summarySetBadgeRoot;
    [SerializeField] private Image summarySetBadgeBackgroundImage;
    [SerializeField] private Image summarySetLogoImage;
    [SerializeField] private Text summarySetBadgeText;
    [SerializeField] private RectTransform summaryMetricsRoot;
    [SerializeField] private RectTransform summaryHighlightsRoot;
    [SerializeField] private RectTransform summaryDetailsRoot;
    [SerializeField] private RectTransform summaryActionRoot;
    [SerializeField] private Image summaryDetailsBackgroundImage;
    [SerializeField] private Text summaryDetailsHeaderText;
    [SerializeField] private Text summaryDetailsLeftColumnText;
    [SerializeField] private Text summaryDetailsRightColumnText;
    [SerializeField] private PackRevealCardView summaryPreviewCardView;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button summarySkipButton;
    [SerializeField] private Button stageSkipButton;
    [SerializeField] private Text stageSkipLabel;
    [SerializeField] private Text continueLabel;
    [SerializeField] private Text summarySkipLabel;

    private readonly List<PackRevealCardView> _cardPool = new List<PackRevealCardView>();
    private readonly Queue<Image> _burstPool = new Queue<Image>();
    private readonly List<SummaryMetricWidget> _summaryMetricWidgets = new List<SummaryMetricWidget>();
    private readonly List<SummaryHighlightWidget> _summaryHighlightWidgets = new List<SummaryHighlightWidget>();
    private readonly List<PackOpeningResult.CardEntry> _summaryHighlightEntries = new List<PackOpeningResult.CardEntry>();
    private readonly List<Task<Sprite>> _summaryHighlightSpriteTasks = new List<Task<Sprite>>();
    private readonly Dictionary<string, float> _sfxLastPlayedAt = new Dictionary<string, float>(StringComparer.Ordinal);
    private Coroutine _sequenceCoroutine;
    private Coroutine _idleCoroutine;
    private PackOpeningResult _currentResult;
    private PackPresentationTheme _currentTheme;
    private Action _onClosed;
    private PackOpeningState _state = PackOpeningState.Hidden;
    private bool _uiBuilt;
    private bool _buttonsBound;
    private bool _openRequested;
    private bool _fastForward;
    private bool _skipToSummary;
    private bool _summaryContinueRequested;
    private int _summarySelectedCardIndex = -1;
    private Vector2 _packRestPosition = new Vector2(0f, 120f);
    private Vector2 _packStartPosition = new Vector2(0f, -560f);
    private Vector2 _summaryRestPosition;
    private Vector2 _summaryIntroPosition;
    private LayoutMetrics _layoutMetrics;
    private const string DebugPrefix = "[PackOpening]";
    private const int SummaryMetricCount = 3;
    private const int SummaryInspectionColumns = 6;
    private const int SummaryInspectionSlotCount = 12;
    private const int SummaryTopPullLineCount = 3;

    private void Awake()
    {
        EnsureStructure();
        ApplyLayout();
        WireButtons();
        HideOverlayImmediate();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!_uiBuilt)
        {
            return;
        }

        ApplyLayout();
    }

    public void Play(PackOpeningResult result, PackPresentationCatalog catalogOverride = null, Action onClosed = null)
    {
        if (result == null || result.Cards == null || result.Cards.Count == 0)
        {
            Debug.LogWarning($"{DebugPrefix} Refused to play because no cards were provided.");
            return;
        }

        presentationCatalog = catalogOverride != null ? catalogOverride : presentationCatalog;
        _currentResult = result;
        _currentTheme = ResolveTheme(result);
        _onClosed = onClosed;

        EnsureStructure();
        ApplyLayout();
        WireButtons();

        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = null;
        }

        StopIdleWobble();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _sequenceCoroutine = StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        ResetPlaybackState();
        ApplyLayout();
        ApplyTheme();
        PrepareSummary();
        PreparePack();
        PrepareCardPool();

        SetState(PackOpeningState.FadingIn);
        SetOverlayVisible(true);
        overlayGroup.alpha = 0f;
        yield return TweenValue(0.18f, progress => overlayGroup.alpha = progress, EaseOutCubic);

        if (_skipToSummary)
        {
            yield return JumpToSummary();
            yield break;
        }

        SetState(PackOpeningState.PackEnter);
        PlayOptionalSfx(_currentTheme != null ? _currentTheme.introSfxId : null);
        packRoot.gameObject.SetActive(true);
        packRoot.anchoredPosition = _packStartPosition;
        packRoot.localScale = Vector3.one * 0.82f;
        packRoot.localRotation = Quaternion.identity;
        packRoot.gameObject.SetActive(true);
        yield return TweenValue(0.34f, progress =>
        {
            float eased = EaseOutBack(progress);
            packRoot.anchoredPosition = Vector2.Lerp(_packStartPosition, _packRestPosition, eased);
            packRoot.localScale = Vector3.Lerp(Vector3.one * 0.82f, Vector3.one, eased);
            ApplyPackDepthTreatment(
                Mathf.Lerp(0.18f, 0.62f, progress),
                Mathf.Lerp(0.08f, 0.38f, progress),
                0f,
                Mathf.Lerp(0.18f, 0.62f, progress));
        });

        if (_skipToSummary)
        {
            yield return JumpToSummary();
            yield break;
        }

        SetState(PackOpeningState.WaitingForTap);
        StartIdleWobble();
        while (!_openRequested && !_skipToSummary)
        {
            yield return null;
        }

        StopIdleWobble();
        if (_skipToSummary)
        {
            yield return JumpToSummary();
            yield break;
        }

        SetState(PackOpeningState.OpeningBurst);
        yield return PlayOpenBurst();
        packRoot.gameObject.SetActive(false);

        if (_skipToSummary)
        {
            yield return JumpToSummary();
            yield break;
        }

        int totalCards = _currentResult.Cards.Count;
        int batchSize = Mathf.Max(1, pooledCardViewCount);
        for (int batchStart = 0; batchStart < totalCards; batchStart += batchSize)
        {
            int visibleCount = Mathf.Min(batchSize, totalCards - batchStart);
            Task<Sprite>[] spriteTasks = StartBatchSpriteLoads(batchStart, visibleCount);
            PrepareBatch(visibleCount);

            SetState(PackOpeningState.FanningSlots);
            yield return AnimateFanOut(visibleCount);

            if (_skipToSummary)
            {
                yield return JumpToSummary();
                yield break;
            }

            SetState(PackOpeningState.RevealingCards);
            yield return RevealBatch(batchStart, visibleCount, spriteTasks);

            if (_skipToSummary)
            {
                yield return JumpToSummary();
                yield break;
            }

            if (batchStart + visibleCount < totalCards)
            {
                SetState(PackOpeningState.BatchTransition);
                yield return AnimateBatchCollapse(visibleCount);
            }
        }

        yield return JumpToSummary();
    }

    private IEnumerator JumpToSummary()
    {
        SetState(PackOpeningState.Summary);
        StopIdleWobble();
        packRoot.gameObject.SetActive(false);
        flashImage.color = new Color(1f, 1f, 1f, 0f);
        tearImage.color = new Color(1f, 1f, 1f, 0f);

        for (int index = 0; index < _cardPool.Count; index++)
        {
            if (_cardPool[index] == null)
            {
                continue;
            }

            _cardPool[index].gameObject.SetActive(false);
            _cardPool[index].ViewCanvasGroup.alpha = 0f;
        }

        stageSkipButton.gameObject.SetActive(false);
        summaryRoot.SetAsLastSibling();
        summaryRoot.gameObject.SetActive(true);
        summaryCanvasGroup.alpha = 0f;
        summaryRoot.anchoredPosition = _summaryIntroPosition;
        yield return PopulateSummaryHighlights();

        yield return TweenValue(_fastForward ? 0.08f : 0.2f, progress =>
        {
            summaryCanvasGroup.alpha = progress;
            summaryRoot.anchoredPosition = Vector2.Lerp(_summaryIntroPosition, _summaryRestPosition, EaseOutCubic(progress));
        }, EaseOutCubic);

        while (!_summaryContinueRequested)
        {
            yield return null;
        }

        yield return CloseOverlay(false);
    }

    private IEnumerator CloseOverlay(bool immediate)
    {
        SetState(PackOpeningState.Closing);

        if (!immediate)
        {
            yield return TweenValue(0.16f, progress =>
            {
                float inverse = 1f - progress;
                overlayGroup.alpha = inverse;
                summaryCanvasGroup.alpha = inverse;
            });
        }

        Action callback = _onClosed;
        _onClosed = null;
        callback?.Invoke();
        HideOverlayImmediate();
    }

    private IEnumerator PlayOpenBurst()
    {
        PlayOptionalSfx(_currentTheme != null ? _currentTheme.openSfxId : null);
        ApplyPackDepthTreatment(0.78f, 0.18f, 0f, 0.88f);
        flashImage.color = new Color(1f, 1f, 1f, 0f);
        tearImage.color = new Color(1f, 1f, 1f, 0f);

        yield return TweenValue(0.09f, progress =>
        {
            packRoot.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.08f, progress);
            packRoot.anchoredPosition = _packRestPosition + UnityEngine.Random.insideUnitCircle * 10f * progress;
            flashImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.6f, progress));
            tearImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.72f, progress));
            tearImage.rectTransform.localScale = new Vector3(Mathf.Lerp(0.6f, 1.2f, progress), 1f, 1f);
        });

        PlayBurst(_packRestPosition, _currentTheme.accentColor, 10, 120f);

        yield return TweenValue(0.12f, progress =>
        {
            packRoot.localScale = Vector3.Lerp(Vector3.one * 1.08f, Vector3.one * 0.78f, progress);
            packRoot.anchoredPosition = _packRestPosition + UnityEngine.Random.insideUnitCircle * 18f * (1f - progress);
            flashImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.6f, 0f, progress));
            tearImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.72f, 0f, progress));
            packGlowImage.color = new Color(_currentTheme.glowColor.r, _currentTheme.glowColor.g, _currentTheme.glowColor.b, Mathf.Lerp(0.4f, 0.16f, progress));
        });
    }

    private void PrepareBatch(int visibleCount)
    {
        SlotPose[] poses = BuildSlotPoses(visibleCount);
        Sprite backSprite = ContinuousController.instance != null && ContinuousController.instance.ReverseCard != null
            ? ContinuousController.instance.ReverseCard
            : PackOpeningUiUtility.GetFallbackCardSprite();

        for (int index = 0; index < _cardPool.Count; index++)
        {
            PackRevealCardView view = _cardPool[index];
            if (view == null)
            {
                continue;
            }

            bool active = index < visibleCount;
            view.gameObject.SetActive(active);
            view.ViewCanvasGroup.alpha = active ? 0f : 0f;
            if (!active)
            {
                continue;
            }

            SlotPose pose = poses[index];
            view.RectTransform.sizeDelta = pose.Size;
            view.ApplyLayout(pose.Size);
            view.RectTransform.anchoredPosition = Vector2.zero;
            view.RectTransform.localScale = Vector3.one * 0.76f;
            view.RectTransform.localRotation = Quaternion.identity;
            view.PrepareBack(backSprite, _currentTheme.cardBackColor, _currentTheme.accentColor);
        }
    }

    private IEnumerator AnimateFanOut(int visibleCount)
    {
        SlotPose[] poses = BuildSlotPoses(visibleCount);
        Vector2 origin = Vector2.zero;
        yield return TweenValue(landscapeLayout.fanRevealDuration, progress =>
        {
            float eased = EaseOutCubic(progress);
            for (int index = 0; index < visibleCount; index++)
            {
                PackRevealCardView view = _cardPool[index];
                if (view == null)
                {
                    continue;
                }

                view.ViewCanvasGroup.alpha = Mathf.Lerp(0f, 1f, Mathf.Clamp01(progress * 1.15f));
                view.RectTransform.anchoredPosition = Vector2.Lerp(origin, poses[index].Position, eased);
                view.RectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, poses[index].Rotation, eased));
                view.RectTransform.localScale = Vector3.one * Mathf.Lerp(0.76f, poses[index].Scale, eased);
            }
        }, EaseOutCubic);
    }

    private IEnumerator RevealBatch(int batchStart, int visibleCount, Task<Sprite>[] spriteTasks)
    {
        for (int index = 0; index < visibleCount; index++)
        {
            if (_skipToSummary)
            {
                yield break;
            }

            PackOpeningResult.CardEntry entry = _currentResult.Cards[batchStart + index];
            PackRevealCardView view = _cardPool[index];
            if (entry == null || view == null)
            {
                continue;
            }

            yield return WaitForTaskOrTimeout(spriteTasks[index], _fastForward ? 0.04f : landscapeLayout.revealSpriteWaitDuration);
            Sprite sprite = GetTaskResult(spriteTasks[index]) ?? PackOpeningUiUtility.GetFallbackCardSprite();
            Color rarityColor = _currentTheme.GetRarityColor(entry.Rarity);
            view.SetRevealContent(entry, sprite, rarityColor);
            Debug.Log($"{DebugPrefix} Revealing {entry.CardId} ({entry.Rarity}) new={entry.IsNew}");
            PlayOptionalSfx(_currentTheme != null ? _currentTheme.revealSfxId : null, _fastForward ? 0.06f : 0.02f);
            yield return view.PlayReveal(_fastForward);

            int burstCount = entry.IsRare ? 16 : 5;
            float burstRadius = entry.IsRare ? 156f : 74f;
            float burstSizeMultiplier = entry.IsRare ? 1.56f : 1f;
            float burstAlphaMultiplier = entry.IsRare ? 1.26f : 1f;
            PlayBurst(view.RectTransform.anchoredPosition, rarityColor, burstCount, burstRadius, burstSizeMultiplier, burstAlphaMultiplier);

            if (entry.IsRare)
            {
                PlayOptionalSfx(_currentTheme != null ? _currentTheme.rareRevealSfxId : null, 0.05f);
                yield return view.PlayRarePulse(_fastForward);
            }

            yield return WaitForSecondsScaled(entry.IsRare ? landscapeLayout.revealRarePauseDuration : landscapeLayout.revealPauseDuration);
        }
    }

    private IEnumerator AnimateBatchCollapse(int visibleCount)
    {
        Vector2 endPosition = _layoutMetrics.FanCenter + new Vector2(0f, -_layoutMetrics.CardSize.y * 0.14f);
        Vector2[] startPositions = new Vector2[visibleCount];
        float[] startScales = new float[visibleCount];
        for (int index = 0; index < visibleCount; index++)
        {
            PackRevealCardView view = _cardPool[index];
            if (view == null)
            {
                continue;
            }

            startPositions[index] = view.RectTransform.anchoredPosition;
            startScales[index] = view.RectTransform.localScale.x;
        }

        yield return TweenValue(landscapeLayout.batchCollapseDuration, progress =>
        {
            float eased = EaseInOutSine(progress);
            for (int index = 0; index < visibleCount; index++)
            {
                PackRevealCardView view = _cardPool[index];
                if (view == null)
                {
                    continue;
                }

                view.ViewCanvasGroup.alpha = 1f - progress;
                view.RectTransform.anchoredPosition = Vector2.Lerp(startPositions[index], endPosition, eased);
                float scale = Mathf.Lerp(startScales[index], 0.68f, eased);
                view.RectTransform.localScale = Vector3.one * scale;
            }
        }, EaseInOutSine);
    }

    private Task<Sprite>[] StartBatchSpriteLoads(int batchStart, int visibleCount)
    {
        Task<Sprite>[] tasks = new Task<Sprite>[visibleCount];
        for (int index = 0; index < visibleCount; index++)
        {
            PackOpeningResult.CardEntry entry = _currentResult.Cards[batchStart + index];
            tasks[index] = entry != null ? entry.LoadSpriteAsync() : null;
        }

        return tasks;
    }

    private void PreparePack()
    {
        packRoot.gameObject.SetActive(true);
        packRoot.localScale = Vector3.one;
        packRoot.localRotation = Quaternion.identity;
        packRoot.anchoredPosition = _packStartPosition;

        Sprite packSprite = _currentTheme.packArt;
        bool usingArt = packSprite != null;
        packShadowImage.sprite = PackOpeningUiUtility.GetSoftCircleSprite();
        packGlowImage.sprite = PackOpeningUiUtility.GetSoftCircleSprite();
        packRimLightImage.sprite = PackOpeningUiUtility.GetSoftFrameSprite();
        packShineImage.sprite = PackOpeningUiUtility.GetShineSprite();
        packImage.sprite = usingArt ? packSprite : PackOpeningUiUtility.GetWhiteSprite();
        packImage.preserveAspect = true;
        packImage.color = usingArt ? Color.white : _currentTheme.packTint;
        packShadowImage.color = new Color(0f, 0f, 0f, 0.24f);
        packGlowImage.color = new Color(_currentTheme.glowColor.r, _currentTheme.glowColor.g, _currentTheme.glowColor.b, 0.16f);
        packRimLightImage.color = new Color(1f, 1f, 1f, usingArt ? 0.11f : 0.06f);
        packShineImage.color = new Color(1f, 1f, 1f, usingArt ? 0.05f : 0.02f);
        packShineImage.gameObject.SetActive(usingArt);
        packLabelText.text = _currentResult.DisplayName ?? "Pack";
        packSubLabelText.text = string.IsNullOrWhiteSpace(_currentResult.SetId) ? "Tap to open" : _currentResult.SetId.Trim();
        packLabelText.gameObject.SetActive(!usingArt);
        packSubLabelText.gameObject.SetActive(!usingArt);
        ApplyPackIdentity(usingArt);
        ApplyPackDepthTreatment(0.3f, 0.12f, 0f, usingArt ? 0.24f : 0f);
    }

    private void PrepareSummary()
    {
        summaryRoot.gameObject.SetActive(false);
        summaryCanvasGroup.alpha = 0f;
        summaryGlowImage.raycastTarget = false;
        summaryBackgroundImage.raycastTarget = false;
        summaryAccentImage.raycastTarget = false;
        continueLabel.text = "Open Another";
        summarySkipLabel.text = "Back to Shop";
        summaryRewardLabelText.text = string.Empty;
        summaryTitleText.text = string.Empty;
        summaryFeaturedLabelText.text = string.Empty;
        summaryStatsText.text = string.Empty;
        summaryBodyText.text = string.Empty;
        summaryHeroPackImage.sprite = _currentTheme.packArt != null ? _currentTheme.packArt : PackOpeningUiUtility.GetWhiteSprite();
        summaryHeroPackImage.color = _currentTheme.packArt != null
            ? new Color(1f, 1f, 1f, 0.82f)
            : new Color(_currentTheme.packTint.r, _currentTheme.packTint.g, _currentTheme.packTint.b, 0.76f);
        ApplySummaryIdentity();
        PrepareSummaryMetrics();
        PrepareSummaryHighlightsModel();
        PrepareSummaryPreview();

        summaryRewardLabelText.gameObject.SetActive(false);
        summaryTitleText.gameObject.SetActive(false);
        summaryFeaturedLabelText.gameObject.SetActive(false);
        summaryStatsText.gameObject.SetActive(false);
        summaryBodyText.gameObject.SetActive(false);
        summaryHeroPackImage.gameObject.SetActive(false);
        summarySetBadgeRoot.gameObject.SetActive(false);
        summaryMetricsRoot.gameObject.SetActive(false);
    }

    private string BuildSummaryStats()
    {
        return $"{_currentResult.TotalCardCount} card{(_currentResult.TotalCardCount == 1 ? string.Empty : "s")} opened";
    }

    private string BuildSummaryInspectionHint()
    {
        if (_currentResult == null || _currentResult.Cards == null || _currentResult.Cards.Count == 0)
        {
            return string.Empty;
        }

        return _currentResult.RareCardCount > 0
            ? "Tap any card to inspect. Best hit selected."
            : "Tap any card to inspect. Cards stay in pack order.";
    }

        private RectTransform FindOrCreateSummaryRect(Transform parent, string preferredName, string legacyName = null)
    {
        Transform child = parent.Find(preferredName);
        if (child == null && !string.IsNullOrWhiteSpace(legacyName))
        {
            child = parent.Find(legacyName);
        }

        RectTransform rect = child as RectTransform;
        if (rect == null)
        {
            rect = PackOpeningUiUtility.FindOrCreateRect(parent, preferredName);
        }

        rect.name = preferredName;
        return rect;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }

private void PrepareSummaryPreview()
    {
        if (summaryDetailsRoot != null)
        {
            summaryDetailsRoot.gameObject.SetActive(true);
        }

        if (summaryActionRoot != null)
        {
            summaryActionRoot.gameObject.SetActive(true);
        }

        if (summaryDetailsHeaderText != null)
        {
            summaryDetailsHeaderText.text = string.Empty;
            summaryDetailsHeaderText.gameObject.SetActive(false);
        }

        if (summaryDetailsLeftColumnText != null)
        {
            summaryDetailsLeftColumnText.text = string.Empty;
            summaryDetailsLeftColumnText.gameObject.SetActive(false);
        }

        if (summaryDetailsRightColumnText != null)
        {
            summaryDetailsRightColumnText.text = string.Empty;
            summaryDetailsRightColumnText.gameObject.SetActive(false);
        }

        if (summaryPreviewCardView != null)
        {
            summaryPreviewCardView.gameObject.SetActive(true);
            summaryPreviewCardView.ViewCanvasGroup.alpha = 1f;
        }

        if (summaryDetailsBackgroundImage != null)
        {
            summaryDetailsBackgroundImage.sprite = PackOpeningUiUtility.GetSoftCircleSprite();
            summaryDetailsBackgroundImage.type = Image.Type.Simple;
            summaryDetailsBackgroundImage.raycastTarget = false;
        }

        ApplySummaryInspectionLayoutOverrides();

        continueButton.gameObject.SetActive(true);
        summarySkipButton.gameObject.SetActive(true);
    }

    private void ApplySummaryInspectionLayoutOverrides()
    {
        if (summaryRoot == null || summaryHighlightsRoot == null || summaryDetailsRoot == null || summaryActionRoot == null)
        {
            return;
        }

        RectTransform rootRect = summaryRoot;
        float rootWidth = Mathf.Max(1f, rootRect.rect.width);
        float rootHeight = Mathf.Max(1f, rootRect.rect.height);

        float outerMargin = Mathf.Round(rootWidth * 0.024f);
        float topBottomMargin = Mathf.Round(rootHeight * 0.07f);
        float columnGap = Mathf.Round(rootWidth * 0.018f);
        float actionBottom = Mathf.Round(rootHeight * 0.03f);
        float actionHeight = Mathf.Round(rootHeight * 0.105f);
        float previewWidth = Mathf.Round(rootWidth * 0.35f);
        float previewHeight = Mathf.Round(rootHeight * 0.88f);
        float gridWidth = rootWidth - (outerMargin * 2f) - previewWidth - columnGap;
        float gridHeight = Mathf.Round(rootHeight * 0.84f);
        float gridDrop = Mathf.Round(rootHeight * 0.03f);
        float gridCenterOffsetX = outerMargin + (gridWidth * 0.5f) - (rootWidth * 0.5f);

        summaryHighlightsRoot.anchorMin = new Vector2(0f, 0.5f);
        summaryHighlightsRoot.anchorMax = new Vector2(0f, 0.5f);
        summaryHighlightsRoot.pivot = new Vector2(0f, 0.5f);
        summaryHighlightsRoot.sizeDelta = new Vector2(gridWidth, gridHeight);
        summaryHighlightsRoot.anchoredPosition = new Vector2(outerMargin, -gridDrop);

        summaryDetailsRoot.anchorMin = new Vector2(1f, 0.5f);
        summaryDetailsRoot.anchorMax = new Vector2(1f, 0.5f);
        summaryDetailsRoot.pivot = new Vector2(1f, 0.5f);
        summaryDetailsRoot.sizeDelta = new Vector2(previewWidth, previewHeight);
        summaryDetailsRoot.anchoredPosition = new Vector2(-outerMargin, 0f);

        summaryActionRoot.anchorMin = new Vector2(0.5f, 0f);
        summaryActionRoot.anchorMax = new Vector2(0.5f, 0f);
        summaryActionRoot.pivot = new Vector2(0.5f, 0f);
        summaryActionRoot.sizeDelta = new Vector2(Mathf.Min(rootWidth * 0.34f, 580f), actionHeight);
        summaryActionRoot.anchoredPosition = new Vector2(gridCenterOffsetX, actionBottom);

        HorizontalLayoutGroup actionLayout = GetOrAddComponent<HorizontalLayoutGroup>(summaryActionRoot.gameObject);
        actionLayout.childAlignment = TextAnchor.MiddleCenter;
        actionLayout.childControlWidth = true;
        actionLayout.childControlHeight = true;
        actionLayout.childForceExpandWidth = false;
        actionLayout.childForceExpandHeight = false;
        actionLayout.spacing = Mathf.Round(rootWidth * 0.012f);
        actionLayout.padding = new RectOffset(0, 0, 0, 0);

        Image actionPanelImage = summaryActionRoot.GetComponent<Image>();
        if (actionPanelImage != null)
        {
            actionPanelImage.color = new Color(0f, 0f, 0f, 0f);
        }

        ConfigureSummaryActionButton(continueButton, Mathf.Min(rootWidth * 0.12f, 240f), Mathf.Round(actionHeight * 0.72f));
        ConfigureSummaryActionButton(summarySkipButton, Mathf.Min(rootWidth * 0.12f, 240f), Mathf.Round(actionHeight * 0.72f));

        if (summaryPreviewCardView != null)
        {
            RectTransform previewCardRect = summaryPreviewCardView.RectTransform;
            float cardWidth = Mathf.Min(previewWidth - 36f, (previewHeight - 28f) / 1.425f);
            float cardHeight = cardWidth * 1.425f;

            previewCardRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewCardRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewCardRect.pivot = new Vector2(0.5f, 0.5f);
            previewCardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
            previewCardRect.anchoredPosition = new Vector2(0f, 0f);
            previewCardRect.localScale = Vector3.one;
            summaryPreviewCardView.ApplyLayout(new Vector2(cardWidth, cardHeight));

            if (summaryDetailsBackgroundImage != null)
            {
                RectTransform auraRect = summaryDetailsBackgroundImage.rectTransform;
                auraRect.anchorMin = new Vector2(0.5f, 0.5f);
                auraRect.anchorMax = new Vector2(0.5f, 0.5f);
                auraRect.pivot = new Vector2(0.5f, 0.5f);
                auraRect.sizeDelta = new Vector2(cardWidth + 20f, cardHeight + 20f);
                auraRect.anchoredPosition = previewCardRect.anchoredPosition;
            }
        }
    }

    private void ConfigureSummaryActionButton(Button button, float width, float height)
    {
        if (button == null)
        {
            return;
        }

        LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(button.gameObject);
        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.flexibleWidth = 0f;
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;
        layoutElement.flexibleHeight = 0f;
    }

    private static void AppendEntryNames(StringBuilder builder, List<PackOpeningResult.CardEntry> entries)
    {
        for (int index = 0; index < entries.Count; index++)
        {
            PackOpeningResult.CardEntry entry = entries[index];
            if (entry == null)
            {
                continue;
            }

            builder.Append(!string.IsNullOrWhiteSpace(entry.CardName) ? entry.CardName : entry.CardId);
            if (index < entries.Count - 1)
            {
                builder.Append(", ");
            }
        }
    }

private void ApplyTheme()
    {
        overlayPrimary.color = WithAlpha(_currentTheme.overlayColor, Mathf.Max(_currentTheme.overlayColor.a, landscapeLayout.minimumPrimaryOverlayAlpha));
        overlayTint.color = _currentTheme.backgroundTintColor;
        overlaySecondary.color = WithAlpha(_currentTheme.secondaryOverlayColor, Mathf.Max(_currentTheme.secondaryOverlayColor.a * landscapeLayout.secondaryOverlayAlphaMultiplier, 0.18f));
        summaryGlowImage.sprite = PackOpeningUiUtility.GetSoftCircleSprite();
        summaryGlowImage.color = Color.clear;
        summaryBackgroundImage.color = new Color(0f, 0f, 0f, 1f);
        summaryAccentImage.color = new Color(_currentTheme.accentColor.r, _currentTheme.accentColor.g, _currentTheme.accentColor.b, 0f);
        summaryRewardLabelText.color = Color.Lerp(_currentTheme.summaryTextColor, _currentTheme.accentColor, 0.55f);
        summaryTitleText.color = _currentTheme.summaryTextColor;
        summaryStatsText.color = WithAlpha(_currentTheme.summaryTextColor, 0.88f);
        summaryFeaturedLabelText.color = WithAlpha(_currentTheme.summaryTextColor, 0.84f);
        summaryBodyText.color = WithAlpha(_currentTheme.summaryTextColor, 0.78f);
        summaryHeroPackImage.color = _currentTheme.packArt != null
            ? new Color(1f, 1f, 1f, 0.92f)
            : new Color(_currentTheme.packTint.r, _currentTheme.packTint.g, _currentTheme.packTint.b, 0.84f);
        summaryDetailsBackgroundImage.color = Color.clear;
        summaryDetailsHeaderText.color = WithAlpha(_currentTheme.summaryTextColor, 0.94f);
        summaryDetailsLeftColumnText.color = WithAlpha(_currentTheme.summaryTextColor, 0.68f);
        summaryDetailsRightColumnText.color = WithAlpha(_currentTheme.summaryTextColor, 0.82f);
        stageSkipLabel.color = _currentTheme.summaryTextColor;
        continueLabel.color = WithAlpha(_currentTheme.summaryTextColor, 0.94f);
        summarySkipLabel.color = WithAlpha(_currentTheme.summaryTextColor, 0.92f);
        hintText.color = _currentTheme.summaryTextColor;
        packIdentityBackgroundImage.color = new Color(0f, 0f, 0f, 0.34f);
        packIdentityText.color = _currentTheme.summaryTextColor;
        summarySetBadgeBackgroundImage.color = Color.Lerp(_currentTheme.accentColor, Color.black, 0.6f);
        summarySetBadgeText.color = _currentTheme.summaryTextColor;
        if (packIdentityLogoImage != null)
        {
            packIdentityLogoImage.color = Color.white;
        }

        if (summarySetLogoImage != null)
        {
            summarySetLogoImage.color = Color.white;
        }

        SetButtonFill(stageSkipButton, new Color(0f, 0f, 0f, 0.42f));
        SetButtonFill(summarySkipButton, new Color(0.18f, 0.22f, 0.28f, 0.98f));
        SetButtonFill(continueButton, new Color(0.23f, 0.27f, 0.34f, 0.98f));
        ApplySummaryWidgetTheme();

        Image gridPanelImage = summaryHighlightsRoot != null ? GetOrAddComponent<Image>(summaryHighlightsRoot.gameObject) : null;
        if (gridPanelImage != null)
        {
            gridPanelImage.sprite = PackOpeningUiUtility.GetWhiteSprite();
            gridPanelImage.type = Image.Type.Simple;
            gridPanelImage.color = Color.clear;
            gridPanelImage.raycastTarget = false;
        }

        Image previewPanelImage = summaryDetailsRoot != null ? GetOrAddComponent<Image>(summaryDetailsRoot.gameObject) : null;
        if (previewPanelImage != null)
        {
            previewPanelImage.sprite = PackOpeningUiUtility.GetWhiteSprite();
            previewPanelImage.type = Image.Type.Simple;
            previewPanelImage.color = Color.clear;
            previewPanelImage.raycastTarget = false;
        }

        Image actionPanelImage = summaryActionRoot != null ? GetOrAddComponent<Image>(summaryActionRoot.gameObject) : null;
        if (actionPanelImage != null)
        {
            actionPanelImage.sprite = PackOpeningUiUtility.GetWhiteSprite();
            actionPanelImage.type = Image.Type.Simple;
            actionPanelImage.color = Color.clear;
            actionPanelImage.raycastTarget = false;
        }

        if (!string.IsNullOrWhiteSpace(_currentTheme.introSfxId))
        {
            Debug.Log($"{DebugPrefix} Intro SFX queued: {_currentTheme.introSfxId}");
        }
    }

    private void PrepareCardPool()
    {
        int targetCount = Mathf.Max(1, pooledCardViewCount);
        while (_cardPool.Count < targetCount)
        {
            GameObject cardObject = new GameObject($"RevealCard_{_cardPool.Count + 1}", typeof(RectTransform), typeof(CanvasGroup), typeof(PackRevealCardView));
            cardObject.layer = 5;
            RectTransform rect = cardObject.GetComponent<RectTransform>();
            rect.SetParent(slotsRoot, false);
            rect.localScale = Vector3.one;
            PackRevealCardView cardView = cardObject.GetComponent<PackRevealCardView>();
            cardView.EnsureStructure();
            cardObject.SetActive(false);
            _cardPool.Add(cardView);
        }
    }

private void ApplyLayout()
    {
        if (!_uiBuilt || landscapeLayout == null)
        {
            return;
        }

        _layoutMetrics = BuildLayoutMetrics();
        _packRestPosition = _layoutMetrics.PackPosition;
        _packStartPosition = _layoutMetrics.PackStartPosition;
        _summaryRestPosition = _layoutMetrics.SummaryPosition;
        _summaryIntroPosition = _layoutMetrics.SummaryIntroPosition;

        PackOpeningUiUtility.Stretch(overlayPrimary.rectTransform, 0f, 0f, 0f, 0f);
        PackOpeningUiUtility.Stretch(overlayTint.rectTransform, 0f, 0f, 0f, 0f);
        PackOpeningUiUtility.Stretch(overlaySecondary.rectTransform, 0f, 0f, 0f, 0f);
        PackOpeningUiUtility.Stretch(inputCatcherButton.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        PackOpeningUiUtility.Stretch(slotsRoot, 0f, 0f, 0f, 0f);
        PackOpeningUiUtility.Stretch(burstRoot, 0f, 0f, 0f, 0f);
        PackOpeningUiUtility.Stretch(flashImage.rectTransform, 0f, 0f, 0f, 0f);

        packRoot.anchorMin = new Vector2(0.5f, 0.5f);
        packRoot.anchorMax = new Vector2(0.5f, 0.5f);
        packRoot.pivot = new Vector2(0.5f, 0.5f);
        packRoot.sizeDelta = _layoutMetrics.PackSize;
        packRoot.anchoredPosition = _packRestPosition;

        Vector2 packSize = _layoutMetrics.PackSize;
        Vector2 shadowSize = new Vector2(
            packSize.x * landscapeLayout.packShadowWidthScale,
            packSize.y * landscapeLayout.packShadowHeightScale);
        Vector2 glowSize = new Vector2(
            packSize.x * landscapeLayout.packHaloWidthScale,
            packSize.y * landscapeLayout.packHaloHeightScale);
        float rimInset = packSize.x * landscapeLayout.packRimInsetNormalized;
        Vector2 shineSize = new Vector2(
            packSize.x * landscapeLayout.packShineWidthScale,
            packSize.y * landscapeLayout.packShineHeightScale);

        SetCenteredRect(
            packShadowImage.rectTransform,
            new Vector2(0f, packSize.y * landscapeLayout.packShadowYOffsetNormalized),
            shadowSize);
        SetCenteredRect(packGlowImage.rectTransform, Vector2.zero, glowSize);
        PackOpeningUiUtility.Stretch(packImage.rectTransform, 0f, 0f, 0f, 0f);
        PackOpeningUiUtility.Stretch(packRimLightImage.rectTransform, rimInset, rimInset, -rimInset, -rimInset);
        SetCenteredRect(packShineImage.rectTransform, Vector2.zero, shineSize);

        packLabelText.fontSize = ScaleFont(_layoutMetrics.PackSize.y * landscapeLayout.packLabelFontNormalized, 28, 56);
        packSubLabelText.fontSize = ScaleFont(_layoutMetrics.PackSize.y * landscapeLayout.packSubLabelFontNormalized, 18, 28);
        float packLabelHeight = _layoutMetrics.PackSize.y * 0.42f;
        float packLabelBottom = _layoutMetrics.PackSize.y * landscapeLayout.packLabelBottomNormalized;
        float packSubLabelHeight = _layoutMetrics.PackSize.y * 0.12f;
        float packSubLabelBottom = _layoutMetrics.PackSize.y * landscapeLayout.packSubLabelBottomNormalized;
        PackOpeningUiUtility.Stretch(
            packLabelText.rectTransform,
            _layoutMetrics.PackSize.x * 0.1f,
            packLabelBottom,
            -_layoutMetrics.PackSize.x * 0.1f,
            -(Mathf.Max(0f, _layoutMetrics.PackSize.y - packLabelBottom - packLabelHeight)));
        PackOpeningUiUtility.Stretch(
            packSubLabelText.rectTransform,
            _layoutMetrics.PackSize.x * 0.08f,
            packSubLabelBottom,
            -_layoutMetrics.PackSize.x * 0.08f,
            -(Mathf.Max(0f, _layoutMetrics.PackSize.y - packSubLabelBottom - packSubLabelHeight)));
        SetCenteredRect(
            packIdentityRoot,
            new Vector2(0f, -_layoutMetrics.PackSize.y * 0.33f),
            new Vector2(_layoutMetrics.PackSize.x * 0.56f, _layoutMetrics.PackSize.y * 0.12f));
        PackOpeningUiUtility.Stretch(packIdentityBackgroundImage.rectTransform, 0f, 0f, 0f, 0f);

        float packBadgeWidth = packIdentityRoot.sizeDelta.x;
        float packBadgeHeight = packIdentityRoot.sizeDelta.y;
        float packBadgePad = packBadgeWidth * 0.08f;
        float packBadgeLogoSize = packBadgeHeight * 0.58f;
        if (packIdentityLogoImage != null)
        {
            SetCenteredRect(
                packIdentityLogoImage.rectTransform,
                new Vector2(-packBadgeWidth * 0.5f + packBadgePad + packBadgeLogoSize * 0.5f, 0f),
                new Vector2(packBadgeLogoSize, packBadgeLogoSize));
        }

        SetCenteredRect(packIdentityText.rectTransform, Vector2.zero, new Vector2(packBadgeWidth - packBadgePad * 2f, packBadgeHeight * 0.72f));

        tearImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        tearImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        tearImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        tearImage.rectTransform.anchoredPosition = _packRestPosition;
        tearImage.rectTransform.sizeDelta = new Vector2(_layoutMetrics.PackSize.x * 1.26f, Mathf.Clamp(_layoutMetrics.PackSize.y * 0.07f, 24f, 34f));
        tearImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);

        hintText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        hintText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        hintText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hintText.rectTransform.sizeDelta = _layoutMetrics.HintSize;
        hintText.rectTransform.anchoredPosition = _layoutMetrics.HintPosition;
        hintText.fontSize = ScaleFont(_layoutMetrics.SafeHeight * landscapeLayout.hintFontNormalized, 22, 40);

        ApplyAbsoluteButtonLayout(stageSkipButton, _layoutMetrics.SkipButtonPosition, _layoutMetrics.SkipButtonSize, false);

        summaryRoot.anchorMin = new Vector2(0.5f, 0.5f);
        summaryRoot.anchorMax = new Vector2(0.5f, 0.5f);
        summaryRoot.pivot = new Vector2(0.5f, 0.5f);
        summaryRoot.sizeDelta = new Vector2(_layoutMetrics.SafeWidth, _layoutMetrics.SafeHeight);
        if (_state != PackOpeningState.Closing)
        {
            summaryRoot.anchoredPosition = _summaryRestPosition;
        }

        float summaryWidth = _layoutMetrics.SafeWidth;
        float summaryHeight = _layoutMetrics.SafeHeight;
        SetCenteredRect(summaryGlowImage.rectTransform, Vector2.zero, new Vector2(summaryWidth * 0.96f, summaryHeight * 0.96f));
        PackOpeningUiUtility.Stretch(summaryBackgroundImage.rectTransform, 0f, 0f, 0f, 0f);
        SetCenteredRect(summaryAccentImage.rectTransform, Vector2.zero, Vector2.zero);
        PackOpeningUiUtility.Stretch(summaryMetricsRoot, 0f, 0f, 0f, 0f);

        float topBottomMargin = summaryHeight * 0.08f;
        float usableHeight = summaryHeight - topBottomMargin * 2f;
        float gridWidth = summaryWidth * 0.69f;
        float contentGap = summaryWidth * 0.02f;
        float previewWidth = summaryWidth * 0.29f;
        float actionHeight = summaryHeight * 0.14f;
        float previewHeight = usableHeight - actionHeight;
        float left = -summaryWidth * 0.5f;
        float bottom = -summaryHeight * 0.5f + topBottomMargin;
        float previewLeft = left + gridWidth + contentGap;
        float previewMetaHeight = summaryHeight * 0.065f;

        SetCenteredRect(summaryHighlightsRoot, new Vector2(left + gridWidth * 0.5f, bottom + usableHeight * 0.5f), new Vector2(gridWidth, usableHeight));
        SetCenteredRect(summaryDetailsRoot, new Vector2(previewLeft + previewWidth * 0.5f, bottom + actionHeight + previewHeight * 0.5f), new Vector2(previewWidth, previewHeight));
        SetCenteredRect(summaryActionRoot, new Vector2(previewLeft + previewWidth * 0.5f, bottom + actionHeight * 0.5f), new Vector2(previewWidth, actionHeight));

        summarySetBadgeText.fontSize = ScaleFont(summaryHeight * 0.034f, 14, 20);
        summaryDetailsHeaderText.fontSize = ScaleFont(summaryHeight * 0.034f, 15, 22);
        summaryDetailsLeftColumnText.fontSize = ScaleFont(summaryHeight * 0.023f, 11, 16);
        summaryBodyText.resizeTextForBestFit = false;
        summaryBodyText.verticalOverflow = VerticalWrapMode.Truncate;
        summaryBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        summaryDetailsHeaderText.alignment = TextAnchor.MiddleCenter;
        summaryDetailsLeftColumnText.alignment = TextAnchor.MiddleCenter;

        for (int index = 0; index < _summaryMetricWidgets.Count; index++)
        {
            SummaryMetricWidget widget = _summaryMetricWidgets[index];
            if (widget == null || widget.Root == null)
            {
                continue;
            }

            widget.Root.gameObject.SetActive(false);
        }

        GridLayoutGroup gridLayout = GetOrAddComponent<GridLayoutGroup>(summaryHighlightsRoot.gameObject);
        int padX = Mathf.RoundToInt(summaryWidth * 0.012f);
        int padY = Mathf.RoundToInt(summaryHeight * 0.02f);
        float thumbGap = Mathf.Round(summaryWidth * 0.008f);
        float availableGridWidth = gridWidth - padX * 2f - thumbGap * (SummaryInspectionColumns - 1);
        float thumbWidth = availableGridWidth / SummaryInspectionColumns;
        float thumbHeight = thumbWidth * 1.42f;
        float maxThumbHeight = (usableHeight - padY * 2f - thumbGap) / 2f;
        if (thumbHeight > maxThumbHeight)
        {
            thumbHeight = maxThumbHeight;
            thumbWidth = thumbHeight / 1.42f;
        }

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = SummaryInspectionColumns;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        gridLayout.spacing = new Vector2(thumbGap, thumbGap);
        gridLayout.padding = new RectOffset(padX, padX, padY, padY);
        gridLayout.cellSize = new Vector2(thumbWidth, thumbHeight);

        for (int index = 0; index < _summaryHighlightWidgets.Count; index++)
        {
            SummaryHighlightWidget widget = _summaryHighlightWidgets[index];
            if (widget == null || widget.Root == null)
            {
                continue;
            }

            widget.BasePosition = Vector2.zero;
            widget.Root.anchorMin = new Vector2(0f, 1f);
            widget.Root.anchorMax = new Vector2(0f, 1f);
            widget.Root.pivot = new Vector2(0.5f, 0.5f);
            widget.Root.sizeDelta = new Vector2(thumbWidth, thumbHeight);
            widget.Root.localScale = Vector3.one;
            PackOpeningUiUtility.Stretch(widget.Glow.rectTransform, -thumbWidth * 0.05f, -thumbHeight * 0.05f, thumbWidth * 0.05f, thumbHeight * 0.05f);
            PackOpeningUiUtility.Stretch(widget.SelectionFrame.rectTransform, -thumbWidth * 0.04f, -thumbHeight * 0.04f, thumbWidth * 0.04f, thumbHeight * 0.04f);
            PackOpeningUiUtility.Stretch(widget.Frame.rectTransform, 0f, 0f, 0f, 0f);
            PackOpeningUiUtility.Stretch(widget.Art.rectTransform, thumbWidth * 0.026f, thumbHeight * 0.026f, -thumbWidth * 0.026f, -thumbHeight * 0.026f);
            SetCenteredRect(widget.NewBadgeRoot, new Vector2(-thumbWidth * 0.27f, thumbHeight * 0.4f), new Vector2(thumbWidth * 0.22f, thumbHeight * 0.078f));
            PackOpeningUiUtility.Stretch(widget.NewBadgeImage.rectTransform, 0f, 0f, 0f, 0f);
            PackOpeningUiUtility.Stretch(widget.NewBadgeText.rectTransform, 0f, 0f, 0f, 0f);
            widget.NewBadgeText.fontSize = ScaleFont(thumbHeight * 0.046f, 8, 11);
        }

        HorizontalLayoutGroup actionLayout = GetOrAddComponent<HorizontalLayoutGroup>(summaryActionRoot.gameObject);
        actionLayout.childAlignment = TextAnchor.MiddleCenter;
        actionLayout.childControlWidth = true;
        actionLayout.childControlHeight = true;
        actionLayout.childForceExpandWidth = true;
        actionLayout.childForceExpandHeight = true;
        actionLayout.spacing = summaryWidth * 0.012f;
        int actionPad = Mathf.RoundToInt(summaryWidth * 0.012f);
        actionLayout.padding = new RectOffset(actionPad, actionPad, actionPad, actionPad);

        float previewCardHeight = previewHeight * 0.82f;
        float previewCardWidth = Mathf.Min(previewWidth * 0.82f, previewCardHeight / 1.42f);
        float previewCardCenterY = previewHeight * 0.06f;
        SetCenteredRect(summaryDetailsBackgroundImage.rectTransform, new Vector2(0f, previewCardCenterY), new Vector2(previewWidth * 0.9f, previewCardHeight * 1.05f));
        SetCenteredRect(summaryPreviewCardView.RectTransform, new Vector2(0f, previewCardCenterY), new Vector2(previewCardWidth, previewCardHeight));
        summaryPreviewCardView.ApplyLayout(new Vector2(previewCardWidth, previewCardHeight));
        SetCenteredRect(summaryDetailsHeaderText.rectTransform, new Vector2(0f, -previewHeight * 0.38f), new Vector2(previewWidth * 0.84f, previewMetaHeight));
        SetCenteredRect(summaryDetailsLeftColumnText.rectTransform, new Vector2(0f, -previewHeight * 0.44f), new Vector2(previewWidth * 0.84f, previewMetaHeight * 0.58f));
        summaryDetailsRightColumnText.gameObject.SetActive(false);

        LayoutElement secondaryLayout = GetOrAddComponent<LayoutElement>(summarySkipButton.gameObject);
        secondaryLayout.minHeight = actionHeight - actionPad * 2f;
        secondaryLayout.preferredHeight = actionHeight - actionPad * 2f;
        secondaryLayout.flexibleWidth = 1f;
        secondaryLayout.preferredWidth = previewWidth * 0.44f;
        secondaryLayout.minWidth = 0f;
        LayoutElement primaryLayout = GetOrAddComponent<LayoutElement>(continueButton.gameObject);
        primaryLayout.minHeight = actionHeight - actionPad * 2f;
        primaryLayout.preferredHeight = actionHeight - actionPad * 2f;
        primaryLayout.flexibleWidth = 1f;
        primaryLayout.preferredWidth = previewWidth * 0.44f;
        primaryLayout.minWidth = 0f;

        RectTransform continueRect = continueButton.GetComponent<RectTransform>();
        continueRect.anchorMin = new Vector2(0.5f, 0.5f);
        continueRect.anchorMax = new Vector2(0.5f, 0.5f);
        continueRect.pivot = new Vector2(0.5f, 0.5f);
        RectTransform skipRect = summarySkipButton.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(0.5f, 0.5f);
        skipRect.anchorMax = new Vector2(0.5f, 0.5f);
        skipRect.pivot = new Vector2(0.5f, 0.5f);

        continueLabel.fontSize = ScaleFont((actionHeight - actionPad * 2f) * (landscapeLayout.buttonFontNormalized * 10f), 16, 22);
        summarySkipLabel.fontSize = ScaleFont((actionHeight - actionPad * 2f) * (landscapeLayout.buttonFontNormalized * 10f), 16, 22);

        if (_state == PackOpeningState.Summary && _summarySelectedCardIndex >= 0)
        {
            ApplySummarySelection(_summarySelectedCardIndex);
        }
    }

    private void ApplyAbsoluteButtonLayout(Button button, Vector2 anchoredPosition, Vector2 size, bool primary)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text label = button == continueButton ? continueLabel : button == summarySkipButton ? summarySkipLabel : stageSkipLabel;
        if (label != null)
        {
            label.fontSize = ScaleFont(size.y * (landscapeLayout.buttonFontNormalized * 10f), 18, 30);
        }

        if (primary)
        {
            Color accent = _currentTheme != null ? _currentTheme.accentColor : new Color(0.29f, 0.72f, 1f, 1f);
            SetButtonFill(button, Color.Lerp(accent, Color.black, 0.42f));
        }
    }

    private LayoutMetrics BuildLayoutMetrics()
    {
        SafeAreaFrame safeArea = GetSafeAreaFrame();
        float packHeight = safeArea.Height * landscapeLayout.packHeightNormalized;
        float packWidth = packHeight * landscapeLayout.packAspectRatio;
        Vector2 packPosition = safeArea.Center + new Vector2(0f, safeArea.Height * landscapeLayout.packCenterYNormalized);
        Vector2 packStartPosition = packPosition + new Vector2(0f, safeArea.Height * landscapeLayout.packStartYOffsetNormalized);

        float summaryWidth = safeArea.Width * landscapeLayout.summaryWidthNormalized;
        float summaryHeight = safeArea.Height * landscapeLayout.summaryHeightNormalized;
        Vector2 summaryPosition = safeArea.Center + new Vector2(0f, safeArea.Height * landscapeLayout.summaryCenterYNormalized);
        Vector2 summaryIntroPosition = summaryPosition + new Vector2(0f, -safeArea.Height * landscapeLayout.summaryIntroYOffsetNormalized);

        Vector2 hintSize = new Vector2(safeArea.Width * landscapeLayout.hintWidthNormalized, safeArea.Height * landscapeLayout.hintHeightNormalized);
        Vector2 hintPosition = new Vector2(
            safeArea.Center.x,
            safeArea.Bottom + safeArea.Height * landscapeLayout.bottomPromptMarginNormalized + hintSize.y * 0.5f);

        Vector2 skipButtonSize = new Vector2(safeArea.Width * landscapeLayout.skipButtonWidthNormalized, safeArea.Height * landscapeLayout.skipButtonHeightNormalized);
        Vector2 skipButtonPosition = new Vector2(
            safeArea.Right - safeArea.Width * landscapeLayout.sideButtonMarginNormalized - skipButtonSize.x * 0.5f,
            safeArea.Top - safeArea.Height * landscapeLayout.topButtonMarginNormalized - skipButtonSize.y * 0.5f);

        Vector2 cardSize = new Vector2((safeArea.Height * landscapeLayout.fanCardHeightNormalized) / 1.42f, safeArea.Height * landscapeLayout.fanCardHeightNormalized);
        Vector2 summaryButtonSize = new Vector2(summaryWidth * landscapeLayout.summaryButtonWidthNormalized, summaryHeight * landscapeLayout.summaryButtonHeightNormalized);

        return new LayoutMetrics
        {
            SafeCenter = safeArea.Center,
            SafeWidth = safeArea.Width,
            SafeHeight = safeArea.Height,
            SafeLeft = safeArea.Left,
            SafeRight = safeArea.Right,
            SafeTop = safeArea.Top,
            SafeBottom = safeArea.Bottom,
            PackSize = new Vector2(packWidth, packHeight),
            PackPosition = packPosition,
            PackStartPosition = packStartPosition,
            FanCenter = safeArea.Center + new Vector2(0f, safeArea.Height * landscapeLayout.fanCenterYNormalized),
            FanWidth = safeArea.Width * landscapeLayout.fanWidthNormalized,
            CardSize = cardSize,
            FanArcDepth = safeArea.Height * landscapeLayout.fanArcDepthNormalized,
            SummarySize = new Vector2(summaryWidth, summaryHeight),
            SummaryPosition = summaryPosition,
            SummaryIntroPosition = summaryIntroPosition,
            HintSize = hintSize,
            HintPosition = hintPosition,
            SkipButtonSize = skipButtonSize,
            SkipButtonPosition = skipButtonPosition,
            SummaryButtonSize = summaryButtonSize,
            SummaryButtonBottom = -summaryHeight * 0.5f + summaryHeight * landscapeLayout.summaryButtonBottomNormalized + summaryButtonSize.y * 0.5f,
        };
    }

    private SafeAreaFrame GetSafeAreaFrame()
    {
        RectTransform root = transform as RectTransform;
        float rootWidth = root != null && root.rect.width > 0f ? root.rect.width : Screen.width;
        float rootHeight = root != null && root.rect.height > 0f ? root.rect.height : Screen.height;
        Rect safeArea = Screen.safeArea;
        float scaleX = rootWidth / Mathf.Max(1f, Screen.width);
        float scaleY = rootHeight / Mathf.Max(1f, Screen.height);
        float leftInset = safeArea.xMin * scaleX;
        float rightInset = (Screen.width - safeArea.xMax) * scaleX;
        float bottomInset = safeArea.yMin * scaleY;
        float topInset = (Screen.height - safeArea.yMax) * scaleY;
        float left = -rootWidth * 0.5f + leftInset;
        float right = rootWidth * 0.5f - rightInset;
        float bottom = -rootHeight * 0.5f + bottomInset;
        float top = rootHeight * 0.5f - topInset;
        return new SafeAreaFrame(left, right, bottom, top);
    }

    private void EnsureStructure()
    {
        if (_uiBuilt &&
            overlayGroup != null &&
            overlayPrimary != null &&
            overlaySecondary != null &&
            inputCatcherButton != null &&
            packRoot != null &&
            slotsRoot != null &&
            burstRoot != null &&
            summaryRoot != null &&
            summaryFeaturedLabelText != null &&
            summaryActionRoot != null &&
            summaryDetailsRoot != null &&
            summaryDetailsBackgroundImage != null &&
            summaryDetailsHeaderText != null &&
            summaryDetailsLeftColumnText != null &&
            summaryDetailsRightColumnText != null &&
            summaryPreviewCardView != null)
        {
            return;
        }

        RectTransform root = transform as RectTransform;
        if (root == null)
        {
            root = gameObject.AddComponent<RectTransform>();
        }

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);
        gameObject.layer = 5;

        overlayGroup = overlayGroup != null ? overlayGroup : GetComponent<CanvasGroup>();
        if (overlayGroup == null)
        {
            overlayGroup = gameObject.AddComponent<CanvasGroup>();
        }

        overlayPrimary = overlayPrimary ?? PackOpeningUiUtility.CreateImage(root, "OverlayPrimary", new Color(0f, 0f, 0f, 0.88f));
        PackOpeningUiUtility.Stretch(overlayPrimary.rectTransform, 0f, 0f, 0f, 0f);

        overlayTint = overlayTint ?? PackOpeningUiUtility.CreateImage(root, "OverlayTint", new Color(0f, 0f, 0f, 0f));
        PackOpeningUiUtility.Stretch(overlayTint.rectTransform, 0f, 0f, 0f, 0f);

        overlaySecondary = overlaySecondary ?? PackOpeningUiUtility.CreateImage(root, "OverlaySecondary", new Color(0f, 0f, 0f, 0.48f));
        PackOpeningUiUtility.Stretch(overlaySecondary.rectTransform, 0f, 0f, 0f, 0f);

        if (inputCatcherButton == null)
        {
            Image inputImage = PackOpeningUiUtility.CreateImage(root, "InputCatcher", new Color(1f, 1f, 1f, 0.001f));
            PackOpeningUiUtility.Stretch(inputImage.rectTransform, 0f, 0f, 0f, 0f);
            inputImage.raycastTarget = true;
            inputCatcherButton = inputImage.GetComponent<Button>();
            if (inputCatcherButton == null)
            {
                inputCatcherButton = inputImage.gameObject.AddComponent<Button>();
            }

            inputCatcherButton.targetGraphic = inputImage;
            Navigation nav = inputCatcherButton.navigation;
            nav.mode = Navigation.Mode.None;
            inputCatcherButton.navigation = nav;
        }

        if (packRoot == null)
        {
            packRoot = PackOpeningUiUtility.FindOrCreateRect(root, "PackRoot");
            packRoot.anchorMin = new Vector2(0.5f, 0.5f);
            packRoot.anchorMax = new Vector2(0.5f, 0.5f);
            packRoot.pivot = new Vector2(0.5f, 0.5f);
            packRoot.anchoredPosition = _packRestPosition;
            packRoot.sizeDelta = new Vector2(270f, 378f);
        }

        if (packShadowImage == null)
        {
            packShadowImage = PackOpeningUiUtility.CreateImage(packRoot, "PackShadow", new Color(0f, 0f, 0f, 0.38f));
            PackOpeningUiUtility.Stretch(packShadowImage.rectTransform, -16f, -18f, 16f, 18f);
        }

        if (packGlowImage == null)
        {
            packGlowImage = PackOpeningUiUtility.CreateImage(packRoot, "PackGlow", new Color(1f, 1f, 1f, 0.12f));
            PackOpeningUiUtility.Stretch(packGlowImage.rectTransform, -28f, -28f, 28f, 28f);
        }

        if (packRimLightImage == null)
        {
            packRimLightImage = PackOpeningUiUtility.CreateImage(packRoot, "PackRimLight", new Color(1f, 1f, 1f, 0.14f));
            PackOpeningUiUtility.Stretch(packRimLightImage.rectTransform, 6f, 6f, -6f, -6f);
        }

        if (packImage == null)
        {
            packImage = PackOpeningUiUtility.CreateImage(packRoot, "PackImage", new Color(0.12f, 0.17f, 0.22f, 1f));
            PackOpeningUiUtility.Stretch(packImage.rectTransform, 0f, 0f, 0f, 0f);
            packImage.preserveAspect = true;
        }

        if (packShineImage == null)
        {
            packShineImage = PackOpeningUiUtility.CreateImage(packRoot, "PackShine", new Color(1f, 1f, 1f, 0.1f));
        }

        if (packLabelText == null)
        {
            packLabelText = PackOpeningUiUtility.CreateText(packRoot, "PackLabel", 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            PackOpeningUiUtility.Stretch(packLabelText.rectTransform, 22f, 120f, -22f, -120f);
        }

        if (packSubLabelText == null)
        {
            packSubLabelText = PackOpeningUiUtility.CreateText(packRoot, "PackSubLabel", 18, TextAnchor.LowerCenter, FontStyle.Bold);
            packSubLabelText.color = new Color(0.88f, 0.93f, 1f, 0.92f);
            PackOpeningUiUtility.Stretch(packSubLabelText.rectTransform, 20f, 16f, -20f, 110f);
        }

        if (packIdentityRoot == null)
        {
            packIdentityRoot = PackOpeningUiUtility.FindOrCreateRect(packRoot, "PackIdentityRoot");
        }

        if (packIdentityBackgroundImage == null)
        {
            packIdentityBackgroundImage = PackOpeningUiUtility.CreateImage(packIdentityRoot, "PackIdentityBackground", new Color(0f, 0f, 0f, 0.5f));
            PackOpeningUiUtility.Stretch(packIdentityBackgroundImage.rectTransform, 0f, 0f, 0f, 0f);
        }

        if (packIdentityLogoImage == null)
        {
            packIdentityLogoImage = PackOpeningUiUtility.CreateImage(packIdentityRoot, "PackIdentityLogo", Color.white);
        }

        if (packIdentityText == null)
        {
            packIdentityText = PackOpeningUiUtility.CreateText(packIdentityRoot, "PackIdentityText", 18, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        packShadowImage.sprite = PackOpeningUiUtility.GetSoftCircleSprite();
        packGlowImage.sprite = PackOpeningUiUtility.GetSoftCircleSprite();
        packRimLightImage.sprite = PackOpeningUiUtility.GetSoftFrameSprite();
        packShineImage.sprite = PackOpeningUiUtility.GetShineSprite();
        packShineImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -17f);
        packShadowImage.rectTransform.SetAsFirstSibling();
        packGlowImage.rectTransform.SetSiblingIndex(packShadowImage.rectTransform.GetSiblingIndex() + 1);
        packImage.rectTransform.SetSiblingIndex(packGlowImage.rectTransform.GetSiblingIndex() + 1);
        packRimLightImage.rectTransform.SetSiblingIndex(packImage.rectTransform.GetSiblingIndex() + 1);
        packShineImage.rectTransform.SetSiblingIndex(packRimLightImage.rectTransform.GetSiblingIndex() + 1);
        packLabelText.rectTransform.SetAsLastSibling();
        packSubLabelText.rectTransform.SetAsLastSibling();
        packIdentityRoot.SetAsLastSibling();

        if (slotsRoot == null)
        {
            slotsRoot = PackOpeningUiUtility.FindOrCreateRect(root, "SlotsRoot");
            PackOpeningUiUtility.Stretch(slotsRoot, 0f, 0f, 0f, 0f);
        }

        if (burstRoot == null)
        {
            burstRoot = PackOpeningUiUtility.FindOrCreateRect(root, "BurstRoot");
            PackOpeningUiUtility.Stretch(burstRoot, 0f, 0f, 0f, 0f);
        }

        if (flashImage == null)
        {
            flashImage = PackOpeningUiUtility.CreateImage(root, "Flash", new Color(1f, 1f, 1f, 0f));
            PackOpeningUiUtility.Stretch(flashImage.rectTransform, 0f, 0f, 0f, 0f);
        }

        if (tearImage == null)
        {
            tearImage = PackOpeningUiUtility.CreateImage(root, "Tear", new Color(1f, 1f, 1f, 0f));
            tearImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            tearImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            tearImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tearImage.rectTransform.anchoredPosition = _packRestPosition;
            tearImage.rectTransform.sizeDelta = new Vector2(340f, 26f);
            tearImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        }

        if (hintText == null)
        {
            hintText = PackOpeningUiUtility.CreateText(root, "HintText", 24, TextAnchor.MiddleCenter, FontStyle.Bold);
            hintText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            hintText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            hintText.rectTransform.pivot = new Vector2(0.5f, 0f);
            hintText.rectTransform.anchoredPosition = new Vector2(0f, 180f);
            hintText.rectTransform.sizeDelta = new Vector2(420f, 52f);
        }

        if (stageSkipButton == null)
        {
            stageSkipButton = CreateActionButton(root, "StageSkipButton", "Skip", new Vector2(-26f, -26f), new Vector2(120f, 42f), anchor: new Vector2(1f, 1f), out stageSkipLabel);
        }

        if (summaryRoot == null)
        {
            summaryRoot = FindOrCreateSummaryRect(root, "InspectionRoot", "SummaryRoot");
            summaryRoot.anchorMin = new Vector2(0.5f, 0.5f);
            summaryRoot.anchorMax = new Vector2(0.5f, 0.5f);
            summaryRoot.pivot = new Vector2(0.5f, 0.5f);
            summaryRoot.anchoredPosition = Vector2.zero;
            summaryRoot.sizeDelta = new Vector2(640f, 340f);
        }
        else
        {
            summaryRoot.name = "InspectionRoot";
        }

        summaryCanvasGroup = summaryCanvasGroup ?? summaryRoot.GetComponent<CanvasGroup>();
        if (summaryCanvasGroup == null)
        {
            summaryCanvasGroup = summaryRoot.gameObject.AddComponent<CanvasGroup>();
        }

        if (summaryBackgroundImage == null)
        {
            summaryBackgroundImage = PackOpeningUiUtility.CreateImage(summaryRoot, "SummaryBackground", new Color(0.07f, 0.11f, 0.16f, 0.96f));
            PackOpeningUiUtility.Stretch(summaryBackgroundImage.rectTransform, 0f, 0f, 0f, 0f);
        }

        if (summaryGlowImage == null)
        {
            summaryGlowImage = PackOpeningUiUtility.CreateImage(summaryRoot, "SummaryGlow", new Color(1f, 1f, 1f, 0.16f));
            summaryGlowImage.rectTransform.SetAsFirstSibling();
        }

        if (summaryAccentImage == null)
        {
            summaryAccentImage = PackOpeningUiUtility.CreateImage(summaryRoot, "SummaryAccent", new Color(1f, 1f, 1f, 1f));
        }

        if (summaryRewardLabelText == null)
        {
            summaryRewardLabelText = PackOpeningUiUtility.CreateText(summaryRoot, "SummaryRewardLabel", 18, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        if (summaryTitleText == null)
        {
            summaryTitleText = PackOpeningUiUtility.CreateText(summaryRoot, "SummaryTitle", 28, TextAnchor.UpperLeft, FontStyle.Bold);
            PackOpeningUiUtility.Stretch(summaryTitleText.rectTransform, 24f, 270f, -24f, -16f);
        }

        if (summaryStatsText == null)
        {
            summaryStatsText = PackOpeningUiUtility.CreateText(summaryRoot, "SummaryStats", 20, TextAnchor.UpperLeft, FontStyle.Bold);
            PackOpeningUiUtility.Stretch(summaryStatsText.rectTransform, 24f, 234f, -24f, -46f);
        }

        if (summaryFeaturedLabelText == null)
        {
            summaryFeaturedLabelText = PackOpeningUiUtility.CreateText(summaryRoot, "SummaryFeaturedLabel", 18, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        if (summaryBodyText == null)
        {
            summaryBodyText = PackOpeningUiUtility.CreateText(summaryRoot, "SummaryBody", 18, TextAnchor.UpperLeft, FontStyle.Normal);
            PackOpeningUiUtility.Stretch(summaryBodyText.rectTransform, 24f, 78f, -24f, -84f);
            summaryBodyText.resizeTextForBestFit = true;
            summaryBodyText.resizeTextMinSize = 14;
            summaryBodyText.resizeTextMaxSize = 18;
            summaryBodyText.verticalOverflow = VerticalWrapMode.Overflow;
            summaryBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        if (summaryHeroPackImage == null)
        {
            summaryHeroPackImage = PackOpeningUiUtility.CreateImage(summaryRoot, "SummaryHeroPack", new Color(1f, 1f, 1f, 1f));
            summaryHeroPackImage.preserveAspect = true;
        }

        if (summarySetBadgeRoot == null)
        {
            summarySetBadgeRoot = PackOpeningUiUtility.FindOrCreateRect(summaryRoot, "SummarySetBadge");
        }

        if (summarySetBadgeBackgroundImage == null)
        {
            summarySetBadgeBackgroundImage = PackOpeningUiUtility.CreateImage(summarySetBadgeRoot, "Background", new Color(0f, 0f, 0f, 0.42f));
            PackOpeningUiUtility.Stretch(summarySetBadgeBackgroundImage.rectTransform, 0f, 0f, 0f, 0f);
        }

        if (summarySetLogoImage == null)
        {
            summarySetLogoImage = PackOpeningUiUtility.CreateImage(summarySetBadgeRoot, "Logo", Color.white);
        }

        if (summarySetBadgeText == null)
        {
            summarySetBadgeText = PackOpeningUiUtility.CreateText(summarySetBadgeRoot, "Text", 18, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        if (summaryMetricsRoot == null)
        {
            summaryMetricsRoot = PackOpeningUiUtility.FindOrCreateRect(summaryRoot, "SummaryMetricsRoot");
            PackOpeningUiUtility.Stretch(summaryMetricsRoot, 0f, 0f, 0f, 0f);
        }

        if (summaryHighlightsRoot == null)
        {
            summaryHighlightsRoot = FindOrCreateSummaryRect(summaryRoot, "GridPanel", "SummaryHighlightsRoot");
            PackOpeningUiUtility.Stretch(summaryHighlightsRoot, 0f, 0f, 0f, 0f);
        }
        else
        {
            summaryHighlightsRoot.name = "GridPanel";
        }

        if (summaryDetailsRoot == null)
        {
            summaryDetailsRoot = FindOrCreateSummaryRect(summaryRoot, "PreviewPanel", "SummaryDetailsRoot");
        }
        else
        {
            summaryDetailsRoot.name = "PreviewPanel";
        }

        if (summaryActionRoot == null)
        {
            summaryActionRoot = FindOrCreateSummaryRect(summaryRoot, "ActionPanel");
        }
        else
        {
            summaryActionRoot.name = "ActionPanel";
        }

        if (summaryDetailsBackgroundImage == null)
        {
            summaryDetailsBackgroundImage = PackOpeningUiUtility.CreateImage(summaryDetailsRoot, "Background", new Color(0f, 0f, 0f, 0.68f));
            PackOpeningUiUtility.Stretch(summaryDetailsBackgroundImage.rectTransform, 0f, 0f, 0f, 0f);
        }

        if (summaryDetailsHeaderText == null)
        {
            summaryDetailsHeaderText = PackOpeningUiUtility.CreateText(summaryDetailsRoot, "Header", 16, TextAnchor.UpperLeft, FontStyle.Bold);
        }
        summaryDetailsHeaderText.horizontalOverflow = HorizontalWrapMode.Wrap;
        summaryDetailsHeaderText.verticalOverflow = VerticalWrapMode.Truncate;

        if (summaryDetailsLeftColumnText == null)
        {
            summaryDetailsLeftColumnText = PackOpeningUiUtility.CreateText(summaryDetailsRoot, "LeftColumn", 16, TextAnchor.UpperLeft, FontStyle.Normal);
        }
        summaryDetailsLeftColumnText.horizontalOverflow = HorizontalWrapMode.Wrap;
        summaryDetailsLeftColumnText.verticalOverflow = VerticalWrapMode.Overflow;

        if (summaryDetailsRightColumnText == null)
        {
            summaryDetailsRightColumnText = PackOpeningUiUtility.CreateText(summaryDetailsRoot, "RightColumn", 16, TextAnchor.UpperLeft, FontStyle.Normal);
        }
        summaryDetailsRightColumnText.horizontalOverflow = HorizontalWrapMode.Wrap;
        summaryDetailsRightColumnText.verticalOverflow = VerticalWrapMode.Overflow;

        if (summaryPreviewCardView == null)
        {
            GameObject previewCardObject = new GameObject("SummaryPreviewCard", typeof(RectTransform), typeof(CanvasGroup), typeof(PackRevealCardView));
            previewCardObject.layer = 5;
            RectTransform previewCardRect = previewCardObject.GetComponent<RectTransform>();
            previewCardRect.SetParent(summaryDetailsRoot, false);
            previewCardRect.localScale = Vector3.one;
            summaryPreviewCardView = previewCardObject.GetComponent<PackRevealCardView>();
            summaryPreviewCardView.EnsureStructure();
        }

        summaryDetailsRoot.gameObject.SetActive(false);
        summaryActionRoot.gameObject.SetActive(false);

        if (continueButton == null)
        {
            continueButton = CreateActionButton(summaryActionRoot, "ContinueButton", "Continue", new Vector2(-98f, 26f), new Vector2(160f, 42f), anchor: new Vector2(0.5f, 0f), out continueLabel);
        }
        else if (continueButton.transform.parent != summaryActionRoot)
        {
            continueButton.transform.SetParent(summaryActionRoot, false);
        }

        if (summarySkipButton == null)
        {
            summarySkipButton = CreateActionButton(summaryActionRoot, "SummarySkipButton", "Skip", new Vector2(98f, 26f), new Vector2(160f, 42f), anchor: new Vector2(0.5f, 0f), out summarySkipLabel);
        }
        else if (summarySkipButton.transform.parent != summaryActionRoot)
        {
            summarySkipButton.transform.SetParent(summaryActionRoot, false);
        }

        EnsureSummaryMetricWidgets();
        EnsureSummaryHighlightWidgets();

        _uiBuilt = true;
    }

    private Button CreateActionButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, out Text labelText)
    {
        Image background = PackOpeningUiUtility.CreateImage(parent, name, new Color(1f, 1f, 1f, 0.11f));
        RectTransform rect = background.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        background.raycastTarget = true;

        Button button = background.GetComponent<Button>();
        if (button == null)
        {
            button = background.gameObject.AddComponent<Button>();
        }

        button.targetGraphic = background;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;

        labelText = PackOpeningUiUtility.CreateText(rect, "Label", 20, TextAnchor.MiddleCenter, FontStyle.Bold);
        PackOpeningUiUtility.Stretch(labelText.rectTransform, 0f, 0f, 0f, 0f);
        labelText.text = label;
        return button;
    }

    private void EnsureSummaryMetricWidgets()
    {
        while (_summaryMetricWidgets.Count < SummaryMetricCount)
        {
            RectTransform root = PackOpeningUiUtility.FindOrCreateRect(summaryMetricsRoot, $"Metric_{_summaryMetricWidgets.Count + 1}");
            Image background = PackOpeningUiUtility.CreateImage(root, "Background", new Color(1f, 1f, 1f, 0.08f));
            Text valueText = PackOpeningUiUtility.CreateText(root, "Value", 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            Text labelText = PackOpeningUiUtility.CreateText(root, "Label", 16, TextAnchor.MiddleCenter, FontStyle.Normal);
            _summaryMetricWidgets.Add(new SummaryMetricWidget
            {
                Root = root,
                Background = background,
                ValueText = valueText,
                LabelText = labelText,
            });
        }
    }

    private void EnsureSummaryHighlightWidgets()
    {
        while (_summaryHighlightWidgets.Count < SummaryInspectionSlotCount)
        {
            int widgetIndex = _summaryHighlightWidgets.Count;
            RectTransform root = PackOpeningUiUtility.FindOrCreateRect(summaryHighlightsRoot, $"Highlight_{widgetIndex + 1}");
            Image glow = PackOpeningUiUtility.CreateImage(root, "Glow", new Color(1f, 1f, 1f, 0.14f));
            Image selectionFrame = PackOpeningUiUtility.CreateImage(root, "SelectionFrame", new Color(1f, 1f, 1f, 0f));
            Image frame = PackOpeningUiUtility.CreateImage(root, "Frame", new Color(0.18f, 0.23f, 0.29f, 1f));
            Image art = PackOpeningUiUtility.CreateImage(root, "Art", Color.white);
            art.preserveAspect = true;
            Text labelText = PackOpeningUiUtility.CreateText(root, "Label", 16, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform badgeRoot = PackOpeningUiUtility.FindOrCreateRect(root, "NewBadge");
            Image badgeImage = PackOpeningUiUtility.CreateImage(badgeRoot, "Background", new Color(1f, 0.74f, 0.15f, 0.95f));
            Text badgeText = PackOpeningUiUtility.CreateText(badgeRoot, "Text", 14, TextAnchor.MiddleCenter, FontStyle.Bold);
            badgeText.text = "NEW";
            badgeText.color = new Color(0.18f, 0.08f, 0f, 1f);

            frame.raycastTarget = true;
            selectionFrame.sprite = PackOpeningUiUtility.GetSoftFrameSprite();
            selectionFrame.raycastTarget = false;

            Button button = root.GetComponent<Button>();
            if (button == null)
            {
                button = root.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnSummaryCardPressed(widgetIndex));

            _summaryHighlightWidgets.Add(new SummaryHighlightWidget
            {
                Root = root,
                Button = button,
                Glow = glow,
                SelectionFrame = selectionFrame,
                Frame = frame,
                Art = art,
                LabelText = labelText,
                NewBadgeRoot = badgeRoot,
                NewBadgeImage = badgeImage,
                NewBadgeText = badgeText,
            });
        }
    }

    private void WireButtons()
    {
        if (_buttonsBound)
        {
            return;
        }

        inputCatcherButton.onClick.AddListener(OnOverlayTapped);
        stageSkipButton.onClick.AddListener(OnStageSkipPressed);
        continueButton.onClick.AddListener(OnContinuePressed);
        summarySkipButton.onClick.AddListener(OnSummarySkipPressed);
        _buttonsBound = true;
    }

    private void ApplyPackIdentity(bool usingArt)
    {
        if (packIdentityRoot == null)
        {
            return;
        }

        string identityText = ResolveSetIdentityText();
        bool hasLogo = _currentTheme != null && _currentTheme.setLogoSprite != null;
        bool showIdentity = usingArt && (hasLogo || !string.IsNullOrWhiteSpace(identityText));
        packIdentityRoot.gameObject.SetActive(showIdentity);
        if (!showIdentity)
        {
            return;
        }

        packIdentityLogoImage.gameObject.SetActive(hasLogo);
        packIdentityLogoImage.sprite = hasLogo ? _currentTheme.setLogoSprite : null;
        packIdentityText.text = identityText;
        packIdentityText.alignment = hasLogo ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
    }

    private void ApplyPackDepthTreatment(float breatheNormalized, float hoverNormalized, float driftNormalized, float shineNormalized)
    {
        if (_currentTheme == null || packShadowImage == null || packGlowImage == null)
        {
            return;
        }

        float breathe = Mathf.Clamp01(breatheNormalized);
        float hover = Mathf.Clamp01(hoverNormalized);
        float drift = Mathf.Clamp(driftNormalized, -1f, 1f);
        float shine = Mathf.Clamp01(shineNormalized);
        Vector2 packSize = _layoutMetrics.PackSize;

        if (packShadowImage != null)
        {
            packShadowImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.2f, 0.34f, 1f - hover * 0.55f));
            packShadowImage.rectTransform.anchoredPosition = new Vector2(
                -drift * packSize.x * 0.018f,
                packSize.y * landscapeLayout.packShadowYOffsetNormalized - hover * packSize.y * 0.025f);
            packShadowImage.rectTransform.localScale = new Vector3(
                1.04f - hover * 0.08f,
                0.94f - hover * 0.14f,
                1f);
        }

        if (packGlowImage != null)
        {
            packGlowImage.color = new Color(
                _currentTheme.glowColor.r,
                _currentTheme.glowColor.g,
                _currentTheme.glowColor.b,
                Mathf.Lerp(0.12f, 0.24f, breathe * 0.75f + hover * 0.25f));
            packGlowImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.04f, breathe);
        }

        if (packRimLightImage != null)
        {
            packRimLightImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.06f, 0.16f, breathe * 0.65f + shine * 0.35f));
        }

        if (packShineImage != null)
        {
            packShineImage.gameObject.SetActive(_currentTheme.packArt != null);
            packShineImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.025f, 0.11f, shine));
            packShineImage.rectTransform.anchoredPosition = new Vector2(
                Mathf.Lerp(-packSize.x * 0.08f, packSize.x * 0.08f, shine),
                -packSize.y * 0.01f + (breathe - 0.5f) * packSize.y * 0.02f);
            packShineImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.06f, breathe);
        }
    }

    private void ApplySummaryIdentity()
    {
        string identityText = ResolveSetIdentityText();
        bool hasLogo = _currentTheme != null && _currentTheme.setLogoSprite != null;
        bool showIdentity = hasLogo || !string.IsNullOrWhiteSpace(identityText);
        summarySetBadgeRoot.gameObject.SetActive(showIdentity);
        summarySetLogoImage.gameObject.SetActive(hasLogo);
        summarySetLogoImage.sprite = hasLogo ? _currentTheme.setLogoSprite : null;
        summarySetBadgeText.text = identityText;
        summarySetBadgeText.alignment = hasLogo ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
    }

    private string ResolveSetIdentityText()
    {
        if (_currentTheme != null && !string.IsNullOrWhiteSpace(_currentTheme.setLogoText))
        {
            return _currentTheme.setLogoText.Trim();
        }

        if (_currentResult != null && !string.IsNullOrWhiteSpace(_currentResult.SetId))
        {
            return _currentResult.SetId.Trim().ToUpperInvariant();
        }

        return string.Empty;
    }

    private void PrepareSummaryMetrics()
    {
        EnsureSummaryMetricWidgets();
        SetSummaryMetric(0, _currentResult.TotalCardCount.ToString(), "Total");
        SetSummaryMetric(1, _currentResult.NewCardCount.ToString(), "New");
        SetSummaryMetric(2, _currentResult.RareCardCount.ToString(), "Rare+");
    }

    private void SetSummaryMetric(int index, string value, string label)
    {
        if (index < 0 || index >= _summaryMetricWidgets.Count)
        {
            return;
        }

        SummaryMetricWidget widget = _summaryMetricWidgets[index];
        widget.ValueText.text = value;
        widget.LabelText.text = label;
        widget.Root.gameObject.SetActive(true);
    }

    private void PrepareSummaryHighlightsModel()
    {
        EnsureSummaryHighlightWidgets();
        _summaryHighlightEntries.Clear();
        _summaryHighlightSpriteTasks.Clear();
        _summarySelectedCardIndex = -1;

        if (_currentResult == null || _currentResult.Cards == null)
        {
            return;
        }

        int bestScore = int.MinValue;
        for (int index = 0; index < _currentResult.Cards.Count; index++)
        {
            PackOpeningResult.CardEntry entry = _currentResult.Cards[index];
            if (entry == null)
            {
                continue;
            }

            _summaryHighlightEntries.Add(entry);
            _summaryHighlightSpriteTasks.Add(entry.LoadSpriteAsync());

            int score = GetHighlightScore(entry, index);
            if (score > bestScore)
            {
                bestScore = score;
                _summarySelectedCardIndex = _summaryHighlightEntries.Count - 1;
            }
        }

        if (_summarySelectedCardIndex < 0 && _summaryHighlightEntries.Count > 0)
        {
            _summarySelectedCardIndex = 0;
        }
    }

    private IEnumerator PopulateSummaryHighlights()
    {
        EnsureSummaryHighlightWidgets();

        for (int index = 0; index < _summaryHighlightWidgets.Count; index++)
        {
            SummaryHighlightWidget widget = _summaryHighlightWidgets[index];
            bool active = index < _summaryHighlightEntries.Count;
            widget.Root.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            PackOpeningResult.CardEntry entry = _summaryHighlightEntries[index];
            Task<Sprite> task = index < _summaryHighlightSpriteTasks.Count ? _summaryHighlightSpriteTasks[index] : null;
            if (task != null && !task.IsCompleted)
            {
                yield return WaitForTaskOrTimeout(task, _fastForward ? 0.03f : 0.08f);
            }

            Sprite sprite = GetTaskResult(task) ?? PackOpeningUiUtility.GetFallbackCardSprite();
            Color rarityColor = _currentTheme.GetRarityColor(entry.Rarity);
            widget.Sprite = sprite;
            widget.Art.sprite = sprite;
            widget.Art.color = Color.white;
            widget.LabelText.gameObject.SetActive(false);
            widget.Frame.color = Color.Lerp(new Color(0.11f, 0.14f, 0.18f, 0.72f), rarityColor, entry.IsRare ? 0.34f : 0.18f);
            widget.Glow.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, entry.IsRare ? 0.07f : 0.03f);
            widget.NewBadgeRoot.gameObject.SetActive(entry.IsNew);
            widget.SelectionFrame.gameObject.SetActive(false);
            widget.Root.localScale = Vector3.one;
        }

        ApplySummarySelection(Mathf.Clamp(_summarySelectedCardIndex, 0, Mathf.Max(0, _summaryHighlightEntries.Count - 1)));
    }

private void ApplySummaryWidgetTheme()
    {
        for (int index = 0; index < _summaryMetricWidgets.Count; index++)
        {
            SummaryMetricWidget widget = _summaryMetricWidgets[index];
            if (widget == null)
            {
                continue;
            }

            widget.Background.color = new Color(_currentTheme.packTint.r, _currentTheme.packTint.g, _currentTheme.packTint.b, 0.78f);
            widget.ValueText.color = _currentTheme.summaryTextColor;
            widget.LabelText.color = WithAlpha(_currentTheme.summaryTextColor, 0.76f);
        }

        for (int index = 0; index < _summaryHighlightWidgets.Count; index++)
        {
            SummaryHighlightWidget widget = _summaryHighlightWidgets[index];
            if (widget == null)
            {
                continue;
            }

            widget.LabelText.color = _currentTheme.summaryTextColor;
            widget.SelectionFrame.color = Color.clear;
            widget.NewBadgeImage.color = new Color(0.74f, 0.82f, 0.92f, 0.2f);
            widget.NewBadgeText.color = new Color(0.9f, 0.95f, 1f, 0.82f);
        }
    }

    private static int GetHighlightScore(PackOpeningResult.CardEntry entry, int originalIndex)
    {
        if (entry == null)
        {
            return int.MinValue;
        }

        int rarityScore = GetRarityPriority(entry.Rarity) * 100;
        int newBonus = entry.IsNew ? 25 : 0;
        int copyPenalty = entry.Count > 1 ? Mathf.Min(8, entry.Count) : 0;
        return rarityScore + newBonus - copyPenalty - originalIndex;
    }

    private static int GetRarityPriority(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.SEC:
                return 6;
            case Rarity.P:
                return 5;
            case Rarity.SR:
                return 4;
            case Rarity.R:
                return 3;
            case Rarity.U:
                return 2;
            case Rarity.C:
                return 1;
            case Rarity.None:
            default:
                return 0;
        }
    }

private void ApplySummarySelection(int index)
    {
        if (_summaryHighlightEntries.Count == 0)
        {
            _summarySelectedCardIndex = -1;
            return;
        }

        _summarySelectedCardIndex = Mathf.Clamp(index, 0, _summaryHighlightEntries.Count - 1);
        for (int widgetIndex = 0; widgetIndex < _summaryHighlightWidgets.Count; widgetIndex++)
        {
            SummaryHighlightWidget widget = _summaryHighlightWidgets[widgetIndex];
            if (widget == null || widget.Root == null || !widget.Root.gameObject.activeSelf)
            {
                continue;
            }

            bool selected = widgetIndex == _summarySelectedCardIndex;
            PackOpeningResult.CardEntry widgetEntry = widgetIndex < _summaryHighlightEntries.Count ? _summaryHighlightEntries[widgetIndex] : null;
            Color rarityColor = widgetEntry != null ? _currentTheme.GetRarityColor(widgetEntry.Rarity) : _currentTheme.accentColor;
            Color baseFrameColor = new Color(0.12f, 0.15f, 0.2f, 0.72f);
            widget.SelectionFrame.gameObject.SetActive(selected);
            widget.SelectionFrame.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, selected ? 0.38f : 0f);
            widget.Frame.color = selected
                ? Color.Lerp(baseFrameColor, rarityColor, widgetEntry != null && widgetEntry.IsRare ? 0.38f : 0.24f)
                : Color.Lerp(baseFrameColor, rarityColor, widgetEntry != null && widgetEntry.IsRare ? 0.18f : 0.08f);
            widget.Glow.color = new Color(
                rarityColor.r,
                rarityColor.g,
                rarityColor.b,
                selected ? (widgetEntry != null && widgetEntry.IsRare ? 0.09f : 0.05f) : (widgetEntry != null && widgetEntry.IsRare ? 0.035f : 0.015f));
            widget.Art.color = selected
                ? Color.white
                : new Color(0.9f, 0.93f, 0.97f, widgetEntry != null && widgetEntry.IsRare ? 0.96f : 0.92f);
            widget.Root.localScale = Vector3.one;
            widget.NewBadgeRoot.localScale = Vector3.one;
        }

        UpdateSummaryPreview(_summarySelectedCardIndex);
    }

    private void UpdateSummaryPreview(int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= _summaryHighlightEntries.Count)
        {
            return;
        }

        PackOpeningResult.CardEntry entry = _summaryHighlightEntries[selectedIndex];
        if (entry == null)
        {
            return;
        }

        Sprite sprite = selectedIndex < _summaryHighlightWidgets.Count && _summaryHighlightWidgets[selectedIndex] != null
            ? _summaryHighlightWidgets[selectedIndex].Sprite
            : null;

        if (sprite == null && selectedIndex < _summaryHighlightSpriteTasks.Count)
        {
            sprite = GetTaskResult(_summaryHighlightSpriteTasks[selectedIndex]);
        }

        if (sprite == null)
        {
            sprite = PackOpeningUiUtility.GetFallbackCardSprite();
        }

        Color rarityColor = _currentTheme.GetRarityColor(entry.Rarity);
        if (summaryPreviewCardView != null)
        {
            summaryPreviewCardView.SetStaticFront(entry, sprite, rarityColor);
        }

        if (summaryDetailsHeaderText != null)
        {
            summaryDetailsHeaderText.text = string.Empty;
            summaryDetailsHeaderText.gameObject.SetActive(false);
        }

        if (summaryDetailsLeftColumnText != null)
        {
            summaryDetailsLeftColumnText.text = string.Empty;
            summaryDetailsLeftColumnText.gameObject.SetActive(false);
        }

        if (summaryDetailsBackgroundImage != null)
        {
            summaryDetailsBackgroundImage.color = Color.clear;
            summaryDetailsBackgroundImage.rectTransform.localScale = Vector3.one;
        }

        if (summaryPreviewCardView != null)
        {
            summaryPreviewCardView.RectTransform.localScale = Vector3.one;
        }
    }

    private string BuildSummaryPreviewMeta(PackOpeningResult.CardEntry entry)
    {
        List<string> statusParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.CardId))
        {
            statusParts.Add(entry.CardId.Trim());
        }

        if (entry.IsNew)
        {
            statusParts.Add("NEW");
        }

        return string.Join("  •  ", statusParts);
    }

    private static string GetRarityLabel(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.C:
                return "Common";
            case Rarity.U:
                return "Uncommon";
            case Rarity.R:
                return "Rare";
            case Rarity.SR:
                return "Super Rare";
            case Rarity.SEC:
                return "Secret";
            case Rarity.P:
                return "Promo";
            case Rarity.None:
            default:
                return string.Empty;
        }
    }

    private void PlayOptionalSfx(string sfxId, float minInterval = 0f)
    {
        if (string.IsNullOrWhiteSpace(sfxId))
        {
            return;
        }

        float now = Time.unscaledTime;
        if (minInterval > 0f &&
            _sfxLastPlayedAt.TryGetValue(sfxId, out float lastPlayedAt) &&
            now - lastPlayedAt < minInterval)
        {
            return;
        }

        AudioClip clip = PackOpeningSfxResolver.Resolve(sfxId);
        if (clip == null || ContinuousController.instance == null)
        {
            return;
        }

        _sfxLastPlayedAt[sfxId] = now;
        ContinuousController.instance.PlaySE(clip);
    }

    private void OnOverlayTapped()
    {
        switch (_state)
        {
            case PackOpeningState.WaitingForTap:
                _openRequested = true;
                Debug.Log($"{DebugPrefix} Tap-to-open confirmed.");
                break;

            case PackOpeningState.Hidden:
            case PackOpeningState.Summary:
            case PackOpeningState.Closing:
                break;

            default:
                if (!_fastForward)
                {
                    _fastForward = true;
                    Debug.Log($"{DebugPrefix} Fast-forward requested.");
                }

                break;
        }
    }

    private void OnStageSkipPressed()
    {
        if (_state == PackOpeningState.Summary || _state == PackOpeningState.Hidden)
        {
            return;
        }

        _skipToSummary = true;
        Debug.Log($"{DebugPrefix} Skip-to-summary requested.");
    }

    private void OnContinuePressed()
    {
        PlayOptionalSfx(_currentTheme != null ? _currentTheme.summaryConfirmSfxId : null);
        _summaryContinueRequested = true;
        Debug.Log($"{DebugPrefix} Summary open-another pressed.");
    }

    private void OnSummarySkipPressed()
    {
        PlayOptionalSfx(_currentTheme != null ? _currentTheme.summaryConfirmSfxId : null);
        _summaryContinueRequested = true;
        Debug.Log($"{DebugPrefix} Summary back-to-shop pressed.");
    }

    private void OnSummaryCardPressed(int index)
    {
        if (_state != PackOpeningState.Summary)
        {
            return;
        }

        ApplySummarySelection(index);
        Debug.Log($"{DebugPrefix} Summary card selected => {index}");
    }

    private void SetState(PackOpeningState nextState)
    {
        _state = nextState;
        Debug.Log($"{DebugPrefix} State => {nextState}");

        switch (nextState)
        {
            case PackOpeningState.WaitingForTap:
                hintText.text = "Tap to Open";
                hintText.gameObject.SetActive(true);
                stageSkipButton.gameObject.SetActive(true);
                summaryRoot.gameObject.SetActive(false);
                break;

            case PackOpeningState.Summary:
                hintText.gameObject.SetActive(false);
                stageSkipButton.gameObject.SetActive(false);
                summaryRoot.gameObject.SetActive(true);
                break;

            case PackOpeningState.Hidden:
            case PackOpeningState.Closing:
                hintText.gameObject.SetActive(false);
                if (nextState == PackOpeningState.Hidden)
                {
                    summaryRoot.gameObject.SetActive(false);
                }
                break;

            default:
                hintText.text = "Tap to Speed Up";
                hintText.gameObject.SetActive(true);
                stageSkipButton.gameObject.SetActive(true);
                summaryRoot.gameObject.SetActive(false);
                break;
        }
    }

    private PackPresentationTheme ResolveTheme(PackOpeningResult result)
    {
        PackPresentationCatalog catalog = presentationCatalog != null
            ? presentationCatalog
            : PackPresentationCatalog.LoadDefault();

        return catalog != null
            ? catalog.Resolve(result.SourceId, result.SetId)
            : PackPresentationTheme.CreateFallback(result.SetId);
    }

    private void ResetPlaybackState()
    {
        _openRequested = false;
        _fastForward = false;
        _skipToSummary = false;
        _summaryContinueRequested = false;
        _summarySelectedCardIndex = -1;
        _summaryHighlightEntries.Clear();
        _summaryHighlightSpriteTasks.Clear();
        _sfxLastPlayedAt.Clear();
        overlayGroup.alpha = 0f;
        summaryCanvasGroup.alpha = 0f;
        summaryRoot.gameObject.SetActive(false);
        flashImage.color = new Color(1f, 1f, 1f, 0f);
        tearImage.color = new Color(1f, 1f, 1f, 0f);
    }

    private void SetOverlayVisible(bool visible)
    {
        overlayGroup.blocksRaycasts = visible;
        overlayGroup.interactable = visible;
    }

    private void HideOverlayImmediate()
    {
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;
        overlayGroup.interactable = false;
        summaryCanvasGroup.alpha = 0f;
        packRoot.gameObject.SetActive(false);
        summaryRoot.gameObject.SetActive(false);
        flashImage.color = new Color(1f, 1f, 1f, 0f);
        tearImage.color = new Color(1f, 1f, 1f, 0f);

        for (int index = 0; index < _cardPool.Count; index++)
        {
            if (_cardPool[index] != null)
            {
                _cardPool[index].gameObject.SetActive(false);
            }
        }

        SetState(PackOpeningState.Hidden);
        gameObject.SetActive(false);
    }

    private void StartIdleWobble()
    {
        StopIdleWobble();
        _idleCoroutine = StartCoroutine(IdleWobbleRoutine());
    }

    private void StopIdleWobble()
    {
        if (_idleCoroutine != null)
        {
            StopCoroutine(_idleCoroutine);
            _idleCoroutine = null;
        }

        if (packRoot != null)
        {
            packRoot.anchoredPosition = _packRestPosition;
            packRoot.localRotation = Quaternion.identity;
            packRoot.localScale = Vector3.one;
        }

        ApplyPackDepthTreatment(0.32f, 0f, 0f, 0.24f);
    }

    private IEnumerator IdleWobbleRoutine()
    {
        while (true)
        {
            float time = Time.unscaledTime;
            float hoverWave = Mathf.Sin(time * 0.95f + 0.35f);
            float swayWave = Mathf.Sin(time * 0.58f + 0.9f);
            float tiltWave = Mathf.Sin(time * 1.12f + 0.15f);
            float breatheWave = Mathf.Sin(time * 0.76f + 0.55f);
            float microLift = Mathf.Sin(time * 1.95f + 0.7f);
            float hoverAmplitude = _layoutMetrics.SafeHeight * landscapeLayout.packIdleHoverNormalized;
            float swayAmplitude = _layoutMetrics.SafeWidth * landscapeLayout.packIdleSwayNormalized;
            float breatheAmount = landscapeLayout.packIdleBreathScale;
            float shineNormalized = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(time * 0.34f - 0.8f));
            packRoot.anchoredPosition = _packRestPosition + new Vector2(
                swayWave * swayAmplitude,
                hoverWave * hoverAmplitude + microLift * hoverAmplitude * 0.18f);
            packRoot.localRotation = Quaternion.Euler(0f, 0f, (tiltWave * 0.7f + swayWave * 0.3f) * landscapeLayout.packIdleTiltDegrees);
            packRoot.localScale = Vector3.one * (1f + breatheWave * breatheAmount);
            ApplyPackDepthTreatment(
                Mathf.InverseLerp(-1f, 1f, breatheWave),
                Mathf.InverseLerp(-1f, 1f, hoverWave),
                swayWave,
                shineNormalized);
            yield return null;
        }
    }

    private void PlayBurst(Vector2 anchoredPosition, Color color, int count, float radius, float sizeMultiplier = 1f, float alphaMultiplier = 1f)
    {
        for (int index = 0; index < count; index++)
        {
            Image burst = AcquireBurstParticle();
            RectTransform rect = burst.rectTransform;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = Vector2.one * UnityEngine.Random.Range(10f, 20f) * sizeMultiplier;
            rect.localScale = Vector3.one;
            burst.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(UnityEngine.Random.Range(0.55f, 0.92f) * alphaMultiplier));
            StartCoroutine(AnimateBurstParticle(burst, anchoredPosition, radius));
        }
    }

    private Image AcquireBurstParticle()
    {
        while (_burstPool.Count > 0)
        {
            Image pooled = _burstPool.Dequeue();
            if (pooled == null)
            {
                continue;
            }

            pooled.gameObject.SetActive(true);
            return pooled;
        }

        Image created = PackOpeningUiUtility.CreateImage(burstRoot, $"Burst_{_burstPool.Count + _cardPool.Count + 1}", Color.white);
        created.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        created.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        created.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        created.raycastTarget = false;
        return created;
    }

    private IEnumerator AnimateBurstParticle(Image burst, Vector2 origin, float radius)
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 destination = origin + direction * UnityEngine.Random.Range(radius * 0.45f, radius);
        Vector2 control = origin + direction * (radius * 0.3f) + new Vector2(UnityEngine.Random.Range(-12f, 12f), UnityEngine.Random.Range(-12f, 12f));
        float duration = _fastForward ? 0.12f : 0.24f;

        Color startColor = burst.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
        Vector3 startScale = burst.rectTransform.localScale;
        Vector3 endScale = Vector3.one * UnityEngine.Random.Range(0.2f, 0.6f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            burst.rectTransform.anchoredPosition = EvaluateQuadraticBezier(origin, control, destination, progress);
            burst.color = Color.Lerp(startColor, endColor, progress);
            burst.rectTransform.localScale = Vector3.Lerp(startScale, endScale, progress);
            yield return null;
        }

        burst.gameObject.SetActive(false);
        _burstPool.Enqueue(burst);
    }

    private IEnumerator WaitForTaskOrTimeout(Task<Sprite> task, float timeout)
    {
        if (task == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (!task.IsCompleted && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static Sprite GetTaskResult(Task<Sprite> task)
    {
        if (task == null || !task.IsCompleted || task.IsCanceled || task.IsFaulted)
        {
            return null;
        }

        return task.Result;
    }

    private IEnumerator WaitForSecondsScaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !_skipToSummary)
        {
            elapsed += Time.unscaledDeltaTime * (_fastForward ? 4f : 1f);
            yield return null;
        }
    }

    private IEnumerator TweenValue(float duration, Action<float> onValue, Func<float, float> easing = null)
    {
        if (duration <= 0f)
        {
            onValue?.Invoke(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_skipToSummary && _state != PackOpeningState.Summary && _state != PackOpeningState.Closing)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime * (_fastForward ? 4f : 1f);
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = easing != null ? easing(progress) : progress;
            onValue?.Invoke(eased);
            yield return null;
        }

        onValue?.Invoke(1f);
    }

    private SlotPose[] BuildSlotPoses(int visibleCount)
    {
        float cardWidth = _layoutMetrics.CardSize.x;
        float cardHeight = _layoutMetrics.CardSize.y;
        float spacing = visibleCount > 1
            ? Mathf.Max(cardWidth * landscapeLayout.minimumRevealCardSpacingNormalized, (_layoutMetrics.FanWidth - cardWidth) / Mathf.Max(1, visibleCount - 1))
            : 0f;
        float spanWidth = spacing * Mathf.Max(0, visibleCount - 1) + cardWidth;
        float startX = _layoutMetrics.FanCenter.x - spanWidth * 0.5f + cardWidth * 0.5f;
        float mid = (visibleCount - 1) * 0.5f;
        SlotPose[] poses = new SlotPose[visibleCount];

        for (int index = 0; index < visibleCount; index++)
        {
            float offset = index - mid;
            float normalized = mid <= 0f ? 0f : offset / mid;
            poses[index] = new SlotPose
            {
                Position = new Vector2(startX + index * spacing, _layoutMetrics.FanCenter.y - Mathf.Abs(normalized) * _layoutMetrics.FanArcDepth),
                Rotation = -normalized * landscapeLayout.fanRotationDegrees,
                Scale = 1f - Mathf.Abs(normalized) * landscapeLayout.fanEdgeScaleDrop,
                Size = new Vector2(cardWidth, cardHeight),
            };
        }

        return poses;
    }

    private void SetButtonFill(Button button, Color color)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static int ScaleFont(float value, int min, int max)
    {
        return Mathf.Clamp(Mathf.RoundToInt(value), min, max);
    }

    private static Vector2 EvaluateQuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * a + 2f * oneMinusT * t * b + t * t * c;
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInCubic(float value)
    {
        return value * value * value;
    }

    private static float EaseInOutSine(float value)
    {
        return -(Mathf.Cos(Mathf.PI * value) - 1f) * 0.5f;
    }

    private static float EaseOutBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float t = value - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    private struct SlotPose
    {
        public Vector2 Position;
        public float Rotation;
        public float Scale;
        public Vector2 Size;
    }

    private sealed class SummaryMetricWidget
    {
        public RectTransform Root;
        public Image Background;
        public Text ValueText;
        public Text LabelText;
    }

    private sealed class SummaryHighlightWidget
    {
        public RectTransform Root;
        public Button Button;
        public Image Glow;
        public Image SelectionFrame;
        public Image Frame;
        public Image Art;
        public Text LabelText;
        public RectTransform NewBadgeRoot;
        public Image NewBadgeImage;
        public Text NewBadgeText;
        public Sprite Sprite;
        public Vector2 BasePosition;
    }

    [Serializable]
    private sealed class LandscapeLayoutConfig
    {
        [Range(0.7f, 1f)] public float minimumPrimaryOverlayAlpha = 0.95f;
        [Range(0.2f, 1f)] public float secondaryOverlayAlphaMultiplier = 0.75f;
        [Range(-1.2f, -0.2f)] public float packStartYOffsetNormalized = -0.72f;
        [Range(-0.2f, 0.35f)] public float packCenterYNormalized = 0.15f;
        [Range(0.3f, 0.8f)] public float packHeightNormalized = 0.58f;
        [Range(0.5f, 0.9f)] public float packAspectRatio = 0.72f;
        [Range(0.4f, 1f)] public float packShadowWidthScale = 0.92f;
        [Range(0.08f, 0.4f)] public float packShadowHeightScale = 0.22f;
        [Range(-0.6f, -0.15f)] public float packShadowYOffsetNormalized = -0.42f;
        [Range(1f, 1.8f)] public float packHaloWidthScale = 1.36f;
        [Range(1f, 1.9f)] public float packHaloHeightScale = 1.5f;
        [Range(0f, 0.08f)] public float packRimInsetNormalized = 0.018f;
        [Range(0.12f, 0.45f)] public float packShineWidthScale = 0.28f;
        [Range(0.45f, 1.1f)] public float packShineHeightScale = 0.9f;
        [Range(0.004f, 0.05f)] public float packIdleHoverNormalized = 0.02f;
        [Range(0.002f, 0.03f)] public float packIdleSwayNormalized = 0.006f;
        [Range(0.005f, 0.05f)] public float packIdleBreathScale = 0.02f;
        [Range(0.5f, 5f)] public float packIdleTiltDegrees = 1.9f;
        [Range(0.02f, 0.1f)] public float packLabelBottomNormalized = 0.3f;
        [Range(0.02f, 0.2f)] public float packSubLabelBottomNormalized = 0.11f;
        [Range(0.05f, 0.2f)] public float packLabelFontNormalized = 0.1f;
        [Range(0.03f, 0.1f)] public float packSubLabelFontNormalized = 0.05f;
        [Range(-0.05f, 0.25f)] public float fanCenterYNormalized = 0.12f;
        [Range(0.2f, 0.65f)] public float fanCardHeightNormalized = 0.5f;
        [Range(0.25f, 0.82f)] public float fanWidthNormalized = 0.68f;
        [Range(0.01f, 0.12f)] public float fanArcDepthNormalized = 0.042f;
        [Range(0f, 20f)] public float fanRotationDegrees = 10f;
        [Range(0f, 0.15f)] public float fanEdgeScaleDrop = 0.02f;
        [Range(0.55f, 0.95f)] public float minimumRevealCardSpacingNormalized = 0.74f;
        [Range(0.18f, 0.55f)] public float fanRevealDuration = 0.36f;
        [Range(0.1f, 0.4f)] public float batchCollapseDuration = 0.22f;
        [Range(0.08f, 0.28f)] public float revealSpriteWaitDuration = 0.16f;
        [Range(0.08f, 0.28f)] public float revealPauseDuration = 0.14f;
        [Range(0.12f, 0.4f)] public float revealRarePauseDuration = 0.24f;
        [Range(0.2f, 0.6f)] public float hintWidthNormalized = 0.34f;
        [Range(0.04f, 0.12f)] public float hintHeightNormalized = 0.072f;
        [Range(0.02f, 0.12f)] public float bottomPromptMarginNormalized = 0.055f;
        [Range(0.03f, 0.08f)] public float hintFontNormalized = 0.052f;
        [Range(0.02f, 0.1f)] public float topButtonMarginNormalized = 0.04f;
        [Range(0.02f, 0.08f)] public float sideButtonMarginNormalized = 0.032f;
        [Range(0.08f, 0.18f)] public float skipButtonWidthNormalized = 0.11f;
        [Range(0.05f, 0.11f)] public float skipButtonHeightNormalized = 0.075f;
        [Range(0.3f, 0.92f)] public float summaryWidthNormalized = 0.84f;
        [Range(0.3f, 0.85f)] public float summaryHeightNormalized = 0.74f;
        [Range(-0.15f, 0.2f)] public float summaryCenterYNormalized = 0.03f;
        [Range(0.02f, 0.2f)] public float summaryIntroYOffsetNormalized = 0.05f;
        [Range(0.03f, 0.12f)] public float summaryPaddingNormalized = 0.055f;
        [Range(0.12f, 0.3f)] public float summaryButtonWidthNormalized = 0.18f;
        [Range(0.08f, 0.18f)] public float summaryButtonHeightNormalized = 0.12f;
        [Range(0.03f, 0.14f)] public float summaryButtonBottomNormalized = 0.07f;
        [Range(0.02f, 0.1f)] public float summaryButtonGapNormalized = 0.03f;
        [Range(0.22f, 0.42f)] public float summaryPreviewWidthNormalized = 0.31f;
        [Range(0.008f, 0.03f)] public float summaryGridGapNormalized = 0.012f;
        [Range(0.1f, 0.24f)] public float summaryHeroPackHeightNormalized = 0.17f;
        [Range(0.45f, 0.75f)] public float summaryPreviewCardHeightNormalized = 0.66f;
        [Range(0.03f, 0.08f)] public float summaryInspectionHintHeightNormalized = 0.05f;
        [Range(0.15f, 0.4f)] public float summaryBodyBottomNormalized = 0.18f;
        [Range(0.05f, 0.16f)] public float summaryTitleFontNormalized = 0.095f;
        [Range(0.04f, 0.1f)] public float summaryStatsFontNormalized = 0.06f;
        [Range(0.03f, 0.08f)] public float summaryBodyFontNormalized = 0.05f;
        [Range(0.03f, 0.08f)] public float buttonFontNormalized = 0.046f;
        [Range(2, 8)] public int maxSummaryItems = 5;

        public static LandscapeLayoutConfig CreateDefault()
        {
            return new LandscapeLayoutConfig();
        }
    }

    private readonly struct SafeAreaFrame
    {
        public SafeAreaFrame(float left, float right, float bottom, float top)
        {
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
            Width = right - left;
            Height = top - bottom;
            Center = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
        }

        public float Left { get; }
        public float Right { get; }
        public float Bottom { get; }
        public float Top { get; }
        public float Width { get; }
        public float Height { get; }
        public Vector2 Center { get; }
    }

    private struct LayoutMetrics
    {
        public Vector2 SafeCenter;
        public float SafeWidth;
        public float SafeHeight;
        public float SafeLeft;
        public float SafeRight;
        public float SafeTop;
        public float SafeBottom;
        public Vector2 PackSize;
        public Vector2 PackPosition;
        public Vector2 PackStartPosition;
        public Vector2 FanCenter;
        public float FanWidth;
        public Vector2 CardSize;
        public float FanArcDepth;
        public Vector2 SummarySize;
        public Vector2 SummaryPosition;
        public Vector2 SummaryIntroPosition;
        public Vector2 HintSize;
        public Vector2 HintPosition;
        public Vector2 SkipButtonSize;
        public Vector2 SkipButtonPosition;
        public Vector2 SummaryButtonSize;
        public float SummaryButtonBottom;
    }
}

internal static class PackOpeningSfxResolver
{
    private static readonly Dictionary<string, AudioClip> CachedClips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
    private static readonly HashSet<string> MissingClipIds = new HashSet<string>(StringComparer.Ordinal);
    private const BindingFlags AudioFieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static AudioClip Resolve(string sfxId)
    {
        if (string.IsNullOrWhiteSpace(sfxId))
        {
            return null;
        }

        string trimmed = sfxId.Trim();
        if (CachedClips.TryGetValue(trimmed, out AudioClip cachedClip))
        {
            return cachedClip;
        }

        AudioClip clip = Resources.Load<AudioClip>(trimmed);
        if (clip == null)
        {
            clip = ResolveFromObject(Opening.instance, trimmed) ?? ResolveFromObject(GManager.instance, trimmed);
        }

        if (clip != null)
        {
            CachedClips[trimmed] = clip;
            return clip;
        }

        if (MissingClipIds.Add(trimmed))
        {
            Debug.Log($"[PackOpening] Missing optional SFX '{trimmed}'.");
        }

        return null;
    }

    private static AudioClip ResolveFromObject(UnityEngine.Object target, string fieldName)
    {
        if (target == null)
        {
            return null;
        }

        FieldInfo field = target.GetType().GetField(fieldName, AudioFieldFlags);
        if (field == null || field.FieldType != typeof(AudioClip))
        {
            return null;
        }

        return field.GetValue(target) as AudioClip;
    }
}
