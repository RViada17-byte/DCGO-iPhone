using UnityEngine;
using UnityEngine.UI;

public class PlayerInfo : MonoBehaviour
{
    private static readonly Vector2 PlayerNamePadding = new Vector2(32f, 24f);
    private static readonly Vector2 CurrencyPadding = new Vector2(28f, 18f);
    private static readonly Vector2 CurrencySize = new Vector2(180f, 40f);
    private const int CurrencyMaxFontSize = 54;
    private const int CurrencyMinFontSize = 18;

    public InputField PlayerNameInputField;
    public Text WinCountText;
    public Text CurrencyText;

    int _lastDisplayedCurrency = int.MinValue;
    Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
    Vector2 _lastHudParentSize = Vector2.zero;
    RectTransform _playerNameTextRect;
    RectTransform _playerNamePlaceholderRect;
    Vector2 _playerNameTextOffset = Vector2.zero;
    Vector2 _playerNamePlaceholderOffset = Vector2.zero;
    bool _playerNameOffsetsCaptured;

    private void Start()
    {
        EnsureHudLayout(force: true);
        if (PlayerNameInputField == null)
        {
            return;
        }

        PlayerNameInputField.onEndEdit.RemoveAllListeners();
        PlayerNameInputField.onEndEdit.AddListener(SavePlayerName);
    }

    public void SetPlayerInfo()
    {
        gameObject.SetActive(true);
        EnsureCurrencyText();
        EnsureHudLayout(force: true);
        SetPlayerNameAccessoryVisibility(true);

        if (PlayerNameInputField != null && ContinuousController.instance != null)
        {
            PlayerNameInputField.characterLimit = ContinuousController.instance.PlayerNameMaxLength;
            PlayerNameInputField.onEndEdit.RemoveAllListeners();
            PlayerNameInputField.text = ContinuousController.instance.PlayerName;
        }

        if (WinCountText != null && ContinuousController.instance != null)
        {
            WinCountText.text = ContinuousController.instance.WinCount.ToString();
        }

        RefreshCurrencyText(force: true);

        gameObject.SetActive(true);
        if (CurrencyText != null)
        {
            CurrencyText.gameObject.SetActive(true);
        }

        if (PlayerNameInputField != null)
        {
            PlayerNameInputField.gameObject.SetActive(true);
        }

        if (PlayerNameInputField != null)
        {
            PlayerNameInputField.onEndEdit.AddListener(SavePlayerName);
        }
    }

    public void OffPlayerInfo()
    {
        if (CurrencyText != null)
        {
            CurrencyText.gameObject.SetActive(false);
        }

        if (PlayerNameInputField != null)
        {
            PlayerNameInputField.gameObject.SetActive(false);
        }

        SetPlayerNameAccessoryVisibility(false);

        gameObject.SetActive(false);
    }

    public void SavePlayerName(string text)
    {
        if (ContinuousController.instance == null)
        {
            return;
        }

        string playerName = text;

        playerName.Trim();

        while (playerName.Length > ContinuousController.instance.PlayerNameMaxLength)
        {
            playerName = playerName.Substring(0, playerName.Length - 1);
        }

        ContinuousController.instance.SavePlayerName(playerName);

        SetPlayerInfo();
    }

    private void LateUpdate()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        EnsureHudLayout(force: false);

        if (CurrencyText == null)
        {
            return;
        }

