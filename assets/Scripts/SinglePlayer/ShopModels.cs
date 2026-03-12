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
    public string displayGroup;
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
    public ShopProductDisplayGroup DisplayGroup => ShopProductDisplayGroupUtility.Resolve(displayGroup, this);
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

public enum ShopProductDisplayGroup
{
    Auto = 0,
    StructureDecks = 1,
    StarterSets = 2,
    BoosterPacks = 3,
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

public static class ShopProductDisplayGroupUtility
{
    public static ShopProductDisplayGroup Resolve(string value, ShopProductDef product)
    {
        ShopProductDisplayGroup parsed = Parse(value);
        if (parsed != ShopProductDisplayGroup.Auto)
        {
            return parsed;
        }

        if (product != null && product.IsStructureDeck)
        {
            return ShopProductDisplayGroup.StructureDecks;
        }

        if (product != null &&
            product.IsPack &&
            !product.repeatable &&
            !string.IsNullOrWhiteSpace(product.setId) &&
            product.setId.Trim().StartsWith("ST", StringComparison.OrdinalIgnoreCase))
        {
            return ShopProductDisplayGroup.StarterSets;
        }

        return ShopProductDisplayGroup.BoosterPacks;
    }

    public static ShopProductDisplayGroup Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ShopProductDisplayGroup.Auto;
        }

        string normalized = value.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "structuredeck":
            case "structuredecks":
            case "structure_deck":
            case "structure_decks":
            case "structure-deck":
            case "structure-decks":
                return ShopProductDisplayGroup.StructureDecks;

            case "starterset":
            case "startersets":
            case "starter_set":
            case "starter_sets":
            case "starter-set":
            case "starter-sets":
                return ShopProductDisplayGroup.StarterSets;

            case "boosterpack":
            case "boosterpacks":
            case "booster_pack":
            case "booster_packs":
            case "booster-pack":
            case "booster-packs":
            case "pack":
            case "packs":
                return ShopProductDisplayGroup.BoosterPacks;

            default:
                return ShopProductDisplayGroup.Auto;
        }
    }
}
