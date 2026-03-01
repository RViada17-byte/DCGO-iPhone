using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class ContinuousController : MonoBehaviour
{
    [Header("game language")]
    // public Language language;

    [Header("Game version")]
    public float GameVer;

    [Header("ignore updates")]
    public bool IgnoreUpdate;

    [Header("card list")]
    public CEntity_Base[] CardList = new CEntity_Base[] { };

    [Header("Card list sorted by card ID")]
    public CEntity_Base[] SortedCardList = new CEntity_Base[] { };

    [Header("Card back image")]
    public Sprite ReverseCard;
    public Sprite ReverseCard_Digitama;

    [Header("SE prefab")]
    public SoundObject soundObject;

    [Header("deck code encryption")]
    public ShuffleDeckCode ShuffleDeckCode;
    DeckData _battleDeckData = null;

    public DeckData BattleDeckData
    {
        get
        {
            return _battleDeckData;
        }

        set
        {
            _battleDeckData = value;

            if (value != null)
            {
                LastBattleDeckData = value;
            }
        }
    }
    public DeckData LastBattleDeckData { get; private set; } = null;

    public bool NeedUpdate { get; set; }

    public bool isRandomMatch { get; set; }
    [HideInInspector] public List<SkillInfo> nullSkillInfos = null;
    public String GameVerString => Application.version;//GameVer.ToString(CultureInfo.InvariantCulture);
    #region Key for property to save deck data for battle
    public static string DeckDataPropertyKey => "BattleDeckData";
    #endregion

    #region Key for the property that stores the player name data
    public static string PlayerNameKey => "PlayerNameKey";
    #endregion

    #region Key for the property that stores the win count data
    public static string WinCountKey => "WinCountKey";
    #endregion

    [Header("Player name character limit")]
    public int PlayerNameMaxLength;

    #region Call up a scene for data storage
    public static IEnumerator LoadCoroutine()
    {
        if (instance == null)
        {
            SceneManager.LoadSceneAsync("ContinuousControllerScene", LoadSceneMode.Additive);

            while (instance == null)
            {
                yield return null;
            }

            instance.Init();
        }
    }
    #endregion

    #region List of Deck Recipes
    public List<DeckData> DeckDatas { get; set; } = new List<DeckData>();
    #endregion

    #region Deck Recipe Key
    public string DeckDatasPlayerPrefsKey { get { return "DeckDatas3"; } }
    #endregion

    public CEntity_Base DiaboromonToken { get; private set; }
    public CEntity_Base AmonToken { get; private set; }
    public CEntity_Base UmonToken { get; private set; }
    public CEntity_Base FujitsumonToken { get; private set; }
    public CEntity_Base GyuukimonToken { get; private set; }
    public CEntity_Base KoHagurumonToken { get; private set; }
    public CEntity_Base FamiliarToken { get; private set; }
    public CEntity_Base SelfDeleteFamiliarToken { get; private set; }
    public CEntity_Base VoleeZerdruckenToken { get; private set; }
    public CEntity_Base UkaNoMitamaToken { get; private set; }
    public CEntity_Base WarGrowlmonToken { get; private set; }
    public CEntity_Base TaomonToken { get; private set; }
    public CEntity_Base RapidmonToken { get; private set; }
    public CEntity_Base PipeFoxToken { get; private set; }
    public CEntity_Base AthoRenePorToken { get; private set; }
    public CEntity_Base HinukamuyToken { get; private set; }
    public CEntity_Base PetrificationToken { get; private set; }
    public CardRestriction BanList { get; private set; } = new CardRestriction(new List<CardLimitCount>(), new List<BannedPair>());
    readonly Dictionary<string, CEntity_Base> _offlineRuntimeCards = new Dictionary<string, CEntity_Base>(StringComparer.OrdinalIgnoreCase);
    int _nextOfflineRuntimeCardIndex = -1;

    static readonly OfflineDeckCardSpec[] OfflineSt1EggDeckSpec = new[]
    {
        new OfflineDeckCardSpec("ST1-01", 4),
    };

    static readonly OfflineDeckCardSpec[] OfflineSt1MainDeckSpec = new[]
    {
        new OfflineDeckCardSpec("ST1-02", 4),
        new OfflineDeckCardSpec("ST1-03", 4),
        new OfflineDeckCardSpec("ST1-04", 4),
        new OfflineDeckCardSpec("ST1-05", 3),
        new OfflineDeckCardSpec("ST1-06", 3),
        new OfflineDeckCardSpec("ST1-07", 4),
        new OfflineDeckCardSpec("ST1-08", 4),
        new OfflineDeckCardSpec("ST1-09", 4),
        new OfflineDeckCardSpec("ST1-10", 2),
        new OfflineDeckCardSpec("ST1-11", 4),
        new OfflineDeckCardSpec("ST1-12", 4),
        new OfflineDeckCardSpec("ST1-13", 3),
        new OfflineDeckCardSpec("ST1-14", 3),
        new OfflineDeckCardSpec("ST1-15", 3),
        new OfflineDeckCardSpec("ST1-16", 1),
    };

    static readonly OfflineDeckCardSpec[] OfflineSt2EggDeckSpec = new[]
    {
        new OfflineDeckCardSpec("ST2-01", 4),
    };

    static readonly OfflineDeckCardSpec[] OfflineSt2MainDeckSpec = new[]
    {
        new OfflineDeckCardSpec("ST2-02", 4),
        new OfflineDeckCardSpec("ST2-03", 4),
        new OfflineDeckCardSpec("ST2-04", 4),
        new OfflineDeckCardSpec("ST2-05", 4),
        new OfflineDeckCardSpec("ST2-06", 4),
        // User-provided list had 51 mains (ST2-07 x3). Keep legal 50-card main deck.
        new OfflineDeckCardSpec("ST2-07", 2),
        new OfflineDeckCardSpec("ST2-08", 4),
        new OfflineDeckCardSpec("ST2-09", 4),
        new OfflineDeckCardSpec("ST2-10", 2),
        new OfflineDeckCardSpec("ST2-11", 4),
        new OfflineDeckCardSpec("ST2-12", 4),
        new OfflineDeckCardSpec("ST2-13", 4),
        new OfflineDeckCardSpec("ST2-14", 4),
        new OfflineDeckCardSpec("ST2-15", 1),
        new OfflineDeckCardSpec("ST2-16", 1),
    };

    struct OfflineDeckCardSpec
    {
        public string CardId;
        public int Count;

        public OfflineDeckCardSpec(string cardId, int count)
        {
            CardId = cardId;
            Count = count;
        }
    }

    sealed class OfflineCardDefinition
    {
        public string CardId;
        public string CardNameEng;
        public CardKind CardKind;
        public CardColor CardColor;
        public int PlayCost;
        public int Level;
        public int DP;
        public string EffectText;
        public string InheritedEffectText;
        public string SecurityEffectText;
        public string EffectClassName;
        public List<EvoCost> EvoCosts;
        public Rarity Rarity;
    }

    void LoadBanList()
    {
        BanList = DataBase.ENGBanList;
    }

    void CreateTokenData()
    {
        DiaboromonToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.White },
            PlayCost = 14,
            Level = 6,
            CardName_JPN = "ディアボロモン",
            CardName_ENG = "Diaboromon",
            Form_JPN = new List<string>() { "究極体" },
            Form_ENG = new List<string>() { "Mega" },
            Attribute_JPN = new List<string>() { "不明" },
            Attribute_ENG = new List<string>() { "Unknown" },
            Type_JPN = new List<string>() { "種族不明" },
            Type_ENG = new List<string>() { "Unidentified" },
            CardSpriteName = "BT2-082-token",
            cardKind = CardKind.Digimon,
            DP = 3000,
        };
        AmonToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Red },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "紅炎のアモン",
            CardName_ENG = "Amon of Crimson Flame",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "BT14-018-token-red",
            cardKind = CardKind.Digimon,
            DP = 6000,
            CardEffectClassName = "BT4_038"
        };
        UmonToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Yellow },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "蒼雷のウモン",
            CardName_ENG = "Umon of Blue Thunder",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "BT14-018-token-yellow",
            cardKind = CardKind.Digimon,
            DP = 6000,
            CardEffectClassName = "BT1_031"
        };
        FujitsumonToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Purple },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "フジツモン",
            CardName_ENG = "Fujitsumon",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "EX5-058-token",
            cardKind = CardKind.Digimon,
            DP = 3000,
            CardEffectClassName = "EX5_058_token"
        };
        GyuukimonToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Purple },
            PlayCost = 7,
            Level = 5,
            CardName_JPN = "ギュウキモン",
            CardName_ENG = "Gyuukimon",
            Form_JPN = new List<string>() { "究極の" },
            Form_ENG = new List<string>() { "Ultimate" },
            Attribute_JPN = new List<string>() { "ウイルス" },
            Attribute_ENG = new List<string>() { "Virus" },
            Type_JPN = new List<string>() { "ダークアニマル" },
            Type_ENG = new List<string>() { "Dark Animal" },
            CardSpriteName = "LM-018-token",
            cardKind = CardKind.Digimon,
            DP = 3000,
        };
        KoHagurumonToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Black },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "",
            CardName_ENG = "KoHagurumon",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "BT16-052-token",
            cardKind = CardKind.Digimon,
            DP = 1000,
            CardEffectClassName = "BT16_052_token"
        };
        FamiliarToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Yellow },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "",
            CardName_ENG = "Familiar",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "EX7-030-token",
            cardKind = CardKind.Digimon,
            DP = 3000,
            CardEffectClassName = "EX7_030_token"
        };
        SelfDeleteFamiliarToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Yellow },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "",
            CardName_ENG = "Familiar",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "EX7-030-token",
            cardKind = CardKind.Digimon,
            DP = 3000,
            CardEffectClassName = "P_165_token"
        };
        VoleeZerdruckenToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Purple },
            PlayCost = -1,
            Level = 4,
            CardName_JPN = "",
            CardName_ENG = "Volée & Zerdrücken",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "EX7-058-token",
            cardKind = CardKind.Digimon,
            DP = 5000,
            CardEffectClassName = "EX7_058_token"
        };
        UkaNoMitamaToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Yellow },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "",
            CardName_ENG = "Uka-no-Mitama",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "EX8-037-token",
            cardKind = CardKind.Digimon,
            DP = 9000,
            CardEffectClassName = "EX8_037_token"
        };
        WarGrowlmonToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Red },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "",
            CardName_ENG = "WarGrowlmon",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "BT19-091-token",
            cardKind = CardKind.Digimon,
            DP = 6000
        };
        TaomonToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Yellow },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "",
            CardName_ENG = "Taomon",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "BT19-091-token",
            cardKind = CardKind.Digimon,
            DP = 6000
        };
        RapidmonToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Green },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "",
            CardName_ENG = "Rapidmon",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "BT19-091-token",
            cardKind = CardKind.Digimon,
            DP = 6000
        };
        PipeFoxToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.Yellow },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "",
            CardName_ENG = "Pipe-Fox",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "BT19-040-token",
            cardKind = CardKind.Digimon,
            DP = 6000,
            CardEffectClassName = "BT19_040_token"
        };
        AthoRenePorToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.White },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "",
            CardName_ENG = "Atho, René & Por",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "BT20-017-token",
            cardKind = CardKind.Digimon,
            DP = 6000,
            CardEffectClassName = "BT20_017_token"
        };
        HinukamuyToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.White },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "",
            CardName_ENG = "HinukamuyToken",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "BT23-057-token",
            cardKind = CardKind.Digimon,
            DP = 6000,
            CardEffectClassName = "BT23_057_token"
        };
        PetrificationToken = new CEntity_Base()
        {
            cardColors = new List<CardColor>() { CardColor.White },
            PlayCost = -1,
            Level = 0,
            CardName_JPN = "",
            CardName_ENG = "Petrification",
            Form_JPN = new List<string>(),
            Form_ENG = new List<string>(),
            Attribute_JPN = new List<string>(),
            Attribute_ENG = new List<string>(),
            Type_JPN = new List<string>(),
            Type_ENG = new List<string>(),
            CardSpriteName = "BT21-029-token",
            cardKind = CardKind.Digimon,
            DP = 3000,
            CardEffectClassName = "BT21_029_token"
        };
    }

    public static ContinuousController instance = null;

    private void Awake()
    {
        instance = this;
    }

    public async void Init()
    {
        Application.targetFrameRate = 60;
        int random = RandomUtility.getRamdom();
        UnityEngine.Random.InitState(random);
        Debug.Log($"Game Initialize - random number sequence initialization,InitState:{random}");

        if (SortedCardList == null || SortedCardList.Length == 0)
        {
            SortedCardList = CardList
                .Where(card => card != null)
                .OrderBy(card => card.CardIndex)
                .ToArray();
        }

        Sprite reverseCardSprite = await StreamingAssetsUtility.GetSprite("card_back_main");

        if (reverseCardSprite != null)
        {
            ReverseCard = reverseCardSprite;
        }

        Sprite reverseDigieggCardSprite = await StreamingAssetsUtility.GetSprite("card_back_sub");

        if (reverseDigieggCardSprite != null)
        {
            ReverseCard_Digitama = reverseDigieggCardSprite;
        }

        if (ReverseCard == null)
        {
            ReverseCard = Resources.Load<Sprite>("Placeholders/EmptyCard");
        }

        if (ReverseCard_Digitama == null)
        {
            ReverseCard_Digitama = ReverseCard;
        }

        LoadBanList();

        // deck data
        //DeckDatas = PlayerPrefsUtil.LoadList<DeckData>(DeckDatasPlayerPrefsKey);
        LoadDeckLists();
        GetComponent<StarterDeck>().SetStarterDecks();
        EnsureOfflineDemoDecks();

        // player data
        LoadPlayerName();
        LoadWinCount();

        // game play
        LoadAutoEffectOrder();
        LoadAutoDeckBottomOrder();
        LoadAutoDeckTopOrder();
        LoadAutoMinDigivolutionCost();
        LoadAutoMaxCardCount();
        LoadAutoHatch();
        LoadShowCutInAnimation();
        LoadReverseOpponentsCards();
        LoadTurnSuspendedCards();
        LoadCheckBeforeEndingSelection();
        LoadSuspendedCardsDirectionIsLeft();

        //Graphics
        LoadShowBackgroundParticle();

        // Sound
        LoadVolume();

        // ServerRegion
        LoadServerRegion();

        // Language
        LoadLanguage();

        // Skip eager token bootstrap for offline iPhone mode to keep first load fast.
        if (!BootstrapConfig.IsOfflineLocal)
        {
            CreateTokenData();
        }

        DontDestroyOnLoad(gameObject);
    }

    [Obsolete("This is obsolete, switching to save files")]
    public void ModifyAllDeckDatas()
    {
        List<DeckData> tempDeckDatas = new List<DeckData>();

        foreach (DeckData deckData in DeckDatas)
        {
            tempDeckDatas.Add(deckData);
        }

        foreach (DeckData deckData in tempDeckDatas)
        {
            if (deckData.AllDeckCards().Count == 0)
            {
                DeckDatas.Remove(deckData);
            }
        }

        for (int i = 0; i < DeckDatas.Count; i++)
        {
            //DeckData deckData = new DeckData(DeckData.GetDeckCode(DeckDatas[i].DeckName, DeckData.SortedDeckCardsList(DeckDatas[i].DeckCards()), DeckData.SortedDeckCardsList(DeckDatas[i].DigitamaDeckCards()), DeckDatas[i].KeyCard));

            DeckData deckData = DeckDatas[i];

            DeckDatas[i] = deckData.ModifiedDeckData();
        }

        SaveDeckDatas();
    }

    [Obsolete("This is obsolete, switching to save files")]
    public void SaveDeckDatas()
    {
        PlayerPrefsUtil.SaveList(DeckDatasPlayerPrefsKey, DeckDatas);

        PlayerPrefs.Save();
    }

    public void SaveDeckData(DeckData data)
    {
        if (data == null)
        {
            return;
        }

        try
        {
            string savePath = GetDeckStoragePath();
            string deckPath = GetDeckFilePath(data.DeckName, data.DeckID, savePath);
            File.WriteAllText(deckPath, DeckCodeUtility.GetDeckBuilderFile(data));
        }

        catch (Exception exception)
        {
            Debug.LogWarning($"[ContinuousController] Failed to save deck '{data.DeckName}': {exception.Message}");
        }
    }

    public void RenameDeck(DeckData data, string newName)
    {
        if (data == null)
        {
            return;
        }

        string validatedName = DeckData.ValidateDeckName(newName);

        try
        {
            string savePath = GetDeckStoragePath();
            string oldPath = GetDeckFilePath(data.DeckName, data.DeckID, savePath);
            string newPath = GetDeckFilePath(validatedName, data.DeckID, savePath);

            if (File.Exists(oldPath) && !string.Equals(oldPath, newPath, StringComparison.Ordinal))
            {
                File.Move(oldPath, newPath);
            }

            data.DeckName = validatedName;
            SaveDeckData(data);
        }

        catch (Exception exception)
        {
            Debug.LogWarning($"[ContinuousController] Failed to rename deck '{data.DeckName}' to '{validatedName}': {exception.Message}");
            data.DeckName = validatedName;
        }
    }

    public void DeleteDeck(DeckData data)
    {
        if (data == null)
            return;

        try
        {
            string filePath = GetDeckStoragePath();

            if (!Directory.Exists(filePath))
                return;

            string deckPath = GetDeckFilePath(data.DeckName, data.DeckID, filePath);
            if (!File.Exists(deckPath))
                return;

            File.Delete(deckPath);
        }

        catch (Exception exception)
        {
            Debug.LogWarning($"[ContinuousController] Failed to delete deck '{data.DeckName}': {exception.Message}");
        }
    }

    public void DeleteAllDecks()
    {
        foreach(DeckData data in DeckDatas)
        {
            DeleteDeck(data);
        }
    }

    public void LoadDeckLists()
    {
        string loadPath = GetDeckStoragePath();

        if (!Directory.Exists(loadPath))
            return;

        string[] deckLists = Directory.GetFiles(loadPath);

        foreach(string deckPath in deckLists)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(deckPath);

                if (!fileName.Contains("_"))
                    continue;

                string deckList = File.ReadAllText(deckPath);

                using StreamReader sr = new StreamReader(deckPath);

                string deckNameLine = sr.ReadLine();
                string keyCardLine = sr.ReadLine();
                string sortValueLine = sr.ReadLine();

                if (string.IsNullOrEmpty(deckNameLine) || string.IsNullOrEmpty(keyCardLine) || string.IsNullOrEmpty(sortValueLine))
                {
                    Debug.LogWarning($"[ContinuousController] Skipping malformed deck file: {deckPath}");
                    continue;
                }

                string deckName = deckNameLine.Replace("Name: ", "");

                if (!int.TryParse(keyCardLine.Replace("Key Card: ", ""), out int KeyCard))
                {
                    KeyCard = -1;
                }

                if (!int.TryParse(sortValueLine.Replace("Sort Index: ", ""), out int SortValue))
                {
                    SortValue = 0;
                }

                if (!deckList.Contains("//"))
                {
                    Debug.LogWarning($"[ContinuousController] Skipping deck file without deck code body: {deckPath}");
                    continue;
                }

                string deck = deckList.Substring(deckList.IndexOf("//", StringComparison.Ordinal));

                if (SortValue < 0)
                    SortValue = 0;

                CreateDeckFromFile(fileName.Split("_")[1], deckName, KeyCard, deck, SortValue);
            }

            catch (Exception exception)
            {
                Debug.LogWarning($"[ContinuousController] Failed to load deck file '{deckPath}': {exception.Message}");
            }
        }

        DeckDatas = DeckDatas.OrderBy(x => x.DeckName).ToList();
    }

    public DeckData FindDeckDataBySelector(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return null;
        }

        string trimmed = selector.Trim();

        DeckData byId = DeckDatas.FirstOrDefault(deckData =>
            deckData != null &&
            !string.IsNullOrEmpty(deckData.DeckID) &&
            string.Equals(deckData.DeckID, trimmed, StringComparison.OrdinalIgnoreCase) &&
            deckData.IsValidDeckData());
        if (byId != null)
        {
            return byId;
        }

        DeckData byName = DeckDatas.FirstOrDefault(deckData =>
            deckData != null &&
            !string.IsNullOrEmpty(deckData.DeckName) &&
            string.Equals(deckData.DeckName, trimmed, StringComparison.OrdinalIgnoreCase) &&
            deckData.IsValidDeckData());
        if (byName != null)
        {
            return byName;
        }

        DeckData partialName = DeckDatas.FirstOrDefault(deckData =>
            deckData != null &&
            !string.IsNullOrEmpty(deckData.DeckName) &&
            deckData.DeckName.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0 &&
            deckData.IsValidDeckData());

        return partialName;
    }

    public DeckData FirstValidDeckData()
    {
        return DeckDatas.FirstOrDefault(deckData => deckData != null && deckData.IsValidDeckData());
    }

    string GetDeckStoragePath()
    {
        return StreamingAssetsUtility.GetWritablePersistentPath("Decks");
    }

    static string GetDeckFilePath(string deckName, string deckId, string basePath)
    {
        string safeName = DeckData.ValidateDeckName(deckName);
        return Path.Combine(basePath, $"{safeName}_{deckId}.txt");
    }

    void EnsureOfflineDemoDecks()
    {
        bool IsUsableDeck(DeckData deckData)
        {
            if (deckData == null)
            {
                return false;
            }

            try
            {
                return deckData.IsValidDeckData();
            }

            catch (Exception exception)
            {
                Debug.LogWarning($"[ContinuousController] Ignoring invalid deck data during offline bootstrap: {exception.Message}");
                return false;
            }
        }

        if (BootstrapConfig.IsOfflineLocal)
        {
            if (DeckDatas == null)
            {
                DeckDatas = new List<DeckData>();
            }

            DeckDatas.RemoveAll(deckData =>
                deckData != null &&
                (
                    string.Equals(deckData.DeckName, "ST1 Demo", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(deckData.DeckName, "ST2 Demo", StringComparison.OrdinalIgnoreCase)
                ));

            DeckData st2OfflineDeck = GenerateManualDeck("ST2 Demo", OfflineSt2MainDeckSpec, OfflineSt2EggDeckSpec);
            DeckData st1OfflineDeck = GenerateManualDeck("ST1 Demo", OfflineSt1MainDeckSpec, OfflineSt1EggDeckSpec);

            if (st2OfflineDeck != null)
            {
                DeckDatas.Insert(0, st2OfflineDeck);
            }

            if (st1OfflineDeck != null)
            {
                DeckDatas.Insert(0, st1OfflineDeck);
            }

            foreach (DeckData deckData in DeckDatas.Where(deckData =>
                deckData != null &&
                (string.Equals(deckData.DeckName, "ST1 Demo", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(deckData.DeckName, "ST2 Demo", StringComparison.OrdinalIgnoreCase))))
            {
                int mainCount = deckData.DeckCards().Count;
                int eggCount = deckData.DigitamaDeckCards().Count;
                bool isValid = deckData.IsValidDeckData();
                Debug.Log($"[ContinuousController] Offline demo deck status -> Name:{deckData.DeckName} ID:{deckData.DeckID} Main:{mainCount} Eggs:{eggCount} Valid:{isValid}");
            }

            if (!DeckDatas.Any(IsUsableDeck))
            {
                DeckData fallbackDeck = GenerateDemoDeckFromSets("Offline Demo", Array.Empty<string>());
                if (fallbackDeck != null)
                {
                    DeckDatas.Add(fallbackDeck);
                }
            }

            return;
        }

        if (DeckDatas.Any(IsUsableDeck))
        {
            return;
        }

        Debug.LogWarning("[ContinuousController] No valid decks found. Generating ST1/ST2 demo decks.");
        DeckDatas = new List<DeckData>();

        DeckData st1Deck = GenerateManualDeck("ST1 Demo", OfflineSt1MainDeckSpec, OfflineSt1EggDeckSpec);
        DeckData st2Deck = GenerateManualDeck("ST2 Demo", OfflineSt2MainDeckSpec, OfflineSt2EggDeckSpec);

        if (st1Deck != null)
        {
            DeckDatas.Add(st1Deck);
        }

        if (st2Deck != null)
        {
            DeckDatas.Add(st2Deck);
        }

        if (!DeckDatas.Any(IsUsableDeck))
        {
            DeckData fallbackDeck = GenerateDemoDeckFromSets("Offline Demo", Array.Empty<string>());
            if (fallbackDeck != null)
            {
                DeckDatas.Add(fallbackDeck);
            }
        }
    }

    DeckData GenerateDemoDeckFromSets(string deckName, IReadOnlyCollection<string> setIds)
    {
        bool MatchesSet(CEntity_Base card)
        {
            return setIds.Count == 0 || setIds.Contains(card.SetID);
        }

        bool IsPlayableCard(CEntity_Base card)
        {
            if (card == null)
            {
                return false;
            }

            try
            {
                return card.IsStandardValid;
            }

            catch
            {
                return false;
            }
        }

        List<CEntity_Base> validCards = CardList
            .Where(IsPlayableCard)
            .Where(card => card.CardIndex > 0)
            .ToList();

        List<CEntity_Base> scopedCards = validCards
            .Where(MatchesSet)
            .ToList();

        List<CEntity_Base> mainDeck = new List<CEntity_Base>();
        List<CEntity_Base> digiEggDeck = new List<CEntity_Base>();

        List<CEntity_Base> scopedMain = UniqueByCardId(scopedCards.Where(card => card.cardKind != CardKind.DigiEgg && StreamingAssetsUtility.IsCardExists(card)));
        List<CEntity_Base> scopedEggs = UniqueByCardId(scopedCards.Where(card => card.cardKind == CardKind.DigiEgg && StreamingAssetsUtility.IsCardExists(card)));

        FillDeckWithCardLimit(mainDeck, scopedMain, 50);
        FillDeckWithCardLimit(digiEggDeck, scopedEggs, 5);

        if (mainDeck.Count < 50)
        {
            Debug.LogWarning($"[ContinuousController] Could not generate a valid demo main deck for {deckName}.");
            return null;
        }

        // Build a direct deterministic deck first to avoid failing on partially loaded metadata.
        DeckData directDeck = new DeckData("");
        directDeck.DeckName = deckName;
        directDeck.DeckCardIDs = mainDeck.Take(50).Select(card => card.CardIndex).ToList();
        directDeck.DigitamaDeckCardIDs = digiEggDeck.Take(5).Select(card => card.CardIndex).ToList();
        directDeck.KeyCardId = directDeck.DeckCardIDs.Count > 0 ? directDeck.DeckCardIDs[0] : -1;

        if (directDeck.DeckCardIDs.Count == 50)
        {
            return directDeck;
        }

        Debug.LogWarning($"[ContinuousController] Demo deck generation did not produce enough indexed cards for {deckName}.");
        return null;

        static List<CEntity_Base> UniqueByCardId(IEnumerable<CEntity_Base> cards)
        {
            return cards
                .GroupBy(card => card.CardID)
                .Select(group => group.First())
                .OrderBy(card => card.CardIndex)
                .ToList();
        }

        static void FillDeckWithCardLimit(List<CEntity_Base> target, List<CEntity_Base> pool, int targetCount)
        {
            if (pool.Count == 0 || target.Count >= targetCount)
            {
                return;
            }

            Dictionary<string, int> counts = new Dictionary<string, int>();

            foreach (CEntity_Base card in target)
            {
                if (!counts.ContainsKey(card.CardID))
                {
                    counts[card.CardID] = 0;
                }

                counts[card.CardID]++;
            }

            bool addedAny;

            do
            {
                addedAny = false;

                foreach (CEntity_Base card in pool)
                {
                    if (target.Count >= targetCount)
                    {
                        return;
                    }

                    if (!counts.TryGetValue(card.CardID, out int count))
                    {
                        count = 0;
                    }

                    int maxCount = Math.Max(1, card.MaxCountInDeck);

                    if (count >= maxCount)
                    {
                        continue;
                    }

                    target.Add(card);
                    counts[card.CardID] = count + 1;
                    addedAny = true;
                }
            }

            while (addedAny && target.Count < targetCount);
        }
    }

    DeckData GenerateManualDeck(string deckName, IReadOnlyCollection<OfflineDeckCardSpec> mainDeckSpec, IReadOnlyCollection<OfflineDeckCardSpec> eggDeckSpec)
    {
        if (mainDeckSpec == null || eggDeckSpec == null)
        {
            return null;
        }

        List<int> mainDeckCardIndexes = new List<int>();
        List<int> eggDeckCardIndexes = new List<int>();

        foreach (OfflineDeckCardSpec spec in eggDeckSpec)
        {
            if (!TryResolveOfflineDeckCard(spec.CardId, out CEntity_Base card))
            {
                Debug.LogWarning($"[ContinuousController] Missing card definition for egg '{spec.CardId}' while building {deckName}.");
                return null;
            }

            for (int i = 0; i < spec.Count; i++)
            {
                eggDeckCardIndexes.Add(card.CardIndex);
            }
        }

        foreach (OfflineDeckCardSpec spec in mainDeckSpec)
        {
            if (!TryResolveOfflineDeckCard(spec.CardId, out CEntity_Base card))
            {
                Debug.LogWarning($"[ContinuousController] Missing card definition for main deck card '{spec.CardId}' while building {deckName}.");
                return null;
            }

            for (int i = 0; i < spec.Count; i++)
            {
                mainDeckCardIndexes.Add(card.CardIndex);
            }
        }

        if (mainDeckCardIndexes.Count != 50)
        {
            Debug.LogWarning($"[ContinuousController] Manual deck '{deckName}' has {mainDeckCardIndexes.Count} main cards; expected 50.");
            return null;
        }

        if (eggDeckCardIndexes.Count > 5)
        {
            Debug.LogWarning($"[ContinuousController] Manual deck '{deckName}' has {eggDeckCardIndexes.Count} digi-eggs; expected 0-5.");
            return null;
        }

        DeckData manualDeck = new DeckData("");
        manualDeck.DeckName = deckName;
        manualDeck.DeckID = $"offline-{NormalizeDeckId(deckName)}";
        manualDeck.DeckCardIDs = mainDeckCardIndexes;
        manualDeck.DigitamaDeckCardIDs = eggDeckCardIndexes;
        manualDeck.KeyCardId = mainDeckCardIndexes.Count > 0 ? mainDeckCardIndexes[0] : -1;

        string mainBreakdown = string.Join(", ", mainDeckSpec.Select(spec => $"{spec.CardId}x{spec.Count}"));
        string eggBreakdown = string.Join(", ", eggDeckSpec.Select(spec => $"{spec.CardId}x{spec.Count}"));
        Debug.Log($"[ContinuousController] Built manual deck '{deckName}' -> Main({mainDeckCardIndexes.Count}): {mainBreakdown} | Eggs({eggDeckCardIndexes.Count}): {eggBreakdown}");

        return manualDeck;
    }

    bool TryResolveOfflineDeckCard(string cardId, out CEntity_Base card)
    {
        card = FindCardByIdOrSpriteName(cardId);
        if (card != null)
        {
            return true;
        }

        OfflineCardDefinition definition = GetOfflineStarterCardDefinition(cardId);
        if (definition == null)
        {
            return false;
        }

        card = EnsureOfflineRuntimeCard(definition);
        return card != null;
    }

    CEntity_Base FindCardByIdOrSpriteName(string cardId)
    {
        string normalizedCardId = NormalizeCardCode(cardId);
        return CardList.FirstOrDefault(entity =>
            entity != null &&
            (
                NormalizeCardCode(entity.CardID) == normalizedCardId ||
                NormalizeCardCode(entity.CardSpriteName) == normalizedCardId
            ));
    }

    CEntity_Base EnsureOfflineRuntimeCard(OfflineCardDefinition definition)
    {
        if (definition == null)
        {
            return null;
        }

        if (_offlineRuntimeCards.TryGetValue(definition.CardId, out CEntity_Base cachedCard) && cachedCard != null)
        {
            return cachedCard;
        }

        CEntity_Base existingCard = FindCardByIdOrSpriteName(definition.CardId);
        if (existingCard != null)
        {
            _offlineRuntimeCards[definition.CardId] = existingCard;
            return existingCard;
        }

        if (_nextOfflineRuntimeCardIndex < 0)
        {
            _nextOfflineRuntimeCardIndex = (CardList ?? Array.Empty<CEntity_Base>())
                .Where(entity => entity != null)
                .Select(entity => entity.CardIndex)
                .DefaultIfEmpty(0)
                .Max() + 1;
        }

        CEntity_Base runtimeCard = ScriptableObject.CreateInstance<CEntity_Base>();
        runtimeCard.CardIndex = _nextOfflineRuntimeCardIndex++;
        runtimeCard.cardColors = new List<CardColor>() { definition.CardColor };
        runtimeCard.PlayCost = definition.PlayCost;
        runtimeCard.EvoCosts = definition.EvoCosts ?? new List<EvoCost>();
        runtimeCard.Level = definition.Level;
        runtimeCard.CardName_JPN = definition.CardNameEng;
        runtimeCard.CardName_ENG = definition.CardNameEng;
        runtimeCard.Form_JPN = new List<string>();
        runtimeCard.Form_ENG = new List<string>();
        runtimeCard.Attribute_JPN = new List<string>();
        runtimeCard.Attribute_ENG = new List<string>();
        runtimeCard.Type_JPN = new List<string>();
        runtimeCard.Type_ENG = new List<string>();
        runtimeCard.CardSpriteName = definition.CardId;
        runtimeCard.cardKind = definition.CardKind;
        runtimeCard.EffectDiscription_JPN = definition.EffectText ?? "";
        runtimeCard.EffectDiscription_ENG = definition.EffectText ?? "";
        runtimeCard.InheritedEffectDiscription_JPN = definition.InheritedEffectText ?? "";
        runtimeCard.InheritedEffectDiscription_ENG = definition.InheritedEffectText ?? "";
        runtimeCard.SecurityEffectDiscription_JPN = definition.SecurityEffectText ?? "";
        runtimeCard.SecurityEffectDiscription_ENG = definition.SecurityEffectText ?? "";
        runtimeCard.CardEffectClassName = definition.EffectClassName ?? "";
        runtimeCard.DP = definition.DP;
        runtimeCard.rarity = definition.Rarity;
        runtimeCard.OverflowMemory = 0;
        runtimeCard.LinkDP = 0;
        runtimeCard.LinkEffect = "";
        runtimeCard.LinkRequirement = "";
        runtimeCard.CardID = definition.CardId;
        runtimeCard.MaxCountInDeck = 4;

        List<CEntity_Base> cardList = (CardList ?? Array.Empty<CEntity_Base>())
            .Where(entity => entity != null)
            .ToList();
        cardList.Add(runtimeCard);
        CardList = cardList.ToArray();
        SortedCardList = cardList.OrderBy(entity => entity.CardIndex).ToArray();

        _offlineRuntimeCards[definition.CardId] = runtimeCard;
        return runtimeCard;
    }

    OfflineCardDefinition GetOfflineStarterCardDefinition(string cardId)
    {
        string normalizedCardId = NormalizeCardCode(cardId);

        switch (normalizedCardId)
        {
            case "ST1-01":
                return NewOfflineCardDefinition(
                    cardId: "ST1-01",
                    name: "Koromon",
                    kind: CardKind.DigiEgg,
                    color: CardColor.Red,
                    playCost: -1,
                    level: 2,
                    dp: 0,
                    effectText: "",
                    inheritedEffectText: "[Your Turn] While this Digimon has 4 or more digivolution cards, it gets +1000 DP.",
                    securityEffectText: "",
                    effectClassName: "ST1_01",
                    rarity: Rarity.U,
                    evoCosts: new List<EvoCost>());

            case "ST1-02":
                return NewOfflineCardDefinition("ST1-02", "Biyomon", CardKind.Digimon, CardColor.Red, 2, 3, 3000, "", "", "", "", Rarity.C, CreateRedEvoCosts(2, 0));

            case "ST1-03":
                return NewOfflineCardDefinition("ST1-03", "Agumon", CardKind.Digimon, CardColor.Red, 3, 3, 2000, "", "[Your Turn] This Digimon gets +1000 DP.", "", "ST1_03", Rarity.U, CreateRedEvoCosts(2, 0));

            case "ST1-04":
                return NewOfflineCardDefinition("ST1-04", "Dracomon", CardKind.Digimon, CardColor.Red, 3, 3, 4000, "", "", "", "", Rarity.C, CreateRedEvoCosts(2, 0));

            case "ST1-05":
                return NewOfflineCardDefinition("ST1-05", "Birdramon", CardKind.Digimon, CardColor.Red, 4, 4, 5000, "", "", "", "", Rarity.C, CreateRedEvoCosts(3, 2));

            case "ST1-06":
                return NewOfflineCardDefinition("ST1-06", "Coredramon", CardKind.Digimon, CardColor.Red, 5, 4, 6000, "＜Blocker＞\n[When Attacking] Lose 2 memory.", "", "", "ST1_06", Rarity.C, CreateRedEvoCosts(3, 2));

            case "ST1-07":
                return NewOfflineCardDefinition("ST1-07", "Greymon", CardKind.Digimon, CardColor.Red, 5, 4, 4000, "", "＜Security A. +1＞", "", "ST1_07", Rarity.U, CreateRedEvoCosts(3, 2));

            case "ST1-08":
                return NewOfflineCardDefinition("ST1-08", "Garudamon", CardKind.Digimon, CardColor.Red, 6, 5, 7000, "[When Digivolving] 1 of your Digimon gets +3000 DP for the turn.", "", "", "ST1_08", Rarity.U, CreateRedEvoCosts(4, 3));

            case "ST1-09":
                return NewOfflineCardDefinition("ST1-09", "MetalGreymon", CardKind.Digimon, CardColor.Red, 7, 5, 7000, "", "[Your Turn] When this Digimon is blocked, gain 3 memory.", "", "ST1_09", Rarity.R, CreateRedEvoCosts(4, 3));

            case "ST1-10":
                return NewOfflineCardDefinition("ST1-10", "Phoenixmon", CardKind.Digimon, CardColor.Red, 10, 6, 12000, "", "", "", "", Rarity.R, CreateRedEvoCosts(5, 2));

            case "ST1-11":
                return NewOfflineCardDefinition("ST1-11", "WarGreymon", CardKind.Digimon, CardColor.Red, 12, 6, 12000, "[Your Turn] For every 2 digivolution cards this Digimon has, it gains ＜Security A. +1＞", "", "", "ST1_11", Rarity.SR, CreateRedEvoCosts(5, 4));

            case "ST1-12":
                return NewOfflineCardDefinition("ST1-12", "Tai Kamiya", CardKind.Tamer, CardColor.Red, 2, 0, 0, "[Your Turn] All of your Digimon get +1000 DP.", "", "[Security] Play this card without paying the cost.", "ST1_12", Rarity.R, new List<EvoCost>());

            case "ST1-13":
                return NewOfflineCardDefinition("ST1-13", "Shadow Wing", CardKind.Option, CardColor.Red, 1, 0, 0, "[Main] 1 of your Digimon gets +3000 DP for the turn.", "", "[Security] All of your Digimon gain ＜Security A. +1＞ until the end of your next turn.", "ST1_13", Rarity.C, new List<EvoCost>());

            case "ST1-14":
                return NewOfflineCardDefinition("ST1-14", "Starlight Explosion", CardKind.Option, CardColor.Red, 2, 0, 0, "[Main] All of your Security Digimon get +7000 DP until the end of your opponent's next turn.", "", "[Security] All of your Security Digimon get +7000 DP for the turn.", "ST1_14", Rarity.C, new List<EvoCost>());

            case "ST1-15":
                return NewOfflineCardDefinition("ST1-15", "Giga Destroyer", CardKind.Option, CardColor.Red, 6, 0, 0, "[Main] Delete up to 2 of your opponent's Digimon with 4000 DP or less.", "", "[Security] Activate this card's [Main] effects.", "ST1_15", Rarity.C, new List<EvoCost>());

            case "ST1-16":
                return NewOfflineCardDefinition("ST1-16", "Gaia Force", CardKind.Option, CardColor.Red, 8, 0, 0, "[Main] Delete 1 of your opponent's Digimon.", "", "[Security] Activate this card's [Main] effects.", "ST1_16", Rarity.U, new List<EvoCost>());

            case "ST2-01":
                return NewOfflineCardDefinition(
                    cardId: "ST2-01",
                    name: "Tsunomon",
                    kind: CardKind.DigiEgg,
                    color: CardColor.Blue,
                    playCost: -1,
                    level: 2,
                    dp: 0,
                    effectText: "",
                    inheritedEffectText: "[Your Turn] This Digimon gets +1000 DP when battling an opponent's Digimon that has no digivolution cards.",
                    securityEffectText: "",
                    effectClassName: "ST2_01",
                    rarity: Rarity.U,
                    evoCosts: new List<EvoCost>());

            case "ST2-02":
                return NewOfflineCardDefinition("ST2-02", "Gomamon", CardKind.Digimon, CardColor.Blue, 2, 3, 3000, "", "", "", "", Rarity.C, CreateBlueEvoCosts(2, 0));

            case "ST2-03":
                return NewOfflineCardDefinition("ST2-03", "Gabumon", CardKind.Digimon, CardColor.Blue, 3, 3, 2000, "", "[When Attacking] Trash the digivolution card at the bottom of 1 of your opponent's Digimon with a level of 5 or less.", "", "ST2_03", Rarity.U, CreateBlueEvoCosts(2, 0));

            case "ST2-04":
                return NewOfflineCardDefinition("ST2-04", "Bearmon", CardKind.Digimon, CardColor.Blue, 3, 3, 4000, "", "", "", "", Rarity.C, CreateBlueEvoCosts(2, 0));

            case "ST2-05":
                return NewOfflineCardDefinition("ST2-05", "Ikkakumon", CardKind.Digimon, CardColor.Blue, 4, 4, 5000, "", "", "", "", Rarity.C, CreateBlueEvoCosts(3, 2));

            case "ST2-06":
                return NewOfflineCardDefinition("ST2-06", "Garurumon", CardKind.Digimon, CardColor.Blue, 5, 4, 4000, "", "[When Attacking] Trash the digivolution card at the bottom of 1 of your opponent's Digimon.", "", "ST2_06", Rarity.U, CreateBlueEvoCosts(3, 2));

            case "ST2-07":
                return NewOfflineCardDefinition("ST2-07", "Grizzlymon", CardKind.Digimon, CardColor.Blue, 5, 4, 6000, "＜Blocker＞\n[When Attacking] Lose 2 memory.", "", "", "ST1_06", Rarity.C, CreateBlueEvoCosts(3, 2));

            case "ST2-08":
                return NewOfflineCardDefinition("ST2-08", "WereGarurumon", CardKind.Digimon, CardColor.Blue, 7, 5, 7000, "", "[Your Turn] While your opponent has a Digimon with no digivolution cards, this Digimon gains ＜Security A. +1＞", "", "ST2_08", Rarity.U, CreateBlueEvoCosts(4, 3));

            case "ST2-09":
                return NewOfflineCardDefinition("ST2-09", "Zudomon", CardKind.Digimon, CardColor.Blue, 6, 5, 7000, "[When Digivolving] Trash 2 digivolution cards at the bottom of 1 of your opponent's Digimon.", "", "", "ST2_09", Rarity.R, CreateBlueEvoCosts(4, 3));

            case "ST2-10":
                return NewOfflineCardDefinition("ST2-10", "Plesiomon", CardKind.Digimon, CardColor.Blue, 10, 6, 12000, "", "", "", "", Rarity.R, CreateBlueEvoCosts(5, 2));

            case "ST2-11":
                return NewOfflineCardDefinition("ST2-11", "MetalGarurumon", CardKind.Digimon, CardColor.Blue, 12, 6, 11000, "[When Attacking] [Once Per Turn] Unsuspend this Digimon.", "", "", "ST2_11", Rarity.SR, CreateBlueEvoCosts(5, 4));

            case "ST2-12":
                return NewOfflineCardDefinition("ST2-12", "Matt Ishida", CardKind.Tamer, CardColor.Blue, 2, 0, 0, "[Start of Your Turn] If your opponent has a Digimon with no Digivolution cards, gain 1 memory.", "", "[Security] Play this card without paying the cost.", "ST2_12", Rarity.R, new List<EvoCost>());

            case "ST2-13":
                return NewOfflineCardDefinition("ST2-13", "Hammer Spark", CardKind.Option, CardColor.Blue, 0, 0, 0, "[Main] Gain 1 memory.", "", "[Security] Gain 2 memory.", "ST2_13", Rarity.C, new List<EvoCost>());

            case "ST2-14":
                return NewOfflineCardDefinition("ST2-14", "Sorrow Blue", CardKind.Option, CardColor.Blue, 2, 0, 0, "[Main] Choose 1 of your opponent's Digimon with no digivolution cards. That Digimon can't attack or block until the end of your opponent's next turn.", "", "[Security] Choose 1 of your opponent's Digimon with no digivolution cards. That Digimon can't attack or block until the end of your next turn.", "ST2_14", Rarity.C, new List<EvoCost>());

            case "ST2-15":
                return NewOfflineCardDefinition("ST2-15", "Kaiser Nail", CardKind.Option, CardColor.Blue, 4, 0, 0, "[Main] Choose 1 Digimon digivolution card placed under 1 of your Digimon and play it without paying the cost.", "", "[Security] Activate this card's [Main] effects.", "ST2_15", Rarity.C, new List<EvoCost>());

            case "ST2-16":
                return NewOfflineCardDefinition("ST2-16", "Cocytus Breath", CardKind.Option, CardColor.Blue, 7, 0, 0, "[Main] Return 1 of your opponent's Digimon to the hand.", "", "[Security] Activate this card's [Main] effects.", "ST2_16", Rarity.U, new List<EvoCost>());
        }

        return null;
    }

    static OfflineCardDefinition NewOfflineCardDefinition(string cardId, string name, CardKind kind, CardColor color, int playCost, int level, int dp, string effectText, string inheritedEffectText, string securityEffectText, string effectClassName, Rarity rarity, List<EvoCost> evoCosts)
    {
        return new OfflineCardDefinition()
        {
            CardId = cardId,
            CardNameEng = name,
            CardKind = kind,
            CardColor = color,
            PlayCost = playCost,
            Level = level,
            DP = dp,
            EffectText = effectText,
            InheritedEffectText = inheritedEffectText,
            SecurityEffectText = securityEffectText,
            EffectClassName = effectClassName,
            Rarity = rarity,
            EvoCosts = evoCosts ?? new List<EvoCost>(),
        };
    }

    static List<EvoCost> CreateRedEvoCosts(int level, int memoryCost)
    {
        return new List<EvoCost>()
        {
            new EvoCost()
            {
                CardColor = CardColor.Red,
                Level = level,
                MemoryCost = memoryCost,
            },
        };
    }

    static List<EvoCost> CreateBlueEvoCosts(int level, int memoryCost)
    {
        return new List<EvoCost>()
        {
            new EvoCost()
            {
                CardColor = CardColor.Blue,
                Level = level,
                MemoryCost = memoryCost,
            },
        };
    }

    static string NormalizeCardCode(string cardCode)
    {
        if (string.IsNullOrWhiteSpace(cardCode))
        {
            return "";
        }

        return cardCode.Trim().Replace("_", "-").ToUpperInvariant();
    }

    static string NormalizeDeckId(string deckName)
    {
        if (string.IsNullOrWhiteSpace(deckName))
        {
            return "deck";
        }

        string normalized = deckName.Trim().ToLowerInvariant();
        normalized = normalized.Replace(" ", "-");
        normalized = normalized.Replace("_", "-");

        return normalized;
    }

    private void CreateDeckFromFile(string id, string name, int keyID, string deckCode, int index = 0)
    {
        List<CEntity_Base> AllDeckCards = DeckCodeUtility.GetAllDeckCardsFromDeckBuilderDeckCode(deckCode);

        if (AllDeckCards.Count == 0)
        {
            AllDeckCards = DeckCodeUtility.GetAllDeckCardsFromTTSDeckCode(deckCode);
        }

        List<CEntity_Base> deckCards = new List<CEntity_Base>();
        List<CEntity_Base> digitamaDeckCards = new List<CEntity_Base>();

        foreach (CEntity_Base cEntity_Base in AllDeckCards)
        {
            if (cEntity_Base.cardKind == CardKind.DigiEgg)
            {
                digitamaDeckCards.Add(cEntity_Base);
            }

            else
            {
                deckCards.Add(cEntity_Base);
            }
        }
        Debug.Log($"Create Deck From File: {name}");
        DeckData deckData = (new DeckData(DeckData.GetDeckCode(name, deckCards, digitamaDeckCards, null),id)).ModifiedDeckData();

        deckData.KeyCardId = keyID;
        deckData.DeckName = name;
        deckData.SortValue = index;

        DeckDatas.Insert(index, deckData);
    }

    #region Player Name
    string _playerName;
    string _playerNameKey = "PlayerName";
    public string PlayerName
    {
        get
        {
            if (string.IsNullOrEmpty(_playerName))
            {
                return "Player";
            }

            return _playerName;
        }

        set
        {
            _playerName = DeckData.ValidateDeckName(value);
        }
    }

    public void SavePlayerName(string playerName)
    {
        PlayerName = playerName;
        PlayerPrefs.SetString(_playerNameKey, playerName);
        PlayerPrefs.Save();
    }

    public void LoadPlayerName()
    {
        if (PlayerPrefs.HasKey(_playerNameKey))
        {
            PlayerName = PlayerPrefs.GetString(_playerNameKey);
        }


        if (string.IsNullOrEmpty(PlayerName))
        {
            PlayerName = "Player";
        }
    }
    #endregion

    #region number of victories
    public int WinCount { get; set; }
    string _winCountKey = "WinCount";

    public void SaveWinCount()
    {
        PlayerPrefs.SetInt(_winCountKey, WinCount);
        PlayerPrefs.Save();
    }
    public void LoadWinCount()
    {
        if (PlayerPrefs.HasKey(_winCountKey))
        {
            WinCount = PlayerPrefs.GetInt(_winCountKey);
        }

    }
    #endregion

    #region Auto effect order
    [HideInInspector] public bool autoEffectOrder = false;
    string _autoEffectOrderKey = "AutoEffectOrder";

    public void SaveAutoEffectOrder()
    {
        PlayerPrefsUtil.SetBool(_autoEffectOrderKey, autoEffectOrder);
        PlayerPrefs.Save();
    }
    public void LoadAutoEffectOrder()
    {
        autoEffectOrder = PlayerPrefsUtil.GetBool(_autoEffectOrderKey, false);
    }
    #endregion

    #region Auto deck bottom order
    [HideInInspector] public bool autoDeckBottomOrder = false;
    string _autoDeckBottomOrderKey = "AutoDeckBottomOrder";

    public void SaveAutoDeckBottomOrder()
    {
        PlayerPrefsUtil.SetBool(_autoDeckBottomOrderKey, autoDeckBottomOrder);
        PlayerPrefs.Save();
    }
    public void LoadAutoDeckBottomOrder()
    {
        autoDeckBottomOrder = PlayerPrefsUtil.GetBool(_autoDeckBottomOrderKey, false);
    }
    #endregion

    #region Auto deck top order
    [HideInInspector] public bool autoDeckTopOrder = false;
    string _autoDeckTopOrderKey = "AutoDeckTopOrder";

    public void SaveAutoDeckTopOrder()
    {
        PlayerPrefsUtil.SetBool(_autoDeckTopOrderKey, autoDeckTopOrder);
        PlayerPrefs.Save();
    }
    public void LoadAutoDeckTopOrder()
    {
        autoDeckTopOrder = PlayerPrefsUtil.GetBool(_autoDeckTopOrderKey, false);
    }
    #endregion

    #region Auto min digivolution cost
    [HideInInspector] public bool autoMinDigivolutionCost = false;
    string _autoMinDigivolutionCostKey = "AutoMinDigivolutionCost";

    public void SaveAutoMinDigivolutionCost()
    {
        PlayerPrefsUtil.SetBool(_autoMinDigivolutionCostKey, autoMinDigivolutionCost);
        PlayerPrefs.Save();
    }
    public void LoadAutoMinDigivolutionCost()
    {
        autoMinDigivolutionCost = PlayerPrefsUtil.GetBool(_autoMinDigivolutionCostKey, false);
    }
    #endregion

    #region Auto max card count
    [HideInInspector] public bool autoMaxCardCount = false;
    string _autoMaxCardCountKey = "AutoMaxCardCount";

    public void SaveAutoMaxCardCount()
    {
        PlayerPrefsUtil.SetBool(_autoMaxCardCountKey, autoMaxCardCount);
        PlayerPrefs.Save();
    }
    public void LoadAutoMaxCardCount()
    {
        autoMaxCardCount = PlayerPrefsUtil.GetBool(_autoMaxCardCountKey, false);
    }
    #endregion

    #region Auto hatch
    [HideInInspector] public bool autoHatch = false;
    string _autoHatchKey = "AutoHatch";

    public void SaveAutoHatch()
    {
        PlayerPrefsUtil.SetBool(_autoHatchKey, autoHatch);
        PlayerPrefs.Save();
    }
    public void LoadAutoHatch()
    {
        autoHatch = PlayerPrefsUtil.GetBool(_autoHatchKey, false);
    }
    #endregion

    #region Show CutIn Animation
    [HideInInspector] public bool showCutInAnimation = false;
    string _showCutInAnimationKey = "ShowCutInAnimation";

    public void SaveShowCutInAnimation()
    {
        PlayerPrefsUtil.SetBool(_showCutInAnimationKey, showCutInAnimation);
        PlayerPrefs.Save();
    }
    public void LoadShowCutInAnimation()
    {
        //TODO: Setting default to false, to fix animation syncing bug, MB
        showCutInAnimation = false;
        //showCutInAnimation = PlayerPrefsUtil.GetBool(_showCutInAnimationKey, true);
    }
    #endregion

    #region Reverse opponents' cards
    [HideInInspector] public bool reverseOpponentsCards = false;
    string _reverseOpponentsCardsKey = "ReverseOpponentsCards";

    public void SaveReverseOpponentsCards()
    {
        PlayerPrefsUtil.SetBool(_reverseOpponentsCardsKey, reverseOpponentsCards);
        PlayerPrefs.Save();
    }
    public void LoadReverseOpponentsCards()
    {
        reverseOpponentsCards = PlayerPrefsUtil.GetBool(_reverseOpponentsCardsKey, false);
    }
    #endregion

    #region Turn suspended cards
    [HideInInspector] public bool turnSuspendedCards = false;
    string _turnSuspendedCardsKey = "TurnSuspendedCards";

    public void SaveTurnSuspendedCards()
    {
        PlayerPrefsUtil.SetBool(_turnSuspendedCardsKey, turnSuspendedCards);
        PlayerPrefs.Save();
    }
    public void LoadTurnSuspendedCards()
    {
        turnSuspendedCards = PlayerPrefsUtil.GetBool(_turnSuspendedCardsKey, true);
    }
    #endregion

    #region Check before ending selection
    [HideInInspector] public bool checkBeforeEndingSelection = false;
    string _checkBeforeEndingSelectionKey = "CheckBeforeEndingSelection";

    public void SaveCheckBeforeEndingSelection()
    {
        PlayerPrefsUtil.SetBool(_checkBeforeEndingSelectionKey, checkBeforeEndingSelection);
        PlayerPrefs.Save();
    }
    public void LoadCheckBeforeEndingSelection()
    {
        checkBeforeEndingSelection = PlayerPrefsUtil.GetBool(_checkBeforeEndingSelectionKey, true);
    }
    #endregion

    #region Suspended cards' direction is left
    [HideInInspector] public bool suspendedCardsDirectionIsLeft = false;
    string _suspendedCardsDirectionIsLeftKey = "SuspendedCardsDirectionIsLeft";

    public void SaveSuspendedCardsDirectionIsLeft()
    {
        PlayerPrefsUtil.SetBool(_suspendedCardsDirectionIsLeftKey, suspendedCardsDirectionIsLeft);
        PlayerPrefs.Save();
    }
    public void LoadSuspendedCardsDirectionIsLeft()
    {
        suspendedCardsDirectionIsLeft = PlayerPrefsUtil.GetBool(_suspendedCardsDirectionIsLeftKey, true);
    }
    #endregion

    #region Show background particle
    [HideInInspector] public bool showBackgroundParticle = false;
    string _showBackgroundParticleKey = "ShowBackgroundParticle";

    public void SaveShowBackgroundParticle()
    {
        PlayerPrefsUtil.SetBool(_showBackgroundParticleKey, showBackgroundParticle);
        PlayerPrefs.Save();
    }
    public void LoadShowBackgroundParticle()
    {
        showBackgroundParticle = PlayerPrefsUtil.GetBool(_showBackgroundParticleKey, false);

        if (BootstrapConfig.IsOfflineLocal)
        {
            showBackgroundParticle = false;
        }
    }
    #endregion

    #region Sound volume
    public float BGMVolume { get; set; }
    public float SEVolume { get; set; }

    public void SetBGMVolume(float BGMVolume)
    {
        this.BGMVolume = BGMVolume;

        PlayerPrefs.SetFloat("BGMVolume", BGMVolume);
        PlayerPrefs.Save();
    }

    public void SetSEVolume(float SEVolume)
    {
        this.SEVolume = SEVolume;

        PlayerPrefs.SetFloat("SEVolume", SEVolume);
        PlayerPrefs.Save();
    }

    public void ChangeBGMVolume(AudioSource audioSource)
    {
        audioSource.volume = BGMVolume * 0.25f * 0.8f;
    }

    public void ChangeSEVolume(AudioSource audioSource)
    {
        audioSource.volume = SEVolume * 0.5f * 0.8f;
    }

    void LoadVolume()
    {
        BGMVolume = 0.5f;
        SEVolume = 0.5f;

        if (PlayerPrefs.HasKey("BGMVolume"))
        {
            BGMVolume = PlayerPrefs.GetFloat("BGMVolume");
        }

        if (PlayerPrefs.HasKey("SEVolume"))
        {
            SEVolume = PlayerPrefs.GetFloat("SEVolume");
        }
    }
    #endregion

    #region Server region
    [HideInInspector] public string serverRegion = "us";
    string _serverRegionKey = "ServerRegion";

    public void SaveServerRegion()
    {
        PlayerPrefs.SetString(_serverRegionKey, serverRegion);
        PlayerPrefs.Save();
    }
    public void LoadServerRegion()
    {
        //serverRegion = PlayerPrefs.GetString(_serverRegionKey, "us");
    }
    public string LastConnectServerRegion = "";
    #endregion

    #region Language
    [HideInInspector] public Language language = Language.ENG;
    string _languageKey = "Language";

    public void SaveLanguage()
    {
        PlayerPrefs.SetString(_languageKey, language.ToString());
        PlayerPrefs.Save();
    }
    public void LoadLanguage()
    {
        language = (Language)Enum.Parse(typeof(Language), PlayerPrefs.GetString(_languageKey, "ENG"));
    }
    #endregion

    #region PlaySE(AudioClip clip)
    public SoundObject PlaySE(AudioClip clip)
    {
        SoundObject _soundObject = Instantiate(soundObject);

        _soundObject.PlaySE(clip);

        return _soundObject;
    }
    #endregion

    #region カードIndexからカードを取得
    public CEntity_Base getCardEntityByCardID(int cardIndex)
    {
        //int searchIndex = cardIndex - 1;
        //int count = 0;

        if (SortedCardList == null)
        {
            return null;
        }

        CEntity_Base cEntity_Base = SortedCardList.FirstOrDefault(entity => entity != null && entity.CardIndex == cardIndex);

        return cEntity_Base;

        //TODO: REMOVE IN FUTURE
        /*do
        {
            if (count != 0)
            {
                searchIndex += (int)Math.Pow(-1, count % 2) * count / 2;
            }

            if (0 <= searchIndex)
            {
                if (searchIndex <= SortedCardList.Length - 1)
                {
                    CEntity_Base cEntity_Base = SortedCardList[searchIndex];

                    if (cEntity_Base != null)
                    {
                        if (cEntity_Base.CardIndex == cardIndex)
                        {
                            return cEntity_Base;
                        }
                    }
                }

                else
                {
                    for (int i = 0; i < 300; i++)
                    {
                        CEntity_Base cEntity_Base = SortedCardList[SortedCardList.Length - 1 - i];

                        if (cEntity_Base != null)
                        {
                            if (cEntity_Base.CardIndex == cardIndex)
                            {
                                return cEntity_Base;
                            }
                        }
                    }

                    return null;
                }
            }

            if (count != 0)
            {
                searchIndex -= (int)Math.Pow(-1, count % 2) * count / 2;
            }

            count++;
        }

        while (count <= 20);

        return null;*/
    }
    #endregion

    public Coroutine LoadingTextCoroutine;

    bool _endBattle = false;

    public void EndBattle()
    {
        if (!_endBattle)
        {
            _endBattle = true;
            StartCoroutine(EndBattleCoroutine());
        }
    }
    public IEnumerator EndBattleCoroutine()
    {
        if (Opening.instance == null)
        {
            yield break;
        }

        Opening.instance.openingObject.SetActive(true);

        //yield return StartCoroutine(Opening.instance.LoadingObject_Unload.StartLoading("Now Loading"));

        //Camera camera1 = Camera.main;

        //Destroy(camera1.gameObject);

        //yield return null;

        bool endedFromAiMatch = isAI;
        bool endedFromRandomMatch = isRandomMatch;
        GameObject postBattleSelectionTarget = null;

        isAI = false;

        int random = RandomUtility.getRamdom();
        UnityEngine.Random.InitState(random);
        Debug.Log($"random number sequence initialization, InitState:{random}");

        var unload = SceneManager.UnloadSceneAsync("BattleScene");
        yield return unload;

        yield return Resources.UnloadUnusedAssets();

        yield return StartCoroutine(Opening.instance.LoadingObject_Unload.StartLoading("Now Loading"));

        //Opening.instance.MainCamera.gameObject.SetActive(true);

        foreach (Camera camera in Opening.instance.openingCameras)
        {
            camera.gameObject.SetActive(true);
        }

        Opening.instance.LoadingObject_light.gameObject.SetActive(false);
        yield return ContinuousController.instance.StartCoroutine(PhotonUtility.SetPlayerName());

        if (endedFromAiMatch)
        {
            Debug.Log("Unload from Offline AI Match");
            yield return StartCoroutine(Opening.instance.battle.lobbyManager_RandomMatch.CloseLobbyCoroutine());
            Opening.instance.battle.OffBattle();
            Opening.instance.home.SetUpHome();

            if (Opening.instance.battle.BattleButton != null)
            {
                postBattleSelectionTarget = Opening.instance.battle.BattleButton.gameObject;
            }
        }

        else if (endedFromRandomMatch)
        {
            Debug.Log("Unload from Random Match");
            yield return StartCoroutine(Opening.instance.battle.lobbyManager_RandomMatch.CloseLobbyCoroutine());
            yield return StartCoroutine(Opening.instance.battle.selectBattleMode.SetUpSelectBattleModeCoroutine());

            if (Opening.instance.battle.selectBattleMode != null && Opening.instance.battle.selectBattleMode.transform.childCount > 0)
            {
                postBattleSelectionTarget = Opening.instance.battle.selectBattleMode.transform.GetChild(0).gameObject;
            }
        }

        else
        {
            Debug.Log("Unload from Room Match");
            yield return StartCoroutine(Opening.instance.battle.roomManager.Init(true));
            yield return new WaitForSeconds(0.1f);

            if (Opening.instance.battle.selectBattleMode != null && Opening.instance.battle.selectBattleMode.transform.childCount > 0)
            {
                postBattleSelectionTarget = Opening.instance.battle.selectBattleMode.transform.GetChild(0).gameObject;
            }
        }

        yield return new WaitWhile(() => GManager.instance != null);

        Opening.instance.LoadingObject.gameObject.SetActive(false);
        yield return StartCoroutine(Opening.instance.LoadingObject_Unload.EndLoading());
        _endBattle = false;

        if (!endedFromRandomMatch)
        {
            Hashtable PlayerProp = PhotonNetwork.LocalPlayer.CustomProperties;

            if (PlayerProp.TryGetValue("isBattle", out object value))
            {
                PlayerProp["isBattle"] = false;
            }

            else
            {
                PlayerProp.Add("isBattle", false);
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(PlayerProp);
        }

        Scene newScene = SceneManager.GetSceneByName("Opening");
        SceneManager.SetActiveScene(newScene);

        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(0.1f);

            if (EventSystem.current != null && postBattleSelectionTarget != null)
            {
                EventSystem.current.SetSelectedGameObject(postBattleSelectionTarget);
            }
        }

        //GUI.UnfocusWindow();

        yield return null;

        //StartCoroutine(DestroyEffectCoroutine());

        if (Opening.instance.OpeningBGM != null)
        {
            if (!Opening.instance.OpeningBGM.isPlaying)
            {
                Opening.instance.OpeningBGM.StartPlayBGM(Opening.instance.bgm);
            }
        }
    }
    private void Update()
    {
#if UNITY_WINDOWS
        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }
#endif
    }
    int _frameCount = 0;
    int _updateFrame = 40;
    void LateUpdate()
    {
        #region Update only once every few frames
        _frameCount++;

        if (_frameCount < _updateFrame)
        {
            return;
        }

        else
        {
            _frameCount = 0;
        }
        #endregion

        if (PhotonNetwork.InRoom)
        {
            if (!isAI)
            {
                bool notEnterOther = false;

                if (PhotonNetwork.PlayerList.Length == 1)
                {
                    if (GManager.instance != null)
                    {
                        notEnterOther = true;
                    }
                }

                if (notEnterOther)
                {
                    if (PhotonNetwork.CurrentRoom.MaxPlayers != 1)
                    {
                        PhotonNetwork.CurrentRoom.MaxPlayers = 1;
                    }
                }

                else
                {
                    if (PhotonNetwork.CurrentRoom.MaxPlayers != 2)
                    {
                        PhotonNetwork.CurrentRoom.MaxPlayers = 2;
                    }
                }
            }
        }
    }

    public bool isAI { get; set; } = false;

    //Flag that the sharing of the random number sequence is over.
    public bool DoneSetRandom { get; set; } = false;
    public bool CanSetRandom { get; set; } = false;
    [PunRPC]
    public void SetRandom(int random)
    {
        StartCoroutine(SetRandomCoroutine(random));
    }

    IEnumerator SetRandomCoroutine(int random)
    {
        yield return new WaitWhile(() => !CanSetRandom);

        UnityEngine.Random.InitState(random);
        DoneSetRandom = true;

        Debug.Log($"random number sequence initialization,InitState:{random}");
    }


}

