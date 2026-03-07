using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class StoryDatabase
{
    private const string StoryFileName = "story.json";
    private const string StoryScenesFileName = "story_scenes.json";
    private const string LegacyActId = "legacy_story_act";
    private const string LegacyWorldId = "legacy_story_world";

    private static StoryDatabase _instance;

    public static StoryDatabase Instance => _instance ??= new StoryDatabase();

    private readonly List<StoryActDef> _acts = new List<StoryActDef>();
    private readonly List<StoryWorldDef> _worlds = new List<StoryWorldDef>();
    private readonly List<StoryEncounterDef> _encounters = new List<StoryEncounterDef>();
    private readonly List<StorySceneDef> _scenes = new List<StorySceneDef>();
    private readonly Dictionary<string, StoryActDef> _actsById = new Dictionary<string, StoryActDef>(StringComparer.Ordinal);
    private readonly Dictionary<string, StoryWorldDef> _worldsById = new Dictionary<string, StoryWorldDef>(StringComparer.Ordinal);
    private readonly Dictionary<string, StoryEncounterDef> _encountersById = new Dictionary<string, StoryEncounterDef>(StringComparer.Ordinal);
    private readonly Dictionary<string, StorySceneDef> _scenesById = new Dictionary<string, StorySceneDef>(StringComparer.Ordinal);

    public IReadOnlyList<StoryActDef> Acts => _acts;
    public IReadOnlyList<StoryWorldDef> Worlds => _worlds;
    public IReadOnlyList<StoryEncounterDef> Encounters => _encounters;
    public IReadOnlyList<StorySceneDef> Scenes => _scenes;

    public StoryDatabase()
    {
        Load();
    }

    public StoryActDef GetAct(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        _actsById.TryGetValue(id, out StoryActDef act);
        return act;
    }

    public StoryWorldDef GetWorld(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        _worldsById.TryGetValue(id, out StoryWorldDef world);
        return world;
    }

    public StoryEncounterDef GetEncounter(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        _encountersById.TryGetValue(id, out StoryEncounterDef encounter);
        return encounter;
    }

    public StorySceneDef GetScene(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        _scenesById.TryGetValue(id, out StorySceneDef scene);
        return scene;
    }

    public void Reload()
    {
        Load();
    }

    private void Load()
    {
        _acts.Clear();
        _worlds.Clear();
        _encounters.Clear();
        _scenes.Clear();
        _actsById.Clear();
        _worldsById.Clear();
        _encountersById.Clear();
        _scenesById.Clear();

        string json = TryReadStoryJson();
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            StoryDatabaseDef database = JsonUtility.FromJson<StoryDatabaseDef>(json);
            if (database == null)
            {
                return;
            }

            if (database.acts != null && database.acts.Length > 0)
            {
                LoadActs(database.acts);
            }
            else if (database.nodes != null && database.nodes.Length > 0)
            {
                LoadLegacyNodes(database.nodes);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"StoryDatabase: Failed to parse {StoryFileName}. {exception.Message}");
        }

        LoadScenes();
    }

    private void LoadActs(StoryActDef[] acts)
    {
        for (int actIndex = 0; actIndex < acts.Length; actIndex++)
        {
            StoryActDef act = acts[actIndex];
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
                StoryWorldDef world = act.worlds[worldIndex];
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

                if (world.encounters == null || world.encounters.Length == 0)
                {
                    continue;
                }

                for (int encounterIndex = 0; encounterIndex < world.encounters.Length; encounterIndex++)
                {
                    StoryEncounterDef encounter = world.encounters[encounterIndex];
                    if (encounter == null || string.IsNullOrWhiteSpace(encounter.id))
                    {
                        continue;
                    }

                    encounter.parentActId = act.id;
                    encounter.parentWorldId = world.id;
                    encounter.orderIndex = encounterIndex;
                    _encounters.Add(encounter);

                    if (!_encountersById.ContainsKey(encounter.id))
                    {
                        _encountersById.Add(encounter.id, encounter);
                    }
                }
            }
        }
    }

    private void LoadLegacyNodes(StoryNodeDef[] nodes)
    {
        StoryActDef legacyAct = new StoryActDef
        {
            id = LegacyActId,
            title = "Story",
        };

        StoryWorldDef legacyWorld = new StoryWorldDef
        {
            id = LegacyWorldId,
            title = "Story",
            isAuthored = true,
            encounters = new StoryEncounterDef[nodes.Length],
        };

        for (int index = 0; index < nodes.Length; index++)
        {
            StoryNodeDef node = nodes[index];
            if (node == null || string.IsNullOrWhiteSpace(node.id))
            {
                continue;
            }

            legacyWorld.encounters[index] = new StoryEncounterDef
            {
                id = node.id,
                title = string.IsNullOrWhiteSpace(node.title) ? node.id : node.title,
                role = "standard",
                enemyDeckCode = node.enemyDeckCode,
                rewardCurrency = node.rewardCurrency,
                rewardPromoCardId = node.rewardPromoCardId,
                prereqEncounterIds = node.prereqNodeIds,
            };
        }

        legacyAct.worlds = new[] { legacyWorld };
        LoadActs(new[] { legacyAct });
    }

    private static string TryReadStoryJson()
    {
        List<string> candidatePaths = GetCandidateStoryPaths(StoryFileName);
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

    private void LoadScenes()
    {
        string json = TryReadScenesJson();
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            StoryDatabaseDef database = JsonUtility.FromJson<StoryDatabaseDef>(json);
            if (database?.scenes == null || database.scenes.Length == 0)
            {
                return;
            }

            for (int index = 0; index < database.scenes.Length; index++)
            {
                StorySceneDef scene = database.scenes[index];
                if (scene == null || string.IsNullOrWhiteSpace(scene.id))
                {
                    continue;
                }

                _scenes.Add(scene);
                if (!_scenesById.ContainsKey(scene.id))
                {
                    _scenesById.Add(scene.id, scene);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"StoryDatabase: Failed to parse {StoryScenesFileName}. {exception.Message}");
        }
    }

    private static string TryReadScenesJson()
    {
        List<string> candidatePaths = GetCandidateStoryPaths(StoryScenesFileName);
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

    private static List<string> GetCandidateStoryPaths(string fileName)
    {
        List<string> paths = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddPath(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            string fullPath = NormalizePath(Path.Combine(root, fileName));
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
