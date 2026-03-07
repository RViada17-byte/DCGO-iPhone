#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class StoryDataValidation
{
    private const string IzzyEncounterId = "story.act1.adventure.01_izzy";

    [MenuItem("Build/DCGO/Validate Story Data")]
    public static void ValidateMenu()
    {
        Validate();
    }

    public static void ValidateBatch()
    {
        try
        {
            Validate();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"StoryDataValidation failed: {exception}");
            EditorApplication.Exit(1);
        }
    }

    private static void Validate()
    {
        StoryDatabase.Instance.Reload();

        StoryEncounterDef encounter = StoryDatabase.Instance.GetEncounter(IzzyEncounterId);
        if (encounter == null)
        {
            throw new InvalidOperationException($"Missing encounter '{IzzyEncounterId}'.");
        }

        if (string.IsNullOrWhiteSpace(encounter.preDuelSceneId))
        {
            throw new InvalidOperationException($"Encounter '{IzzyEncounterId}' is missing pre-duel scene wiring.");
        }

        StorySceneDef scene = StoryDatabase.Instance.GetScene(encounter.preDuelSceneId);
        if (scene == null)
        {
            throw new InvalidOperationException($"Could not resolve scene '{encounter.preDuelSceneId}' for encounter '{IzzyEncounterId}'.");
        }

        if (scene.lines == null || scene.lines.Length == 0)
        {
            throw new InvalidOperationException($"Scene '{scene.id}' has no dialogue lines.");
        }

        Debug.Log($"StoryDataValidation passed. Scenes={StoryDatabase.Instance.Scenes.Count}, IzzyPreScene={scene.id}, Lines={scene.lines.Length}");
    }
}
#endif
