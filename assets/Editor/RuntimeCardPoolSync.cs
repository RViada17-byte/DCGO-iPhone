using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RuntimeCardPoolSync
{
    const string ScenePath = "Assets/Scenes/ContinuousControllerScene.unity";
    const string CardAssetRoot = "Assets/CardBaseEntity";

    [MenuItem("DCGO/Card Pool/Sync ContinuousController Scene")]
    public static void SyncContinuousControllerSceneMenu()
    {
        SyncContinuousControllerScene();
    }

    public static void SyncContinuousControllerSceneBatch()
    {
        SyncContinuousControllerScene();
    }

    static void SyncContinuousControllerScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ContinuousController controller = UnityEngine.Object.FindFirstObjectByType<ContinuousController>();
        if (controller == null)
        {
            throw new InvalidOperationException($"Could not find ContinuousController in scene '{ScenePath}'.");
        }

        List<CEntity_Base> existingRefs = new List<CEntity_Base>();
        if (controller.CardList != null)
        {
            existingRefs.AddRange(controller.CardList.Where(card => card != null));
        }

        if (controller.SortedCardList != null)
        {
            existingRefs.AddRange(controller.SortedCardList.Where(card => card != null));
        }

        List<CEntity_Base> preservedExtras = existingRefs
            .Where(card => !IsManagedCardAsset(card))
            .ToList();

        string[] cardGuids = AssetDatabase.FindAssets("t:CEntity_Base", new[] { CardAssetRoot });
        List<CEntity_Base> supportedCards = new List<CEntity_Base>(cardGuids.Length);
        for (int index = 0; index < cardGuids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(cardGuids[index]);
            CEntity_Base card = AssetDatabase.LoadAssetAtPath<CEntity_Base>(path);
            if (card == null || !DeckBuilderSetScope.IsAllowedCard(card))
            {
                continue;
            }

            supportedCards.Add(card);
        }

        List<CEntity_Base> mergedCards = DeduplicateByGlobalId(preservedExtras.Concat(supportedCards))
            .OrderBy(card => card.CardIndex)
            .ThenBy(card => card.CardID, StringComparer.OrdinalIgnoreCase)
            .ThenBy(card => card.EffectivePrintID, StringComparer.OrdinalIgnoreCase)
            .ToList();

        controller.CardList = mergedCards.ToArray();
        controller.SortedCardList = mergedCards.ToArray();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[RuntimeCardPoolSync] Synced '{ScenePath}'. Supported={supportedCards.Count}, PreservedExtras={preservedExtras.Count}, Total={mergedCards.Count}");
    }

    static bool IsManagedCardAsset(CEntity_Base card)
    {
        string assetPath = AssetDatabase.GetAssetPath(card);
        return !string.IsNullOrWhiteSpace(assetPath) &&
               assetPath.Replace("\\", "/").StartsWith(CardAssetRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    static List<CEntity_Base> DeduplicateByGlobalId(IEnumerable<CEntity_Base> cards)
    {
        List<CEntity_Base> result = new List<CEntity_Base>();
        HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (CEntity_Base card in cards)
        {
            if (card == null)
            {
                continue;
            }

            string id = GlobalObjectId.GetGlobalObjectIdSlow(card).ToString();
            if (!seenIds.Add(id))
            {
                continue;
            }

            result.Add(card);
        }

        return result;
    }
}
