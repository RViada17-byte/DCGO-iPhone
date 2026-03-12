#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[Serializable]
public sealed class YamlReferenceAuditEntry
{
    public string sourceAssetPath;
    public int lineNumber;
    public string propertyName;
    public string referenceType;
    public string guid;
    public string context;
    public string details;
}

public static class YamlReferenceAudit
{
    private static readonly Regex GuidRegex = new Regex(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
    private static readonly string[] CsvHeaders =
    {
        "sourceAssetPath",
        "lineNumber",
        "propertyName",
        "referenceType",
        "guid",
        "context",
        "details",
    };

    [MenuItem("Build/DCGO/Audit/Run YAML Reference Audit")]
    public static void RunMenu()
    {
        RunInternal(captureCurrentSceneSetup: false);
    }

    public static void RunBatch()
    {
        RunInternal(captureCurrentSceneSetup: false);
    }

    internal static List<YamlReferenceAuditEntry> RunInternal(bool captureCurrentSceneSetup)
    {
        using var _ = new VisualAuditUtility.SceneSetupScope(captureCurrentSceneSetup);

        var entries = new List<YamlReferenceAuditEntry>();
        foreach (string assetPath in FindYamlAssets())
        {
            ScanYamlAsset(assetPath, entries);
        }

        string csvPath = VisualAuditUtility.GetReportPath(VisualAuditUtility.MissingGuidsCsvFileName);
        VisualAuditUtility.WriteCsv(csvPath, CsvHeaders, entries.Select(ToCsvRow).ToArray());
        AssetDatabase.Refresh();

        Debug.Log($"YamlReferenceAudit: wrote {entries.Count} unresolved GUID entries to {csvPath}");
        return entries;
    }

    private static IEnumerable<string> FindYamlAssets()
    {
        return AssetDatabase.FindAssets(string.Empty, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path =>
                path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".controller", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static void ScanYamlAsset(string assetPath, List<YamlReferenceAuditEntry> entries)
    {
        string fullPath = Path.Combine(VisualAuditUtility.ProjectRoot, assetPath);
        if (!File.Exists(fullPath))
        {
            return;
        }

        string[] lines = File.ReadAllLines(fullPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            MatchCollection matches = GuidRegex.Matches(line);
            for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
            {
                string guid = matches[matchIndex].Groups[1].Value;
                if (VisualAuditUtility.IsBuiltInGuid(guid))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    continue;
                }

                string propertyName = ExtractPropertyName(line);
                string context = BuildContext(lines, i);
                string referenceType = ClassifyReference(assetPath, propertyName, context);
                if (string.IsNullOrEmpty(referenceType))
                {
                    continue;
                }

                entries.Add(new YamlReferenceAuditEntry
                {
                    sourceAssetPath = assetPath,
                    lineNumber = i + 1,
                    propertyName = propertyName,
                    referenceType = referenceType,
                    guid = guid,
                    context = context,
                    details = "GUID could not be resolved via AssetDatabase.",
                });
            }
        }
    }

    private static string ExtractPropertyName(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        int colonIndex = line.IndexOf(':');
        if (colonIndex <= 0)
        {
            return string.Empty;
        }

        return line.Substring(0, colonIndex).Trim();
    }

    private static string BuildContext(string[] lines, int lineIndex)
    {
        int start = Mathf.Max(0, lineIndex - 2);
        int end = Mathf.Min(lines.Length - 1, lineIndex + 1);
        var contextLines = new List<string>();
        for (int i = start; i <= end; i++)
        {
            string trimmed = lines[i].Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                contextLines.Add(trimmed);
            }
        }

        return string.Join(" | ", contextLines);
    }

    private static string ClassifyReference(string assetPath, string propertyName, string context)
    {
        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        string normalizedContext = (propertyName + " " + context).ToLowerInvariant();

        if (extension == ".anim" || extension == ".controller")
        {
            return "animation_object";
        }

        if (normalizedContext.Contains("m_sprite") || normalizedContext.Contains("sprite"))
        {
            return "sprite";
        }

        if (normalizedContext.Contains("material") || normalizedContext.Contains("shader"))
        {
            return "material";
        }

        if (normalizedContext.Contains("font"))
        {
            return "font";
        }

        if (normalizedContext.Contains("controller") || normalizedContext.Contains("clip") || normalizedContext.Contains("curve"))
        {
            return "animation_object";
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ToCsvRow(YamlReferenceAuditEntry entry)
    {
        return new[]
        {
            entry.sourceAssetPath ?? string.Empty,
            entry.lineNumber.ToString(),
            entry.propertyName ?? string.Empty,
            entry.referenceType ?? string.Empty,
            entry.guid ?? string.Empty,
            entry.context ?? string.Empty,
            entry.details ?? string.Empty,
        };
    }
}
#endif
