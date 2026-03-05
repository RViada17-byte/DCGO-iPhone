using System.Collections.Generic;
using UnityEngine;

public class PackService
{
    public static readonly List<string> DefaultPackSetIds = BuildDefaultPackSetIds();

    public static List<CEntity_Base> GetCardsForSet(string setId)
    {
        List<CEntity_Base> cards = new List<CEntity_Base>();

        if (string.IsNullOrWhiteSpace(setId))
        {
            return cards;
        }

        if (ContinuousController.instance == null || ContinuousController.instance.CardList == null)
        {
            return cards;
        }

        string targetSetId = setId.Trim();

        foreach (CEntity_Base card in ContinuousController.instance.CardList)
        {
            if (card == null)
            {
                continue;
            }

            if (card.SetID == targetSetId)
            {
                cards.Add(card);
            }
        }

        return cards;
    }

    public static List<CEntity_Base> OpenPack(string setId, int cardsPerPack = 12)
    {
        List<CEntity_Base> pool = GetCardsForSet(setId);
        List<CEntity_Base> openedCards = new List<CEntity_Base>();

        if (cardsPerPack <= 0 || pool.Count == 0)
        {
            return openedCards;
        }

        for (int i = 0; i < cardsPerPack; i++)
        {
            int index = Random.Range(0, pool.Count);
            openedCards.Add(pool[index]);
        }

        return openedCards;
    }

    private static List<string> BuildDefaultPackSetIds()
    {
        List<string> setIds = new List<string>();

        for (int i = 1; i <= 24; i++)
        {
            setIds.Add($"BT{i}");
        }

        for (int i = 1; i <= 11; i++)
        {
            setIds.Add($"EX{i}");
        }

        return setIds;
    }
}
