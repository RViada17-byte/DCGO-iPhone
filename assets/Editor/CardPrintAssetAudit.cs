using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CardPrintAssetAudit
{
    const string CardAssetRoot = "Assets/CardBaseEntity";

    [MenuItem("DCGO/Card Prints/Audit")]
    public static void Audit()
    {
        RunAudit(applyFixes: false, targetCardIds: null, scopeLabel: "all card assets");
    }

    [MenuItem("DCGO/Card Prints/Backfill Print Metadata")]
    public static void Backfill()
    {
        HashSet<string> targetCardIds = GetSelectedCardIds();
        if (targetCardIds == null || targetCardIds.Count == 0)
        {
            Debug.LogWarning("[CardPrintAssetAudit] No card assets or folders selected. Export the backfill report, select the card groups you want to patch, then rerun backfill.");
            return;
        }

        RunAudit(applyFixes: true, targetCardIds, $"selected card groups ({targetCardIds.Count})");
    }

    [MenuItem("DCGO/Card Prints/Backfill All Print Metadata")]
    public static void BackfillAll()
    {
        RunAudit(applyFixes: true, targetCardIds: null, scopeLabel: "all card assets");
    }

    static void RunAudit(bool applyFixes, HashSet<string> targetCardIds, string scopeLabel)
    {
        List<CEntity_Base> cards = AssetDatabase.FindAssets("t:CEntity_Base", new[] { CardAssetRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => AssetDatabase.LoadAssetAtPath<CEntity_Base>(path))
            .Where(card => card != null)
            .ToList();

        int touchedCount = 0;
        int touchedGroups = 0;
        int duplicatePrintGroups = 0;
        int duplicateStoredPrintGroups = 0;
        int mixedEffectGroups = 0;
        int missingCanonicalGroups = 0;

        Dictionary<string, List<CEntity_Base>> cardsByPrintId = cards
            .Select(card => new
            {
                Card = card,
                PrintId = CardPrintCatalog.NormalizeLookupCode(CardPrintCatalog.SuggestPrintId(card)),
            })
            .Where(entry => entry.Card != null && !string.IsNullOrWhiteSpace(entry.PrintId))
            .GroupBy(entry => entry.PrintId)
            .ToDictionary(entry => entry.Key, entry => entry.Select(value => value.Card).ToList());

        foreach (KeyValuePair<string, List<CEntity_Base>> printEntry in cardsByPrintId)
        {
            if (printEntry.Value.Count <= 1)
            {
                continue;
            }

            duplicatePrintGroups++;
            string affectedCards = string.Join(", ", printEntry.Value
                .Select(card => CardPrintCatalog.NormalizeCardId(card.CardID))
                .Distinct()
                .OrderBy(cardId => cardId));
            Debug.LogWarning($"[CardPrintAssetAudit] Duplicate PrintID candidate {printEntry.Key} ({printEntry.Value.Count} assets): {affectedCards}");
        }

        Dictionary<string, List<CEntity_Base>> cardsByStoredPrintId = cards
            .Select(card => new
            {
                Card = card,
                PrintId = CardPrintCatalog.NormalizeLookupCode(card?.PrintID),
            })
            .Where(entry => entry.Card != null && !string.IsNullOrWhiteSpace(entry.PrintId))
            .GroupBy(entry => entry.PrintId)
            .ToDictionary(entry => entry.Key, entry => entry.Select(value => value.Card).ToList());

        foreach (KeyValuePair<string, List<CEntity_Base>> printEntry in cardsByStoredPrintId)
        {
            if (printEntry.Value.Count <= 1)
            {
                continue;
            }

            duplicateStoredPrintGroups++;
            string affectedPaths = string.Join(", ", printEntry.Value
                .Select(AssetDatabase.GetAssetPath)
                .OrderBy(path => path, System.StringComparer.OrdinalIgnoreCase));
            Debug.LogError($"[CardPrintAssetAudit] Duplicate stored PrintID {printEntry.Key} ({printEntry.Value.Count} assets): {affectedPaths}");
        }

        foreach (IGrouping<string, CEntity_Base> group in cards
            .Where(card => !string.IsNullOrWhiteSpace(card.CardID))
            .GroupBy(card => CardPrintCatalog.NormalizeCardId(card.CardID)))
        {
            bool inTargetScope = targetCardIds == null || targetCardIds.Contains(group.Key);
            List<CEntity_Base> prints = group
                .Where(card => card != null)
                .OrderBy(card => card.CardIndex)
                .ToList();

            List<string> distinctEffectClasses = prints
                .Select(card => (card.CardEffectClassName ?? string.Empty).Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct()
                .ToList();

            if (distinctEffectClasses.Count > 1)
            {
                mixedEffectGroups++;
                Debug.LogError($"[CardPrintAssetAudit] Mixed effect classes for {group.Key}: {string.Join(", ", distinctEffectClasses)}");
            }

            CEntity_Base canonicalPrint = CardPrintCatalog.SelectCanonicalPrint(prints);
            if (canonicalPrint == null)
            {
                missingCanonicalGroups++;
                Debug.LogError($"[CardPrintAssetAudit] No canonical print could be determined for {group.Key}.");
                continue;
            }

            bool groupTouched = false;
            foreach (CEntity_Base card in prints)
            {
                bool changed = false;

                string suggestedPrintId = CardPrintCatalog.SuggestPrintId(card);
                if (string.IsNullOrWhiteSpace(card.PrintID) || CardPrintCatalog.NormalizeStoredPrintId(card.PrintID) != suggestedPrintId)
                {
                    if (applyFixes && inTargetScope)
                    {
                        card.PrintID = suggestedPrintId;
                        changed = true;
                    }
                }

                bool shouldBeCanonical = card == canonicalPrint;
                if (card.IsCanonicalPrint != shouldBeCanonical)
                {
                    if (applyFixes && inTargetScope)
                    {
                        card.IsCanonicalPrint = shouldBeCanonical;
                        changed = true;
                    }
                    else if (shouldBeCanonical)
                    {
                        missingCanonicalGroups++;
                    }
                }

                if (applyFixes && inTargetScope && changed)
                {
                    touchedCount++;
                    groupTouched = true;
                    EditorUtility.SetDirty(card);
                }
            }

            if (groupTouched)
            {
                touchedGroups++;
            }
        }

        if (applyFixes && touchedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CardPrintCatalog.ResetCache();
        }

        string action = applyFixes ? "backfill" : "audit";
        Debug.Log($"[CardPrintAssetAudit] Completed {action} scope={scopeLabel}. touchedAssets={touchedCount} touchedGroups={touchedGroups} duplicatePrintGroups={duplicatePrintGroups} duplicateStoredPrintGroups={duplicateStoredPrintGroups} mixedEffectGroups={mixedEffectGroups} missingCanonicalGroups={missingCanonicalGroups}");
    }

    static HashSet<string> GetSelectedCardIds()
    {
        HashSet<string> selectedCardIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        foreach (string guid in Selection.assetGUIDs ?? new string[0])
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            IEnumerable<CEntity_Base> selectedCards = AssetDatabase.IsValidFolder(path)
                ? AssetDatabase.FindAssets("t:CEntity_Base", new[] { path })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<CEntity_Base>)
                : new[] { AssetDatabase.LoadAssetAtPath<CEntity_Base>(path) };

            foreach (CEntity_Base card in selectedCards)
            {
                string normalizedCardId = CardPrintCatalog.NormalizeCardId(card?.CardID);
                if (!string.IsNullOrWhiteSpace(normalizedCardId))
                {
                    selectedCardIds.Add(normalizedCardId);
                }
            }
        }

        return selectedCardIds;
    }
}
