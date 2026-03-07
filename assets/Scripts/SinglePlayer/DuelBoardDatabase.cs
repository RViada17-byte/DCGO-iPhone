using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DuelBoardDatabase
{
    private const string DuelBoardFileName = "duel_board.json";
    private const string LegacyActId = "legacy_duel_board_act";
    private const string LegacyWorldId = "legacy_duel_board_world";

    private static DuelBoardDatabase _instance;

    public static DuelBoardDatabase Instance => _instance ??= new DuelBoardDatabase();

    private readonly List<DuelBoardActDef> _acts = new List<DuelBoardActDef>();
    private readonly List<DuelBoardWorldDef> _worlds = new List<DuelBoardWorldDef>();
    private readonly List<DuelBoardDuelDef> _duels = new List<DuelBoardDuelDef>();
    private readonly Dictionary<string, DuelBoardActDef> _actsById = new Dictionary<string, DuelBoardActDef>(StringComparer.Ordinal);
    private readonly Dictionary<string, DuelBoardWorldDef> _worldsById = new Dictionary<string, DuelBoardWorldDef>(StringComparer.Ordinal);
    private readonly Dictionary<string, DuelBoardDuelDef> _duelsById = new Dictionary<string, DuelBoardDuelDef>(StringComparer.Ordinal);

    public IReadOnlyList<DuelBoardActDef> Acts => _acts;
    public IReadOnlyList<DuelBoardWorldDef> Worlds => _worlds;
    public IReadOnlyList<DuelBoardDuelDef> Duels => _duels;

    public DuelBoardDatabase()
    {
        Load();
    }

    public DuelBoardActDef GetAct(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        _actsById.TryGetValue(id, out DuelBoardActDef act);
        return act;
    }

    public DuelBoardWorldDef GetWorld(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        _worldsById.TryGetValue(id, out DuelBoardWorldDef world);
        return world;
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
        _acts.Clear();
        _worlds.Clear();
        _duels.Clear();
        _actsById.Clear();
        _worldsById.Clear();
        _duelsById.Clear();

        string json = TryReadDuelBoardJson();
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            DuelBoardDatabaseDef database = JsonUtility.FromJson<DuelBoardDatabaseDef>(json);
            if (database == null)
            {
                return;
            }

            if (database.acts != null && database.acts.Length > 0)
            {
                LoadActs(database.acts);
            }
            else if (database.duels != null && database.duels.Length > 0)
            {
                LoadLegacyDuels(database.duels);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"DuelBoardDatabase: Failed to parse {DuelBoardFileName}. {exception.Message}");
        }
    }

    private void LoadActs(DuelBoardActDef[] acts)
    {
        for (int actIndex = 0; actIndex < acts.Length; actIndex++)
        {
            DuelBoardActDef act = acts[actIndex];
            if (act == null || string.IsNullOrWhiteSpace(act.id))
            {
                continue;
            }

            act.orderIndex = actIndex;
            _acts.Add(act);

            if (!_actsById.ContainsKey(act.id))
            {
                _actsById.Add(act.id, act);
            }

            if (act.worlds == null || act.worlds.Length == 0)
            {
                continue;
            }

            for (int worldIndex = 0; worldIndex < act.worlds.Length; worldIndex++)
            {
                DuelBoardWorldDef world = act.worlds[worldIndex];
                if (world == null || string.IsNullOrWhiteSpace(world.id))
                {
                    continue;
                }

                world.parentActId = act.id;
                world.orderIndex = worldIndex;
                _worlds.Add(world);

                if (!_worldsById.ContainsKey(world.id))
                {
                    _worldsById.Add(world.id, world);
                }

                if (world.duels == null || world.duels.Length == 0)
                {
                    continue;
                }

                for (int duelIndex = 0; duelIndex < world.duels.Length; duelIndex++)
                {
                    DuelBoardDuelDef duel = world.duels[duelIndex];
                    if (duel == null || string.IsNullOrWhiteSpace(duel.id))
                    {
                        continue;
                    }

                    duel.parentActId = act.id;
                    duel.parentWorldId = world.id;
                    duel.orderIndex = duelIndex;
                    _duels.Add(duel);

                    if (!_duelsById.ContainsKey(duel.id))
                    {
                        _duelsById.Add(duel.id, duel);
                    }
                }
            }
        }
    }

    private void LoadLegacyDuels(DuelBoardDuelDef[] duels)
    {
        DuelBoardActDef legacyAct = new DuelBoardActDef
        {
            id = LegacyActId,
            title = "Duelist Board",
        };

        DuelBoardWorldDef legacyWorld = new DuelBoardWorldDef
        {
            id = LegacyWorldId,
            title = "Duelist Board",
            isAuthored = true,
            duels = duels,
        };

        legacyAct.worlds = new[] { legacyWorld };
        LoadActs(new[] { legacyAct });
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
