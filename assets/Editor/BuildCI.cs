#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildCI
{
    [MenuItem("Build/DCGO/Build iOS")]
    public static void BuildIOS()
    {
        string outputPath = Environment.GetEnvironmentVariable("DCGO_IOS_BUILD_PATH");
        // iPhone exports are expected to boot straight into offline local mode by default.
        bool offlineBoot = GetBoolEnv("DCGO_OFFLINE_BOOT", defaultValue: true);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = "Builds/iOS";
        }

        if (!Path.IsPathRooted(outputPath))
        {
            outputPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputPath));
        }

        Directory.CreateDirectory(outputPath);

        ConfigureIOSPlayerSettings();
        ConfigureScriptingDefines(offlineBoot);

        string[] scenes = GetBuildScenes(offlineBoot);

        if (scenes.Length == 0)
        {
            throw new Exception("No valid scenes found for iOS build.");
        }

        var buildOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            target = BuildTarget.iOS,
            locationPathName = outputPath,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"iOS build failed. Result={summary.result}, Errors={summary.totalErrors}, Output={outputPath}");
        }

        Debug.Log($"iOS build succeeded at: {outputPath} (offlineBoot={offlineBoot})");
    }

    static void ConfigureIOSPlayerSettings()
    {
        string sdk = Environment.GetEnvironmentVariable("DCGO_IOS_SDK");
        bool buildForSimulator = !string.IsNullOrWhiteSpace(sdk) &&
                                 sdk.Trim().Equals("simulator", StringComparison.OrdinalIgnoreCase);
        string appleTeamId = Environment.GetEnvironmentVariable("DCGO_APPLE_TEAM_ID");
        if (string.IsNullOrWhiteSpace(appleTeamId))
        {
            appleTeamId = "Q2N8478U8C";
        }
        else
        {
            appleTeamId = appleTeamId.Trim();
        }

        PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1); // ARM64
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.sdkVersion = buildForSimulator
            ? iOSSdkVersion.SimulatorSDK
            : iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.appleDeveloperTeamID = appleTeamId;
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.stripEngineCode = true;

        Debug.Log($"Configured iOS build SDK={PlayerSettings.iOS.sdkVersion} ARM64 IL2CPP team={appleTeamId}");
    }

    static void ConfigureScriptingDefines(bool offlineBoot)
    {
        const BuildTargetGroup targetGroup = BuildTargetGroup.iOS;
        const string define = "DCGO_OFFLINE_BOOT";

        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
        bool hasDefine = defines.Split(';').Contains(define);

        if (offlineBoot && !hasDefine)
        {
            defines = string.IsNullOrWhiteSpace(defines) ? define : $"{defines};{define}";
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
        }
        else if (!offlineBoot && hasDefine)
        {
            string updated = string.Join(";", defines.Split(';').Where(symbol => symbol != define && !string.IsNullOrWhiteSpace(symbol)));
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, updated);
        }

        Debug.Log($"Configured iOS scripting defines: {PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup)}");
    }

    static string[] GetBuildScenes(bool offlineBoot)
    {
        if (offlineBoot)
        {
            string[] offlineScenes = new[]
            {
                "Assets/Scenes/Opening.unity",
                "Assets/Scenes/ContinuousControllerScene.unity",
                "Assets/Scenes/BattleScene.unity",
                "Assets/Scenes/GetCardImage.unity",
            }.Where(File.Exists).ToArray();

            if (offlineScenes.Length == 0)
            {
                throw new Exception("Offline boot requested, but BattleScene was not found.");
            }

            Debug.Log($"Using offline boot scenes: {string.Join(", ", offlineScenes)}");
            return offlineScenes;
        }

        string[] enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .Where(File.Exists)
            .ToArray();

        if (enabledScenes.Length > 0)
        {
            return enabledScenes;
        }

        string[] fallbackScenes = new[]
        {
            "Assets/Scenes/ContinuousControllerScene.unity",
            "Assets/Scenes/Opening.unity",
            "Assets/Scenes/GetCardImage.unity",
            "Assets/Scenes/BattleScene.unity",
        }.Where(File.Exists).ToArray();

        if (fallbackScenes.Length > 0)
        {
            var editorScenes = fallbackScenes.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            EditorBuildSettings.scenes = editorScenes;
            Debug.Log($"EditorBuildSettings was empty. Applied fallback scenes: {string.Join(", ", fallbackScenes)}");
        }

        return fallbackScenes;
    }

    static bool GetBoolEnv(string name, bool defaultValue = false)
    {
        string value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        value = value.Trim();
        return value == "1" ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
#endif
