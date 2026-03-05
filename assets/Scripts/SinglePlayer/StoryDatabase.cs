using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class StoryDatabase
{
    private const string StoryFileName = "story.json";

    private static StoryDatabase _instance;

    public static StoryDatabase Instance => _instance ??= new StoryDatabase();

    private readonly List<StoryNodeDef> _nodes = new List<StoryNodeDef>();
    private readonly Dictionary<string, StoryNodeDef> _nodesById = new Dictionary<string, StoryNodeDef>(StringComparer.Ordinal);

    public IReadOnlyList<StoryNodeDef> Nodes => _nodes;

    public StoryDatabase()
    {
        Load();
    }

    public StoryNodeDef GetNode(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        _nodesById.TryGetValue(id, out StoryNodeDef node);
        return node;
    }

    public void Reload()
    {
        Load();
    }

    private void Load()
    {
        _nodes.Clear();
        _nodesById.Clear();

        string json = TryReadStoryJson();
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            StoryDatabaseDef database = JsonUtility.FromJson<StoryDatabaseDef>(json);
            if (database == null || database.nodes == null)
            {
                return;
            }

            for (int i = 0; i < database.nodes.Length; i++)
            {
                StoryNodeDef node = database.nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.id))
                {
                    continue;
                }

                _nodes.Add(node);

                if (!_nodesById.ContainsKey(node.id))
                {
                    _nodesById.Add(node.id, node);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"StoryDatabase: Failed to parse {StoryFileName}. {exception.Message}");
        }
    }

    private static string TryReadStoryJson()
    {
        List<string> candidatePaths = GetCandidateStoryPaths();
        for (int i = 0; i < candidatePaths.Count; i++)
        {
            string path = candidatePaths[i];

            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"StoryDatabase: Failed to read '{path}'. {exception.Message}");
            }
        }

        return string.Empty;
    }

    private static List<string> GetCandidateStoryPaths()
    {
        List<string> paths = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddPath(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            string fullPath = NormalizePath(Path.Combine(root, StoryFileName));
            if (seen.Add(fullPath))
            {
                paths.Add(fullPath);
            }
        }

        AddPath(StreamingAssetsUtility.GetStreamingAssetPath("", false));
        AddPath(Application.streamingAssetsPath);
        AddPath(Application.persistentDataPath);

#if UNITY_EDITOR
        AddPath(Path.Combine(Application.dataPath, "StreamingAssets"));
#endif

        return paths;
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/");
    }
}
