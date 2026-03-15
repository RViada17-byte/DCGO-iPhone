using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Events;

public class SelectDeck : OffAnimation
{
    struct RectTransformState
    {
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 OffsetMin;
        public Vector2 OffsetMax;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public Vector2 Pivot;
        public Vector3 LocalScale;

        public static RectTransformState Capture(RectTransform rectTransform)
        {
            return new RectTransformState
            {
                AnchorMin = rectTransform.anchorMin,
                AnchorMax = rectTransform.anchorMax,
                OffsetMin = rectTransform.offsetMin,
                OffsetMax = rectTransform.offsetMax,
                AnchoredPosition = rectTransform.anchoredPosition,
                SizeDelta = rectTransform.sizeDelta,
                Pivot = rectTransform.pivot,
                LocalScale = rectTransform.localScale,
            };
        }

        public void Restore(RectTransform rectTransform)
        {
            rectTransform.anchorMin = AnchorMin;
            rectTransform.anchorMax = AnchorMax;
            rectTransform.offsetMin = OffsetMin;
            rectTransform.offsetMax = OffsetMax;
            rectTransform.anchoredPosition = AnchoredPosition;
            rectTransform.sizeDelta = SizeDelta;
            rectTransform.pivot = Pivot;
            rectTransform.localScale = LocalScale;
        }
    }

    [Header("Select Deck Object")]
    public GameObject SelectDeckObject;

    [Header("Deck Info Tab Prefab")]
    public DeckInfoPrefab deckInfoPrefab;

    [Header("ScrollRect to place deck information tabs")]
    public ScrollRect deckInfoPrefabParentScroll;

    [Header("Deck Info Panel")]
    public DeckInfoPanel deckInfoPanel;

    [Header("Animator")]
    public Animator anim;

    public bool isOpen { get; set; } = false;
    IPhoneSafeAreaRoot _iPhoneSafeAreaRoot;
    Image _iPhoneBackdrop;
    bool _editorPreviewActive;
    bool _capturedRectState;
    RectTransformState _selectDeckRectState;

    bool UseIPhoneFullscreenLayout => _editorPreviewActive;

    void LateUpdate()
    {
        if (!UseIPhoneFullscreenLayout || SelectDeckObject == null || !SelectDeckObject.activeSelf)
        {
            return;
        }

        ApplyIPhoneFullscreenLayout();
    }

    public void OffSelectDeck()
    {
        anim.enabled = true;
        anim.SetInteger("Open", 0);
        anim.SetInteger("Close", 1);

        isOpen = false;
    }

    public override void Off()
    {
        SelectDeckObject.SetActive(false);
    }

    public async void SetUpSelectDeck()
    {
        if (isOpen)
        {
            return;
        }

        int lastBattleDeckDataIndex = -1;

        if (ContinuousController.instance.LastBattleDeckData != null
            && ContinuousController.instance.DeckDatas.Contains(ContinuousController.instance.LastBattleDeckData))
        {
            lastBattleDeckDataIndex = ContinuousController.instance.DeckDatas.IndexOf(ContinuousController.instance.LastBattleDeckData);
        }

        if (NeedsDeckNormalization())
        {
            ContinuousController.instance.ModifyAllDeckDatas();
        }

        if (0 <= lastBattleDeckDataIndex && lastBattleDeckDataIndex <= ContinuousController.instance.DeckDatas.Count - 1)
        {
            ContinuousController.instance.BattleDeckData = ContinuousController.instance.DeckDatas[lastBattleDeckDataIndex];
        }

        Opening.instance.deck.deckListPanel.Off();

        isOpen = true;

        ContinuousController.instance.StartCoroutine(SetDeckList(true));

        SelectDeckObject.SetActive(true);
        ApplyIPhoneFullscreenLayout(force: true);
        Opening.instance?.home?.OffHome();
        Opening.instance?.OffModeButtons();

        anim.SetInteger("Open", 1);
        anim.SetInteger("Close", 0);

        if (ContinuousController.instance.DeckDatas.Count > 0)
        {
            if (ContinuousController.instance.LastBattleDeckData != null
            && ContinuousController.instance.DeckDatas.Contains(ContinuousController.instance.LastBattleDeckData))
            {
                await deckInfoPanel.SetUpDeckInfoPanel(ContinuousController.instance.LastBattleDeckData);
            }

            else
            {
                await deckInfoPanel.SetUpDeckInfoPanel(ContinuousController.instance.DeckDatas[0]);
            }
        }

        else
        {
            ResetDeckInfoPanel();
        }
    }

    public async void ResetDeckInfoPanel()
    {
        await deckInfoPanel.SetUpDeckInfoPanel(null);
    }

