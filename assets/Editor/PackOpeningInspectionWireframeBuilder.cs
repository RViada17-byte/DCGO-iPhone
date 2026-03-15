using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PackOpeningInspectionWireframeBuilder
{
    private const string ReferenceCanvasPath = "Opening/Main Menu";
    private const string PreviewCanvasName = "InspectionWireframeCanvas";
    private const string RootName = "InspectionRoot";
    private static Sprite _solidSprite;

    [MenuItem("Tools/Pack Opening/Rebuild Inspection Wireframe")]
    public static void RebuildWireframe()
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

        Transform legacyRoot = referenceCanvasObject.transform.Find(RootName);
        if (legacyRoot != null)
        {
            Object.DestroyImmediate(legacyRoot.gameObject);
        }

        GameObject existingPreviewCanvas = GameObject.Find(PreviewCanvasName);
        if (existingPreviewCanvas != null)
        {
            Object.DestroyImmediate(existingPreviewCanvas);
        }

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
        previewCanvas.sortingOrder = 500;

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
        inspectionRoot.SetAsLastSibling();
        Stretch(inspectionRoot, 0f, 0f, 0f, 0f);
        Image rootImage = inspectionRoot.gameObject.AddComponent<Image>();
        ConfigureImage(rootImage, new Color(0.035f, 0.045f, 0.06f, 0.965f), true);

        Rect safeRect = ComputeSafeRect(referenceCanvasRect.rect);
        float width = safeRect.width;
        float height = safeRect.height;
        float gap = width * 0.02f;
        float gridWidth = width * 0.69f;
        float previewWidth = width * 0.29f;
        float actionHeight = height * 0.14f;
        float previewHeight = height * 0.7f;
        float topBottomMargin = height * 0.08f;
        float usableHeight = height - topBottomMargin * 2f;
        float centeredBlockHeight = usableHeight;
        float originX = safeRect.x;
        float originY = safeRect.y;

        RectTransform gridPanel = CreatePanel("GridPanel", inspectionRoot, new Color(0.08f, 0.1f, 0.125f, 0.92f));
        SetRect(gridPanel,
            originX,
            originY + (centeredBlockHeight - usableHeight) * 0.5f,
            gridWidth,
            usableHeight);

        GridLayoutGroup grid = gridPanel.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        int cellGap = Mathf.RoundToInt(width * 0.008f);
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
            RectTransform thumb = CreateUiObject($"Thumb_{i + 1:00}", gridPanel);
            Image thumbImage = thumb.gameObject.AddComponent<Image>();
            ConfigureImage(thumbImage, i == 0
                ? new Color(0.42f, 0.51f, 0.62f, 1f)
                : new Color(0.2f, 0.24f, 0.3f, 1f));
            Outline outline = thumb.gameObject.AddComponent<Outline>();
            outline.effectColor = i == 0
                ? new Color(0.9f, 0.97f, 1f, 0.75f)
                : new Color(1f, 1f, 1f, 0.08f);
            outline.effectDistance = new Vector2(2f, -2f);

            RectTransform inner = CreateUiObject("Face", thumb);
            Stretch(inner, 10f, 10f, -10f, -10f);
            Image innerImage = inner.gameObject.AddComponent<Image>();
            ConfigureImage(innerImage, i == 0
                ? new Color(0.3f, 0.36f, 0.44f, 1f)
                : new Color(0.11f, 0.13f, 0.17f, 1f));
        }

        float previewX = originX + gridWidth + gap;
        float previewY = originY + usableHeight - previewHeight;
        RectTransform previewPanel = CreatePanel("PreviewPanel", inspectionRoot, new Color(0.075f, 0.09f, 0.11f, 0.96f));
        SetRect(previewPanel, previewX, previewY, previewWidth, previewHeight);

        RectTransform previewCard = CreateUiObject("SelectedCardPlaceholder", previewPanel);
        float previewCardHeight = previewHeight * 0.82f;
        float previewCardWidth = Mathf.Min(previewWidth * 0.82f, previewCardHeight / 1.42f);
        SetCenteredRect(previewCard, previewWidth * 0.5f, previewHeight * 0.56f, previewCardWidth, previewCardHeight);
        Image previewCardImage = previewCard.gameObject.AddComponent<Image>();
        ConfigureImage(previewCardImage, new Color(0.23f, 0.28f, 0.35f, 1f));
        Outline previewOutline = previewCard.gameObject.AddComponent<Outline>();
        previewOutline.effectColor = new Color(0.82f, 0.9f, 1f, 0.32f);
        previewOutline.effectDistance = new Vector2(3f, -3f);

        RectTransform previewInner = CreateUiObject("Face", previewCard);
        Stretch(previewInner, 18f, 18f, -18f, -18f);
        Image previewInnerImage = previewInner.gameObject.AddComponent<Image>();
        ConfigureImage(previewInnerImage, new Color(0.09f, 0.11f, 0.145f, 1f));

        float actionY = originY;
        RectTransform actionPanel = CreatePanel("ActionPanel", inspectionRoot, new Color(0.065f, 0.075f, 0.095f, 0.94f));
        SetRect(actionPanel, previewX, actionY, previewWidth, actionHeight);

        HorizontalLayoutGroup actions = actionPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        actions.childAlignment = TextAnchor.MiddleCenter;
        actions.childControlWidth = true;
        actions.childControlHeight = true;
        actions.childForceExpandWidth = true;
        actions.childForceExpandHeight = true;
        actions.spacing = width * 0.012f;
        int actionPad = Mathf.RoundToInt(width * 0.012f);
        actions.padding = new RectOffset(actionPad, actionPad, actionPad, actionPad);

        CreateButtonPlaceholder(actionPanel, "OpenAnotherButton", "Open Another", new Color(0.21f, 0.25f, 0.31f, 1f));
        CreateButtonPlaceholder(actionPanel, "BackToShopButton", "Back to Shop", new Color(0.16f, 0.19f, 0.24f, 1f));

        EditorSceneManager.MarkSceneDirty(previewCanvasObject.scene);
        Debug.Log("[PackOpening] Rebuilt inspection wireframe preview.");
    }

    private static Rect ComputeSafeRect(Rect canvasRect)
    {
        float horizontalInset = Mathf.Max(canvasRect.width * 0.035f, 54f);
        float verticalInset = Mathf.Max(canvasRect.height * 0.055f, 38f);
        return new Rect(
            horizontalInset,
            verticalInset,
            canvasRect.width - horizontalInset * 2f,
            canvasRect.height - verticalInset * 2f);
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

        RectTransform labelRect = CreateUiObject("Label", buttonRect);
        Stretch(labelRect, 0f, 0f, 0f, 0f);
        Text text = labelRect.gameObject.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = 22;
        text.color = new Color(0.84f, 0.89f, 0.95f, 0.92f);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.raycastTarget = false;
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

    private static Sprite GetSolidSprite()
    {
        if (_solidSprite != null)
        {
            return _solidSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "InspectionWireframeSolid";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);
        _solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return _solidSprite;
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    private static void SetRect(RectTransform rect, float left, float bottom, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(left, bottom);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetCenteredRect(RectTransform rect, float centerX, float centerY, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(centerX, centerY);
        rect.sizeDelta = new Vector2(width, height);
    }
}