#region Manage random numbers
public static class RandomUtility
{
    private static System.Random random;
    public static int getRamdom()
    {
        int _max = 1500000000;

        if (random == null)
        {
            random = new System.Random((int)DateTime.Now.Ticks);
        }

        return random.Next(0, _max);
    }

    #region IsSucceedProbability(float Probability)
    public static bool IsSucceedProbability(float Probability)
    {
        if (Probability >= 1)
        {
            return true;
        }

        if (Probability <= 0)
        {
            return false;
        }

        float random = UnityEngine.Random.Range(0f, 1f);

        if (random <= Probability)
        {
            return true;
        }

        return false;
    }
    #endregion

    #region Shuffle the deck
    public static List<CEntity_Base> ShuffledDeckCards(List<CEntity_Base> DeckCards)
    {
        List<CEntity_Base> CardDatas = new List<CEntity_Base>();
        CardDatas.AddRange(DeckCards);

        // The initial value of the integer n is the number of cards in the deck
        int n = CardDatas.Count;

        while (n > 0)
        {
            n--;

            // Random index from 0 to i (inclusive)
            int k = UnityEngine.Random.Range(0, n + 1);

            // Swap elements at indices i and k
            CEntity_Base temp = CardDatas[n];
            CardDatas[n] = CardDatas[k];
            CardDatas[k] = temp;
        }


        return CardDatas;
    }

