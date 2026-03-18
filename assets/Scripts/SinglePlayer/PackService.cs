using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PackService
{
    private const int StructuredBoosterCommonsPerPack = 6;
    private const int StructuredBoosterUncommonsPerPack = 3;
    private const int StructuredBoosterRaresPerPack = 2;
    private const int StructuredBoosterChasePerPack = 1;
    private const int StructuredBoosterBaseSlotsPerPack =
        StructuredBoosterCommonsPerPack +
        StructuredBoosterUncommonsPerPack +
        StructuredBoosterRaresPerPack;

    private static readonly Dictionary<string, List<CEntity_Base>> SetCardCache = new Dictionary<string, List<CEntity_Base>>(StringComparer.OrdinalIgnoreCase);
    private static CEntity_Base[] _cachedCardListRef;
    private static int _cachedCardListLength = -1;

    public static readonly PackRules DefaultPackRules = new PackRules
    {
        cardsPerPack = 12,
        guaranteedNewCards = 5,
        randomCards = 7,
    };

    public static List<CEntity_Base> GetCardsForSet(string setId)
    {
        IReadOnlyList<CEntity_Base> cachedCards = GetCachedCardsForSet(setId);
        return cachedCards.Count == 0
            ? new List<CEntity_Base>()
            : new List<CEntity_Base>(cachedCards);
    }

    public static int GetUniqueCardCountForSet(string setId)
    {
        return GetCachedCardsForSet(setId).Count;
    }

    public static int CountOwnedCardsForSet(string setId, ISet<string> ownedCardIds)
    {
        if (ownedCardIds == null || ownedCardIds.Count == 0)
        {
            return 0;
        }

        IReadOnlyList<CEntity_Base> cachedCards = GetCachedCardsForSet(setId);
        int ownedCount = 0;

        for (int index = 0; index < cachedCards.Count; index++)
        {
            CEntity_Base card = cachedCards[index];
            if (card != null && ownedCardIds.Contains(card.CardID))
            {
                ownedCount++;
            }
        }

        return ownedCount;
    }

    public static List<PackPullResult> OpenPack(string setId, PackRules rules = null)
    {
        rules ??= DefaultPackRules;

        if (UsesStructuredBoosterDistribution(setId))
        {
            return OpenStructuredBoosterPack(setId);
        }

        List<CEntity_Base> pool = GetCardsForSet(setId);
        List<PackPullResult> openedCards = new List<PackPullResult>();
        if (pool.Count == 0)
        {
            return openedCards;
        }

        int totalCards = Mathf.Max(0, Mathf.Max(rules.cardsPerPack, rules.guaranteedNewCards + rules.randomCards));
        if (totalCards == 0)
        {
            return openedCards;
        }

        HashSet<string> initiallyOwnedCardIds = BuildOwnedCardIdSet();
        List<CEntity_Base> selectedCards = new List<CEntity_Base>(totalCards);
        List<CEntity_Base> unseenCards = pool
            .Where(card => !initiallyOwnedCardIds.Contains(card.CardID))
            .ToList();
        List<CEntity_Base> remainingUniqueCards = pool.ToList();

        int guaranteedTarget = Mathf.Min(totalCards, Mathf.Max(0, rules.guaranteedNewCards));
        int guaranteedCount = Mathf.Min(guaranteedTarget, unseenCards.Count);
        AddRandomUniqueCards(selectedCards, unseenCards, remainingUniqueCards, guaranteedCount);

        while (selectedCards.Count < totalCards)
        {
            if (remainingUniqueCards.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, remainingUniqueCards.Count);
                selectedCards.Add(remainingUniqueCards[index]);
                remainingUniqueCards.RemoveAt(index);
                continue;
            }

            int fallbackIndex = UnityEngine.Random.Range(0, pool.Count);
            selectedCards.Add(pool[fallbackIndex]);
        }

        HashSet<string> newlyUnlockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < selectedCards.Count; index++)
        {
            CEntity_Base card = selectedCards[index];
            if (card == null)
            {
                continue;
            }

            bool wasNew = !initiallyOwnedCardIds.Contains(card.CardID) && newlyUnlockedIds.Add(card.CardID);
            openedCards.Add(new PackPullResult
            {
                Card = card,
                WasNew = wasNew,
                IsChase = false,
            });
        }

        return openedCards;
    }

    private static List<PackPullResult> OpenStructuredBoosterPack(string setId)
    {
        List<CEntity_Base> canonicalPool = GetCardsForSet(setId);
        List<PackPullResult> openedCards = new List<PackPullResult>();
        if (canonicalPool.Count == 0)
        {
            return openedCards;
        }

        HashSet<string> ownedPrintIds = BuildOwnedPrintIdSet();
        HashSet<string> selectedCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<CEntity_Base> selectedCards = new List<CEntity_Base>(
            StructuredBoosterBaseSlotsPerPack +
            StructuredBoosterChasePerPack);

        AddStructuredCanonicalCards(
            selectedCards,
            canonicalPool.Where(card => card != null && card.rarity == Rarity.C),
            StructuredBoosterCommonsPerPack,
            selectedCardIds,
            canonicalPool);
        AddStructuredCanonicalCards(
            selectedCards,
            canonicalPool.Where(card => card != null && card.rarity == Rarity.U),
            StructuredBoosterUncommonsPerPack,
            selectedCardIds,
            canonicalPool);
        AddStructuredCanonicalCards(
            selectedCards,
            canonicalPool.Where(card => card != null && card.rarity == Rarity.R),
            StructuredBoosterRaresPerPack,
            selectedCardIds,
            canonicalPool);

        if (selectedCards.Count < StructuredBoosterBaseSlotsPerPack)
        {
            AddStructuredCanonicalCards(
                selectedCards,
                canonicalPool,
                StructuredBoosterBaseSlotsPerPack - selectedCards.Count,
                selectedCardIds,
                canonicalPool);
        }

        if (!TryAddStructuredBoosterChaseCard(setId, canonicalPool, selectedCards, selectedCardIds))
        {
            int shortfall = StructuredBoosterBaseSlotsPerPack + StructuredBoosterChasePerPack - selectedCards.Count;
            if (shortfall > 0)
            {
                AddStructuredCanonicalCards(
                    selectedCards,
                    canonicalPool,
                    shortfall,
                    selectedCardIds,
                    canonicalPool);
            }
        }

        EnsureStructuredBoosterChaseSlot(setId, canonicalPool, selectedCards);

        HashSet<string> newlyUnlockedPrintIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < selectedCards.Count; index++)
        {
            CEntity_Base card = selectedCards[index];
            if (card == null)
            {
                continue;
            }

            string normalizedPrintLookup = CardPrintCatalog.NormalizeLookupCode(card.EffectivePrintID);
            bool wasNew = !string.IsNullOrEmpty(normalizedPrintLookup) &&
                          !ownedPrintIds.Contains(normalizedPrintLookup) &&
                          newlyUnlockedPrintIds.Add(normalizedPrintLookup);
            openedCards.Add(new PackPullResult
            {
                Card = card,
                WasNew = wasNew,
                IsChase = index == StructuredBoosterBaseSlotsPerPack && IsStructuredBoosterChaseCard(card),
            });
        }

        return openedCards;
    }

    private static void AddRandomUniqueCards(
        List<CEntity_Base> selectedCards,
        List<CEntity_Base> sourceCards,
        List<CEntity_Base> remainingUniqueCards,
        int count)
    {
        if (count <= 0)
        {
            return;
        }

        List<CEntity_Base> workingSource = sourceCards.ToList();
        for (int index = 0; index < count && workingSource.Count > 0; index++)
        {
            int choiceIndex = UnityEngine.Random.Range(0, workingSource.Count);
            CEntity_Base chosenCard = workingSource[choiceIndex];
            workingSource.RemoveAt(choiceIndex);

            selectedCards.Add(chosenCard);
            remainingUniqueCards.RemoveAll(card =>
                card != null &&
                chosenCard != null &&
                string.Equals(card.CardID, chosenCard.CardID, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AddStructuredCanonicalCards(
        List<CEntity_Base> selectedCards,
        IEnumerable<CEntity_Base> primarySource,
        int count,
        HashSet<string> selectedCardIds,
        IReadOnlyList<CEntity_Base> fallbackSource)
    {
        if (selectedCards == null || count <= 0)
        {
            return;
        }

        int initialSelectedCount = selectedCards.Count;
        List<CEntity_Base> primaryPool = BuildAvailableCanonicalPool(primarySource, selectedCardIds);
        TakeRandomCards(selectedCards, primaryPool, count, selectedCardIds);

        int remaining = count - (selectedCards.Count - initialSelectedCount);
        if (remaining <= 0)
        {
            return;
        }

        List<CEntity_Base> fallbackPool = BuildAvailableCanonicalPool(fallbackSource, selectedCardIds);
        TakeRandomCards(selectedCards, fallbackPool, remaining, selectedCardIds);
    }

    private static List<CEntity_Base> BuildAvailableCanonicalPool(IEnumerable<CEntity_Base> source, ISet<string> excludedCardIds)
    {
        List<CEntity_Base> pool = new List<CEntity_Base>();
        if (source == null)
        {
            return pool;
        }

        HashSet<string> addedCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CEntity_Base card in source)
        {
            if (card == null)
            {
                continue;
            }

            string normalizedCardId = NormalizeCardCode(card.CardID);
            if (string.IsNullOrEmpty(normalizedCardId) ||
                (excludedCardIds != null && excludedCardIds.Contains(normalizedCardId)) ||
                !addedCardIds.Add(normalizedCardId))
            {
                continue;
            }

            pool.Add(card);
        }

        return pool;
    }

    private static void TakeRandomCards(
        List<CEntity_Base> selectedCards,
        List<CEntity_Base> pool,
        int count,
        HashSet<string> selectedCardIds)
    {
        if (selectedCards == null || pool == null || count <= 0)
        {
            return;
        }

        for (int index = 0; index < count && pool.Count > 0; index++)
        {
            int choiceIndex = UnityEngine.Random.Range(0, pool.Count);
            CEntity_Base chosenCard = pool[choiceIndex];
            pool.RemoveAt(choiceIndex);
            if (chosenCard == null)
            {
                continue;
            }

            string normalizedCardId = NormalizeCardCode(chosenCard.CardID);
            if (!string.IsNullOrEmpty(normalizedCardId))
            {
                selectedCardIds?.Add(normalizedCardId);
            }

            selectedCards.Add(chosenCard);
        }
    }

    private static CEntity_Base SelectStructuredBoosterChaseCard(
        string setId,
        IReadOnlyList<CEntity_Base> canonicalPool,
        ISet<string> selectedCardIds)
    {
        List<CEntity_Base> highRarityCanonicals = GetStructuredBoosterHighRarityPool(canonicalPool, selectedCardIds);
        List<CEntity_Base> altPrints = GetStructuredBoosterAltPrintPool(setId, selectedCardIds);

        bool canPullAlt = altPrints.Count > 0;
        bool canPullHighRarity = highRarityCanonicals.Count > 0;
        if (canPullAlt && canPullHighRarity)
        {
            return UnityEngine.Random.value < 0.5f
                ? altPrints[UnityEngine.Random.Range(0, altPrints.Count)]
                : highRarityCanonicals[UnityEngine.Random.Range(0, highRarityCanonicals.Count)];
        }

        if (canPullAlt)
        {
            return altPrints[UnityEngine.Random.Range(0, altPrints.Count)];
        }

        if (canPullHighRarity)
        {
            return highRarityCanonicals[UnityEngine.Random.Range(0, highRarityCanonicals.Count)];
        }

        Debug.LogWarning($"[PackService] No structured chase candidate found for {setId}.");
        return null;
    }

    private static void EnsureStructuredBoosterChaseSlot(
        string setId,
        IReadOnlyList<CEntity_Base> canonicalPool,
        List<CEntity_Base> selectedCards)
    {
        if (selectedCards == null || selectedCards.Count == 0)
        {
            return;
        }

        while (selectedCards.Count < StructuredBoosterBaseSlotsPerPack + StructuredBoosterChasePerPack)
        {
            selectedCards.Add(null);
        }

        int chaseIndex = StructuredBoosterBaseSlotsPerPack + StructuredBoosterChasePerPack - 1;
        CEntity_Base chaseCard = selectedCards[chaseIndex];
        if (IsStructuredBoosterChaseCard(chaseCard))
        {
            return;
        }

        CEntity_Base replacement = SelectStructuredBoosterChaseCard(setId, canonicalPool, selectedCardIds: null);
        if (replacement == null || !IsStructuredBoosterChaseCard(replacement))
        {
            Debug.LogWarning($"[PackService] Structured booster {setId} finished without a valid chase slot.");
            return;
        }

        selectedCards[chaseIndex] = replacement;
    }

    private static bool TryAddStructuredBoosterChaseCard(
        string setId,
        IReadOnlyList<CEntity_Base> canonicalPool,
        List<CEntity_Base> selectedCards,
        HashSet<string> selectedCardIds)
    {
        CEntity_Base chaseCard = SelectStructuredBoosterChaseCard(setId, canonicalPool, selectedCardIds);
        if (chaseCard == null || !IsStructuredBoosterChaseCard(chaseCard))
        {
            return false;
        }

        selectedCards.Add(chaseCard);

        string normalizedCardId = NormalizeCardCode(chaseCard.CardID);
        if (!string.IsNullOrEmpty(normalizedCardId))
        {
            selectedCardIds?.Add(normalizedCardId);
        }

        return true;
    }

    private static bool IsStructuredBoosterChaseCard(CEntity_Base card)
    {
        if (card == null)
        {
            return false;
        }

        return !card.IsCanonicalPrint || card.rarity == Rarity.SR || card.rarity == Rarity.SEC;
    }

    private static List<CEntity_Base> GetStructuredBoosterAltPrintPool(string setId, ISet<string> excludedCardIds)
    {
        List<CEntity_Base> preferredPool = BuildStructuredBoosterAltPrintPool(setId, excludedCardIds);
        if (preferredPool.Count > 0 || excludedCardIds == null || excludedCardIds.Count == 0)
        {
            return preferredPool;
        }

        return BuildStructuredBoosterAltPrintPool(setId, excludedCardIds: null);
    }

    private static List<CEntity_Base> BuildStructuredBoosterAltPrintPool(string setId, ISet<string> excludedCardIds)
    {
        List<CEntity_Base> altPrints = new List<CEntity_Base>();
        CEntity_Base[] cardList = ContinuousController.instance?.CardList;
        if (cardList == null || cardList.Length == 0)
        {
            return altPrints;
        }

        string normalizedSetId = NormalizeCardCode(setId);
        HashSet<string> addedPrintIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < cardList.Length; index++)
        {
            CEntity_Base card = cardList[index];
            if (card == null ||
                string.IsNullOrWhiteSpace(card.CardID) ||
                card.IsCanonicalPrint ||
                NormalizeCardCode(card.SetID) != normalizedSetId)
            {
                continue;
            }

            string normalizedCardId = NormalizeCardCode(card.CardID);
            if (!string.IsNullOrEmpty(normalizedCardId) &&
                excludedCardIds != null &&
                excludedCardIds.Contains(normalizedCardId))
            {
                continue;
            }

            string normalizedPrintId = CardPrintCatalog.NormalizeLookupCode(card.EffectivePrintID);
            if (string.IsNullOrEmpty(normalizedPrintId) || !addedPrintIds.Add(normalizedPrintId))
            {
                continue;
            }

            altPrints.Add(card);
        }

        return altPrints;
    }

    private static List<CEntity_Base> GetStructuredBoosterHighRarityPool(
        IReadOnlyList<CEntity_Base> canonicalPool,
        ISet<string> excludedCardIds)
    {
        List<CEntity_Base> highRaritySource = canonicalPool
            .Where(card =>
                card != null &&
                (card.rarity == Rarity.SR || card.rarity == Rarity.SEC))
            .ToList();

        List<CEntity_Base> preferredPool = BuildAvailableCanonicalPool(highRaritySource, excludedCardIds);
        if (preferredPool.Count > 0)
        {
            return preferredPool;
        }

        return BuildAvailableCanonicalPool(highRaritySource, excludedCardIds: null);
    }

    private static HashSet<string> BuildOwnedCardIdSet()
    {
        if (ProgressionManager.Instance == null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return ProgressionManager.Instance.GetUnlockedCardIdSetSnapshot();
    }

    private static HashSet<string> BuildOwnedPrintIdSet()
    {
        if (ProgressionManager.Instance == null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return ProgressionManager.Instance.GetOwnedPrintIdSetSnapshot();
    }

    private static IReadOnlyList<CEntity_Base> GetCachedCardsForSet(string setId)
    {
        if (string.IsNullOrWhiteSpace(setId))
        {
            return Array.Empty<CEntity_Base>();
        }

        EnsureSetCardCache();

        return SetCardCache.TryGetValue(setId.Trim(), out List<CEntity_Base> cards)
            ? cards
            : Array.Empty<CEntity_Base>();
    }

    private static void EnsureSetCardCache()
    {
        CEntity_Base[] cardList = ContinuousController.instance?.CardList;
        int cardCount = cardList?.Length ?? 0;

        if (ReferenceEquals(cardList, _cachedCardListRef) && cardCount == _cachedCardListLength)
        {
            return;
        }

        _cachedCardListRef = cardList;
        _cachedCardListLength = cardCount;
        SetCardCache.Clear();

        if (cardList == null || cardCount == 0)
        {
            return;
        }

        Dictionary<string, List<CEntity_Base>> uniqueCardsBySet =
            new Dictionary<string, List<CEntity_Base>>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> addedCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < cardList.Length; index++)
        {
            CEntity_Base card = cardList[index];
            if (card == null || string.IsNullOrWhiteSpace(card.CardID) || string.IsNullOrWhiteSpace(card.SetID))
            {
                continue;
            }

            string normalizedCardId = NormalizeCardCode(card.CardID);
            if (!addedCardIds.Add(normalizedCardId))
            {
                continue;
            }

            CEntity_Base canonicalPrint = CardPrintCatalog.GetCanonicalPrint(card.CardID);
            if (canonicalPrint == null)
            {
                continue;
            }

            if (!uniqueCardsBySet.TryGetValue(canonicalPrint.SetID, out List<CEntity_Base> cardsById))
            {
                cardsById = new List<CEntity_Base>();
                uniqueCardsBySet[canonicalPrint.SetID] = cardsById;
            }

            cardsById.Add(canonicalPrint);
        }

        foreach (KeyValuePair<string, List<CEntity_Base>> setEntry in uniqueCardsBySet)
        {
            SetCardCache[setEntry.Key] = setEntry.Value
                .Where(card => card != null)
                .OrderBy(card => card.CardIndex)
                .ToList();
        }
    }

    private static string NormalizeCardCode(string cardCode)
    {
        if (string.IsNullOrWhiteSpace(cardCode))
        {
            return string.Empty;
        }

        return cardCode.Trim().Replace("_", "-").ToUpperInvariant();
    }

    private static bool UsesStructuredBoosterDistribution(string setId)
    {
        string normalizedSetId = NormalizeCardCode(setId);
        return normalizedSetId.StartsWith("BT", StringComparison.OrdinalIgnoreCase) ||
               normalizedSetId.StartsWith("EX", StringComparison.OrdinalIgnoreCase);
    }
}
