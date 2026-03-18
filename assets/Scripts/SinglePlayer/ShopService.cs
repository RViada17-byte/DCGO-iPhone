using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShopPurchaseCardResult
{
    public string CardId;
    public string PrintId;
    public string CardName;
    public int Count = 1;
    public bool IsNew;
    public bool IsChase;
}

public class ShopPurchaseResult
{
    public bool Succeeded;
    public string Message;
    public string SummaryLine;
    [NonSerialized] public string DialogTitle;
    public List<ShopPurchaseCardResult> CardResults = new List<ShopPurchaseCardResult>();
    public List<string> Lines = new List<string>();
}

public class PackPullResult
{
    public CEntity_Base Card;
    public bool WasNew;
    public bool IsChase;
}

public static class ShopService
{
    private const string PackResultsDialogTitle = "Pack Open Results";
    private const string DeckResultsDialogTitle = "Deck Purchase Results";
    private static bool _hasReconciledPurchasedStructureDecksThisSession;

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
        return IsNonRepeatableProductOwned(product);
    }

    public static bool IsSinglePurchaseProduct(ShopProductDef product)
    {
        if (product == null)
        {
            return false;
        }

        return !product.repeatable || product.IsStructureDeck;
    }

    public static bool IsNonRepeatableProductOwned(ShopProductDef product)
    {
        if (!IsSinglePurchaseProduct(product))
        {
            return false;
        }

        if (ProgressionManager.Instance.HasPurchasedProduct(product.id))
        {
            return true;
        }

        return HasLegacyStructureDeckOwnership(product);
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
        if (_hasReconciledPurchasedStructureDecksThisSession)
        {
            return;
        }

        ContinuousController controller = ContinuousController.instance;
        if (controller == null || controller.CardList == null || controller.CardList.Length == 0)
        {
            return;
        }

        bool saveProfile = false;
        IReadOnlyList<ShopProductDef> products = ShopCatalogDatabase.Instance.Products;
        for (int index = 0; index < products.Count; index++)
        {
            ShopProductDef product = products[index];
            if (product == null || !product.IsStructureDeck)
            {
                continue;
            }

            bool owned = IsNonRepeatableProductOwned(product);
            if (!owned)
            {
                continue;
            }

            if (!ProgressionManager.Instance.HasPurchasedProduct(product.id))
            {
                ProgressionManager.Instance.MarkProductPurchased(product.id, saveImmediately: false);
                saveProfile = true;
            }

            if (TryBuildStructureDeck(product, out DeckData deckData, out _, out _) && deckData != null)
            {
                EnsureStructureDeckExists(controller, deckData);
            }
        }

        if (saveProfile)
        {
            ProgressionManager.Instance.Save("shop reconciliation");
        }

        _hasReconciledPurchasedStructureDecksThisSession = true;
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

        HashSet<string> unlockedPrintIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<CEntity_Base> newlyUnlockedCards = new List<CEntity_Base>();
        int ownedPullCount = 0;
        ShopPurchaseResult result = Success(product.repeatable
            ? $"Opened {GetProductTitle(product)}."
            : $"Unlocked {GetProductTitle(product)}.");
        result.DialogTitle = PackResultsDialogTitle;

        for (int index = 0; index < pulls.Count; index++)
        {
            PackPullResult pull = pulls[index];
            if (pull?.Card == null)
            {
                continue;
            }

            unlockedPrintIds.Add(pull.Card.EffectivePrintID);
            if (pull.WasNew)
            {
                newlyUnlockedCards.Add(pull.Card);
            }
            else
            {
                ownedPullCount++;
            }

            ShopPurchaseCardResult cardResult = BuildCardResult(pull.Card, 1, pull.WasNew, pull.IsChase);
            result.CardResults.Add(cardResult);
            result.Lines.Add(BuildHistoryCardLine(cardResult));
        }

        result.SummaryLine = BuildUnlockSummary(newlyUnlockedCards, ownedPullCount);

        ProgressionManager.Instance.UnlockPrints(unlockedPrintIds, saveImmediately: false);
        if (!product.repeatable)
        {
            ProgressionManager.Instance.MarkProductPurchased(product.id, saveImmediately: false);
        }

        ProgressionManager.Instance.Save("pack opening committed");
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

        HashSet<string> unlockedPrintIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<CEntity_Base> newlyUnlockedCards = new List<CEntity_Base>();
        int alreadyOwnedCount = 0;
        ShopPurchaseResult result = Success($"Purchased {GetProductTitle(product)}.");
        result.DialogTitle = DeckResultsDialogTitle;
        result.Lines.Add($"DECK READY: {deckData.DeckName}");
        Dictionary<string, bool> wasNewById = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < uniqueCards.Count; index++)
        {
            CEntity_Base card = uniqueCards[index];
            if (card == null)
            {
                continue;
            }

            bool wasNew = !ProgressionManager.Instance.IsCardUnlocked(card.CardID);
            unlockedPrintIds.Add(card.EffectivePrintID);
            wasNewById[card.CardID] = wasNew;
            if (wasNew)
            {
                newlyUnlockedCards.Add(card);
            }
            else
            {
                alreadyOwnedCount++;
            }
        }

        result.SummaryLine = BuildUnlockSummary(newlyUnlockedCards, alreadyOwnedCount);
        AddStructureDeckCardResults(result, product, wasNewById);

        ProgressionManager.Instance.UnlockPrints(unlockedPrintIds, saveImmediately: false);
        ProgressionManager.Instance.MarkProductPurchased(product.id, saveImmediately: false);
        EnsureStructureDeckExists(controller, deckData);
        ProgressionManager.Instance.Save("structure deck purchase committed");
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

        List<CEntity_Base> mainDeckCards = new List<CEntity_Base>();
        List<CEntity_Base> eggDeckCards = new List<CEntity_Base>();

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
                eggDeckCards.Add(card);
            }
            else
            {
                mainDeckCards.Add(card);
            }
        }

        if (mainDeckCards.Count != 50)
        {
            error = $"{deckName} resolves to {mainDeckCards.Count} main-deck cards instead of 50.";
            return false;
        }

        if (eggDeckCards.Count > 5)
        {
            error = $"{deckName} resolves to {eggDeckCards.Count} digi-eggs instead of 0-5.";
            return false;
        }

        deckData = new DeckData("")
        {
            DeckName = string.IsNullOrWhiteSpace(deckName) ? "Story Duel" : deckName.Trim(),
            DeckID = "story-" + Guid.NewGuid().ToString("N"),
        };
        deckData.SetDeckCardsFromResolvedCards(mainDeckCards, eggDeckCards, mainDeckCards.FirstOrDefault());

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

        List<CEntity_Base> mainDeckCards = new List<CEntity_Base>();
        List<CEntity_Base> eggDeckCards = new List<CEntity_Base>();
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

            for (int count = 0; count < cardDef.count; count++)
            {
                if (card.cardKind == CardKind.DigiEgg)
                {
                    eggDeckCards.Add(card);
                }
                else
                {
                    mainDeckCards.Add(card);
                }
            }
        }

        if (mainDeckCards.Count != 50)
        {
            error = $"{GetProductTitle(product)} resolves to {mainDeckCards.Count} main-deck cards instead of 50.";
            return false;
        }

        if (eggDeckCards.Count > 5)
        {
            error = $"{GetProductTitle(product)} resolves to {eggDeckCards.Count} digi-eggs instead of 0-5.";
            return false;
        }

        deckData = new DeckData("")
        {
            DeckName = string.IsNullOrWhiteSpace(product.deckName) ? GetProductTitle(product) : product.deckName.Trim(),
            DeckID = BuildStructureDeckId(product.id),
        };
        deckData.SetDeckCardsFromResolvedCards(mainDeckCards, eggDeckCards, mainDeckCards.FirstOrDefault());

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

        if (!StructureDeckNeedsUpdate(existingDeck, deckData))
        {
            return;
        }

        existingDeck.DeckName = deckData.DeckName;
        existingDeck.DeckID = deckData.DeckID;
        existingDeck.SetStoredDeckRefs(
            deckData.GetStoredMainDeckRefs(),
            deckData.GetStoredDigitamaDeckRefs(),
            deckData.GetStoredKeyCardRef());
        controller.SaveDeckData(existingDeck);
    }

    private static bool StructureDeckNeedsUpdate(DeckData existingDeck, DeckData authoredDeck)
    {
        if (existingDeck == null || authoredDeck == null)
        {
            return false;
        }

        if (!string.Equals(existingDeck.DeckName, authoredDeck.DeckName, StringComparison.Ordinal))
        {
            return true;
        }

        if (!CardPrintRefsEqual(existingDeck.GetStoredMainDeckRefs(), authoredDeck.GetStoredMainDeckRefs()))
        {
            return true;
        }

        if (!CardPrintRefsEqual(existingDeck.GetStoredDigitamaDeckRefs(), authoredDeck.GetStoredDigitamaDeckRefs()))
        {
            return true;
        }

        return !CardPrintRefsEqual(existingDeck.GetStoredKeyCardRef(), authoredDeck.GetStoredKeyCardRef());
    }

    private static bool CardPrintRefsEqual(IReadOnlyList<CardPrintRef> left, IReadOnlyList<CardPrintRef> right)
    {
        int leftCount = left?.Count ?? 0;
        int rightCount = right?.Count ?? 0;
        if (leftCount != rightCount)
        {
            return false;
        }

        for (int index = 0; index < leftCount; index++)
        {
            if (!CardPrintRefsEqual(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CardPrintRefsEqual(CardPrintRef left, CardPrintRef right)
    {
        string leftCardId = CardPrintCatalog.NormalizeCardId(left?.CardId);
        string rightCardId = CardPrintCatalog.NormalizeCardId(right?.CardId);
        if (!string.Equals(leftCardId, rightCardId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string leftPrintId = CardPrintCatalog.NormalizeStoredPrintId(left?.PrintId);
        string rightPrintId = CardPrintCatalog.NormalizeStoredPrintId(right?.PrintId);
        return string.Equals(leftPrintId, rightPrintId, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddStructureDeckCardResults(
        ShopPurchaseResult result,
        ShopProductDef product,
        IReadOnlyDictionary<string, bool> wasNewById)
    {
        if (result == null || product?.structureDeckCards == null || product.structureDeckCards.Length == 0)
        {
            return;
        }

        Dictionary<string, ShopPurchaseCardResult> resultsByCardId = new Dictionary<string, ShopPurchaseCardResult>(StringComparer.OrdinalIgnoreCase);
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
                continue;
            }

            if (!resultsByCardId.TryGetValue(card.CardID, out ShopPurchaseCardResult cardResult))
            {
                bool isNew = wasNewById != null &&
                    wasNewById.TryGetValue(card.CardID, out bool trackedIsNew) &&
                    trackedIsNew;

                cardResult = BuildCardResult(card, 0, isNew);
                resultsByCardId.Add(card.CardID, cardResult);
                result.CardResults.Add(cardResult);
            }

            cardResult.Count += cardDef.count;
        }

        for (int index = 0; index < result.CardResults.Count; index++)
        {
            result.Lines.Add(BuildHistoryCardLine(result.CardResults[index]));
        }
    }

    private static CEntity_Base ResolveCard(string cardId)
    {
        return CardPrintCatalog.ResolveCardOrPrint(cardId);
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

    private static bool HasLegacyStructureDeckOwnership(ShopProductDef product)
    {
        if (product == null || !product.IsStructureDeck)
        {
            return false;
        }

        string deckId = BuildStructureDeckId(product.id);
        if (string.IsNullOrWhiteSpace(deckId))
        {
            return false;
        }

        ContinuousController controller = ContinuousController.instance;
        if (controller?.DeckDatas == null)
        {
            return false;
        }

        return controller.DeckDatas.Any(deckData =>
            deckData != null &&
            !string.IsNullOrWhiteSpace(deckData.DeckID) &&
            string.Equals(deckData.DeckID, deckId, StringComparison.OrdinalIgnoreCase));
    }

    private static ShopPurchaseCardResult BuildCardResult(CEntity_Base card, int count, bool isNew, bool isChase = false)
    {
        return new ShopPurchaseCardResult
        {
            CardId = card?.CardID ?? string.Empty,
            PrintId = card?.EffectivePrintID ?? string.Empty,
            CardName = GetCardDisplayName(card),
            Count = Mathf.Max(0, count),
            IsNew = isNew,
            IsChase = isChase,
        };
    }

    private static string BuildHistoryCardLine(ShopPurchaseCardResult cardResult)
    {
        if (cardResult == null)
        {
            return string.Empty;
        }

        string prefix = cardResult.IsNew ? "NEW" : "OWNED";
        if (cardResult.IsChase)
        {
            prefix += " CHASE";
        }

        string countLabel = cardResult.Count > 1 ? $"{cardResult.Count}x " : string.Empty;
        return $"{prefix}: {countLabel}{FormatCardResultCode(cardResult)}, {cardResult.CardName}";
    }

    private static string FormatCardResultCode(ShopPurchaseCardResult cardResult)
    {
        string cardId = cardResult?.CardId ?? string.Empty;
        string normalizedCardId = CardPrintCatalog.NormalizeLookupCode(cardId);
        string normalizedPrintId = CardPrintCatalog.NormalizeLookupCode(cardResult?.PrintId);
        if (string.IsNullOrEmpty(normalizedPrintId) ||
            string.Equals(normalizedCardId, normalizedPrintId, StringComparison.OrdinalIgnoreCase))
        {
            return cardId;
        }

        return $"{cardId} ({cardResult.PrintId})";
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

            string displayCode = CardPrintCatalog.NormalizeLookupCode(card.EffectivePrintID) ==
                                 CardPrintCatalog.NormalizeLookupCode(card.CardID)
                ? card.CardID
                : $"{card.CardID} ({card.EffectivePrintID})";
            names.Add($"{displayCode}: {GetCardDisplayName(card)}");
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
