using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Linq;

public class SelectBattleDeck : MonoBehaviour
{
    [Header("Deck selection object")]
    public GameObject SelectDeckObject;

    [Header("Deck Information Tab Prefab")]
    public DeckInfoPrefab deckInfoPrefab;

    [Header("ScrollRect to place deck information tabs")]
    public ScrollRect deckInfoPrefabParentScroll;

    [Header("Deck Information Panel")]
    public DeckInfoPanel deckInfoPanel;

    [Header("Animator")]
    public Animator anim;

    [Header("Deck Information Panel")]
    public Button SelectDeckButton;

    [Header("LoadingObject")]
    public LoadingObject loadingObject;

    [Header("Invalid deck display")]
    public GameObject InvalidDeckObject;

    [Header("タイトルテキスト")]
    public Text TitleText;

    readonly List<DeckData> _activeDeckDatas = new List<DeckData>();

    public DeckData GetSelectedDeckData()
    {
        if (deckInfoPanel == null)
        {
            return null;
        }

        return deckInfoPanel.ShowingDeckData;
    }

    private bool DeckContainsLockedCards(DeckData deck)
    {
        if (deck == null || ProgressionManager.Instance == null)
        {
            return false;
        }

        ProgressionManager.Instance.LoadOrCreate();
        HashSet<string> unlockedCardIds = ProgressionManager.Instance.GetUnlockedCardIdSetSnapshot();

        foreach (CEntity_Base card in deck.AllDeckCards())
        {
            if (card == null)
            {
                continue;
            }

            string normalizedCardId = CardPrintCatalog.NormalizeCardId(card.CardID);
            if (!string.IsNullOrEmpty(normalizedCardId) && !unlockedCardIds.Contains(normalizedCardId))
            {
                return true;
            }
        }

        return false;
    }

    bool IsDeckSelectable(DeckData deck)
    {
        if (deck == null)
        {
            return false;
        }

        if ((deck.DeckCardRefs == null || deck.DeckCardRefs.Count == 0) &&
            (deck.DeckCardIDs == null || deck.DeckCardIDs.Count == 0))
        {
            return false;
        }

        return deck.IsValidDeckData() && !DeckContainsLockedCards(deck);
    }

    void RefreshInvalidDeckObject()
    {
        if (InvalidDeckObject == null)
        {
            return;
        }

        DeckData deck = GetSelectedDeckData();

        if (deck == null)
        {
            InvalidDeckObject.SetActive(false);
            return;
        }

        InvalidDeckObject.SetActive(!deck.IsValidDeckData() || DeckContainsLockedCards(deck));
    }

    public void OnClickEditDeckButton()
    {
        Opening.instance.deck.editDeck.EndEditAction = () =>
        {
            SetSelectDeckButton();
            RefreshInvalidDeckObject();
        };
    }

    public void SetSelectDeckButton()
    {
        if (SelectDeckButton == null)
        {
            return;
        }

        SelectDeckButton.interactable = IsDeckSelectable(GetSelectedDeckData());
    }

    bool once = false;
    public void OnClickSelectButton_RandomMatch()
    {
        if (once || !IsDeckSelectable(GetSelectedDeckData()))
        {
            return;
        }

        ContinuousController.instance.StartCoroutine(SetOnce());

        ContinuousController.instance.BattleDeckData = deckInfoPanel.ShowingDeckData;

        Opening.instance.battle.lobbyManager_RandomMatch.SetUpLobby();
    }

    public void OnClickSelectButton_BotMatch()
    {
        if (once || !IsDeckSelectable(GetSelectedDeckData()))
        {
            return;
        }

        ContinuousController.instance.StartCoroutine(SetOnce());

        ContinuousController.instance.BattleDeckData = deckInfoPanel.ShowingDeckData;
    }

    public IEnumerator OnClickSelectButton_RoomMatchCoroutine()
    {
        if (once || !IsDeckSelectable(GetSelectedDeckData()))
        {
            yield break;
        }

        ContinuousController.instance.StartCoroutine(SetOnce());

        ContinuousController.instance.BattleDeckData = deckInfoPanel.ShowingDeckData;

        Off();

        yield return ContinuousController.instance.StartCoroutine(PhotonUtility.SignUpBattleDeckData());
    }

    IEnumerator SetOnce()
    {
        once = true;
        yield return new WaitForSeconds(1f);
        once = false;
    }

    public void Off()
    {
        if (this.gameObject.activeSelf)
        {
            this.gameObject.SetActive(false);
            OnCloseSelectBattleDeckAction?.Invoke();
        }

        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();
    }

    public UnityAction OnCloseSelectBattleDeckAction;

