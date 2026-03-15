using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShopPanel : MonoBehaviour
{
    private const string RuntimeRootName = "ShopRuntimeBody";
    private const string STSectionName = "STGrid";
    private const string BTSectionName = "BTGrid";
    private const string EXSectionName = "EXGrid";
    private const string PromoSectionName = "PromoGrid";
    private const string ResultsTextName = "ResultsText";
    private const string InfoTextName = "InfoText";
    private const string CurrencyTextName = "CurrencyText";
    private const string ResultsDialogRootName = "PurchaseResultsDialog";
    private const string ResultsDialogTitleName = "PurchaseResultsDialogTitle";
    private const string ResultsDialogBodyName = "PurchaseResultsDialogBody";
    private const int MaxResultHistoryLines = 64;
    private const int GridColumns = 6;
    private const float TileVerticalSpacing = 2f;
    private const int TilePadding = 6;
    private const float TileArtAspectHeight = 1.95f;
    private const float TileArtMinHeight = 180f;
    private const float TileHeightFudge = 4f;

    [Header("Runtime UI Refs")]
    [SerializeField] private Text CurrencyText;
    [SerializeField] private Text InfoText;
    [SerializeField] private Text LastOpenResultsText;
    [SerializeField] private RectTransform STSection;
    [SerializeField] private RectTransform BTSection;
    [SerializeField] private RectTransform EXSection;
    [SerializeField] private RectTransform PromoSection;
    [SerializeField] private ScrollRect ProductScrollRect;
    [Header("Pack Opening")]
    [SerializeField] private PackOpeningController PackOpeningOverlayPrefab;
    [SerializeField] private PackPresentationCatalog PackOpeningPresentationCatalog;
    private GameObject ResultsDialogRoot;
    private Text ResultsDialogTitleText;
    private Text ResultsDialogBodyText;
    private ScrollRect ResultsDialogScrollRect;

    private readonly List<ShopTileRefs> _tiles = new List<ShopTileRefs>();
    private readonly List<string> _resultHistory = new List<string>();
    private bool _runtimeUiBuilt;
    private static Font _runtimeFont;
    private PackOpeningController _packOpeningOverlayInstance;

    public ShopPurchaseResult LastPurchaseResult { get; private set; }
    public bool IsPurchaseResultsDialogOpen => ResultsDialogRoot != null && ResultsDialogRoot.activeSelf;
    public string ActiveResultsDialogTitle => ResultsDialogTitleText != null ? ResultsDialogTitleText.text : string.Empty;
    public string ActiveResultsDialogBody => ResultsDialogBodyText != null ? ResultsDialogBodyText.text : string.Empty;

    private void OnEnable()
    {
        ProgressionManager.Instance.LoadOrCreate();
        ShopCatalogDatabase.Instance.EnsureLoaded();
        ShopService.ReconcilePurchasedStructureDecks();
        EnsureRuntimeUi();
        LastPurchaseResult = null;
        HidePurchaseResultsDialog();

        if (NeedsProductTileRebuild())
        {
            RebuildProductTiles();
        }

        RefreshCurrency();
        RefreshProductStates();
        RefreshInfoText();
        RefreshResultsText();
        RefreshGridLayout();
    }

    public void RefreshCurrency()
    {
        if (CurrencyText == null)
        {
            return;
        }

        CurrencyText.text = "$ " + ProgressionManager.Instance.GetCurrency();
    }

    public void RefreshView()
    {
        RefreshCurrency();
        RefreshProductStates();
        RefreshInfoText();
        RefreshResultsText();
        RefreshGridLayout();
    }

    private void EnsureRuntimeUi()
    {
        if (_runtimeUiBuilt &&
            CurrencyText != null &&
            InfoText != null &&
            LastOpenResultsText != null &&
            STSection != null &&
            BTSection != null &&
            EXSection != null &&
            PromoSection != null &&
            ProductScrollRect != null &&
            ResultsDialogRoot != null &&
            ResultsDialogTitleText != null &&
            ResultsDialogBodyText != null &&
            ResultsDialogScrollRect != null)
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
        runtimeRoot.offsetMin = new Vector2(48f, 36f);
        runtimeRoot.offsetMax = new Vector2(-48f, -220f);

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
        currencyRect.anchoredPosition = new Vector2(-20f, -22f);
        currencyRect.sizeDelta = new Vector2(260f, 44f);

        InfoText = FindOrCreateText(runtimeRoot, InfoTextName, 22, TextAnchor.UpperLeft, FontStyle.Normal);
        RectTransform infoRect = InfoText.rectTransform;
        infoRect.anchorMin = new Vector2(0f, 1f);
        infoRect.anchorMax = new Vector2(1f, 1f);
        infoRect.pivot = new Vector2(0.5f, 1f);
        infoRect.offsetMin = new Vector2(20f, -78f);
        infoRect.offsetMax = new Vector2(-300f, -22f);

        RectTransform scrollRoot = FindOrCreateRectTransform(runtimeRoot, "ScrollRoot");
        scrollRoot.anchorMin = new Vector2(0f, 0f);
        scrollRoot.anchorMax = new Vector2(1f, 1f);
        scrollRoot.offsetMin = new Vector2(20f, 20f);
        scrollRoot.offsetMax = new Vector2(-20f, -94f);

        Image scrollBackground = scrollRoot.GetComponent<Image>();
        if (scrollBackground == null)
        {
            scrollBackground = scrollRoot.gameObject.AddComponent<Image>();
        }

        scrollBackground.color = new Color(0.09f, 0.11f, 0.17f, 0.86f);
        scrollBackground.raycastTarget = true;

        ScrollRect scrollRect = scrollRoot.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        }

        ProductScrollRect = scrollRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 24f;

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
        viewportImage.raycastTarget = true;

        Mask viewportMask = viewport.GetComponent<Mask>();
        if (viewportMask == null)
        {
            viewportMask = viewport.gameObject.AddComponent<Mask>();
        }

        viewportMask.showMaskGraphic = false;

        RectTransform content = FindOrCreateRectTransform(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        if (contentLayout == null)
        {
            contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        contentLayout.padding = new RectOffset(18, 18, 18, 18);
        contentLayout.spacing = 14f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = content.GetComponent<ContentSizeFitter>();
        if (contentFitter == null)
        {
            contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        }

        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;

        CreateSectionHeader(content, "ST");
        STSection = CreateGridSectionContainer(content, STSectionName);

        CreateSectionHeader(content, "BT");
        BTSection = CreateGridSectionContainer(content, BTSectionName);

        CreateSectionHeader(content, "EX");
        EXSection = CreateGridSectionContainer(content, EXSectionName);

        CreateSectionHeader(content, "Promo");
        PromoSection = CreateGridSectionContainer(content, PromoSectionName);

        CreateSectionHeader(content, "Results");
        RectTransform resultsFrame = FindOrCreateRectTransform(content, "ResultsFrame");
        resultsFrame.sizeDelta = new Vector2(0f, 0f);

        Image resultsBackground = resultsFrame.GetComponent<Image>();
        if (resultsBackground == null)
        {
            resultsBackground = resultsFrame.gameObject.AddComponent<Image>();
        }

        resultsBackground.color = new Color(0.12f, 0.13f, 0.18f, 0.95f);

        LayoutElement resultsLayout = resultsFrame.GetComponent<LayoutElement>();
        if (resultsLayout == null)
        {
            resultsLayout = resultsFrame.gameObject.AddComponent<LayoutElement>();
        }

        resultsLayout.preferredHeight = 320f;

        LastOpenResultsText = FindOrCreateText(resultsFrame, ResultsTextName, 20, TextAnchor.UpperLeft, FontStyle.Normal);
        RectTransform resultsRect = LastOpenResultsText.rectTransform;
        resultsRect.anchorMin = Vector2.zero;
        resultsRect.anchorMax = Vector2.one;
        resultsRect.offsetMin = new Vector2(18f, 18f);
        resultsRect.offsetMax = new Vector2(-18f, -18f);
        LastOpenResultsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        LastOpenResultsText.verticalOverflow = VerticalWrapMode.Overflow;

        BuildResultsDialog(runtimeRoot);
        _runtimeUiBuilt = true;
    }

    private void BuildResultsDialog(RectTransform runtimeRoot)
    {
        RectTransform dialogRoot = FindOrCreateRectTransform(runtimeRoot, ResultsDialogRootName);
        dialogRoot.anchorMin = Vector2.zero;
        dialogRoot.anchorMax = Vector2.one;
        dialogRoot.offsetMin = Vector2.zero;
        dialogRoot.offsetMax = Vector2.zero;
        dialogRoot.SetAsLastSibling();
        ResultsDialogRoot = dialogRoot.gameObject;

        Image overlay = dialogRoot.GetComponent<Image>();
        if (overlay == null)
        {
            overlay = dialogRoot.gameObject.AddComponent<Image>();
        }

        overlay.color = new Color(0.02f, 0.03f, 0.06f, 0.88f);
        overlay.raycastTarget = true;

        RectTransform dialogFrame = FindOrCreateRectTransform(dialogRoot, "DialogFrame");
        dialogFrame.anchorMin = new Vector2(0.14f, 0.12f);
        dialogFrame.anchorMax = new Vector2(0.86f, 0.88f);
        dialogFrame.offsetMin = Vector2.zero;
        dialogFrame.offsetMax = Vector2.zero;

        Image dialogFrameBackground = dialogFrame.GetComponent<Image>();
        if (dialogFrameBackground == null)
        {
            dialogFrameBackground = dialogFrame.gameObject.AddComponent<Image>();
        }

        dialogFrameBackground.color = new Color(0.08f, 0.1f, 0.15f, 0.98f);
        dialogFrameBackground.raycastTarget = true;

        ResultsDialogTitleText = FindOrCreateText(dialogFrame, ResultsDialogTitleName, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
        RectTransform titleRect = ResultsDialogTitleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(28f, -64f);
        titleRect.offsetMax = new Vector2(-28f, -16f);

        RectTransform scrollRoot = FindOrCreateRectTransform(dialogFrame, "DialogScrollRoot");
        scrollRoot.anchorMin = new Vector2(0f, 0f);
        scrollRoot.anchorMax = new Vector2(1f, 1f);
        scrollRoot.offsetMin = new Vector2(28f, 92f);
        scrollRoot.offsetMax = new Vector2(-28f, -92f);

        Image scrollRootBackground = scrollRoot.GetComponent<Image>();
        if (scrollRootBackground == null)
        {
            scrollRootBackground = scrollRoot.gameObject.AddComponent<Image>();
        }

        scrollRootBackground.color = new Color(0.11f, 0.13f, 0.19f, 0.96f);
        scrollRootBackground.raycastTarget = true;

        ScrollRect dialogScrollRect = scrollRoot.GetComponent<ScrollRect>();
        if (dialogScrollRect == null)
        {
            dialogScrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        }

        dialogScrollRect.horizontal = false;
        dialogScrollRect.vertical = true;
        dialogScrollRect.scrollSensitivity = 24f;
        ResultsDialogScrollRect = dialogScrollRect;

        RectTransform viewport = FindOrCreateRectTransform(scrollRoot, "Viewport");
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(12f, 12f);
        viewport.offsetMax = new Vector2(-12f, -12f);

        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage == null)
        {
            viewportImage = viewport.gameObject.AddComponent<Image>();
        }

        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;

        Mask viewportMask = viewport.GetComponent<Mask>();
        if (viewportMask == null)
        {
            viewportMask = viewport.gameObject.AddComponent<Mask>();
        }

        viewportMask.showMaskGraphic = false;

        RectTransform content = FindOrCreateRectTransform(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        if (contentLayout == null)
        {
            contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        contentLayout.padding = new RectOffset(0, 0, 0, 0);
        contentLayout.spacing = 8f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter dialogContentFitter = content.GetComponent<ContentSizeFitter>();
        if (dialogContentFitter == null)
        {
            dialogContentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        }

        dialogContentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        dialogContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        dialogScrollRect.viewport = viewport;
        dialogScrollRect.content = content;

        ResultsDialogBodyText = FindOrCreateText(content, ResultsDialogBodyName, 20, TextAnchor.UpperLeft, FontStyle.Normal);
        ResultsDialogBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        ResultsDialogBodyText.verticalOverflow = VerticalWrapMode.Overflow;
        LayoutElement bodyLayout = ResultsDialogBodyText.GetComponent<LayoutElement>();
        if (bodyLayout == null)
        {
            bodyLayout = ResultsDialogBodyText.gameObject.AddComponent<LayoutElement>();
        }

        bodyLayout.minHeight = 0f;

        Button closeButton = CreateButton(dialogFrame, "CloseButton", "Close");
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 28f);
        closeRect.sizeDelta = new Vector2(220f, 52f);
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(HidePurchaseResultsDialog);

        ResultsDialogRoot.SetActive(false);
    }

    private void RebuildProductTiles()
    {
        if (STSection == null || BTSection == null || EXSection == null || PromoSection == null)
        {
            return;
        }

        ClearChildren(STSection);
        ClearChildren(BTSection);
        ClearChildren(EXSection);
        ClearChildren(PromoSection);
        _tiles.Clear();

        List<ShopProductDef> orderedProducts = ShopCatalogDatabase.Instance.Products
            .Where(product => product != null)
            .OrderBy(GetSectionSortKey)
            .ThenBy(GetSetSortNumber)
            .ThenBy(product => GetProductTitle(product), StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int index = 0; index < orderedProducts.Count; index++)
        {
            ShopProductDef product = orderedProducts[index];
            RectTransform parent = ResolveSectionContainer(product);
            if (parent == null)
            {
                continue;
            }

            _tiles.Add(CreateProductTile(parent, product));
        }
    }

    private bool NeedsProductTileRebuild()
    {
        IReadOnlyList<ShopProductDef> products = ShopCatalogDatabase.Instance.Products;
        if (_tiles.Count != products.Count)
        {
            return true;
        }

        for (int index = 0; index < _tiles.Count; index++)
        {
            ShopTileRefs tile = _tiles[index];
            if (tile == null ||
                tile.Product == null ||
                tile.TitleText == null ||
                tile.CountText == null ||
                tile.PriceText == null ||
                tile.BuyButton == null ||
                tile.ButtonLabel == null ||
                tile.ArtImage == null ||
                tile.ArtFallbackText == null ||
                tile.ArtLayout == null)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshProductStates()
    {
        int currency = ProgressionManager.Instance.GetCurrency();
        HashSet<string> ownedPrintIds = ProgressionManager.Instance.GetOwnedPrintIdSetSnapshot();
        BuildPrintCountsBySet(ownedPrintIds, out Dictionary<string, int> ownedCountsBySet, out Dictionary<string, int> totalCountsBySet);

        for (int index = 0; index < _tiles.Count; index++)
        {
            ShopTileRefs tile = _tiles[index];
            if (tile == null || tile.Product == null)
            {
                continue;
            }

            bool locked = !ShopService.IsProductUnlocked(tile.Product);
            bool purchased = ShopService.IsProductPurchased(tile.Product);
            bool afford = currency >= tile.Product.price;

            string normalizedSetId = NormalizeSetId(tile.Product.setId);
            int ownedCount = ownedCountsBySet.TryGetValue(normalizedSetId, out int cachedOwnedCount) ? cachedOwnedCount : 0;
            int totalCount = totalCountsBySet.TryGetValue(normalizedSetId, out int cachedTotalCount) ? cachedTotalCount : 0;

            tile.CountText.text = totalCount > 0 ? $"{ownedCount} / {totalCount}" : "-- / --";
            tile.PriceText.text = BuildTilePriceLabel(tile.Product, locked, purchased);

            bool singlePurchaseProduct = ShopService.IsSinglePurchaseProduct(tile.Product);
            string buttonLabel = tile.Product.IsPack
                ? (singlePurchaseProduct ? "Unlock Set" : "Buy Pack")
                : "Buy Deck";

            if (locked)
            {
                buttonLabel = "Locked";
            }
            else if (purchased && singlePurchaseProduct)
            {
                buttonLabel = "Owned";
            }
            else if (!afford)
            {
                buttonLabel = "Need Cash";
            }

            tile.ButtonLabel.text = buttonLabel;
            tile.BuyButton.interactable = !locked && (!purchased || !singlePurchaseProduct) && afford;

            PackPresentationTheme theme = tile.Theme ?? ResolveProductTheme(tile.Product);
            Sprite packArt = ResolveProductPackArt(tile.Product, theme);
            if (theme != null)
            {
                theme.packArt = packArt;
            }

            if (tile.ArtImage != null)
            {
                tile.ArtImage.sprite = packArt;
                tile.ArtImage.enabled = packArt != null;
            }

            if (tile.ArtFallbackText != null)
            {
                tile.ArtFallbackText.gameObject.SetActive(packArt == null);
            }

            tile.Theme = theme;
            ApplyTileVisualState(tile, theme, locked, purchased, totalCount > 0 && ownedCount >= totalCount);
        }
    }

    private void RefreshInfoText()
    {
        if (InfoText == null)
        {
            return;
        }

        if (ContinuousController.instance?.CardList == null || ContinuousController.instance.CardList.Length == 0)
        {
            InfoText.text = "Card data is still loading.";
            return;
        }

        if (ShopCatalogDatabase.Instance.Products.Count == 0)
        {
            InfoText.text = "No shop catalog entries found.";
            return;
        }

        InfoText.text = "Shop sets are grouped by ST, BT, and EX. Collection counts include alternate prints, so each tile shows owned prints over total prints for that set.";
    }

    private void RefreshResultsText()
    {
        if (LastOpenResultsText == null)
        {
            return;
        }

        if (_resultHistory.Count == 0)
        {
            LastOpenResultsText.text = "No purchases yet.";
            return;
        }

        LastOpenResultsText.text = string.Join("\n", _resultHistory);
    }

    private void RefreshGridLayout()
    {
        if (!_runtimeUiBuilt)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        RefreshGridCellSize(STSection);
        RefreshGridCellSize(BTSection);
        RefreshGridCellSize(EXSection);
        RefreshGridCellSize(PromoSection);
    }

    private void RefreshGridCellSize(RectTransform section)
    {
        if (section == null)
        {
            return;
        }

        GridLayoutGroup grid = section.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            return;
        }

        float availableWidth = section.rect.width;
        if (availableWidth <= 0f && ProductScrollRect != null && ProductScrollRect.viewport != null)
        {
            availableWidth = ProductScrollRect.viewport.rect.width - 36f;
        }

        if (availableWidth <= 0f)
        {
            return;
        }

        float spacing = grid.spacing.x;
        float cellWidth = Mathf.Floor((availableWidth - spacing * (GridColumns - 1)) / GridColumns);
        cellWidth = Mathf.Max(112f, cellWidth);
        float titleHeight = cellWidth >= 150f ? 20f : 18f;
        float countHeight = cellWidth >= 150f ? 14f : 12f;
        float priceHeight = cellWidth >= 150f ? 14f : 12f;
        float buttonHeight = cellWidth >= 150f ? 28f : 26f;
        float artHeight = Mathf.Max(TileArtMinHeight, Mathf.Round(cellWidth * TileArtAspectHeight));
        float cellHeight = Mathf.Round(
            artHeight +
            titleHeight +
            countHeight +
            priceHeight +
            buttonHeight +
            (TilePadding * 2f) +
            (TileVerticalSpacing * 4f) +
            TileHeightFudge);
        grid.cellSize = new Vector2(cellWidth, cellHeight);

        for (int index = 0; index < _tiles.Count; index++)
        {
            ShopTileRefs tile = _tiles[index];
            if (tile?.Root == null || tile.ArtLayout == null || tile.Root.parent != section)
            {
                continue;
            }

            tile.ArtLayout.preferredHeight = artHeight;
            if (tile.TitleText != null)
            {
                tile.TitleText.fontSize = cellWidth >= 150f ? 16 : 14;
            }

            if (tile.TitleLayout != null)
            {
                tile.TitleLayout.preferredHeight = titleHeight;
            }

            if (tile.CountText != null)
            {
                tile.CountText.fontSize = cellWidth >= 150f ? 12 : 11;
            }

            if (tile.CountLayout != null)
            {
                tile.CountLayout.preferredHeight = countHeight;
            }

            if (tile.PriceText != null)
            {
                tile.PriceText.fontSize = cellWidth >= 150f ? 12 : 11;
            }

            if (tile.PriceLayout != null)
            {
                tile.PriceLayout.preferredHeight = priceHeight;
            }

            if (tile.ButtonLabel != null)
            {
                tile.ButtonLabel.fontSize = cellWidth >= 150f ? 12 : 11;
            }

            if (tile.ButtonLayout != null)
            {
                tile.ButtonLayout.preferredHeight = buttonHeight;
            }
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        RefreshGridLayout();
    }

    private void OnClickBuyProduct(ShopProductDef product)
    {
        if (product == null)
        {
            return;
        }

        ShopPurchaseResult result = ShopService.Purchase(product);
        LastPurchaseResult = result;
        AppendPurchaseResult(result);
        RefreshCurrency();
        RefreshProductStates();
        RefreshInfoText();
        ScrollResultsIntoView();

        if (result != null && result.Succeeded)
        {
            ShowPurchaseResultsDialog(result);
            TryShowPackOpeningOverlay(product, result);
        }
        else
        {
            HidePurchaseResultsDialog();
        }
    }

    private void AppendPurchaseResult(ShopPurchaseResult result)
    {
        if (result == null)
        {
            return;
        }

        List<string> lines = new List<string>();
        string prefix = result.Succeeded ? "SHOP" : "ERROR";
        lines.Add($"{prefix}: {result.Message}");

        if (result.Lines != null && result.Lines.Count > 0)
        {
            lines.AddRange(result.Lines);
        }

        if (!string.IsNullOrWhiteSpace(result.SummaryLine))
        {
            lines.Insert(1, result.SummaryLine);
        }

        if (_resultHistory.Count > 0)
        {
            _resultHistory.Insert(0, string.Empty);
        }

        for (int index = lines.Count - 1; index >= 0; index--)
        {
            _resultHistory.Insert(0, lines[index]);
        }

        while (_resultHistory.Count > MaxResultHistoryLines)
        {
            _resultHistory.RemoveAt(_resultHistory.Count - 1);
        }

        RefreshResultsText();
    }

    private void ShowPurchaseResultsDialog(ShopPurchaseResult result)
    {
        if (result == null || ResultsDialogRoot == null || ResultsDialogTitleText == null || ResultsDialogBodyText == null)
        {
            return;
        }

        ResultsDialogTitleText.text = string.IsNullOrWhiteSpace(result.DialogTitle)
            ? "Purchase Results"
            : result.DialogTitle.Trim();
        ResultsDialogBodyText.text = BuildResultsDialogBody(result);

        ResultsDialogRoot.SetActive(true);
        ResultsDialogRoot.transform.SetAsLastSibling();

        if (ResultsDialogScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            ResultsDialogScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void HidePurchaseResultsDialog()
    {
        if (ResultsDialogRoot != null)
        {
            ResultsDialogRoot.SetActive(false);
        }
    }

    private string BuildResultsDialogBody(ShopPurchaseResult result)
    {
        List<string> lines = new List<string>();

        string deckReadyLine = ExtractDeckReadyLine(result);
        if (!string.IsNullOrWhiteSpace(deckReadyLine))
        {
            lines.Add(deckReadyLine);
        }

        if (!string.IsNullOrWhiteSpace(result.SummaryLine))
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add(result.SummaryLine);
        }

        if (result.CardResults != null && result.CardResults.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            for (int index = 0; index < result.CardResults.Count; index++)
            {
                ShopPurchaseCardResult cardResult = result.CardResults[index];
                if (cardResult == null)
                {
                    continue;
                }

                lines.Add(FormatDialogCardLine(cardResult));
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(result.Message);
        }

        return string.Join("\n", lines);
    }

    private static string ExtractDeckReadyLine(ShopPurchaseResult result)
    {
        if (result?.Lines == null)
        {
            return string.Empty;
        }

        for (int index = 0; index < result.Lines.Count; index++)
        {
            string line = result.Lines[index];
            if (!string.IsNullOrWhiteSpace(line) &&
                line.StartsWith("DECK READY:", StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
        }

        return string.Empty;
    }

    private static string FormatDialogCardLine(ShopPurchaseCardResult cardResult)
    {
        if (cardResult == null)
        {
            return string.Empty;
        }

        string ownershipLabel = cardResult.IsNew ? "NEW" : "OWNED";
        string countLabel = cardResult.Count > 1 ? $"{cardResult.Count}x " : string.Empty;
        return $"{ownershipLabel}  {countLabel}{cardResult.CardId} - {cardResult.CardName}";
    }

    private void TryShowPackOpeningOverlay(ShopProductDef product, ShopPurchaseResult result)
    {
        if (product == null ||
            !product.IsPack ||
            result == null ||
            !result.Succeeded ||
            result.CardResults == null ||
            result.CardResults.Count == 0)
        {
            return;
        }

        PackOpeningResult openingResult = PackOpeningResult.FromShopPurchase(product, result);
        if (openingResult == null)
        {
            Debug.LogWarning("[PackOpening] ShopPanel could not build a presentation result from the purchase payload.");
            return;
        }

        PackOpeningController overlay = GetOrCreatePackOpeningOverlay();
        if (overlay == null)
        {
            Debug.LogWarning("[PackOpening] ShopPanel could not load the PackOpeningOverlay prefab.");
            return;
        }

        overlay.Play(openingResult, PackOpeningPresentationCatalog, HidePurchaseResultsDialog);
    }

    private PackOpeningController GetOrCreatePackOpeningOverlay()
    {
        if (_packOpeningOverlayInstance != null)
        {
            return _packOpeningOverlayInstance;
        }

        PackOpeningController prefab = PackOpeningOverlayPrefab != null
            ? PackOpeningOverlayPrefab
            : Resources.Load<PackOpeningController>("PackOpeningOverlay");
        if (prefab == null)
        {
            return null;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;
        _packOpeningOverlayInstance = Instantiate(prefab, parent, false);
        _packOpeningOverlayInstance.name = prefab.name;
        _packOpeningOverlayInstance.gameObject.SetActive(false);
        return _packOpeningOverlayInstance;
    }

    private RectTransform ResolveSectionContainer(ShopProductDef product)
    {
        switch (GetProductSection(product))
        {
            case ShopProductSection.ST:
                return STSection;

            case ShopProductSection.EX:
                return EXSection;

            case ShopProductSection.Promo:
                return PromoSection;

            case ShopProductSection.BT:
            default:
                return BTSection;
        }
    }

    private static int GetSectionSortKey(ShopProductDef product)
    {
        switch (GetProductSection(product))
        {
            case ShopProductSection.ST:
                return 0;

            case ShopProductSection.BT:
                return 1;

            case ShopProductSection.EX:
                return 2;

            case ShopProductSection.Promo:
            default:
                return 3;
        }
    }

    private static ShopProductSection GetProductSection(ShopProductDef product)
    {
        string normalizedSetId = NormalizeSetId(product?.setId);
        if (normalizedSetId.StartsWith("ST", StringComparison.OrdinalIgnoreCase))
        {
            return ShopProductSection.ST;
        }

        if (normalizedSetId.StartsWith("EX", StringComparison.OrdinalIgnoreCase))
        {
            return ShopProductSection.EX;
        }

        if (normalizedSetId.StartsWith("LM", StringComparison.OrdinalIgnoreCase))
        {
            return ShopProductSection.Promo;
        }

        if (IsPromoProduct(product))
        {
            return ShopProductSection.Promo;
        }

        return ShopProductSection.BT;
    }

    private PackPresentationTheme ResolveProductTheme(ShopProductDef product)
    {
        PackPresentationCatalog catalog = PackOpeningPresentationCatalog != null
            ? PackOpeningPresentationCatalog
            : PackPresentationCatalog.LoadDefault();

        if (catalog != null)
        {
            return catalog.Resolve(product?.id, product?.setId);
        }

        return PackPresentationTheme.CreateFallback(product?.setId);
    }

    private static Sprite ResolveProductPackArt(ShopProductDef product, PackPresentationTheme theme)
    {
        if (!string.IsNullOrWhiteSpace(product?.setId))
        {
            Sprite localArt = PackPresentationArtCache.Load(product.setId);
            if (localArt != null)
            {
                return localArt;
            }
        }

        return theme?.packArt;
    }

    private static string BuildProductLabel(ShopProductDef product)
    {
        string normalizedSetId = NormalizeSetId(product?.setId);
        if (!string.IsNullOrWhiteSpace(normalizedSetId))
        {
            return string.Equals(normalizedSetId, "P", StringComparison.OrdinalIgnoreCase)
                ? "PROMO"
                : normalizedSetId;
        }

        return GetProductTitle(product);
    }

    private static string BuildTilePriceLabel(ShopProductDef product, bool locked, bool purchased)
    {
        if (locked)
        {
            return "LOCKED";
        }

        if (purchased && ShopService.IsSinglePurchaseProduct(product))
        {
            return "OWNED";
        }

        return "$ " + Mathf.Max(0, product?.price ?? 0);
    }

    private static void ApplyTileVisualState(ShopTileRefs tile, PackPresentationTheme theme, bool locked, bool purchased, bool complete)
    {
        if (tile == null || theme == null)
        {
            return;
        }

        Color baseTint = theme.packTint;
        if (locked)
        {
            baseTint = Color.Lerp(baseTint, Color.black, 0.42f);
        }
        else if (complete)
        {
            baseTint = Color.Lerp(baseTint, theme.accentColor, 0.24f);
        }

        if (tile.Background != null)
        {
            tile.Background.color = new Color(baseTint.r, baseTint.g, baseTint.b, 0.94f);
        }

        if (tile.ArtFrameBackground != null)
        {
            Color artFrameTint = Color.Lerp(theme.packTint, Color.black, locked ? 0.54f : 0.34f);
            tile.ArtFrameBackground.color = new Color(artFrameTint.r, artFrameTint.g, artFrameTint.b, 0.98f);
        }

        if (tile.ArtImage != null)
        {
            tile.ArtImage.color = locked
                ? new Color(0.78f, 0.78f, 0.78f, 0.58f)
                : Color.white;
        }

        if (tile.ArtFallbackText != null)
        {
            tile.ArtFallbackText.color = locked
                ? new Color(0.84f, 0.88f, 0.94f, 0.72f)
                : new Color(0.96f, 0.98f, 1f, 0.94f);
        }

        Image buttonBackground = tile.BuyButton != null ? tile.BuyButton.GetComponent<Image>() : null;
        if (buttonBackground != null)
        {
            buttonBackground.color = tile.BuyButton.interactable
                ? new Color(theme.accentColor.r, theme.accentColor.g, theme.accentColor.b, 0.94f)
                : new Color(0.24f, 0.25f, 0.29f, 0.88f);
        }

        if (tile.ButtonLabel != null)
        {
            tile.ButtonLabel.color = tile.BuyButton != null && tile.BuyButton.interactable
                ? new Color(0.06f, 0.07f, 0.1f, 1f)
                : new Color(0.88f, 0.9f, 0.94f, 0.96f);
        }
    }

    private static void BuildPrintCountsBySet(
        ISet<string> ownedPrintIds,
        out Dictionary<string, int> ownedCountsBySet,
        out Dictionary<string, int> totalCountsBySet)
    {
        ownedCountsBySet = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        totalCountsBySet = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        CEntity_Base[] cardList = ContinuousController.instance?.CardList;
        if (cardList == null || cardList.Length == 0)
        {
            return;
        }

        HashSet<string> seenSetPrints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < cardList.Length; index++)
        {
            CEntity_Base card = cardList[index];
            if (card == null || string.IsNullOrWhiteSpace(card.CardID) || string.IsNullOrWhiteSpace(card.SetID))
            {
                continue;
            }

            string normalizedSetId = NormalizeSetId(card.SetID);
            string normalizedPrintId = CardPrintCatalog.NormalizeLookupCode(card.EffectivePrintID);
            if (string.IsNullOrWhiteSpace(normalizedSetId) || string.IsNullOrWhiteSpace(normalizedPrintId))
            {
                continue;
            }

            string printKey = normalizedSetId + "::" + normalizedPrintId;
            if (!seenSetPrints.Add(printKey))
            {
                continue;
            }

            totalCountsBySet[normalizedSetId] = totalCountsBySet.TryGetValue(normalizedSetId, out int totalCount)
                ? totalCount + 1
                : 1;

            if (ownedPrintIds != null && ownedPrintIds.Contains(normalizedPrintId))
            {
                ownedCountsBySet[normalizedSetId] = ownedCountsBySet.TryGetValue(normalizedSetId, out int ownedCount)
                    ? ownedCount + 1
                    : 1;
            }
        }
    }

    private static int GetSetSortNumber(ShopProductDef product)
    {
        return ExtractTrailingNumber(NormalizeSetId(product?.setId));
    }

    private static int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return int.MaxValue;
        }

        int digitStart = -1;
        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsDigit(value[index]))
            {
                digitStart = index;
                break;
            }
        }

        if (digitStart < 0)
        {
            return int.MaxValue;
        }

        return int.TryParse(value.Substring(digitStart), out int number)
            ? number
            : int.MaxValue;
    }

    private static string NormalizeSetId(string setId)
    {
        return string.IsNullOrWhiteSpace(setId)
            ? string.Empty
            : setId.Trim().Replace("_", "-").ToUpperInvariant();
    }

    private ShopTileRefs CreateProductTile(RectTransform parent, ShopProductDef product)
    {
        PackPresentationTheme theme = ResolveProductTheme(product);
        if (theme == null)
        {
            theme = PackPresentationTheme.CreateFallback(product?.setId);
        }

        theme.packArt = ResolveProductPackArt(product, theme);

        string tileLabel = BuildProductLabel(product);

        RectTransform tileRoot = new GameObject(product.id + "_Tile", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        tileRoot.SetParent(parent, false);

        Image tileBackground = tileRoot.GetComponent<Image>();
        tileBackground.color = theme.packTint;

        VerticalLayoutGroup tileLayout = tileRoot.GetComponent<VerticalLayoutGroup>();
        tileLayout.padding = new RectOffset(TilePadding, TilePadding, TilePadding, TilePadding);
        tileLayout.spacing = TileVerticalSpacing;
        tileLayout.childAlignment = TextAnchor.UpperCenter;
        tileLayout.childControlWidth = true;
        tileLayout.childControlHeight = true;
        tileLayout.childForceExpandWidth = true;
        tileLayout.childForceExpandHeight = false;

        RectTransform artFrame = new GameObject("ArtFrame", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<RectTransform>();
        artFrame.SetParent(tileRoot, false);
        Image artFrameBackground = artFrame.GetComponent<Image>();
        artFrameBackground.color = Color.Lerp(theme.packTint, Color.black, 0.38f);
        LayoutElement artLayout = artFrame.GetComponent<LayoutElement>();
        artLayout.preferredHeight = 220f;
        artLayout.flexibleHeight = 0f;

        Image artImage = new GameObject("Art", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        artImage.rectTransform.SetParent(artFrame, false);
        artImage.rectTransform.anchorMin = new Vector2(0f, 0f);
        artImage.rectTransform.anchorMax = new Vector2(1f, 1f);
        artImage.rectTransform.offsetMin = new Vector2(2f, 2f);
        artImage.rectTransform.offsetMax = new Vector2(-2f, -2f);
        artImage.preserveAspect = true;
        artImage.sprite = theme.packArt;
        artImage.enabled = theme.packArt != null;

        Text artFallbackText = FindOrCreateText(artFrame, "ArtFallbackText", 18, TextAnchor.MiddleCenter, FontStyle.Bold);
        artFallbackText.text = tileLabel;
        artFallbackText.color = new Color(0.95f, 0.97f, 1f, 0.94f);
        RectTransform artFallbackRect = artFallbackText.rectTransform;
        artFallbackRect.anchorMin = Vector2.zero;
        artFallbackRect.anchorMax = Vector2.one;
        artFallbackRect.offsetMin = new Vector2(6f, 6f);
        artFallbackRect.offsetMax = new Vector2(-6f, -6f);
        artFallbackText.gameObject.SetActive(theme.packArt == null);

        Text titleText = FindOrCreateText(tileRoot, "Title", 16, TextAnchor.MiddleCenter, FontStyle.Bold);
        titleText.text = tileLabel;
        LayoutElement titleLayout = GetOrAddLayoutElement(titleText.gameObject);
        titleLayout.preferredHeight = 20f;
        titleLayout.flexibleHeight = 0f;

        Text countText = FindOrCreateText(tileRoot, "Count", 12, TextAnchor.MiddleCenter, FontStyle.Normal);
        countText.text = "-- / --";
        countText.color = new Color(0.82f, 0.88f, 0.95f, 0.96f);
        LayoutElement countLayout = GetOrAddLayoutElement(countText.gameObject);
        countLayout.preferredHeight = 14f;
        countLayout.flexibleHeight = 0f;

        Text priceText = FindOrCreateText(tileRoot, "Price", 12, TextAnchor.MiddleCenter, FontStyle.Bold);
        priceText.text = "$ " + product.price;
        LayoutElement priceLayout = GetOrAddLayoutElement(priceText.gameObject);
        priceLayout.preferredHeight = 14f;
        priceLayout.flexibleHeight = 0f;

        Button buyButton = CreateButton(tileRoot, "BuyButton", product.IsPack ? "Buy Pack" : "Buy Deck");
        LayoutElement buttonLayout = GetOrAddLayoutElement(buyButton.gameObject);
        buttonLayout.preferredHeight = 28f;
        buttonLayout.flexibleHeight = 0f;
        Text buttonLabel = buyButton.GetComponentInChildren<Text>(true);
        if (buttonLabel != null)
        {
            buttonLabel.fontSize = 12;
        }

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => OnClickBuyProduct(product));

        return new ShopTileRefs
        {
            Product = product,
            Theme = theme,
            Root = tileRoot,
            Background = tileBackground,
            ArtLayout = artLayout,
            ArtFrameBackground = artFrameBackground,
            ArtImage = artImage,
            ArtFallbackText = artFallbackText,
            TitleText = titleText,
            TitleLayout = titleLayout,
            CountText = countText,
            CountLayout = countLayout,
            PriceText = priceText,
            PriceLayout = priceLayout,
            BuyButton = buyButton,
            ButtonLayout = buttonLayout,
            ButtonLabel = buttonLabel,
        };
    }

    private static RectTransform CreateGridSectionContainer(RectTransform parent, string name)
    {
        RectTransform container = FindOrCreateRectTransform(parent, name);
        GridLayoutGroup layout = container.GetComponent<GridLayoutGroup>();
        if (layout == null)
        {
            layout = container.gameObject.AddComponent<GridLayoutGroup>();
        }

        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = new Vector2(12f, 10f);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = GridColumns;
        layout.cellSize = new Vector2(160f, 284f);

        ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = container.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return container;
    }

    private static void CreateSectionHeader(RectTransform parent, string label)
    {
        Text header = FindOrCreateText(parent, label.Replace(" ", string.Empty) + "Header", 28, TextAnchor.MiddleLeft, FontStyle.Bold);
        header.text = label;
        LayoutElement layout = header.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = header.gameObject.AddComponent<LayoutElement>();
        }

        layout.preferredHeight = 36f;
    }

    private void ScrollResultsIntoView()
    {
        if (ProductScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        ProductScrollRect.verticalNormalizedPosition = 0f;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        RectTransform buttonRoot = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        buttonRoot.SetParent(parent, false);

        Image background = buttonRoot.GetComponent<Image>();
        background.color = new Color(0.27f, 0.49f, 0.23f, 0.95f);

        Button button = buttonRoot.GetComponent<Button>();
        button.targetGraphic = background;

        Text buttonText = FindOrCreateText(buttonRoot, "Label", 18, TextAnchor.MiddleCenter, FontStyle.Bold);
        buttonText.text = label;
        RectTransform labelRect = buttonText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private static LayoutElement GetOrAddLayoutElement(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return null;
        }

        LayoutElement layoutElement = gameObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }

        return layoutElement;
    }

    private static Text FindOrCreateText(Transform parent, string name, int fontSize, TextAnchor alignment, FontStyle fontStyle)
    {
        Transform existing = parent.Find(name);
        Text text = existing != null ? existing.GetComponent<Text>() : null;
        if (text == null)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(parent, false);
            text = textObject.GetComponent<Text>();
        }

        text.font = GetRuntimeFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform FindOrCreateRectTransform(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rectTransform = child.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        return rectTransform;
    }

    private static Font GetRuntimeFont()
    {
        if (_runtimeFont != null)
        {
            return _runtimeFont;
        }

        _runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_runtimeFont == null)
        {
            _runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return _runtimeFont;
    }

    private static string GetProductTitle(ShopProductDef product)
    {
        if (!string.IsNullOrWhiteSpace(product?.title))
        {
            return product.title.Trim();
        }

        return product?.id ?? "Shop Product";
    }

    private static bool IsPromoProduct(ShopProductDef product)
    {
        if (product == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(product.setId) &&
            string.Equals(product.setId.Trim(), "P", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(product.id) &&
            product.id.IndexOf("promo", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            Destroy(parent.GetChild(index).gameObject);
        }
    }

    private enum ShopProductSection
    {
        ST = 0,
        BT = 1,
        EX = 2,
        Promo = 3,
    }

    private sealed class ShopTileRefs
    {
        public ShopProductDef Product;
        public PackPresentationTheme Theme;
        public RectTransform Root;
        public Image Background;
        public LayoutElement ArtLayout;
        public Image ArtFrameBackground;
        public Image ArtImage;
        public Text ArtFallbackText;
        public Text TitleText;
        public LayoutElement TitleLayout;
        public Text CountText;
        public LayoutElement CountLayout;
        public Text PriceText;
        public LayoutElement PriceLayout;
        public Button BuyButton;
        public LayoutElement ButtonLayout;
        public Text ButtonLabel;
    }
}
