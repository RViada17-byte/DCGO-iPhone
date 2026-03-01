using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MobileUiPolishRuntime : MonoBehaviour
{
    static bool _bootstrapped;
    static Shader _tmpFallbackShader;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!Application.isMobilePlatform || _bootstrapped)
        {
            return;
        }

        _bootstrapped = true;
        FixTmpShaders();
        GameObject runtimeObject = new GameObject(nameof(MobileUiPolishRuntime));
        DontDestroyOnLoad(runtimeObject);
        runtimeObject.hideFlags = HideFlags.HideAndDontSave;
        runtimeObject.AddComponent<MobileUiPolishRuntime>();
    }

    void Awake()
    {
        ApplyLandscapeLock();
        ApplyUiPolish();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        ApplyLandscapeLock();
        StartCoroutine(ApplyUiPolishDelayed());
    }

    IEnumerator ApplyUiPolishDelayed()
    {
        // UI objects are spawned across several frames in battle scenes.
        yield return null;
        ApplyUiPolish();
        yield return new WaitForSeconds(0.5f);
        ApplyUiPolish();
    }

    static void ApplyLandscapeLock()
    {
        if (!Application.isMobilePlatform)
        {
            return;
        }

        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        if (Screen.orientation == ScreenOrientation.Portrait || Screen.orientation == ScreenOrientation.PortraitUpsideDown)
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        Screen.orientation = ScreenOrientation.AutoRotation;
    }

    static void ApplyUiPolish()
    {
        FixTmpShaders();
        ImproveTextReadability();
        ImproveTmpTextReadability();
        // Disabled for now due over-broad panel darkening on some iOS layouts.
    }

    static void FixTmpShaders()
    {
        if (!TryResolveTmpFallbackShader())
        {
            return;
        }

        Material[] allMaterials = Resources.FindObjectsOfTypeAll<Material>();
        for (int i = 0; i < allMaterials.Length; i++)
        {
            ReplaceProblematicTmpShader(allMaterials[i]);
        }

        TMP_Text[] tmpTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            TMP_Text tmp = tmpTexts[i];
            if (tmp == null)
            {
                continue;
            }

            ReplaceProblematicTmpShader(tmp.fontSharedMaterial);
            ReplaceProblematicTmpShader(tmp.fontMaterial);
            tmp.UpdateMeshPadding();
        }

        TMP_SubMeshUI[] subMeshes = Object.FindObjectsByType<TMP_SubMeshUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < subMeshes.Length; i++)
        {
            TMP_SubMeshUI subMesh = subMeshes[i];
            if (subMesh == null)
            {
                continue;
            }

            ReplaceProblematicTmpShader(subMesh.sharedMaterial);
        }

        TMP_SubMesh[] worldSubMeshes = Object.FindObjectsByType<TMP_SubMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < worldSubMeshes.Length; i++)
        {
            TMP_SubMesh subMesh = worldSubMeshes[i];
            if (subMesh == null)
            {
                continue;
            }

            ReplaceProblematicTmpShader(subMesh.sharedMaterial);
        }
    }

    static bool TryResolveTmpFallbackShader()
    {
        _tmpFallbackShader ??= Shader.Find("TextMeshPro/Distance Field");
        if (_tmpFallbackShader == null)
        {
            _tmpFallbackShader = Shader.Find("TextMeshPro/Distance Field Overlay");
        }

        if (_tmpFallbackShader == null)
        {
            _tmpFallbackShader = Shader.Find("TextMeshPro/Mobile/Distance Field SSD");
        }

        return _tmpFallbackShader != null;
    }

    static void ReplaceProblematicTmpShader(Material material)
    {
        if (material == null || material.shader == null || material.shader == _tmpFallbackShader)
        {
            return;
        }

        if (!IsProblematicTmpShader(material.shader.name))
        {
            return;
        }

        material.shader = _tmpFallbackShader;
    }

    static bool IsProblematicTmpShader(string shaderName)
    {
        if (string.IsNullOrEmpty(shaderName))
        {
            return false;
        }

        return shaderName.Contains("TextMeshPro/Mobile/Distance Field") ||
               shaderName.Contains("TextMeshPro/Bitmap");
    }

    static void ImproveTextReadability()
    {
        Text[] legacyTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < legacyTexts.Length; i++)
        {
            Text text = legacyTexts[i];
            if (text == null)
            {
                continue;
            }

            Color textColor = text.color;
            textColor.a = 1f;
            text.color = textColor;

            Outline outline = text.GetComponent<Outline>();
            if (outline == null)
            {
                outline = text.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color32(0, 0, 0, 230);
            outline.effectDistance = new Vector2(1.25f, -1.25f);
        }
    }

    static void ImproveTmpTextReadability()
    {
        TMP_Text[] tmpTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            TMP_Text tmp = tmpTexts[i];
            if (tmp == null)
            {
                continue;
            }

            Color color = tmp.color;
            color.a = 1f;
            tmp.color = color;

            // Add a subtle outline so white text remains readable on mixed backgrounds.
            if (tmp.outlineWidth < 0.12f)
            {
                tmp.outlineWidth = 0.12f;
            }

            tmp.outlineColor = new Color32(0, 0, 0, 230);
        }
    }

    static void DarkenUnreadableUiPanels()
    {
        Image[] images = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            string objectName = image.gameObject.name.ToLowerInvariant();
            if (!IsLikelyTextPanel(objectName))
            {
                continue;
            }

            if (IsCardArtImage(objectName))
            {
                continue;
            }

            image.material = null;

            Color color = image.color;
            if (color.a < 0.08f)
            {
                continue;
            }

            image.color = new Color(0f, 0f, 0f, Mathf.Clamp(color.a * 0.92f, 0.28f, 0.72f));
        }
    }

    static bool IsLikelyTextPanel(string objectName)
    {
        return objectName.Contains("effect") ||
               objectName.Contains("discription") ||
               objectName.Contains("description") ||
               objectName.Contains("command") ||
               objectName.Contains("message") ||
               objectName.Contains("popup") ||
               objectName.Contains("window") ||
               objectName.Contains("background") ||
               objectName.Contains("showcard") ||
               objectName.Contains("showhand") ||
               objectName.Contains("panel") ||
               objectName.Contains("frame");
    }

    static bool IsCardArtImage(string objectName)
    {
        return objectName.Contains("cardimage") ||
               objectName.Contains("card_art") ||
               objectName == "card" ||
               objectName.Contains("icon");
    }

    static void ToneDownWashedPanels()
    {
        Image[] images = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            Color color = image.color;
            if (color.a < 0.05f || color.a > 0.86f)
            {
                continue;
            }

            if (color.r < 0.78f || color.g < 0.78f || color.b < 0.78f)
            {
                continue;
            }

            string objectName = image.gameObject.name.ToLowerInvariant();
            if (objectName.Contains("card") ||
                objectName.Contains("icon") ||
                objectName.Contains("life") ||
                objectName.Contains("security") ||
                objectName.Contains("avatar") ||
                objectName.Contains("art"))
            {
                continue;
            }

            image.color = new Color(0f, 0f, 0f, Mathf.Clamp(color.a * 0.9f, 0.2f, 0.48f));
        }
    }
}
