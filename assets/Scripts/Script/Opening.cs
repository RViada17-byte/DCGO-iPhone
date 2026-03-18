using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;
using System;
using System.Threading.Tasks;
public class Opening : MonoBehaviour
{
    [Header("読み込み中表示オブジェクト")]
    public LoadingObject LoadingObject;

    [Header("読み込み中表示オブジェクト(明るい)")]
    public LoadingObject LoadingObject_light;

    [Header("読み込み中表示オブジェクト_アンロード")]
    public LoadingObject LoadingObject_Unload;

    [Header("ホーム")]
    public HomeMode home;

    [Header("デッキ")]
    public DeckMode deck;

    [Header("バトル")]
    public BattleMode battle;

    [Header("クリック時エフェクト")]
    public GameObject OnClickEffect;

    [Header("キャンバス")]
    public RectTransform canvasRect;

    [Header("YesNoオブジェクト")]
    [SerializeField] List<YesNoObject> YesNoObjects = new List<YesNoObject>();
    [Header("Background Particle effects")]
    [SerializeField] List<ParticleSystem> _backgroundParticles = new List<ParticleSystem>();
    [SerializeField] CardImagePanel _cardImagePanel;

    public CheckUpdate checkUpdate;

    public static Opening instance = null;

    public Text VerText;

    public OptionPanel optionPanel;
    public PatchNotes patchNotesPanel;

    public GameObject ModeButtons;

    public Vector3 DeckInfoPrefabStartScale;

    public Vector3 DeckInfoPrefabExpandScale;

    [SerializeField] Transform camerasParent;

    public Title title;

    [Header("背景Image")]
    public List<Image> BackgroundImages = new List<Image>();

    public GameObject openingObject;

    [Header("TitleButtonSE")]
    public AudioClip TitleButtonSE;

    [Header("DrawSE")]
    public AudioClip DrawSE;

    [Header("MoveSE")]
    public AudioClip MoveSE;

    [Header("DecisionSE")]
    public AudioClip DecisionSE;

    [Header("CancelSE")]
    public AudioClip CancelSE;

    [Header("BGM")]
    public AudioClip bgm;

    [Header("BGMObject")]
    public BGMObject OpeningBGM;
    bool _hasAppliedBackgroundParticleState;
    bool _lastBackgroundParticleState;
    private void Awake()
    {
        StartupPerfTrace.StartBootIfNeeded("Opening.Awake");
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("Opening.Awake");

        try
        {
            instance = this;

            if (openingCameras.Count >= 1)
            {
                MainCamera = openingCameras[0];
            }
        }
        finally
        {
            perfScope.SetItemCount("openingCameras", openingCameras.Count);
            perfScope.Dispose();
        }
    }

    int count = 0;
    int UpdateFrame = 5;
    private void Update()
    {
        #region 数フレームに一度だけ更新
        count++;

        if (count < UpdateFrame)
        {
            return;
        }

        else
        {
            count = 0;
        }
        #endregion

        GetRayCast();

        UpdateBackgroundParticles();
    }

    void UpdateBackgroundParticles()
    {
        if (ContinuousController.instance != null)
        {
            bool showBackgroundParticles = ContinuousController.instance.showBackgroundParticle;
            if (_hasAppliedBackgroundParticleState && _lastBackgroundParticleState == showBackgroundParticles)
            {
                return;
            }

            _hasAppliedBackgroundParticleState = true;
            _lastBackgroundParticleState = showBackgroundParticles;

            foreach (ParticleSystem particleSystem in _backgroundParticles)
            {
                if (particleSystem != null && particleSystem.gameObject.activeSelf != showBackgroundParticles)
                {
                    particleSystem.gameObject.SetActive(showBackgroundParticles);
                }
            }
        }
    }

    YesNoObject ActiveYesNoObject()
    {
        foreach (YesNoObject yesNoObject in YesNoObjects)
        {
            if (!yesNoObject.gameObject.activeSelf)
            {
                return yesNoObject;
            }
        }

        if (YesNoObjects.Count >= 1)
        {
            return YesNoObjects[0];
        }

        return null;
    }

    public void OffYesNoObjects()
    {
        foreach (YesNoObject yesNoObject in YesNoObjects)
        {
            yesNoObject.Close_(false);
        }
    }

