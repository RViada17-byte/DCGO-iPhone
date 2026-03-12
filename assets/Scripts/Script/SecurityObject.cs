using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Linq;

public class SecurityObject : MonoBehaviour
{
    static readonly Color YouSecurityColor = new Color32(104, 220, 255, 255);
    static readonly Color OpponentSecurityColor = new Color32(255, 170, 84, 255);

    [Header("プレイヤー")]
    [SerializeField] Player player;

    [Header("セキュリティテキスト")]
    public Text SecurityText;

    [Header("Face up card Icon")]
    public GameObject faceupIcon;

    [Header("ライフカード")]
    public List<Image> LifeCards = new List<Image>();

    [Header("クリック判定")]
    public GameObject Collider;

    [Header("セキュリティガラス")]
    public SecurityBreakGlass securityBreakGlass;

    [Header("セキュリティアタックDropArea")]
    public DropArea securityAttackDropArea;

    [Header("セキュリティアタック表示オブジェクト")]
    [SerializeField] GameObject ShowSecurityAttackObject;

    [Header("セキュリティアタック表示テキスト")]
    [SerializeField] Text ShowSecurityAttackText;

    [Header("セキュリティアタック表示画像")]
    [SerializeField] Image ShowSecurityAttackImage;
    [SerializeField] Image _securityIconImage;
    [SerializeField] Sprite _fallbackYouSecurityIcon;
    [SerializeField] Sprite _fallbackOpponentSecurityIcon;

    UnityAction OnClickAction = null;
    private async void Start()
    {
        RemoveClickTarget();

        securityBreakGlass.Init(null);

        OffShowSecurityAttackObject();

        if (_securityIconImage != null)
        {
            if (player != null)
            {
                Sprite securityIconSprite = await ResolveSecurityIconSprite(player.isYou);

                if (securityIconSprite != null)
                {
                    _securityIconImage.sprite = securityIconSprite;
                }

                _securityIconImage.color = player.isYou ? YouSecurityColor : OpponentSecurityColor;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BattleSecurityVisualLogger.LogSpecificImage(_securityIconImage, "SecurityObject.Start");
#endif
        }

        ConfigureSecurityTextStyle();

        GManager.OnSecurityStackChanged += CheckFaceupSecurity;
    }

    public void OffShowSecurityAttackObject()
    {
        if (ShowSecurityAttackObject != null)
        {
            ShowSecurityAttackObject.SetActive(false);
        }
    }

    public void SetSecurityAttackObject()
    {
        if (ShowSecurityAttackObject != null)
        {
            ShowSecurityAttackObject.SetActive(true);

            if (player != null)
            {
                if (ShowSecurityAttackText != null)
                {
                    if (player.SecurityCards.Count >= 1)
                    {
                        ShowSecurityAttackText.text = "Security Attack";
                    }

                    else
                    {
                        ShowSecurityAttackText.text = "Direct Attack";
                    }
                }
            }
        }
    }

    public void SetSecurityOutline(bool isSelected)
    {
        if (ShowSecurityAttackImage != null)
        {
            Outline outline = ShowSecurityAttackImage.GetComponent<Outline>();

            if (outline != null)
            {
                if (isSelected)
                {
                    outline.effectColor = new Color32(0, 0, 0, 255);
                }

                else
                {
                    outline.effectColor = new Color32(0, 0, 0, 50);
                }
            }
        }
    }

    public void SetSecurity(Player player)
    {
        int securityCount = player.SecurityCards.Count;
        SecurityText.text = securityCount.ToString();
        SecurityText.color = player.isYou ? YouSecurityColor : OpponentSecurityColor;

        for (int i = 0; i < LifeCards.Count; i++)
        {
            if (i < player.SecurityCards.Count)
            {
                LifeCards[i].gameObject.SetActive(true);
                LifeCards[i].sprite = ContinuousController.instance.ReverseCard;
                LifeCards[i].color = player.isYou ? new Color32(180, 240, 255, 255) : new Color32(255, 215, 170, 255);
            }

            else
            {
                LifeCards[i].gameObject.SetActive(false);
            }
        }
    }

    void ConfigureSecurityTextStyle()
    {
        if (SecurityText == null)
        {
            return;
        }

        SecurityText.resizeTextForBestFit = true;
        SecurityText.resizeTextMinSize = 10;
        SecurityText.resizeTextMaxSize = 32;

        Outline outline = SecurityText.GetComponent<Outline>();
        if (outline == null)
        {
            outline = SecurityText.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color32(0, 0, 0, 255);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    async System.Threading.Tasks.Task<Sprite> ResolveSecurityIconSprite(bool isYouSide)
    {
        string primaryKey = isYouSide ? "SecurityIcon_You" : "SecurityIcon_Opponent";
        Sprite icon = await StreamingAssetsUtility.GetSprite(primaryKey);
        if (icon != null)
        {
            return icon;
        }

        string[] fallbackStreamingKeys = isYouSide
            ? new[] { "Faceup Security Player Icon", "Security Player Icon" }
            : new[] { "Faceup Security Opponent Icon", "Security Opponent Icon" };

        for (int i = 0; i < fallbackStreamingKeys.Length; i++)
        {
            icon = await StreamingAssetsUtility.GetSprite(fallbackStreamingKeys[i]);
            if (icon != null)
            {
                return icon;
            }
        }

        return isYouSide ? _fallbackYouSecurityIcon : _fallbackOpponentSecurityIcon;
    }
    public void RemoveClickTarget()
    {
        if (Collider != null)
        {
            Collider.SetActive(false);
            this.OnClickAction = null;
        }
    }

    public void AddClickTarget(UnityAction OnClickAction)
    {
        if (Collider != null)
        {
            Collider.SetActive(true);
            this.OnClickAction = OnClickAction;
        }
    }

    public void OnClick()
    {
        OnClickAction?.Invoke();
        RemoveClickTarget();
    }

    public void CheckFaceupSecurity(Player changedPlayer)
    {
        if (player != changedPlayer)
            return;

        faceupIcon.SetActive((changedPlayer.SecurityCards.Count(cardSource => !cardSource.IsFlipped) > 0));
    }

    public void OnDestroy()
    {
        GManager.OnSecurityStackChanged -= CheckFaceupSecurity;
    }
}
