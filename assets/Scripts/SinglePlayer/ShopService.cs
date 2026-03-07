using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class ShopPurchaseResult
{
    public bool Succeeded;
    public string Message;
    public string SummaryLine;
    public List<string> Lines = new List<string>();
}

public class PackPullResult
{
    public CEntity_Base Card;
    public bool WasNew;
}

public static class ShopService
{
    private static readonly Regex ParallelSuffixRegex = new Regex(@"([_-])P\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsProductUnlocked(ShopProductDef product)
    {
        if (product == null || product.prereqStoryNodeIds == null || product.prereqStoryNodeIds.Length == 0)
        {
            return true;
        }

        for (int index = 0; index < product.prereqStoryNodeIds.Length; index++)
        {
            string prereqId = product.prereqStoryNodeIds[index];
            if (string.IsNullOrWhiteSpace(prereqId))
            {
                continue;
            }

            if (!ProgressionManager.Instance.IsStoryCompleted(prereqId))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsProductPurchased(ShopProductDef product)
    {
        return product != null &&
               !product.repeatable &&
               ProgressionManager.Instance.HasPurchasedProduct(product.id);
    }

    public static bool CanPurchase(ShopProductDef product, out string reason)
    {
        reason = string.Empty;

        if (product == null)
        {
            reason = "Product not found.";
            return false;
        }

        if (!IsProductUnlocked(product))
        {
            reason = "Locked by story progress.";
            return false;
        }

        if (IsProductPurchased(product))
        {
            reason = "Already purchased.";
            return false;
        }

        if (ProgressionManager.Instance.GetCurrency() < product.price)
        {
            reason = "Not enough currency.";
            return false;
        }

        return true;
    }

    public static ShopPurchaseResult Purchase(string productId)
    {
        ShopProductDef product = ShopCatalogDatabase.Instance.GetProduct(productId);
        if (product == null)
        {
            return Failure("Product not found.");
        }

        return Purchase(product);
    }

    public static ShopPurchaseResult Purchase(ShopProductDef product)
    {
        if (!CanPurchase(product, out string reason))
        {
            return Failure(reason);
        }

        switch (product.ProductKind)
        {
            case ShopProductKind.StructureDeck:
                return PurchaseStructureDeck(product);

            case ShopProductKind.BoosterPack:
                return PurchasePack(product);

            default:
                return Failure("Unsupported product type.");
        }
    }

    public static void ReconcilePurchasedStructureDecks()
    {
        ContinuousController controller = ContinuousController.instance;
        if (controller == null || controller.CardList == null || controller.CardList.Length == 0)
        {
            return;
        }

        IReadOnlyList<ShopProductDef> products = ShopCatalogDatabase.Instance.Products;
        for (int index = 0; index < products.Count; index++)
        {
            ShopProductDef product = products[index];
            if (product == null || !product.IsStructureDeck || !IsProductPurchased(product))
            {
                continue;
            }

            if (TryBuildStructureDeck(product, out DeckData deckData, out _, out _) && deckData != null)
            {
                EnsureStructureDeckExists(controller, deckData);
            }
        }
    }

    private static ShopPurchaseResult PurchasePack(ShopProductDef product)
    {
        PackRules rules = product.packRules ?? new PackRules();
        List<PackPullResult> pulls = PackService.OpenPack(product.setId, rules);
        if (pulls.Count == 0)
        {
            return Failure($"No cards available for {product.setId}.");
        }

        if (!ProgressionManager.Instance.TrySpendCurrency(product.price, saveImmediately: false))
        {
            return Failure("Not enough currency.");
        }

        HashSet<string> unlockedCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<CEntity_Base> newlyUnlockedCards = new List<CEntity_Base>();
        int ownedPullCount = 0;
        ShopPurchaseResult result = Success(product.repeatable
            ? $"Opened {GetProductTitle(product)}."
            : $"Unlocked {GetProductTitle(product)}.");

        for (int index = 0; index < pulls.Count; index++)
        {
            PackPullResult pull = pulls[index];
            if (pull?.Card == null)
            {
                continue;
            }

            unlockedCardIds.Add(pull.Card.CardID);
            if (pull.WasNew)
            {
                newlyUnlockedCards.Add(pull.Card);
                continue;
            }

            ownedPullCount++;
        }

        result.SummaryLine = BuildUnlockSummary(newlyUnlockedCards, ownedPullCount);

        for (int index = 0; index < newlyUnlockedCards.Count; index++)
        {
            CEntity_Base card = newlyUnlockedCards[index];
            result.Lines.Add($"NEW: {card.CardID}, {GetCardDisplayName(card)}");
        }

        if (ownedPullCount > 0)
        {
            result.Lines.Add($"OWNED PULLS: {ownedPullCount}");
        }

        ProgressionManager.Instance.UnlockCards(unlockedCardIds, saveImmediately: false);
        if (!product.repeatable)
        {
            ProgressionManager.Instance.MarkProductPurchased(product.id, saveImmediately: false);
        }

        ProgressionManager.Instance.Save();
        return result;
    }

    private static ShopPurchaseResult PurchaseStructureDeck(ShopProductDef product)
    {
        ContinuousController controller = ContinuousController.instance;
        if (controller == null || controller.CardList == null || controller.CardList.Length == 0)
        {
            return Failure("Card data is not loaded yet.");
        }

        if (!TryBuildStructureDeck(product, out DeckData deckData, out List<CEntity_Base> uniqueCards, out string error))
        {
            return Failure(error);
        }

        if (!ProgressionManager.Instance.TrySpendCurrency(product.price, saveImmediately: false))
        {
            return Failure("Not enough currency.");
        }

        HashSet<string> unlockedCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<CEntity_Base> newlyUnlockedCards = new List<CEntity_Base>();
        int alreadyOwnedCount = 0;
        ShopPurchaseResult result = Success($"Purchased {GetProductTitle(product)}.");
        result.Lines.Add($"DECK READY: {deckData.DeckName}");

        for (int index = 0; index < uniqueCards.Count; index++)
        {
            CEntity_Base card = uniqueCards[index];
            if (card == null)
            {
                continue;
            }

            bool wasNew = !ProgressionManager.Instance.IsCardUnlocked(card.CardID);
            unlockedCardIds.Add(card.CardID);
            if (wasNew)
            {
                newlyUnlockedCards.Add(card);
                continue;
            }

            alreadyOwnedCount++;
        }

        result.SummaryLine = BuildUnlockSummary(newlyUnlockedCards, alreadyOwnedCount);

        for (int index = 0; index < newlyUnlockedCards.Count; index++)
        {
            CEntity_Base card = newlyUnlockedCards[index];
            result.Lines.Add($"NEW: {card.CardID}, {GetCardDisplayName(card)}");
        }

        if (alreadyOwnedCount > 0)
        {
            result.Lines.Add($"ALREADY OWNED: {alreadyOwnedCount}");
        }

        ProgressionManager.Instance.UnlockCards(unlockedCardIds, saveImmediately: false);
        ProgressionManager.Instance.MarkProductPurchased(product.id, saveImmediately: false);
        EnsureStructureDeckExists(controller, deckData);
        ProgressionManager.Instance.Save();
        return result;
    }

    public static bool TryBuildStructureDeckByProductId(string productId, out DeckData deckData, out string error)
    {
        deckData = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(productId))
        {
            error = "Missing enemy structure deck id.";
            return false;
        }

        ShopProductDef product = ShopCatalogDatabase.Instance.GetProduct(productId);
        if (product == null)
        {
            error = $"Could not find structure deck '{productId}'.";
            return false;
        }

        if (!product.IsStructureDeck)
        {
            error = $"Product '{productId}' is not a structure deck.";
            return false;
        }

        if (!TryBuildStructureDeck(product, out deckData, out _, out error))
        {
            return false;
        }

        return deckData != null;
    }

    public static bool TryBuildDeckByCardIds(string deckName, IEnumerable<string> cardIds, out DeckData deckData, out string error)
    {
        deckData = null;
        error = string.Empty;

        if (cardIds == null)
        {
            error = "Enemy deck is missing card ids.";
            return false;
        }

        List<int> mainDeckCardIndexes = new List<int>();
        List<int> eggDeckCardIndexes = new List<int>();

        foreach (string rawCardId in cardIds)
        {
            if (string.IsNullOrWhiteSpace(rawCardId))
            {
                continue;
            }

            CEntity_Base card = ResolveCard(rawCardId);
            if (card == null)
            {
                error = $"Missing card definition for {rawCardId}.";
                return false;
            }

            if (card.cardKind == CardKind.DigiEgg)
            {
                eggDeckCardIndexes.Add(card.CardIndex);
            }
            else
            {
                mainDeckCardIndexes.Add(card.CardIndex);
            }
        }

        if (mainDeckCardIndexes.Count != 50)
        {
            error = $"{deckName} resolves to {mainDeckCardIndexes.Count} main-deck cards instead of 50.";
            return false;
        }

        if (eggDeckCardIndexes.Count > 5)
        {
            error = $"{deckName} resolves to {eggDeckCardIndexes.Count} digi-eggs instead of 0-5.";
            return false;
        }

        deckData = new DeckData("")
        {
            DeckName = string.IsNullOrWhiteSpace(deckName) ? "Story Duel" : deckName.Trim(),
            DeckID = "story-" + Guid.NewGuid().ToString("N"),
            DeckCardIDs = mainDeckCardIndexes,
            DigitamaDeckCardIDs = eggDeckCardIndexes,
            KeyCardId = mainDeckCardIndexes.Count > 0 ? mainDeckCardIndexes[0] : -1,
        };

        return true;
    }

    private static bool TryBuildStructureDeck(
        ShopProductDef product,
        out DeckData deckData,
        out List<CEntity_Base> uniqueCards,
        out string error)
    {
        deckData = null;
        uniqueCards = new List<CEntity_Base>();
        error = string.Empty;

        if (product?.structureDeckCards == null || product.structureDeckCards.Length == 0)
        {
            error = "Structure deck is missing card definitions.";
            return false;
        }

        List<int> mainDeckCardIndexes = new List<int>();
        List<int> eggDeckCardIndexes = new List<int>();
        HashSet<string> seenCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < product.structureDeckCards.Length; index++)
        {
            StructureDeckCardDef cardDef = product.structureDeckCards[index];
            if (cardDef == null || string.IsNullOrWhiteSpace(cardDef.cardId) || cardDef.count <= 0)
            {
                continue;
            }

            CEntity_Base card = ResolveCard(cardDef.cardId);
            if (card == null)
            {
                error = $"Missing card definition for {cardDef.cardId}.";
                return false;
            }

            if (seenCardIds.Add(card.CardID))
            {
                uniqueCards.Add(card);
            }

            List<int> target = card.cardKind == CardKind.DigiEgg ? eggDeckCardIndexes : mainDeckCardIndexes;
            for (int count = 0; count < cardDef.count; count++)
            {
                target.Add(card.CardIndex);
            }
        }

        if (mainDeckCardIndexes.Count != 50)
        {
            error = $"{GetProductTitle(product)} resolves to {mainDeckCardIndexes.Count} main-deck cards instead of 50.";
            return false;
        }

        if (eggDeckCardIndexes.Count > 5)
        {
            error = $"{GetProductTitle(product)} resolves to {eggDeckCardIndexes.Count} digi-eggs instead of 0-5.";
            return false;
        }

        deckData = new DeckData("")
        {
            DeckName = string.IsNullOrWhiteSpace(product.deckName) ? GetProductTitle(product) : product.deckName.Trim(),
            DeckID = BuildStructureDeckId(product.id),
            DeckCardIDs = mainDeckCardIndexes,
            DigitamaDeckCardIDs = eggDeckCardIndexes,
            KeyCardId = mainDeckCardIndexes.Count > 0 ? mainDeckCardIndexes[0] : -1,
        };

        return true;
    }

    private static void EnsureStructureDeckExists(ContinuousController controller, DeckData deckData)
    {
        if (controller == null || deckData == null)
        {
            return;
        }

        controller.DeckDatas ??= new List<DeckData>();

        DeckData existingDeck = controller.DeckDatas.FirstOrDefault(candidate =>
            candidate != null &&
            !string.IsNullOrWhiteSpace(candidate.DeckID) &&
            string.Equals(candidate.DeckID, deckData.DeckID, StringComparison.OrdinalIgnoreCase));

        if (existingDeck == null)
        {
            controller.DeckDatas.Add(deckData);
            controller.DeckDatas = controller.DeckDatas
                .Where(candidate => candidate != null)
                .OrderBy(candidate => candidate.DeckName)
                .ToList();
            controller.SaveDeckData(deckData);
            return;
        }

        controller.SaveDeckData(existingDeck);
    }

    private static CEntity_Base ResolveCard(string cardId)
    {
        ContinuousController controller = ContinuousController.instance;
        if (controller?.CardList == null)
        {
            return null;
        }

        string normalizedCardId = NormalizeCardCode(cardId);
        return controller.CardList
            .Where(card => card != null)
            .Where(card =>
                NormalizeCardCode(card.CardID) == normalizedCardId ||
                NormalizeCardCode(card.CardSpriteName) == normalizedCardId)
            .OrderByDescending(card => IsCanonicalSprite(card, normalizedCardId))
            .ThenBy(card => card.CardIndex)
            .FirstOrDefault();
    }

    private static bool IsCanonicalSprite(CEntity_Base card, string normalizedCardId)
    {
        if (card == null)
        {
            return false;
        }

        string normalizedSpriteName = NormalizeCardCode(ParallelSuffixRegex.Replace(card.CardSpriteName ?? string.Empty, ""));
        return normalizedSpriteName == normalizedCardId;
    }

    private static string NormalizeCardCode(string cardCode)
    {
        if (string.IsNullOrWhiteSpace(cardCode))
        {
            return string.Empty;
        }

        return cardCode.Trim().Replace("_", "-").ToUpperInvariant();
    }

    private static string BuildStructureDeckId(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return "shop-structure-deck";
        }

        return "shop-" + productId.Trim().ToLowerInvariant().Replace(" ", "-").Replace("_", "-");
    }

