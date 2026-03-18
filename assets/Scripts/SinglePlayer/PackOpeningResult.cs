using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class PackOpeningResult
{
    public string SourceId;
    public string SetId;
    public string DisplayName;
    public string SummaryLine;
    public List<CardEntry> Cards = new List<CardEntry>();

    public int TotalCardCount => Cards?.Count ?? 0;

    public int NewCardCount
    {
        get
        {
            if (Cards == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < Cards.Count; index++)
            {
                if (Cards[index] != null && Cards[index].IsNew)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int RareCardCount
    {
        get
        {
            if (Cards == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < Cards.Count; index++)
            {
                if (Cards[index] != null && Cards[index].IsRare)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public static PackOpeningResult FromShopPurchase(ShopProductDef product, ShopPurchaseResult purchaseResult)
    {
        if (product == null || purchaseResult == null || purchaseResult.CardResults == null || purchaseResult.CardResults.Count == 0)
        {
            return null;
        }

        Dictionary<string, CEntity_Base> cardsById = BuildCardLookup(product.setId);
        PackOpeningResult result = new PackOpeningResult
        {
            SourceId = product.id ?? string.Empty,
            SetId = product.setId ?? string.Empty,
            DisplayName = !string.IsNullOrWhiteSpace(product.title) ? product.title.Trim() : (product.id ?? "Pack"),
            SummaryLine = purchaseResult.SummaryLine ?? string.Empty,
        };

        for (int index = 0; index < purchaseResult.CardResults.Count; index++)
        {
            ShopPurchaseCardResult cardResult = purchaseResult.CardResults[index];
            if (cardResult == null)
            {
                continue;
            }

            CEntity_Base cardEntity = CardPrintCatalog.ResolvePrint(cardResult.CardId, cardResult.PrintId);
            if (cardEntity == null)
            {
                cardEntity = ResolveCard(cardsById, cardResult.PrintId);
            }
            if (cardEntity == null)
            {
                cardEntity = ResolveCard(cardsById, cardResult.CardId);
            }
            if (cardEntity == null)
            {
                cardEntity = CardPrintCatalog.ResolveCardOrPrint(cardResult.CardId, preferCanonical: false);
            }
            result.Cards.Add(new CardEntry
            {
                CardId = cardResult.CardId ?? string.Empty,
                PrintId = cardResult.PrintId ?? string.Empty,
                CardName = ResolveCardName(cardResult, cardEntity),
                SpriteName = ResolveSpriteName(cardResult, cardEntity),
                Rarity = cardEntity != null ? cardEntity.rarity : Rarity.None,
                IsNew = cardResult.IsNew,
                IsChase = cardResult.IsChase,
                IsAltPrint = IsAlternatePrint(cardResult, cardEntity),
                Count = Mathf.Max(1, cardResult.Count),
                CardAsset = cardEntity,
            });
        }

        return result.Cards.Count > 0 ? result : null;
    }

    private static Dictionary<string, CEntity_Base> BuildCardLookup(string setId)
    {
        List<CEntity_Base> cards = PackService.GetCardsForSet(setId);
        if (cards.Count == 0 && ContinuousController.instance?.CardList != null)
        {
            cards.AddRange(ContinuousController.instance.CardList);
        }

        Dictionary<string, CEntity_Base> lookup = new Dictionary<string, CEntity_Base>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < cards.Count; index++)
        {
            CEntity_Base card = cards[index];
            if (card == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(card.CardID) && !lookup.ContainsKey(card.CardID))
            {
                lookup.Add(card.CardID, card);
            }

            if (!string.IsNullOrWhiteSpace(card.EffectivePrintID) && !lookup.ContainsKey(card.EffectivePrintID))
            {
                lookup.Add(card.EffectivePrintID, card);
            }

            if (!string.IsNullOrWhiteSpace(card.CardSpriteName) && !lookup.ContainsKey(card.CardSpriteName))
            {
                lookup.Add(card.CardSpriteName, card);
            }
        }

        return lookup;
    }

    private static CEntity_Base ResolveCard(Dictionary<string, CEntity_Base> cardsById, string cardId)
    {
        if (cardsById == null || string.IsNullOrWhiteSpace(cardId))
        {
            return null;
        }

        cardsById.TryGetValue(cardId.Trim(), out CEntity_Base card);
        return card;
    }

    private static string ResolveCardName(ShopPurchaseCardResult cardResult, CEntity_Base cardEntity)
    {
        if (!string.IsNullOrWhiteSpace(cardResult?.CardName))
        {
            return cardResult.CardName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(cardEntity?.CardName_ENG))
        {
            return cardEntity.CardName_ENG.Trim();
        }

        return cardResult?.CardId ?? "Unknown Card";
    }

    private static string ResolveSpriteName(ShopPurchaseCardResult cardResult, CEntity_Base cardEntity)
    {
        if (!string.IsNullOrWhiteSpace(cardEntity?.CardSpriteName))
        {
            return cardEntity.CardSpriteName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(cardResult?.PrintId))
        {
            return cardResult.PrintId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(cardResult?.CardId))
        {
            return cardResult.CardId.Trim();
        }

        return string.Empty;
    }

    private static bool IsAlternatePrint(ShopPurchaseCardResult cardResult, CEntity_Base cardEntity)
    {
        if (cardEntity != null)
        {
            return !cardEntity.IsCanonicalPrint;
        }

        string normalizedCardId = CardPrintCatalog.NormalizeLookupCode(cardResult?.CardId);
        string normalizedPrintId = CardPrintCatalog.NormalizeLookupCode(cardResult?.PrintId);
        if (string.IsNullOrEmpty(normalizedCardId) || string.IsNullOrEmpty(normalizedPrintId))
        {
            return false;
        }

        return !string.Equals(normalizedCardId, normalizedPrintId, StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public class CardEntry
    {
        public string CardId;
        public string PrintId;
        public string CardName;
        public string SpriteName;
        public Rarity Rarity = Rarity.None;
        public bool IsNew;
        public bool IsChase;
        public bool IsAltPrint;
        public int Count = 1;
        [NonSerialized] public CEntity_Base CardAsset;
        [NonSerialized] private Task<Sprite> _spriteTask;

        public bool IsRare => IsChase || IsAltPrint || PackPresentationTheme.IsBurstRarityStatic(Rarity);

        public Task<Sprite> LoadSpriteAsync()
        {
            if (_spriteTask != null)
            {
                return _spriteTask;
            }

            if (CardAsset != null)
            {
                _spriteTask = CardAsset.GetCardSprite();
                return _spriteTask;
            }

            if (!string.IsNullOrWhiteSpace(SpriteName))
            {
                _spriteTask = StreamingAssetsUtility.GetSprite(SpriteName, isCard: true);
                return _spriteTask;
            }

            _spriteTask = Task.FromResult<Sprite>(null);
            return _spriteTask;
        }
    }
}
