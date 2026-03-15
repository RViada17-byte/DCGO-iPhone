using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PackRevealCardView : MonoBehaviour
{
    [SerializeField] private CanvasGroup viewCanvasGroup;
    [SerializeField] private Image shadowImage;
    [SerializeField] private Image glowImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image innerImage;
    [SerializeField] private Image backImage;
    [SerializeField] private Image frontImage;
    [SerializeField] private RectTransform infoPlate;
    [SerializeField] private Text idText;
    [SerializeField] private Text nameText;
    [SerializeField] private RectTransform countPlate;
    [SerializeField] private Text countText;
    [SerializeField] private RectTransform newBadgeRoot;
    [SerializeField] private Image newBadgeImage;
    [SerializeField] private Text newBadgeText;

    private PackOpeningResult.CardEntry _pendingEntry;
    private Sprite _pendingSprite;
    private Color _pendingRarityColor = Color.white;
    private Coroutine _activePulseRoutine;

    public CanvasGroup ViewCanvasGroup
    {
        get
        {
            EnsureStructure();
            return viewCanvasGroup;
        }
    }

    public RectTransform RectTransform => (RectTransform)transform;

    private void Awake()
    {
        EnsureStructure();
    }

    public void EnsureStructure()
    {
        if (viewCanvasGroup == null)
        {
            viewCanvasGroup = GetComponent<CanvasGroup>();
            if (viewCanvasGroup == null)
            {
                viewCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        RectTransform root = RectTransform;
        root.localScale = Vector3.one;
        gameObject.layer = 5;

        if (shadowImage == null)
        {
            shadowImage = PackOpeningUiUtility.CreateImage(root, "Shadow", new Color(0f, 0f, 0f, 0.3f));
            PackOpeningUiUtility.Stretch(shadowImage.rectTransform, -12f, -12f, 12f, 12f);
        }

        if (glowImage == null)
        {
            glowImage = PackOpeningUiUtility.CreateImage(root, "Glow", new Color(1f, 1f, 1f, 0.12f));
            PackOpeningUiUtility.Stretch(glowImage.rectTransform, -18f, -18f, 18f, 18f);
        }

        if (frameImage == null)
        {
            frameImage = PackOpeningUiUtility.CreateImage(root, "Frame", new Color(0.18f, 0.23f, 0.29f, 1f));
            PackOpeningUiUtility.Stretch(frameImage.rectTransform, 0f, 0f, 0f, 0f);
        }

        if (innerImage == null)
        {
            innerImage = PackOpeningUiUtility.CreateImage(root, "Inner", new Color(0.05f, 0.07f, 0.1f, 1f));
            PackOpeningUiUtility.Stretch(innerImage.rectTransform, 8f, 8f, -8f, -8f);
        }

        RectTransform artRoot = PackOpeningUiUtility.FindOrCreateRect(innerImage.rectTransform, "ArtRoot");
        PackOpeningUiUtility.Stretch(artRoot, 6f, 42f, -6f, -6f);

        Image artRootImage = artRoot.GetComponent<Image>();
        if (artRootImage == null)
        {
            artRootImage = artRoot.gameObject.AddComponent<Image>();
        }

        artRootImage.color = new Color(0.08f, 0.11f, 0.16f, 1f);
        artRootImage.raycastTarget = false;
        artRootImage.sprite = PackOpeningUiUtility.GetWhiteSprite();

        if (backImage == null)
        {
            backImage = PackOpeningUiUtility.CreateImage(artRoot, "Back", Color.white);
            PackOpeningUiUtility.Stretch(backImage.rectTransform, 0f, 0f, 0f, 0f);
            backImage.preserveAspect = true;
        }

        if (frontImage == null)
        {
            frontImage = PackOpeningUiUtility.CreateImage(artRoot, "Front", Color.white);
            PackOpeningUiUtility.Stretch(frontImage.rectTransform, 0f, 0f, 0f, 0f);
            frontImage.preserveAspect = true;
            frontImage.gameObject.SetActive(false);
        }

        if (infoPlate == null)
        {
            Image infoImage = PackOpeningUiUtility.CreateImage(root, "InfoPlate", new Color(0f, 0f, 0f, 0.6f));
            infoPlate = infoImage.rectTransform;
            infoPlate.anchorMin = new Vector2(0f, 0f);
            infoPlate.anchorMax = new Vector2(1f, 0f);
            infoPlate.pivot = new Vector2(0.5f, 0f);
            infoPlate.anchoredPosition = Vector2.zero;
            infoPlate.sizeDelta = new Vector2(0f, 54f);
        }

        if (idText == null)
        {
            idText = PackOpeningUiUtility.CreateText(infoPlate, "CardId", 20, TextAnchor.UpperLeft, FontStyle.Bold);
            PackOpeningUiUtility.Stretch(idText.rectTransform, 10f, 18f, -10f, -2f);
            idText.horizontalOverflow = HorizontalWrapMode.Overflow;
            idText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        if (nameText == null)
        {
            nameText = PackOpeningUiUtility.CreateText(infoPlate, "CardName", 17, TextAnchor.LowerLeft, FontStyle.Normal);
            PackOpeningUiUtility.Stretch(nameText.rectTransform, 10f, 2f, -10f, 12f);
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 12;
            nameText.resizeTextMaxSize = 17;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        if (countPlate == null)
        {
            Image countImage = PackOpeningUiUtility.CreateImage(root, "CountPlate", new Color(0f, 0f, 0f, 0.72f));
            countPlate = countImage.rectTransform;
            countPlate.anchorMin = new Vector2(1f, 1f);
            countPlate.anchorMax = new Vector2(1f, 1f);
            countPlate.pivot = new Vector2(1f, 1f);
            countPlate.anchoredPosition = new Vector2(-10f, -10f);
            countPlate.sizeDelta = new Vector2(40f, 26f);
        }

        if (countText == null)
        {
            countText = PackOpeningUiUtility.CreateText(countPlate, "CountText", 18, TextAnchor.MiddleCenter, FontStyle.Bold);
            PackOpeningUiUtility.Stretch(countText.rectTransform, 0f, 0f, 0f, 0f);
        }

        if (newBadgeRoot == null)
        {
            Image badgeImage = PackOpeningUiUtility.CreateImage(root, "NewBadge", new Color(0.92f, 0.74f, 0.16f, 0.84f));
            newBadgeRoot = badgeImage.rectTransform;
            newBadgeImage = badgeImage;
            newBadgeRoot.anchorMin = new Vector2(0f, 1f);
            newBadgeRoot.anchorMax = new Vector2(0f, 1f);
            newBadgeRoot.pivot = new Vector2(0f, 1f);
            newBadgeRoot.anchoredPosition = new Vector2(12f, -12f);
            newBadgeRoot.sizeDelta = new Vector2(62f, 28f);
        }
        else if (newBadgeImage == null)
        {
            newBadgeImage = newBadgeRoot.GetComponent<Image>();
        }

        if (newBadgeText == null)
        {
            newBadgeText = PackOpeningUiUtility.CreateText(newBadgeRoot, "BadgeText", 16, TextAnchor.MiddleCenter, FontStyle.Bold);
            PackOpeningUiUtility.Stretch(newBadgeText.rectTransform, 0f, 0f, 0f, 0f);
            newBadgeText.color = new Color(0.2f, 0.11f, 0.01f, 0.96f);
            newBadgeText.text = "NEW";
        }

        shadowImage.raycastTarget = false;
        glowImage.raycastTarget = false;
        frameImage.raycastTarget = false;
        innerImage.raycastTarget = false;
        backImage.raycastTarget = false;
        frontImage.raycastTarget = false;
        newBadgeImage.raycastTarget = false;

        if (viewCanvasGroup.alpha <= 0f)
        {
            viewCanvasGroup.alpha = 1f;
        }

        if (string.IsNullOrEmpty(newBadgeText.text))
        {
            newBadgeText.text = "NEW";
        }

        Vector2 size = RectTransform.sizeDelta;
        if (size.x <= 0f || size.y <= 0f)
        {
            size = new Vector2(160f, 228f);
        }

        ApplyLayout(size);
    }

    public void ApplyLayout(Vector2 size)
    {
        float width = Mathf.Max(120f, size.x);
        float height = Mathf.Max(172f, size.y);
        float border = Mathf.Clamp(width * 0.04f, 8f, 16f);
        float glowInset = Mathf.Clamp(width * 0.08f, 18f, 34f);
        float shadowInsetX = Mathf.Clamp(width * 0.06f, 12f, 22f);
        float shadowInsetY = Mathf.Clamp(height * 0.05f, 12f, 22f);
        float artInset = Mathf.Clamp(width * 0.032f, 6f, 12f);
        float infoHeight = Mathf.Clamp(height * 0.17f, 52f, 68f);
        float countWidth = Mathf.Clamp(width * 0.22f, 40f, 68f);
        float countHeight = Mathf.Clamp(height * 0.1f, 26f, 38f);
        float badgeWidth = Mathf.Clamp(width * 0.34f, 62f, 92f);
        float badgeHeight = Mathf.Clamp(height * 0.1f, 28f, 40f);
        float titleFont = Mathf.Clamp(Mathf.RoundToInt(height * 0.09f), 20, 30);
        float nameFont = Mathf.Clamp(Mathf.RoundToInt(height * 0.072f), 17, 24);
        float badgeFont = Mathf.Clamp(Mathf.RoundToInt(height * 0.064f), 16, 22);
        float countFont = Mathf.Clamp(Mathf.RoundToInt(height * 0.072f), 18, 26);
        float infoInsetX = Mathf.Clamp(width * 0.06f, 10f, 18f);

        PackOpeningUiUtility.Stretch(shadowImage.rectTransform, -shadowInsetX, -shadowInsetY, shadowInsetX, shadowInsetY);
        PackOpeningUiUtility.Stretch(glowImage.rectTransform, -glowInset, -glowInset, glowInset, glowInset);
        PackOpeningUiUtility.Stretch(frameImage.rectTransform, 0f, 0f, 0f, 0f);
        PackOpeningUiUtility.Stretch(innerImage.rectTransform, border, border, -border, -border);

        RectTransform artRoot = PackOpeningUiUtility.FindOrCreateRect(innerImage.rectTransform, "ArtRoot");
        PackOpeningUiUtility.Stretch(artRoot, artInset, infoHeight, -artInset, -artInset);

        infoPlate.sizeDelta = new Vector2(0f, infoHeight);
        PackOpeningUiUtility.Stretch(idText.rectTransform, infoInsetX, infoHeight * 0.38f, -infoInsetX, -4f);
        PackOpeningUiUtility.Stretch(nameText.rectTransform, infoInsetX, 4f, -infoInsetX, infoHeight * 0.24f);
        idText.fontSize = (int)titleFont;
        nameText.fontSize = (int)nameFont;
        nameText.resizeTextMaxSize = nameText.fontSize;
        nameText.resizeTextMinSize = Mathf.Max(12, nameText.fontSize - 6);

        countPlate.sizeDelta = new Vector2(countWidth, countHeight);
        countPlate.anchoredPosition = new Vector2(-border - 2f, -border - 2f);
        countText.fontSize = (int)countFont;

        newBadgeRoot.sizeDelta = new Vector2(badgeWidth, badgeHeight);
        newBadgeRoot.anchoredPosition = new Vector2(border + 4f, -border - 4f);
        newBadgeText.fontSize = (int)badgeFont;
    }

    public void PrepareBack(Sprite backSprite, Color backTint, Color accentColor)
    {
        EnsureStructure();
        StopPulse();
        gameObject.SetActive(true);
        viewCanvasGroup.alpha = 1f;
        shadowImage.color = new Color(0f, 0f, 0f, 0.32f);
        glowImage.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.08f);
        glowImage.rectTransform.localScale = Vector3.one * 1.02f;
        frameImage.color = Color.Lerp(backTint, accentColor, 0.38f);
        innerImage.color = new Color(0.05f, 0.07f, 0.1f, 1f);
        backImage.sprite = backSprite != null ? backSprite : PackOpeningUiUtility.GetWhiteSprite();
        backImage.color = backSprite != null ? Color.white : backTint;
        backImage.preserveAspect = true;
        backImage.gameObject.SetActive(true);
        frontImage.gameObject.SetActive(false);
        idText.text = string.Empty;
        nameText.text = string.Empty;
        countText.text = string.Empty;
        countPlate.gameObject.SetActive(false);
        SetNewBadgeVisible(false);
    }

    public void SetRevealContent(PackOpeningResult.CardEntry entry, Sprite sprite, Color rarityColor)
    {
        _pendingEntry = entry;
        _pendingSprite = sprite != null ? sprite : PackOpeningUiUtility.GetFallbackCardSprite();
        _pendingRarityColor = rarityColor;
    }

    public IEnumerator PlayReveal(bool fastForward)
    {
        EnsureStructure();
        float shrinkDuration = fastForward ? 0.06f : 0.12f;
        float expandDuration = fastForward ? 0.1f : 0.18f;
        float settleDuration = fastForward ? 0.05f : 0.08f;
        Vector3 startScale = RectTransform.localScale;

        yield return TweenScaleX(startScale.x, 0.08f, shrinkDuration, EaseInOutSine);
        ApplyPendingRevealVisuals();
        yield return TweenScaleX(0.08f, startScale.x * 1.04f, expandDuration * 0.72f, EaseOutCubic);
        yield return TweenScale(RectTransform.localScale, startScale, settleDuration, EaseOutCubic);

        if (_pendingEntry != null && _pendingEntry.IsNew)
        {
            yield return PlayBadgePop(fastForward);
        }
    }

    public IEnumerator PlayRarePulse(bool fastForward)
    {
        EnsureStructure();
        StopPulse();
        float duration = fastForward ? 0.14f : 0.28f;
        Color startGlow = glowImage.color;
        Color pulseGlow = new Color(_pendingRarityColor.r, _pendingRarityColor.g, _pendingRarityColor.b, 0.6f);
        Vector3 glowStartScale = glowImage.rectTransform.localScale;
        Vector3 glowPeakScale = Vector3.one * 1.38f;
        Vector3 cardStartScale = RectTransform.localScale;
        Vector3 cardPeakScale = cardStartScale * 1.1f;
        Color frameStartColor = frameImage.color;
        Color framePeakColor = Color.Lerp(frameStartColor, _pendingRarityColor, 0.9f);

        yield return TweenValue(duration * 0.45f, progress =>
        {
            glowImage.color = Color.Lerp(startGlow, pulseGlow, progress);
            glowImage.rectTransform.localScale = Vector3.Lerp(glowStartScale, glowPeakScale, progress);
            RectTransform.localScale = Vector3.Lerp(cardStartScale, cardPeakScale, progress);
            frameImage.color = Color.Lerp(frameStartColor, framePeakColor, progress);
        }, EaseOutCubic);

        yield return TweenValue(duration * 0.55f, progress =>
        {
            glowImage.color = Color.Lerp(pulseGlow, startGlow, progress);
            glowImage.rectTransform.localScale = Vector3.Lerp(glowPeakScale, glowStartScale, progress);
            RectTransform.localScale = Vector3.Lerp(cardPeakScale, cardStartScale, progress);
            frameImage.color = Color.Lerp(framePeakColor, frameStartColor, progress);
        }, EaseInOutSine);
    }

    public void SetStaticFront(PackOpeningResult.CardEntry entry, Sprite sprite, Color rarityColor)
    {
        EnsureStructure();
        StopPulse();
        _pendingEntry = entry;
        _pendingSprite = sprite != null ? sprite : PackOpeningUiUtility.GetFallbackCardSprite();
        _pendingRarityColor = rarityColor;
        viewCanvasGroup.alpha = 1f;
        RectTransform.localScale = Vector3.one;
        ApplyPendingRevealVisuals();

        frameImage.color = new Color(0f, 0f, 0f, 0f);
        glowImage.color = Color.clear;
        glowImage.rectTransform.localScale = Vector3.one;
        shadowImage.color = new Color(0f, 0f, 0f, 0.2f);
    }

    public void StopPulse()
    {
        if (_activePulseRoutine != null)
        {
            StopCoroutine(_activePulseRoutine);
            _activePulseRoutine = null;
        }
    }

    private void ApplyPendingRevealVisuals()
    {
        if (_pendingEntry == null)
        {
            return;
        }

        frontImage.sprite = _pendingSprite != null ? _pendingSprite : PackOpeningUiUtility.GetFallbackCardSprite();
        frontImage.color = Color.white;
        frontImage.gameObject.SetActive(true);
        backImage.gameObject.SetActive(false);
        frameImage.color = Color.Lerp(new Color(0.27f, 0.3f, 0.36f, 1f), _pendingRarityColor, 0.74f);
        glowImage.color = new Color(_pendingRarityColor.r, _pendingRarityColor.g, _pendingRarityColor.b, _pendingEntry.IsRare ? 0.24f : 0.18f);
        idText.text = _pendingEntry.CardId ?? string.Empty;
        nameText.text = _pendingEntry.CardName ?? string.Empty;
        countPlate.gameObject.SetActive(_pendingEntry.Count > 1);
        countText.text = _pendingEntry.Count > 1 ? "x" + _pendingEntry.Count : string.Empty;
        SetNewBadgeVisible(_pendingEntry.IsNew);
    }

    private void SetNewBadgeVisible(bool visible)
    {
        if (newBadgeRoot != null)
        {
            newBadgeRoot.gameObject.SetActive(visible);
            if (visible)
            {
                newBadgeRoot.localScale = Vector3.one;
            }
        }
    }

    private IEnumerator PlayBadgePop(bool fastForward)
    {
        if (newBadgeRoot == null || !newBadgeRoot.gameObject.activeSelf)
        {
            yield break;
        }

        Vector3 baseScale = Vector3.one;
        newBadgeRoot.localScale = Vector3.one * 0.8f;
        yield return TweenBadgeScale(newBadgeRoot.localScale, baseScale * 1.08f, fastForward ? 0.05f : 0.1f, EaseOutCubic);
        yield return TweenBadgeScale(newBadgeRoot.localScale, baseScale, fastForward ? 0.05f : 0.1f, EaseInOutSine);
    }

    private IEnumerator TweenScaleX(float from, float to, float duration, System.Func<float, float> easing = null)
    {
        yield return TweenValue(duration, progress =>
        {
            Vector3 scale = RectTransform.localScale;
            scale.x = Mathf.Lerp(from, to, progress);
            RectTransform.localScale = scale;
        }, easing);
    }

    private IEnumerator TweenScale(Vector3 from, Vector3 to, float duration, System.Func<float, float> easing = null)
    {
        yield return TweenValue(duration, progress =>
        {
            RectTransform.localScale = Vector3.Lerp(from, to, progress);
        }, easing);
    }

    private IEnumerator TweenBadgeScale(Vector3 from, Vector3 to, float duration, System.Func<float, float> easing = null)
    {
        yield return TweenValue(duration, progress =>
        {
            newBadgeRoot.localScale = Vector3.Lerp(from, to, progress);
        }, easing);
    }

    private IEnumerator TweenValue(float duration, System.Action<float> onValue, System.Func<float, float> easing = null)
    {
        if (duration <= 0f)
        {
            onValue?.Invoke(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            onValue?.Invoke(easing != null ? easing(progress) : progress);
            yield return null;
        }

        onValue?.Invoke(1f);
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInOutSine(float value)
    {
        return -(Mathf.Cos(Mathf.PI * value) - 1f) * 0.5f;
    }
}

internal static class PackOpeningUiUtility
{
    private static Font _defaultFont;
    private static Sprite _whiteSprite;
    private static Sprite _softCircleSprite;
    private static Sprite _softFrameSprite;
    private static Sprite _shineSprite;

    public static Font GetDefaultFont()
    {
        if (_defaultFont == null)
        {
            _defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return _defaultFont;
    }

    public static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
        {
            return _whiteSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);
        _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return _whiteSprite;
    }

    public static Sprite GetFallbackCardSprite()
    {
        if (ContinuousController.instance != null && ContinuousController.instance.ReverseCard != null)
        {
            return ContinuousController.instance.ReverseCard;
        }

        Sprite resourceSprite = Resources.Load<Sprite>("Placeholders/EmptyCard");
        return resourceSprite != null ? resourceSprite : GetWhiteSprite();
    }

    public static Sprite GetSoftCircleSprite()
    {
        if (_softCircleSprite != null)
        {
            return _softCircleSprite;
        }

        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "PackSoftCircle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                alpha *= alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        _softCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _softCircleSprite;
    }

    public static Sprite GetSoftFrameSprite()
    {
        if (_softFrameSprite != null)
        {
            return _softFrameSprite;
        }

        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "PackSoftFrame";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float v = y / (float)(size - 1);
                float edgeDistance = Mathf.Min(u, v, 1f - u, 1f - v);
                float outer = 1f - Mathf.SmoothStep(0.03f, 0.18f, edgeDistance);
                float inner = Mathf.SmoothStep(0.005f, 0.04f, edgeDistance);
                float alpha = Mathf.Clamp01(outer * inner);
                alpha = Mathf.Pow(alpha, 0.85f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        _softFrameSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _softFrameSprite;
    }

    public static Sprite GetShineSprite()
    {
        if (_shineSprite != null)
        {
            return _shineSprite;
        }

        const int width = 48;
        const int height = 160;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "PackShine";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float v = y / (float)(height - 1);
                float band = 1f - Mathf.Abs(u - 0.5f) / 0.5f;
                band = Mathf.Pow(Mathf.Clamp01(band), 2.2f);
                float verticalFade = Mathf.Sin(v * Mathf.PI);
                verticalFade = Mathf.Pow(Mathf.Clamp01(verticalFade), 0.75f);
                float alpha = band * verticalFade;
                alpha *= 0.9f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        _shineSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        return _shineSprite;
    }

    public static RectTransform FindOrCreateRect(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            return child as RectTransform;
        }

        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = 5;
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    public static Image CreateImage(Transform parent, string name, Color color)
    {
        RectTransform rect = FindOrCreateRect(parent, name);
        Image image = rect.GetComponent<Image>();
        if (image == null)
        {
            image = rect.gameObject.AddComponent<Image>();
        }

        image.sprite = GetWhiteSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static Text CreateText(Transform parent, string name, int fontSize, TextAnchor anchor, FontStyle fontStyle)
    {
        RectTransform rect = FindOrCreateRect(parent, name);
        Text text = rect.GetComponent<Text>();
        if (text == null)
        {
            text = rect.gameObject.AddComponent<Text>();
        }

        text.font = GetDefaultFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = anchor;
        text.color = Color.white;
        text.supportRichText = true;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    public static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }
}
