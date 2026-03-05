using UnityEngine;

public class SinglePlayerMenuController : MonoBehaviour
{
    [Header("Panel Roots")]
    [SerializeField] private GameObject ShopModeRoot;
    [SerializeField] private GameObject StoryModeRoot;
    [SerializeField] private GameObject DuelistBoardModeRoot;

    public void OpenShop()
    {
        OpenMode(ShopModeRoot);
    }

    public void OpenStory()
    {
        OpenMode(StoryModeRoot);
    }

    public void OpenDuelistBoard()
    {
        OpenMode(DuelistBoardModeRoot);
    }

    public void CloseAllSinglePlayerModes()
    {
        SetActiveSafe(ShopModeRoot, false);
        SetActiveSafe(StoryModeRoot, false);
        SetActiveSafe(DuelistBoardModeRoot, false);
    }

    public void BackToHome()
    {
        CloseAllSinglePlayerModes();

        Opening opening = Opening.instance;
        if (!CanCallSetUpHome(opening))
        {
            SafeShowModeButtons(opening);
            SafeCloseMiscPopups(opening);
            return;
        }

        opening.home.SetUpHome();
    }

    private void OpenMode(GameObject targetRoot)
    {
        Opening opening = Opening.instance;

        SafeOffDeck(opening);
        SafeOffBattle(opening);
        SafeCloseMiscPopups(opening);
        SafeShowModeButtons(opening);

        CloseAllSinglePlayerModes();
        SetActiveSafe(targetRoot, true);
    }

    private static void SafeOffDeck(Opening opening)
    {
        if (!CanCallOffDeck(opening))
        {
            return;
        }

        opening.deck.OffDeck();
    }

    private static bool CanCallOffDeck(Opening opening)
    {
        if (opening == null || opening.deck == null)
        {
            return false;
        }

        DeckMode deck = opening.deck;
        return deck.DeckButton != null
            && deck.selectDeck != null
            && deck.editDeck != null
            && deck.editDeck.CreateDeckObject != null
            && deck.trialDraw != null
            && deck.deckListPanel != null;
    }

    private static void SafeOffBattle(Opening opening)
    {
        if (!CanCallOffBattle(opening))
        {
            return;
        }

        opening.battle.OffBattle();
    }

    private static bool CanCallOffBattle(Opening opening)
    {
        if (opening == null || opening.battle == null)
        {
            return false;
        }

        BattleMode battle = opening.battle;
        return battle.BattleButton != null
            && battle.selectBattleMode != null
            && battle.selectBattleDeck != null
            && battle.lobbyManager_RandomMatch != null
            && battle.roomManager != null;
    }

    private static void SafeCloseMiscPopups(Opening opening)
    {
        if (opening != null)
        {
            opening.OffYesNoObjects();
        }

        if (opening != null && opening.optionPanel != null)
        {
            opening.optionPanel.CloseOptionPanel();
        }
    }

    private static void SafeShowModeButtons(Opening opening)
    {
        if (opening != null && opening.ModeButtons != null)
        {
            opening.OnModeButtons();
        }
    }

    private static bool CanCallSetUpHome(Opening opening)
    {
        if (opening == null || opening.home == null || opening.ModeButtons == null)
        {
            return false;
        }

        HomeMode home = opening.home;
        DeckMode deck = opening.deck;

        if (home.playerInfo == null || opening.optionPanel == null || deck == null)
        {
            return false;
        }

        if (deck.trialDraw == null || deck.deckListPanel == null || deck.selectDeck == null)
        {
            return false;
        }

        if (deck.selectDeck.deckInfoPrefabParentScroll == null)
        {
            return false;
        }

        return deck.selectDeck.deckInfoPrefabParentScroll.content != null;
    }

    private static void SetActiveSafe(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }
}