    private static string GetProductTitle(ShopProductDef product)
    {
        if (!string.IsNullOrWhiteSpace(product?.title))
        {
            return product.title.Trim();
        }

        return product?.id ?? "Shop Product";
    }

    private static string GetCardDisplayName(CEntity_Base card)
    {
        if (!string.IsNullOrWhiteSpace(card?.CardName_ENG))
        {
            return card.CardName_ENG.Trim();
        }

        return card?.CardID ?? "Unknown Card";
    }

    private static string BuildUnlockSummary(List<CEntity_Base> newlyUnlockedCards, int ownedOrDuplicateCount)
    {
        if (newlyUnlockedCards == null || newlyUnlockedCards.Count == 0)
        {
            return ownedOrDuplicateCount > 0
                ? $"No new cards this time. {ownedOrDuplicateCount} pull{(ownedOrDuplicateCount == 1 ? "" : "s")} were already owned."
                : "No new cards this time.";
        }

        List<string> names = new List<string>();
        int shownCount = Math.Min(newlyUnlockedCards.Count, 5);
        for (int index = 0; index < shownCount; index++)
        {
            CEntity_Base card = newlyUnlockedCards[index];
            if (card == null)
            {
                continue;
            }

            names.Add($"{card.CardID}: {GetCardDisplayName(card)}");
        }

        if (newlyUnlockedCards.Count > shownCount)
        {
            names.Add($"{newlyUnlockedCards.Count - shownCount} more");
        }

        string summary = "CONGRATS: unlocked " + JoinNaturalLanguage(names) + ".";
        if (ownedOrDuplicateCount > 0)
        {
            summary += $" {ownedOrDuplicateCount} other pull{(ownedOrDuplicateCount == 1 ? "" : "s")} were already owned.";
        }

        return summary;
    }

    private static string JoinNaturalLanguage(IReadOnlyList<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return string.Empty;
        }

        if (values.Count == 1)
        {
            return values[0];
        }

        if (values.Count == 2)
        {
            return values[0] + " and " + values[1];
        }

        return string.Join(", ", values.Take(values.Count - 1)) + ", and " + values[values.Count - 1];
    }

    private static ShopPurchaseResult Success(string message)
    {
        return new ShopPurchaseResult
        {
            Succeeded = true,
            Message = message ?? string.Empty,
        };
    }

    private static ShopPurchaseResult Failure(string message)
    {
        return new ShopPurchaseResult
        {
            Succeeded = false,
            Message = message ?? string.Empty,
        };
    }
}
