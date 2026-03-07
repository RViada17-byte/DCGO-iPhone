using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RuntimeModeRootFactory
{
    private const string BackButtonName = "BackButton";
    private const string TitleTextName = "TitleText";
    private const string PlaceholderTextName = "PlaceholderText";
    private const string ModeButtonsName = "ModeButtons";
    private const string WorldModeRootName = "WorldModeRoot";
    private const string StoryModeRootName = "StoryModeRoot";
    private const string DuelistBoardModeRootName = "DuelistBoardModeRoot";
    private const string ShopModeRootName = "ShopModeRoot";
    private const string StoryModeButtonName = "StoryModeButton";
    private const string DuelistBoardButtonName = "DuelistBoardButton";
    private static Font _runtimeFont;

    public static GameObject FindOrCreateRoot(Scene scene, Opening opening, MainMenuRouter router, RectTransform parent, string rootName, string titleText)
    {
        GameObject root = FindGameObjectByName(scene, rootName);
        if (root == null)
        {
            Debug.LogWarning($"{nameof(RuntimeModeRootFactory)} could not find custom root '{rootName}' in scene '{scene.name}'. Creating runtime placeholder.");
            if (parent == null)
            {
                Debug.LogError($"{nameof(RuntimeModeRootFactory)} could not create '{rootName}' because no UI canvas parent was found.");
                return null;
            }

            root = CreatePlaceholderRoot(opening, router, scene, parent, rootName, titleText);
        }

        if (root == null)
        {
            return null;
        }

        EnsureTitleText(root.transform, titleText);
        EnsureBackButton(root.transform, scene, opening, router);
        if (root.name == WorldModeRootName)
        {
            EnsureWorldModeChoices(root.transform, scene, opening, router);
        }
        else if (root.name == StoryModeRootName)
        {
            EnsureStoryMode(root.transform);
        }
        else if (root.name == DuelistBoardModeRootName)
        {
            EnsureDuelistBoardMode(root.transform);
        }
        else if (root.name == ShopModeRootName)
        {
            EnsureShopMode(root.transform);
        }

        root.SetActive(false);
        return root;
    }

    private static GameObject CreatePlaceholderRoot(Opening opening, MainMenuRouter router, Scene scene, RectTransform parent, string rootName, string titleText)
    {
        if (parent == null)
        {
            return null;
        }

        GameObject rootObject = new GameObject(rootName, typeof(RectTransform), typeof(Image));
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.SetParent(parent, false);
        StretchFullScreen(rootRect);

        Image background = rootObject.GetComponent<Image>();
        if (background != null)
        {
            background.color = new Color(0.03137255f, 0.05882353f, 0.12156863f, 0.92f);
            background.raycastTarget = true;
        }

        CreateTitleText(rootRect, titleText);
        EnsureBackButton(rootRect, scene, opening, router);
        PlaceRootBehindModeButtons(scene, opening, rootRect);
        rootObject.SetActive(false);
        return rootObject;
    }

    private static void EnsureTitleText(Transform root, string titleText)
    {
        if (root == null || string.IsNullOrWhiteSpace(titleText))
        {
            return;
        }

        Transform titleTransform = FindChildRecursive(root, TitleTextName);
        Text title = null;
        TMP_Text tmpTitle = null;

        if (titleTransform != null)
        {
            title = titleTransform.GetComponent<Text>();
            tmpTitle = titleTransform.GetComponent<TMP_Text>();
        }

        if (title == null && tmpTitle == null)
        {
            GameObject titleObject = new GameObject(TitleTextName, typeof(RectTransform), typeof(Text));
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.SetParent(root, false);
            title = titleObject.GetComponent<Text>();
        }

        if (tmpTitle != null)
        {
            RectTransform titleRect = tmpTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, -120f);
            titleRect.sizeDelta = new Vector2(1000f, 160f);
            tmpTitle.text = titleText;
            tmpTitle.alignment = TextAlignmentOptions.Center;
            tmpTitle.fontSize = 96f;
            tmpTitle.color = Color.white;
            tmpTitle.raycastTarget = false;
            return;
        }

        if (title == null)
        {
            return;
        }

        RectTransform titleTextRect = title.rectTransform;
        titleTextRect.anchorMin = new Vector2(0.5f, 1f);
        titleTextRect.anchorMax = new Vector2(0.5f, 1f);
        titleTextRect.pivot = new Vector2(0.5f, 0.5f);
        titleTextRect.anchoredPosition = new Vector2(0f, -120f);
        titleTextRect.sizeDelta = new Vector2(1000f, 160f);
        title.font = GetRuntimeFont();
        title.text = titleText;
        title.fontSize = 96;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        title.verticalOverflow = VerticalWrapMode.Overflow;
        title.raycastTarget = false;
    }

    private static void EnsurePlaceholderText(Transform root, string message)
    {
        if (root == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Transform placeholderTransform = FindChildRecursive(root, PlaceholderTextName);
        Text placeholder = null;
        TMP_Text tmpPlaceholder = null;

        if (placeholderTransform != null)
        {
            placeholder = placeholderTransform.GetComponent<Text>();
            tmpPlaceholder = placeholderTransform.GetComponent<TMP_Text>();
        }

        if (placeholder == null && tmpPlaceholder == null)
        {
            GameObject placeholderObject = new GameObject(PlaceholderTextName, typeof(RectTransform), typeof(Text));
            RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.SetParent(root, false);
            placeholder = placeholderObject.GetComponent<Text>();
        }

        if (tmpPlaceholder != null)
        {
            RectTransform placeholderRect = tmpPlaceholder.rectTransform;
            placeholderRect.anchorMin = new Vector2(0.5f, 0.5f);
            placeholderRect.anchorMax = new Vector2(0.5f, 0.5f);
            placeholderRect.pivot = new Vector2(0.5f, 0.5f);
            placeholderRect.anchoredPosition = Vector2.zero;
            placeholderRect.sizeDelta = new Vector2(860f, 180f);
            tmpPlaceholder.text = message;
            tmpPlaceholder.alignment = TextAlignmentOptions.Center;
            tmpPlaceholder.fontSize = 52f;
            tmpPlaceholder.color = Color.white;
            tmpPlaceholder.raycastTarget = false;
            return;
        }

        if (placeholder == null)
        {
            return;
        }

        RectTransform placeholderTextRect = placeholder.rectTransform;
        placeholderTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        placeholderTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        placeholderTextRect.pivot = new Vector2(0.5f, 0.5f);
        placeholderTextRect.anchoredPosition = Vector2.zero;
        placeholderTextRect.sizeDelta = new Vector2(860f, 180f);
        placeholder.font = GetRuntimeFont();
        placeholder.text = message;
        placeholder.fontSize = 52;
        placeholder.fontStyle = FontStyle.Bold;
        placeholder.alignment = TextAnchor.MiddleCenter;
        placeholder.color = Color.white;
        placeholder.horizontalOverflow = HorizontalWrapMode.Wrap;
        placeholder.verticalOverflow = VerticalWrapMode.Overflow;
        placeholder.resizeTextForBestFit = true;
        placeholder.resizeTextMinSize = 20;
        placeholder.resizeTextMaxSize = 52;
        placeholder.raycastTarget = false;
    }

    private static void EnsureShopMode(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform placeholderTransform = FindChildRecursive(root, PlaceholderTextName);
        if (placeholderTransform != null)
        {
            placeholderTransform.gameObject.SetActive(false);
        }

        ShopPanel shopPanel = root.GetComponent<ShopPanel>();
        if (shopPanel == null)
        {
            shopPanel = root.gameObject.AddComponent<ShopPanel>();
        }

        if (shopPanel != null && !shopPanel.enabled)
        {
            shopPanel.enabled = true;
        }
    }

    private static void EnsureStoryMode(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform placeholderTransform = FindChildRecursive(root, PlaceholderTextName);
        if (placeholderTransform != null)
        {
            placeholderTransform.gameObject.SetActive(false);
        }

        StoryPanel storyPanel = root.GetComponent<StoryPanel>();
        if (storyPanel == null)
        {
            storyPanel = root.gameObject.AddComponent<StoryPanel>();
        }

        if (storyPanel != null && !storyPanel.enabled)
        {
            storyPanel.enabled = true;
        }
    }

    private static void EnsureDuelistBoardMode(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform placeholderTransform = FindChildRecursive(root, PlaceholderTextName);
        if (placeholderTransform != null)
        {
            placeholderTransform.gameObject.SetActive(false);
        }

        DuelistBoardPanel duelistBoardPanel = root.GetComponent<DuelistBoardPanel>();
        if (duelistBoardPanel == null)
        {
            duelistBoardPanel = root.gameObject.AddComponent<DuelistBoardPanel>();
        }

        if (duelistBoardPanel != null && !duelistBoardPanel.enabled)
        {
            duelistBoardPanel.enabled = true;
        }
    }

    private static void EnsureBackButton(Transform root, Scene scene, Opening opening, MainMenuRouter router)
    {
        if (root == null || router == null)
        {
            return;
        }

        Button backButton = FindComponentInChildrenByName<Button>(root, BackButtonName);
        if (backButton == null)
        {
            backButton = TryCloneMenuButton(scene, opening, root, BackButtonName);
        }

        if (backButton == null)
        {
            backButton = CreateSimpleButton(root, BackButtonName, "BACK");
        }

        if (backButton == null)
        {
            return;
        }

        backButton.gameObject.name = BackButtonName;
        backButton.gameObject.SetActive(true);
        PositionBackButton(backButton);
        ConfigureButtonAction(backButton, router.BackToHome);
        SetButtonLabel(backButton, "BACK");
    }

    private static void EnsureWorldModeChoices(Transform root, Scene scene, Opening opening, MainMenuRouter router)
    {
        if (root == null || router == null)
        {
            return;
        }

        Button storyButton = FindButtonByNameOrLabel(root, StoryModeButtonName, "STORY MODE");
        if (storyButton == null)
        {
            storyButton = TryCloneMenuButton(scene, opening, root, StoryModeButtonName);
        }

        if (storyButton == null)
        {
            storyButton = CreateSimpleButton(root, StoryModeButtonName, "STORY MODE");
        }

        if (storyButton != null)
        {
            storyButton.gameObject.name = StoryModeButtonName;
            storyButton.gameObject.SetActive(true);
            PositionWorldChoiceButton(storyButton, 56f);
            ConfigureButtonAction(storyButton, router.OpenStory);
            SetButtonLabel(storyButton, "STORY MODE");
        }

        Button duelistBoardButton = FindButtonByNameOrLabel(root, DuelistBoardButtonName, "DUELIST BOARD");
        if (duelistBoardButton == null)
        {
            duelistBoardButton = TryCloneMenuButton(scene, opening, root, DuelistBoardButtonName);
        }

        if (duelistBoardButton == null)
        {
            duelistBoardButton = CreateSimpleButton(root, DuelistBoardButtonName, "DUELIST BOARD");
        }

        if (duelistBoardButton != null)
        {
            duelistBoardButton.gameObject.name = DuelistBoardButtonName;
            duelistBoardButton.gameObject.SetActive(true);
            PositionWorldChoiceButton(duelistBoardButton, -56f);
            ConfigureButtonAction(duelistBoardButton, router.OpenDuelistBoard);
            SetButtonLabel(duelistBoardButton, "DUELIST BOARD");
        }
    }

    private static Button TryCloneMenuButton(Scene scene, Opening opening, Transform root, string objectName)
    {
        GameObject modeButtons = ResolveModeButtons(scene, opening);
        if (modeButtons == null)
        {
            return null;
        }

        Button[] buttons = modeButtons.GetComponentsInChildren<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            Button template = buttons[index];
            if (template == null)
            {
                continue;
            }

            GameObject clone = Object.Instantiate(template.gameObject, root, false);
            if (clone == null)
            {
                continue;
            }

            clone.name = objectName;
            clone.SetActive(true);
            return clone.GetComponent<Button>();
        }

        return null;
    }

    private static Button CreateSimpleButton(Transform root, string objectName, string label)
    {
        if (root == null)
        {
            return null;
        }

        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(root, false);

        Image buttonImage = buttonObject.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = new Color(0.11764706f, 0.1882353f, 0.28627452f, 0.92f);
            buttonImage.raycastTarget = true;
        }

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(buttonRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text buttonText = textObject.GetComponent<Text>();
        if (buttonText != null)
        {
            buttonText.font = GetRuntimeFont();
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.fontSize = 44;
            buttonText.color = Color.white;
            buttonText.raycastTarget = false;
            buttonText.text = label;
        }

        return buttonObject.GetComponent<Button>();
    }

    private static void PositionBackButton(Button backButton)
    {
        if (backButton == null)
        {
            return;
        }

        RectTransform buttonRect = backButton.GetComponent<RectTransform>();
        if (buttonRect == null)
        {
            return;
        }

        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.anchoredPosition = new Vector2(32f, -32f);
        buttonRect.sizeDelta = new Vector2(220f, 72f);
        buttonRect.localScale = Vector3.one;
    }

    private static void PositionWorldChoiceButton(Button button, float yPosition)
    {
        if (button == null)
        {
            return;
        }

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect == null)
        {
            return;
        }

        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, yPosition);
        buttonRect.sizeDelta = new Vector2(520f, 96f);
        buttonRect.localScale = Vector3.one;
    }

    private static void ConfigureButtonAction(Button button, UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        RemovePointerClickTriggers(button.gameObject);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);

        Button[] nestedButtons = button.GetComponentsInChildren<Button>(true);
        for (int index = 0; index < nestedButtons.Length; index++)
        {
            Button nestedButton = nestedButtons[index];
            if (nestedButton == null || nestedButton == button)
            {
                continue;
            }

            RemovePointerClickTriggers(nestedButton.gameObject);
            nestedButton.onClick.RemoveAllListeners();
        }
    }

    private static Button FindButtonByNameOrLabel(Transform root, string buttonName, string labelText)
    {
        if (root == null)
        {
            return null;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = buttons[index];
            if (button == null)
            {
                continue;
            }

            if (button.gameObject.name == buttonName)
            {
                return button;
            }
        }

        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = buttons[index];
            if (button == null)
            {
                continue;
            }

            string normalizedLabel = GetButtonLabel(button);
            if (!string.IsNullOrWhiteSpace(normalizedLabel) && normalizedLabel == labelText)
            {
                return button;
            }
        }

        return null;
    }

    private static string GetButtonLabel(Button button)
    {
        if (button == null)
        {
            return null;
        }

        TMP_Text[] tmpLabels = button.GetComponentsInChildren<TMP_Text>(true);
        for (int index = 0; index < tmpLabels.Length; index++)
        {
            TMP_Text tmpLabel = tmpLabels[index];
            if (tmpLabel != null && !string.IsNullOrWhiteSpace(tmpLabel.text))
            {
                return tmpLabel.text.Trim().ToUpperInvariant();
            }
        }

        Text[] uiLabels = button.GetComponentsInChildren<Text>(true);
        for (int index = 0; index < uiLabels.Length; index++)
        {
            Text uiLabel = uiLabels[index];
            if (uiLabel != null && !string.IsNullOrWhiteSpace(uiLabel.text))
            {
                return uiLabel.text.Trim().ToUpperInvariant();
            }
        }

        return null;
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        bool updated = false;

        TMP_Text[] tmpLabels = button.GetComponentsInChildren<TMP_Text>(true);
        for (int index = 0; index < tmpLabels.Length; index++)
        {
            TMP_Text tmpLabel = tmpLabels[index];
            if (tmpLabel == null)
            {
                continue;
            }

            tmpLabel.text = label;
            updated = true;
        }

        Text[] uiLabels = button.GetComponentsInChildren<Text>(true);
        for (int index = 0; index < uiLabels.Length; index++)
        {
            Text uiLabel = uiLabels[index];
            if (uiLabel == null)
            {
                continue;
            }

            uiLabel.text = label;
            updated = true;
        }

        if (updated)
        {
            return;
        }

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(button.transform, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObject.GetComponent<Text>();
        if (text == null)
        {
            return;
        }

        text.font = GetRuntimeFont();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = 44;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private static GameObject ResolveModeButtons(Scene scene, Opening opening)
    {
        GameObject modeButtons = opening != null ? opening.ModeButtons : null;
        if (modeButtons != null)
        {
            return modeButtons;
        }

        modeButtons = FindGameObjectByName(scene, ModeButtonsName);
        if (opening != null && modeButtons != null)
        {
            opening.ModeButtons = modeButtons;
        }

        return modeButtons;
    }

    private static void CreateTitleText(RectTransform parent, string titleText)
    {
        if (parent == null)
        {
            return;
        }

        GameObject titleObject = new GameObject(TitleTextName, typeof(RectTransform), typeof(Text));
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.SetParent(parent, false);
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, -120f);
        titleRect.sizeDelta = new Vector2(1000f, 160f);

        Text title = titleObject.GetComponent<Text>();
        if (title == null)
        {
            return;
        }

        title.font = GetRuntimeFont();
        title.text = titleText;
        title.fontSize = 96;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        title.verticalOverflow = VerticalWrapMode.Overflow;
        title.raycastTarget = false;
    }

    private static void PlaceRootBehindModeButtons(Scene scene, Opening opening, RectTransform rootRect)
    {
        if (rootRect == null)
        {
            return;
        }

        GameObject modeButtonsObject = ResolveModeButtons(scene, opening);
        if (modeButtonsObject == null)
        {
            return;
        }

        Transform modeButtons = modeButtonsObject.transform;
        if (modeButtons.parent != rootRect.parent)
        {
            return;
        }

        rootRect.SetSiblingIndex(modeButtons.GetSiblingIndex());
    }

    private static void StretchFullScreen(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private static void RemovePointerClickTriggers(GameObject buttonRoot)
    {
        if (buttonRoot == null)
        {
            return;
        }

        EventTrigger[] triggers = buttonRoot.GetComponentsInChildren<EventTrigger>(true);
        for (int triggerIndex = 0; triggerIndex < triggers.Length; triggerIndex++)
        {
            EventTrigger trigger = triggers[triggerIndex];
            if (trigger == null || trigger.triggers == null)
            {
                continue;
            }

            for (int entryIndex = trigger.triggers.Count - 1; entryIndex >= 0; entryIndex--)
            {
                EventTrigger.Entry entry = trigger.triggers[entryIndex];
                if (entry != null && entry.eventID == EventTriggerType.PointerClick)
                {
                    trigger.triggers.RemoveAt(entryIndex);
                }
            }
        }
    }

    private static Font GetRuntimeFont()
    {
        if (_runtimeFont == null)
        {
            _runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_runtimeFont == null)
            {
                _runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }

        return _runtimeFont;
    }

    private static GameObject FindGameObjectByName(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int index = 0; index < rootObjects.Length; index++)
        {
            GameObject rootObject = rootObjects[index];
            if (rootObject == null)
            {
                continue;
            }

            Transform match = FindChildRecursive(rootObject.transform, objectName);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);
            Transform match = FindChildRecursive(child, objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static T FindComponentInChildrenByName<T>(Transform root, string objectName) where T : Component
    {
        Transform target = FindChildRecursive(root, objectName);
        if (target == null)
        {
            return null;
        }

        return target.GetComponent<T>();
    }
}