        RefreshCurrencyText(force: false);
    }

    void EnsureCurrencyText()
    {
        if (CurrencyText != null)
        {
            return;
        }

        RectTransform parentRectTransform = transform.parent as RectTransform;
        if (parentRectTransform == null)
        {
            return;
        }

        GameObject currencyObject = new GameObject("CurrencyText");
        currencyObject.layer = gameObject.layer;
        currencyObject.transform.SetParent(parentRectTransform, false);

        RectTransform rectTransform = currencyObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.pivot = new Vector2(1f, 0f);
        rectTransform.sizeDelta = CurrencySize;

        currencyObject.AddComponent<CanvasRenderer>();
        CurrencyText = currencyObject.AddComponent<Text>();

        if (WinCountText != null)
        {
            CurrencyText.font = WinCountText.font;
            CurrencyText.material = WinCountText.material;
            CurrencyText.fontStyle = WinCountText.fontStyle;
            CurrencyText.fontSize = WinCountText.fontSize;
            CurrencyText.color = WinCountText.color;
            CurrencyText.lineSpacing = WinCountText.lineSpacing;
            CurrencyText.supportRichText = WinCountText.supportRichText;
            CurrencyText.resizeTextForBestFit = WinCountText.resizeTextForBestFit;
            CurrencyText.resizeTextMinSize = WinCountText.resizeTextMinSize;
            CurrencyText.resizeTextMaxSize = WinCountText.resizeTextMaxSize;
        }

        if (CurrencyText.font == null)
        {
            CurrencyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        ApplyCurrencyStyle();
        CurrencyText.text = "$ 0";
    }

    void RefreshCurrencyText(bool force)
    {
        if (CurrencyText == null || ProgressionManager.Instance == null)
        {
            return;
        }

        int currency = ProgressionManager.Instance.GetCurrency();
        if (!force && currency == _lastDisplayedCurrency)
        {
            return;
        }

        _lastDisplayedCurrency = currency;
        CurrencyText.text = "$ " + currency;
    }

    void EnsureHudLayout(bool force)
    {
        RectTransform hudParent = transform.parent as RectTransform;
        if (hudParent == null)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        Vector2 hudParentSize = hudParent.rect.size;

        if (!force && safeArea == _lastSafeArea && hudParentSize == _lastHudParentSize)
        {
            return;
        }

        _lastSafeArea = safeArea;
        _lastHudParentSize = hudParentSize;

        ApplyPlayerNameLayout(hudParent, safeArea);
        ApplyCurrencyLayout(hudParent, safeArea);
    }

    void ApplyPlayerNameLayout(RectTransform hudParent, Rect safeArea)
    {
        if (PlayerNameInputField == null)
        {
            return;
        }

        CapturePlayerNameOffsets();
    }

    void ApplyCurrencyLayout(RectTransform hudParent, Rect safeArea)
    {
        if (hudParent == null || CurrencyText == null)
        {
            return;
        }

        RectTransform currencyRect = CurrencyText.rectTransform;
        if (currencyRect == null)
        {
            return;
        }

        GetSafeInsetsInCanvasUnits(hudParent, safeArea, out float rightInset, out _, out float bottomInset);
        currencyRect.anchorMin = new Vector2(1f, 0f);
        currencyRect.anchorMax = new Vector2(1f, 0f);
        currencyRect.pivot = new Vector2(1f, 0f);
        currencyRect.anchoredPosition = new Vector2(
            -(rightInset + CurrencyPadding.x),
            bottomInset + CurrencyPadding.y);
        currencyRect.sizeDelta = CurrencySize;
        ApplyCurrencyStyle();
        currencyRect.SetAsLastSibling();
    }

    void ApplyCurrencyStyle()
    {
        if (CurrencyText == null)
        {
            return;
        }

        CurrencyText.fontSize = CurrencyMaxFontSize;
        CurrencyText.resizeTextForBestFit = true;
        CurrencyText.resizeTextMinSize = CurrencyMinFontSize;
        CurrencyText.resizeTextMaxSize = CurrencyMaxFontSize;
        CurrencyText.alignment = TextAnchor.MiddleRight;
        CurrencyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        CurrencyText.verticalOverflow = VerticalWrapMode.Truncate;
        CurrencyText.raycastTarget = false;
    }

    void CapturePlayerNameOffsets()
    {
        if (_playerNameOffsetsCaptured || PlayerNameInputField == null)
        {
            return;
        }

        RectTransform playerNameRect = PlayerNameInputField.transform as RectTransform;
        if (playerNameRect == null)
        {
            return;
        }

        _playerNameTextRect = PlayerNameInputField.textComponent != null ? PlayerNameInputField.textComponent.rectTransform : null;
        _playerNamePlaceholderRect = PlayerNameInputField.placeholder != null ? PlayerNameInputField.placeholder.rectTransform : null;

        if (_playerNameTextRect != null && _playerNameTextRect.parent == playerNameRect.parent)
        {
            _playerNameTextOffset = _playerNameTextRect.anchoredPosition - playerNameRect.anchoredPosition;
        }

        if (_playerNamePlaceholderRect != null && _playerNamePlaceholderRect.parent == playerNameRect.parent)
        {
            _playerNamePlaceholderOffset = _playerNamePlaceholderRect.anchoredPosition - playerNameRect.anchoredPosition;
        }

        _playerNameOffsetsCaptured = true;
    }

    void ApplyPlayerNameAccessoryLayout(RectTransform hudParent, RectTransform accessoryRect, RectTransform playerNameRect, Vector2 offset)
    {
        if (hudParent == null || accessoryRect == null || playerNameRect == null)
        {
            return;
        }

        if (accessoryRect.parent != hudParent)
        {
            accessoryRect.SetParent(hudParent, false);
        }

        accessoryRect.anchorMin = new Vector2(1f, 1f);
        accessoryRect.anchorMax = new Vector2(1f, 1f);
        accessoryRect.anchoredPosition = playerNameRect.anchoredPosition + offset;
        accessoryRect.SetAsLastSibling();
    }

    void SetPlayerNameAccessoryVisibility(bool isVisible)
    {
        if (_playerNameTextRect != null)
        {
            _playerNameTextRect.gameObject.SetActive(isVisible);
        }

        if (_playerNamePlaceholderRect != null)
        {
            _playerNamePlaceholderRect.gameObject.SetActive(isVisible);
        }
    }

    void GetSafeInsetsInCanvasUnits(RectTransform hudParent, Rect safeArea, out float rightInset, out float topInset, out float bottomInset)
    {
        rightInset = 0f;
        topInset = 0f;
        bottomInset = 0f;

        if (hudParent == null)
        {
            return;
        }

        float screenWidth = Mathf.Max(1f, Screen.width);
        float screenHeight = Mathf.Max(1f, Screen.height);
        float unitsPerPixelX = hudParent.rect.width / screenWidth;
        float unitsPerPixelY = hudParent.rect.height / screenHeight;

        rightInset = Mathf.Max(0f, screenWidth - safeArea.xMax) * unitsPerPixelX;
        topInset = Mathf.Max(0f, screenHeight - safeArea.yMax) * unitsPerPixelY;
        bottomInset = Mathf.Max(0f, safeArea.yMin) * unitsPerPixelY;
    }
}