    public static List<CardSource> ShuffledDeckCards(List<CardSource> DeckCards)
    {
        List<CardSource> CardDatas = new List<CardSource>();
        CardDatas.AddRange(DeckCards);

        // The initial value of the integer n is the number of cards in the deck
        int n = CardDatas.Count;

        while (n > 0)
        {
            n--;

            // Random index from 0 to i (inclusive)
            int k = UnityEngine.Random.Range(0, n + 1);

            // Swap elements at indices i and k
            CardSource temp = CardDatas[n];

            if (!temp.IsFlipped)
            {
                temp.SetReverse();

                if(temp.Owner.SecurityCards.Contains(temp))
                    GManager.OnSecurityStackChanged?.Invoke(temp.Owner);
            }
                

            CardDatas[n] = CardDatas[k];
            CardDatas[k] = temp;
        }

        return CardDatas;
    }

    #endregion
}
#endregion

#region Manage connections to Photon
public class PhotonUtility
{
    static IMatchTransport CurrentTransport => MatchTransportFactory.CurrentTransport;

    static bool CanAccessLocalPlayerProperties()
    {
        return PhotonNetwork.IsConnectedAndReady && PhotonNetwork.LocalPlayer != null;
    }

    #region Disconnected from Photon
    public static IEnumerator DisconnectCoroutine()
    {
        yield return ContinuousController.instance.StartCoroutine(CurrentTransport.Disconnect());
    }
    #endregion

