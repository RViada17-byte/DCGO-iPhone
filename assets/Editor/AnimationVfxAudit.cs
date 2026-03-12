#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class AnimationVfxAuditEntry
{
    public string sourceAssetPath;
    public string sourceKind;
    public string hierarchyPath;
    public string componentType;
    public string bindingPath;
    public string propertyName;
    public string issueType;
    public string assetType;
    public string assetPath;
    public string shaderName;
    public string details;
}

public static class AnimationVfxAudit
{
    private static readonly string[] CsvHeaders =
    {
        "sourceAssetPath",
        "sourceKind",
        "hierarchyPath",
        "componentType",
        "bindingPath",
        "propertyName",
        "issueType",
        "assetType",
        "assetPath",
        "shaderName",
        "details",
    };

    [MenuItem("Build/DCGO/Audit/Run Animation/VFX Audit")]
    public static void RunMenu()
    {
        RunInternal(captureCurrentSceneSetup: false);
    }

    public static void RunBatch()
    {
        RunInternal(captureCurrentSceneSetup: false);
    }

    internal static List<AnimationVfxAuditEntry> RunInternal(bool captureCurrentSceneSetup)
    {
        using var _ = new VisualAuditUtility.SceneSetupScope(captureCurrentSceneSetup);

        var entries = new List<AnimationVfxAuditEntry>();

        foreach (string clipPath in FindAssetPaths("t:AnimationClip"))
        {
            ScanAnimationClip(clipPath, entries);
        }

        foreach (string prefabPath in FindAssetPaths("t:Prefab"))
        {
            ScanPrefab(prefabPath, entries);
        }

        string csvPath = VisualAuditUtility.GetReportPath(VisualAuditUtility.AnimationVfxAuditCsvFileName);
        VisualAuditUtility.WriteCsv(csvPath, CsvHeaders, entries.Select(ToCsvRow).ToArray());
        AssetDatabase.Refresh();

        Debug.Log($"AnimationVfxAudit: wrote {entries.Count} entries to {csvPath}");
        return entries;
    }

    private static IEnumerable<string> FindAssetPaths(string filter)
    {
        return AssetDatabase.FindAssets(filter, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static void ScanAnimationClip(string clipPath, List<AnimationVfxAuditEntry> entries)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            return;
        }

        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        for (int i = 0; i < bindings.Length; i++)
        {
            EditorCurveBinding binding = bindings[i];
            if (!IsRelevantAnimationBinding(binding.propertyName))
            {
                continue;
            }

            ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            for (int keyframeIndex = 0; keyframeIndex < keyframes.Length; keyframeIndex++)
            {
                ObjectReferenceKeyframe keyframe = keyframes[keyframeIndex];
                if (keyframe.value == null)
                {
                    entries.Add(new AnimationVfxAuditEntry
                    {
                        sourceAssetPath = clipPath,
                        sourceKind = "AnimationClip",
                        bindingPath = binding.path,
                        propertyName = binding.propertyName,
                        issueType = "null_curve_reference",
                        assetType = "AnimationObjectReference",
                        details = $"Curve keyframe at time {keyframe.time:0.###} has a null object reference.",
                    });
                    continue;
                }

                if (keyframe.value is Sprite sprite && sprite.texture == null)
                {
                    entries.Add(new AnimationVfxAuditEntry
                    {
                        sourceAssetPath = clipPath,
                        sourceKind = "AnimationClip",
                        bindingPath = binding.path,
                        propertyName = binding.propertyName,
                        issueType = "missing_texture",
                        assetType = "Texture",
                        assetPath = AssetDatabase.GetAssetPath(sprite),
                        details = $"Animated sprite '{sprite.name}' resolved a null texture.",
                    });
                    continue;
                }

                if (keyframe.value is Material material)
                {
                    AddMaterialEntry(entries, clipPath, "AnimationClip", string.Empty, binding.path, binding.propertyName, material);
                }
            }
        }
    }

