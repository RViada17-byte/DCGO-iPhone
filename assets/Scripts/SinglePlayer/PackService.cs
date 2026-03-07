using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public static class PackService
{
    private static readonly Regex ParallelSuffixRegex = new Regex(@"([_-])P\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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

    private static HashSet<string> BuildOwnedCardIdSet()
    {
        if (ProgressionManager.Instance == null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return ProgressionManager.Instance.GetUnlockedCardIdSetSnapshot();
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

        Dictionary<string, Dictionary<string, CEntity_Base>> uniqueCardsBySet =
            new Dictionary<string, Dictionary<string, CEntity_Base>>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < cardList.Length; index++)
        {
            CEntity_Base card = cardList[index];
            if (card == null || string.IsNullOrWhiteSpace(card.CardID) || string.IsNullOrWhiteSpace(card.SetID))
            {
                continue;
            }

            if (!uniqueCardsBySet.TryGetValue(card.SetID, out Dictionary<string, CEntity_Base> cardsById))
            {
                cardsById = new Dictionary<string, CEntity_Base>(StringComparer.OrdinalIgnoreCase);
                uniqueCardsBySet[card.SetID] = cardsById;
            }

            if (cardsById.TryGetValue(card.CardID, out CEntity_Base existingCard))
            {
                cardsById[card.CardID] = SelectPrimaryCard(existingCard, card);
                continue;
            }

            cardsById[card.CardID] = card;
        }

        foreach (KeyValuePair<string, Dictionary<string, CEntity_Base>> setEntry in uniqueCardsBySet)
        {
            SetCardCache[setEntry.Key] = setEntry.Value.Values
                .Where(card => card != null)
                .OrderBy(card => card.CardIndex)
                .ToList();
        }
    }

    private static CEntity_Base SelectPrimaryCard(CEntity_Base first, CEntity_Base second)
    {
        if (first == null)
        {
            return second;
        }

        if (second == null)
        {
            return first;
        }

        string normalizedCardId = NormalizeCardCode(first.CardID);
        bool firstIsCanonical = NormalizeCardCode(ParallelSuffixRegex.Replace(first.CardSpriteName ?? string.Empty, "")) == normalizedCardId;
        bool secondIsCanonical = NormalizeCardCode(ParallelSuffixRegex.Replace(second.CardSpriteName ?? string.Empty, "")) == normalizedCardId;

        if (firstIsCanonical != secondIsCanonical)
        {
            return secondIsCanonical ? second : first;
        }

        return second.CardIndex < first.CardIndex ? second : first;
    }

    private static string NormalizeCardCode(string cardCode)
    {
        if (string.IsNullOrWhiteSpace(cardCode))
        {
            return string.Empty;
        }

        return cardCode.Trim().Replace("_", "-").ToUpperInvariant();
    }
}
