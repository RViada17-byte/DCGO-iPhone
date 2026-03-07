using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Writes minimal startup diagnostics to persistent data so device-only hangs
/// can be inspected without Xcode.
/// </summary>
public static class StartupTrace
{
    static readonly object LockObject = new object();
    static string _logPath;
    static bool _initialized;
    static bool _isWriting;
    static StartupTraceRunner _runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        try
        {
            string dir = Application.persistentDataPath;
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "startup-trace.log");
            File.WriteAllText(_logPath, string.Empty);

            Write("StartupTrace initialized");
            Write($"UnityVersion={Application.unityVersion}");
            Write($"Platform={Application.platform}");
            Write($"PersistentDataPath={Application.persistentDataPath}");
            Write($"TemporaryCachePath={Application.temporaryCachePath}");

            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Application.quitting += OnQuitting;

            GameObject runnerObject = new GameObject("StartupTraceRunner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            _runner = runnerObject.AddComponent<StartupTraceRunner>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"StartupTrace init failed: {ex}");
        }
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Write($"SceneLoaded name={scene.name} buildIndex={scene.buildIndex} mode={mode}");
    }

    static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        Write($"ActiveSceneChanged old={oldScene.name}({oldScene.buildIndex}) new={newScene.name}({newScene.buildIndex})");
    }

    static void OnQuitting()
    {
        Write("Application.quitting");
    }

    static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        string line = condition ?? string.Empty;
        if (line.Length > 400)
        {
            line = line.Substring(0, 400);
        }

        Write($"UnityLog [{type}] {line}");
    }

    static void Write(string message)
    {
        if (_isWriting || string.IsNullOrEmpty(_logPath))
        {
            return;
        }

        try
        {
            lock (LockObject)
            {
                _isWriting = true;
                File.AppendAllText(_logPath, $"{DateTime.UtcNow:O} {message}\n");
            }
        }
        catch
        {
            // Ignore write errors to avoid impacting runtime flow.
        }
        finally
        {
            _isWriting = false;
        }
    }

    sealed class StartupTraceRunner : MonoBehaviour
    {
        bool _isPaused;

        void OnApplicationPause(bool pauseStatus)
        {
            _isPaused = pauseStatus;
            Write($"OnApplicationPause pause={pauseStatus}");
        }

        IEnumerator Start()
        {
            for (int i = 0; i < 10; i++)
            {
                WriteSnapshot(i);
                yield return new WaitForSeconds(2f);
            }
        }

        void WriteSnapshot(int index)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            string[] rootNames = Array.ConvertAll(activeScene.GetRootGameObjects(), go => go.name);
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Write(
                "Snapshot " + index +
                $" scene={activeScene.name}({activeScene.buildIndex})" +
                $" focused={Application.isFocused}" +
                $" paused={_isPaused}" +
                $" screen={Screen.width}x{Screen.height}" +
                $" orientation={Screen.orientation}" +
                $" cameraCount={cameras.Length}" +
                $" canvasCount={canvases.Length}");

            for (int i = 0; i < cameras.Length && i < 6; i++)
            {
                Camera cam = cameras[i];
                if (cam == null)
                {
                    continue;
                }

                Write(
                    $"Camera[{i}] name={cam.name} enabled={cam.enabled} active={cam.gameObject.activeInHierarchy} " +
                    $"depth={cam.depth} cullMask={cam.cullingMask} clear={cam.clearFlags}");
            }

            for (int i = 0; i < canvases.Length && i < 6; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                {
                    continue;
                }

                Write(
                    $"Canvas[{i}] name={canvas.name} enabled={canvas.enabled} active={canvas.gameObject.activeInHierarchy} " +
                    $"sortingOrder={canvas.sortingOrder} renderMode={canvas.renderMode}");
            }

            if (rootNames.Length > 0)
            {
                Write("RootObjects " + string.Join(", ", rootNames));
            }
        }
    }
}
