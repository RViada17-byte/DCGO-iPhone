#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StartupBootPerfExperiment
{
    const string OpeningScenePath = "Assets/Scenes/Opening.unity";
    const double TimeoutSeconds = 180d;

    static bool _exiting;
    static double _phaseStartTime;

    [MenuItem("Build/DCGO/Run Startup Boot Perf Experiment")]
    public static void Run()
    {
        Cleanup();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Fail("Cancelled before opening the startup perf scene.");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            Fail("Startup perf experiment started while the editor was already in play mode.");
            return;
        }

        EditorSceneManager.OpenScene(OpeningScenePath, OpenSceneMode.Single);

        _phaseStartTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    public static void RunBatch()
    {
        Run();
    }

    static void Tick()
    {
        if (_exiting)
        {
            return;
        }

        try
        {
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - _phaseStartTime > TimeoutSeconds)
                {
                    Fail("Timed out before entering play mode.");
                }

                return;
            }

            if (!StartupPerfTrace.BootCompleted)
            {
                if (EditorApplication.timeSinceStartup - _phaseStartTime > TimeoutSeconds)
                {
                    Fail("Timed out waiting for startup boot trace completion.");
                }

                return;
            }

            Succeed($"Startup boot perf experiment completed. label={StartupPerfTrace.Label} bootMs={StartupPerfTrace.BootElapsedMilliseconds}");
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
        }
    }

    static void Succeed(string message)
    {
        Debug.Log($"StartupBootPerfExperiment: {message}");
        Exit(0);
    }

    static void Fail(string message)
    {
        Debug.LogError($"StartupBootPerfExperiment: {message}");
        Exit(1);
    }

    static void Exit(int exitCode)
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

    static void Cleanup()
    {
        EditorApplication.update -= Tick;
    }
}
#endif