    public IEnumerator SetDeckList(bool open)
    {
        for (int i = 0; i < deckInfoPrefabParentScroll.content.childCount; i++)
        {
            if (i > 0)
            {
                Destroy(deckInfoPrefabParentScroll.content.GetChild(i).gameObject);
            }
        }

        for (int i = 0; i < ContinuousController.instance.DeckDatas.Count; i++)
        {
            DeckInfoPrefab _deckInfoPrefab = Instantiate(deckInfoPrefab, deckInfoPrefabParentScroll.content);

            _deckInfoPrefab.scrollRect.content = deckInfoPrefabParentScroll.content;

            _deckInfoPrefab.scrollRect.viewport = deckInfoPrefabParentScroll.viewport;

            _deckInfoPrefab.scrollRect.verticalScrollbar = deckInfoPrefabParentScroll.verticalScrollbar;

            _deckInfoPrefab.SetUpDeckInfoPrefab(ContinuousController.instance.DeckDatas[i]);

            _deckInfoPrefab.transform.localScale = Opening.instance.DeckInfoPrefabStartScale * 1.02f;
            ConfigureIPhoneTouchTarget(_deckInfoPrefab.gameObject);

            _deckInfoPrefab.OnClickAction = (deckdata) =>
            {
                deckInfoPanel.SetUpDeckInfoPanel(deckdata);

                Opening.instance.CreateOnClickEffect();
            };
        }

        yield return null;

        for (int i = 0; i < deckInfoPrefabParentScroll.content.childCount; i++)
        {
            deckInfoPrefabParentScroll.content.GetChild(i).transform.localScale = Opening.instance.DeckInfoPrefabStartScale;
            ConfigureIPhoneTouchTarget(deckInfoPrefabParentScroll.content.GetChild(i).gameObject);
        }

        if (ContinuousController.instance.DeckDatas.Count == 0)
        {
            for (int i = 0; i < deckInfoPrefabParentScroll.content.childCount; i++)
            {
                if (deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<CreateNewDeckButton>() != null)
                {
                    deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<CreateNewDeckButton>().Outline.SetActive(true);
                    break;
                }
            }
        }

        else
        {
            for (int i = 0; i < deckInfoPrefabParentScroll.content.childCount; i++)
            {
                if (deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<CreateNewDeckButton>() != null)
                {
                    deckInfoPrefabParentScroll.content.GetChild(i).GetComponent<CreateNewDeckButton>().Outline.SetActive(false);
                    break;
                }
            }

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

    bool NeedsDeckNormalization()
    {
        if (ContinuousController.instance == null || ContinuousController.instance.DeckDatas == null)
        {
            return false;
        }

        foreach (DeckData deckData in ContinuousController.instance.DeckDatas)
        {
            if (deckData == null)
            {
                continue;
            }

            bool mainDeckNeedsRefs =
                (deckData.DeckCardRefs == null || deckData.DeckCardRefs.Count == 0) &&
                deckData.DeckCardIDs != null &&
                deckData.DeckCardIDs.Count > 0;

            bool digitamaDeckNeedsRefs =
                (deckData.DigitamaDeckCardRefs == null || deckData.DigitamaDeckCardRefs.Count == 0) &&
                deckData.DigitamaDeckCardIDs != null &&
                deckData.DigitamaDeckCardIDs.Count > 0;

            if (mainDeckNeedsRefs || digitamaDeckNeedsRefs)
            {
                return true;
            }
        }

        return false;
    }

    void ApplyIPhoneFullscreenLayout(bool force = false)
    {
        if (!UseIPhoneFullscreenLayout || SelectDeckObject == null)
        {
            RestoreIPhoneFullscreenLayout();
            return;
        }

        RectTransform rectTransform = SelectDeckObject.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return;
        }

        CaptureRectState(rectTransform);

        if (Application.platform == RuntimePlatform.IPhonePlayer && _iPhoneSafeAreaRoot == null)
        {
            _iPhoneSafeAreaRoot = SelectDeckObject.GetComponent<IPhoneSafeAreaRoot>();

            if (_iPhoneSafeAreaRoot == null)
            {
                _iPhoneSafeAreaRoot = SelectDeckObject.AddComponent<IPhoneSafeAreaRoot>();
            }
        }

        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            EnsureIPhoneBackdrop();
        }

        rectTransform.localScale = Vector3.one;
        rectTransform.SetAsLastSibling();

        if (force)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }

    void CaptureRectState(RectTransform rectTransform)
    {
        if (_capturedRectState)
        {
            return;
        }

        _selectDeckRectState = RectTransformState.Capture(rectTransform);
        _capturedRectState = true;
    }

    void RestoreIPhoneFullscreenLayout()
    {
        if (!_capturedRectState || SelectDeckObject == null)
        {
            return;
        }

        RectTransform rectTransform = SelectDeckObject.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return;
        }

