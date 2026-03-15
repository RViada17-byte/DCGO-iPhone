using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Threading.Tasks;

[CreateAssetMenu(menuName = "Create/CEntity_Base")]
public class CEntity_Base : ScriptableObject
{
    public int CardIndex = 0;
    public List<int> LegacyCardIndices = new List<int>();
    public List<CardColor> cardColors = new List<CardColor>();
    public int PlayCost = -1;
    public List<EvoCost> EvoCosts = new List<EvoCost>();
    public int Level = 0;
    public string CardName_JPN = "";
    public string CardName_ENG = "";
    public List<string> Form_JPN = new List<string>();
    public List<string> Form_ENG = new List<string>();
    public List<string> Attribute_JPN = new List<string>();
    public List<string> Attribute_ENG = new List<string>();
    public List<string> Type_JPN = new List<string>();
    public List<string> Type_ENG = new List<string>();
    public string CardSpriteName = "";
    public string PrintID = "";
    public bool IsCanonicalPrint = false;
    public CardKind cardKind = CardKind.Digimon;
    [TextArea] public string EffectDiscription_JPN = "";
    [TextArea] public string EffectDiscription_ENG = "";
    [TextArea] public string InheritedEffectDiscription_JPN = "";
    [TextArea] public string InheritedEffectDiscription_ENG = "";
    [TextArea] public string SecurityEffectDiscription_JPN = "";
    [TextArea] public string SecurityEffectDiscription_ENG = "";
    public string CardEffectClassName = "";
    public int DP = 0;
    public Rarity rarity;
    public int OverflowMemory = 0;
    public int LinkDP = 0;
    [TextArea] public string LinkEffect = "";
    [TextArea] public string LinkRequirement = "";

    public bool HasInhetitedEffect => !string.IsNullOrEmpty(InheritedEffectDiscription_ENG) && !InheritedEffectDiscription_ENG.Equals("-");
    public bool HasSecutiryEffect => !string.IsNullOrEmpty(SecurityEffectDiscription_ENG) && !SecurityEffectDiscription_ENG.Equals("-");
    public string CardID = "";
    public int MaxCountInDeck = 4;
    public bool HasLoadStarted { get; set; } = false;
    public Sprite CardSprite { get; set; } = null;
    public string EffectivePrintID => CardPrintCatalog.GetStoredPrintId(this);

    public void ClearLoadedCardImageReference()
    {
        CardSprite = null;
        HasLoadStarted = false;
    }

    public async Task LoadCardImage()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("CEntity_Base.LoadCardImage");
        bool skippedEmpty = String.IsNullOrEmpty(CardSpriteName);
        bool skippedStarted = !skippedEmpty && HasLoadStarted;

        StartupPerfTrace.RecordLoadCardImageCall(skippedEmpty, skippedStarted);

