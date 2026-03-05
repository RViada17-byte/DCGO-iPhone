using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DuelBoardDatabase
{
    private const string DuelBoardFileName = "duel_board.json";

    private static DuelBoardDatabase _instance;

    public static DuelBoardDatabase Instance => _instance ??= new DuelBoardDatabase();

    private readonly List<DuelBoardDuelDef> _duels = new List<DuelBoardDuelDef>();
    private readonly Dictionary<string, DuelBoardDuelDef> _duelsById = new Dictionary<string, DuelBoardDuelDef>(StringComparer.Ordinal);

    public IReadOnlyList<DuelBoardDuelDef> Duels => _duels;

    public DuelBoardDatabase()
    {
        Load();
    }

    public DuelBoardDuelDef GetDuel(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        _duelsById.TryGetValue(id, out DuelBoardDuelDef duel);
        return duel;
    }

    public void Reload()
    {
        Load();
    }

    private void Load()
    {
        _duels.Clear();
        _duelsById.Clear();

        string json = TryReadDuelBoardJson();
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            DuelBoardDatabaseDef database = JsonUtility.FromJson<DuelBoardDatabaseDef>(json);
            if (database == null || database.duels == null)
            {
                return;
            }

            for (int i = 0; i < database.duels.Length; i++)
            {
                DuelBoardDuelDef duel = database.duels[i];
                if (duel == null || string.IsNullOrWhiteSpace(duel.id))
                {
                    continue;
                }

                _duels.Add(duel);

                if (!_duelsById.ContainsKey(duel.id))
                {
                    _duelsById.Add(duel.id, duel);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"DuelBoardDatabase: Failed to parse {DuelBoardFileName}. {exception.Message}");
        }
    }

    private static string TryReadDuelBoardJson()
    {
        List<string> candidatePaths = GetCandidateJsonPaths();
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
                Debug.LogWarning($"DuelBoardDatabase: Failed to read '{path}'. {exception.Message}");
            }
        }

        return string.Empty;
    }

    private static List<string> GetCandidateJsonPaths()
    {
        List<string> paths = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddPath(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            string fullPath = NormalizePath(Path.Combine(root, DuelBoardFileName));
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
