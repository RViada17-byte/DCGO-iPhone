using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PackOpeningInspectionFinalPreview
{
    private const string ReferenceCanvasPath = "Opening/Main Menu";
    private const string PreviewCanvasName = "InspectionFinalPreviewCanvas";
    private const string RootName = "InspectionRoot";
    private static Sprite _solidSprite;
    private static Sprite _softCircleSprite;
    private static Sprite _softFrameSprite;
    private static Sprite _fallbackCardSprite;

    [MenuItem("Tools/Pack Opening/Build Final Inspection Preview")]
    public static void BuildPreview()
    {
        GameObject referenceCanvasObject = GameObject.Find(ReferenceCanvasPath);
        if (referenceCanvasObject == null)
        {
            Debug.LogError("[PackOpening] Could not find canvas at path: " + ReferenceCanvasPath);
            return;
        }

        RectTransform referenceCanvasRect = referenceCanvasObject.GetComponent<RectTransform>();
        Canvas referenceCanvas = referenceCanvasObject.GetComponent<Canvas>();
        CanvasScaler referenceScaler = referenceCanvasObject.GetComponent<CanvasScaler>();
        if (referenceCanvasRect == null || referenceCanvas == null || referenceScaler == null)
        {
            Debug.LogError("[PackOpening] Target canvas is missing required UI components.");
            return;
        }

        GameObject existingPreviewCanvas = GameObject.Find(PreviewCanvasName);
        if (existingPreviewCanvas != null)
        {
            Object.DestroyImmediate(existingPreviewCanvas);
        }

        List<PackOpeningResult.CardEntry> entries = BuildPreviewEntries();
        if (entries.Count < 12)
        {
            Debug.LogError("[PackOpening] Could not find 12 real cards with readable art for preview.");
            return;
        }

        int selectedIndex = GetBestHitIndex(entries);

        GameObject previewCanvasObject = new GameObject(
            PreviewCanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        previewCanvasObject.layer = LayerMask.NameToLayer("UI");

        RectTransform canvasRect = previewCanvasObject.GetComponent<RectTransform>();
        canvasRect.SetParent(referenceCanvasObject.transform.parent, false);
        canvasRect.anchorMin = referenceCanvasRect.anchorMin;
        canvasRect.anchorMax = referenceCanvasRect.anchorMax;
        canvasRect.pivot = referenceCanvasRect.pivot;
        canvasRect.anchoredPosition = referenceCanvasRect.anchoredPosition;
        canvasRect.sizeDelta = referenceCanvasRect.sizeDelta;
        canvasRect.localScale = referenceCanvasRect.localScale;
        canvasRect.localRotation = referenceCanvasRect.localRotation;
        canvasRect.localPosition = referenceCanvasRect.localPosition;

        Canvas previewCanvas = previewCanvasObject.GetComponent<Canvas>();
        previewCanvas.renderMode = referenceCanvas.renderMode;
        previewCanvas.worldCamera = referenceCanvas.worldCamera;
        previewCanvas.planeDistance = referenceCanvas.planeDistance;
        previewCanvas.overrideSorting = true;
        previewCanvas.sortingLayerID = referenceCanvas.sortingLayerID;
        previewCanvas.sortingOrder = 600;

        CanvasScaler previewScaler = previewCanvasObject.GetComponent<CanvasScaler>();
        previewScaler.uiScaleMode = referenceScaler.uiScaleMode;
        previewScaler.referenceResolution = referenceScaler.referenceResolution;
        previewScaler.screenMatchMode = referenceScaler.screenMatchMode;
        previewScaler.matchWidthOrHeight = referenceScaler.matchWidthOrHeight;
        previewScaler.referencePixelsPerUnit = referenceScaler.referencePixelsPerUnit;
        previewScaler.scaleFactor = referenceScaler.scaleFactor;
        previewScaler.physicalUnit = referenceScaler.physicalUnit;
        previewScaler.fallbackScreenDPI = referenceScaler.fallbackScreenDPI;
        previewScaler.defaultSpriteDPI = referenceScaler.defaultSpriteDPI;
        previewScaler.dynamicPixelsPerUnit = referenceScaler.dynamicPixelsPerUnit;

        RectTransform inspectionRoot = CreateUiObject(RootName, previewCanvasObject.transform);
        Stretch(inspectionRoot, 0f, 0f, 0f, 0f);
        Image rootImage = inspectionRoot.gameObject.AddComponent<Image>();
        ConfigureImage(rootImage, new Color(0.035f, 0.045f, 0.06f, 0.965f), true);

        Rect safeRect = new Rect(0f, 0f, referenceCanvasRect.rect.width, referenceCanvasRect.rect.height);
        float width = safeRect.width;
        float height = safeRect.height;
        float gap = width * 0.02f;
        float gridWidth = width * 0.69f;
        float previewWidth = width * 0.29f;
        float actionHeight = height * 0.14f;
        float topBottomMargin = height * 0.08f;
        float usableHeight = height - topBottomMargin * 2f;
        float previewHeight = usableHeight - actionHeight;
        float originX = safeRect.xMin - width * 0.5f;
        float originY = safeRect.yMin - height * 0.5f + topBottomMargin;

        RectTransform gridPanel = CreatePanel("GridPanel", inspectionRoot, new Color(0.08f, 0.1f, 0.125f, 0.92f));
        SetCenteredRect(gridPanel, originX + gridWidth * 0.5f, originY + usableHeight * 0.5f, gridWidth, usableHeight);

        GridLayoutGroup grid = gridPanel.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        float cellGap = Mathf.Round(width * 0.008f);
        grid.spacing = new Vector2(cellGap, cellGap);
        int padX = Mathf.RoundToInt(width * 0.012f);
        int padY = Mathf.RoundToInt(height * 0.02f);
        grid.padding = new RectOffset(padX, padX, padY, padY);
        float availableGridWidth = gridWidth - padX * 2f - cellGap * 5f;
        float cellWidth = availableGridWidth / 6f;
        float cellHeight = cellWidth * 1.42f;
        float maxCellHeight = (usableHeight - padY * 2f - cellGap) / 2f;
        if (cellHeight > maxCellHeight)
        {
            cellHeight = maxCellHeight;
            cellWidth = cellHeight / 1.42f;
        }
        grid.cellSize = new Vector2(cellWidth, cellHeight);

        for (int i = 0; i < 12; i++)
        {
            CreateThumbnail(gridPanel, entries[i], i == selectedIndex, cellWidth, cellHeight);
        }

        float previewCenterX = originX + gridWidth + gap + previewWidth * 0.5f;
        float previewCenterY = originY + actionHeight + previewHeight * 0.5f;
        RectTransform previewPanel = CreatePanel("PreviewPanel", inspectionRoot, new Color(0.075f, 0.09f, 0.11f, 0.96f));
        SetCenteredRect(previewPanel, previewCenterX, previewCenterY, previewWidth, previewHeight);

        PackOpeningResult.CardEntry selectedEntry = entries[selectedIndex];
        Color selectedColor = GetRarityColor(selectedEntry.Rarity);

        RectTransform previewAura = CreateUiObject("PreviewAura", previewPanel);
        SetCenteredRect(previewAura, 0f, previewHeight * 0.06f, previewWidth * 0.9f, previewHeight * 0.86f);
        Image previewAuraImage = previewAura.gameObject.AddComponent<Image>();
        ConfigureImage(previewAuraImage, new Color(selectedColor.r, selectedColor.g, selectedColor.b, 0.14f));
        previewAuraImage.sprite = GetSoftCircleSprite();

        GameObject previewCardObject = new GameObject("SelectedCard", typeof(RectTransform), typeof(CanvasGroup), typeof(PackRevealCardView));
        previewCardObject.layer = LayerMask.NameToLayer("UI");
        RectTransform previewCardRect = previewCardObject.GetComponent<RectTransform>();
        previewCardRect.SetParent(previewPanel, false);
        previewCardRect.localScale = Vector3.one;
        float previewCardHeight = previewHeight * 0.82f;
        float previewCardWidth = Mathf.Min(previewWidth * 0.82f, previewCardHeight / 1.42f);
        SetCenteredRect(previewCardRect, 0f, previewHeight * 0.06f, previewCardWidth, previewCardHeight);
        PackRevealCardView previewView = previewCardObject.GetComponent<PackRevealCardView>();
        previewView.EnsureStructure();
        previewView.ApplyLayout(new Vector2(previewCardWidth, previewCardHeight));
        previewView.SetStaticFront(selectedEntry, LoadSprite(selectedEntry), selectedColor);

        RectTransform previewNameRect = CreateUiObject("PreviewName", previewPanel);
        SetCenteredRect(previewNameRect, 0f, -previewHeight * 0.38f, previewWidth * 0.84f, height * 0.065f);
        Text previewName = previewNameRect.gameObject.AddComponent<Text>();
        ConfigureText(previewName, selectedEntry.CardName, 22, FontStyle.Bold, new Color(0.94f, 0.97f, 1f, 0.96f));

        RectTransform previewMetaRect = CreateUiObject("PreviewMeta", previewPanel);
        SetCenteredRect(previewMetaRect, 0f, -previewHeight * 0.44f, previewWidth * 0.84f, height * 0.04f);
        Text previewMeta = previewMetaRect.gameObject.AddComponent<Text>();
        ConfigureText(previewMeta, BuildMeta(selectedEntry), 15, FontStyle.Normal, new Color(0.82f, 0.87f, 0.93f, 0.82f));

        RectTransform actionPanel = CreatePanel("ActionPanel", inspectionRoot, new Color(0.065f, 0.075f, 0.095f, 0.94f));
        SetCenteredRect(actionPanel, previewCenterX, originY + actionHeight * 0.5f, previewWidth, actionHeight);
        HorizontalLayoutGroup actions = actionPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        actions.childAlignment = TextAnchor.MiddleCenter;
        actions.childControlWidth = true;
        actions.childControlHeight = true;
        actions.childForceExpandWidth = true;
        actions.childForceExpandHeight = true;
        actions.spacing = width * 0.012f;
        int actionPad = Mathf.RoundToInt(width * 0.012f);
        actions.padding = new RectOffset(actionPad, actionPad, actionPad, actionPad);
        CreateButtonPlaceholder(actionPanel, "OpenAnotherButton", "Open Another", new Color(0.23f, 0.27f, 0.34f, 0.98f));
        CreateButtonPlaceholder(actionPanel, "BackToShopButton", "Back to Shop", new Color(0.18f, 0.22f, 0.28f, 0.98f));

        EditorSceneManager.MarkSceneDirty(previewCanvasObject.scene);
        Debug.Log("[PackOpening] Built final inspection preview.");
    }

    private static List<PackOpeningResult.CardEntry> BuildPreviewEntries()
    {
        string[] assetPaths =
        {
            "Assets/CardBaseEntity/BT20/Green/DigiEgg/BT20_004.asset",
            "Assets/CardBaseEntity/BT20/Green/Digimon/BT20_038.asset",
            "Assets/CardBaseEntity/BT20/Green/Digimon/BT20_039.asset",
            "Assets/CardBaseEntity/BT20/Green/Digimon/BT20_040.asset",
            "Assets/CardBaseEntity/BT20/Green/Digimon/BT20_041.asset",
            "Assets/CardBaseEntity/BT20/Green/Digimon/BT20_042.asset",
            "Assets/CardBaseEntity/BT20/Green/Digimon/BT20_043.asset",
            "Assets/CardBaseEntity/BT20/Green/Digimon/BT20_044.asset",
            "Assets/CardBaseEntity/BT20/Green/Digimon/BT20_045.asset",
            "Assets/CardBaseEntity/BT20/Green/Tamer/BT20_085.asset",
            "Assets/CardBaseEntity/BT20/Green/Digimon/BT20_101.asset",
            "Assets/CardBaseEntity/BT20/Green/Digimon/BT20_045_P1.asset",
        };

        List<CEntity_Base> chosenCards = assetPaths
            .Select(AssetDatabase.LoadAssetAtPath<CEntity_Base>)
            .Where(card => card != null && !string.IsNullOrWhiteSpace(card.CardID) && !string.IsNullOrWhiteSpace(card.CardSpriteName))
            .ToList();

        List<PackOpeningResult.CardEntry> entries = new List<PackOpeningResult.CardEntry>();
        for (int i = 0; i < chosenCards.Count && entries.Count < 12; i++)
        {
            CEntity_Base card = chosenCards[i];
            entries.Add(new PackOpeningResult.CardEntry
            {
                CardId = card.CardID,
                CardName = !string.IsNullOrWhiteSpace(card.CardName_ENG) ? card.CardName_ENG.Trim() : card.CardID,
                SpriteName = card.CardSpriteName,
                Rarity = card.rarity,
                IsNew = i == 0 || i == 3 || i == 7,
                Count = 1,
                CardAsset = card,
            });
        }

        return entries;
    }

    private static void CreateThumbnail(Transform parent, PackOpeningResult.CardEntry entry, bool selected, float width, float height)
    {
        RectTransform thumb = CreateUiObject(entry.CardId, parent);
        Image thumbFrame = thumb.gameObject.AddComponent<Image>();
        Color rarityColor = GetRarityColor(entry.Rarity);
        ConfigureImage(thumbFrame, selected ? Color.Lerp(new Color(0.22f, 0.27f, 0.34f, 1f), rarityColor, 0.34f) : Color.Lerp(new Color(0.16f, 0.19f, 0.24f, 1f), rarityColor, 0.14f));
        thumb.gameObject.AddComponent<Button>();

        RectTransform glow = CreateUiObject("Glow", thumb);
        Stretch(glow, -width * 0.05f, -height * 0.05f, width * 0.05f, height * 0.05f);
        Image glowImage = glow.gameObject.AddComponent<Image>();
        glowImage.sprite = GetSoftFrameSprite();
        glowImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, selected ? 0.22f : 0.06f);
        glowImage.raycastTarget = false;

        RectTransform art = CreateUiObject("Art", thumb);
        Stretch(art, width * 0.026f, height * 0.026f, -width * 0.026f, -height * 0.026f);
        Image artImage = art.gameObject.AddComponent<Image>();
        artImage.sprite = LoadSprite(entry);
        artImage.color = Color.white;
        artImage.preserveAspect = true;
        artImage.raycastTarget = false;

        if (selected)
        {
            RectTransform selection = CreateUiObject("SelectionFrame", thumb);
            Stretch(selection, -width * 0.04f, -height * 0.04f, width * 0.04f, height * 0.04f);
            Image selectionImage = selection.gameObject.AddComponent<Image>();
            selectionImage.sprite = GetSoftFrameSprite();
            selectionImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.82f);
            selectionImage.raycastTarget = false;
        }

        if (entry.IsNew)
        {
            RectTransform badge = CreateUiObject("NewBadge", thumb);
            SetCenteredRect(badge, -width * 0.27f, height * 0.4f, width * 0.22f, height * 0.078f);
            Image badgeImage = badge.gameObject.AddComponent<Image>();
            ConfigureImage(badgeImage, new Color(0.74f, 0.82f, 0.92f, 0.2f));
            RectTransform badgeTextRect = CreateUiObject("Text", badge);
            Stretch(badgeTextRect, 0f, 0f, 0f, 0f);
            Text badgeText = badgeTextRect.gameObject.AddComponent<Text>();
            ConfigureText(badgeText, "NEW", Mathf.Clamp(Mathf.RoundToInt(height * 0.046f), 8, 11), FontStyle.Bold, new Color(0.9f, 0.95f, 1f, 0.82f));
        }
    }

    private static int GetBestHitIndex(List<PackOpeningResult.CardEntry> entries)
    {
        int bestIndex = 0;
        int bestScore = int.MinValue;
        for (int i = 0; i < entries.Count; i++)
        {
            PackOpeningResult.CardEntry entry = entries[i];
            int score = GetRarityPriority(entry.Rarity) * 100 + (entry.IsNew ? 25 : 0) - i;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int GetRarityPriority(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.SEC: return 6;
            case Rarity.P: return 5;
            case Rarity.SR: return 4;
            case Rarity.R: return 3;
            case Rarity.U: return 2;
            case Rarity.C: return 1;
            default: return 0;
        }
    }

    private static Color GetRarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.SEC: return new Color(1f, 0.84f, 0.45f, 1f);
            case Rarity.P: return new Color(1f, 0.58f, 0.4f, 1f);
            case Rarity.SR: return new Color(0.92f, 0.46f, 0.96f, 1f);
            case Rarity.R: return new Color(0.39f, 0.72f, 1f, 1f);
            case Rarity.U: return new Color(0.41f, 0.88f, 0.65f, 1f);
            case Rarity.C:
            default:
                return new Color(0.64f, 0.72f, 0.84f, 1f);
        }
    }

    private static string BuildMeta(PackOpeningResult.CardEntry entry)
    {
        return entry.IsNew ? entry.CardId + "  •  NEW" : entry.CardId;
    }

    private static Sprite LoadSprite(PackOpeningResult.CardEntry entry)
    {
        Sprite sprite = entry.LoadSpriteAsync().GetAwaiter().GetResult();
        return sprite != null ? sprite : GetFallbackCardSprite();
    }

    private static RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateUiObject(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        ConfigureImage(image, color);
        return rect;
    }

    private static void CreateButtonPlaceholder(Transform parent, string name, string label, Color fill)
    {
        RectTransform buttonRect = CreateUiObject(name, parent);
        Image image = buttonRect.gameObject.AddComponent<Image>();
        ConfigureImage(image, fill, true);
        buttonRect.gameObject.AddComponent<Button>();
        buttonRect.gameObject.AddComponent<LayoutElement>();

        RectTransform labelRect = CreateUiObject("Label", buttonRect);
        Stretch(labelRect, 0f, 0f, 0f, 0f);
        Text text = labelRect.gameObject.AddComponent<Text>();
        ConfigureText(text, label, 22, FontStyle.Bold, new Color(0.84f, 0.89f, 0.95f, 0.92f));
    }

    private static RectTransform CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static void ConfigureImage(Image image, Color color, bool raycastTarget = false)
    {
        image.sprite = GetSolidSprite();
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = raycastTarget;
    }

    private static void ConfigureText(Text text, string value, int fontSize, FontStyle fontStyle, Color color)
    {
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontStyle = fontStyle;
        text.fontSize = fontSize;
        text.color = color;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private static Sprite GetSolidSprite()
    {
        if (_solidSprite != null)
        {
            return _solidSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "InspectionFinalPreviewSolid";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);
        _solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return _solidSprite;
    }

    private static Sprite GetFallbackCardSprite()
    {
        if (_fallbackCardSprite != null)
        {
            return _fallbackCardSprite;
        }

        _fallbackCardSprite = Resources.Load<Sprite>("Placeholders/EmptyCard");
        return _fallbackCardSprite != null ? _fallbackCardSprite : GetSolidSprite();
    }

    private static Sprite GetSoftCircleSprite()
    {
        if (_softCircleSprite != null)
        {
            return _softCircleSprite;
        }

        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "InspectionFinalPreviewSoftCircle";
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

    private static Sprite GetSoftFrameSprite()
    {
        if (_softFrameSprite != null)
        {
            return _softFrameSprite;
        }

        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "InspectionFinalPreviewSoftFrame";
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

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    private static void SetCenteredRect(RectTransform rect, float centerX, float centerY, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(centerX, centerY);
        rect.sizeDelta = new Vector2(width, height);
    }
}
