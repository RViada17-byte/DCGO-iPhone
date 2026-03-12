#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal enum VisualReferenceState
{
    Present,
    Null,
    Missing,
}

internal static class VisualAuditUtility
{
    internal const string VisualAuditJsonFileName = "visual_audit.json";
    internal const string VisualAuditCsvFileName = "visual_audit.csv";
    internal const string MissingGuidsCsvFileName = "missing_guids.csv";
    internal const string AnimationVfxAuditCsvFileName = "animation_vfx_audit.csv";
    internal const string BuiltInExtraResourceGuid = "0000000000000000f000000000000000";
    internal const string BuiltInLibraryGuid = "0000000000000000e000000000000000";

    internal static string ProjectRoot => Directory.GetCurrentDirectory();

    internal static string GetReportPath(string fileName)
    {
        return Path.Combine(ProjectRoot, fileName);
    }

    internal static void EnsureParentDirectory(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    internal static void WriteTextFile(string filePath, string contents)
    {
        EnsureParentDirectory(filePath);
        File.WriteAllText(filePath, contents, Encoding.UTF8);
    }

    internal static void WriteJson(string filePath, string json)
    {
        WriteTextFile(filePath, json);
    }

    internal static void WriteCsv(string filePath, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, headers);

        foreach (IReadOnlyList<string> row in rows)
        {
            AppendCsvLine(builder, row);
        }

        WriteTextFile(filePath, builder.ToString());
    }

    internal static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!needsQuotes)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    internal static string GetHierarchyPath(Transform transform)
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

    internal static string GetNearestPrefabRoot(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return string.Empty;
        }

        GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
        if (prefabRoot != null)
        {
            return GetHierarchyPath(prefabRoot.transform);
        }

        if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
        {
            return GetHierarchyPath(gameObject.transform.root);
        }

        return string.Empty;
    }

    internal static string FormatVector2(Vector2 value)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.##}x{1:0.##}",
            value.x,
            value.y);
    }

    internal static string FormatVector3(Vector3 value)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.##}x{1:0.##}x{2:0.##}",
            value.x,
            value.y,
            value.z);
    }

    internal static VisualReferenceState GetReferenceState(SerializedObject serializedObject, string propertyPath, out SerializedProperty property)
    {
        property = serializedObject != null ? serializedObject.FindProperty(propertyPath) : null;
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
        {
            return VisualReferenceState.Null;
        }

        if (property.objectReferenceValue != null)
        {
            return VisualReferenceState.Present;
        }

        return property.objectReferenceInstanceIDValue != 0
            ? VisualReferenceState.Missing
            : VisualReferenceState.Null;
    }

    internal static bool IsMaterialMissing(Material material)
    {
        return material == null;
    }

    internal static bool IsShaderMissing(Material material)
    {
        return material == null || material.shader == null;
    }

    internal static bool IsShaderUnsupported(Material material)
    {
        return material != null && material.shader != null && !material.shader.isSupported;
    }

    internal static bool IsBuiltInGuid(string guid)
    {
        return string.Equals(guid, BuiltInExtraResourceGuid, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(guid, BuiltInLibraryGuid, StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendCsvLine(StringBuilder builder, IReadOnlyList<string> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(EscapeCsv(values[i]));
        }

        builder.AppendLine();
    }

    internal readonly struct SceneSetupScope : IDisposable
    {
        private readonly SceneSetup[] _sceneSetup;
        private readonly bool _restore;

        internal SceneSetupScope(bool captureCurrentSetup)
        {
            _restore = captureCurrentSetup;
            _sceneSetup = captureCurrentSetup ? EditorSceneManager.GetSceneManagerSetup() : null;
        }

        public void Dispose()
        {
            if (_restore && _sceneSetup != null)
            {
                EditorSceneManager.RestoreSceneManagerSetup(_sceneSetup);
            }
        }
    }
}
#endif
