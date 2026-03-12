using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LandscapeOrientationBootstrap : MonoBehaviour
{
    static bool _installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (_installed || !Application.isMobilePlatform)
        {
            return;
        }

        _installed = true;
        ApplyLandscapeLock();

        GameObject root = new GameObject(nameof(LandscapeOrientationBootstrap));
        DontDestroyOnLoad(root);
        root.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;
        root.AddComponent<LandscapeOrientationBootstrap>();
    }

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ReinforceLandscapeLock();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _installed = false;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            ReinforceLandscapeLock();
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            ReinforceLandscapeLock();
        }
    }

    void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        ReinforceLandscapeLock();
    }

    void ReinforceLandscapeLock()
    {
        ApplyLandscapeLock();
        StopAllCoroutines();
        StartCoroutine(RestoreLandscapeAutorotationNextFrame());
    }

    static void ApplyLandscapeLock()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        if (Screen.height > Screen.width ||
            Screen.orientation == ScreenOrientation.Portrait ||
            Screen.orientation == ScreenOrientation.PortraitUpsideDown)
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            return;
        }

        Screen.orientation = ScreenOrientation.AutoRotation;
    }

    IEnumerator RestoreLandscapeAutorotationNextFrame()
    {
        yield return null;
        ApplyLandscapeLock();
    }
}
