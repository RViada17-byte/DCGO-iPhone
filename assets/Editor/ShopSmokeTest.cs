#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ShopSmokeTest
{
    private const string OpeningScenePath = "Assets/Scenes/Opening.unity";
    private const string StructureDeckProductId = "st1-structure";
    private const string BoosterPackProductId = "bt1-pack";
    private const string PromoPackProductId = "promo-pack";

    private static bool _shopOpened;
    private static bool _exiting;
    private static double _phaseStartTime;

    [MenuItem("Build/DCGO/Run Shop Smoke Test")]
    public static void Run()
    {
        Cleanup();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Fail("Cancelled before opening the smoke test scene.");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            Fail("Smoke test started while the editor was already in play mode.");
            return;
        }

        EditorSceneManager.OpenScene(OpeningScenePath, OpenSceneMode.Single);

        _shopOpened = false;
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

            if (!_shopOpened)
            {
                if (!TryOpenShop())
                {
                    if (EditorApplication.timeSinceStartup - _phaseStartTime > 90d)
                    {
                        Fail("Timed out waiting for the opening scene to initialize.");
                    }

                    return;
                }

                _shopOpened = true;
                _phaseStartTime = EditorApplication.timeSinceStartup;
                return;
            }

            if (!TryRunAssertions())
            {
                if (EditorApplication.timeSinceStartup - _phaseStartTime > 90d)
                {
                    Fail("Timed out waiting for the runtime shop UI.");
                }

                return;
            }

            Succeed("Shop smoke test passed.");
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

        ProgressionManager.Instance.ResetProfileForDev();
        router.OpenShop();
        return true;
    }

    private static bool TryRunAssertions()
    {
        ShopPanel shopPanel = UnityEngine.Object.FindObjectOfType<ShopPanel>(true);
        if (shopPanel == null)
        {
            return false;
        }

        Transform structureRows = shopPanel.transform.Find("ShopRuntimeBody/ScrollRoot/Viewport/Content/StructureDeckRows");
        Transform packRows = shopPanel.transform.Find("ShopRuntimeBody/ScrollRoot/Viewport/Content/PackRows");

        if (structureRows == null || packRows == null)
        {
            return false;
        }

        int expectedStructureCount = ShopCatalogDatabase.Instance.Products.Count(product => product != null && product.IsStructureDeck);
        int expectedPackCount = ShopCatalogDatabase.Instance.Products.Count(product => product != null && product.IsPack);

        if (structureRows.childCount != expectedStructureCount)
        {
            Fail($"Expected {expectedStructureCount} structure deck rows, found {structureRows.childCount}.");
            return false;
        }

        if (packRows.childCount != expectedPackCount)
        {
            Fail($"Expected {expectedPackCount} booster pack rows, found {packRows.childCount}.");
            return false;
        }

        ShopPurchaseResult structureDeckResult = ShopService.Purchase(StructureDeckProductId);
        if (!structureDeckResult.Succeeded)
        {
            Fail($"Structure deck purchase failed: {structureDeckResult.Message}");
            return false;
        }

        if (!ProgressionManager.Instance.HasPurchasedProduct(StructureDeckProductId))
        {
            Fail("Structure deck purchase did not persist to the profile.");
            return false;
        }

        bool hasPremadeDeck = ContinuousController.instance.DeckDatas.Any(deckData =>
            deckData != null &&
            string.Equals(deckData.DeckID, "shop-st1-structure", StringComparison.OrdinalIgnoreCase));
        if (!hasPremadeDeck)
        {
            Fail("Structure deck purchase did not create the premade deck.");
            return false;
        }

        ShopPurchaseResult packResult = ShopService.Purchase(BoosterPackProductId);
        if (!packResult.Succeeded)
        {
            Fail($"Booster pack purchase failed: {packResult.Message}");
            return false;
        }

        if (packResult.Lines == null || packResult.Lines.Count != 12)
        {
            Fail($"Expected 12 pack result lines, found {packResult.Lines?.Count ?? 0}.");
            return false;
        }

        int newCount = packResult.Lines.Count(line => line.StartsWith("NEW: ", StringComparison.Ordinal));
        if (newCount < 5)
        {
            Fail($"Expected at least 5 NEW pulls, found {newCount}.");
            return false;
        }

        ProgressionManager.Instance.AddCurrency(1000);

        ShopPurchaseResult promoResult = ShopService.Purchase(PromoPackProductId);
        if (!promoResult.Succeeded)
        {
            Fail($"Promo pack purchase failed: {promoResult.Message}");
            return false;
        }

        if (promoResult.Lines == null || promoResult.Lines.Count != 2)
        {
            Fail($"Expected 2 promo pack result lines, found {promoResult.Lines?.Count ?? 0}.");
            return false;
        }

        int promoNewCount = promoResult.Lines.Count(line => line.StartsWith("NEW: ", StringComparison.Ordinal));
        if (promoNewCount < 1)
        {
            Fail($"Expected at least 1 NEW promo pull, found {promoNewCount}.");
            return false;
        }

        return true;
    }

    private static void Succeed(string message)
    {
        Debug.Log($"ShopSmokeTest: {message}");
        Exit(0);
    }

    private static void Fail(string message)
    {
        Debug.LogError($"ShopSmokeTest: {message}");
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
