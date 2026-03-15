using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SinglePlayerWorldDuelLauncher
{
    public static bool TryLaunchWithDeckSelection(
        MonoBehaviour host,
        DeckData enemyDeck,
        Action onConfirmLaunch,
        out string error)
    {
        error = string.Empty;

        if (host == null)
        {
            error = "World Duel launcher is unavailable.";
            return false;
        }

        if (enemyDeck == null || !enemyDeck.IsValidDeckData())
        {
            error = "This duel is not authored yet.";
            return false;
        }

        Opening opening = Opening.instance;
        if (opening == null || opening.battle == null || opening.battle.selectBattleDeck == null)
        {
            error = "Deck selection is unavailable.";
            return false;
        }

        ContinuousController controller = ContinuousController.instance;
        if (controller == null)
        {
            error = "Game controller is unavailable.";
            return false;
        }

        ProgressionManager.Instance.LoadOrCreate();

        List<DeckData> selectableDecks = GetSelectableDecks(controller);
        if (selectableDecks.Count == 0)
        {
            error = LocalizeUtility.GetLocalizedString(
                EngMessage: "No valid unlocked decks are available. Build or unlock one in Deck Editor first.",
                JpnMessage: "使用可能な有効デッキがありません。先にデッキ編集で作成するか、必要なカードを入手してください。");
            return false;
        }

        controller.isAI = true;
        controller.isRandomMatch = false;
        controller.EnemyDeckData = null;
        BootstrapConfig.SetMode(GameMode.OfflineLocal);
        BootstrapConfig.ClearOfflineDuelConfig();

        opening.OffYesNoObjects();
        if (opening.deck != null)
        {
            if (opening.deck.trialDraw != null)
            {
                opening.deck.trialDraw.Close();
            }

            if (opening.deck.deckListPanel != null)
            {
                opening.deck.deckListPanel.Close();
            }
        }

        bool launchQueued = false;
        opening.battle.selectBattleDeck.OnCloseSelectBattleDeckAction = null;
        opening.battle.selectBattleDeck.SetUpSelectBattleDeck(
            () =>
            {
                if (launchQueued)
                {
                    return;
                }

                opening.battle.selectBattleDeck.OnClickSelectButton_BotMatch();
                if (!IsSelectableDeck(controller.BattleDeckData))
                {
                    return;
                }

                launchQueued = true;
                host.StartCoroutine(LaunchSelectedWorldDuelCoroutine());
            },
            GetDefaultSelectedDeckIndex(controller, selectableDecks),
            GetDeckSelectionTitle(),
            selectableDecks);

        return true;

        IEnumerator LaunchSelectedWorldDuelCoroutine()
        {
            controller.isAI = true;
            controller.isRandomMatch = false;
            controller.EnemyDeckData = enemyDeck;
            BootstrapConfig.SetMode(GameMode.OfflineLocal);
            BootstrapConfig.ClearOfflineDuelConfig();

            onConfirmLaunch?.Invoke();

            opening.battle.selectBattleDeck.OnCloseSelectBattleDeckAction = null;
            opening.battle.selectBattleDeck.Off();

            yield return host.StartCoroutine(SinglePlayerBattleLoader.LoadBattleSceneAdditiveCoroutine());
        }
    }

    static string GetDeckSelectionTitle()
    {
        return LocalizeUtility.GetLocalizedString(
            EngMessage: "Select Your Deck - World Duel",
            JpnMessage: "使用デッキ選択 - ワールドデュエル");
    }

    static List<DeckData> GetSelectableDecks(ContinuousController controller)
    {
        if (controller == null || controller.DeckDatas == null)
        {
            return new List<DeckData>();
        }

        HashSet<string> unlockedCardIds = ProgressionManager.Instance == null
            ? null
            : ProgressionManager.Instance.GetUnlockedCardIdSetSnapshot();

        return controller.DeckDatas
            .Where(deck => IsSelectableDeck(deck, unlockedCardIds))
            .ToList();
    }

    static bool IsSelectableDeck(DeckData deck)
    {
        return IsSelectableDeck(deck, ProgressionManager.Instance == null
            ? null
            : ProgressionManager.Instance.GetUnlockedCardIdSetSnapshot());
    }

    static bool IsSelectableDeck(DeckData deck, ISet<string> unlockedCardIds)
    {
        if (deck == null ||
            (((deck.DeckCardRefs == null || deck.DeckCardRefs.Count == 0) &&
              (deck.DeckCardIDs == null || deck.DeckCardIDs.Count == 0))))
        {
            return false;
        }

        return deck.IsValidDeckData() &&
               DeckBuilderSetScope.IsAllowedDeck(deck) &&
               !DeckContainsLockedCards(deck, unlockedCardIds);
    }

    static bool DeckContainsLockedCards(DeckData deck)
    {
        return DeckContainsLockedCards(deck, ProgressionManager.Instance == null
            ? null
            : ProgressionManager.Instance.GetUnlockedCardIdSetSnapshot());
    }

    static bool DeckContainsLockedCards(DeckData deck, ISet<string> unlockedCardIds)
    {
        if (deck == null || ProgressionManager.Instance == null)
        {
            return false;
        }

        foreach (CEntity_Base card in deck.AllDeckCards())
        {
            if (card == null)
            {
                continue;
            }

            string normalizedCardId = CardPrintCatalog.NormalizeCardId(card.CardID);
            if (!string.IsNullOrEmpty(normalizedCardId) &&
                (unlockedCardIds == null || !unlockedCardIds.Contains(normalizedCardId)))
            {
                return true;
            }
        }

        return false;
    }

    static int GetDefaultSelectedDeckIndex(ContinuousController controller, IList<DeckData> selectableDecks)
    {
        if (controller == null || selectableDecks == null || selectableDecks.Count == 0)
        {
            return 0;
        }

        int selectedIndex = IndexOfDeck(selectableDecks, controller.BattleDeckData);
        if (selectedIndex >= 0)
        {
            return selectedIndex;
        }

        selectedIndex = IndexOfDeck(selectableDecks, controller.LastBattleDeckData);
        if (selectedIndex >= 0)
        {
            return selectedIndex;
        }

        return 0;
    }

    static int IndexOfDeck(IList<DeckData> decks, DeckData targetDeck)
    {
        if (decks == null || targetDeck == null)
        {
            return -1;
        }

        for (int index = 0; index < decks.Count; index++)
        {
            DeckData deck = decks[index];
            if (deck == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(deck.DeckID) &&
                !string.IsNullOrWhiteSpace(targetDeck.DeckID) &&
                string.Equals(deck.DeckID, targetDeck.DeckID, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }

            if (!string.IsNullOrWhiteSpace(deck.DeckName) &&
                !string.IsNullOrWhiteSpace(targetDeck.DeckName) &&
                string.Equals(deck.DeckName, targetDeck.DeckName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