    #region Connect to Photon server
    public static IEnumerator ConnectToMasterServerCoroutine()
    {
        yield return ContinuousController.instance.StartCoroutine(CurrentTransport.ConnectToMasterServer());
    }
    #endregion
    #region Connect to Photon Server and Lobby
    public static IEnumerator ConnectToLobbyCoroutine()
    {
        #region Connect to Photon server
        yield return ContinuousController.instance.StartCoroutine(CurrentTransport.ConnectToLobby());
        #endregion

        #region Save player name to custom properties
        yield return ContinuousController.instance.StartCoroutine(SetPlayerName());
        #endregion

        if (!CanAccessLocalPlayerProperties())
        {
            yield break;
        }

        #region Save the number of wins to a custom property
        Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;

        object value;

        hash = PhotonNetwork.LocalPlayer.CustomProperties;

        if (hash.TryGetValue(ContinuousController.WinCountKey, out value))
        {
            hash[ContinuousController.WinCountKey] = ContinuousController.instance.WinCount;
        }

        else
        {
            hash.Add(ContinuousController.WinCountKey, ContinuousController.instance.WinCount);
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

        while (true)
        {
            Hashtable _hash = PhotonNetwork.LocalPlayer.CustomProperties;

            if (_hash.TryGetValue(ContinuousController.WinCountKey, out value))
            {
                if ((int)value == ContinuousController.instance.WinCount)
                {
                    break;
                }

            }

            yield return null;
        }
        #endregion
    }
    #endregion

    #region Save player name to properties
    public static IEnumerator SetPlayerName()
    {
        if (!CanAccessLocalPlayerProperties())
        {
            yield break;
        }

        Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;

        object value;

        hash = PhotonNetwork.LocalPlayer.CustomProperties;

        if (hash.TryGetValue(ContinuousController.PlayerNameKey, out value))
        {
            hash[ContinuousController.PlayerNameKey] = ContinuousController.instance.PlayerName;
        }

        else
        {
            hash.Add(ContinuousController.PlayerNameKey, ContinuousController.instance.PlayerName);
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

        while (true)
        {
            Hashtable _hash = PhotonNetwork.LocalPlayer.CustomProperties;

            if (_hash.TryGetValue(ContinuousController.PlayerNameKey, out value))
            {
                if ((string)value == ContinuousController.instance.PlayerName)
                {
                    break;
                }
            }

            yield return null;
        }
    }
    #endregion

    #region Save deck data to custom properties
    public static IEnumerator SignUpBattleDeckData()
    {
        if (ContinuousController.instance.BattleDeckData == null || !CanAccessLocalPlayerProperties())
        {
            yield break;
        }

        Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;

        if (hash.TryGetValue(ContinuousController.DeckDataPropertyKey, out object value))
        {
            hash[ContinuousController.DeckDataPropertyKey] = ContinuousController.instance.BattleDeckData.GetThisDeckCode();
        }

        else
        {
            hash.Add(ContinuousController.DeckDataPropertyKey, ContinuousController.instance.BattleDeckData.GetThisDeckCode());
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

        while (true)
        {
            Hashtable _hash = PhotonNetwork.LocalPlayer.CustomProperties;

            if (_hash.TryGetValue(ContinuousController.DeckDataPropertyKey, out value))
            {
                if ((string)value == ContinuousController.instance.BattleDeckData.GetThisDeckCode())
                {
                    break;
                }
            }

            yield return null;
        }
    }
    #endregion

    #region Remove custom properties from deck data
    public static IEnumerator DeleteBattleDeckData()
    {
        if (!CanAccessLocalPlayerProperties())
        {
            yield break;
        }

        Hashtable hash = PhotonNetwork.LocalPlayer.CustomProperties;

        if (hash.TryGetValue(ContinuousController.DeckDataPropertyKey, out object value))
        {
            hash.Remove(ContinuousController.DeckDataPropertyKey);
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

        while (true)
        {
            Hashtable _hash = PhotonNetwork.LocalPlayer.CustomProperties;

            if (!_hash.TryGetValue(ContinuousController.DeckDataPropertyKey, out value))
            {
                break;
            }

            yield return null;
        }
    }
    #endregion
}
#endregion

public enum Language
{
    ENG,
    JPN,
}
