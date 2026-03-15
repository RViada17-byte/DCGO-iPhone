using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public class CardPrintRef
{
    public string CardId = string.Empty;
    public string PrintId = string.Empty;

    public CardPrintRef()
    {
    }

    public CardPrintRef(string cardId, string printId)
    {
        CardId = CardPrintCatalog.NormalizeCardId(cardId);
        PrintId = CardPrintCatalog.NormalizeStoredPrintId(printId);
    }

    public bool IsEmpty => string.IsNullOrWhiteSpace(CardId) && string.IsNullOrWhiteSpace(PrintId);

    public CardPrintRef Clone()
    {
        return new CardPrintRef(CardId, PrintId);
    }

    public static CardPrintRef FromCard(CEntity_Base card)
    {
        if (card == null)
        {
            return new CardPrintRef();
        }

        return new CardPrintRef(card.CardID, CardPrintCatalog.GetStoredPrintId(card));
    }
}

public static class CardPrintCatalog
{
    static readonly Regex ParallelSuffixRegex = new Regex(@"([_-])P\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

    static readonly Dictionary<string, List<CEntity_Base>> PrintsByCardId = new Dictionary<string, List<CEntity_Base>>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, List<CEntity_Base>> PrintsByLookupCode = new Dictionary<string, List<CEntity_Base>>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, CEntity_Base> CanonicalPrintByCardId = new Dictionary<string, CEntity_Base>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<int, CEntity_Base> PrintByLegacyCardIndex = new Dictionary<int, CEntity_Base>();
    static readonly HashSet<string> LoggedWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    static CEntity_Base[] _cachedCardListRef;
    static int _cachedCardCount = -1;

    public static void ResetCache()
    {
        _cachedCardListRef = null;
        _cachedCardCount = -1;
        PrintsByCardId.Clear();
        PrintsByLookupCode.Clear();
        CanonicalPrintByCardId.Clear();
        PrintByLegacyCardIndex.Clear();
        LoggedWarnings.Clear();
    }

    public static string NormalizeCardId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return WhitespaceRegex.Replace(value.Trim(), string.Empty)
            .Replace("_", "-")
            .ToUpperInvariant();
    }

