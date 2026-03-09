using UnityEngine;

[ExecuteAlways]
public class DeckMode : MonoBehaviour
{
    enum EditorPreviewScreen
    {
        None,
        SelectDeck,
        EditDeck,
        DeckList,
    }

    [Header("DeckButton")]
    public OpeningButton DeckButton;

    [Header("selectDeck")]
    public SelectDeck selectDeck;

    [Header("editDeck")]
    public EditDeck editDeck;

    [Header("デッキ確認")]
    public DeckListPanel deckListPanel;

    [Header("Trial5Draw")]
    public TrialDraw trialDraw;

    [Header("Editor Preview")]
    [SerializeField] EditorPreviewScreen _editorPreviewScreen = EditorPreviewScreen.None;
    [SerializeField] bool _applyIPhoneThemeInEditorPreview = true;

    bool first = false;
    bool _isLegacyIPhoneDeckPresentationActive;
    bool _didCaptureOrientationState;
    ScreenOrientation _previousOrientation = ScreenOrientation.AutoRotation;
    bool _previousAutorotateToPortrait;
    bool _previousAutorotateToPortraitUpsideDown;
    bool _previousAutorotateToLandscapeLeft = true;
    bool _previousAutorotateToLandscapeRight = true;
    bool _capturedEditorPreviewStates;
    bool _selectDeckActiveBeforePreview;
    bool _editDeckActiveBeforePreview;
    bool _deckListActiveBeforePreview;
    EditorPreviewScreen _lastStyledPreviewScreen = (EditorPreviewScreen)(-1);

    public bool IsLegacyIPhoneDeckBuilderActive =>
        Application.platform == RuntimePlatform.IPhonePlayer &&
        (
            (selectDeck != null && selectDeck.isOpen && selectDeck.SelectDeckObject != null && selectDeck.SelectDeckObject.activeSelf) ||
            (editDeck != null && editDeck.isEditting && editDeck.CreateDeckObject != null && editDeck.CreateDeckObject.activeSelf) ||
            (deckListPanel != null && deckListPanel.gameObject.activeSelf) ||
            (trialDraw != null && trialDraw.gameObject.activeSelf)
        );
    public bool IsIPhoneDeckBuilderActive => IsLegacyIPhoneDeckBuilderActive;

    void OnEnable()
    {
        ApplyEditorPreviewState();
    }

    void Update()
    {
        ApplyEditorPreviewState();
    }

    void OnDisable()
    {
        RestoreEditorPreviewState();
    }

    public void OffDeck()
    {
        RestoreIPhoneLegacyPresentation();

        selectDeck?.OffSelectDeck();

        if (editDeck != null && editDeck.CreateDeckObject != null)
        {
            editDeck.CreateDeckObject.SetActive(false);
        }

        if(!first)
        {
            DeckButton.OnExit();
            first = true;
        }

        trialDraw?.Off();

        deckListPanel?.Off();
    }

    public void SetUpDeckMode()
    {
        ApplyIPhoneLegacyPresentation();

        if(selectDeck != null && selectDeck.isOpen)
        {
            return;
        }

        if (editDeck != null && editDeck.CreateDeckObject != null)
        {
            editDeck.CreateDeckObject.SetActive(false);
        }

        selectDeck?.SetUpSelectDeck();

        Opening.instance.optionPanel.CloseOptionPanel();
    }

    void ApplyIPhoneLegacyPresentation()
    {
        if (Application.platform != RuntimePlatform.IPhonePlayer)
        {
            return;
        }

        if (!_didCaptureOrientationState)
        {
            _didCaptureOrientationState = true;
            _previousOrientation = Screen.orientation;
            _previousAutorotateToPortrait = Screen.autorotateToPortrait;
            _previousAutorotateToPortraitUpsideDown = Screen.autorotateToPortraitUpsideDown;
            _previousAutorotateToLandscapeLeft = Screen.autorotateToLandscapeLeft;
            _previousAutorotateToLandscapeRight = Screen.autorotateToLandscapeRight;
        }

        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = Screen.width < Screen.height
            ? ScreenOrientation.LandscapeLeft
            : ScreenOrientation.AutoRotation;

        _isLegacyIPhoneDeckPresentationActive = true;

        if (Opening.instance != null && Opening.instance.home != null)
        {
            Opening.instance.home.OffHome();
        }
        else
        {
            Opening.instance?.OffModeButtons();
        }
    }