    private static void ScanPrefab(string prefabPath, List<AnimationVfxAuditEntry> entries)
    {
        if (!LooksRelevantPrefab(prefabPath))
        {
            return;
        }

        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                return;
            }

            bool hasAnimator = root.GetComponentInChildren<Animator>(true) != null;
            bool hasVfxRenderer = root.GetComponentInChildren<ParticleSystemRenderer>(true) != null ||
                                  root.GetComponentInChildren<TrailRenderer>(true) != null ||
                                  root.GetComponentInChildren<LineRenderer>(true) != null;

            if (!hasAnimator && !hasVfxRenderer && !PathLooksLikeAnimatedUi(prefabPath))
            {
                return;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                AuditPrefabComponent<Image>(current, prefabPath, entries, AuditImage);
                AuditPrefabComponent<RawImage>(current, prefabPath, entries, AuditRawImage);
                AuditPrefabComponent<TMP_Text>(current, prefabPath, entries, AuditTmpText);
                AuditPrefabComponent<SpriteRenderer>(current, prefabPath, entries, AuditSpriteRenderer);
                AuditPrefabComponent<ParticleSystemRenderer>(current, prefabPath, entries, AuditRenderer);
                AuditPrefabComponent<TrailRenderer>(current, prefabPath, entries, AuditRenderer);
                AuditPrefabComponent<LineRenderer>(current, prefabPath, entries, AuditRenderer);
                AuditPrefabComponent<Animator>(current, prefabPath, entries, AuditAnimator);
            }
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void AuditPrefabComponent<T>(
        Transform transform,
        string prefabPath,
        List<AnimationVfxAuditEntry> entries,
        Action<T, string, List<AnimationVfxAuditEntry>> audit)
        where T : Component
    {
        T component = transform.GetComponent<T>();
        if (component != null)
        {
            audit(component, prefabPath, entries);
        }
    }

    private static void AuditImage(Image image, string prefabPath, List<AnimationVfxAuditEntry> entries)
    {
        var serializedObject = new SerializedObject(image);
        VisualReferenceState spriteState = VisualAuditUtility.GetReferenceState(serializedObject, "m_Sprite", out _);
        if (spriteState != VisualReferenceState.Present)
        {
            AddReferenceEntry(entries, prefabPath, image, "m_Sprite", spriteState, "Sprite");
        }
        else if (image.sprite.texture == null)
        {
            entries.Add(new AnimationVfxAuditEntry
            {
                sourceAssetPath = prefabPath,
                sourceKind = "Prefab",
                hierarchyPath = VisualAuditUtility.GetHierarchyPath(image.transform),
                componentType = image.GetType().Name,
                propertyName = "m_Sprite.texture",
                issueType = "missing_texture",
                assetType = "Texture",
                assetPath = AssetDatabase.GetAssetPath(image.sprite),
                details = "Animated UI image sprite texture resolved to null.",
            });
        }

        AuditGraphicMaterial(image, prefabPath, entries);
    }

    private static void AuditRawImage(RawImage rawImage, string prefabPath, List<AnimationVfxAuditEntry> entries)
    {
        var serializedObject = new SerializedObject(rawImage);
        VisualReferenceState textureState = VisualAuditUtility.GetReferenceState(serializedObject, "m_Texture", out _);
        if (textureState != VisualReferenceState.Present)
        {
            AddReferenceEntry(entries, prefabPath, rawImage, "m_Texture", textureState, "Texture");
        }

        AuditGraphicMaterial(rawImage, prefabPath, entries);
    }

    private static void AuditTmpText(TMP_Text tmpText, string prefabPath, List<AnimationVfxAuditEntry> entries)
    {
        var serializedObject = new SerializedObject(tmpText);
        VisualReferenceState fontState = VisualAuditUtility.GetReferenceState(serializedObject, "m_fontAsset", out _);
        if (fontState != VisualReferenceState.Present)
        {
            AddReferenceEntry(entries, prefabPath, tmpText, "m_fontAsset", fontState, "TMP_FontAsset");
        }

        AddMaterialEntry(entries, prefabPath, "Prefab", VisualAuditUtility.GetHierarchyPath(tmpText.transform), string.Empty, "fontSharedMaterial", tmpText.fontSharedMaterial);
    }

