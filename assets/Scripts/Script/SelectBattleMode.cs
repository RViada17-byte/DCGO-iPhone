using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Linq;

public class SelectBattleMode : MonoBehaviour
{
    [Header("バトルモード選択")]
    public YesNoObject selectBattleModeWindow;

    [Header("ルームマッチ選択")]
    [SerializeField] YesNoObject selectRoomMatchWindow;

    [Header("ルームID入力")]
    [SerializeField] EnterRoom enterRoom;

    [Header("ルームマッチマネージャ")]
    [SerializeField] RoomManager roomManager;

    [Header("LoadingObject")]
    public LoadingObject loadingObject;

    public void OffSelectBattleMode()
    {
        Off();
    }

    public void Off()
    {
        this.gameObject.SetActive(false);
    }

    bool connecting = false;

    public void SetUpSelectBattleMode()
    {
        if (connecting)
        {
            return;
        }

        ContinuousController.instance.StartCoroutine(SetUpSelectBattleModeCoroutine());
    }

    public IEnumerator SetUpSelectBattleModeCoroutine()
    {
        selectBattleModeWindow.CloseOnButtonClicked = false;
        selectRoomMatchWindow.CloseOnButtonClicked = false;

        selectBattleModeWindow.Off();
        selectRoomMatchWindow.Off();
        enterRoom.Off();

        Opening.instance.battle.selectBattleDeck.Off();

        connecting = true;

        connecting = false;

        this.gameObject.SetActive(true);

        StartSelectBattleMode();
        yield break;
    }

    public void StartSelectBattleMode()
    {
        List<UnityAction> Commands = new List<UnityAction>()
            {
                () =>
                {
                    //シングルプレイヤー対戦
                    ContinuousController.instance.StartCoroutine(StartOfflineFlowCoroutine(() => StartSelectBattleDeck(true)));
                },
            };

        List<string> CommandTexts = new List<string>()
            {
                LocalizeUtility.GetLocalizedString(
                    EngMessage:"Single Player Duel",
                    JpnMessage:"シングルプレイヤー対戦"
                ),
            };

        selectBattleModeWindow.SetUpYesNoObject(
            Commands,
            CommandTexts,
            LocalizeUtility.GetLocalizedString(
                    EngMessage: "Please select the mode to play.",
                    JpnMessage: "対戦モードを選択してください"
                ),
            true);
    }

    IEnumerator StartOfflineFlowCoroutine(UnityAction onReady)
    {
        if (connecting)
        {
            yield break;
        }

        connecting = true;
        BootstrapConfig.SetMode(GameMode.OfflineLocal);

        yield return ContinuousController.instance.StartCoroutine(loadingObject.StartLoading("Preparing Offline Match"));
        yield return ContinuousController.instance.StartCoroutine(MatchTransportFactory.CurrentTransport.ConnectToMasterServer());
        yield return ContinuousController.instance.StartCoroutine(loadingObject.EndLoading());

        connecting = false;
        onReady?.Invoke();
    }