    public void SetUpActiveYesNoObject(List<UnityAction> OnClickActions, List<string> CommandTexts, string _InfoText, bool CanClose)
    {
        YesNoObject activeYesNoObject = ActiveYesNoObject();

        if (activeYesNoObject != null)
        {
            activeYesNoObject.Off();
            activeYesNoObject.transform.parent.gameObject.SetActive(true);
            activeYesNoObject.transform.SetSiblingIndex(activeYesNoObject.transform.parent.childCount - 1);
            activeYesNoObject.SetUpYesNoObject(OnClickActions, CommandTexts, _InfoText, CanClose);
        }
    }

    void GetRayCast()
    {
        if (GManager.instance != null)
        {
            return;
        }

        if (ContinuousController.instance == null)
        {
            return;
        }

        if (deck != null && deck.IsIPhoneDeckBuilderActive)
        {
            return;
        }

        //bool isRay = false;

        List<RaycastResult> results = new List<RaycastResult>();
        PointerEventData pointer = new PointerEventData(EventSystem.current);
        // マウスポインタの位置にレイ飛ばし、ヒットしたものを保存
        pointer.position = Input.mousePosition;
        EventSystem.current.RaycastAll(pointer, results);

        // ヒットしたUIの名前
        //isRay = true;

        CardPrefab_CreateDeck cardPrefab = null;

        foreach (RaycastResult target in results)
        {
            if (target.gameObject.CompareTag("CardPrefab_CreateDeck_CardImage"))
            {
                cardPrefab = target.gameObject.transform.parent.parent.parent.GetComponent<CardPrefab_CreateDeck>();

                /*
                if (cardPrefab != null)
                {
                    if (Opening.instance.deck.trialDraw.gameObject.activeSelf)
                    {
                        if (cardPrefab.transform.parent != Opening.instance.deck.trialDraw.CardScroll.content)
                        {
                            cardPrefab = null;
                        }
                    }

                    else if (Opening.instance.deck.deckListPanel.gameObject.activeSelf)
                    {
                        if (cardPrefab.transform.parent != Opening.instance.deck.deckListPanel.DeckScroll.content)
                        {
                            cardPrefab = null;
                        }
                    }
                }
                */

                if (cardPrefab != null)
                {
                    break;
                }
            }
        }

        //isRay = cardPrefab != null;

        if (cardPrefab != null)
        {
            if (deck.editDeck.isDragging)
            {
                cardPrefab = null;
            }

            else
            {
                foreach (RaycastResult target in results)
                {
                    if (target.gameObject.layer == LayerMask.NameToLayer("IgnoreCardPrefabRaycast"))
                    {
                        if (DropArea.IsChild(Opening.instance.deck.trialDraw.gameObject, target.gameObject))
                        {
                            if (cardPrefab.transform.parent == Opening.instance.deck.trialDraw.CardScroll.content)
                            {
                                continue;
                            }
                        }

                        else if (DropArea.IsChild(Opening.instance.deck.deckListPanel.gameObject, target.gameObject))
                        {
                            if (cardPrefab.transform.parent == Opening.instance.deck.deckListPanel.DeckScroll.content)
                            {
                                continue;
                            }

                            if (cardPrefab.transform.parent == Opening.instance.deck.trialDraw.CardScroll.content)
                            {
                                continue;
                            }
                        }

                        cardPrefab = null;
                        break;
                    }
                }
            }
        }

        if (cardPrefab != null)
        {
            if (Opening.instance.deck.editDeck.gameObject.activeSelf && Opening.instance.deck.editDeck.isEditting && !Opening.instance.deck.editDeck.isDragging)
            {
                for (int i = 0; i < Opening.instance.deck.editDeck.CardPoolCardPrefabs_CreateDeck.Count; i++)
                {
                    if (Opening.instance.deck.editDeck.CardPoolCardPrefabs_CreateDeck[i] != cardPrefab)
                    {
                        Opening.instance.deck.editDeck.CardPoolCardPrefabs_CreateDeck[i]._OnExit();
                    }
                }

                for (int i = 0; i < Opening.instance.deck.editDeck.DeckScroll.content.childCount; i++)
                {
                    if (Opening.instance.deck.editDeck.DeckScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>() != cardPrefab)
                    {
                        Opening.instance.deck.editDeck.DeckScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>()._OnExit();
                    }
                }
            }

            if (Opening.instance.deck.deckListPanel.gameObject.activeSelf)
            {
                for (int i = 0; i < Opening.instance.deck.deckListPanel.DeckScroll.content.childCount; i++)
                {
                    if (Opening.instance.deck.deckListPanel.DeckScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>() != cardPrefab)
                    {
                        Opening.instance.deck.deckListPanel.DeckScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>()._OnExit();
                    }
                }
            }

            if (Opening.instance.deck.trialDraw.gameObject.activeSelf)
            {
                for (int i = 0; i < Opening.instance.deck.trialDraw.CardScroll.content.childCount; i++)
                {
                    if (Opening.instance.deck.trialDraw.CardScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>() != cardPrefab)
                    {
                        Opening.instance.deck.trialDraw.CardScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>()._OnExit();
                    }
                }
            }

            cardPrefab.OnEnter();
        }

        //if (!isRay)
        else
        {
            if (Opening.instance.deck.editDeck.gameObject.activeSelf && Opening.instance.deck.editDeck.isEditting)
            {
                for (int i = 0; i < Opening.instance.deck.editDeck.CardPoolCardPrefabs_CreateDeck.Count; i++)
                {
                    Opening.instance.deck.editDeck.CardPoolCardPrefabs_CreateDeck[i]._OnExit();
                }

                for (int i = 0; i < Opening.instance.deck.editDeck.DeckScroll.content.childCount; i++)
                {
                    Opening.instance.deck.editDeck.DeckScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>()._OnExit();
                }
            }

            if (Opening.instance.deck.deckListPanel.gameObject.activeSelf)
            {
                for (int i = 0; i < Opening.instance.deck.deckListPanel.DeckScroll.content.childCount; i++)
                {
                    Opening.instance.deck.deckListPanel.DeckScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>()._OnExit();
                }
            }

            if (Opening.instance.deck.trialDraw.gameObject.activeSelf)
            {
                for (int i = 0; i < Opening.instance.deck.trialDraw.CardScroll.content.childCount; i++)
                {
                    Opening.instance.deck.trialDraw.CardScroll.content.GetChild(i).GetComponent<CardPrefab_CreateDeck>()._OnExit();
                }
            }
        }
    }