    void RestoreIPhoneLegacyPresentation()
    {
        if (Application.platform != RuntimePlatform.IPhonePlayer || !_isLegacyIPhoneDeckPresentationActive)
        {
            return;
        }

        _isLegacyIPhoneDeckPresentationActive = false;

        if (_didCaptureOrientationState)
        {
            Screen.autorotateToPortrait = _previousAutorotateToPortrait;
            Screen.autorotateToPortraitUpsideDown = _previousAutorotateToPortraitUpsideDown;
            Screen.autorotateToLandscapeLeft = _previousAutorotateToLandscapeLeft;
            Screen.autorotateToLandscapeRight = _previousAutorotateToLandscapeRight;
            Screen.orientation = _previousOrientation;
            _didCaptureOrientationState = false;
        }
    }

    void ApplyEditorPreviewState()
    {
        if (Application.isPlaying)
        {
            RestoreEditorPreviewState();
            return;
        }

        bool wantsPreview = _editorPreviewScreen != EditorPreviewScreen.None;
        if (!wantsPreview)
        {
            RestoreEditorPreviewState();
            return;
        }

        CaptureEditorPreviewStateIfNeeded();

        GameObject selectDeckRoot = selectDeck != null ? selectDeck.SelectDeckObject : null;
        GameObject editDeckRoot = editDeck != null ? editDeck.CreateDeckObject : null;
        GameObject deckListRoot = GetDeckListPreviewRoot();

        bool previewSelectDeck = _editorPreviewScreen == EditorPreviewScreen.SelectDeck;
        bool previewEditDeck = _editorPreviewScreen == EditorPreviewScreen.EditDeck;
        bool previewDeckList = _editorPreviewScreen == EditorPreviewScreen.DeckList;

        if (selectDeckRoot != null)
        {
            selectDeckRoot.SetActive(previewSelectDeck);
        }

        if (editDeckRoot != null)
        {
            editDeckRoot.SetActive(previewEditDeck);
        }

        if (deckListRoot != null)
        {
            deckListRoot.SetActive(previewDeckList);
        }

        selectDeck?.SetEditorPreviewActive(previewSelectDeck);
        editDeck?.SetEditorPreviewActive(previewEditDeck);

        if (editDeck != null && editDeck.DetailCard != null && !previewEditDeck)
        {
            editDeck.DetailCard.gameObject.SetActive(false);
        }

        if (_applyIPhoneThemeInEditorPreview && _lastStyledPreviewScreen != _editorPreviewScreen)
        {
            MobileUiPolishRuntime.ApplyDeckUiThemePreview(
                selectDeckRoot,
                editDeckRoot,
                deckListRoot);
            _lastStyledPreviewScreen = _editorPreviewScreen;
        }
    }

    void CaptureEditorPreviewStateIfNeeded()
    {
        if (_capturedEditorPreviewStates)
        {
            return;
        }

        _capturedEditorPreviewStates = true;
        _selectDeckActiveBeforePreview = selectDeck != null &&
            selectDeck.SelectDeckObject != null &&
            selectDeck.SelectDeckObject.activeSelf;
        _editDeckActiveBeforePreview = editDeck != null &&
            editDeck.CreateDeckObject != null &&
            editDeck.CreateDeckObject.activeSelf;

        GameObject deckListRoot = GetDeckListPreviewRoot();
        _deckListActiveBeforePreview = deckListRoot != null && deckListRoot.activeSelf;
    }

    void RestoreEditorPreviewState()
    {
        if (!_capturedEditorPreviewStates)
        {
            return;
        }

        selectDeck?.SetEditorPreviewActive(false);
        editDeck?.SetEditorPreviewActive(false);

        if (selectDeck != null && selectDeck.SelectDeckObject != null)
        {
            selectDeck.SelectDeckObject.SetActive(_selectDeckActiveBeforePreview);
        }

        if (editDeck != null && editDeck.CreateDeckObject != null)
        {
            editDeck.CreateDeckObject.SetActive(_editDeckActiveBeforePreview);
        }

        GameObject deckListRoot = GetDeckListPreviewRoot();
        if (deckListRoot != null)
        {
            deckListRoot.SetActive(_deckListActiveBeforePreview);
        }

        _capturedEditorPreviewStates = false;
        _lastStyledPreviewScreen = (EditorPreviewScreen)(-1);
    }

    GameObject GetDeckListPreviewRoot()
    {
        if (deckListPanel == null)
        {
            return null;
        }

        if (deckListPanel.DeckListPanelObject != null)
        {
            return deckListPanel.DeckListPanelObject;
        }

        return deckListPanel.gameObject;
    }
}
