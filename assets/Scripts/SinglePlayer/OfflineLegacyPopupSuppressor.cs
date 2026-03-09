using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class OfflineLegacyPopupSuppressor
{
    private const string OpeningSceneName = "Opening";
    private static readonly HashSet<int> ProcessedSceneHandles = new HashSet<int>();
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
        ProcessedSceneHandles.Clear();
        _subscribed = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || scene.name != OpeningSceneName)
        {
            return;
        }

        if (IsAuthoredOfflineMenu(scene))
        {
            return;
        }

        if (!ProcessedSceneHandles.Add(scene.handle))
        {
            return;
        }

        SuppressLegacyPopups(scene);
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        ProcessedSceneHandles.Remove(scene.handle);
    }

    private static void SuppressLegacyPopups(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        HashSet<int> hiddenRoots = new HashSet<int>();
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int index = 0; index < rootObjects.Length; index++)
        {
            GameObject rootObject = rootObjects[index];
            if (rootObject == null)
            {
                continue;
            }

            SuppressFromUnityText(rootObject.transform, hiddenRoots);
            SuppressFromTmpText(rootObject.transform, hiddenRoots);
        }
    }

    private static void SuppressFromUnityText(Transform root, HashSet<int> hiddenRoots)
    {
        if (root == null)
        {
            return;
        }

        Text[] texts = root.GetComponentsInChildren<Text>(true);
        for (int index = 0; index < texts.Length; index++)
        {
            Text text = texts[index];
            if (text == null)
            {
                continue;
            }

            TryHideModalRoot(text.transform, text.text, hiddenRoots);
        }
    }

    private static void SuppressFromTmpText(Transform root, HashSet<int> hiddenRoots)
    {
        if (root == null)
        {
            return;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int index = 0; index < texts.Length; index++)
        {
            TMP_Text text = texts[index];
            if (text == null)
            {
                continue;
            }

            TryHideModalRoot(text.transform, text.text, hiddenRoots);
        }
    }

    private static void TryHideModalRoot(Transform sourceTransform, string textValue, HashSet<int> hiddenRoots)
    {
        if (sourceTransform == null || string.IsNullOrWhiteSpace(textValue) || hiddenRoots == null)
        {
            return;
        }

        if (!ContainsKeyword(textValue))
        {
            return;
        }

        GameObject modalRoot = FindLikelyModalRoot(sourceTransform);
        if (modalRoot == null)
        {
            return;
        }

        int instanceId = modalRoot.GetInstanceID();
        if (!hiddenRoots.Add(instanceId))
        {
            return;
        }

        if (!modalRoot.activeSelf)
        {
            return;
        }

        modalRoot.SetActive(false);
        Debug.Log($"{nameof(OfflineLegacyPopupSuppressor)} hid '{GetHierarchyPath(modalRoot.transform)}' because it contained '{textValue}'.");
    }

    private static bool ContainsKeyword(string textValue)
    {
        if (string.IsNullOrWhiteSpace(textValue))
        {
            return false;
        }

        if (textValue.Contains("サーバー") || textValue.Contains("地域"))
        {
            return true;
        }

        string normalized = textValue.ToUpperInvariant();
        return normalized.Contains("REGION") || normalized.Contains("SERVER");
    }

    private static GameObject FindLikelyModalRoot(Transform sourceTransform)
    {
        if (sourceTransform == null)
        {
            return null;
        }

        Transform fallback = null;

        for (Transform current = sourceTransform; current != null; current = current.parent)
        {
            if (current.GetComponent<ServerRegionPanel>() != null)
            {
                return current.gameObject;
            }

            string currentName = current.name ?? string.Empty;
            string normalizedName = currentName.ToUpperInvariant();
            bool looksLikeModal = normalizedName.Contains("PANEL")
                || normalizedName.Contains("POPUP")
                || normalizedName.Contains("WINDOW")
                || normalizedName.Contains("DIALOG")
                || normalizedName.Contains("MODAL");

            bool looksLikeRegionUi = normalizedName.Contains("SERVER") || normalizedName.Contains("REGION");
            if (looksLikeModal && looksLikeRegionUi)
            {
                return current.gameObject;
            }

            if (fallback == null && looksLikeModal)
            {
                fallback = current;
            }

            if (current.GetComponent<Canvas>() != null)
            {
                break;
            }
        }

        if (fallback != null)
        {
            return fallback.gameObject;
        }

        if (sourceTransform.parent != null && sourceTransform.parent.GetComponent<Canvas>() == null)
        {
            return sourceTransform.parent.gameObject;
        }

        return sourceTransform.gameObject;
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        List<string> names = new List<string>();
        for (Transform current = target; current != null; current = current.parent)
        {
            names.Add(current.name);
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static bool IsAuthoredOfflineMenu(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int index = 0; index < rootObjects.Length; index++)
        {
            GameObject rootObject = rootObjects[index];
            if (rootObject == null)
            {
                continue;
            }

            if (rootObject.GetComponentInChildren<OpeningOfflineMenuMarker>(true) != null)
            {
                return true;
            }
        }

        return false;
    }
}