    public void OpenRuleBook()
    {
        OpenURL openURL = GetComponent<OpenURL>();

        openURL.Open();
    }

    public List<Camera> openingCameras
    {
        get
        {
            List<Camera> openingCameras = new List<Camera>();

            for (int i = 0; i < camerasParent.childCount; i++)
            {
                Camera camera = camerasParent.GetChild(i).GetComponent<Camera>();

                if (camera != null)
                {
                    openingCameras.Add(camera);
                }
            }

            return openingCameras;
        }
    }

    public Camera MainCamera { get; set; }

    public void PlayDecisionSE()
    {
        ContinuousController.instance.PlaySE(Opening.instance.DecisionSE);
    }

    public void PlayCancelSE()
    {
        ContinuousController.instance.PlaySE(Opening.instance.CancelSE);
    }
    public void OffModeButtons()
    {
        ModeButtons.SetActive(false);
    }

    public void OnModeButtons()
    {
        ModeButtons.SetActive(true);
    }

    public void CreateOnClickEffect()
    {
        if (OnClickEffect == null || canvasRect == null)
        {
            return;
        }

        // The effect prefab has missing components in this mobile bootstrap; skip it to keep UI stable.
        if (Application.isMobilePlatform)
        {
            return;
        }

        GameObject effect = Instantiate(OnClickEffect, canvasRect.transform);

        var mousePos = Input.mousePosition;
        var magnification = canvasRect.sizeDelta.x / Screen.width;
        mousePos.x = mousePos.x * magnification - canvasRect.sizeDelta.x / 2;
        mousePos.y = mousePos.y * magnification - canvasRect.sizeDelta.y / 2;
        mousePos.z = 0;// -0.5f;// transform.localPosition.z;

        effect.transform.localPosition = mousePos;

        effect.transform.localRotation = Quaternion.EulerAngles(new Vector3(77, 0, 0));

        StartCoroutine(Effects.DeleteCoroutine(effect, null));
    }

    private void Start()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("Opening.Start");

