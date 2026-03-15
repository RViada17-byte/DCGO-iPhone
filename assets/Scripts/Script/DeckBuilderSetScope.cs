using System;
using System.Collections.Generic;
using System.Linq;

public static class DeckBuilderSetScope
{
    static readonly HashSet<string> ExplicitlyAllowedSetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "LM",
        "P",
        "RB1",
    };
    static readonly HashSet<string> ExplicitlyExcludedCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "LM-051",
        "LM-052",
        "LM-053",
        "LM-054",
        "LM-055",
        "LM-056",
    };
    static readonly HashSet<string> AllowedSetIds = BuildAllowedSetIds();

    public static IReadOnlyCollection<string> AllowedSets => AllowedSetIds;

    public static bool IsAllowedSet(string setId)
    {
        string normalizedSetId = NormalizeSetId(setId);
        if (string.IsNullOrWhiteSpace(normalizedSetId))
        {
            return false;
        }

        if (normalizedSetId.StartsWith("AD", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ExplicitlyAllowedSetIds.Contains(normalizedSetId))
        {
            return true;
        }

        if (!TryParseNumberedSet(normalizedSetId, out string prefix, out int setNumber))
        {
            return false;
        }

        switch (prefix)
        {
            case "BT":
                return setNumber >= 1 && setNumber <= 22;

            case "EX":
                return setNumber >= 1 && setNumber <= 10;

            case "ST":
                return setNumber >= 1 && setNumber <= 22;

            default:
                return false;
        }
    }

    public static bool IsAllowedCard(CEntity_Base card)
    {
        if (card == null)
        {
            return false;
        }

        if (ExplicitlyExcludedCardIds.Contains(NormalizeCardCode(card.CardID)))
        {
            return false;
        }

        return IsAllowedSet(card.SetID);
    }

    public static bool IsAllowedDeck(DeckData deck)
    {
        if (deck == null)
        {
            return false;
        }

        List<CEntity_Base> allCards = deck.AllDeckCards();

        if (allCards == null || allCards.Count == 0)
        {
            return false;
        }

        return allCards.All(IsAllowedCard);
    }

    public static List<CEntity_Base> FilterAllowedCards(IEnumerable<CEntity_Base> cards)
    {
        if (cards == null)
        {
            return new List<CEntity_Base>();
        }

        return cards
            .Where(IsAllowedCard)
            .ToList();
    }

    static HashSet<string> BuildAllowedSetIds()
    {
        HashSet<string> allowedSetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (DataBase.SetIDs != null)
        {
            foreach (string setId in DataBase.SetIDs)
            {
                if (IsAllowedSet(setId))
                {
                    allowedSetIds.Add(NormalizeSetId(setId));
                }
            }
        }

        foreach (string setId in ExplicitlyAllowedSetIds)
        {
            allowedSetIds.Add(setId);
        }

        return allowedSetIds;
    }

    static string NormalizeSetId(string setId)
    {
        return string.IsNullOrWhiteSpace(setId)
            ? string.Empty
            : setId.Trim().ToUpperInvariant();
    }

    static string NormalizeCardCode(string cardCode)
    {
        return string.IsNullOrWhiteSpace(cardCode)
            ? string.Empty
            : cardCode.Trim().Replace("_", "-").ToUpperInvariant();
    }

    static bool TryParseNumberedSet(string setId, out string prefix, out int setNumber)
    {
        prefix = string.Empty;
        setNumber = 0;

        if (string.IsNullOrWhiteSpace(setId))
        {
            return false;
        }

        int splitIndex = 0;
        while (splitIndex < setId.Length && char.IsLetter(setId[splitIndex]))
        {
            splitIndex++;
        }

        if (splitIndex <= 0 || splitIndex >= setId.Length)
        {
            return false;
        }

        prefix = setId.Substring(0, splitIndex);
        return int.TryParse(setId.Substring(splitIndex), out setNumber);
    }
}
