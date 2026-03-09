using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class EditDeck : MonoBehaviour
{
    struct RectTransformState
    {
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 OffsetMin;
        public Vector2 OffsetMax;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public Vector2 Pivot;
        public Vector3 LocalScale;
        public bool ActiveSelf;

        public static RectTransformState Capture(RectTransform rectTransform)
        {
            return new RectTransformState
            {
                AnchorMin = rectTransform.anchorMin,
                AnchorMax = rectTransform.anchorMax,
                OffsetMin = rectTransform.offsetMin,
                OffsetMax = rectTransform.offsetMax,
                AnchoredPosition = rectTransform.anchoredPosition,
                SizeDelta = rectTransform.sizeDelta,
                Pivot = rectTransform.pivot,
                LocalScale = rectTransform.localScale,
                ActiveSelf = rectTransform.gameObject.activeSelf,
            };
        }

        public void Restore(RectTransform rectTransform)
        {
            rectTransform.anchorMin = AnchorMin;
            rectTransform.anchorMax = AnchorMax;
            rectTransform.offsetMin = OffsetMin;
            rectTransform.offsetMax = OffsetMax;
            rectTransform.anchoredPosition = AnchoredPosition;
            rectTransform.sizeDelta = SizeDelta;
            rectTransform.pivot = Pivot;
            rectTransform.localScale = LocalScale;
            rectTransform.gameObject.SetActive(ActiveSelf);
        }
    }

    struct GridLayoutState
    {
        public GridLayoutGroup.Constraint Constraint;
        public int ConstraintCount;
        public Vector2 CellSize;
        public Vector2 Spacing;
        public int PaddingLeft;
        public int PaddingRight;
        public int PaddingTop;
        public int PaddingBottom;

        public static GridLayoutState Capture(GridLayoutGroup gridLayoutGroup)
        {
            return new GridLayoutState
            {
                Constraint = gridLayoutGroup.constraint,
                ConstraintCount = gridLayoutGroup.constraintCount,
                CellSize = gridLayoutGroup.cellSize,
                Spacing = gridLayoutGroup.spacing,
                PaddingLeft = gridLayoutGroup.padding.left,
                PaddingRight = gridLayoutGroup.padding.right,
                PaddingTop = gridLayoutGroup.padding.top,
                PaddingBottom = gridLayoutGroup.padding.bottom,
            };
        }

        public void Restore(GridLayoutGroup gridLayoutGroup)
        {
            gridLayoutGroup.constraint = Constraint;
            gridLayoutGroup.constraintCount = ConstraintCount;
            gridLayoutGroup.cellSize = CellSize;
            gridLayoutGroup.spacing = Spacing;
            gridLayoutGroup.padding = new RectOffset(PaddingLeft, PaddingRight, PaddingTop, PaddingBottom);
        }
    }

    struct TransformParentState
    {
        public Transform Parent;
        public int SiblingIndex;

        public static TransformParentState Capture(Transform transform)
        {
            return new TransformParentState
            {
                Parent = transform.parent,
                SiblingIndex = transform.GetSiblingIndex(),
            };
        }

        public void Restore(Transform transform)
        {
            if (transform == null || Parent == null)
            {
                return;
            }

            transform.SetParent(Parent, false);
            transform.SetSiblingIndex(Mathf.Clamp(SiblingIndex, 0, Parent.childCount - 1));
        }
    }

    [Header("Deck creation object")]
    public GameObject CreateDeckObject;

    [Header("card prefab")]
    public CardPrefab_CreateDeck cardPrefab_CreateDeck;

    [Header("card pool scroll rect")]
    public ScrollRect CardPoolScroll;

    [Header("Deck card scroll rect")]
    public ScrollRect DeckScroll;

    [Header("Deck card number display text")]
    public Text DeckCountText;

    [Header("loading object")]
    public LoadingObject LoadingObjec;

    [Header("Card details display")]
    public DetailCard_DeckEditor DetailCard;

    [Header("filter")]
    public FilterCardList filterCardList;

    /*
    [Header("フィルターパネル")]
    public FilterPanel filterPanel;
    */

    [Header("Deck name input field")]
    public InputField DeckNameInputField;

    //Deck data being edited
    public DeckData EdittingDeckData { get; set; }

    bool _isFromSelectDeck = false;

    public UnityAction EndEditAction;

    public int DisplayPageIndex = 0;

    public Button UpDisplayPageIndexButton;
    public Button DownDisplayPageIndexButton;
    public Text ShowDisplayPageIndexText;
    public LoadingObject isSearchingObject;

    [Header("Page switching SE")]
    public AudioClip SwitchPageSE;

    [Header("Card classification display")]
    [SerializeField] CardDistribution cardDistribution;
    int _maxDisplayPageIndex;
    string _oldDeckName = "";
    List<CEntity_Base> _oldDeckCards = new List<CEntity_Base>();
    List<CEntity_Base> _oldDigitamaDeckCards = new List<CEntity_Base>();
    int _oldKeyCardId = -1;
    bool _isFromClipboard = false;
    readonly List<CEntity_Base> _filteredPoolCards = new List<CEntity_Base>();
    // Keep the existing paged collection path as the canonical deck editor flow.
    bool UseIPhoneDeckScroll => false;
    bool UseIPhoneSafeAreaLayout => _editorPreviewActive;
    bool UseIPhoneTapPreview => Application.platform == RuntimePlatform.IPhonePlayer && !_editorPreviewActive;
    IPhoneSafeAreaRoot _iPhoneSafeAreaRoot;
    Image _iPhoneBackdrop;
    bool _editorPreviewActive;
    bool _hasAppliedIPhoneDeckLayout;
    readonly Dictionary<RectTransform, RectTransformState> _iPhoneLayoutStates = new Dictionary<RectTransform, RectTransformState>();
    readonly Dictionary<Text, string> _iPhoneTextStates = new Dictionary<Text, string>();
    bool _detailCardActiveStateCaptured;
    bool _detailCardWasActive;
    readonly Dictionary<GridLayoutGroup, GridLayoutState> _iPhoneGridStates = new Dictionary<GridLayoutGroup, GridLayoutState>();
    readonly Dictionary<Transform, TransformParentState> _iPhoneParentStates = new Dictionary<Transform, TransformParentState>();
    RectTransform _iPhonePreviewBackdropRect;
    Button _iPhonePreviewBackdropButton;
    RectTransform _iPhonePreviewActionBarRect;
    Button _iPhonePreviewCloseButton;
    Button _iPhonePreviewAddButton;
    Button _iPhonePreviewRemoveButton;
    Text _iPhonePreviewCloseButtonText;
    Text _iPhonePreviewAddButtonText;
    Text _iPhonePreviewRemoveButtonText;
    CardPrefab_CreateDeck _iPhonePreviewSourceCard;
    bool _iPhonePreviewOpenedFromDeck;

    private void Start()
    {
#if !UNITY_EDITOR && UNITY_ANDROID
        CardPoolScroll.content.GetComponent<GridLayoutGroup>().constraintCount = 5;
#endif

        CaptureAuthoredDetailCardState();
        ApplyIPhoneCreateDeckLayout(force: true);
        EnsureCardPoolScrollConfiguredForIPhone();
        SetPageUiVisible(!UseIPhoneDeckScroll);
        SubscribeCardPoolScrollEvents();
    }

    void LateUpdate()
    {
        if (!isEditting)
        {
            return;
        }

        ApplyIPhoneCreateDeckLayout();
    }

    void OnDestroy()
    {
        if (CardPoolScroll != null)
        {
            CardPoolScroll.onValueChanged.RemoveListener(OnCardPoolScrollValueChanged);
        }
    }

    void SubscribeCardPoolScrollEvents()
    {
        if (CardPoolScroll == null)
        {
            return;
        }

        CardPoolScroll.onValueChanged.RemoveListener(OnCardPoolScrollValueChanged);
        CardPoolScroll.onValueChanged.AddListener(OnCardPoolScrollValueChanged);
    }

    void OnCardPoolScrollValueChanged(Vector2 _)
    {
        if (!UseIPhoneDeckScroll)
        {
            return;
        }

        RefreshVisiblePoolCards();
    }

    void EnsureCardPoolScrollConfiguredForIPhone()
    {
        if (!UseIPhoneDeckScroll || CardPoolScroll == null)
        {
            return;
        }

        CardPoolScroll.enabled = true;
        CardPoolScroll.vertical = true;
    }

    void CaptureAuthoredDetailCardState()
    {
        if (_detailCardActiveStateCaptured || DetailCard == null)
        {
            return;
        }

        _detailCardWasActive = DetailCard.gameObject.activeSelf;
        _detailCardActiveStateCaptured = true;
    }

    bool ShouldPreserveAuthoredDetailCardState()
    {
        CaptureAuthoredDetailCardState();
        bool preserveAuthoredState =
            Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.isEditor;
        return preserveAuthoredState && !UseIPhoneSafeAreaLayout && _detailCardActiveStateCaptured && _detailCardWasActive;
    }

    void ApplyIPhoneCreateDeckLayout(bool force = false)
    {
        if (!UseIPhoneSafeAreaLayout)
        {
            if (_hasAppliedIPhoneDeckLayout)
            {
                RestoreIPhoneDeckLayout();
            }

            return;
        }

        if (CreateDeckObject == null)
        {
            return;
        }

        RectTransform createDeckRect = CreateDeckObject.GetComponent<RectTransform>();
        RectTransform parentRect = createDeckRect != null ? createDeckRect.parent as RectTransform : null;
        if (createDeckRect == null || parentRect == null)
        {
            return;
        }

        if (Application.platform == RuntimePlatform.IPhonePlayer && _iPhoneSafeAreaRoot == null)
        {
            _iPhoneSafeAreaRoot = CreateDeckObject.GetComponent<IPhoneSafeAreaRoot>();

            if (_iPhoneSafeAreaRoot == null)
            {
                _iPhoneSafeAreaRoot = CreateDeckObject.AddComponent<IPhoneSafeAreaRoot>();
            }
        }

        CaptureIPhoneLayoutState(createDeckRect);

        if (_editorPreviewActive)
        {
            createDeckRect.anchorMin = Vector2.zero;
            createDeckRect.anchorMax = Vector2.one;
            createDeckRect.offsetMin = Vector2.zero;
            createDeckRect.offsetMax = Vector2.zero;
            createDeckRect.anchoredPosition = Vector2.zero;
        }

        if (Application.platform == RuntimePlatform.IPhonePlayer || _editorPreviewActive)
        {
            EnsureIPhoneBackdrop();
        }

        if (_iPhoneBackdrop != null)
        {
            _iPhoneBackdrop.gameObject.SetActive(true);
        }

        createDeckRect.localScale = Vector3.one;
        createDeckRect.SetAsLastSibling();
        ApplyIPhoneDeckPanelLayout();
        _hasAppliedIPhoneDeckLayout = true;

        if (force)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(createDeckRect);
        }
    }

    void CaptureIPhoneLayoutState(RectTransform rectTransform)
    {
        if (rectTransform == null || _iPhoneLayoutStates.ContainsKey(rectTransform))
        {
            return;
        }

        _iPhoneLayoutStates.Add(rectTransform, RectTransformState.Capture(rectTransform));
    }

    void RestoreIPhoneDeckLayout()
    {
        if (!_hasAppliedIPhoneDeckLayout)
        {
            return;
        }

        HideIPhonePreview(force: true);

        foreach (KeyValuePair<GridLayoutGroup, GridLayoutState> pair in _iPhoneGridStates)
        {
            if (pair.Key != null)
            {
                pair.Value.Restore(pair.Key);
            }
        }

        foreach (KeyValuePair<Transform, TransformParentState> pair in _iPhoneParentStates)
        {
            if (pair.Key != null)
            {
                pair.Value.Restore(pair.Key);
            }
        }

        foreach (KeyValuePair<RectTransform, RectTransformState> pair in _iPhoneLayoutStates)
        {
            if (pair.Key != null)
            {
                pair.Value.Restore(pair.Key);
            }
        }

        foreach (KeyValuePair<Text, string> pair in _iPhoneTextStates)
        {
            if (pair.Key != null)
            {
                pair.Key.text = pair.Value;
            }
        }

        if (_detailCardActiveStateCaptured && DetailCard != null)
        {
            DetailCard.gameObject.SetActive(_detailCardWasActive);
        }

        if (_iPhoneBackdrop != null)
        {
            _iPhoneBackdrop.gameObject.SetActive(false);
        }

        _hasAppliedIPhoneDeckLayout = false;
    }

    void ApplyIPhoneDeckPanelLayout()
    {
        RectTransform createDeckRect = CreateDeckObject != null
            ? CreateDeckObject.GetComponent<RectTransform>()
            : null;
        RectTransform deckCardsRoot = CreateDeckObject != null
            ? CreateDeckObject.transform.Find("DeckCards") as RectTransform
            : null;
        RectTransform cardPoolRoot = CreateDeckObject != null
            ? CreateDeckObject.transform.Find("CardPool") as RectTransform
            : null;
        RectTransform distributionRoot = cardDistribution != null
            ? cardDistribution.transform as RectTransform
            : null;
        RectTransform searchRoot = filterCardList != null
            ? filterCardList.transform as RectTransform
            : null;
        RectTransform searchInputRoot = searchRoot != null
            ? GetDirectChildRect(searchRoot, filterCardList != null ? filterCardList.SearchInputRoot : null)
            : null;
        RectTransform cardSetRoot = searchRoot != null
            ? GetDirectChildRect(searchRoot, filterCardList != null ? filterCardList.CardSetRoot : null)
            : null;
        RectTransform searchButtonRoot = searchRoot != null
            ? FindRectTransformByName(searchRoot, "SearchButton")
            : null;
        RectTransform resetButtonRoot = searchRoot != null
            ? FindRectTransformByName(searchRoot, "ResetButton")
            : null;
        RectTransform buttonsRoot = CreateDeckObject != null
            ? CreateDeckObject.transform.Find("Buttons") as RectTransform
            : null;
        RectTransform saveButtonRoot = buttonsRoot != null
            ? GetDirectChildRect(buttonsRoot, FindRectTransformByName(buttonsRoot, "Save")) ?? FindRectTransformByName(buttonsRoot, "Save")
            : null;
        RectTransform quitButtonRoot = buttonsRoot != null
            ? GetDirectChildRect(buttonsRoot, FindRectTransformByName(buttonsRoot, "Quit")) ?? FindRectTransformByName(buttonsRoot, "Quit")
            : null;
        RectTransform clearButtonRoot = buttonsRoot != null
            ? GetDirectChildRect(buttonsRoot, FindRectTransformByName(buttonsRoot, "Clear")) ?? FindRectTransformByName(buttonsRoot, "Clear")
            : null;
        RectTransform testDrawButtonRoot = buttonsRoot != null
            ? GetDirectChildRect(buttonsRoot, FindRectTransformByName(buttonsRoot, "TestDraw")) ?? FindRectTransformByName(buttonsRoot, "TestDraw")
            : null;
        RectTransform setKeyCardButtonRoot = buttonsRoot != null
            ? GetDirectChildRect(buttonsRoot, FindRectTransformByName(buttonsRoot, "SetKeyCard")) ?? FindRectTransformByName(buttonsRoot, "SetKeyCard")
            : null;
        Text quitButtonLabel = FindLabelText(quitButtonRoot);

        CaptureIPhoneLayoutState(createDeckRect);
        CaptureIPhoneLayoutState(deckCardsRoot);
        CaptureIPhoneLayoutState(cardPoolRoot);
        CaptureIPhoneLayoutState(distributionRoot);
        CaptureIPhoneLayoutState(searchRoot);
        CaptureIPhoneLayoutState(searchInputRoot);
        CaptureIPhoneLayoutState(cardSetRoot);
        CaptureIPhoneLayoutState(searchButtonRoot);
        CaptureIPhoneLayoutState(resetButtonRoot);
        CaptureIPhoneLayoutState(buttonsRoot);
        CaptureIPhoneLayoutState(saveButtonRoot);
        CaptureIPhoneLayoutState(quitButtonRoot);
        CaptureIPhoneLayoutState(clearButtonRoot);
        CaptureIPhoneLayoutState(testDrawButtonRoot);
        CaptureIPhoneLayoutState(setKeyCardButtonRoot);
        CaptureIPhoneTextState(quitButtonLabel);

        if (createDeckRect == null)
        {
            return;
        }

        RectTransform parentRect = createDeckRect.parent as RectTransform;
        float width = createDeckRect.rect.width > 400f
            ? createDeckRect.rect.width
            : parentRect != null ? parentRect.rect.width : Screen.width;
        float height = createDeckRect.rect.height > 220f
            ? createDeckRect.rect.height
            : parentRect != null ? parentRect.rect.height : Screen.height;
        float layoutT = Mathf.InverseLerp(1334f, 1920f, width);
        float topInset = Mathf.Lerp(26f, 34f, layoutT);
        float topRowHeight = Mathf.Lerp(66f, 74f, layoutT);
        float sideInset = Mathf.Lerp(18f, 26f, layoutT);
        float minGap = Mathf.Lerp(28f, 40f, layoutT);
        float buttonsWidth = Mathf.Clamp(width * 0.24f, 260f, 320f);
        float searchWidth = Mathf.Clamp(
            width - (sideInset * 2f) - minGap - buttonsWidth,
            440f,
            620f);
        float deckScale = Mathf.Lerp(1.06f, 1.12f, layoutT);
        float poolScale = Mathf.Lerp(1.04f, 1.09f, layoutT);
        float summaryScale = Mathf.Lerp(1.10f, 1.16f, layoutT);
        float contentTop = topInset + topRowHeight + Mathf.Lerp(22f, 30f, layoutT);

        if (deckCardsRoot != null)
        {
            SetAnchoredRootTransform(
                deckCardsRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(-width * 0.12f, -height * 0.055f),
                new Vector3(deckScale, deckScale, 1f));
        }

        if (cardPoolRoot != null)
        {
            SetAnchoredRootTransform(
                cardPoolRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(width * 0.38f, -height * 0.02f),
                new Vector3(poolScale, poolScale, 1f));
        }

        if (distributionRoot != null)
        {
            distributionRoot.anchorMin = new Vector2(0.5f, 1f);
            distributionRoot.anchorMax = new Vector2(0.5f, 1f);
            distributionRoot.pivot = new Vector2(0.5f, 1f);
            distributionRoot.anchoredPosition = new Vector2(-width * 0.18f, -contentTop);
            distributionRoot.localScale = new Vector3(summaryScale, summaryScale, 1f);
        }

        if (buttonsRoot != null)
        {
            SetTopLeftBox(buttonsRoot, sideInset, topInset, buttonsWidth, topRowHeight);
        }

        if (searchRoot != null)
        {
            SetTopRightBox(searchRoot, sideInset, topInset, searchWidth, topRowHeight);
        }

        LayoutIPhoneActionButtons(
            buttonsRoot,
            saveButtonRoot,
            clearButtonRoot,
            quitButtonRoot,
            testDrawButtonRoot,
            setKeyCardButtonRoot,
            buttonsWidth,
            topRowHeight);
        LayoutIPhoneCollectionControls(
            searchRoot,
            searchInputRoot,
            cardSetRoot,
            searchButtonRoot,
            resetButtonRoot,
            searchWidth,
            topRowHeight);
        SetButtonLabel(quitButtonRoot, LocalizeUtility.GetLocalizedString("Back", "戻る"));

        if (searchRoot != null)
        {
            searchRoot.SetAsLastSibling();
        }

        if (buttonsRoot != null)
        {
            buttonsRoot.SetAsLastSibling();
        }
    }

    void CaptureIPhoneGridState(GridLayoutGroup gridLayoutGroup)
    {
        if (gridLayoutGroup == null || _iPhoneGridStates.ContainsKey(gridLayoutGroup))
        {
            return;
        }

        _iPhoneGridStates.Add(gridLayoutGroup, GridLayoutState.Capture(gridLayoutGroup));
    }

    void CaptureIPhoneParentState(Transform transform)
    {
        if (transform == null || _iPhoneParentStates.ContainsKey(transform))
        {
            return;
        }

        _iPhoneParentStates.Add(transform, TransformParentState.Capture(transform));
    }

    static void SetAnchoredRootTransform(RectTransform rectTransform, Vector2 anchor, Vector2 anchoredPosition, Vector3 localScale)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.localScale = localScale;
    }

    static void StretchRect(RectTransform rectTransform, float leftInset, float topInset, float rightInset, float bottomInset)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(leftInset, bottomInset);
        rectTransform.offsetMax = new Vector2(-rightInset, -topInset);
        rectTransform.localScale = Vector3.one;
    }

    static void SetTopLeftBox(RectTransform rectTransform, float left, float top, float width, float height)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.anchoredPosition = new Vector2(left, -top);
        rectTransform.localScale = Vector3.one;
    }

    static void SetTopRightBox(RectTransform rectTransform, float right, float top, float width, float height)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(1f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.anchoredPosition = new Vector2(-right, -top);
        rectTransform.localScale = Vector3.one;
    }

    static void SetChildBox(RectTransform rectTransform, float x, float y, float width, float height, Vector2 pivot)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rectTransform.anchorMax = new Vector2(0f, 0.5f);
        rectTransform.pivot = pivot;
        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.anchoredPosition = new Vector2(x, y);
        rectTransform.localScale = Vector3.one;
    }

    static RectTransform FindRectTransformByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i].name == name)
            {
                return descendants[i] as RectTransform;
            }
        }

        return null;
    }

    static RectTransform GetDirectChildRect(Transform root, Transform descendant)
    {
        if (root == null || descendant == null)
        {
            return null;
        }

        Transform current = descendant;

        while (current != null && current.parent != root)
        {
            current = current.parent;
        }

        return current as RectTransform;
    }

    void CaptureIPhoneTextState(Text text)
    {
        if (text == null || _iPhoneTextStates.ContainsKey(text))
        {
            return;
        }

        _iPhoneTextStates.Add(text, text.text);
    }

    static Text FindLabelText(RectTransform root)
    {
        if (root == null)
        {
            return null;
        }

        Text[] texts = root.GetComponentsInChildren<Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && !string.IsNullOrEmpty(texts[i].text))
            {
                return texts[i];
            }
        }

        return root.GetComponentInChildren<Text>(true);
    }

    static void SetButtonLabel(RectTransform root, string label)
    {
        Text text = FindLabelText(root);

        if (text != null)
        {
            text.text = label;
        }
    }

    Vector3 GetIPhoneScaledLocalScale(RectTransform rectTransform, float multiplier)
    {
        if (rectTransform != null && _iPhoneLayoutStates.TryGetValue(rectTransform, out RectTransformState state))
        {
            return state.LocalScale * multiplier;
        }

        return new Vector3(multiplier, multiplier, 1f);
    }

    void LayoutIPhoneDeckHeader(
        RectTransform deckBarRoot,
        RectTransform deckNameRoot,
        RectTransform deckCountRoot,
        float deckColumnWidth,
        float deckBarHeight)
    {
        if (deckBarRoot == null)
        {
            return;
        }

        float innerPadding = 10f;
        float availableWidth = Mathf.Max(120f, deckColumnWidth - (innerPadding * 2f));
        float deckNameWidth = Mathf.Clamp(availableWidth * 0.46f, 140f, 260f);
        float deckNameHeight = Mathf.Clamp(deckBarHeight - 18f, 28f, 40f);
        float countWidth = Mathf.Clamp(availableWidth * 0.26f, 110f, 168f);

        if (deckNameRoot != null)
        {
            SetChildBox(deckNameRoot, innerPadding + (deckNameWidth * 0.5f), 0f, deckNameWidth, deckNameHeight, new Vector2(0.5f, 0.5f));
            deckNameRoot.localScale = new Vector3(0.8f, 0.8f, 1f);
        }

        if (deckCountRoot != null)
        {
            SetChildBox(deckCountRoot, availableWidth - (countWidth * 0.5f) + innerPadding, 0f, countWidth, deckNameHeight, new Vector2(0.5f, 0.5f));
            deckCountRoot.localScale = new Vector3(1.18f, 1.18f, 1f);
        }
    }

    void LayoutIPhoneCollectionControls(
        RectTransform searchRoot,
        RectTransform searchInputRoot,
        RectTransform cardSetRoot,
        RectTransform searchButtonRoot,
        RectTransform resetButtonRoot,
        float searchWidth,
        float topRowHeight)
    {
        if (searchRoot == null)
        {
            return;
        }

        if (resetButtonRoot != null)
        {
            resetButtonRoot.gameObject.SetActive(false);
        }

        float horizontalPadding = 8f;
        float gap = 8f;
        float elementHeight = Mathf.Clamp(topRowHeight - 14f, 42f, 54f);
        float searchButtonWidth = Mathf.Clamp(searchWidth * 0.18f, 102f, 124f);
        float cardSetWidth = Mathf.Clamp(searchWidth * 0.24f, 122f, 150f);
        float searchInputWidth = Mathf.Max(196f, searchWidth - (horizontalPadding * 2f) - (gap * 2f) - cardSetWidth - searchButtonWidth);
        float cursorX = horizontalPadding;

        if (searchInputRoot != null)
        {
            SetChildBox(searchInputRoot, cursorX + (searchInputWidth * 0.5f), 0f, searchInputWidth, elementHeight, new Vector2(0.5f, 0.5f));
            searchInputRoot.localScale = GetIPhoneScaledLocalScale(searchInputRoot, 1.04f);
            cursorX += searchInputWidth + gap;
        }

        if (cardSetRoot != null)
        {
            SetChildBox(cardSetRoot, cursorX + (cardSetWidth * 0.5f), 0f, cardSetWidth, elementHeight, new Vector2(0.5f, 0.5f));
            cardSetRoot.localScale = GetIPhoneScaledLocalScale(cardSetRoot, 0.98f);
            cursorX += cardSetWidth + gap;
        }

        if (searchButtonRoot != null)
        {
            SetChildBox(searchButtonRoot, cursorX + (searchButtonWidth * 0.5f), 0f, searchButtonWidth, elementHeight, new Vector2(0.5f, 0.5f));
            searchButtonRoot.localScale = GetIPhoneScaledLocalScale(searchButtonRoot, 0.82f);
        }
    }

    void LayoutIPhoneActionButtons(
        RectTransform buttonsRoot,
        RectTransform saveButtonRoot,
        RectTransform clearButtonRoot,
        RectTransform quitButtonRoot,
        RectTransform testDrawButtonRoot,
        RectTransform setKeyCardButtonRoot,
        float buttonsWidth,
        float topRowHeight)
    {
        if (buttonsRoot == null)
        {
            return;
        }

        if (clearButtonRoot != null)
        {
            clearButtonRoot.gameObject.SetActive(false);
        }

        if (testDrawButtonRoot != null)
        {
            testDrawButtonRoot.gameObject.SetActive(false);
        }

        if (setKeyCardButtonRoot != null)
        {
            setKeyCardButtonRoot.gameObject.SetActive(false);
        }

        List<RectTransform> visibleButtons = new List<RectTransform>();

        if (quitButtonRoot != null)
        {
            visibleButtons.Add(quitButtonRoot);
        }

        if (saveButtonRoot != null)
        {
            visibleButtons.Add(saveButtonRoot);
        }

        float gap = 12f;
        float horizontalPadding = 8f;
        float buttonWidth = visibleButtons.Count > 0
            ? (buttonsWidth - (horizontalPadding * 2f) - (gap * (visibleButtons.Count - 1))) / visibleButtons.Count
            : 0f;
        float buttonHeight = Mathf.Clamp(topRowHeight - 14f, 42f, 54f);
        float cursorX = horizontalPadding;

        for (int i = 0; i < visibleButtons.Count; i++)
        {
            SetChildBox(visibleButtons[i], cursorX + (buttonWidth * 0.5f), 0f, buttonWidth, buttonHeight, new Vector2(0.5f, 0.5f));
            visibleButtons[i].localScale = GetIPhoneScaledLocalScale(visibleButtons[i], 0.82f);
            cursorX += buttonWidth + gap;
        }
    }

    void LayoutIPhoneCollectionPager(
        RectTransform upButtonRoot,
        RectTransform downButtonRoot,
        RectTransform pageTextRoot,
        RectTransform footerRoot,
        float footerHeight)
    {
        if (footerRoot == null)
        {
            return;
        }

        float buttonWidth = 62f;
        float textWidth = 76f;
        float gap = 10f;
        float centerX = footerRoot.rect.width * 0.5f;

        if (downButtonRoot != null)
        {
            downButtonRoot.SetParent(footerRoot, true);
            SetChildBox(downButtonRoot, centerX - (textWidth * 0.5f) - gap - (buttonWidth * 0.5f), 0f, buttonWidth, footerHeight - 6f, new Vector2(0.5f, 0.5f));
        }

        if (pageTextRoot != null)
        {
            pageTextRoot.SetParent(footerRoot, true);
            SetChildBox(pageTextRoot, centerX, 0f, textWidth, footerHeight - 8f, new Vector2(0.5f, 0.5f));
            pageTextRoot.localScale = Vector3.one;
        }

        if (upButtonRoot != null)
        {
            upButtonRoot.SetParent(footerRoot, true);
            SetChildBox(upButtonRoot, centerX + (textWidth * 0.5f) + gap + (buttonWidth * 0.5f), 0f, buttonWidth, footerHeight - 6f, new Vector2(0.5f, 0.5f));
        }
    }

    void ApplyIPhoneGridSizing(
        GridLayoutGroup deckGridLayout,
        GridLayoutGroup poolGridLayout,
        float deckColumnWidth,
        float collectionColumnWidth)
    {
        if (deckGridLayout != null)
        {
            float availableDeckWidth = Mathf.Max(220f, deckColumnWidth - 26f);
            float cellWidth = Mathf.Floor((availableDeckWidth - (8f * 3f)) / 4f);
            float cellHeight = Mathf.Round(cellWidth * 1.4f);
            deckGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            deckGridLayout.constraintCount = 4;
            deckGridLayout.spacing = new Vector2(8f, 10f);
            deckGridLayout.padding = new RectOffset(8, 8, 8, 8);
            deckGridLayout.cellSize = new Vector2(cellWidth, cellHeight);
        }

        if (poolGridLayout != null)
        {
            float availableCollectionWidth = Mathf.Max(160f, collectionColumnWidth - 18f);
            float cellWidth = Mathf.Floor((availableCollectionWidth - (8f * 2f)) / 3f);
            float cellHeight = Mathf.Round(cellWidth * 1.4f);
            poolGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            poolGridLayout.constraintCount = 3;
            poolGridLayout.spacing = new Vector2(8f, 10f);
            poolGridLayout.padding = new RectOffset(6, 6, 8, 8);
            poolGridLayout.cellSize = new Vector2(cellWidth, cellHeight);
        }
    }

    void ApplyIPhoneDetailCardLayout(RectTransform detailCardRoot, RectTransform createDeckRect)
    {
        if (detailCardRoot == null || createDeckRect == null)
        {
            return;
        }

        EnsureIPhonePreviewUi();
        _iPhonePreviewBackdropRect.gameObject.SetActive(true);
        _iPhonePreviewBackdropRect.SetAsLastSibling();

        float width = createDeckRect.rect.width > 0f ? createDeckRect.rect.width : Screen.width;
        float height = createDeckRect.rect.height > 0f ? createDeckRect.rect.height : Screen.height;
        float maxWidth = Mathf.Clamp(width * 0.48f, 260f, 376f);
        float maxHeight = Mathf.Clamp(height * 0.9f, 280f, 520f);
        Vector2 originalSize = _iPhoneLayoutStates.ContainsKey(detailCardRoot)
            ? _iPhoneLayoutStates[detailCardRoot].SizeDelta
            : detailCardRoot.sizeDelta;
        float scaleX = originalSize.x > 0f ? maxWidth / originalSize.x : 1f;
        float scaleY = originalSize.y > 0f ? maxHeight / originalSize.y : 1f;
        float uniformScale = Mathf.Clamp(Mathf.Min(scaleX, scaleY), 0.58f, 1f);

        detailCardRoot.anchorMin = new Vector2(0.5f, 0.5f);
        detailCardRoot.anchorMax = new Vector2(0.5f, 0.5f);
        detailCardRoot.pivot = new Vector2(0.5f, 0.5f);
        detailCardRoot.anchoredPosition = new Vector2(0f, 12f);
        detailCardRoot.localScale = new Vector3(uniformScale, uniformScale, 1f);
        detailCardRoot.gameObject.SetActive(true);
        detailCardRoot.SetAsLastSibling();

        if (_iPhonePreviewActionBarRect != null)
        {
            _iPhonePreviewActionBarRect.gameObject.SetActive(true);
            _iPhonePreviewActionBarRect.anchorMin = new Vector2(0.5f, 0f);
            _iPhonePreviewActionBarRect.anchorMax = new Vector2(0.5f, 0f);
            _iPhonePreviewActionBarRect.pivot = new Vector2(0.5f, 0f);
            _iPhonePreviewActionBarRect.sizeDelta = new Vector2(Mathf.Min(maxWidth + 16f, width - 24f), 58f);
            _iPhonePreviewActionBarRect.anchoredPosition = new Vector2(0f, 10f);
            _iPhonePreviewActionBarRect.SetAsLastSibling();
            LayoutIPhonePreviewButtons();
        }
    }

    void EnsureIPhonePreviewUi()
    {
        if (CreateDeckObject == null)
        {
            return;
        }

        if (_iPhonePreviewBackdropRect == null)
        {
            GameObject backdropObject = new GameObject("IPhonePreviewBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdropObject.transform.SetParent(CreateDeckObject.transform, false);
            _iPhonePreviewBackdropRect = backdropObject.GetComponent<RectTransform>();
            _iPhonePreviewBackdropRect.anchorMin = Vector2.zero;
            _iPhonePreviewBackdropRect.anchorMax = Vector2.one;
            _iPhonePreviewBackdropRect.offsetMin = Vector2.zero;
            _iPhonePreviewBackdropRect.offsetMax = Vector2.zero;
            Image backdropImage = backdropObject.GetComponent<Image>();
            backdropImage.color = new Color32(4, 8, 14, 224);
            _iPhonePreviewBackdropButton = backdropObject.GetComponent<Button>();
            _iPhonePreviewBackdropButton.targetGraphic = backdropImage;
            _iPhonePreviewBackdropButton.onClick.AddListener(() => HideIPhonePreview(force: true));
            backdropObject.SetActive(false);
        }

        if (_iPhonePreviewActionBarRect == null)
        {
            GameObject actionBarObject = new GameObject("IPhonePreviewActionBar", typeof(RectTransform), typeof(Image));
            actionBarObject.transform.SetParent(CreateDeckObject.transform, false);
            _iPhonePreviewActionBarRect = actionBarObject.GetComponent<RectTransform>();
            Image actionBarImage = actionBarObject.GetComponent<Image>();
            actionBarImage.color = new Color32(10, 28, 38, 240);

            _iPhonePreviewAddButton = CreateIPhonePreviewButton(
                _iPhonePreviewActionBarRect,
                "Add",
                LocalizeUtility.GetLocalizedString("Add", "追加"),
                out _iPhonePreviewAddButtonText);
            _iPhonePreviewRemoveButton = CreateIPhonePreviewButton(
                _iPhonePreviewActionBarRect,
                "Remove",
                LocalizeUtility.GetLocalizedString("Remove", "除去"),
                out _iPhonePreviewRemoveButtonText);
            _iPhonePreviewCloseButton = CreateIPhonePreviewButton(
                _iPhonePreviewActionBarRect,
                "Close",
                LocalizeUtility.GetLocalizedString("Close", "閉じる"),
                out _iPhonePreviewCloseButtonText);

            _iPhonePreviewAddButton.onClick.AddListener(OnClickIPhonePreviewAdd);
            _iPhonePreviewRemoveButton.onClick.AddListener(OnClickIPhonePreviewRemove);
            _iPhonePreviewCloseButton.onClick.AddListener(() => HideIPhonePreview(force: true));
            actionBarObject.SetActive(false);
        }

        RefreshIPhonePreviewButtonLabels();
    }

    Button CreateIPhonePreviewButton(RectTransform parent, string name, string label, out Text labelText)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color32(28, 100, 130, 255);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        labelText = labelObject.GetComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        labelText.fontSize = 18;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.text = label;

        return button;
    }

    void RefreshIPhonePreviewButtonLabels()
    {
        if (_iPhonePreviewAddButtonText != null)
        {
            _iPhonePreviewAddButtonText.text = LocalizeUtility.GetLocalizedString("Add", "追加");
        }

        if (_iPhonePreviewRemoveButtonText != null)
        {
            _iPhonePreviewRemoveButtonText.text = LocalizeUtility.GetLocalizedString("Remove", "除去");
        }

        if (_iPhonePreviewCloseButtonText != null)
        {
            _iPhonePreviewCloseButtonText.text = LocalizeUtility.GetLocalizedString("Close", "閉じる");
        }
    }

    void LayoutIPhonePreviewButtons()
    {
        if (_iPhonePreviewActionBarRect == null)
        {
            return;
        }

        List<Button> visibleButtons = new List<Button>();

        if (_iPhonePreviewAddButton != null && _iPhonePreviewAddButton.gameObject.activeSelf)
        {
            visibleButtons.Add(_iPhonePreviewAddButton);
        }

        if (_iPhonePreviewRemoveButton != null && _iPhonePreviewRemoveButton.gameObject.activeSelf)
        {
            visibleButtons.Add(_iPhonePreviewRemoveButton);
        }

        if (_iPhonePreviewCloseButton != null && _iPhonePreviewCloseButton.gameObject.activeSelf)
        {
            visibleButtons.Add(_iPhonePreviewCloseButton);
        }

        float totalWidth = _iPhonePreviewActionBarRect.rect.width > 0f
            ? _iPhonePreviewActionBarRect.rect.width
            : _iPhonePreviewActionBarRect.sizeDelta.x;
        float gap = 10f;
        float padding = 10f;
        float buttonWidth = visibleButtons.Count > 0
            ? (totalWidth - (padding * 2f) - (gap * (visibleButtons.Count - 1))) / visibleButtons.Count
            : 0f;
        float cursorX = padding;

        for (int i = 0; i < visibleButtons.Count; i++)
        {
            RectTransform buttonRect = visibleButtons[i].transform as RectTransform;
            SetChildBox(buttonRect, cursorX + (buttonWidth * 0.5f), 0f, buttonWidth, 42f, new Vector2(0.5f, 0.5f));
            cursorX += buttonWidth + gap;
        }
    }

    void OpenIPhonePreview(CardPrefab_CreateDeck sourceCard, bool openedFromDeck)
    {
        if (sourceCard == null || sourceCard.cEntity_Base == null || DetailCard == null || isDragging)
        {
            return;
        }

        EnsureIPhonePreviewUi();
        _iPhonePreviewSourceCard = sourceCard;
        _iPhonePreviewOpenedFromDeck = openedFromDeck;
        RefreshIPhonePreviewActions();
        DetailCard.SetUpDetailCard(sourceCard.cEntity_Base);

        RectTransform detailCardRoot = DetailCard.transform as RectTransform;
        RectTransform createDeckRect = CreateDeckObject != null
            ? CreateDeckObject.GetComponent<RectTransform>()
            : null;
        ApplyIPhoneDetailCardLayout(detailCardRoot, createDeckRect);
    }

    void RefreshIPhonePreviewActions()
    {
        EnsureIPhonePreviewUi();
        RefreshIPhonePreviewButtonLabels();

        bool hasSource = _iPhonePreviewSourceCard != null && _iPhonePreviewSourceCard.cEntity_Base != null;
        bool canAdd = hasSource &&
            !_iPhonePreviewSourceCard.IsLocked &&
            EdittingDeckData != null &&
            DeckBuildingRule.CanAddCard(_iPhonePreviewSourceCard.cEntity_Base, EdittingDeckData);
        bool canRemove = hasSource &&
            _iPhonePreviewOpenedFromDeck &&
            EdittingDeckData != null &&
            EdittingDeckData.AllDeckCards().Any(cardData => cardData.CardID == _iPhonePreviewSourceCard.cEntity_Base.CardID);

        if (_iPhonePreviewBackdropRect != null)
        {
            _iPhonePreviewBackdropRect.gameObject.SetActive(hasSource);
        }

        if (_iPhonePreviewActionBarRect != null)
        {
            _iPhonePreviewActionBarRect.gameObject.SetActive(hasSource);
        }

        if (_iPhonePreviewAddButton != null)
        {
            _iPhonePreviewAddButton.gameObject.SetActive(hasSource);
            _iPhonePreviewAddButton.interactable = canAdd;
        }

        if (_iPhonePreviewRemoveButton != null)
        {
            _iPhonePreviewRemoveButton.gameObject.SetActive(_iPhonePreviewOpenedFromDeck);
            _iPhonePreviewRemoveButton.interactable = canRemove;
        }

        if (_iPhonePreviewCloseButton != null)
        {
            _iPhonePreviewCloseButton.gameObject.SetActive(hasSource);
        }

        LayoutIPhonePreviewButtons();
    }

    void HideIPhonePreview(bool force = false)
    {
        _iPhonePreviewSourceCard = null;
        _iPhonePreviewOpenedFromDeck = false;

        if (_iPhonePreviewBackdropRect != null)
        {
            _iPhonePreviewBackdropRect.gameObject.SetActive(false);
        }

        if (_iPhonePreviewActionBarRect != null)
        {
            _iPhonePreviewActionBarRect.gameObject.SetActive(false);
        }

        if (DetailCard != null && !ShouldPreserveAuthoredDetailCardState())
        {
            DetailCard.OffDetailCard();

            if (UseIPhoneSafeAreaLayout)
            {
                DetailCard.gameObject.SetActive(force ? false : DetailCard.gameObject.activeSelf && !UseIPhoneSafeAreaLayout);
            }
        }
    }

    void OnClickIPhonePreviewAdd()
    {
        if (_iPhonePreviewSourceCard == null)
        {
            return;
        }

        CardPrefab_CreateDeck sourceCard = _iPhonePreviewSourceCard;
        HideIPhonePreview(force: true);
        StartCoroutine(AddDeckCardCoroutine_OnClick(sourceCard));
    }

    void OnClickIPhonePreviewRemove()
    {
        if (_iPhonePreviewSourceCard == null)
        {
            return;
        }

        CardPrefab_CreateDeck sourceCard = _iPhonePreviewSourceCard;
        HideIPhonePreview(force: true);
        StartCoroutine(RemoveDeckCardCoroutine_OnClick(sourceCard));
    }

    void SetPageUiVisible(bool visible)
    {
        if (UpDisplayPageIndexButton != null)
        {
            UpDisplayPageIndexButton.gameObject.SetActive(visible);
        }

        if (DownDisplayPageIndexButton != null)
        {
            DownDisplayPageIndexButton.gameObject.SetActive(visible);
        }

        if (ShowDisplayPageIndexText != null)
        {
            ShowDisplayPageIndexText.gameObject.SetActive(visible);
        }
    }

    void CheckButtonEnabled()
    {
        if (UseIPhoneDeckScroll)
        {
            SetPageUiVisible(false);
            return;
        }

        UpDisplayPageIndexButton.interactable = DisplayPageIndex < _maxDisplayPageIndex;
        UpDisplayPageIndexButton.transform.GetChild(0).gameObject.SetActive(DisplayPageIndex < _maxDisplayPageIndex);
        DownDisplayPageIndexButton.interactable = DisplayPageIndex > 0;
        DownDisplayPageIndexButton.transform.GetChild(0).gameObject.SetActive(DisplayPageIndex > 0);

        ShowDisplayPageIndexText.text = $"{DisplayPageIndex + 1}/{_maxDisplayPageIndex + 1}";

        CardPoolScroll.verticalNormalizedPosition = 1;
    }

    public void UpDisplayPageIndex()
    {
        if (UseIPhoneDeckScroll)
        {
            return;
        }

        ContinuousController.instance.PlaySE(SwitchPageSE);

        DisplayPageIndex++;

        if (DisplayPageIndex > _maxDisplayPageIndex)
        {
            DisplayPageIndex = _maxDisplayPageIndex;
        }

        SetCardData();
        CheckButtonEnabled();
        ReflectDeckData();
    }

    public void DownDisplayPageIndex()
    {
        if (UseIPhoneDeckScroll)
        {
            return;
        }

        ContinuousController.instance.PlaySE(SwitchPageSE);

        DisplayPageIndex--;

        if (DisplayPageIndex < 0)
        {
            DisplayPageIndex = 0;
        }

        SetCardData();
        CheckButtonEnabled();
        ReflectDeckData();
    }

    List<List<CEntity_Base>> SprittedCardLists = new List<List<CEntity_Base>>();

    //List<CEntity_Base> ActiveCardList = new List<CEntity_Base>();

    IEnumerator SetSprittedCardLists()
    {
        _filteredPoolCards.Clear();

        foreach (CEntity_Base cEntity_Base in ContinuousController.instance.CardList)
        {
            if (MatchCondition(cEntity_Base))
            {
                _filteredPoolCards.Add(cEntity_Base);
            }
        }

        if (UseIPhoneDeckScroll)
        {
            EnsureCardPoolPrefabCapacityForIPhone(_filteredPoolCards.Count);
            yield return null;
            yield break;
        }

        SprittedCardLists = new List<List<CEntity_Base>>();

        for (int i = 0; i < _maxDisplayPageIndex + 1; i++)
        {
            SprittedCardLists.Add(new List<CEntity_Base>());
        }

        for (int i = 0; i < _filteredPoolCards.Count; i++)
        {
            CEntity_Base cEntity_Base = _filteredPoolCards[i];

            int Quotient = i / _cardPoolPrefabs.Count;

            if (0 <= Quotient && Quotient < SprittedCardLists.Count)
            {
                SprittedCardLists[Quotient].Add(cEntity_Base);
            }
        }

        yield return null;
    }

    void SetCardData()
    {
        if (UseIPhoneDeckScroll)
        {
            EnsureCardPoolPrefabCapacityForIPhone(_filteredPoolCards.Count);

            for (int i = 0; i < _cardPoolPrefabs.Count; i++)
            {
                CardPrefab_CreateDeck poolPrefab = _cardPoolPrefabs[i];
                bool isVisibleCard = i < _filteredPoolCards.Count;

                if (isVisibleCard)
                {
                    ApplyPoolCardData(poolPrefab, _filteredPoolCards[i]);
                }
                else
                {
                    poolPrefab.isActive = false;
                    poolPrefab.HideCardImage();
                }
            }

            RefreshVisiblePoolCards();
            return;
        }

        for (int i = 0; i < _cardPoolPrefabs.Count; i++)
        {
            if (i < SprittedCardLists[DisplayPageIndex].Count)
            {
                ApplyPoolCardData(_cardPoolPrefabs[i], SprittedCardLists[DisplayPageIndex][i]);
            }

            else
            {
                _cardPoolPrefabs[i].isActive = false;
                _cardPoolPrefabs[i].HideCardImage();
            }
        }
    }

    void ApplyPoolCardData(CardPrefab_CreateDeck poolPrefab, CEntity_Base cEntity_Base)
    {
        if (poolPrefab == null)
        {
            return;
        }

        poolPrefab.isActive = true;
        poolPrefab.SetUpCardPrefab_CreateDeck(cEntity_Base);

        bool unlocked = ProgressionManager.Instance == null || ProgressionManager.Instance.IsCardUnlocked(cEntity_Base.CardID);
        poolPrefab.SetLocked(!unlocked);
    }

    #region Card details display
    public void OffDetailCard()
    {
        HideIPhonePreview(force: true);

        if (DetailCard == null || ShouldPreserveAuthoredDetailCardState())
        {
            return;
        }

        DetailCard.OffDetailCard();
    }

    public void OnDetailCard(CEntity_Base cEntity_Base)
    {
        if (isDragging)
        {
            return;
        }

        if (UseIPhoneSafeAreaLayout)
        {
            return;
        }

        DetailCard.SetUpDetailCard(cEntity_Base);
    }
    #endregion

    int _frameCount = 0;
    int _updateFrame = 3;

    private void Update()
    {
        #region Updates only once every few frames
        _frameCount++;

        if (_frameCount < _updateFrame)
        {
            return;
        }

        else
        {
            _frameCount = 0;
        }
        #endregion

        if (!UseIPhoneDeckScroll)
        {
            //TODO: need to find a better way, this shouldn't happen every 3rd frame, should only happen on changes. MikeB
            ShowOnlyVisibleObjects();
        }

        if (EdittingDeckData != null)
        {
            TrialDrawButton.gameObject.SetActive(EdittingDeckData.DeckCards().Count >= 1);
            ClearDeckButton.gameObject.SetActive(EdittingDeckData.AllDeckCards().Count >= 1);
        }
    }

    #region Show only visible cards
    public void ShowOnlyVisibleObjects()
    {
        if (DoneSetUp)
        {
            foreach (CardPrefab_CreateDeck cardPrefab_CreateDeck in _cardPoolPrefabs)
            {
                if (!cardPrefab_CreateDeck.isActive)
                {
                    cardPrefab_CreateDeck.HideCardImage();
                    cardPrefab_CreateDeck.gameObject.SetActive(false);
                }

                else
                {
                    cardPrefab_CreateDeck.gameObject.SetActive(true);

                    if (!((RectTransform)cardPrefab_CreateDeck.transform).IsVisibleFrom(Opening.instance.MainCamera))
                    {
                        cardPrefab_CreateDeck.HideCardImage();
                    }

                    else
                    {
                        cardPrefab_CreateDeck.CardImage.transform.parent.gameObject.SetActive(true);

                        if (cardPrefab_CreateDeck.cEntity_Base != null)
                        {
                            //if (cardPrefab_CreateDeck.cEntity_Base.CardSprite != null)
                            {
                                cardPrefab_CreateDeck.ShowCardImage();
                            }
                        }
                    }
                }
            }
        }
    }
    #endregion

    public bool isEditting { get; set; } = false;

    #region Close deck editing screen
    public void OnClickCompleteEditButton()
    {
        ContinuousController.instance.PlaySE(Opening.instance.DecisionSE);
        ContinuousController.instance.StartCoroutine(CloseCreateDeckCoroutine(false));
    }

    public void OnClickCancelEditButton()
    {
        ContinuousController.instance.PlaySE(Opening.instance.CancelSE);
        ContinuousController.instance.StartCoroutine(CloseCreateDeckCoroutine(true));
    }

    public IEnumerator CloseCreateDeckCoroutine(bool Canceled)
    {
        isEditting = false;
        HideIPhonePreview(force: true);
        yield return ContinuousController.instance.StartCoroutine(LoadingObjec.StartLoading("Now Loading"));

        Opening.instance.OffYesNoObjects();
        Opening.instance.home.OffHome();
        Opening.instance.OffModeButtons();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();

        for (int i = 0; i < Opening.instance.deck.selectDeck.deckInfoPrefabParentScroll.content.childCount; i++)
        {
            CreateNewDeckButton createNewDeckButton = Opening.instance.deck.selectDeck.deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<CreateNewDeckButton>();

            if (createNewDeckButton != null)
            {
                createNewDeckButton.CreateNewDeckWayObject.Close_(false);
                break;
            }
        }

        if (Canceled)
        {
            EdittingDeckData.DeckCardIDs = DeckData.GetDeckCardCodes(DeckData.SortedDeckCardsList(_oldDeckCards));
            EdittingDeckData.DigitamaDeckCardIDs = DeckData.GetDeckCardCodes(DeckData.SortedDeckCardsList(_oldDigitamaDeckCards));
            EdittingDeckData.DeckName = _oldDeckName;
            EdittingDeckData.KeyCardId = _oldKeyCardId;
        }

        if (!EdittingDeckData.AllDeckCards().Contains(EdittingDeckData.KeyCard))
        {
            EdittingDeckData.KeyCardId = -1;
        }

        #region not save if deck is empty or canceled when importing from clipboard
        if (EdittingDeckData != null && (EdittingDeckData.DeckCards().Count == 0 && EdittingDeckData.DigitamaDeckCards().Count == 0 || (Canceled && _isFromClipboard)))
        {
            ContinuousController.instance.DeckDatas.Remove(EdittingDeckData);
        }
        #endregion

        ContinuousController.instance.SaveDeckDatas();
        ContinuousController.instance.SaveDeckData(EdittingDeckData);

        for (int i = 0; i < DeckScroll.content.childCount; i++)
        {
            Destroy(DeckScroll.content.GetChild(i).gameObject);
        }

        if (!Canceled)
        {
            EdittingDeckData.DeckCardIDs = DeckData.GetDeckCardCodes(DeckData.SortedDeckCardsList(EdittingDeckData.DeckCards()));
            EdittingDeckData.DigitamaDeckCardIDs = DeckData.GetDeckCardCodes(DeckData.SortedDeckCardsList(EdittingDeckData.DigitamaDeckCards()));
        }

        foreach (CardPrefab_CreateDeck cardPrefab_CreateDeck in _cardPoolPrefabs)
        {
            if (cardPrefab_CreateDeck._OnEnterCoroutine != null)
            {
                cardPrefab_CreateDeck.StopCoroutine(cardPrefab_CreateDeck._OnEnterCoroutine);
                cardPrefab_CreateDeck._OnEnterCoroutine = null;
            }

            cardPrefab_CreateDeck.StopAllCoroutines();
            cardPrefab_CreateDeck.HideCardImage();

            cardPrefab_CreateDeck.gameObject.SetActive(false);
        }

        if (Application.isMobilePlatform)
        {
            ContinuousController.instance.ReleaseLoadedCardImageReferences();
            yield return Resources.UnloadUnusedAssets();
        }

        yield return new WaitUntil(() => DeckScroll.content.childCount == 0);

        yield return new WaitForSeconds(0.1f);

        CreateDeckObject.SetActive(false);

        #region Set up previous screen
        if (_isFromSelectDeck)
        {
            Opening.instance.deck.selectDeck.ResetDeckInfoPanel();

            yield return ContinuousController.instance.StartCoroutine(Opening.instance.deck.selectDeck.SetDeckList(false));

            for (int i = 0; i < Opening.instance.deck.selectDeck.deckInfoPrefabParentScroll.content.childCount; i++)
            {
                if (Opening.instance.deck.selectDeck.deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<DeckInfoPrefab>() != null)
                {
                    if (Opening.instance.deck.selectDeck.deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<DeckInfoPrefab>().thisDeckData == EdittingDeckData)
                    {
                        if (Opening.instance.deck.selectDeck.deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<DeckInfoPrefab>().thisDeckData != null)
                        {
                            if (Opening.instance.deck.selectDeck.deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<DeckInfoPrefab>().thisDeckData.DeckCards().Count > 0)
                            {
                                Opening.instance.deck.selectDeck.deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<DeckInfoPrefab>().OnClick_DeckInfoPrefab(false);
                            }
                        }
                    }
                }
            }

            if (EdittingDeckData != null)
            {
                ContinuousController.instance.BattleDeckData = EdittingDeckData;
            }
        }

        else
        {
            Opening.instance.battle.selectBattleDeck.ResetDeckInfoPanel();

            yield return ContinuousController.instance.StartCoroutine(Opening.instance.battle.selectBattleDeck.SetDeckList(false));

            for (int i = 0; i < Opening.instance.battle.selectBattleDeck.deckInfoPrefabParentScroll.content.childCount; i++)
            {
                if (Opening.instance.battle.selectBattleDeck.deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<DeckInfoPrefab>() != null)
                {
                    if (Opening.instance.battle.selectBattleDeck.deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<DeckInfoPrefab>().thisDeckData == EdittingDeckData)
                    {
                        Opening.instance.battle.selectBattleDeck.deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<DeckInfoPrefab>().OnClick_DeckInfoPrefab(false);
                    }
                }
            }
        }
        #endregion

        EndEditAction?.Invoke();

        EndEditAction = null;

        yield return ContinuousController.instance.StartCoroutine(LoadingObjec.EndLoading());
    }
    #endregion

    #region Open deck editing screen
    public void SetUpCreateDeck(DeckData deckData, bool isFromSelectDeck, bool isFromClipboard = false)
    {
        if (deckData == null)
        {
            return;
        }

        ProgressionManager.Instance.LoadOrCreate();

        EnsureCardPoolScrollConfiguredForIPhone();
        SetPageUiVisible(!UseIPhoneDeckScroll);

        _isFromClipboard = isFromClipboard;

        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();

        _oldDeckName = deckData.DeckName;

        _oldDeckCards = new List<CEntity_Base>();

        foreach (CEntity_Base cEntity_Base in deckData.DeckCards())
        {
            _oldDeckCards.Add(cEntity_Base);
        }

        _oldDigitamaDeckCards = new List<CEntity_Base>();

        foreach (CEntity_Base cEntity_Base in deckData.DigitamaDeckCards())
        {
            _oldDigitamaDeckCards.Add(cEntity_Base);
        }

        _oldKeyCardId = deckData.KeyCardId;

        _checkCoverCoroutine = null;
        this._isFromSelectDeck = isFromSelectDeck;
        DisplayPageIndex = 0;
        _filteredPoolCards.Clear();
        SetCardData();
        CheckButtonEnabled();
        isSearchingObject.Off();
        ContinuousController.instance.StartCoroutine(SetUpCreateDeckCoroutine(deckData));
    }

    public IEnumerator SetUpCreateDeckCoroutine(DeckData deckData)
    {
        yield return ContinuousController.instance.StartCoroutine(LoadingObjec.StartLoading("Now Loading"));

        yield return new WaitForSeconds(0.1f);

        for (int i = 0; i < draggable_CardParent.childCount; i++)
        {
            Destroy(draggable_CardParent.GetChild(i).gameObject);
        }

        DraggingCover.SetActive(false);

        CardPoolScroll.verticalNormalizedPosition = 1;
        DeckScroll.verticalNormalizedPosition = 1;

        deckData.DeckCardIDs = DeckData.GetDeckCardCodes(DeckData.SortedDeckCardsList(deckData.DeckCards()));
        deckData.DigitamaDeckCardIDs = DeckData.GetDeckCardCodes(DeckData.SortedDeckCardsList(deckData.DigitamaDeckCards()));

        EdittingDeckData = deckData;

        ContinuousController.instance.SaveDeckDatas();
        //ContinuousController.instance.SaveDeckData(EdittingDeckData);

        OffDetailCard();

        for (int i = 0; i < DeckScroll.content.childCount; i++)
        {
            Destroy(DeckScroll.content.GetChild(i).gameObject);
        }

        yield return new WaitUntil(() => DeckScroll.content.childCount == 0);

        for (int i = 0; i < DeckScroll.viewport.childCount; i++)
        {
            if (i != 0)
            {
                Destroy(DeckScroll.viewport.GetChild(i).gameObject);
            }
        }

        yield return new WaitUntil(() => DeckScroll.viewport.childCount == 1);

        int scopedCardCount = ContinuousController.instance.CardList
            .Where(cEntity_Base => DeckBuilderSetScope.IsAllowedCard(cEntity_Base))
            .Count();
        _maxDisplayPageIndex = UseIPhoneDeckScroll
            ? 0
            : Mathf.Max(0, (scopedCardCount - 1) / _cardPoolPrefabs.Count);

        if (deckData != null)
        {
            List<CEntity_Base> DeckCards = new List<CEntity_Base>();

            foreach (CEntity_Base cEntity_Base in deckData.AllDeckCards())
            {
                DeckCards.Add(cEntity_Base);
            }

            foreach (CEntity_Base cEntity_Base in DeckCards)
            {
                CreateDeckCard(cEntity_Base);
            }
        }

        CreateDeckObject.SetActive(true);
        ApplyIPhoneCreateDeckLayout(force: true);
        Opening.instance.home.OffHome();
        Opening.instance.OffModeButtons();

        foreach (CardPrefab_CreateDeck _CardPrefab_CreateDeck in _cardPoolPrefabs)
        {
            _CardPrefab_CreateDeck.isActive = true;

            if (_CardPrefab_CreateDeck._OnEnterCoroutine != null)
            {
                _CardPrefab_CreateDeck.StopCoroutine(_CardPrefab_CreateDeck._OnEnterCoroutine);
                _CardPrefab_CreateDeck._OnEnterCoroutine = null;
            }
        }

        IEnumerable<CEntity_Base> scopedCards = ContinuousController.instance.CardList
            .Where(cEntity_Base => DeckBuilderSetScope.IsAllowedCard(cEntity_Base));
        filterCardList.Init(() => ContinuousController.instance.StartCoroutine(ShowPoolCard_MatchCondition()), scopedCards);

        DeckNameInputField.onEndEdit.RemoveAllListeners();
        DeckNameInputField.text = EdittingDeckData.DeckName;
        DeckNameInputField.onEndEdit.AddListener(OnEndEdit);

        yield return ContinuousController.instance.StartCoroutine(SetSprittedCardLists());

        SetCardData();

        CheckButtonEnabled();

        ReflectDeckData();

        cardDistribution.SetCardDistribution(EdittingDeckData);

        yield return new WaitForSeconds(0.1f);

        for (int i = 0; i < DeckScroll.content.childCount; i++)
        {
            DeckScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>().ShowCardImage();
        }

        yield return StartCoroutine(LoadingObjec.EndLoading());
        isEditting = true;
    }

    void EnsureIPhoneBackdrop()
    {
        if (!UseIPhoneSafeAreaLayout || _iPhoneBackdrop != null || CreateDeckObject == null)
        {
            return;
        }

        Transform existingBackdrop = CreateDeckObject.transform.Find("IPhoneDeckBackdrop");
        GameObject backdropObject;

        if (existingBackdrop != null)
        {
            backdropObject = existingBackdrop.gameObject;
        }

        else
        {
            backdropObject = new GameObject("IPhoneDeckBackdrop", typeof(RectTransform), typeof(Image));
            backdropObject.transform.SetParent(CreateDeckObject.transform, false);
            backdropObject.transform.SetSiblingIndex(0);
        }

        RectTransform backdropRect = backdropObject.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        _iPhoneBackdrop = backdropObject.GetComponent<Image>();
        _iPhoneBackdrop.color = new Color32(6, 10, 18, 255);
        _iPhoneBackdrop.raycastTarget = false;
        _iPhoneBackdrop.gameObject.SetActive(true);
    }

    public void SetEditorPreviewActive(bool active)
    {
        _editorPreviewActive = active;
        ApplyIPhoneCreateDeckLayout(force: true);
    }

    public void OnEndEdit(string text)
    {
        ContinuousController.instance.RenameDeck(EdittingDeckData, text);

        DeckNameInputField.onEndEdit.RemoveAllListeners();
        DeckNameInputField.text = text;
        DeckNameInputField.onEndEdit.AddListener(OnEndEdit);
    }
    #endregion

    public List<CardPrefab_CreateDeck> CardPoolCardPrefabs_CreateDeck = new List<CardPrefab_CreateDeck>();

    void RefreshVisiblePoolCards()
    {
        if (!UseIPhoneDeckScroll)
        {
            return;
        }

        ShowOnlyVisibleObjects();
    }

    void ConfigurePoolCardPrefab(CardPrefab_CreateDeck poolPrefab)
    {
        if (poolPrefab == null)
        {
            return;
        }

        CardPrefab_CreateDeck configuredPrefab = poolPrefab;

        foreach (ScrollRect scroll in configuredPrefab.scroll)
        {
            scroll.content = CardPoolScroll.content;
            scroll.viewport = CardPoolScroll.viewport;

            // Key: allow drag-to-scroll on iPhone, keep old behavior elsewhere
            scroll.enabled = UseIPhoneDeckScroll;

            if (UseIPhoneDeckScroll)
            {
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
            }
        }

        configuredPrefab.OnClickAction = () =>
        {
            if (UseIPhoneSafeAreaLayout)
            {
                OpenIPhonePreview(configuredPrefab, openedFromDeck: false);
                return;
            }

            if (UseIPhoneTapPreview)
            {
                OnDetailCard(configuredPrefab.cEntity_Base);
                configuredPrefab.SetupAddRemoveButton(EdittingDeckData);
                return;
            }

            StartCoroutine(AddDeckCardCoroutine_OnClick(configuredPrefab));
        };
        configuredPrefab.OnBeginDragAction = (cardPrefab) => { StartCoroutine(OnBeginDrag(cardPrefab)); };
        configuredPrefab.OnEnterAction = (cardPrefab) =>
        {
            if (!UseIPhoneSafeAreaLayout)
            {
                OnDetailCard(configuredPrefab.cEntity_Base);
            }
        };
    }

    void EnsureCardPoolPrefabCapacityForIPhone(int requiredCardCount)
    {
        if (!UseIPhoneDeckScroll || CardPoolScroll == null || cardPrefab_CreateDeck == null)
        {
            return;
        }

        while (_cardPoolPrefabs.Count < requiredCardCount)
        {
            CardPrefab_CreateDeck newPoolPrefab = Instantiate(cardPrefab_CreateDeck, CardPoolScroll.content);
            newPoolPrefab.gameObject.SetActive(false);
            ConfigurePoolCardPrefab(newPoolPrefab);
            _cardPoolPrefabs.Add(newPoolPrefab);
            CardPoolCardPrefabs_CreateDeck.Add(newPoolPrefab);
        }
    }

    #region Initialize the deck editing screen at the start of the game
    public List<CardPrefab_CreateDeck> cardPoolPrefabs_all = new List<CardPrefab_CreateDeck>();
    List<CardPrefab_CreateDeck> _cardPoolPrefabs = new List<CardPrefab_CreateDeck>();
    bool DoneSetUp { get; set; } = false;
    public IEnumerator InitEditDeck()
    {
        EnsureCardPoolScrollConfiguredForIPhone();
        cardDistribution.Init();
        CardPoolCardPrefabs_CreateDeck.Clear();
        _cardPoolPrefabs.Clear();

        for (int i = 0; i < CardPoolScroll.content.childCount; i++)
        {
            CardPrefab_CreateDeck cardPrefab_CreateDeck = CardPoolScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>();

            if (cardPrefab_CreateDeck != null)
            {
                CardPoolCardPrefabs_CreateDeck.Add(cardPrefab_CreateDeck);
            }
        }

        for (int i = 0; i < DeckScroll.content.childCount; i++)
        {
            Destroy(DeckScroll.content.GetChild(i).gameObject);
        }

        foreach (CardPrefab_CreateDeck cardPrefab_CreateDeck in cardPoolPrefabs_all)
        {
            if (cardPrefab_CreateDeck.gameObject.activeSelf)
            {
                cardPrefab_CreateDeck.gameObject.SetActive(false);
            }
        }

        for (int i = 0; i < cardPoolPrefabs_all.Count; i++)
        {
            if (i < ContinuousController.instance.CardList.Length)
            {
                CardPrefab_CreateDeck _cardPrefab_CreateDeck = cardPoolPrefabs_all[i];
                _cardPoolPrefabs.Add(_cardPrefab_CreateDeck);
                ConfigurePoolCardPrefab(_cardPrefab_CreateDeck);
            }
        }

        yield return ContinuousController.instance.StartCoroutine(SetSprittedCardLists());

        yield return new WaitForSeconds(0.1f);

        DoneSetUp = true;
    }
    #endregion

    #region Changed deck content changes to UI
    public void ReflectDeckData()
    {
        if (EdittingDeckData != null)
        {
            //Turn on covers for cards that cannot be included in the deck
            if (_checkCoverCoroutine != null)
            {
                StopCoroutine(_checkCoverCoroutine);
            }

            _checkCoverCoroutine = StartCoroutine(CheckCoverIEnumerator());

            SetDeckCountText();
            RefreshVisiblePoolCards();
        }
    }

    Coroutine _checkCoverCoroutine = null;
    IEnumerator CheckCoverIEnumerator()
    {
        foreach (CardPrefab_CreateDeck cardPrefab_CreateDeck in _cardPoolPrefabs)
        {
            yield return null;
            cardPrefab_CreateDeck.CheckCover(EdittingDeckData);
        }
    }
    #endregion

    #region Show deck number text
    public void SetDeckCountText()
    {
        DeckCountText.text = $"{EdittingDeckData.DeckCards().Count}+{EdittingDeckData.DigitamaDeckCards().Count}/{DeckBuildingRule.MainDeckMax}+{DeckBuildingRule.DigitamaDeckMax}";

        if (EdittingDeckData.IsValidDeckData())
        {
            DeckCountText.color = new Color32(69, 255, 69, 255);
        }

        else
        {
            DeckCountText.color = new Color32(255, 64, 64, 255);
        }
    }
    #endregion

    #region Generate cards for deck
    public CardPrefab_CreateDeck CreateDeckCard(CEntity_Base cEntity_Base)
    {
        CardPrefab_CreateDeck _cardPrefab_CreateDeck = Instantiate(cardPrefab_CreateDeck, DeckScroll.content);

        SetUpDeckCard(_cardPrefab_CreateDeck, cEntity_Base);

        return _cardPrefab_CreateDeck;
    }

    public void SetUpDeckCard(CardPrefab_CreateDeck _cardPrefab_CreateDeck, CEntity_Base cEntity_Base)
    {
        foreach (ScrollRect _scroll in _cardPrefab_CreateDeck.scroll)
        {
            _scroll.content = DeckScroll.content;

            _scroll.viewport = DeckScroll.viewport;
            _scroll.enabled = UseIPhoneDeckScroll;

            if (UseIPhoneDeckScroll)
            {
                _scroll.horizontal = false;
                _scroll.vertical = true;
                _scroll.movementType = ScrollRect.MovementType.Clamped;
            }
        }

        _cardPrefab_CreateDeck.SetUpCardPrefab_CreateDeck(cEntity_Base);
        _cardPrefab_CreateDeck.ShowCardImage();

        _cardPrefab_CreateDeck.OnClickAction = () =>
        {
            if (UseIPhoneSafeAreaLayout)
            {
                OpenIPhonePreview(_cardPrefab_CreateDeck, openedFromDeck: true);
                return;
            }

            if (UseIPhoneTapPreview)
            {
                OnDetailCard(cEntity_Base);
                _cardPrefab_CreateDeck.SetupAddRemoveButton(EdittingDeckData);
                return;
            }

            StartCoroutine(RemoveDeckCardCoroutine_OnClick(_cardPrefab_CreateDeck));
        };

        _cardPrefab_CreateDeck.OnBeginDragAction = (cardPrefab_CreateDeck) => { StartCoroutine(OnBeginDrag(cardPrefab_CreateDeck)); };

        _cardPrefab_CreateDeck.OnEnterAction = (cardPrefab) =>
        {
            if (!UseIPhoneSafeAreaLayout)
            {
                OnDetailCard(cEntity_Base);
                cardPrefab.SetupAddRemoveButton(EdittingDeckData);
            }
        };

        _cardPrefab_CreateDeck.OnExitAction = (cardPrefab) =>
        {
            cardPrefab.OffAddRemoveButton();
        };

        _cardPrefab_CreateDeck.Parent.localScale = UseIPhoneSafeAreaLayout
            ? new Vector3(0.96f, 0.96f, 0.96f)
            : new Vector3(0.76f, 0.76f, 0.76f);

        _cardPrefab_CreateDeck.OnClickAddButtonAction = (cardPrefab) => StartCoroutine(AddDeckCardCoroutine_OnClick(cardPrefab));
        _cardPrefab_CreateDeck.OnClickRemoveButtonAction = (cardPrefab) => StartCoroutine(RemoveDeckCardCoroutine_OnClick(cardPrefab));
    }
    #endregion

    #region Display only cards that meet search/filter conditions
    bool MatchCondition(CEntity_Base cEntity_Base)
    {
        if (cEntity_Base == null)
        {
            return false;
        }

        if (!DeckBuilderSetScope.IsAllowedCard(cEntity_Base))
        {
            return false;
        }

        if (filterCardList == null)
        {
            return true;
        }

        if (!filterCardList.OnlyContainsName()(cEntity_Base))
        {
            return false;
        }

        if (!filterCardList.OnlyContainsColor()(cEntity_Base))
        {
            return false;
        }

        if (!filterCardList.OnlyMatchRarity()(cEntity_Base))
        {
            return false;
        }

        if (!filterCardList.OnlyMatchCardKind()(cEntity_Base))
        {
            return false;
        }

        if (!filterCardList.OnlyMatchCardSet()(cEntity_Base))
        {
            return false;
        }

        if (!filterCardList.OnlyMatchPlayCost()(cEntity_Base))
        {
            return false;
        }

        if (!filterCardList.OnlyMatchLevel()(cEntity_Base))
        {
            return false;
        }

        if (!filterCardList.OnlyMatchSpecialCardKind()(cEntity_Base))
        {
            return false;
        }

        if (!filterCardList.OnlyMatchParallelCondition()(cEntity_Base))
        {
            return false;
        }

        return true;
    }

    public IEnumerator ShowPoolCard_MatchCondition()
    {
        DraggingCover.SetActive(true);

        DisplayPageIndex = 0;

        int matchCount = ContinuousController.instance.CardList
            .Where(cEntity_Base => cEntity_Base != null)
            .Count(cEntity_Base => MatchCondition(cEntity_Base));
        _maxDisplayPageIndex = UseIPhoneDeckScroll
            ? 0
            : Mathf.Max(0, (matchCount - 1) / _cardPoolPrefabs.Count);

        yield return ContinuousController.instance.StartCoroutine(SetSprittedCardLists());

        SetCardData();

        CheckButtonEnabled();

        ReflectDeckData();

        DraggingCover.SetActive(false);

        for (int i = 0; i < CardPoolCardPrefabs_CreateDeck.Count; i++)
        {
            CardPrefab_CreateDeck cardPrefab_CreateDeck = CardPoolCardPrefabs_CreateDeck[i];

            if (cardPrefab_CreateDeck != null)
            {
                cardPrefab_CreateDeck.uIShiny.enabled = false;

                if (cardPrefab_CreateDeck._OnEnterCoroutine != null)
                {
                    cardPrefab_CreateDeck.StopCoroutine(cardPrefab_CreateDeck._OnEnterCoroutine);
                    cardPrefab_CreateDeck._OnEnterCoroutine = null;
                }

                cardPrefab_CreateDeck.StopAllCoroutines();
            }
        }
    }
    #endregion

    #region Manipulating card objects

    [Header("draggable card object")]
    public Draggable_Card draggable_CardPrefab;

    [Header("draggable card object parent")]
    public Transform draggable_CardParent;

    [Header("Deck card dropArea")]
    public DropArea DeckCardsDropArea;

    [Header("card pool dropArea")]
    public DropArea CardPoolDropArea;

    [Header("Cover while dragging")]
    public GameObject DraggingCover;

    [Header("Coordinates of the disappearing deck card")]
    public Transform DisappearDeckCardTransform;

    //Is it draggable?
    public bool CanDrag { get; set; } = true;

    //Whether you are dragging
    public bool isDragging { get; set; } = false;

    #region Generate draggable objects
    public Draggable_Card CreateDraggable_Card()
    {
        Draggable_Card draggable_Card = Instantiate(draggable_CardPrefab, draggable_CardParent);

        draggable_Card.DefaultParent = draggable_CardParent;
        draggable_Card.transform.localScale = new Vector3(1, 1, 1);
        draggable_Card.CardImage.color = new Color(1, 1, 1, 1);

        return draggable_Card;
    }
    #endregion

    #region At the start of drag
    public IEnumerator OnBeginDrag(CardPrefab_CreateDeck _cardPrefab_CreateDeck)
    {
        if (_cardPrefab_CreateDeck.transform.parent == CardPoolScroll.content)
        {
            if (_cardPrefab_CreateDeck.IsLocked)
            {
                yield break;
            }

            if (!DeckBuildingRule.CanAddCard(_cardPrefab_CreateDeck.cEntity_Base, EdittingDeckData))
            {
                yield break;
            }
        }

        if (Input.GetMouseButton(1))
        {
            yield break;
        }

        if (!isDragging && CanDrag)
        {
            Draggable_Card draggable_Card = CreateDraggable_Card();

            SetStartDrag(_cardPrefab_CreateDeck, draggable_Card);

            draggable_Card.OnDropAction = (dropAreas) => { OnEndDrag(dropAreas, _cardPrefab_CreateDeck, draggable_Card); };

            draggable_Card.OnDragAction = (dropAreas) =>
            {
                DeckCardsDropArea.OnPointerExit();
                CardPoolDropArea.OnPointerExit();

                if (_cardPrefab_CreateDeck.transform.parent == DeckScroll.content)
                {
                    if (dropAreas.Count((dropArea) => dropArea == CardPoolDropArea) > 0)
                    {
                        CardPoolDropArea.OnPointerEnter();
                    }
                }

                else if (_cardPrefab_CreateDeck.transform.parent == CardPoolScroll.content)
                {
                    if (dropAreas.Count((dropArea) => dropArea == DeckCardsDropArea) > 0)
                    {
                        DeckCardsDropArea.OnPointerEnter();
                    }
                }
            };

            if (_cardPrefab_CreateDeck.transform.parent == DeckScroll.content)
            {
                DeckCardsDropArea.OffDropPanel();
                CardPoolDropArea.OnDropPanel();

                _cardPrefab_CreateDeck.HideDeckCardTab();
                _cardPrefab_CreateDeck.OffAddRemoveButton();
            }

            else if (_cardPrefab_CreateDeck.transform.parent == CardPoolScroll.content)
            {
                DeckCardsDropArea.OnDropPanel();
                CardPoolDropArea.OffDropPanel();
            }

            DeckScroll.enabled = false;

            for (int i = 0; i < DeckScroll.content.childCount; i++)
            {
                foreach (ScrollRect scroll in DeckScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>().scroll)
                {
                    scroll.enabled = false;
                }
            }

            CardPoolScroll.enabled = false;

            for (int i = 0; i < _cardPoolPrefabs.Count; i++)
            {
                foreach (ScrollRect scroll in _cardPoolPrefabs[i].scroll)
                {
                    scroll.enabled = false;
                }
            }
        }
    }

    public void SetStartDrag(CardPrefab_CreateDeck _cardPrefab_CreateDeck, Draggable_Card draggable_Card)
    {
        isDragging = true;

        CanDrag = false;

        DraggingCover.SetActive(true);

        draggable_Card.SetUpDraggable_Card(_cardPrefab_CreateDeck.cEntity_Base, _cardPrefab_CreateDeck.transform.position);
    }
    #endregion

    #region At the end of dragging
    public void OnEndDrag(List<DropArea> dropAreas, CardPrefab_CreateDeck _cardPrefab_CreateDeck, Draggable_Card draggable_Card)
    {
        DeckCardsDropArea.OffDropPanel();
        CardPoolDropArea.OffDropPanel();

        StartCoroutine(OnEndDragCoroutine(dropAreas, _cardPrefab_CreateDeck, draggable_Card));

        DeckScroll.enabled = true;

        for (int i = 0; i < DeckScroll.content.childCount; i++)
        {
            foreach (ScrollRect scroll in DeckScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>().scroll)
            {
                scroll.enabled = UseIPhoneDeckScroll;

                if (UseIPhoneDeckScroll)
                {
                    scroll.horizontal = false;
                    scroll.vertical = true;
                    scroll.movementType = ScrollRect.MovementType.Clamped;
                }
            }
        }

        CardPoolScroll.enabled = true;

        for (int i = 0; i < _cardPoolPrefabs.Count; i++)
        {
            foreach (ScrollRect scroll in _cardPoolPrefabs[i].scroll)
            {
                scroll.enabled = UseIPhoneDeckScroll;

                if (UseIPhoneDeckScroll)
                {
                    scroll.horizontal = false;
                    scroll.vertical = true;
                    scroll.movementType = ScrollRect.MovementType.Clamped;
                }
            }
        }

        RefreshVisiblePoolCards();
    }

    IEnumerator OnEndDragCoroutine(List<DropArea> dropAreas, CardPrefab_CreateDeck _cardPrefab_CreateDeck, Draggable_Card draggable_Card)
    {
        if (_cardPrefab_CreateDeck.transform.parent == DeckScroll.content)
        {
            if (dropAreas.Count((dropArea) => dropArea == CardPoolDropArea) > 0)
            {
                yield return StartCoroutine(RemoveDeckCardCoroutine(_cardPrefab_CreateDeck, draggable_Card));
            }

            else
            {
                _cardPrefab_CreateDeck.ShowCardImage();
            }
        }

        else if (_cardPrefab_CreateDeck.transform.parent == CardPoolScroll.content)
        {
            if (dropAreas.Count((dropArea) => dropArea == DeckCardsDropArea) > 0)
            {
                yield return StartCoroutine(AddDeckCardCoroutine(_cardPrefab_CreateDeck, draggable_Card));
            }
        }

        Reset_EndDrag(draggable_Card);
    }

    public void Reset_EndDrag(Draggable_Card draggable_Card)
    {
        isDragging = false;
        DestroyImmediate(draggable_Card.gameObject);
        CanDrag = true;
        DraggingCover.SetActive(false);
        RefreshVisiblePoolCards();
    }
    #endregion

    float _addCardAnimationTime = 0.01f;

    #region Animation to add cards to deck when dropped
    IEnumerator AddDeckCardCoroutine(CardPrefab_CreateDeck _cardPrefab_CreateDeck, Draggable_Card draggable_Card)
    {
        if (_cardPrefab_CreateDeck.IsLocked)
        {
            yield break;
        }

        if (!DeckBuildingRule.CanAddCard(_cardPrefab_CreateDeck.cEntity_Base, EdittingDeckData))
        {
            yield break;
        }

        ContinuousController.instance.PlaySE(Opening.instance.DrawSE);

        for (int i = 0; i < CardPoolCardPrefabs_CreateDeck.Count; i++)
        {
            CardPrefab_CreateDeck cardPrefab_CreateDeck = CardPoolCardPrefabs_CreateDeck[i];

            if (cardPrefab_CreateDeck != null)
            {
                cardPrefab_CreateDeck.uIShiny.enabled = false;
                cardPrefab_CreateDeck.OffAddRemoveButton();
            }
        }
        Debug.Log($"CARD TO ADD: {_cardPrefab_CreateDeck.cEntity_Base}");
        yield return StartCoroutine(AddDeckCards(_cardPrefab_CreateDeck.cEntity_Base));

        List<CEntity_Base> DeckCards = new List<CEntity_Base>();

        foreach (CEntity_Base cEntity_Base in EdittingDeckData.AllDeckCards())
        {
            //Debug.Log($"ADD: {cEntity_Base.CardIndex} || {cEntity_Base.CardID}");
            DeckCards.Add(cEntity_Base);
        }
        Debug.Log($"ADDED EDITTING DECK DATA ENTITIES TO DECK CARDS LIST: {DeckCards.Count} || {DeckCards.IndexOf(_cardPrefab_CreateDeck.cEntity_Base)}  || {DeckCards.Count((cEntity_Base) => cEntity_Base == _cardPrefab_CreateDeck.cEntity_Base)}");
        int index = DeckCards.IndexOf(_cardPrefab_CreateDeck.cEntity_Base) + DeckCards.Count((cEntity_Base) => cEntity_Base == _cardPrefab_CreateDeck.cEntity_Base) - 1;
        Debug.Log("GETTING INDEX OF LAST ADDED CARD: " + index);
        CardPrefab_CreateDeck newCardPrefab = CreateDeckCard(_cardPrefab_CreateDeck.cEntity_Base);

        newCardPrefab.HideDeckCardTab();

        newCardPrefab.transform.SetSiblingIndex(index);

        List<CardPrefab_CreateDeck> cardPrefabs = new List<CardPrefab_CreateDeck>();

        for (int i = 0; i < DeckScroll.content.childCount; i++)
        {
            CardPrefab_CreateDeck cardPrefab = DeckScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>();

            if (cardPrefab != null)
            {
                cardPrefabs.Add(cardPrefab);

                if (cardPrefab.AddRemoveButtonParent.activeSelf)
                {
                    cardPrefab.OffAddRemoveButton();
                    cardPrefab.SetupAddRemoveButton(EdittingDeckData);
                }
            }
        }

        foreach (CardPrefab_CreateDeck cardPrefab in cardPrefabs)
        {
            cardPrefab.transform.SetParent(null);
        }

        foreach (CEntity_Base cEntity_Base in EdittingDeckData.AllDeckCards())
        {
            for (int i = 0; i < cardPrefabs.Count; i++)
            {
                if (cardPrefabs[i].cEntity_Base == cEntity_Base)
                {
                    cardPrefabs[i].transform.SetParent(DeckScroll.content);
                    cardPrefabs.Remove(cardPrefabs[i]);

                    break;
                }
            }
        }

        yield return new WaitForSeconds(Time.deltaTime);

        bool end = false;

        var sequence = DOTween.Sequence();

        sequence
            .Append(draggable_Card.transform.DOMove(newCardPrefab.transform.position, _addCardAnimationTime))
            .Join(draggable_Card.transform.DOScale(new Vector3(0.5f, 0.5f, 0.5f), _addCardAnimationTime))
            .AppendCallback(() => end = true);

        sequence.Play();

        yield return new WaitWhile(() => !end);
        end = false;

        yield return new WaitForSeconds(_addCardAnimationTime);
        newCardPrefab.ShowCardImage();
        draggable_Card.gameObject.SetActive(false);
    }
    #endregion

    #region Animation to remove cards on drop
    IEnumerator RemoveDeckCardCoroutine(CardPrefab_CreateDeck _cardPrefab_CreateDeck, Draggable_Card draggable_Card)
    {
        ContinuousController.instance.PlaySE(Opening.instance.DrawSE);

        bool end = false;

        yield return StartCoroutine(RemoveDeckCards(_cardPrefab_CreateDeck.cEntity_Base));

        var sequence = DOTween.Sequence();

        sequence
            .Append(draggable_Card.transform.DOMove(DisappearDeckCardTransform.position, _addCardAnimationTime))
            .Join(draggable_Card.transform.DOScale(new Vector3(0.5f, 0.5f, 0.5f), _addCardAnimationTime))
            .AppendCallback(() => end = true);

        sequence.Play();

        yield return new WaitWhile(() => !end);
        end = false;

        yield return new WaitForSeconds(_addCardAnimationTime);
        draggable_Card.gameObject.SetActive(false);

        if (_cardPrefab_CreateDeck.transform.parent != CardPoolScroll.content)
        {
            DestroyImmediate(_cardPrefab_CreateDeck.gameObject);
        }
    }
    #endregion

    #region Add card to deck animation when right-clicking
    public IEnumerator AddDeckCardCoroutine_OnClick(CardPrefab_CreateDeck _cardPrefab_CreateDeck)
    {
        if (_cardPrefab_CreateDeck.IsLocked)
        {
            yield break;
        }

        if (!DeckBuildingRule.CanAddCard(_cardPrefab_CreateDeck.cEntity_Base, EdittingDeckData))
        {
            yield break;
        }

        Draggable_Card draggable_Card = CreateDraggable_Card();

        SetStartDrag(_cardPrefab_CreateDeck, draggable_Card);

        draggable_Card.IsDragging = false;

        yield return StartCoroutine(AddDeckCardCoroutine(_cardPrefab_CreateDeck, draggable_Card));

        Reset_EndDrag(draggable_Card);
    }
    #endregion

    #region Animation to remove cards from deck when right-clicking
    public IEnumerator RemoveDeckCardCoroutine_OnClick(CardPrefab_CreateDeck _cardPrefab_CreateDeck)
    {
        Draggable_Card draggable_Card = CreateDraggable_Card();

        _cardPrefab_CreateDeck.HideDeckCardTab();

        SetStartDrag(_cardPrefab_CreateDeck, draggable_Card);

        draggable_Card.IsDragging = false;

        yield return StartCoroutine(RemoveDeckCardCoroutine(_cardPrefab_CreateDeck, draggable_Card));

        Reset_EndDrag(draggable_Card);
    }
    #endregion

    #region add cards to deck
    public IEnumerator AddDeckCards(CEntity_Base cEntity_Base)
    {
        Debug.Log($"ADD DECK CARD: {cEntity_Base.CardID}: {DeckBuildingRule.CanAddCard(cEntity_Base, EdittingDeckData)}");
        if (!DeckBuildingRule.CanAddCard(cEntity_Base, EdittingDeckData))
        {
            yield break;
        }
        Debug.Log($"ADD DECK CARD: {EdittingDeckData.DeckCardIDs.Count}");
        EdittingDeckData.AddCard(cEntity_Base);

        Debug.Log($"ADD DECK CARD: {EdittingDeckData.DeckCardIDs.Count}");
        Debug.Log($"ADD DECK CARD: {EdittingDeckData.DeckCards().Count}");
        EdittingDeckData.DeckCardIDs = DeckData.GetDeckCardCodes(DeckData.SortedDeckCardsList(EdittingDeckData.DeckCards()));
        EdittingDeckData.DigitamaDeckCardIDs = DeckData.GetDeckCardCodes(DeckData.SortedDeckCardsList(EdittingDeckData.DigitamaDeckCards()));
        
        ReflectDeckData();
        cardDistribution.SetCardDistribution(EdittingDeckData);
    }
    #endregion

    #region remove cards from deck
    public IEnumerator RemoveDeckCards(CEntity_Base cEntity_Base)
    {
        if (cEntity_Base.cardKind == CardKind.DigiEgg)
        {
            if (EdittingDeckData.DigitamaDeckCards().Count((cardData) => cardData.CardID == cEntity_Base.CardID) <= 0)
            {
                yield break;
            }
        }

        else
        {
            if (EdittingDeckData.DeckCards().Count((cardData) => cardData.CardID == cEntity_Base.CardID) <= 0)
            {
                yield break;
            }
        }

        EdittingDeckData.RemoveCard(cEntity_Base);

        EdittingDeckData.DeckCardIDs = DeckData.GetDeckCardCodes(DeckData.SortedDeckCardsList(EdittingDeckData.DeckCards()));
        EdittingDeckData.DigitamaDeckCardIDs = DeckData.GetDeckCardCodes(DeckData.SortedDeckCardsList(EdittingDeckData.DigitamaDeckCards()));

        ReflectDeckData();
        cardDistribution.SetCardDistribution(EdittingDeckData);
    }
    #endregion



    #endregion

    public void OnPointerEnterBackground()
    {
        List<CardPrefab_CreateDeck> cardPrefabs = new List<CardPrefab_CreateDeck>();

        for (int i = 0; i < DeckScroll.content.childCount; i++)
        {
            CardPrefab_CreateDeck cardPrefab = DeckScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>();

            if (cardPrefab != null)
            {
                cardPrefabs.Add(cardPrefab);
            }
        }

        foreach (CardPrefab_CreateDeck cardPrefab in cardPrefabs)
        {
            if (cardPrefab != null)
            {
                cardPrefab._OnExit();
            }
        }
    }

    public Button TrialDrawButton;

    public void OnClickTrial5DrawButton()
    {
        Opening.instance.deck.trialDraw.Off();
        ContinuousController.instance.PlaySE(Opening.instance.DecisionSE);
        ContinuousController.instance.StartCoroutine(Opening.instance.deck.trialDraw.SetUpTrialDraw(EdittingDeckData.DeckCards()));
    }

    public Button ClearDeckButton;

    public void OnClickClearDeckButton()
    {
        List<UnityAction> Commands = new List<UnityAction>()
            {
                () =>
                {
                    ContinuousController.instance.StartCoroutine(ClearDeckCoroutine());

                        IEnumerator ClearDeckCoroutine()
                        {
                            DraggingCover.SetActive(true);

                            List<CEntity_Base> cEntity_Bases = new List<CEntity_Base>();

                            foreach(CEntity_Base cEntity_Base in EdittingDeckData.DeckCards())
                            {
                               cEntity_Bases.Add(cEntity_Base);
                            }

                            foreach(CEntity_Base cEntity_Base in EdittingDeckData.DigitamaDeckCards())
                            {
                                cEntity_Bases.Add(cEntity_Base);
                            }

                            foreach(CEntity_Base cEntity_Base in cEntity_Bases)
                            {
                                yield return ContinuousController.instance.StartCoroutine(RemoveDeckCards(cEntity_Base));
                            }

                            for (int i = 0; i < DeckScroll.content.childCount; i++)
                            {
                                Destroy(DeckScroll.content.GetChild(i).gameObject);
                            }

                            yield return new WaitWhile(() => DeckScroll.content.childCount >= 1);

                            DraggingCover.SetActive(false);
                        }
                },

                null,
            };

        List<string> CommandTexts = new List<string>()
        {
            LocalizeUtility.GetLocalizedString(
                    EngMessage: "Yes",
                    JpnMessage: "はい"
                ),
                LocalizeUtility.GetLocalizedString(
                    EngMessage: "No",
                    JpnMessage: "いいえ"
                ),
        };

        Opening.instance.SetUpActiveYesNoObject(
            Commands,
            CommandTexts,
            LocalizeUtility.GetLocalizedString(
            EngMessage: "Remove all cards from the deck?",
            JpnMessage: "全てのカードをデッキから除きますか?"
            ),
            true);
    }

    public void OnClickSetDeckIconButton()
    {
        ContinuousController.instance.StartCoroutine(Opening.instance.deck.deckListPanel.SetUpDeckListPanel(
            EdittingDeckData,
            OnClick,
            LocalizeUtility.GetLocalizedString(
            EngMessage: "Choose a deck icon card.",
            JpnMessage: "デッキアイコン選択"
        )));

        void OnClick(CEntity_Base cEntity_Base)
        {
            EdittingDeckData.KeyCardId = cEntity_Base.CardIndex;
            Opening.instance.deck.deckListPanel.Close();
            Opening.instance.PlayDecisionSE();
        }
    }
}
