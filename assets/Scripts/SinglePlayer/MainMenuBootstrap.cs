using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuBootstrap
{
    private const string OpeningSceneName = "Opening";
    private const string WorldModeRootName = "WorldModeRoot";
    private const string StoryModeRootName = "StoryModeRoot";
    private const string DuelistBoardModeRootName = "DuelistBoardModeRoot";
    private const string ShopModeRootName = "ShopModeRoot";
    private const string ModeButtonsName = "ModeButtons";
    private const string BattleButtonName = "BattleButton";
    private const string DeckButtonName = "DeckButton";
    private const string ConfigButtonName = "ConfigButton";
    private const string PatchNotesButtonName = "PatchNotesButton";
    private const string ReportButtonName = "ReportButton";
    private const string CustomClickCatcherName = "CustomClickCatcher";

    private static readonly HashSet<int> ConfiguredSceneHandles = new HashSet<int>();
    private static bool _subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (_subscribed)
        {
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        ConfiguredSceneHandles.Clear();
        _subscribed = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || scene.name != OpeningSceneName)
        {
            return;
        }

        if (!ConfiguredSceneHandles.Add(scene.handle))
        {
            return;
        }

        SetupOpeningScene(scene);
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        ConfiguredSceneHandles.Remove(scene.handle);
    }

    private static void SetupOpeningScene(Scene scene)
    {
        Opening opening = Opening.instance;
        if (opening == null)
        {
            Debug.LogError($"{nameof(MainMenuBootstrap)} could not initialize {OpeningSceneName}: Opening.instance was null.");
            return;
        }

        MainMenuRouter router = opening.gameObject.GetComponent<MainMenuRouter>();
        if (router == null)
        {
            router = opening.gameObject.AddComponent<MainMenuRouter>();
        }

        if (router == null)
        {
            Debug.LogError($"{nameof(MainMenuBootstrap)} could not add {nameof(MainMenuRouter)} to {opening.gameObject.name}.");
            return;
        }

        router.opening = opening;
        SetupCustomMenu(scene, opening, router);
    }

    private static void SetupCustomMenu(Scene scene, Opening opening, MainMenuRouter router)
    {
        if (opening == null || router == null)
        {
            return;
        }

        WireCustomRoots(scene, opening, router);
        RepurposeModeButtons(scene, opening, router);
        Debug.Log("[CustomMenu] Ready: WORLD/DECK/SHOP wired, PATCH and REPORT hidden.");
    }

    private static void WireCustomRoots(Scene scene, Opening opening, MainMenuRouter router)
    {
        if (opening == null || router == null)
        {
            return;
        }

        RectTransform rootParent = ResolveRootParent(opening, scene);

        bool worldRootExisted = FindGameObjectByName(scene, WorldModeRootName) != null;
        bool storyRootExisted = FindGameObjectByName(scene, StoryModeRootName) != null;
        bool duelistBoardRootExisted = FindGameObjectByName(scene, DuelistBoardModeRootName) != null;
        bool shopRootExisted = FindGameObjectByName(scene, ShopModeRootName) != null;

        router.worldModeRoot = RuntimeModeRootFactory.FindOrCreateRoot(scene, opening, router, rootParent, WorldModeRootName, "WORLD");
        router.storyModeRoot = RuntimeModeRootFactory.FindOrCreateRoot(scene, opening, router, rootParent, StoryModeRootName, "STORY MODE");
        router.duelistBoardModeRoot = RuntimeModeRootFactory.FindOrCreateRoot(scene, opening, router, rootParent, DuelistBoardModeRootName, "DUELIST BOARD");
        router.shopModeRoot = RuntimeModeRootFactory.FindOrCreateRoot(scene, opening, router, rootParent, ShopModeRootName, "SHOP");

        LogRootStatus(WorldModeRootName, router.worldModeRoot, worldRootExisted);
        LogRootStatus(StoryModeRootName, router.storyModeRoot, storyRootExisted);
        LogRootStatus(DuelistBoardModeRootName, router.duelistBoardModeRoot, duelistBoardRootExisted);
        LogRootStatus(ShopModeRootName, router.shopModeRoot, shopRootExisted);
    }

    private static void LogRootStatus(string rootName, GameObject root, bool existedInScene)
    {
        if (root == null)
        {
            Debug.LogWarning($"[CustomMenu] {rootName}: unavailable.");
            return;
        }

        string status = existedInScene ? "found existing" : "runtime-created";
        Debug.Log($"[CustomMenu] {rootName}: {status}.");
    }

    private static RectTransform ResolveRootParent(Opening opening, Scene scene)
    {
        if (opening != null && opening.canvasRect != null)
        {
            return opening.canvasRect;
        }

        Canvas firstCanvas = FindFirstCanvas(scene);
        if (firstCanvas != null)
        {
            return firstCanvas.transform as RectTransform;
        }

        return null;
    }

    private static Canvas FindFirstCanvas(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
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

            Canvas canvas = rootObject.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                return canvas;
            }
        }

        return null;
    }

    private static void RepurposeModeButtons(Scene scene, Opening opening, MainMenuRouter router)
    {
        if (router == null)
        {
            return;
        }

        GameObject modeButtons = ResolveModeButtons(scene, opening);
        if (modeButtons == null)
        {
            Debug.LogWarning($"{nameof(MainMenuBootstrap)} could not find {ModeButtonsName} to repurpose menu buttons.");
            return;
        }

        Transform modeButtonsTransform = modeButtons.transform;
        bool matchedAny =
            ConfigureNamedMenuEntry(modeButtonsTransform, BattleButtonName, "WORLD", router.OpenWorld)
            | ConfigureNamedMenuEntry(modeButtonsTransform, ConfigButtonName, "SHOP", router.OpenShop)
            | HideNamedMenuEntry(modeButtonsTransform, PatchNotesButtonName)
            | HideNamedMenuEntry(modeButtonsTransform, ReportButtonName)
            | LogUnchangedMenuEntry(modeButtonsTransform, DeckButtonName);

        if (!matchedAny)
        {
            Debug.LogWarning($"{nameof(MainMenuBootstrap)} could not match any named entries under {ModeButtonsName}.");
        }
    }

    private static GameObject ResolveModeButtons(Scene scene, Opening opening)
    {
        GameObject modeButtons = opening != null ? opening.ModeButtons : null;
        if (modeButtons == null)
        {
            modeButtons = FindGameObjectByName(scene, ModeButtonsName);
            if (opening != null && modeButtons != null)
            {
                opening.ModeButtons = modeButtons;
            }
        }

        return modeButtons;
    }

    private static bool ConfigureNamedMenuEntry(Transform modeButtonsRoot, string entryName, string label, UnityAction action)
    {
        if (modeButtonsRoot == null || string.IsNullOrWhiteSpace(entryName) || string.IsNullOrWhiteSpace(label) || action == null)
        {
            return false;
        }

        Transform entryTransform = FindChildRecursive(modeButtonsRoot, entryName);
        if (entryTransform == null)
        {
            Debug.LogWarning($"{nameof(MainMenuBootstrap)} could not find menu entry '{entryName}'.");
            return false;
        }

        GameObject entryRoot = entryTransform.gameObject;
        entryRoot.SetActive(true);
        UpdateMenuEntryLabels(entryRoot, label);
        ClearLegacyMenuInteractions(entryRoot);

        Button clickCatcher = EnsureMenuClickCatcher(entryTransform);
        if (clickCatcher == null)
        {
            Debug.LogWarning($"{nameof(MainMenuBootstrap)} could not create click catcher for '{entryName}'.");
            return true;
        }

        ConfigureButtonAction(clickCatcher, action);
        Debug.Log($"{nameof(MainMenuBootstrap)} repurposed '{entryName}' to {label}.");
        return true;
    }

    private static bool HideNamedMenuEntry(Transform modeButtonsRoot, string entryName)
    {
        if (modeButtonsRoot == null || string.IsNullOrWhiteSpace(entryName))
        {
            return false;
        }

        Transform entryTransform = FindChildRecursive(modeButtonsRoot, entryName);
        if (entryTransform == null)
        {
            Debug.LogWarning($"{nameof(MainMenuBootstrap)} could not find menu entry '{entryName}' to hide.");
            return false;
        }

        entryTransform.gameObject.SetActive(false);
        Debug.Log($"{nameof(MainMenuBootstrap)} hid '{entryName}'.");
        return true;
    }

    private static bool LogUnchangedMenuEntry(Transform modeButtonsRoot, string entryName)
    {
        if (modeButtonsRoot == null || string.IsNullOrWhiteSpace(entryName))
        {
            return false;
        }

        Transform entryTransform = FindChildRecursive(modeButtonsRoot, entryName);
        if (entryTransform == null)
        {
            Debug.LogWarning($"{nameof(MainMenuBootstrap)} could not find menu entry '{entryName}' to leave unchanged.");
            return false;
        }

        Debug.Log($"{nameof(MainMenuBootstrap)} left '{entryName}' unchanged.");
        return true;
    }

    private static void UpdateMenuEntryLabels(GameObject entryRoot, string label)
    {
        if (entryRoot == null || string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        LocalizeTMPro[] localizers = entryRoot.GetComponentsInChildren<LocalizeTMPro>(true);
        for (int index = 0; index < localizers.Length; index++)
        {
            LocalizeTMPro localizer = localizers[index];
            if (localizer == null)
            {
                continue;
            }

            localizer._text_ENG = label;
            localizer._text_JPN = label;
        }

        TMP_Text[] tmpLabels = entryRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int index = 0; index < tmpLabels.Length; index++)
        {
            TMP_Text tmpLabel = tmpLabels[index];
            if (tmpLabel != null)
            {
                tmpLabel.text = label;
            }
        }

        Text[] uiLabels = entryRoot.GetComponentsInChildren<Text>(true);
        for (int index = 0; index < uiLabels.Length; index++)
        {
            Text uiLabel = uiLabels[index];
            if (uiLabel != null)
            {
                uiLabel.text = label;
            }
        }
    }

    private static void ClearLegacyMenuInteractions(GameObject entryRoot)
    {
        if (entryRoot == null)
        {
            return;
        }

        RemovePointerClickTriggers(entryRoot);

        Button[] buttons = entryRoot.GetComponentsInChildren<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = buttons[index];
            if (button == null || button.gameObject.name == CustomClickCatcherName)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();
        }
    }

    private static Button EnsureMenuClickCatcher(Transform entryRoot)
    {
        if (entryRoot == null)
        {
            return null;
        }

        Transform existingTransform = FindChildRecursive(entryRoot, CustomClickCatcherName);
        GameObject clickCatcherObject = existingTransform != null ? existingTransform.gameObject : null;
        if (clickCatcherObject == null)
        {
            clickCatcherObject = new GameObject(CustomClickCatcherName, typeof(RectTransform), typeof(Image), typeof(Button));
        }

        RectTransform clickCatcherRect = clickCatcherObject.GetComponent<RectTransform>();
        if (clickCatcherRect == null)
        {
            return null;
        }

        clickCatcherRect.SetParent(entryRoot, false);
        clickCatcherRect.anchorMin = Vector2.zero;
        clickCatcherRect.anchorMax = Vector2.one;
        clickCatcherRect.offsetMin = Vector2.zero;
        clickCatcherRect.offsetMax = Vector2.zero;
        clickCatcherRect.SetAsLastSibling();

        Image clickCatcherImage = clickCatcherObject.GetComponent<Image>();
        if (clickCatcherImage == null)
        {
            clickCatcherImage = clickCatcherObject.AddComponent<Image>();
        }

        clickCatcherImage.color = new Color(1f, 1f, 1f, 0f);
        clickCatcherImage.raycastTarget = true;

        Button clickCatcherButton = clickCatcherObject.GetComponent<Button>();
        if (clickCatcherButton == null)
        {
            clickCatcherButton = clickCatcherObject.AddComponent<Button>();
        }

        clickCatcherButton.targetGraphic = clickCatcherImage;
        clickCatcherButton.transition = Selectable.Transition.None;
        Navigation navigation = clickCatcherButton.navigation;
        navigation.mode = Navigation.Mode.None;
        clickCatcherButton.navigation = navigation;
        clickCatcherObject.SetActive(true);
        return clickCatcherButton;
    }

    private static string GetButtonLabel(Button button, out Text uiText, out TMP_Text tmpText)
    {
        uiText = null;
        tmpText = null;

        if (button == null)
        {
            return null;
        }

        TMP_Text[] tmpLabels = button.GetComponentsInChildren<TMP_Text>(true);
        for (int index = 0; index < tmpLabels.Length; index++)
        {
            TMP_Text candidate = tmpLabels[index];
            if (candidate != null && !string.IsNullOrWhiteSpace(candidate.text) && candidate.gameObject.activeInHierarchy)
            {
                tmpText = candidate;
                return candidate.text;
            }
        }

        for (int index = 0; index < tmpLabels.Length; index++)
        {
            TMP_Text candidate = tmpLabels[index];
            if (candidate != null && !string.IsNullOrWhiteSpace(candidate.text))
            {
                tmpText = candidate;
                return candidate.text;
            }
        }

        Text[] uiLabels = button.GetComponentsInChildren<Text>(true);
        for (int index = 0; index < uiLabels.Length; index++)
        {
            Text candidate = uiLabels[index];
            if (candidate != null && !string.IsNullOrWhiteSpace(candidate.text) && candidate.gameObject.activeInHierarchy)
            {
                uiText = candidate;
                return candidate.text;
            }
        }

        for (int index = 0; index < uiLabels.Length; index++)
        {
            Text candidate = uiLabels[index];
            if (candidate != null && !string.IsNullOrWhiteSpace(candidate.text))
            {
                uiText = candidate;
                return candidate.text;
            }
        }

        return null;
    }

    private static void SetButtonLabel(Text uiText, TMP_Text tmpText, string label)
    {
        if (tmpText != null)
        {
            tmpText.text = label;
        }

        if (uiText != null)
        {
            uiText.text = label;
        }
    }

    private static void ConfigureButtonAction(Button button, UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.gameObject.SetActive(true);
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

            nestedButton.onClick.RemoveAllListeners();
        }
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

}