    private static void AuditSpriteRenderer(SpriteRenderer spriteRenderer, string prefabPath, List<AnimationVfxAuditEntry> entries)
    {
        var serializedObject = new SerializedObject(spriteRenderer);
        VisualReferenceState spriteState = VisualAuditUtility.GetReferenceState(serializedObject, "m_Sprite", out _);
        if (spriteState != VisualReferenceState.Present)
        {
            AddReferenceEntry(entries, prefabPath, spriteRenderer, "m_Sprite", spriteState, "Sprite");
        }
        else if (spriteRenderer.sprite.texture == null)
        {
            entries.Add(new AnimationVfxAuditEntry
            {
                sourceAssetPath = prefabPath,
                sourceKind = "Prefab",
                hierarchyPath = VisualAuditUtility.GetHierarchyPath(spriteRenderer.transform),
                componentType = spriteRenderer.GetType().Name,
                propertyName = "m_Sprite.texture",
                issueType = "missing_texture",
                assetType = "Texture",
                assetPath = AssetDatabase.GetAssetPath(spriteRenderer.sprite),
                details = "Animated sprite renderer resolved a null texture.",
            });
        }

        AuditRenderer(spriteRenderer, prefabPath, entries);
    }

    private static void AuditRenderer(Renderer renderer, string prefabPath, List<AnimationVfxAuditEntry> entries)
    {
        Material[] materials = renderer.sharedMaterials ?? Array.Empty<Material>();
        if (materials.Length == 0)
        {
            entries.Add(new AnimationVfxAuditEntry
            {
                sourceAssetPath = prefabPath,
                sourceKind = "Prefab",
                hierarchyPath = VisualAuditUtility.GetHierarchyPath(renderer.transform),
                componentType = renderer.GetType().Name,
                propertyName = "m_Materials",
                issueType = "null_material",
                assetType = "Material",
                details = "Renderer has no shared materials.",
            });
            return;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            AddMaterialEntry(
                entries,
                prefabPath,
                "Prefab",
                VisualAuditUtility.GetHierarchyPath(renderer.transform),
                string.Empty,
                $"m_Materials[{i}]",
                materials[i],
                renderer.GetType().Name);
        }
    }

    private static void AuditAnimator(Animator animator, string prefabPath, List<AnimationVfxAuditEntry> entries)
    {
        var serializedObject = new SerializedObject(animator);
        VisualReferenceState controllerState = VisualAuditUtility.GetReferenceState(serializedObject, "m_Controller", out _);
        if (controllerState != VisualReferenceState.Present)
        {
            AddReferenceEntry(entries, prefabPath, animator, "m_Controller", controllerState, "RuntimeAnimatorController");
        }
    }

    private static void AuditGraphicMaterial(Graphic graphic, string prefabPath, List<AnimationVfxAuditEntry> entries)
    {
        AddMaterialEntry(
            entries,
            prefabPath,
            "Prefab",
            VisualAuditUtility.GetHierarchyPath(graphic.transform),
            string.Empty,
            "material",
            graphic.material,
            graphic.GetType().Name);
    }

