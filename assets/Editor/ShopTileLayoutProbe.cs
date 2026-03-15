#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ShopTileLayoutProbe
{
    private const string OpeningScenePath = "Assets/Scenes/Opening.unity";
    private const string ReportPath = "Logs/ShopTileLayoutProbe.txt";
    private static bool _openedShop;
    private static double _phaseStartTime;
    private static bool _exiting;

    [MenuItem("Build/DCGO/Probe Shop Tile Layout")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        Cleanup();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Fail("Cancelled before opening the probe scene.");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            Fail("Probe started while already in play mode.");
            return;
        }

        EditorSceneManager.OpenScene(OpeningScenePath, OpenSceneMode.Single);
        _openedShop = false;
        _phaseStartTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
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

            if (!_openedShop)
            {
                if (!TryOpenShop())
                {
                    if (EditorApplication.timeSinceStartup - _phaseStartTime > 90d)
                    {
                        Fail("Timed out waiting for the opening scene to initialize.");
                    }

                    return;
                }

                _openedShop = true;
                _phaseStartTime = EditorApplication.timeSinceStartup;
                return;
            }

            if (EditorApplication.timeSinceStartup - _phaseStartTime < 0.5d)
            {
                return;
            }

            DumpLayout();
            Succeed("Shop tile layout probe complete.");
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
        }
    }

    private static bool TryOpenShop()
    {
        if (Opening.instance == null || ContinuousController.instance == null)
        {
            return false;
        }

        MainMenuRouter router = UnityEngine.Object.FindObjectOfType<MainMenuRouter>(true);
        if (router == null || router.shopModeRoot == null)
        {
            return false;
        }

        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.SetResolution(2732, 2048, false);
        router.OpenShop();
        return true;
    }

    private static void DumpLayout()
    {
        ShopPanel shopPanel = UnityEngine.Object.FindObjectOfType<ShopPanel>(true);
        if (shopPanel == null)
        {
            Fail("ShopPanel not found.");
            return;
        }

        Canvas.ForceUpdateCanvases();

        Transform btGrid = shopPanel.transform.Find("ShopRuntimeBody/ScrollRoot/Viewport/Content/BTGrid");
        if (btGrid == null || btGrid.childCount == 0)
        {
            Fail("BTGrid or its children were not found.");
            return;
        }

        RectTransform tile = btGrid.GetChild(0) as RectTransform;
        if (tile == null)
        {
            Fail("First BT tile is missing its RectTransform.");
            return;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[ShopTileLayoutProbe] Runtime tile hierarchy:");
        AppendNodeReport(report, tile, depth: 0);
        string absoluteReportPath = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteReportPath) ?? ".");
        File.WriteAllText(absoluteReportPath, report.ToString());
        report.AppendLine("[ShopTileLayoutProbe] Report written to: " + absoluteReportPath);
        Debug.Log(report.ToString());
    }

    private static void AppendNodeReport(StringBuilder report, RectTransform node, int depth)
    {
        if (node == null)
        {
            return;
        }

        string indent = new string(' ', depth * 2);
        LayoutElement layoutElement = node.GetComponent<LayoutElement>();
        VerticalLayoutGroup vlg = node.GetComponent<VerticalLayoutGroup>();
        HorizontalLayoutGroup hlg = node.GetComponent<HorizontalLayoutGroup>();
        GridLayoutGroup glg = node.GetComponent<GridLayoutGroup>();
        ContentSizeFitter fitter = node.GetComponent<ContentSizeFitter>();
        AspectRatioFitter aspect = node.GetComponent<AspectRatioFitter>();
        Image image = node.GetComponent<Image>();
        Text text = node.GetComponent<Text>();
        Button button = node.GetComponent<Button>();

        report.Append(indent)
            .Append(node.name)
            .Append(" rect=").Append(VectorToString(node.rect.size))
            .Append(" anchored=").Append(VectorToString(node.anchoredPosition))
            .Append(" offsets=(").Append(VectorToString(node.offsetMin)).Append(" .. ").Append(VectorToString(node.offsetMax)).Append(")");

        if (layoutElement != null)
        {
            report.Append(" LE[pref=").Append(layoutElement.preferredHeight)
                .Append(",min=").Append(layoutElement.minHeight)
                .Append(",flex=").Append(layoutElement.flexibleHeight)
                .Append("]");
        }

        if (vlg != null)
        {
            report.Append(" VLG[spacing=").Append(vlg.spacing)
                .Append(",padding=").Append(vlg.padding.left).Append("/")
                .Append(vlg.padding.right).Append("/")
                .Append(vlg.padding.top).Append("/")
                .Append(vlg.padding.bottom).Append("]");
        }

        if (hlg != null)
        {
            report.Append(" HLG[spacing=").Append(hlg.spacing).Append("]");
        }

        if (glg != null)
        {
            report.Append(" GLG[cell=").Append(VectorToString(glg.cellSize))
                .Append(",spacing=").Append(VectorToString(glg.spacing))
                .Append(",constraintCount=").Append(glg.constraintCount).Append("]");
        }

        if (fitter != null)
        {
            report.Append(" CSF[h=").Append(fitter.horizontalFit).Append(",v=").Append(fitter.verticalFit).Append("]");
        }

        if (aspect != null)
        {
            report.Append(" ARF[mode=").Append(aspect.aspectMode).Append(",ratio=").Append(aspect.aspectRatio).Append("]");
        }

        if (image != null)
        {
            report.Append(" IMG[enabled=").Append(image.enabled)
                .Append(",preserveAspect=").Append(image.preserveAspect).Append("]");
        }

        if (text != null)
        {
            report.Append(" TXT[size=").Append(text.fontSize)
                .Append(",text=").Append(text.text).Append("]");
        }

        if (button != null)
        {
            report.Append(" BTN[interactable=").Append(button.interactable).Append("]");
        }

        report.AppendLine();

        for (int index = 0; index < node.childCount; index++)
        {
            AppendNodeReport(report, node.GetChild(index) as RectTransform, depth + 1);
        }
    }

    private static string VectorToString(Vector2 value)
    {
        return $"({value.x:0.##},{value.y:0.##})";
    }

    private static void Succeed(string message)
    {
        Debug.Log("ShopTileLayoutProbe: " + message);
        Exit(0);
    }

    private static void Fail(string message)
    {
        Debug.LogError("ShopTileLayoutProbe: " + message);
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
        _openedShop = false;
        _exiting = false;
    }
}
#endif