        try
        {
            foreach (YesNoObject yesNoObject in YesNoObjects)
            {
                yesNoObject.Close_(false);
            }

            ChangeBackground();

            home.OffHome();

            deck.OffDeck();

            battle.OffBattle();

            _cardImagePanel.Close_(false);

            StartCoroutine(Init());
        }
        finally
        {
            perfScope.SetItemCount("yesNoObjects", YesNoObjects.Count);
            perfScope.Dispose();
        }
    }

    async void ChangeBackground()
    {
        Sprite backgroundSprite = await StreamingAssetsUtility.GetSprite("Background_home");

        if (backgroundSprite != null)
        {
            foreach (Image BackgroundImage in BackgroundImages)
            {
                BackgroundImage.sprite = backgroundSprite;
            }
        }
    }

    public IEnumerator Init()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("Opening.Init");

        try
        {
            OfflineBootstrapper.EnsureOfflineReady();

            OpeningBGM.StopPlayBGM();
            yield return StartCoroutine(LoadingObject.StartLoading("Now Loading"));

            LoadingObject_light.gameObject.SetActive(false);
            LoadingObject_Unload.gameObject.SetActive(false);

            StartupPerfTrace.Scope controllerLoadScope = StartupPerfTrace.Measure("Opening.Init.ContinuousController.LoadCoroutine");
            yield return StartCoroutine(ContinuousController.LoadCoroutine());
            controllerLoadScope.SetItemCount("runtimeCards", ContinuousController.instance?.CardList?.Length ?? 0);
            controllerLoadScope.Dispose();

            yield return new WaitWhile(() => ContinuousController.instance == null);

            StartupPerfTrace.Scope progressionScope = StartupPerfTrace.Measure("Opening.Init.ProgressionManager.LoadOrCreate");
            ProgressionManager.Instance.LoadOrCreate();
            progressionScope.SetItemCount("ownedPrints", ProgressionManager.Instance.CurrentProfileData?.OwnedPrintIds?.Count ?? 0);
            progressionScope.SetItemCount("unlockedCardIds", ProgressionManager.Instance.CurrentProfileData?.UnlockedCardIds?.Count ?? 0);
            progressionScope.Dispose();

            // ContinuousController.instance.LoadVolume();

            home.OffHome();

            optionPanel.Init();

            patchNotesPanel.Init();

            if (StartupPerfTrace.Enabled)
            {
                Debug.Log($"[StartupPerf] DEFER label={StartupPerfTrace.Label} step=Opening.InitEditDeckStartup");
            }

            yield return new WaitForSeconds(0.1f);

            VerText.text = $"Ver{ContinuousController.instance.GameVerString}";

            MainMenuRouter router = GetComponent<MainMenuRouter>();
            if (router != null)
            {
                router.HideAllCustomModes();
            }

            if (title != null)
            {
                title.OffTitle();
            }

            home.SetUpHome();

            if (StartupPerfTrace.SkipLoadCardImagesOnStartup)
            {
                Debug.Log($"[StartupPerf] SKIP label={StartupPerfTrace.Label} step=Opening.LoadCardImages");
            }
            else
            {
                LoadCardImages();
            }

            yield return new WaitForSeconds(0.15f);

            yield return StartCoroutine(LoadingObject.EndLoading());
        }
        finally
        {
            perfScope.SetItemCount("runtimeCards", ContinuousController.instance?.CardList?.Length ?? 0);
            perfScope.SetItemCount("savedDecks", ContinuousController.instance?.DeckDatas?.Count ?? 0);
            perfScope.SetItemCount("ownedPrints", ProgressionManager.LoadedInstance?.CurrentProfileData?.OwnedPrintIds?.Count ?? 0);
            perfScope.Dispose();
            StartupPerfTrace.MarkBootComplete("Opening.Init");
        }
    }

    async void LoadCardImages()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("Opening.LoadCardImages");
        int keyCardCount = 0;

        try
        {
#if UNITY_EDITOR
            foreach (CEntity_Base cEntity_Base in ContinuousController.instance.CardList)
            {
                cEntity_Base.HasLoadStarted = false;
            }
#endif

            foreach (DeckData deckData in ContinuousController.instance.DeckDatas)
            {
                CEntity_Base keyCard = deckData.KeyCard;

                if (keyCard != null)
                {
                    keyCardCount++;
                    keyCard.HasLoadStarted = false;
                    await keyCard.LoadCardImage();
                }
            }
        }
        finally
        {
            perfScope.SetItemCount("savedDecks", ContinuousController.instance?.DeckDatas?.Count ?? 0);
            perfScope.SetItemCount("keyCards", keyCardCount);
            perfScope.Dispose();
        }
    }
}