    private static void AddMaterialEntry(
        List<AnimationVfxAuditEntry> entries,
        string sourceAssetPath,
        string sourceKind,
        string hierarchyPath,
        string bindingPath,
        string propertyName,
        Material material,
        string componentType = "")
    {
        if (material == null)
        {
            entries.Add(new AnimationVfxAuditEntry
            {
                sourceAssetPath = sourceAssetPath,
                sourceKind = sourceKind,
                hierarchyPath = hierarchyPath,
                componentType = componentType,
                bindingPath = bindingPath,
                propertyName = propertyName,
                issueType = "null_material",
                assetType = "Material",
                details = "Material reference resolved to null.",
            });
            return;
        }

        string materialPath = AssetDatabase.GetAssetPath(material);
        if (material.shader == null)
        {
            entries.Add(new AnimationVfxAuditEntry
            {
                sourceAssetPath = sourceAssetPath,
                sourceKind = sourceKind,
                hierarchyPath = hierarchyPath,
                componentType = componentType,
                bindingPath = bindingPath,
                propertyName = propertyName,
                issueType = "missing_shader",
                assetType = "Shader",
                assetPath = materialPath,
                details = $"Material '{material.name}' has no shader.",
            });
            return;
        }

        if (!material.shader.isSupported)
        {
            entries.Add(new AnimationVfxAuditEntry
            {
                sourceAssetPath = sourceAssetPath,
                sourceKind = sourceKind,
                hierarchyPath = hierarchyPath,
                componentType = componentType,
                bindingPath = bindingPath,
                propertyName = propertyName,
                issueType = "unsupported_shader",
                assetType = "Shader",
                assetPath = materialPath,
                shaderName = material.shader.name,
                details = $"Material '{material.name}' uses an unsupported shader.",
            });
        }
    }

    private static void AddReferenceEntry(
        List<AnimationVfxAuditEntry> entries,
        string prefabPath,
        Component component,
        string propertyName,
        VisualReferenceState state,
        string assetType)
    {
        entries.Add(new AnimationVfxAuditEntry
        {
            sourceAssetPath = prefabPath,
            sourceKind = "Prefab",
            hierarchyPath = VisualAuditUtility.GetHierarchyPath(component.transform),
            componentType = component.GetType().Name,
            propertyName = propertyName,
            issueType = state == VisualReferenceState.Missing ? "missing_reference" : "null_reference",
            assetType = assetType,
            details = state == VisualReferenceState.Missing
                ? "Serialized object reference is missing."
                : "Serialized object reference is null.",
        });
    }

    private static bool IsRelevantAnimationBinding(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return false;
        }

        string normalized = propertyName.ToLowerInvariant();
        return normalized.Contains("sprite") ||
               normalized.Contains("material") ||
               normalized.Contains("font");
    }

    private static bool LooksRelevantPrefab(string prefabPath)
    {
        return prefabPath.Contains("/Effect/", StringComparison.OrdinalIgnoreCase) ||
               prefabPath.Contains("/Recovered/VFX/", StringComparison.OrdinalIgnoreCase) ||
               prefabPath.Contains("/Prefab/UI/Battle Scene/", StringComparison.OrdinalIgnoreCase) ||
               prefabPath.Contains("/Prefab/UI/Deck Selection/", StringComparison.OrdinalIgnoreCase) ||
               prefabPath.Contains("/Prefab/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathLooksLikeAnimatedUi(string prefabPath)
    {
        return prefabPath.Contains("/Battle Scene/", StringComparison.OrdinalIgnoreCase) ||
               prefabPath.Contains("/Deck ", StringComparison.OrdinalIgnoreCase) ||
               prefabPath.Contains("/Deck/", StringComparison.OrdinalIgnoreCase) ||
               prefabPath.Contains("/Opening/", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ToCsvRow(AnimationVfxAuditEntry entry)
    {
        return new[]
        {
            entry.sourceAssetPath ?? string.Empty,
            entry.sourceKind ?? string.Empty,
            entry.hierarchyPath ?? string.Empty,
            entry.componentType ?? string.Empty,
            entry.bindingPath ?? string.Empty,
            entry.propertyName ?? string.Empty,
            entry.issueType ?? string.Empty,
            entry.assetType ?? string.Empty,
            entry.assetPath ?? string.Empty,
            entry.shaderName ?? string.Empty,
            entry.details ?? string.Empty,
        };
    }
}
#endif
