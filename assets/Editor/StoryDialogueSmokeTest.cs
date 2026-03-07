#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StoryDialogueSmokeTest
{
    private const string OpeningScenePath = "Assets/Scenes/Opening.unity";
    private const string EncounterId = "story.act1.adventure.01_izzy";
    private const string DialogueRootPath = "StoryRuntimeBody/DialogueOverlay";

    private static bool _storyOpened;
    private static bool _encounterSelected;
    private static bool _dialogueSeen;
    private static bool _exiting;
    private static double _phaseStartTime;

    [MenuItem("Build/DCGO/Run Story Dialogue Smoke Test")]
    public static void Run()
    {
        Cleanup();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Fail("Cancelled before opening the story smoke test scene.");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            Fail("Story smoke test started while the editor was already in play mode.");
            return;
        }

        EditorSceneManager.OpenScene(OpeningScenePath, OpenSceneMode.Single);

        _storyOpened = false;
        _encounterSelected = false;
        _dialogueSeen = false;
        _phaseStartTime = EditorApplication.timeSinceStartup;

        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    public static void RunBatch()
    {
        Run();
    }

    private static void Tick()
    {
        if (_exiting)
        {
            return;
        }

        try
        {
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - _phaseStartTime > 90d)
                {
                    Fail("Timed out before entering play mode.");
                }

                return;
            }

            if (!_storyOpened)
            {
                if (!TryOpenStory())
                {
                    if (EditorApplication.timeSinceStartup - _phaseStartTime > 90d)
                    {
                        Fail("Timed out waiting for the opening scene to initialize.");
                    }

                    return;
                }

                _storyOpened = true;
                _phaseStartTime = EditorApplication.timeSinceStartup;
                return;
            }

            if (!_encounterSelected)
            {
                if (!TrySelectIzzyEncounter())
                {
                    if (EditorApplication.timeSinceStartup - _phaseStartTime > 90d)
                    {
                        Fail("Timed out waiting for Story Mode to initialize.");
                    }

                    return;
                }

                _encounterSelected = true;
                _phaseStartTime = EditorApplication.timeSinceStartup;
                return;
            }

            if (TryAdvanceDialogueIntoBattle())
            {
                Succeed("Izzy story dialogue advanced into a story battle successfully.");
                return;
            }

            if (EditorApplication.timeSinceStartup - _phaseStartTime > 90d)
            {
                Fail("Timed out waiting for Izzy dialogue to trigger the story battle.");
            }
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
        }
    }

    private static bool TryOpenStory()
    {
        if (Opening.instance == null || ContinuousController.instance == null)
        {
            return false;
        }

        MainMenuRouter router = UnityEngine.Object.FindObjectOfType<MainMenuRouter>(true);
        if (router == null || router.storyModeRoot == null)
        {
            return false;
        }

        ProgressionManager.Instance.ResetProfileForDev();
        router.OpenStory();
        return true;
    }

    private static bool TrySelectIzzyEncounter()
    {
        StoryPanel storyPanel = UnityEngine.Object.FindObjectOfType<StoryPanel>(true);
        if (storyPanel == null)
        {
            return false;
        }

        StoryEncounterDef encounter = StoryDatabase.Instance.GetEncounter(EncounterId);
        if (encounter == null)
        {
            Fail($"Could not find encounter '{EncounterId}'.");
            return false;
        }

        MethodInfo handler = typeof(StoryPanel).GetMethod("OnEncounterSelected", BindingFlags.Instance | BindingFlags.NonPublic);
        if (handler == null)
        {
            Fail("StoryPanel.OnEncounterSelected reflection lookup failed.");
            return false;
        }

        handler.Invoke(storyPanel, new object[] { encounter });
        return true;
    }

    private static bool TryAdvanceDialogueIntoBattle()
    {
        StoryPanel storyPanel = UnityEngine.Object.FindObjectOfType<StoryPanel>(true);
        if (storyPanel == null)
        {
            return false;
        }

        Transform dialogueRoot = storyPanel.transform.Find(DialogueRootPath);
        if (dialogueRoot != null && dialogueRoot.gameObject.activeInHierarchy)
        {
            _dialogueSeen = true;

            MethodInfo advance = typeof(StoryPanel).GetMethod("AdvanceDialogueScene", BindingFlags.Instance | BindingFlags.NonPublic);
            if (advance == null)
            {
                Fail("StoryPanel.AdvanceDialogueScene reflection lookup failed.");
                return false;
            }

            advance.Invoke(storyPanel, null);
        }

        if (!_dialogueSeen)
        {
            return false;
        }

        GameSessionContext session = GameSessionContext.Instance;
        return ContinuousController.instance != null &&
               ContinuousController.instance.isAI &&
               session != null &&
               session.Mode == SessionMode.Story &&
               string.Equals(session.ContentId, EncounterId, StringComparison.Ordinal);
    }

    private static void Succeed(string message)
    {
        Debug.Log($"StoryDialogueSmokeTest: {message}");
        Exit(0);
    }

    private static void Fail(string message)
    {
        Debug.LogError($"StoryDialogueSmokeTest: {message}");
        Exit(1);
    }

    private static void Exit(int exitCode)
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        Cleanup();

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }

        EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
    }

    private static void Cleanup()
    {
        EditorApplication.update -= Tick;
    }
}
#endif