        try
        {
            if (skippedEmpty)
            {
                return;
            }

            if (skippedStarted)
            {
                return;
            }

            HasLoadStarted = true;

            Sprite sprite = await StreamingAssetsUtility.GetSprite(CardSpriteName, isCard: true);

            CardSprite = sprite;
            StartupPerfTrace.RecordLoadCardImageCompleted(sprite != null);
        }
        finally
        {
            perfScope.SetItemCount("skippedEmpty", skippedEmpty ? 1 : 0);
            perfScope.SetItemCount("skippedStarted", skippedStarted ? 1 : 0);
            perfScope.SetItemCount("hasSprite", CardSprite != null ? 1 : 0);
            perfScope.Dispose();
        }
    }

    public async Task<Sprite> GetCardSprite()
    {
        StartupPerfTrace.Scope perfScope = StartupPerfTrace.Measure("CEntity_Base.GetCardSprite");
        StartupPerfTrace.RecordGetCardSpriteCall();

        try
        {
            if (!HasLoadStarted)
            {
                await LoadCardImage();
            }

            return CardSprite;
        }
        finally
        {
            perfScope.SetItemCount("hasLoadStarted", HasLoadStarted ? 1 : 0);
            perfScope.SetItemCount("hasSprite", CardSprite != null ? 1 : 0);
            perfScope.Dispose();
        }
    }
    public bool IsACE => OverflowMemory >= 1;
    public bool IsStandardValid => true;

    #region regulation mark
    public string RegulationMark
    {
        get
        {
            string regulationMark = "";
            string setId = SetID;

            switch (setId)
            {
                case "ST1":
                    regulationMark = "00";
                    break;

                case "ST2":
                    regulationMark = "00";
                    break;

                case "ST3":
                    regulationMark = "00";
                    break;

                case "ST4":
                    regulationMark = "00";
                    break;

                case "ST5":
                    regulationMark = "00";
                    break;

                case "ST6":
                    regulationMark = "00";
                    break;

                case "ST7":
                    regulationMark = "01";
                    break;

                case "ST8":
                    regulationMark = "01";
                    break;

                case "ST9":
                    regulationMark = "01";
                    break;

                case "ST10":
                    regulationMark = "01";
                    break;

                case "ST11":
                    regulationMark = "01";
                    break;

                case "ST12":
                    regulationMark = "02";
                    break;

                case "ST13":
                    regulationMark = "02";
                    break;

                case "ST14":
                    regulationMark = "02";
                    break;

                case "BT1":
                    regulationMark = "00";
                    break;

                case "BT2":
                    regulationMark = "00";
                    break;

                case "BT3":
                    regulationMark = "00";
                    break;

                case "BT4":
                    regulationMark = "00";
                    break;

                case "BT5":
                    regulationMark = "00";
                    break;

                case "BT6":
                    regulationMark = "01";
                    break;

                case "BT7":
                    regulationMark = "01";
                    break;

                case "BT8":
                    regulationMark = "01";
                    break;

                case "BT9":
                    regulationMark = "01";
                    break;

                case "BT10":
                    regulationMark = "02";
                    break;

                case "BT11":
                    regulationMark = "02";
                    break;

                case "BT12":
                    regulationMark = "02";
                    break;

                case "BT13":
                    regulationMark = "02";
                    break;

                case "EX1":
                    regulationMark = "01";
                    break;

                case "EX2":
                    regulationMark = "01";
                    break;

                case "EX3":
                    regulationMark = "02";
                    break;

                case "EX4":
                    regulationMark = "02";
                    break;

                case "RB1":
                    regulationMark = "02";
                    break;
            }

            return regulationMark;
        }
    }
    #endregion

    #region �Z�b�gID
    public string SetID
    {
        get
        {
            string normalizedName = !string.IsNullOrWhiteSpace(CardID)
                ? (CardID ?? "").Replace("_", "-")
                : (CardSpriteName ?? "").Replace("_", "-");

            if (GetParseByHyphen(normalizedName).Length >= 1)
            {
                return GetParseByHyphen(normalizedName)[0];
            }

            return "";
        }
    }
    #endregion

    #region whether it is permanent card
    public bool IsPermanent => cardKind == CardKind.Digimon || cardKind == CardKind.Tamer || cardKind == CardKind.DigiEgg;
    #endregion

    #region �J�[�hIndex���f�b�L�R�[�h�ɗp���镶����ɕϊ�(256�i��)
    public string CardIndex_String
    {
        get
        {
            string CardID_String = ConvertBinaryNumber.IntToNString(CardIndex, DeckData.m);

            while (CardID_String.Length < DeckData.CardKindCellLength)
            {
                CardID_String = $"0{CardID_String}";
            }

            return CardID_String;
        }
    }
    #endregion

    #region �J�[�h�摜�����A���_�[�o�[�ŋ�؂�
    public static string[] GetParseByUnderBar(string CardImageName)
    {
        string[] parseByUnderBar = new string[] { CardImageName };

        if (CardImageName.Contains('_'))
        {
            parseByUnderBar = CardImageName.Split('_');
        }

        return parseByUnderBar;
    }
    #endregion

    #region �J�[�h�摜�����n�C�t���ŋ�؂�
    public static string[] GetParseByHyphen(string CardImageName)
    {
        string[] parseByHyphen = new string[] { CardImageName };

        if (CardImageName.Contains('-'))
        {
            parseByHyphen = CardImageName.Split('-');
        }

        return parseByHyphen;
    }
    #endregion

    #region �f�b�L�ɂ���Ɠ��J�[�hID�̃J�[�h�������Ă��閇��
    public int SameCardIDCount(List<CEntity_Base> DeckCards)
    {
        return DeckCards.Count((cEntity_Base) => cEntity_Base.CardID == CardID);
    }
    #endregion

    #region �p��������
    public bool isParallel
    {
        get
        {
            if (!string.IsNullOrEmpty(CardSpriteName))
            {
                string s = EffectivePrintID.Replace(CardID, "");

                if (!string.IsNullOrEmpty(s))
                {
                    if (s.Contains("_P"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
    #endregion

    #region ���x��������
    public bool HasLevel
    {
        get
        {
            if (Level == 0)
            {
                return false;
            }

            if (cardKind != CardKind.Digimon && cardKind != CardKind.DigiEgg)
            {
                return false;
            }

            return true;
        }
    }
    #endregion

    #region Whether the card has any cost
    public bool HasCost => PlayCost >= 0;
    #endregion

    #region Wheter the card has play cost
    public bool HasPlayCost => cardKind != CardKind.Option && PlayCost >= 0;
    #endregion

    #region Wheter the card has use cost
    public bool HasUseCost => cardKind == CardKind.Option && PlayCost >= 0;
    #endregion
}
public enum CardKind
{
    Digimon,
    Tamer,
    Option,
    DigiEgg,
}

[Serializable]
public class EvoCost : IEquatable<EvoCost>
{
    public CardColor CardColor;
    public int Level;
    public int MemoryCost;

    public bool Equals(EvoCost other)
    {
        if (other is null)
        {
            return false;
        }

        if (object.ReferenceEquals(this, other))
        {
            return true;
        }

        return CardColor.Equals(other.CardColor) && Level.Equals(other.Level) && MemoryCost.Equals(other.MemoryCost);
    }

    public override int GetHashCode() => CardColor.GetHashCode() ^ Level.GetHashCode() + MemoryCost.GetHashCode();
}

public enum CardColor
{
    Red,
    Blue,
    Yellow,
    Green,
    White,
    Black,
    Purple,
    None,
}

public enum Rarity
{
    C,
    U,
    R,
    SR,
    SEC,
    P,
    None,
}
