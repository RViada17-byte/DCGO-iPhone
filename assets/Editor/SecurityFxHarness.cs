#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SecurityFxHarness
{
    static readonly string[] AuditPrefabPaths =
    {
        "assets/Prefab/SecurityBreakGlass.prefab",
        "assets/Prefab/OpponentSecurityBreakGlass.prefab",
        "assets/Prefab/BreakGlass.prefab",
        "assets/Prefab/BreakCardGlass.prefab",
        "assets/Effect/ポケモン/水.prefab",
    };

    [MenuItem("Build/DCGO/Audit Security FX")]
    public static void AuditSecurityFx()
    {
        string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../security_fx_harness.csv"));
        StringBuilder report = new StringBuilder();
        report.AppendLine("prefabPath,rendererPath,rendererType,materialSlot,materialName,materialPath,shaderName,shaderPath,shaderSupported,shaderErrors,shaderIsBuiltin,mainTexturePath");

        Scene previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            for (int i = 0; i < AuditPrefabPaths.Length; i++)
            {
                string prefabPath = AuditPrefabPaths[i];
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null)
                {
                    report.AppendLine(ToCsv(prefabPath, "<missing prefab>", string.Empty, "-1", string.Empty, string.Empty, string.Empty, string.Empty, "false", "false", "false", string.Empty));
                    continue;
                }

                GameObject root = PrefabUtility.InstantiatePrefab(prefabAsset, previewScene) as GameObject;
                if (root == null)
                {
                    report.AppendLine(ToCsv(prefabPath, "<instantiate failed>", string.Empty, "-1", string.Empty, string.Empty, string.Empty, string.Empty, "false", "false", "false", string.Empty));
                    continue;
                }

                try
                {
                    AppendPrefabReport(report, prefabPath, root);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }

        File.WriteAllText(outputPath, report.ToString());
        Debug.Log($"[SecurityFxHarness] Wrote {outputPath}");
    }

    static void AppendPrefabReport(StringBuilder report, string prefabPath, GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            report.AppendLine(ToCsv(prefabPath, "<no renderers>", string.Empty, "-1", string.Empty, string.Empty, string.Empty, string.Empty, "false", "false", "false", string.Empty));
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            AppendRendererReport(report, prefabPath, root.transform, renderer);
        }
    }

    static void AppendRendererReport(StringBuilder report, string prefabPath, Transform root, Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;
        string rendererPath = GetRelativePath(root, renderer.transform);
        string rendererType = renderer.GetType().Name;

        if (materials == null || materials.Length == 0)
        {
            report.AppendLine(ToCsv(prefabPath, rendererPath, rendererType, "-1", "<none>", string.Empty, string.Empty, string.Empty, "false", "false", "false", string.Empty));
            return;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            AppendMaterialReport(report, prefabPath, rendererPath, rendererType, i, material);
        }
    }

    static void AppendMaterialReport(StringBuilder report, string prefabPath, string rendererPath, string rendererType, int materialSlot, Material material)
    {
        if (material == null)
        {
            report.AppendLine(ToCsv(prefabPath, rendererPath, rendererType, materialSlot.ToString(), "<null>", string.Empty, string.Empty, string.Empty, "false", "false", "false", string.Empty));
            return;
        }

        Shader shader = material.shader;
        string materialPath = AssetDatabase.GetAssetPath(material);
        string shaderName = shader != null ? shader.name : "<null>";
        string shaderPath = shader != null ? AssetDatabase.GetAssetPath(shader) : "<null>";
        string texturePath = material.mainTexture != null ? AssetDatabase.GetAssetPath(material.mainTexture) : "<null>";
        bool shaderSupported = shader != null && shader.isSupported;
        bool hasShaderErrors = shader != null && ShaderUtil.ShaderHasError(shader);
        bool shaderIsBuiltin = shader != null && string.IsNullOrEmpty(shaderPath);

        report.AppendLine(
            ToCsv(
                prefabPath,
                rendererPath,
                rendererType,
                materialSlot.ToString(),
                material.name,
                materialPath,
                shaderName,
                shaderPath,
                shaderSupported.ToString(),
                hasShaderErrors.ToString(),
                shaderIsBuiltin.ToString(),
                texturePath));
    }

    static string GetRelativePath(Transform root, Transform current)
    {
        if (current == root)
        {
            return current.name;
        }

        string path = current.name;
        while (current.parent != null && current.parent != root)
        {
            current = current.parent;
            path = $"{current.name}/{path}";
        }

        return $"{root.name}/{path}";
    }

    static string ToCsv(params string[] values)
    {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(EscapeCsv(values[i]));
        }

        return builder.ToString();
    }

    static string EscapeCsv(string value)
    {
        string safeValue = value ?? string.Empty;
        if (safeValue.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
        {
            return safeValue;
        }

        return $"\"{safeValue.Replace("\"", "\"\"")}\"";
    }
}
#endif
