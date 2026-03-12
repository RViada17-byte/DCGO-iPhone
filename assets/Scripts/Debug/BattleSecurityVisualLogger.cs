#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class BattleSecurityVisualLogger : MonoBehaviour
{
    const string BattleSceneName = "BattleScene";
    static bool _installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (_installed)
        {
            return;
        }

        GameObject root = new GameObject(nameof(BattleSecurityVisualLogger));
        DontDestroyOnLoad(root);
        root.hideFlags = HideFlags.HideAndDontSave;
        root.AddComponent<BattleSecurityVisualLogger>();
        _installed = true;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(LogBattleSecurityImagesDelayed("InitialLoad"));
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _installed = false;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (scene.name == BattleSceneName)
        {
            StartCoroutine(LogBattleSecurityImagesDelayed("SceneLoaded"));
        }
    }

    IEnumerator LogBattleSecurityImagesDelayed(string context)
    {
        if (!IsBattleSceneActive())
        {
            yield break;
        }

        yield return null;
        LogBattleSecurityImages($"{context}/+1f");

        yield return new WaitForSeconds(0.5f);
        LogBattleSecurityImages($"{context}/+0.5s");
    }

    public static void LogBattleSecurityImages(string context)
    {
        if (!IsBattleSceneActive())
        {
            return;
        }

        Image[] images = UnityEngine.Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (!ShouldLog(image))
            {
                continue;
            }

            LogSpecificImage(image, context);
        }
    }

    public static void LogSpecificImage(Image image, string context)
    {
        if (image == null)
        {
            return;
        }

        Debug.Log($"[BattleSecurityVisualLogger] context={context} {DescribeImage(image)}");
    }

    public static void LogSecurityBreakPath(Player player, SecurityBreakGlass securityBreakGlass, GameObject impactPrefab)
    {
        if (player == null || player.securityObject == null)
        {
            return;
        }

        Image securityIcon = FindSecurityIconImage(player.securityObject);
        string iconDescription = securityIcon != null ? DescribeImage(securityIcon) : "icon=<missing>";
        string glassPath = securityBreakGlass != null ? GetHierarchyPath(securityBreakGlass.transform) : "<missing>";
        string glassPrefab = securityBreakGlass != null ? GetPrefabSourcePath(securityBreakGlass.gameObject) : "<missing>";
        string impactPrefabPath = GetPrefabSourcePath(impactPrefab);

        Debug.Log(
            $"[BattleSecurityFx] player={player.PlayerName} isYou={player.isYou} " +
            $"securityObject={GetHierarchyPath(player.securityObject.transform)} " +
            $"glass={glassPath} glassPrefab={glassPrefab} impactPrefab={impactPrefabPath} {iconDescription}");
    }

    static Image FindSecurityIconImage(SecurityObject securityObject)
    {
        Image[] images = securityObject.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            if (image.gameObject.name.IndexOf("SecurityIcon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        return null;
    }

    static bool ShouldLog(Image image)
    {
        if (image == null)
        {
            return false;
        }

        string objectName = image.gameObject.name;
        string spriteName = image.sprite != null ? image.sprite.name : string.Empty;

        return objectName.IndexOf("Security", StringComparison.OrdinalIgnoreCase) >= 0 ||
               spriteName.IndexOf("Security", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsBattleSceneActive()
    {
        Scene scene = SceneManager.GetActiveScene();
        return scene.IsValid() && scene.name == BattleSceneName;
    }

    static string DescribeImage(Image image)
    {
        RectTransform rectTransform = image.rectTransform;
        CanvasGroup[] canvasGroups = image.GetComponentsInParent<CanvasGroup>(true);

        float canvasGroupAlpha = 1f;
        for (int i = 0; i < canvasGroups.Length; i++)
        {
            canvasGroupAlpha *= canvasGroups[i].alpha;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append($"path={GetHierarchyPath(image.transform)}");
        builder.Append($" sprite={(image.sprite != null ? image.sprite.name : "<null>")}");
        builder.Append($" activeSelf={image.gameObject.activeSelf}");
        builder.Append($" activeInHierarchy={image.gameObject.activeInHierarchy}");
        builder.Append($" enabled={image.enabled}");
        builder.Append($" colorA={image.color.a:0.###}");
        builder.Append($" canvasGroupA={canvasGroupAlpha:0.###}");
        builder.Append($" sibling={rectTransform.GetSiblingIndex()}");
        builder.Append($" anchored={rectTransform.anchoredPosition}");
        builder.Append($" size={rectTransform.rect.size}");
        builder.Append($" scale={rectTransform.lossyScale}");
        builder.Append($" mask={GetNearestMaskPath(image.transform)}");
        builder.Append($" prefabRoot={GetPrefabSourcePath(image.gameObject)}");
        return builder.ToString();
    }

    static string GetNearestMaskPath(Transform transform)
    {
        Mask mask = transform.GetComponentInParent<Mask>(true);
        if (mask != null)
        {
            return GetHierarchyPath(mask.transform);
        }

        RectMask2D rectMask = transform.GetComponentInParent<RectMask2D>(true);
        if (rectMask != null)
        {
            return GetHierarchyPath(rectMask.transform);
        }

        return "<none>";
    }

    static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = $"{transform.name}/{path}";
        }

        return path;
    }

    static string GetPrefabSourcePath(GameObject instance)
    {
        if (instance == null)
        {
            return "<null>";
        }

#if UNITY_EDITOR
        UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        if (source == null)
        {
            return "<scene>";
        }

        string assetPath = AssetDatabase.GetAssetPath(source);
        return string.IsNullOrEmpty(assetPath) ? source.name : assetPath;
#else
        return "<runtime>";
#endif
    }
}
#endif
