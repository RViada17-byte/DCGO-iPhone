using System;

[Serializable]
public class ShopCatalogDef
{
    public ShopProductDef[] products;
}

[Serializable]
public class ShopProductDef
{
    public string id;
    public string kind;
    public string title;
    public string deckName;
    public string setId;
    public int price;
    public bool repeatable = true;
    public string[] prereqStoryNodeIds;
    public StructureDeckCardDef[] structureDeckCards;
    public PackRules packRules;

    public ShopProductKind ProductKind
    {
        get
        {
            return ShopProductKindUtility.Parse(kind);
        }
    }

    public bool IsStructureDeck => ProductKind == ShopProductKind.StructureDeck;
    public bool IsPack => ProductKind == ShopProductKind.BoosterPack;
}

[Serializable]
public class StructureDeckCardDef
{
    public string cardId;
    public int count;
}

[Serializable]
public class PackRules
{
    public int cardsPerPack = 12;
    public int guaranteedNewCards = 5;
    public int randomCards = 7;
}

public enum ShopProductKind
{
    Unknown = 0,
    StructureDeck = 1,
    BoosterPack = 2,
}

public static class ShopProductKindUtility
{
    public static ShopProductKind Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ShopProductKind.Unknown;
        }

        string normalized = value.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "structuredeck":
            case "structure_deck":
            case "structure-deck":
                return ShopProductKind.StructureDeck;

            case "boosterpack":
            case "booster_pack":
            case "booster-pack":
            case "pack":
                return ShopProductKind.BoosterPack;

            default:
                return ShopProductKind.Unknown;
        }
    }
}
