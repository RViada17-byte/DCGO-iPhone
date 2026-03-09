using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class MainMenuRouter : MonoBehaviour
{
    public Opening opening;
    public GameObject worldModeRoot;
    public GameObject storyModeRoot;
    public GameObject duelistBoardModeRoot;
    public GameObject shopModeRoot;

    private const string WorldModeRootName = "WorldModeRoot";
    private const string StoryModeRootName = "StoryModeRoot";
    private const string DuelistBoardModeRootName = "DuelistBoardModeRoot";
    private const string ShopModeRootName = "ShopModeRoot";

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();
    }

    public void OpenWorld()
    {
        Debug.Log($"{nameof(MainMenuRouter)}.{nameof(OpenWorld)}");

        EnsureReferences();
        HideAllCustomModesInternal();
        SafeOffDeck();
        SafeOffBattle();
        SafeCloseOptionPanel();
        SafeOffYesNoObjects();
        SafeOnModeButtons();
        SetActiveSafe(worldModeRoot, true);
    }

    public void OpenStory()
    {
        Debug.Log($"{nameof(MainMenuRouter)}.{nameof(OpenStory)}");

        EnsureReferences();
        HideAllCustomModesInternal();
        SafeOffDeck();
        SafeOffBattle();
        SafeCloseOptionPanel();
        SafeOffYesNoObjects();
        SafeOnModeButtons();
        SetActiveSafe(storyModeRoot, true);
    }

    public void OpenDuelistBoard()
    {
        Debug.Log($"{nameof(MainMenuRouter)}.{nameof(OpenDuelistBoard)}");

        EnsureReferences();
        HideAllCustomModesInternal();
        SafeOffDeck();
        SafeOffBattle();
        SafeCloseOptionPanel();
        SafeOffYesNoObjects();
        SafeOnModeButtons();
        SetActiveSafe(duelistBoardModeRoot, true);
    }

    public void OpenShop()
    {
        Debug.Log($"{nameof(MainMenuRouter)}.{nameof(OpenShop)}");

        EnsureReferences();
        HideAllCustomModesInternal();
        SafeOffDeck();
        SafeOffBattle();
        SafeCloseOptionPanel();
        SafeOffYesNoObjects();
        SafeOnModeButtons();
        SetActiveSafe(shopModeRoot, true);
    }

    public void OpenDeck()
    {
        Debug.Log($"{nameof(MainMenuRouter)}.{nameof(OpenDeck)}");

        EnsureReferences();
        HideAllCustomModesInternal();
        SafeOffBattle();
        SafeCloseOptionPanel();
        SafeOffYesNoObjects();
        SafeOnModeButtons();
        SafeSetUpDeck();
    }

    public void BackToHome()
    {
        Debug.Log($"{nameof(MainMenuRouter)}.{nameof(BackToHome)}");

        EnsureReferences();
        HideAllCustomModesInternal();
        SafeSetUpHome();
    }

    public void HideAllCustomModes()
    {
        Debug.Log($"{nameof(MainMenuRouter)}.{nameof(HideAllCustomModes)}");

        EnsureReferences();
        HideAllCustomModesInternal();
    }

    private void EnsureReferences()
    {
        if (opening == null)
        {
            opening = Opening.instance;
        }

        if (opening == null)
        {
            opening = FindOpeningInLoadedScenes();
        }

        Scene searchScene = GetSearchScene();
        if (!searchScene.IsValid() || !searchScene.isLoaded)
        {
            return;
        }

        if (worldModeRoot == null)
        {
            worldModeRoot = FindGameObjectByName(searchScene, WorldModeRootName);
        }

        if (storyModeRoot == null)
        {
            storyModeRoot = FindGameObjectByName(searchScene, StoryModeRootName);
        }

        if (duelistBoardModeRoot == null)
        {
            duelistBoardModeRoot = FindGameObjectByName(searchScene, DuelistBoardModeRootName);
        }

        if (shopModeRoot == null)
        {
            shopModeRoot = FindGameObjectByName(searchScene, ShopModeRootName);
        }
    }

    private Scene GetSearchScene()
    {
        if (opening != null)
        {
            Scene openingScene = opening.gameObject.scene;
            if (openingScene.IsValid() && openingScene.isLoaded)
            {
                return openingScene;
            }
        }

        Scene currentScene = gameObject.scene;
        if (currentScene.IsValid() && currentScene.isLoaded)
        {
            return currentScene;
        }

        return default;
    }

    private static Opening FindOpeningInLoadedScenes()
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root == null)
                {
                    continue;
                }

                Opening candidate = root.GetComponentInChildren<Opening>(true);
                if (candidate != null)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static GameObject FindGameObjectByName(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GameObject root = roots[rootIndex];
            if (root == null)
            {
                continue;
            }

            Transform match = FindChildRecursive(root.transform, objectName);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == objectName)
        {
            return parent;
        }

        for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
        {
            Transform child = parent.GetChild(childIndex);
            Transform match = FindChildRecursive(child, objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private void HideAllCustomModesInternal()
    {
        SetActiveSafe(worldModeRoot, false);
        SetActiveSafe(storyModeRoot, false);
        SetActiveSafe(duelistBoardModeRoot, false);
        SetActiveSafe(shopModeRoot, false);
    }

    private void SafeOffDeck()
    {
        if (opening == null || opening.deck == null)
        {
            return;
        }

        DeckMode deck = opening.deck;
        if (deck.DeckButton == null
            || deck.selectDeck == null
            || deck.editDeck == null
            || deck.editDeck.CreateDeckObject == null
            || deck.trialDraw == null
            || deck.deckListPanel == null)
        {
            return;
        }

        deck.OffDeck();
    }

    private void SafeOffBattle()
    {
        if (opening == null || opening.battle == null)
        {
            return;
        }

        BattleMode battle = opening.battle;
        if (battle.BattleButton == null
            || battle.selectBattleMode == null
            || battle.selectBattleDeck == null
            || battle.lobbyManager_RandomMatch == null
            || battle.roomManager == null)
        {
            return;
        }

        battle.OffBattle();
    }

    private void SafeCloseOptionPanel()
    {
        if (opening?.optionPanel != null)
        {
            opening.optionPanel.CloseOptionPanel();
        }
    }

    private void SafeOffYesNoObjects()
    {
        if (opening != null)
        {
            opening.OffYesNoObjects();
        }
    }

    private void SafeOnModeButtons()
    {
        if (opening != null && opening.ModeButtons != null)
        {
            opening.OnModeButtons();
        }
    }

    private void SafeSetUpHome()
    {
        if (opening == null || opening.home == null || opening.ModeButtons == null)
        {
            return;
        }

        HomeMode home = opening.home;
        DeckMode deck = opening.deck;

        if (home.playerInfo == null || opening.optionPanel == null || deck == null)
        {
            return;
        }

        if (deck.trialDraw == null || deck.deckListPanel == null || deck.selectDeck == null)
        {
            return;
        }

        if (deck.selectDeck.deckInfoPrefabParentScroll == null || deck.selectDeck.deckInfoPrefabParentScroll.content == null)
        {
            return;
        }

        home.SetUpHome();
    }

    private void SafeSetUpDeck()
    {
        if (opening == null || opening.deck == null)
        {
            return;
        }

        DeckMode deck = opening.deck;
        if (deck.selectDeck == null || deck.editDeck == null || deck.trialDraw == null || deck.deckListPanel == null)
        {
            return;
        }

        deck.SetUpDeckMode();
    }

    private static void SetActiveSafe(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }
}