    public async void SetUpSelectBattleDeck(
        UnityAction OnClickSelectButtonAction,
        int defaulSelectDeckIndex,
        string customTitle = null,
        IList<DeckData> customDeckDatas = null)
    {
        OnCloseSelectBattleDeckAction = null;

        //ContinuousController.instance.ModifyAllDeckDatas();

        if (!SelectDeckObject.activeSelf)
        {
            SelectDeckObject.SetActive(true);
        }

        anim.SetInteger("Open", 1);
        anim.SetInteger("Close", 0);

        _activeDeckDatas.Clear();

        IEnumerable<DeckData> sourceDecks = customDeckDatas;
        if (sourceDecks == null)
        {
            sourceDecks = ContinuousController.instance.DeckDatas;
        }

        foreach (DeckData deckData in sourceDecks)
        {
            if (deckData != null)
            {
                _activeDeckDatas.Add(deckData);
            }
        }

        ContinuousController.instance.StartCoroutine(SetDeckList(true));

        deckInfoPanel.OnClickSelectDeckAction = OnClickSelectButtonAction;

        DeckData initialDeck = null;
        if (_activeDeckDatas.Count > 0)
        {
            if (0 <= defaulSelectDeckIndex && defaulSelectDeckIndex < _activeDeckDatas.Count)
            {
                initialDeck = _activeDeckDatas[defaulSelectDeckIndex];
            }

            if (initialDeck == null &&
                ContinuousController.instance.LastBattleDeckData != null &&
                _activeDeckDatas.Contains(ContinuousController.instance.LastBattleDeckData))
            {
                initialDeck = ContinuousController.instance.LastBattleDeckData;
            }

            if (initialDeck == null)
            {
                initialDeck = _activeDeckDatas[0];
            }

            await deckInfoPanel.SetUpDeckInfoPanel(initialDeck);
        }
        else
        {
            ResetDeckInfoPanel();
        }

        TitleText.text = string.IsNullOrWhiteSpace(customTitle) ? GetDefaultTitle() : customTitle;

        SetSelectDeckButton();
        RefreshInvalidDeckObject();
    }

    public void Close()
    {
        Close_(true);
    }

    public void Close_(bool playSE)
    {
        if (playSE)
        {
            Opening.instance.PlayCancelSE();
        }

        anim.SetInteger("Open", 0);
        anim.SetInteger("Close", 1);
    }

    public void ResetDeckInfoPanel()
    {
        deckInfoPanel.SetUpDeckInfoPanel(null);
    }

    public IEnumerator SetDeckList(bool open)
    {
        for (int i = 0; i < deckInfoPrefabParentScroll.content.childCount; i++)
        {
            Destroy(deckInfoPrefabParentScroll.content.GetChild(i).gameObject);
        }

        for (int i = 0; i < _activeDeckDatas.Count; i++)
        {
            DeckInfoPrefab _deckInfoPrefab = Instantiate(deckInfoPrefab, deckInfoPrefabParentScroll.content);

            _deckInfoPrefab.scrollRect.content = deckInfoPrefabParentScroll.content;

            _deckInfoPrefab.scrollRect.viewport = deckInfoPrefabParentScroll.viewport;

            _deckInfoPrefab.scrollRect.verticalScrollbar = deckInfoPrefabParentScroll.verticalScrollbar;

            _deckInfoPrefab.SetUpDeckInfoPrefab(_activeDeckDatas[i]);

            _deckInfoPrefab.transform.localScale = Opening.instance.DeckInfoPrefabStartScale * 1.02f;

            _deckInfoPrefab.OnClickAction = (deckdata) =>
            {
                deckInfoPanel.SetUpDeckInfoPanel(deckdata);

                SetSelectDeckButton();
                RefreshInvalidDeckObject();

                Opening.instance.CreateOnClickEffect();
            };
        }

        yield return null;

        for (int i = 0; i < deckInfoPrefabParentScroll.content.childCount; i++)
        {
            deckInfoPrefabParentScroll.content.GetChild(i).transform.localScale = Opening.instance.DeckInfoPrefabStartScale;
        }

        if (_activeDeckDatas.Count == 0)
        {

        }

        else
        {
            for (int i = 0; i < deckInfoPrefabParentScroll.content.childCount; i++)
            {
                if (deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<DeckInfoPrefab>() != null)
                {
                    if (deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<DeckInfoPrefab>().thisDeckData == deckInfoPanel.ShowingDeckData && deckInfoPanel.DeckInfoPanelObject.activeSelf)
                    {
                        deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<DeckInfoPrefab>().Outline.SetActive(true);
                    }

                    else
                    {
                        deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<DeckInfoPrefab>().Outline.SetActive(false);
                    }
                }
            }
        }

        if (open)
        {
            yield return new WaitForSeconds(Time.deltaTime);
            deckInfoPrefabParentScroll.verticalNormalizedPosition = 1;
        }
    }

    string GetDefaultTitle()
    {
        if (ContinuousController.instance.isAI)
        {
            return LocalizeUtility.GetLocalizedString(
                EngMessage: "Select Your Deck - Bot Match",
                JpnMessage: "使用デッキ選択 - Bot戦");
        }

        if (ContinuousController.instance.isRandomMatch)
        {
            return LocalizeUtility.GetLocalizedString(
                EngMessage: "Select Your Deck - Random Match",
                JpnMessage: "使用デッキ選択 - ランダムマッチ");
        }

        return LocalizeUtility.GetLocalizedString(
            EngMessage: "Select Your Deck - Room Match",
            JpnMessage: "使用デッキ選択 - ルームマッチ");
    }
}
