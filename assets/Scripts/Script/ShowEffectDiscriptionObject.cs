using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;
public class ShowEffectDiscriptionObject : MonoBehaviour
{
    [SerializeField] Transform parent;
    [SerializeField] Image CardImage;
    [SerializeField] Image CardImageOutline;
    [SerializeField] TextMeshProUGUI EffectDiscriptionText;

    ICardEffect cardEffect;

    public async void ShowEffectDiscription(ICardEffect cardEffect)
    {
        if (cardEffect != null)
        {
            if (cardEffect.EffectSourceCard != null)
            {
                if (string.IsNullOrEmpty(cardEffect.EffectDiscription))
                {
                    CloseShowEffectDiscription();
                }

                else
                {
                    this.cardEffect = cardEffect;

                    CardImage.sprite = await cardEffect.EffectSourceCard.GetCardSprite();
                    CardImageOutline.color = DataBase.CardColor_ColorDarkDictionary[cardEffect.EffectSourceCard.BaseCardColorsFromEntity[0]];
                    EffectDiscriptionText.text = DataBase.ReplaceToASCII(cardEffect.EffectDiscription);
                    ApplyMobileStyle();

                    Sequence sequence = DOTween.Sequence();

                    sequence
                        .Append(parent.DOLocalMoveX(840, 0.1f).SetEase(Ease.OutQuad));

                    sequence.Play();

                    StartCoroutine(CloseIEnumerator());
                }
            }
        }
    }

    void ApplyMobileStyle()
    {
        if (!Application.isMobilePlatform)
        {
            return;
        }

        if (EffectDiscriptionText != null)
        {
            EffectDiscriptionText.color = new Color32(245, 245, 245, 255);
            if (EffectDiscriptionText.outlineWidth < 0.12f)
            {
                EffectDiscriptionText.outlineWidth = 0.12f;
            }

            EffectDiscriptionText.outlineColor = new Color32(0, 0, 0, 230);
        }

        Image[] panelImages = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < panelImages.Length; i++)
        {
            Image image = panelImages[i];
            if (image == null)
            {
                continue;
            }

            image.material = null;

            string objectName = image.gameObject.name.ToLowerInvariant();
            if (objectName.Contains("cardimage") || objectName.Contains("card_image") || objectName == "card")
            {
                continue;
            }

            if (objectName.Contains("backgorund") || objectName.Contains("background"))
            {
                image.color = new Color(0f, 0f, 0f, 0.72f);
                continue;
            }

            Color color = image.color;
            bool isWashedPanel = color.a >= 0.12f && color.r >= 0.72f && color.g >= 0.72f && color.b >= 0.72f;
            if (isWashedPanel)
            {
                image.color = new Color(0f, 0f, 0f, Mathf.Clamp(color.a * 0.9f, 0.32f, 0.72f));
            }
        }
    }

    IEnumerator CloseIEnumerator()
    {
        yield return new WaitForSeconds(5.5f);

        CloseShowEffectDiscription();
    }

    public void CloseShowEffectDiscription()
    {
        DestroyImmediate(this.gameObject);
    }

    public void OnClickCardImage()
    {
        if (this.cardEffect != null)
        {
            if (this.cardEffect.EffectSourceCard != null)
            {
                GManager.instance.cardDetail.OpenCardDetail(this.cardEffect.EffectSourceCard, true);

                if (GManager.instance != null)
                {
                    GManager.instance.PlayDecisionSE();
                }
            }
        }
    }
}
