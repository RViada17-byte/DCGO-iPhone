using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShopPanel : MonoBehaviour
{
    private const string RuntimeRootName = "ShopRuntimeBody";
    private const string StructureDeckSectionName = "StructureDeckRows";
    private const string StarterSetSectionName = "StarterSetRows";
    private const string PackSectionName = "PackRows";
    private const string ResultsTextName = "ResultsText";
    private const string InfoTextName = "InfoText";
    private const string CurrencyTextName = "CurrencyText";
    private const string ResultsDialogRootName = "PurchaseResultsDialog";
    private const string ResultsDialogTitleName = "PurchaseResultsDialogTitle";
    private const string ResultsDialogBodyName = "PurchaseResultsDialogBody";
    private const int MaxResultHistoryLines = 64;

    [Header("Runtime UI Refs")]
    [SerializeField] private Text CurrencyText;
    [SerializeField] private Text InfoText;
    [SerializeField] private Text LastOpenResultsText;
    [SerializeField] private RectTransform StructureDeckSection;
    [SerializeField] private RectTransform PackSection;
    [SerializeField] private ScrollRect ProductScrollRect;
    private RectTransform StarterSetSection;
    private GameObject ResultsDialogRoot;
    private Text ResultsDialogTitleText;
    private Text ResultsDialogBodyText;
    private ScrollRect ResultsDialogScrollRect;

    private readonly List<ShopRowRefs> _rows = new List<ShopRowRefs>();
    private readonly List<string> _resultHistory = new List<string>();
    private bool _runtimeUiBuilt;
    private static Font _runtimeFont;

    public ShopPurchaseResult LastPurchaseResult { get; private set; }
    public bool IsPurchaseResultsDialogOpen => ResultsDialogRoot != null && ResultsDialogRoot.activeSelf;
    public string ActiveResultsDialogTitle => ResultsDialogTitleText != null ? ResultsDialogTitleText.text : string.Empty;
    public string ActiveResultsDialogBody => ResultsDialogBodyText != null ? ResultsDialogBodyText.text : string.Empty;

    private void OnEnable()
    {
        ProgressionManager.Instance.LoadOrCreate();
        ShopCatalogDatabase.Instance.Reload();
        ShopService.ReconcilePurchasedStructureDecks();
        EnsureRuntimeUi();
        LastPurchaseResult = null;
        HidePurchaseResultsDialog();
        RebuildProductRows();
        RefreshCurrency();
        RefreshProductStates();
        RefreshInfoText();
        RefreshResultsText();
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
    }

    private void EnsureRuntimeUi()
    {
        if (_runtimeUiBuilt &&
            CurrencyText != null &&
            InfoText != null &&
            LastOpenResultsText != null &&
            StructureDeckSection != null &&
            StarterSetSection != null &&
            PackSection != null &&
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

        CreateSectionHeader(content, "Structure Decks");
        StructureDeckSection = CreateSectionContainer(content, StructureDeckSectionName);

        CreateSectionHeader(content, "Starter Sets");
        StarterSetSection = CreateSectionContainer(content, StarterSetSectionName);

        CreateSectionHeader(content, "Booster Packs");
        PackSection = CreateSectionContainer(content, PackSectionName);

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

    private void RebuildProductRows()
    {
        if (StructureDeckSection == null || StarterSetSection == null || PackSection == null)
        {
            return;
        }

        ClearChildren(StructureDeckSection);
        ClearChildren(StarterSetSection);
        ClearChildren(PackSection);
        _rows.Clear();

        IReadOnlyList<ShopProductDef> products = ShopCatalogDatabase.Instance.Products;
        AddRowsForGroup(products, StructureDeckSection, ShopProductDisplayGroup.StructureDecks);
        AddRowsForGroup(products, StarterSetSection, ShopProductDisplayGroup.StarterSets);
        AddRowsForGroup(products, PackSection, ShopProductDisplayGroup.BoosterPacks, promoLast: true);
    }

    private void AddRowsForGroup(
        IReadOnlyList<ShopProductDef> products,
        RectTransform parent,
        ShopProductDisplayGroup group,
        bool promoLast = false)
    {
        if (products == null || parent == null)
        {
            return;
        }

        if (!promoLast)
        {
            for (int index = 0; index < products.Count; index++)
            {
                ShopProductDef product = products[index];
                if (product != null && product.DisplayGroup == group)
                {
                    _rows.Add(CreateProductRow(parent, product));
                }
            }

            return;
        }

        for (int pass = 0; pass < 2; pass++)
        {
            bool promoPass = pass == 1;
            for (int index = 0; index < products.Count; index++)
            {
                ShopProductDef product = products[index];
                if (product == null || product.DisplayGroup != group)
                {
                    continue;
                }

                bool isPromo = IsPromoProduct(product);
                if (promoPass != isPromo)
                {
                    continue;
                }

                _rows.Add(CreateProductRow(parent, product));
            }
        }
    }

    private void RefreshProductStates()
    {
        int currency = ProgressionManager.Instance.GetCurrency();
        HashSet<string> unlockedCardIds = ProgressionManager.Instance.GetUnlockedCardIdSetSnapshot();

        for (int index = 0; index < _rows.Count; index++)
        {
            ShopRowRefs row = _rows[index];
            if (row == null || row.Product == null)
            {
                continue;
            }

            bool locked = !ShopService.IsProductUnlocked(row.Product);
            bool purchased = ShopService.IsProductPurchased(row.Product);
            bool afford = currency >= row.Product.price;

            row.StatusText.text = BuildStatusLabel(row.Product, locked, purchased, unlockedCardIds);
            row.PriceText.text = "$ " + row.Product.price;

            bool singlePurchaseProduct = ShopService.IsSinglePurchaseProduct(row.Product);
            string buttonLabel = row.Product.IsPack
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

            row.ButtonLabel.text = buttonLabel;
            row.BuyButton.interactable = !locked && (!purchased || !singlePurchaseProduct) && afford;

            Image background = row.BuyButton.GetComponent<Image>();
            if (background != null)
            {
                background.color = row.BuyButton.interactable
                    ? new Color(0.27f, 0.49f, 0.23f, 0.95f)
                    : new Color(0.23f, 0.23f, 0.25f, 0.85f);
            }
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

        InfoText.text = "Structure decks unlock premade decks for $500. Starter Sets unlock their full card pool in one purchase, BT and EX packs cost $250 for 12 cards, and Promo Packs cost $1000 for 2 promo cards with 1 guaranteed new promo if available.";
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

    private ShopRowRefs CreateProductRow(RectTransform parent, ShopProductDef product)
    {
        RectTransform rowRoot = new GameObject(product.id + "_Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
        rowRoot.SetParent(parent, false);
        rowRoot.sizeDelta = new Vector2(0f, 58f);

        Image rowBackground = rowRoot.GetComponent<Image>();
        rowBackground.color = new Color(0.15f, 0.17f, 0.22f, 0.96f);

        HorizontalLayoutGroup rowLayout = rowRoot.GetComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(16, 16, 10, 10);
        rowLayout.spacing = 12f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        LayoutElement rowElement = rowRoot.GetComponent<LayoutElement>();
        rowElement.preferredHeight = 58f;

        Text titleText = CreateRowText(rowRoot, "Title", GetProductTitle(product), 20, TextAnchor.MiddleLeft, FontStyle.Bold);
        LayoutElement titleLayout = titleText.gameObject.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1f;
        titleLayout.minWidth = 300f;
        titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        titleText.verticalOverflow = VerticalWrapMode.Truncate;

        Text statusText = CreateRowText(rowRoot, "Status", string.Empty, 18, TextAnchor.MiddleCenter, FontStyle.Bold);
        LayoutElement statusLayout = statusText.gameObject.AddComponent<LayoutElement>();
        statusLayout.preferredWidth = 150f;

        Text priceText = CreateRowText(rowRoot, "Price", string.Empty, 18, TextAnchor.MiddleRight, FontStyle.Bold);
        LayoutElement priceLayout = priceText.gameObject.AddComponent<LayoutElement>();
        priceLayout.preferredWidth = 90f;

        Button buyButton = CreateButton(rowRoot, "BuyButton", product.IsPack ? "Buy Pack" : "Buy Deck");
        LayoutElement buttonLayout = buyButton.gameObject.AddComponent<LayoutElement>();
        buttonLayout.preferredWidth = 150f;
        buttonLayout.preferredHeight = 38f;

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => OnClickBuyProduct(product));

        return new ShopRowRefs
        {
            Product = product,
            StatusText = statusText,
            PriceText = priceText,
            BuyButton = buyButton,
            ButtonLabel = buyButton.GetComponentInChildren<Text>(true),
        };
    }

    private static RectTransform CreateSectionContainer(RectTransform parent, string name)
    {
        RectTransform container = FindOrCreateRectTransform(parent, name);
        VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

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

    private static Text CreateRowText(Transform parent, string name, string text, int fontSize, TextAnchor alignment, FontStyle fontStyle)
    {
        Text label = FindOrCreateText(parent, name, fontSize, alignment, fontStyle);
        label.text = text;
        return label;
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

    private static string BuildStatusLabel(ShopProductDef product, bool locked, bool purchased, ISet<string> unlockedCardIds)
    {
        if (locked)
        {
            return "LOCKED";
        }

        if (product != null && product.IsPack)
        {
            if (ShopService.IsSinglePurchaseProduct(product) && purchased)
            {
                return "OWNED";
            }

            int ownedCount = PackService.CountOwnedCardsForSet(product.setId, unlockedCardIds);
            int totalCount = PackService.GetUniqueCardCountForSet(product.setId);
            return totalCount > 0
                ? $"{ownedCount}/{totalCount} OWNED"
                : "AVAILABLE";
        }

        return purchased ? "OWNED" : "AVAILABLE";
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

    private sealed class ShopRowRefs
    {
        public ShopProductDef Product;
        public Text StatusText;
        public Text PriceText;
        public Button BuyButton;
        public Text ButtonLabel;
    }
}
