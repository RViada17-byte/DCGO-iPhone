#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public sealed class VisualReferenceAuditEntry
{
    public string sourceAssetPath;
    public string sourceKind;
    public string sceneName;
    public string hierarchyPath;
    public string nearestPrefabRoot;
    public string componentType;
    public string propertyName;
    public string issueType;
    public string assetType;
    public string assetPath;
    public string shaderName;
    public string size;
    public string details;
}

[Serializable]
public sealed class VisualReferenceAuditReport
{
    public string generatedAtUtc;
    public string unityVersion;
    public int entryCount;
    public List<VisualReferenceAuditEntry> entries = new List<VisualReferenceAuditEntry>();
}

public static class VisualReferenceAudit
{
    private static readonly string[] CsvHeaders =
    {
        "sourceAssetPath",
        "sourceKind",
        "sceneName",
        "hierarchyPath",
        "nearestPrefabRoot",
        "componentType",
        "propertyName",
        "issueType",
        "assetType",
        "assetPath",
        "shaderName",
        "size",
        "details",
    };

    [MenuItem("Build/DCGO/Audit/Run Visual Reference Audit")]
    public static void RunMenu()
    {
        if (!PrepareInteractiveRun())
        {
            return;
        }

        RunInternal(captureCurrentSceneSetup: true);
    }

    [MenuItem("Build/DCGO/Audit/Run Visual Reference Audit (No Prompt)")]
    public static void RunMenuNoPrompt()
    {
        RunInternal(captureCurrentSceneSetup: false);
    }

    [MenuItem("Build/DCGO/Audit/Run Full Visual Audit")]
    public static void RunFullMenu()
    {
        if (!PrepareInteractiveRun())
        {
            return;
        }

        RunInternal(captureCurrentSceneSetup: true);
        YamlReferenceAudit.RunInternal(captureCurrentSceneSetup: false);
        AnimationVfxAudit.RunInternal(captureCurrentSceneSetup: false);
    }

    [MenuItem("Build/DCGO/Audit/Run Full Visual Audit (No Prompt)")]
    public static void RunFullMenuNoPrompt()
    {
        RunInternal(captureCurrentSceneSetup: false);
        YamlReferenceAudit.RunInternal(captureCurrentSceneSetup: false);
        AnimationVfxAudit.RunInternal(captureCurrentSceneSetup: false);
    }

    public static void RunBatch()
    {
        RunInternal(captureCurrentSceneSetup: false);
    }

    public static void RunFullBatch()
    {
        RunInternal(captureCurrentSceneSetup: false);
        YamlReferenceAudit.RunInternal(captureCurrentSceneSetup: false);
        AnimationVfxAudit.RunInternal(captureCurrentSceneSetup: false);
    }

    internal static VisualReferenceAuditReport RunInternal(bool captureCurrentSceneSetup)
    {
        using var scope = new VisualAuditUtility.SceneSetupScope(captureCurrentSceneSetup);

        var report = new VisualReferenceAuditReport
        {
            generatedAtUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
        };

        foreach (string scenePath in FindAssetPaths("t:Scene"))
        {
            ScanScene(scenePath, report.entries);
        }

        foreach (string prefabPath in FindAssetPaths("t:Prefab"))
        {
            ScanPrefab(prefabPath, report.entries);
        }

        report.entryCount = report.entries.Count;

        string jsonPath = VisualAuditUtility.GetReportPath(VisualAuditUtility.VisualAuditJsonFileName);
        string csvPath = VisualAuditUtility.GetReportPath(VisualAuditUtility.VisualAuditCsvFileName);
        VisualAuditUtility.WriteJson(jsonPath, JsonUtility.ToJson(report, prettyPrint: true));
        VisualAuditUtility.WriteCsv(csvPath, CsvHeaders, report.entries.Select(ToCsvRow).ToArray());

        AssetDatabase.Refresh();

        Debug.Log(
            $"VisualReferenceAudit: wrote {report.entryCount} entries to {jsonPath} and {csvPath}");

        return report;
    }