        _selectDeckRectState.Restore(rectTransform);
    }

    public void SetEditorPreviewActive(bool active)
    {
        _editorPreviewActive = active;
        ApplyIPhoneFullscreenLayout(force: true);
    }

    void EnsureIPhoneBackdrop()
    {
        if (_iPhoneBackdrop != null || SelectDeckObject == null)
        {
            return;
        }

        Transform existingBackdrop = SelectDeckObject.transform.Find("IPhoneDeckBackdrop");
        GameObject backdropObject;

        if (existingBackdrop != null)
        {
            backdropObject = existingBackdrop.gameObject;
        }

        else
        {
            backdropObject = new GameObject("IPhoneDeckBackdrop", typeof(RectTransform), typeof(Image));
            backdropObject.transform.SetParent(SelectDeckObject.transform, false);
            backdropObject.transform.SetSiblingIndex(0);
        }

        RectTransform backdropRect = backdropObject.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        _iPhoneBackdrop = backdropObject.GetComponent<Image>();
        _iPhoneBackdrop.color = new Color32(6, 10, 18, 255);
        _iPhoneBackdrop.raycastTarget = false;
    }

    void ConfigureIPhoneTouchTarget(GameObject gameObject)
    {
        if (!UseIPhoneFullscreenLayout || gameObject == null)
        {
            return;
        }

        LayoutElement layoutElement = gameObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = Mathf.Max(layoutElement.minHeight, 190f);
        layoutElement.preferredHeight = Mathf.Max(layoutElement.preferredHeight, 190f);
    }

    public void OnClickCopyDeckListButton()
    {
        string deckListCode = "";

        for (int i = 0; i < ContinuousController.instance.DeckDatas.Count; i++)
        {
            deckListCode += DeckData.GetDeckCode(ContinuousController.instance.DeckDatas[i].DeckName, DeckData.SortedDeckCardsList(ContinuousController.instance.DeckDatas[i].DeckCards()), DeckData.SortedDeckCardsList(ContinuousController.instance.DeckDatas[i].DigitamaDeckCards()), ContinuousController.instance.DeckDatas[i].KeyCard) + "分";
        }

        if (deckListCode.Length >= 1)
        {
            GUIUtility.systemCopyBuffer = deckListCode;

            List<UnityAction> Commands = new List<UnityAction>()
                {
                    null
                };

            List<string> CommandTexts = new List<string>()
                {
                    "OK"
                };

            Opening.instance.SetUpActiveYesNoObject(Commands, CommandTexts, $"現在のデッキリストの\nコードをクリップボードにコピーしました!", false);
        }

        else
        {
            List<UnityAction> Commands = new List<UnityAction>()
                {
                    null
                };

            List<string> CommandTexts = new List<string>()
                {
                    "OK"
                };

            Opening.instance.SetUpActiveYesNoObject(Commands, CommandTexts, $"デッキリストのコード取得に失敗しました", false);
        }
    }

    public void OnClickPasterDeckListButton()
    {
        List<DeckData> gotDeckDatas = new List<DeckData>();

        string deckListCode = GUIUtility.systemCopyBuffer;

        if (!string.IsNullOrEmpty(deckListCode))
        {
            string[] deckCodes = deckListCode.Split('分');

            foreach (string deckCode in deckCodes)
            {
                DeckData deckData = new DeckData(deckCode);

                if (deckData.DeckCards().Count >= 1)
                {
                    gotDeckDatas.Add(deckData);
                }
            }
        }

        if (gotDeckDatas.Count >= 1)
        {
            gotDeckDatas.Reverse();

            foreach (DeckData deckdata in gotDeckDatas)
            {
                ContinuousController.instance.DeckDatas.Insert(0, deckdata);
                ContinuousController.instance.SaveDeckData(deckdata);
            }

            List<UnityAction> Commands = new List<UnityAction>()
                {
                    null
                };

            List<string> CommandTexts = new List<string>()
                {
                    "OK"
                };

            Opening.instance.SetUpActiveYesNoObject(Commands, CommandTexts, $"デッキリストを\n復元しました!", false);
        }

        else
        {
            List<UnityAction> Commands = new List<UnityAction>()
                {
                    null
                };

            List<string> CommandTexts = new List<string>()
                {
                    "OK"
                };

            Opening.instance.SetUpActiveYesNoObject(Commands, CommandTexts, $"デッキリストの復元に失敗しました", false);
        }

        ContinuousController.instance.StartCoroutine(SetDeckList(false));
        ContinuousController.instance.SaveDeckDatas();
    }

    public void OnClickDeleteAllDecksButton()
    {
        List<UnityAction> Commands = new List<UnityAction>()
                {
            () =>
            {
                List<DeckData> deckDatas = new List<DeckData>();

                for(int i =0;i<ContinuousController.instance.DeckDatas.Count;i++)
                {
                    deckDatas.Add(ContinuousController.instance.DeckDatas[i]);
                }

                for(int i =0;i<deckDatas.Count;i++)
                {
                    ContinuousController.instance.DeckDatas.Remove(deckDatas[i]);
                }

                ContinuousController.instance.StartCoroutine(SetDeckList(false));
                ResetDeckInfoPanel();
                ContinuousController.instance.SaveDeckDatas();
                ContinuousController.instance.DeleteAllDecks();
            }
                    ,null
                };

        List<string> CommandTexts = new List<string>()
                {
                    "削除する",
                    "しない"
                };

        Opening.instance.SetUpActiveYesNoObject(Commands, CommandTexts, $"全デッキを削除しますか?", false);
    }
}