    void StartSelectBattleDeck(bool isAI)
    {
        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();

        enterRoom.Close_(false);
        selectRoomMatchWindow.Close_(false);

        ContinuousController.instance.isAI = isAI;
        ContinuousController.instance.isRandomMatch = true;
        ContinuousController.instance.EnemyDeckData = null;
        BootstrapConfig.SetMode(isAI ? GameMode.OfflineLocal : GameMode.Online);
        BootstrapConfig.ClearOfflineDuelConfig();

        Opening.instance.battle.selectBattleDeck.Off();

        if (!ContinuousController.instance.isAI)
        {
            Opening.instance.battle.selectBattleDeck.SetUpSelectBattleDeck(Opening.instance.battle.selectBattleDeck.OnClickSelectButton_RandomMatch, 0);
        }

        else
        {
            List<DeckData> validDeckDatas = ContinuousController.instance.DeckDatas
                .Where(deckData =>
                    deckData != null &&
                    deckData.IsValidDeckData() &&
                    DeckBuilderSetScope.IsAllowedDeck(deckData))
                .ToList();

            if (validDeckDatas.Count == 0)
            {
                List<UnityAction> Commands = new List<UnityAction>()
                {
                    () => Opening.instance.deck.SetUpDeckMode()
                };

                List<string> CommandTexts = new List<string>()
                {
                    "OK"
                };

                string message = LocalizeUtility.GetLocalizedString(
                    EngMessage: "No valid unlocked ST1-ST6/BT1-BT6 decks found.\nBuy a structure deck or build one in Deck Editor first.",
                    JpnMessage: "ST1-ST6/BT1-BT6の有効なデッキがありません。\n先にショップで購入するか、デッキ編集で作成してください。");

                Opening.instance.SetUpActiveYesNoObject(Commands, CommandTexts, message, false);
                return;
            }

            string playerDeckTitle = LocalizeUtility.GetLocalizedString(
                EngMessage: "Select Your Deck - Bot Match",
                JpnMessage: "使用デッキ選択 - Bot戦");
            string npcDeckTitle = LocalizeUtility.GetLocalizedString(
                EngMessage: "Select NPC Deck - Bot Match",
                JpnMessage: "NPCデッキ選択 - Bot戦");

            Opening.instance.battle.selectBattleDeck.SetUpSelectBattleDeck(
                () =>
                {
                    DeckData selectedPlayerDeck = Opening.instance.battle.selectBattleDeck.GetSelectedDeckData();
                    if (selectedPlayerDeck == null)
                    {
                        return;
                    }

                    ContinuousController.instance.BattleDeckData = selectedPlayerDeck;

                    List<DeckData> npcDeckDatas = validDeckDatas
                        .Where(deckData => !IsSameDeck(deckData, selectedPlayerDeck))
                        .ToList();

                    if (npcDeckDatas.Count == 0)
                    {
                        npcDeckDatas = validDeckDatas.ToList();
                    }

                    if (npcDeckDatas.Count == 0)
                    {
                        npcDeckDatas.Add(selectedPlayerDeck);
                    }

                    Opening.instance.battle.selectBattleDeck.SetUpSelectBattleDeck(
                        () =>
                        {
                            DeckData selectedNpcDeck = Opening.instance.battle.selectBattleDeck.GetSelectedDeckData();
                            if (selectedNpcDeck == null)
                            {
                                selectedNpcDeck = npcDeckDatas[0];
                            }

                            string playerSelector = BuildDeckSelector(selectedPlayerDeck);
                            string opponentSelector = BuildDeckSelector(selectedNpcDeck);
                            BootstrapConfig.ConfigureOfflineDuel(playerSelector, opponentSelector, false);

                            ContinuousController.instance.StartCoroutine(StartBattleCoroutine());
                        },
                        0,
                        npcDeckTitle,
                        npcDeckDatas);
                },
                0,
                playerDeckTitle,
                validDeckDatas);
        }

        IEnumerator StartBattleCoroutine()
        {
            selectRoomMatchWindow.Close_(false);
            enterRoom.Close_(false);
            Opening.instance.battle.selectBattleDeck.Off();

            yield return ContinuousController.instance.StartCoroutine(SinglePlayerBattleLoader.LoadBattleSceneAdditiveCoroutine());
        }
    }

    static bool IsSameDeck(DeckData left, DeckData right)
    {
        if (left == null || right == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(left.DeckID) && !string.IsNullOrWhiteSpace(right.DeckID))
        {
            return string.Equals(left.DeckID, right.DeckID, System.StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(left.DeckName, right.DeckName, System.StringComparison.OrdinalIgnoreCase);
    }

    static string BuildDeckSelector(DeckData deckData)
    {
        if (deckData == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(deckData.DeckID))
        {
            return deckData.DeckID;
        }

        return deckData.DeckName ?? "";
    }

    public void StartSelectRoomMatch()
    {
        ContinuousController.instance.StartCoroutine(StartOfflineFlowCoroutine(() => StartSelectBattleDeck(true)));
    }

    void StartCreateRoom()
    {
        roomManager.SetUpRoom();
    }

    void StartEnterRoomID()
    {
        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();

        enterRoom.SetUpEnterRoom();
    }

    public void OnClickCloseEnterRoomWindow()
    {
        enterRoom.Close_(false);
        ContinuousController.instance.PlaySE(Opening.instance.CancelSE);

        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();
    }

    public void OnClickCloseSelectRoomMatchWindow()
    {
        enterRoom.Close_(false);
        selectRoomMatchWindow.Close_(false);
        ContinuousController.instance.PlaySE(Opening.instance.CancelSE);

        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();
    }

    public void OnClickSelectBattleModeWindow()
    {
        enterRoom.Close_(true);
        selectRoomMatchWindow.Close_(false);
        selectBattleModeWindow.Close_(false);
        ContinuousController.instance.PlaySE(Opening.instance.CancelSE);

        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();

        ContinuousController.instance.StartCoroutine(OnClickSelectBattleModeWindowIEnumerator());
    }

    IEnumerator OnClickSelectBattleModeWindowIEnumerator()
    {
        yield return new WaitForSeconds(0.3f);
        Opening.instance.battle.OffBattle();
        Opening.instance.home.SetUpHome();
        this.gameObject.SetActive(false);
    }
}
