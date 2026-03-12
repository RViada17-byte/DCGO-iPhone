#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(10000)]
public sealed class RuntimeMissingVisualLogger : MonoBehaviour
{
    private const float ScanIntervalSeconds = 3f;

    private static bool _bootstrapped;
    private static RuntimeMissingVisualLogger _instance;

    private readonly HashSet<string> _loggedIssues = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_bootstrapped)
        {
            return;
        }

        _bootstrapped = true;
        var gameObject = new GameObject(nameof(RuntimeMissingVisualLogger))
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<RuntimeMissingVisualLogger>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(ScanAfterDelay("startup"));
        StartCoroutine(PeriodicScan());
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
            _bootstrapped = false;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ScanAfterDelay($"scene_loaded:{scene.name}:{mode}"));
    }

    private IEnumerator ScanAfterDelay(string reason)
    {
        yield return null;
        Scan(reason);
        yield return null;
        Scan(reason + ":late");
    }

    private IEnumerator PeriodicScan()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(ScanIntervalSeconds);
            Scan("periodic");
        }
    }

    private void Scan(string reason)
    {
        ScanImages(reason);
        ScanRawImages(reason);
        ScanButtons(reason);
        ScanTmpTexts(reason);
        ScanSpriteRenderers(reason);
        ScanRendererSet(Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), reason);
        ScanRendererSet(Object.FindObjectsByType<TrailRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), reason);
        ScanRendererSet(Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), reason);
        ScanAnimators(reason);
    }

    private void ScanImages(string reason)
    {
        Image[] images = Object.FindObjectsByType<Image>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || !image.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (image.sprite == null)
            {
                LogIssue(reason, image, "m_Sprite", "Sprite", "Image sprite is null.");
            }
            else if (image.sprite.texture == null)
            {
                LogIssue(reason, image, "m_Sprite.texture", "Texture", "Image sprite texture is null.");
            }

            AuditMaterial(reason, image, "material", image.material);
        }
    }

    private void ScanRawImages(string reason)
    {
        RawImage[] rawImages = Object.FindObjectsByType<RawImage>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < rawImages.Length; i++)
        {
            RawImage rawImage = rawImages[i];
            if (rawImage == null || !rawImage.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (rawImage.texture == null)
            {
                LogIssue(reason, rawImage, "m_Texture", "Texture", "RawImage texture is null.");
            }

            AuditMaterial(reason, rawImage, "material", rawImage.material);
        }
    }

    private void ScanButtons(string reason)
    {
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !button.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (button.targetGraphic == null)
            {
                LogIssue(reason, button, "m_TargetGraphic", "Graphic", "Button targetGraphic is null.");
            }
        }
    }

    private void ScanTmpTexts(string reason)
    {
        TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || !text.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (text.font == null)
            {
                LogIssue(reason, text, "m_fontAsset", "TMP_FontAsset", "TMP text font asset is null.");
            }

            if (text.fontSharedMaterial == null)
            {
                LogIssue(reason, text, "fontSharedMaterial", "Material", "TMP text shared material is null.");
            }
            else
            {
                AuditMaterial(reason, text, "fontSharedMaterial", text.fontSharedMaterial);
            }
        }
    }

    private void ScanSpriteRenderers(string reason)
    {
        SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null || !spriteRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (spriteRenderer.sprite == null)
            {
                LogIssue(reason, spriteRenderer, "m_Sprite", "Sprite", "SpriteRenderer sprite is null.");
            }
            else if (spriteRenderer.sprite.texture == null)
            {
                LogIssue(reason, spriteRenderer, "m_Sprite.texture", "Texture", "SpriteRenderer sprite texture is null.");
            }

            ScanRendererMaterials(reason, spriteRenderer);
        }
    }

    private void ScanRendererSet<T>(T[] renderers, string reason) where T : Renderer
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            T renderer = renderers[i];
            if (renderer == null || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            ScanRendererMaterials(reason, renderer);
        }
    }

    private void ScanRendererMaterials(string reason, Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            LogIssue(reason, renderer, "m_Materials", "Material", "Renderer has no shared materials.");
            return;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            AuditMaterial(reason, renderer, $"m_Materials[{i}]", materials[i]);
        }
    }

    private void ScanAnimators(string reason)
    {
        Animator[] animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || !animator.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (animator.runtimeAnimatorController == null)
            {
                LogIssue(reason, animator, "m_Controller", "RuntimeAnimatorController", "Animator controller is null.");
            }
        }
    }

    private void AuditMaterial(string reason, Component component, string propertyName, Material material)
    {
        if (material == null)
        {
            LogIssue(reason, component, propertyName, "Material", "Material reference is null.");
            return;
        }

        if (material.shader == null)
        {
            LogIssue(reason, component, propertyName, "Shader", $"Material '{material.name}' has no shader.");
            return;
        }

        if (!material.shader.isSupported)
        {
            LogIssue(reason, component, propertyName, "Shader", $"Shader '{material.shader.name}' is unsupported.");
        }
    }

    private void LogIssue(string reason, Component component, string propertyName, string assetType, string details)
    {
        if (component == null || component.gameObject.scene.name == null)
        {
            return;
        }

        string sceneName = component.gameObject.scene.name;
        string hierarchyPath = GetHierarchyPath(component.transform);
        string issueKey = $"{sceneName}|{hierarchyPath}|{component.GetType().Name}|{propertyName}|{details}";
        if (!_loggedIssues.Add(issueKey))
        {
            return;
        }

        Debug.LogWarning(
            $"[RuntimeMissingVisualLogger] reason={reason} scene={sceneName} path={hierarchyPath} component={component.GetType().Name} property={propertyName} assetType={assetType} size={GetSize(component)} prefabRoot={GetNearestPrefabRoot(component.gameObject)} details={details}",
            component);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        var names = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static string GetSize(Component component)
    {
        if (component == null)
        {
            return string.Empty;
        }

        if (component.transform is RectTransform rectTransform)
        {
            Vector2 size = rectTransform.rect.size;
            return $"{size.x:0.##}x{size.y:0.##}";
        }

        if (component is Renderer renderer)
        {
            Vector3 size = renderer.bounds.size;
            return $"{size.x:0.##}x{size.y:0.##}x{size.z:0.##}";
        }

        Vector3 scale = component.transform.lossyScale;
        return $"{scale.x:0.##}x{scale.y:0.##}x{scale.z:0.##}";
    }

    private static string GetNearestPrefabRoot(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return string.Empty;
        }

#if UNITY_EDITOR
        GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
        if (prefabRoot != null)
        {
            return GetHierarchyPath(prefabRoot.transform);
        }
#endif

        return gameObject.transform.root != null ? GetHierarchyPath(gameObject.transform.root) : string.Empty;
    }
}
#endif
