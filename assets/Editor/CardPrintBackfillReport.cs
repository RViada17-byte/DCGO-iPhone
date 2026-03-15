using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CardPrintBackfillReport
{
    const string CardAssetRoot = "Assets/CardBaseEntity";
    const string ReportRelativePath = "Logs/CardPrintBackfillReport.md";

    [MenuItem("DCGO/Card Prints/Export Backfill Report")]
    public static void Export()
    {
        List<CEntity_Base> cards = AssetDatabase.FindAssets("t:CEntity_Base", new[] { CardAssetRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => AssetDatabase.LoadAssetAtPath<CEntity_Base>(path))
            .Where(card => card != null)
            .OrderBy(card => AssetDatabase.GetAssetPath(card), StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<string> lines = new List<string>
        {
            "# Card Print Backfill Report",
            string.Empty,
            $"Generated: {DateTime.UtcNow:O}",
            $"Asset Count: {cards.Count}",
            string.Empty,
            "## Isolated Commit Workflow",
            "1. Review duplicate groups and land any duplicate cleanup before metadata backfill.",
            "2. Select one or more card assets or folders for the card groups you want to patch.",
            "3. Run `DCGO/Card Prints/Backfill Print Metadata` to backfill only the selected card groups.",
            "4. Review `git diff -- Assets/CardBaseEntity` and confirm only the intended groups changed.",
            "5. Commit only the intended card assets plus any duplicate-cleanup changes.",
            string.Empty,
            "Use `DCGO/Card Prints/Backfill All Print Metadata` only for an intentional library-wide metadata sweep.",
            string.Empty,
        };

        AppendDuplicatePrintGroups(lines, cards);
        AppendMetadataChanges(lines, cards);

        string reportPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ReportRelativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? Directory.GetCurrentDirectory());
        File.WriteAllLines(reportPath, lines);
        Debug.Log($"[CardPrintBackfillReport] Wrote report to {reportPath}");
        AssetDatabase.Refresh();
    }

    static void AppendDuplicatePrintGroups(List<string> lines, List<CEntity_Base> cards)
    {
        List<IGrouping<string, CEntity_Base>> duplicateGroups = cards
            .GroupBy(card => $"{CardPrintCatalog.NormalizeCardId(card.CardID)}::{CardPrintCatalog.NormalizeLookupCode(CardPrintCatalog.SuggestPrintId(card))}")
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        lines.Add("## Duplicate Print Groups");
        if (duplicateGroups.Count == 0)
        {
            lines.Add("None.");
            lines.Add(string.Empty);
            return;
        }

        foreach (IGrouping<string, CEntity_Base> group in duplicateGroups)
        {
            List<CEntity_Base> duplicates = group
                .OrderByDescending(IsInExpectedKindFolder)
                .ThenBy(card => AssetDatabase.GetAssetPath(card), StringComparer.OrdinalIgnoreCase)
                .ToList();
            CEntity_Base survivor = duplicates[0];
            IEnumerable<int> legacyAliases = duplicates
                .Skip(1)
                .SelectMany(card => (card.LegacyCardIndices ?? new List<int>()).Concat(new[] { card.CardIndex }))
                .Distinct()
                .OrderBy(value => value);

            lines.Add($"### {group.Key.Replace("::", " / ")}");
            lines.Add($"Survivor: `{AssetDatabase.GetAssetPath(survivor)}`");
            lines.Add($"Suggested legacy index aliases on survivor: `{string.Join(", ", legacyAliases)}`");
            lines.Add(string.Empty);

            foreach (CEntity_Base duplicate in duplicates)
            {
                lines.Add($"- `{AssetDatabase.GetAssetPath(duplicate)}` | CardIndex `{duplicate.CardIndex}` | Kind `{duplicate.cardKind}` | Print `{CardPrintCatalog.SuggestPrintId(duplicate)}`");
            }

            lines.Add(string.Empty);
        }
    }

    static void AppendMetadataChanges(List<string> lines, List<CEntity_Base> cards)
    {
        List<MetadataChange> changes = cards
            .Select(card => new MetadataChange(
                AssetDatabase.GetAssetPath(card),
                CardPrintCatalog.NormalizeStoredPrintId(card.PrintID),
                CardPrintCatalog.SuggestPrintId(card),
                card.IsCanonicalPrint,
                CardPrintCatalog.SelectCanonicalPrint(cards.Where(candidate => candidate != null && CardPrintCatalog.NormalizeCardId(candidate.CardID) == CardPrintCatalog.NormalizeCardId(card.CardID))) == card))
            .Where(change => change.NeedsUpdate)
            .OrderBy(change => change.AssetPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        lines.Add("## Metadata Changes");
        if (changes.Count == 0)
        {
            lines.Add("None.");
            lines.Add(string.Empty);
            return;
        }

        foreach (MetadataChange change in changes)
        {
            lines.Add($"- `{change.AssetPath}` | PrintID `{Display(change.CurrentPrintId)}` -> `{Display(change.ProposedPrintId)}` | Canonical `{change.CurrentCanonical}` -> `{change.ProposedCanonical}`");
        }

        lines.Add(string.Empty);
    }

    static bool IsInExpectedKindFolder(CEntity_Base card)
    {
        if (card == null || !DataBase.CardKindENNameDictionary.TryGetValue(card.cardKind, out string kindFolder))
        {
            return false;
        }

        string assetPath = AssetDatabase.GetAssetPath(card).Replace('\\', '/');
        return assetPath.Contains($"/{kindFolder}/", StringComparison.OrdinalIgnoreCase);
    }

    static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value;
    }

    readonly struct MetadataChange
    {
        public MetadataChange(string assetPath, string currentPrintId, string proposedPrintId, bool currentCanonical, bool proposedCanonical)
        {
            AssetPath = assetPath;
            CurrentPrintId = currentPrintId;
            ProposedPrintId = proposedPrintId;
            CurrentCanonical = currentCanonical;
            ProposedCanonical = proposedCanonical;
        }

        public string AssetPath { get; }
        public string CurrentPrintId { get; }
        public string ProposedPrintId { get; }
        public bool CurrentCanonical { get; }
        public bool ProposedCanonical { get; }

        public bool NeedsUpdate =>
            !string.Equals(CurrentPrintId, ProposedPrintId, StringComparison.OrdinalIgnoreCase) ||
            CurrentCanonical != ProposedCanonical;
    }
}
