#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ShopSmokeTest
{
    private const string OpeningScenePath = "Assets/Scenes/Opening.unity";
    private const string StructureDeckProductId = "st1-structure";
    private const string StarterSetProductId = "st7-set";
    private const string StarterSetPrereqNodeId = "story.act1.frontier.06_takuya";
    private const string BoosterPackProductId = "bt1-pack";
    private const string PromoPackProductId = "promo-pack";
    private const string StructureDeckDialogTitle = "Deck Purchase Results";
    private const string PackDialogTitle = "Pack Open Results";

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
        Transform starterSetRows = shopPanel.transform.Find("ShopRuntimeBody/ScrollRoot/Viewport/Content/StarterSetRows");
        Transform packRows = shopPanel.transform.Find("ShopRuntimeBody/ScrollRoot/Viewport/Content/PackRows");

        if (structureRows == null || starterSetRows == null || packRows == null)
        {
            return false;
        }

        AssertSectionOrder(structureRows, GetExpectedProductIds(ShopProductDisplayGroup.StructureDecks), "structure deck");
        AssertSectionOrder(starterSetRows, GetExpectedProductIds(ShopProductDisplayGroup.StarterSets), "starter set");
        AssertSectionOrder(packRows, GetExpectedProductIds(ShopProductDisplayGroup.BoosterPacks), "booster pack");

        ShopPurchaseResult structureDeckResult = PurchaseViaUi(shopPanel, StructureDeckProductId);
        AssertSuccess(structureDeckResult, "structure deck");
        AssertCardResults(structureDeckResult, expectedCount: 16, context: "structure deck");
        AssertDialog(shopPanel, StructureDeckDialogTitle, "ST1-01 -");

        if (!ProgressionManager.Instance.HasPurchasedProduct(StructureDeckProductId))
        {
            Fail("Structure deck purchase did not persist to the profile.");
        }

        AssertStructureDeckCount("shop-st1-structure", 1, "Structure deck purchase did not create exactly one premade deck.");

        Transform structureDeckRow = FindProductRow(structureRows, StructureDeckProductId);
        Button structureDeckButton = structureDeckRow != null ? structureDeckRow.Find("BuyButton")?.GetComponent<Button>() : null;
        if (structureDeckButton == null || structureDeckButton.interactable)
        {
            Fail("Owned structure deck row should not remain purchasable.");
        }

        int currencyAfterFirstDeckPurchase = ProgressionManager.Instance.GetCurrency();
        ShopPurchaseResult duplicateStructureDeckResult = ShopService.Purchase(StructureDeckProductId);
        if (duplicateStructureDeckResult.Succeeded || !string.Equals(duplicateStructureDeckResult.Message, "Already purchased.", StringComparison.Ordinal))
        {
            Fail("Second structure deck purchase should fail with 'Already purchased.'.");
        }

        if (ProgressionManager.Instance.GetCurrency() != currencyAfterFirstDeckPurchase)
        {
            Fail("Second structure deck purchase attempt spent currency.");
        }

        AssertStructureDeckCount("shop-st1-structure", 1, "Second structure deck purchase created a duplicate deck.");

        ProgressionManager.Instance.MarkStoryCompleted(StarterSetPrereqNodeId);
        shopPanel.RefreshView();

        ShopPurchaseResult starterSetResult = PurchaseViaUi(shopPanel, StarterSetProductId);
        AssertSuccess(starterSetResult, "starter set");
        AssertCardResults(starterSetResult, expectedCount: PackService.GetUniqueCardCountForSet("ST7"), context: "starter set");
        AssertDialog(shopPanel, PackDialogTitle, "ST7-");

        if (!ProgressionManager.Instance.HasPurchasedProduct(StarterSetProductId))
        {
            Fail("Starter set purchase did not persist to the profile.");
        }

        Transform starterSetRow = FindProductRow(starterSetRows, StarterSetProductId);
        Button starterSetButton = starterSetRow != null ? starterSetRow.Find("BuyButton")?.GetComponent<Button>() : null;
        if (starterSetButton == null || starterSetButton.interactable)
        {
            Fail("Owned starter set row should not remain purchasable.");
        }

        ProgressionManager.Instance.AddCurrency(2500);
        shopPanel.RefreshView();

        ShopPurchaseResult packResult = PurchaseViaUi(shopPanel, BoosterPackProductId);
        AssertSuccess(packResult, "booster pack");
        AssertCardResults(packResult, expectedCount: 12, context: "booster pack");
        AssertDialog(shopPanel, PackDialogTitle, "BT1-");

        Transform boosterPackRow = FindProductRow(packRows, BoosterPackProductId);
        Button boosterPackButton = boosterPackRow != null ? boosterPackRow.Find("BuyButton")?.GetComponent<Button>() : null;
        if (boosterPackButton == null || !boosterPackButton.interactable)
        {
            Fail("Repeatable booster pack row should remain purchasable.");
        }

        ShopPurchaseResult secondPackResult = ShopService.Purchase(BoosterPackProductId);
        AssertSuccess(secondPackResult, "second booster pack");
        AssertCardResults(secondPackResult, expectedCount: 12, context: "second booster pack");

        ShopPurchaseResult promoResult = ShopService.Purchase(PromoPackProductId);
        AssertSuccess(promoResult, "promo pack");
        AssertCardResults(promoResult, expectedCount: 2, context: "promo pack");

        ShopPurchaseResult secondPromoResult = ShopService.Purchase(PromoPackProductId);
        AssertSuccess(secondPromoResult, "second promo pack");
        AssertCardResults(secondPromoResult, expectedCount: 2, context: "second promo pack");

        return true;
    }

    private static ShopPurchaseResult PurchaseViaUi(ShopPanel shopPanel, string productId)
    {
        if (shopPanel == null)
        {
            Fail("Shop panel was null while attempting a UI purchase.");
        }

        Transform row = FindProductRow(shopPanel.transform, productId);
        if (row == null)
        {
            Fail($"Could not find shop row for '{productId}'.");
        }

        Button buyButton = row.Find("BuyButton")?.GetComponent<Button>();
        if (buyButton == null)
        {
            Fail($"Shop row '{productId}' is missing its buy button.");
        }

        if (!buyButton.interactable)
        {
            Fail($"Shop row '{productId}' was unexpectedly not interactable.");
        }

        buyButton.onClick.Invoke();

        if (shopPanel.LastPurchaseResult == null)
        {
            Fail($"Shop row '{productId}' did not populate LastPurchaseResult.");
        }

        return shopPanel.LastPurchaseResult;
    }

    private static void AssertSuccess(ShopPurchaseResult result, string context)
    {
        if (result == null || !result.Succeeded)
        {
            Fail($"{context} purchase failed: {result?.Message ?? "missing result"}");
        }
    }

    private static void AssertCardResults(ShopPurchaseResult result, int expectedCount, string context)
    {
        if (result?.CardResults == null || result.CardResults.Count != expectedCount)
        {
            Fail($"Expected {expectedCount} card results for {context}, found {result?.CardResults?.Count ?? 0}.");
        }

        for (int index = 0; index < result.CardResults.Count; index++)
        {
            ShopPurchaseCardResult cardResult = result.CardResults[index];
            if (cardResult == null ||
                string.IsNullOrWhiteSpace(cardResult.CardId) ||
                string.IsNullOrWhiteSpace(cardResult.CardName) ||
                cardResult.Count <= 0)
            {
                Fail($"{context} purchase produced an invalid structured card result at index {index}.");
            }
        }
    }

    private static void AssertDialog(ShopPanel shopPanel, string expectedTitle, string requiredBodyFragment)
    {
        if (shopPanel == null || !shopPanel.IsPurchaseResultsDialogOpen)
        {
            Fail("Expected the purchase results dialog to be open.");
        }

        if (!string.Equals(shopPanel.ActiveResultsDialogTitle, expectedTitle, StringComparison.Ordinal))
        {
            Fail($"Expected purchase results dialog title '{expectedTitle}', found '{shopPanel.ActiveResultsDialogTitle}'.");
        }

        if (string.IsNullOrWhiteSpace(shopPanel.ActiveResultsDialogBody) ||
            shopPanel.ActiveResultsDialogBody.IndexOf(requiredBodyFragment, StringComparison.OrdinalIgnoreCase) < 0)
        {
            Fail($"Expected purchase results dialog body to contain '{requiredBodyFragment}'.");
        }
    }

    private static void AssertSectionOrder(Transform sectionRoot, IReadOnlyList<string> expectedProductIds, string label)
    {
        if (sectionRoot == null)
        {
            Fail($"Missing {label} section.");
        }

        if (sectionRoot.childCount != expectedProductIds.Count)
        {
            Fail($"Expected {expectedProductIds.Count} {label} rows, found {sectionRoot.childCount}.");
        }

        for (int index = 0; index < expectedProductIds.Count; index++)
        {
            string expectedRowName = expectedProductIds[index] + "_Row";
            string actualRowName = sectionRoot.GetChild(index).name;
            if (!string.Equals(actualRowName, expectedRowName, StringComparison.Ordinal))
            {
                Fail($"Expected {label} row {index} to be '{expectedRowName}', found '{actualRowName}'.");
            }
        }
    }

    private static List<string> GetExpectedProductIds(ShopProductDisplayGroup group)
    {
        List<string> ids = new List<string>();
        IReadOnlyList<ShopProductDef> products = ShopCatalogDatabase.Instance.Products;

        for (int index = 0; index < products.Count; index++)
        {
            ShopProductDef product = products[index];
            if (product == null || product.DisplayGroup != group || (group == ShopProductDisplayGroup.BoosterPacks && IsPromoProduct(product)))
            {
                continue;
            }

            ids.Add(product.id);
        }

        if (group == ShopProductDisplayGroup.BoosterPacks)
        {
            for (int index = 0; index < products.Count; index++)
            {
                ShopProductDef product = products[index];
                if (product != null && product.DisplayGroup == group && IsPromoProduct(product))
                {
                    ids.Add(product.id);
                }
            }
        }

        return ids;
    }

    private static bool IsPromoProduct(ShopProductDef product)
    {
        if (product == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(product.setId) &&
            string.Equals(product.setId.Trim(), "P", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(product.id) &&
            product.id.IndexOf("promo", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Transform FindProductRow(Transform root, string productId)
    {
        if (root == null || string.IsNullOrWhiteSpace(productId))
        {
            return null;
        }

        if (string.Equals(root.name, productId + "_Row", StringComparison.Ordinal))
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform match = FindProductRow(root.GetChild(index), productId);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void AssertStructureDeckCount(string deckId, int expectedCount, string failureMessage)
    {
        int matchingDeckCount = ContinuousController.instance.DeckDatas.Count(deckData =>
            deckData != null &&
            string.Equals(deckData.DeckID, deckId, StringComparison.OrdinalIgnoreCase));

        if (matchingDeckCount != expectedCount)
        {
            Fail($"{failureMessage} Expected {expectedCount}, found {matchingDeckCount}.");
        }
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