    public static string NormalizeStoredPrintId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return WhitespaceRegex.Replace(value.Trim(), string.Empty)
            .ToUpperInvariant();
    }

    public static string NormalizeLookupCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return NormalizeStoredPrintId(value).Replace("_", "-");
    }

    public static string SuggestPrintId(CEntity_Base card)
    {
        if (card == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(card.CardSpriteName))
        {
            return NormalizeStoredPrintId(card.CardSpriteName);
        }

        if (!string.IsNullOrWhiteSpace(card.CardID))
        {
            return NormalizeStoredPrintId(card.CardID);
        }

        return NormalizeStoredPrintId(card.PrintID);
    }

    public static string GetStoredPrintId(CEntity_Base card)
    {
        if (card == null)
        {
            return string.Empty;
        }

        string storedPrintId = NormalizeStoredPrintId(card.PrintID);
        if (!string.IsNullOrWhiteSpace(storedPrintId))
        {
            return storedPrintId;
        }

        return SuggestPrintId(card);
    }

    public static bool IsLikelyCanonicalPrint(CEntity_Base card)
    {
        if (card == null)
        {
            return false;
        }

        return LooksCanonicalArt(card, NormalizeCardId(card.CardID));
    }

    public static IReadOnlyList<CEntity_Base> GetPrints(string cardId)
    {
        EnsureCatalog();
        return PrintsByCardId.TryGetValue(NormalizeCardId(cardId), out List<CEntity_Base> prints)
            ? prints
            : Array.Empty<CEntity_Base>();
    }

    public static CEntity_Base GetCanonicalPrint(string cardId)
    {
        EnsureCatalog();
        string normalizedCardId = NormalizeCardId(cardId);
        if (CanonicalPrintByCardId.TryGetValue(normalizedCardId, out CEntity_Base card))
        {
            return card;
        }

        CEntity_Base[] cardList = ContinuousController.instance?.CardList;
        if (cardList == null || cardList.Length == 0 || string.IsNullOrEmpty(normalizedCardId))
        {
            return null;
        }

        CEntity_Base fallbackCanonical = SelectCanonicalPrint(cardList.Where(candidate =>
            candidate != null &&
            NormalizeCardId(candidate.CardID) == normalizedCardId));

        if (fallbackCanonical != null)
        {
            CanonicalPrintByCardId[normalizedCardId] = fallbackCanonical;
        }

        return fallbackCanonical;
    }

    public static List<CEntity_Base> GetCanonicalPrintsForSet(string setId)
    {
        EnsureCatalog();
        string normalizedSetId = NormalizeCardId(setId);
        return CanonicalPrintByCardId.Values
            .Where(card => card != null && NormalizeCardId(card.SetID) == normalizedSetId)
            .OrderBy(card => card.CardIndex)
            .ToList();
    }

    public static CEntity_Base ResolvePrint(string cardId, string printId)
    {
        string normalizedCardId = NormalizeCardId(cardId);
        string normalizedPrintLookup = NormalizeLookupCode(printId);

        if (string.IsNullOrEmpty(normalizedCardId))
        {
            return ResolveCardOrPrint(printId, preferCanonical: false);
        }

        IReadOnlyList<CEntity_Base> prints = GetPrints(normalizedCardId);
        if (prints.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrEmpty(normalizedPrintLookup))
        {
            return GetCanonicalPrint(normalizedCardId);
        }

        return prints
            .Where(card => card != null && MatchesLookupCode(card, normalizedPrintLookup))
            .OrderBy(card => card.CardIndex)
            .FirstOrDefault();
    }

    public static CEntity_Base ResolveCardOrPrint(string value, bool preferCanonical = true)
    {
        string normalizedLookup = NormalizeLookupCode(value);
        if (string.IsNullOrEmpty(normalizedLookup))
        {
            return null;
        }

        CEntity_Base canonicalCard = GetCanonicalPrint(normalizedLookup);
        if (canonicalCard != null)
        {
            return canonicalCard;
        }

        EnsureCatalog();
        if (PrintsByLookupCode.TryGetValue(normalizedLookup, out List<CEntity_Base> matches) && matches.Count > 0)
        {
            IEnumerable<CEntity_Base> ordered = matches.Where(card => card != null);
            if (preferCanonical)
            {
                ordered = ordered
                    .OrderByDescending(card => card.IsCanonicalPrint)
                    .ThenBy(card => card.CardIndex);
            }
            else
            {
                ordered = ordered.OrderBy(card => card.CardIndex);
            }

            return ordered.FirstOrDefault();
        }

        return null;
    }

    public static CEntity_Base ResolveDeckSlotWithFallback(CardPrintRef cardRef, ISet<string> ownedPrintIds = null, bool ignoreOwnership = false)
    {
        if (cardRef == null || cardRef.IsEmpty)
        {
            return null;
        }

        CEntity_Base requestedPrint = ResolvePrint(cardRef.CardId, cardRef.PrintId);
        if (requestedPrint == null)
        {
            return GetCanonicalPrint(cardRef.CardId);
        }

        if (ignoreOwnership || ownedPrintIds == null)
        {
            return requestedPrint;
        }

        string requestedPrintLookup = NormalizeLookupCode(GetStoredPrintId(requestedPrint));
        if (ownedPrintIds.Contains(requestedPrintLookup))
        {
            return requestedPrint;
        }

        return GetCanonicalPrint(cardRef.CardId) ?? requestedPrint;
    }

    public static CEntity_Base ResolveLegacyCardIndex(int cardIndex)
    {
        if (cardIndex <= 0)
        {
            return null;
        }

        EnsureCatalog();
        PrintByLegacyCardIndex.TryGetValue(cardIndex, out CEntity_Base card);
        return card;
    }

    public static List<CardPrintRef> CreatePrintRefs(IEnumerable<CEntity_Base> cards)
    {
        List<CardPrintRef> refs = new List<CardPrintRef>();
        if (cards == null)
        {
            return refs;
        }

        foreach (CEntity_Base card in cards)
        {
            CardPrintRef cardRef = CardPrintRef.FromCard(card);
            if (!cardRef.IsEmpty)
            {
                refs.Add(cardRef);
            }
        }

        return refs;
    }

    public static List<CardPrintRef> CloneRefs(IEnumerable<CardPrintRef> refs)
    {
        List<CardPrintRef> clones = new List<CardPrintRef>();
        if (refs == null)
        {
            return clones;
        }

        foreach (CardPrintRef cardRef in refs)
        {
            if (cardRef == null || cardRef.IsEmpty)
            {
                continue;
            }

            clones.Add(cardRef.Clone());
        }

        return clones;
    }

    public static CardPrintRef CloneRef(CardPrintRef cardRef)
    {
        return cardRef == null ? new CardPrintRef() : cardRef.Clone();
    }

    public static void EnsureRuntimeIdentity(CEntity_Base card, string fallbackPrintId = null, bool? isCanonical = null)
    {
        if (card == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(card.PrintID))
        {
            card.PrintID = NormalizeStoredPrintId(string.IsNullOrWhiteSpace(fallbackPrintId) ? SuggestPrintId(card) : fallbackPrintId);
        }

        if (isCanonical.HasValue)
        {
            card.IsCanonicalPrint = isCanonical.Value;
        }
    }

    public static CEntity_Base SelectCanonicalPrint(IEnumerable<CEntity_Base> cards)
    {
        List<CEntity_Base> prints = cards?
            .Where(card => card != null)
            .OrderBy(card => card.CardIndex)
            .ToList() ?? new List<CEntity_Base>();

        if (prints.Count == 0)
        {
            return null;
        }

        string normalizedCardId = NormalizeCardId(prints[0].CardID);
        List<CEntity_Base> explicitCanonicals = prints
            .Where(card => card.IsCanonicalPrint)
            .ToList();

        if (explicitCanonicals.Count == 1)
        {
            return explicitCanonicals[0];
        }

        if (explicitCanonicals.Count > 1)
        {
            LogWarningOnce($"multiple-canonical::{normalizedCardId}", $"[CardPrintCatalog] Multiple canonical prints found for {normalizedCardId}. Using the lowest-index canonical print.");
            return explicitCanonicals
                .OrderBy(card => card.CardIndex)
                .First();
        }

        List<CEntity_Base> exactMatchPrints = prints
            .Where(card => LooksCanonicalArt(card, normalizedCardId))
            .ToList();
        if (exactMatchPrints.Count > 0)
        {
            return exactMatchPrints
                .OrderBy(card => card.CardIndex)
                .First();
        }

        List<CEntity_Base> errataPrints = prints
            .Where(LooksErrataPrint)
            .ToList();
        if (errataPrints.Count == 1)
        {
            return errataPrints[0];
        }

        List<CEntity_Base> p0Prints = prints
            .Where(LooksP0Print)
            .ToList();
        if (p0Prints.Count > 0)
        {
            return p0Prints
                .OrderBy(card => card.CardIndex)
                .First();
        }

        LogWarningOnce($"fallback-canonical::{normalizedCardId}", $"[CardPrintCatalog] Falling back to lowest CardIndex canonical print for {normalizedCardId}. Review this card group in the print audit.");
        return prints[0];
    }

    static void EnsureCatalog()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("CardPrintCatalog.EnsureCatalog");
        CEntity_Base[] cardList = ContinuousController.instance?.CardList;
        int cardCount = cardList?.Length ?? 0;
        bool rebuilt = false;
        StartupPerfTrace.Scope buildScope = default;

        try
        {
            if (ReferenceEquals(cardList, _cachedCardListRef) && cardCount == _cachedCardCount)
            {
                return;
            }

            rebuilt = true;
            buildScope = StartupPerfTrace.Measure("CardPrintCatalog.BuildCatalog");

            _cachedCardListRef = cardList;
            _cachedCardCount = cardCount;

            PrintsByCardId.Clear();
            PrintsByLookupCode.Clear();
            CanonicalPrintByCardId.Clear();
            PrintByLegacyCardIndex.Clear();

            if (cardList == null || cardCount == 0)
            {
                return;
            }

            foreach (CEntity_Base card in cardList.Where(card => card != null))
            {
                EnsureRuntimeIdentity(card);
                RegisterLegacyCardIndex(card.CardIndex, card);

                if (card.LegacyCardIndices != null)
                {
                    foreach (int legacyCardIndex in card.LegacyCardIndices)
                    {
                        RegisterLegacyCardIndex(legacyCardIndex, card);
                    }
                }

                string normalizedCardId = NormalizeCardId(card.CardID);
                if (string.IsNullOrEmpty(normalizedCardId))
                {
                    continue;
                }

                if (!PrintsByCardId.TryGetValue(normalizedCardId, out List<CEntity_Base> prints))
                {
                    prints = new List<CEntity_Base>();
                    PrintsByCardId[normalizedCardId] = prints;
                }

                string normalizedPrintLookup = NormalizeLookupCode(GetStoredPrintId(card));
                CEntity_Base existing = prints.FirstOrDefault(existingCard =>
                    existingCard != null &&
                    NormalizeLookupCode(GetStoredPrintId(existingCard)) == normalizedPrintLookup);

                if (existing == null)
                {
                    prints.Add(card);
                }
                else if (card.CardIndex < existing.CardIndex)
                {
                    prints.Remove(existing);
                    prints.Add(card);
                }
            }

            foreach (KeyValuePair<string, List<CEntity_Base>> entry in PrintsByCardId.ToList())
            {
                string normalizedCardId = entry.Key;
                List<CEntity_Base> prints = entry.Value;
                prints.Sort((left, right) => left.CardIndex.CompareTo(right.CardIndex));
                CEntity_Base canonicalPrint = SelectCanonicalPrint(prints);
                if (canonicalPrint != null)
                {
                    CanonicalPrintByCardId[normalizedCardId] = canonicalPrint;
                }

                foreach (CEntity_Base card in prints)
                {
                    AddLookup(card.CardID, card);
                    AddLookup(card.CardSpriteName, card);
                    AddLookup(GetStoredPrintId(card), card);
                }
            }
        }
        finally
        {
            StartupPerfTrace.RecordCatalogEnsure(rebuilt);

            if (rebuilt)
            {
                buildScope.SetItemCount("runtimeCards", cardCount);
                buildScope.SetItemCount("cardGroups", PrintsByCardId.Count);
                buildScope.SetItemCount("lookupGroups", PrintsByLookupCode.Count);
                buildScope.SetItemCount("legacyCardIndexes", PrintByLegacyCardIndex.Count);
                buildScope.Dispose();
            }

            perfScope.SetItemCount("runtimeCards", cardCount);
            perfScope.SetItemCount("rebuilt", rebuilt ? 1 : 0);
            perfScope.SetItemCount("cardGroups", PrintsByCardId.Count);
            perfScope.SetItemCount("lookupGroups", PrintsByLookupCode.Count);
            perfScope.Dispose();
        }
    }

    static void AddLookup(string rawValue, CEntity_Base card)
    {
        string normalizedLookup = NormalizeLookupCode(rawValue);
        if (string.IsNullOrEmpty(normalizedLookup) || card == null)
        {
            return;
        }

        if (!PrintsByLookupCode.TryGetValue(normalizedLookup, out List<CEntity_Base> prints))
        {
            prints = new List<CEntity_Base>();
            PrintsByLookupCode[normalizedLookup] = prints;
        }

        if (!prints.Contains(card))
        {
            prints.Add(card);
            prints.Sort((left, right) => left.CardIndex.CompareTo(right.CardIndex));
        }
    }

    static bool MatchesLookupCode(CEntity_Base card, string normalizedLookup)
    {
        if (card == null || string.IsNullOrEmpty(normalizedLookup))
        {
            return false;
        }

        return NormalizeLookupCode(card.CardID) == normalizedLookup ||
               NormalizeLookupCode(card.CardSpriteName) == normalizedLookup ||
               NormalizeLookupCode(GetStoredPrintId(card)) == normalizedLookup;
    }

    static bool LooksCanonicalArt(CEntity_Base card, string normalizedCardId)
    {
        if (card == null)
        {
            return false;
        }

        string normalizedSprite = NormalizeLookupCode(ParallelSuffixRegex.Replace(card.CardSpriteName ?? string.Empty, string.Empty));
        return !string.IsNullOrEmpty(normalizedCardId) && normalizedSprite == NormalizeLookupCode(normalizedCardId);
    }

    static bool LooksErrataPrint(CEntity_Base card)
    {
        if (card == null)
        {
            return false;
        }

        string normalizedPrintLookup = NormalizeLookupCode(GetStoredPrintId(card));
        return normalizedPrintLookup.EndsWith("-ERRATA", StringComparison.OrdinalIgnoreCase);
    }

    static bool LooksP0Print(CEntity_Base card)
    {
        if (card == null)
        {
            return false;
        }

        string normalizedPrintLookup = NormalizeLookupCode(GetStoredPrintId(card));
        return normalizedPrintLookup.EndsWith("-P0", StringComparison.OrdinalIgnoreCase);
    }

    static void RegisterLegacyCardIndex(int cardIndex, CEntity_Base card)
    {
        if (cardIndex <= 0 || card == null)
        {
            return;
        }

        if (PrintByLegacyCardIndex.TryGetValue(cardIndex, out CEntity_Base existing) && existing != card)
        {
            LogWarningOnce(
                $"legacy-card-index::{cardIndex}",
                $"[CardPrintCatalog] Duplicate legacy CardIndex mapping detected for {cardIndex}. Keeping {existing.name} and ignoring {card.name}.");
            return;
        }

        PrintByLegacyCardIndex[cardIndex] = card;
    }

    static void LogWarningOnce(string key, string message)
    {
        if (!LoggedWarnings.Add(key))
        {
            return;
        }

        Debug.LogWarning(message);
    }
}
