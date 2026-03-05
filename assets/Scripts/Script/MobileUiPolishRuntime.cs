using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MobileUiPolishRuntime : MonoBehaviour
{
    static bool _bootstrapped;
    static Shader _tmpFallbackShader;
    static readonly Color32 DeckBasePanelColor = new Color32(15, 23, 37, 204);
    static readonly Color32 DeckElevatedPanelColor = new Color32(26, 42, 64, 230);
    static readonly Color32 DeckControlIdleColor = new Color32(30, 49, 74, 238);
    static readonly Color32 DeckControlPressedColor = new Color32(22, 38, 58, 255);
    static readonly Color32 DeckTextColor = new Color32(242, 246, 255, 255);

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
        ApplyDeckUiThemeIPhone();
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

    static void ApplyDeckUiThemeIPhone()
    {
        if (Application.platform != RuntimePlatform.IPhonePlayer)
        {
            return;
        }

        List<Transform> deckRoots = GetDeckUiRoots();
        for (int i = 0; i < deckRoots.Count; i++)
        {
            Transform root = deckRoots[i];
            if (root == null)
            {
                continue;
            }

            StylePanelImages(root);
            StyleSelectableControls(root);
            StyleTextElements(root);
        }
    }

    static List<Transform> GetDeckUiRoots()
    {
        List<Transform> roots = new List<Transform>();
        HashSet<int> seenRoots = new HashSet<int>();

        if (Opening.instance != null && Opening.instance.deck != null)
        {
            if (Opening.instance.deck.selectDeck != null)
            {
                AddRoot(roots, seenRoots, Opening.instance.deck.selectDeck.SelectDeckObject);
            }

            if (Opening.instance.deck.editDeck != null)
            {
                AddRoot(roots, seenRoots, Opening.instance.deck.editDeck.CreateDeckObject);
            }

            if (Opening.instance.deck.deckListPanel != null)
            {
                AddRoot(roots, seenRoots, Opening.instance.deck.deckListPanel.DeckListPanelObject);
            }

            if (Opening.instance.deck.trialDraw != null)
            {
                AddRoot(roots, seenRoots, Opening.instance.deck.trialDraw.gameObject);
            }
        }

        AddRoot(roots, seenRoots, GameObject.Find("Search_Filter"));

        return roots;
    }

    static void AddRoot(List<Transform> roots, HashSet<int> seenRoots, GameObject candidate)
    {
        if (candidate == null)
        {
            return;
        }

        int id = candidate.GetInstanceID();
        if (seenRoots.Contains(id))
        {
            return;
        }

        seenRoots.Add(id);
        roots.Add(candidate.transform);
    }

    static void StylePanelImages(Transform root)
    {
        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (!ShouldStylePanelImage(image))
            {
                continue;
            }

            string objectName = image.gameObject.name.ToLowerInvariant();
            image.material = null;
            image.color = ResolvePanelColor(objectName);
        }
    }

    static void StyleSelectableControls(Transform root)
    {
        Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++)
        {
            Selectable selectable = selectables[i];
            if (selectable == null)
            {
                continue;
            }

            ColorBlock colors = selectable.colors;
            colors.normalColor = DeckControlIdleColor;
            colors.highlightedColor = DeckElevatedPanelColor;
            colors.pressedColor = DeckControlPressedColor;
            colors.selectedColor = DeckElevatedPanelColor;
            colors.disabledColor = new Color32(30, 49, 74, 140);
            selectable.colors = colors;

            if (selectable.targetGraphic is Image targetImage && ShouldStyleControlGraphic(targetImage))
            {
                targetImage.color = DeckControlIdleColor;
            }

            if (selectable is Dropdown dropdown)
            {
                StyleDropdown(dropdown);
            }
            else if (selectable is InputField inputField)
            {
                StyleInputField(inputField);
            }
            else if (selectable is Toggle toggle)
            {
                StyleToggle(toggle);
            }
        }
    }

    static void StyleDropdown(Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        if (dropdown.captionText != null && ShouldUseDeckTextColor(dropdown.captionText.color))
        {
            dropdown.captionText.color = DeckTextColor;
        }

        if (dropdown.itemText != null && ShouldUseDeckTextColor(dropdown.itemText.color))
        {
            dropdown.itemText.color = DeckTextColor;
        }

        if (dropdown.template != null)
        {
            Image[] templateImages = dropdown.template.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < templateImages.Length; i++)
            {
                Image image = templateImages[i];
                if (image == null || image == dropdown.itemImage || image == dropdown.captionImage || IsProtectedVisual(image))
                {
                    continue;
                }

                string objectName = image.gameObject.name.ToLowerInvariant();
                image.color = objectName.Contains("item")
                    ? DeckControlPressedColor
                    : DeckElevatedPanelColor;
            }

            Text[] texts = dropdown.template.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text != null && ShouldUseDeckTextColor(text.color))
                {
                    text.color = DeckTextColor;
                }
            }

            TMP_Text[] tmpTexts = dropdown.template.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                TMP_Text tmpText = tmpTexts[i];
                if (tmpText != null && ShouldUseDeckTextColor(tmpText.color))
                {
                    tmpText.color = DeckTextColor;
                }
            }
        }
    }

    static void StyleInputField(InputField inputField)
    {
        if (inputField == null)
        {
            return;
        }

        if (inputField.targetGraphic is Image targetImage && ShouldStyleControlGraphic(targetImage))
        {
            targetImage.color = DeckControlIdleColor;
        }

        if (inputField.textComponent != null && ShouldUseDeckTextColor(inputField.textComponent.color))
        {
            inputField.textComponent.color = DeckTextColor;
        }

        if (inputField.placeholder is Text placeholder)
        {
            if (ShouldUseDeckTextColor(placeholder.color))
            {
                placeholder.color = new Color32(242, 246, 255, 190);
            }
        }

        if (inputField.placeholder is TMP_Text tmpPlaceholder)
        {
            if (ShouldUseDeckTextColor(tmpPlaceholder.color))
            {
                tmpPlaceholder.color = new Color32(242, 246, 255, 190);
            }
        }
    }

    static void StyleToggle(Toggle toggle)
    {
        if (toggle == null)
        {
            return;
        }

        if (toggle.targetGraphic is Image targetImage && ShouldStyleControlGraphic(targetImage))
        {
            targetImage.color = DeckControlIdleColor;
        }

        if (toggle.graphic != null && ShouldUseDeckTextColor(toggle.graphic.color) && !IsProtectedVisualName(toggle.graphic.gameObject.name.ToLowerInvariant()))
        {
            toggle.graphic.color = DeckTextColor;
        }
    }

    static void StyleTextElements(Transform root)
    {
        Text[] texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null || !ShouldUseDeckTextColor(text.color))
            {
                continue;
            }

            text.color = DeckTextColor;
        }

        TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            TMP_Text tmpText = tmpTexts[i];
            if (tmpText == null || !ShouldUseDeckTextColor(tmpText.color))
            {
                continue;
            }

            tmpText.color = DeckTextColor;
        }
    }

    static bool ShouldStylePanelImage(Image image)
    {
        if (image == null || image.color.a <= 0.02f || IsProtectedVisual(image))
        {
            return false;
        }

        string objectName = image.gameObject.name.ToLowerInvariant();
        return IsLikelyTextPanel(objectName) || IsNearWhite(image.color);
    }

    static bool ShouldStyleControlGraphic(Image image)
    {
        return image != null && !IsProtectedVisual(image) && image.color.a > 0.02f;
    }

    static bool IsProtectedVisual(Image image)
    {
        if (image == null)
        {
            return true;
        }

        if (image.GetComponentInParent<CardPrefab_CreateDeck>(true) != null)
        {
            return true;
        }

        return IsProtectedVisualName(image.gameObject.name.ToLowerInvariant());
    }

    static bool IsProtectedVisualName(string objectName)
    {
        return objectName.Contains("card") ||
               objectName.Contains("icon") ||
               objectName.Contains("art") ||
               objectName.Contains("mask") ||
               objectName.Contains("avatar");
    }

    static Color32 ResolvePanelColor(string objectName)
    {
        if (objectName.Contains("dropdown") ||
            objectName.Contains("toggle") ||
            objectName.Contains("button") ||
            objectName.Contains("input"))
        {
            return DeckControlIdleColor;
        }

        if (objectName.Contains("title") ||
            objectName.Contains("line") ||
            objectName.Contains("header") ||
            objectName.Contains("bar"))
        {
            return DeckElevatedPanelColor;
        }

        return DeckBasePanelColor;
    }

    static bool ShouldUseDeckTextColor(Color color)
    {
        if (color.a <= 0.02f)
        {
            return false;
        }

        // Preserve intentionally-color-coded text (for example red/green deck counts).
        bool isNeutral = Mathf.Abs(color.r - color.g) < 0.08f &&
                         Mathf.Abs(color.g - color.b) < 0.08f;
        return isNeutral;
    }

    static bool IsNearWhite(Color color)
    {
        return color.r >= 0.75f && color.g >= 0.75f && color.b >= 0.75f;
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
