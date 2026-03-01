using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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
        if (!BootstrapConfig.ShowLegacyBattleModeChooser)
        {
            ContinuousController.instance.StartCoroutine(StartOfflineFlowCoroutine(() => StartSelectBattleDeck(true)));
            return;
        }

        List<UnityAction> Commands = new List<UnityAction>()
            {
                () =>
                {
                    //ランダムマッチ
                    ContinuousController.instance.StartCoroutine(StartOnlineFlowCoroutine(() => StartSelectBattleDeck(false)));
                },

                () =>
                {
                    //ルームマッチ
                    ContinuousController.instance.StartCoroutine(StartOnlineFlowCoroutine(StartSelectRoomMatch));
                },

                () =>
                {
                    //AI戦
                    ContinuousController.instance.StartCoroutine(StartOfflineFlowCoroutine(() => StartSelectBattleDeck(true)));
                },
            };

        List<string> CommandTexts = new List<string>()
            {
                LocalizeUtility.GetLocalizedString(
                    EngMessage:"Random Match",
                    JpnMessage:"ランダムマッチ"
                ),
                LocalizeUtility.GetLocalizedString(
                    EngMessage:"Room Match",
                    JpnMessage:"ルームマッチ"
                ),
                LocalizeUtility.GetLocalizedString(
                    EngMessage:"Bot Match",
                    JpnMessage:"Bot戦"
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

    IEnumerator StartOnlineFlowCoroutine(UnityAction onReady)
    {
        if (connecting)
        {
            yield break;
        }

        connecting = true;
        BootstrapConfig.SetMode(GameMode.Online);

        yield return ContinuousController.instance.StartCoroutine(loadingObject.StartLoading("Connecting"));
        yield return ContinuousController.instance.StartCoroutine(PhotonUtility.ConnectToLobbyCoroutine());
        yield return ContinuousController.instance.StartCoroutine(PhotonUtility.DeleteBattleDeckData());
        yield return ContinuousController.instance.StartCoroutine(loadingObject.EndLoading());

        connecting = false;
        onReady?.Invoke();
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
        yield return ContinuousController.instance.StartCoroutine(PhotonUtility.DeleteBattleDeckData());
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
        BootstrapConfig.SetMode(isAI ? GameMode.OfflineLocal : GameMode.Online);

        if (!BootstrapConfig.AutoStartOfflineDuel)
        {
            BootstrapConfig.ClearOfflineDuelConfig();
        }

        Opening.instance.battle.selectBattleDeck.Off();

        if (!ContinuousController.instance.isAI)
        {
            Opening.instance.battle.selectBattleDeck.SetUpSelectBattleDeck(Opening.instance.battle.selectBattleDeck.OnClickSelectButton_RandomMatch, 0);
        }

        else
        {
            if (BootstrapConfig.AutoStartOfflineDuel)
            {
                DeckData playerDeck = null;

                if (!string.IsNullOrWhiteSpace(BootstrapConfig.OfflinePlayerDeckSelector))
                {
                    playerDeck = ContinuousController.instance.FindDeckDataBySelector(BootstrapConfig.OfflinePlayerDeckSelector);
                }

                if (playerDeck == null)
                {
                    playerDeck = ContinuousController.instance.FindDeckDataBySelector("ST1 Demo");
                }

                if (playerDeck == null)
                {
                    playerDeck = ContinuousController.instance.FirstValidDeckData();
                }

                if (playerDeck != null)
                {
                    ContinuousController.instance.BattleDeckData = playerDeck;
                }

                ContinuousController.instance.StartCoroutine(StartBattleCoroutine());
                return;
            }

            List<DeckData> validDeckDatas = ContinuousController.instance.DeckDatas
                .Where(deckData => deckData != null && deckData.IsValidDeckData())
                .ToList();

            if (validDeckDatas.Count == 0)
            {
                DeckData fallbackDeck = ContinuousController.instance.FirstValidDeckData();
                if (fallbackDeck != null)
                {
                    validDeckDatas.Add(fallbackDeck);
                }
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

            ContinuousController.instance.StartCoroutine(Opening.instance.OpeningBGM.FadeOut(0.1f));
            yield return ContinuousController.instance.StartCoroutine(Opening.instance.LoadingObject.StartLoading("Now Loading"));

            if (ContinuousController.instance.isAI)
            {
                yield return ContinuousController.instance.StartCoroutine(MatchTransportFactory.CurrentTransport.EnsureSoloRoom());
            }

            foreach (Camera camera in Opening.instance.openingCameras)
            {
                camera.gameObject.SetActive(false);
            }

            Opening.instance.OffYesNoObjects();

            Opening.instance.deck.trialDraw.Close();

            Opening.instance.deck.deckListPanel.Close();

            yield return new WaitForSeconds(0.1f);
            SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Additive);
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
        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();

        Opening.instance.battle.selectBattleDeck.Off();
        ContinuousController.instance.isAI = false;
        BootstrapConfig.SetMode(GameMode.Online);

        List<UnityAction> Commands = new List<UnityAction>()
            {
                () =>
                {
                    //部屋を作る
                    StartCreateRoom();
                },

                () =>
                {
                    //部屋に入る
                    StartEnterRoomID();
                },
            };

        List<string> CommandTexts = new List<string>()
            {
                LocalizeUtility.GetLocalizedString(
                    EngMessage:"Create Room",
                    JpnMessage:"ルーム作成"
                ),
                LocalizeUtility.GetLocalizedString(
                    EngMessage:"Join Room",
                    JpnMessage:"ルームに入る"
                ),
            };

        selectRoomMatchWindow.SetUpYesNoObject(
            Commands,
            CommandTexts,
            LocalizeUtility.GetLocalizedString(
                    EngMessage: "Please choose between creating a room or joining an existing one.",
                    JpnMessage: "ルームを作成するかルームに入るか\n選択してください"
                ),
            true);
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