    private static bool PrepareInteractiveRun()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("VisualReferenceAudit: exit play mode before running the audit.");
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static IEnumerable<string> FindAssetPaths(string filter)
    {
        return AssetDatabase.FindAssets(filter, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static void ScanScene(string scenePath, List<VisualReferenceAuditEntry> entries)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        string sceneName = scene.name;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            ScanGameObjectTree(root, scenePath, "Scene", sceneName, entries);
        }
    }

    private static void ScanPrefab(string prefabPath, List<VisualReferenceAuditEntry> entries)
    {
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);
            ScanGameObjectTree(root, prefabPath, "Prefab", string.Empty, entries);
        }
        catch (Exception exception)
        {
            entries.Add(new VisualReferenceAuditEntry
            {
                sourceAssetPath = prefabPath,
                sourceKind = "Prefab",
                componentType = "Prefab",
                issueType = "scan_error",
                assetType = "Prefab",
                details = exception.Message,
            });
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void ScanGameObjectTree(
        GameObject root,
        string sourceAssetPath,
        string sourceKind,
        string sceneName,
        List<VisualReferenceAuditEntry> entries)
    {
        if (root == null)
        {
            return;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Component[] components = transforms[i].GetComponents<Component>();
            for (int j = 0; j < components.Length; j++)
            {
                Component component = components[j];
                if (component == null)
                {
                    continue;
                }

                if (component is Image image)
                {
                    AuditImage(image, sourceAssetPath, sourceKind, sceneName, entries);
                    continue;
                }

                if (component is RawImage rawImage)
                {
                    AuditRawImage(rawImage, sourceAssetPath, sourceKind, sceneName, entries);
                    continue;
                }

                if (component is Button button)
                {
                    AuditButton(button, sourceAssetPath, sourceKind, sceneName, entries);
                    continue;
                }

                if (component is TMP_Text tmpText)
                {
                    AuditTmpText(tmpText, sourceAssetPath, sourceKind, sceneName, entries);
                    continue;
                }

                if (component is SpriteRenderer spriteRenderer)
                {
                    AuditSpriteRenderer(spriteRenderer, sourceAssetPath, sourceKind, sceneName, entries);
                    continue;
                }

                if (component is ParticleSystemRenderer particleSystemRenderer)
                {
                    AuditRendererMaterials(particleSystemRenderer, sourceAssetPath, sourceKind, sceneName, entries, requireMaterial: true);
                    continue;
                }

                if (component is TrailRenderer trailRenderer)
                {
                    AuditRendererMaterials(trailRenderer, sourceAssetPath, sourceKind, sceneName, entries, requireMaterial: true);
                    continue;
                }

                if (component is LineRenderer lineRenderer)
                {
                    AuditRendererMaterials(lineRenderer, sourceAssetPath, sourceKind, sceneName, entries, requireMaterial: true);
                    continue;
                }

                if (component is Animator animator)
                {
                    AuditAnimator(animator, sourceAssetPath, sourceKind, sceneName, entries);
                }
            }
        }
    }

    private static void AuditImage(
        Image image,
        string sourceAssetPath,
        string sourceKind,
        string sceneName,
        List<VisualReferenceAuditEntry> entries)
    {
        var serializedObject = new SerializedObject(image);
        VisualReferenceState spriteState = VisualAuditUtility.GetReferenceState(serializedObject, "m_Sprite", out _);

        if (spriteState != VisualReferenceState.Present)
        {
            AddReferenceEntry(
                entries,
                image,
                sourceAssetPath,
                sourceKind,
                sceneName,
                "m_Sprite",
                spriteState,
                "Sprite",
                null,
                image.material);
        }
        else if (image.sprite.texture == null)
        {
            AddEntry(
                entries,
                image,
                sourceAssetPath,
                sourceKind,
                sceneName,
                "m_Sprite.texture",
                "missing_texture",
                "Texture",
                AssetDatabase.GetAssetPath(image.sprite),
                null,
                "Sprite texture resolved to null.");
        }

        AuditGraphicMaterial(image, serializedObject, sourceAssetPath, sourceKind, sceneName, entries);
    }

    private static void AuditRawImage(
        RawImage rawImage,
        string sourceAssetPath,
        string sourceKind,
        string sceneName,
        List<VisualReferenceAuditEntry> entries)
    {
        var serializedObject = new SerializedObject(rawImage);
        VisualReferenceState textureState = VisualAuditUtility.GetReferenceState(serializedObject, "m_Texture", out _);
        if (textureState != VisualReferenceState.Present)
        {
            AddReferenceEntry(
                entries,
                rawImage,
                sourceAssetPath,
                sourceKind,
                sceneName,
                "m_Texture",
                textureState,
                "Texture",
                null,
                rawImage.material);
        }

        AuditGraphicMaterial(rawImage, serializedObject, sourceAssetPath, sourceKind, sceneName, entries);
    }

    private static void AuditButton(
        Button button,
        string sourceAssetPath,
        string sourceKind,
        string sceneName,
        List<VisualReferenceAuditEntry> entries)
    {
        var serializedObject = new SerializedObject(button);
        VisualReferenceState targetGraphicState = VisualAuditUtility.GetReferenceState(serializedObject, "m_TargetGraphic", out _);
        if (targetGraphicState != VisualReferenceState.Present)
        {
            AddReferenceEntry(
                entries,
                button,
                sourceAssetPath,
                sourceKind,
                sceneName,
                "m_TargetGraphic",
                targetGraphicState,
                "Graphic",
                null,
                null);
        }
    }

    private static void AuditTmpText(
        TMP_Text tmpText,
        string sourceAssetPath,
        string sourceKind,
        string sceneName,
        List<VisualReferenceAuditEntry> entries)
    {
        var serializedObject = new SerializedObject(tmpText);
        VisualReferenceState fontState = VisualAuditUtility.GetReferenceState(serializedObject, "m_fontAsset", out _);
        if (fontState != VisualReferenceState.Present)
        {
            AddReferenceEntry(
                entries,
                tmpText,
                sourceAssetPath,
                sourceKind,
                sceneName,
                "m_fontAsset",
                fontState,
                "TMP_FontAsset",
                null,
                tmpText.fontSharedMaterial);
        }

        Material material = tmpText.fontSharedMaterial;
        if (material == null)
        {
            AddEntry(
                entries,
                tmpText,
                sourceAssetPath,
                sourceKind,
                sceneName,
                "fontSharedMaterial",
                "null_material",
                "Material",
                null,
                null,
                "TMP text resolved a null shared material.");
        }
        else
        {
            AuditMaterial(
                entries,
                tmpText,
                sourceAssetPath,
                sourceKind,
                sceneName,
                "fontSharedMaterial",
                material);
        }
    }

    private static void AuditSpriteRenderer(
        SpriteRenderer spriteRenderer,
        string sourceAssetPath,
        string sourceKind,
        string sceneName,
        List<VisualReferenceAuditEntry> entries)
    {
        var serializedObject = new SerializedObject(spriteRenderer);
        VisualReferenceState spriteState = VisualAuditUtility.GetReferenceState(serializedObject, "m_Sprite", out _);
        if (spriteState != VisualReferenceState.Present)
        {
            AddReferenceEntry(
                entries,
                spriteRenderer,
                sourceAssetPath,
                sourceKind,
                sceneName,
                "m_Sprite",
                spriteState,
                "Sprite",
                null,
                spriteRenderer.sharedMaterial);
        }
        else if (spriteRenderer.sprite.texture == null)
        {
            AddEntry(
                entries,
                spriteRenderer,
                sourceAssetPath,
                sourceKind,
                sceneName,
                "m_Sprite.texture",
                "missing_texture",
                "Texture",
                AssetDatabase.GetAssetPath(spriteRenderer.sprite),
                null,
                "SpriteRenderer sprite texture resolved to null.");
        }

        AuditRendererMaterials(spriteRenderer, sourceAssetPath, sourceKind, sceneName, entries, requireMaterial: true);
    }

    private static void AuditRendererMaterials(
        Renderer renderer,
        string sourceAssetPath,
        string sourceKind,
        string sceneName,
        List<VisualReferenceAuditEntry> entries,
        bool requireMaterial)
    {
        var serializedObject = new SerializedObject(renderer);
        SerializedProperty materialsProperty = serializedObject.FindProperty("m_Materials");
        if (materialsProperty == null || !materialsProperty.isArray)
        {
            return;
        }

        if (materialsProperty.arraySize == 0 && requireMaterial)
        {
            AddEntry(
                entries,
                renderer,
                sourceAssetPath,
                sourceKind,
                sceneName,
                "m_Materials",
                "null_material",
                "Material",
                null,
                null,
                "Renderer has no assigned shared materials.");
            return;
        }

        Material[] sharedMaterials = renderer.sharedMaterials ?? Array.Empty<Material>();
        for (int i = 0; i < materialsProperty.arraySize; i++)
        {
            SerializedProperty element = materialsProperty.GetArrayElementAtIndex(i);
            VisualReferenceState state = element.objectReferenceValue != null
                ? VisualReferenceState.Present
                : element.objectReferenceInstanceIDValue != 0
                    ? VisualReferenceState.Missing
                    : VisualReferenceState.Null;

            string propertyName = $"m_Materials[{i}]";
            if (state != VisualReferenceState.Present)
            {
                if (requireMaterial || state == VisualReferenceState.Missing)
                {
                    AddReferenceEntry(
                        entries,
                        renderer,
                        sourceAssetPath,
                        sourceKind,
                        sceneName,
                        propertyName,
                        state,
                        "Material",
                        null,
                        null);
                }

                continue;
            }

            Material material = i < sharedMaterials.Length ? sharedMaterials[i] : null;
            AuditMaterial(entries, renderer, sourceAssetPath, sourceKind, sceneName, propertyName, material);
        }
    }

    private static void AuditAnimator(
        Animator animator,
        string sourceAssetPath,
        string sourceKind,
        string sceneName,
        List<VisualReferenceAuditEntry> entries)
    {
        var serializedObject = new SerializedObject(animator);
        VisualReferenceState controllerState = VisualAuditUtility.GetReferenceState(serializedObject, "m_Controller", out _);
        if (controllerState != VisualReferenceState.Present)
        {
            AddReferenceEntry(
                entries,
                animator,
                sourceAssetPath,
                sourceKind,
                sceneName,
                "m_Controller",
                controllerState,
                "RuntimeAnimatorController",
                null,
                null);
        }
    }

    private static void AuditGraphicMaterial(
        Graphic graphic,
        SerializedObject serializedObject,
        string sourceAssetPath,
        string sourceKind,
        string sceneName,
        List<VisualReferenceAuditEntry> entries)
    {
        VisualReferenceState materialState = VisualAuditUtility.GetReferenceState(serializedObject, "m_Material", out _);
        if (materialState == VisualReferenceState.Missing)
        {
            AddReferenceEntry(
                entries,
                graphic,
                sourceAssetPath,
                sourceKind,
                sceneName,
                "m_Material",
                materialState,
                "Material",
                null,
                null);
        }

        Material material = graphic.material;
        if (material != null)
        {
            AuditMaterial(entries, graphic, sourceAssetPath, sourceKind, sceneName, "material", material);
        }
    }

    private static void AuditMaterial(
        List<VisualReferenceAuditEntry> entries,
        Component component,
        string sourceAssetPath,
        string sourceKind,
        string sceneName,
        string propertyName,
        Material material)
    {
        if (material == null)
        {
            AddEntry(
                entries,
                component,
                sourceAssetPath,
                sourceKind,
                sceneName,
                propertyName,
                "null_material",
                "Material",
                null,
                null,
                "Material reference resolved to null.");
            return;
        }

        string materialPath = AssetDatabase.GetAssetPath(material);
        if (VisualAuditUtility.IsShaderMissing(material))
        {
            AddEntry(
                entries,
                component,
                sourceAssetPath,
                sourceKind,
                sceneName,
                propertyName,
                "missing_shader",
                "Shader",
                materialPath,
                null,
                $"Material '{material.name}' has no shader.");
            return;
        }

        if (VisualAuditUtility.IsShaderUnsupported(material))
        {
            AddEntry(
                entries,
                component,
                sourceAssetPath,
                sourceKind,
                sceneName,
                propertyName,
                "unsupported_shader",
                "Shader",
                materialPath,
                material.shader.name,
                $"Material '{material.name}' uses an unsupported shader.");
        }
    }

    private static void AddReferenceEntry(
        List<VisualReferenceAuditEntry> entries,
        Component component,
        string sourceAssetPath,
        string sourceKind,
        string sceneName,
        string propertyName,
        VisualReferenceState state,
        string assetType,
        string assetPath,
        Material material)
    {
        AddEntry(
            entries,
            component,
            sourceAssetPath,
            sourceKind,
            sceneName,
            propertyName,
            state == VisualReferenceState.Missing ? "missing_reference" : "null_reference",
            assetType,
            assetPath,
            material != null && material.shader != null ? material.shader.name : null,
            state == VisualReferenceState.Missing
                ? "Serialized object reference is missing."
                : "Serialized object reference is null.");
    }

    private static void AddEntry(
        List<VisualReferenceAuditEntry> entries,
        Component component,
        string sourceAssetPath,
        string sourceKind,
        string sceneName,
        string propertyName,
        string issueType,
        string assetType,
        string assetPath,
        string shaderName,
        string details)
    {
        entries.Add(new VisualReferenceAuditEntry
        {
            sourceAssetPath = sourceAssetPath,
            sourceKind = sourceKind,
            sceneName = sceneName,
            hierarchyPath = component != null ? VisualAuditUtility.GetHierarchyPath(component.transform) : string.Empty,
            nearestPrefabRoot = component != null ? VisualAuditUtility.GetNearestPrefabRoot(component.gameObject) : string.Empty,
            componentType = component != null ? component.GetType().Name : string.Empty,
            propertyName = propertyName,
            issueType = issueType,
            assetType = assetType,
            assetPath = assetPath,
            shaderName = shaderName,
            size = GetSize(component),
            details = details,
        });
    }

    private static string GetSize(Component component)
    {
        if (component == null)
        {
            return string.Empty;
        }

        if (component.transform is RectTransform rectTransform)
        {
            return VisualAuditUtility.FormatVector2(rectTransform.rect.size);
        }

        if (component is Renderer renderer)
        {
            return VisualAuditUtility.FormatVector3(renderer.bounds.size);
        }

        return VisualAuditUtility.FormatVector3(component.transform.lossyScale);
    }

    private static IReadOnlyList<string> ToCsvRow(VisualReferenceAuditEntry entry)
    {
        return new[]
        {
            entry.sourceAssetPath ?? string.Empty,
            entry.sourceKind ?? string.Empty,
            entry.sceneName ?? string.Empty,
            entry.hierarchyPath ?? string.Empty,
            entry.nearestPrefabRoot ?? string.Empty,
            entry.componentType ?? string.Empty,
            entry.propertyName ?? string.Empty,
            entry.issueType ?? string.Empty,
            entry.assetType ?? string.Empty,
            entry.assetPath ?? string.Empty,
            entry.shaderName ?? string.Empty,
            entry.size ?? string.Empty,
            entry.details ?? string.Empty,
        };
    }
}
#endif
