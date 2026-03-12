using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class OpeningOfflineMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    Button _button;
    OpeningButton _openingButton;

    void Awake()
    {
        CacheReferences();
    }

    void OnEnable()
    {
        CacheReferences();

        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
        }
    }

    void OnDisable()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
        }

        SetHighlighted(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHighlighted(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighlighted(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        SetHighlighted(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetHighlighted(false);
    }

    void CacheReferences()
    {
        _button = GetComponent<Button>();
        _openingButton = GetComponent<OpeningButton>();

        if (_openingButton == null && transform.parent != null)
        {
            _openingButton = transform.parent.GetComponent<OpeningButton>();
        }
    }

    void SetHighlighted(bool isHighlighted)
    {
        if (_openingButton == null)
        {
            return;
        }

        if (isHighlighted)
        {
            _openingButton.OnSelect();
        }
        else
        {
            _openingButton.OnExit();
        }
    }

    void HandleClick()
    {
        MainMenuRouter router = ResolveRouter();
        Opening opening = Opening.instance;
        string actionName = ResolveActionName();

        switch (actionName)
        {
            case "BattleButton":
                if (router != null)
                {
                    router.OpenWorld();
                }
                break;

            case "DeckButton":
                if (router != null)
                {
                    router.OpenDeck();
                }
                else if (opening != null && opening.deck != null)
                {
                    opening.deck.SetUpDeckMode();
                }
                break;

            case "ConfigButton":
                if (router != null)
                {
                    router.OpenShop();
                }
                break;

            case "StoryModeButton":
                if (router != null)
                {
                    router.OpenStory();
                }
                break;

            case "DuelistBoardButton":
                if (router != null)
                {
                    router.OpenDuelistBoard();
                }
                break;

            case "BackButton":
                if (router != null)
                {
                    router.BackToHome();
                }
                break;
        }
    }

    MainMenuRouter ResolveRouter()
    {
        if (Opening.instance != null)
        {
            MainMenuRouter router = Opening.instance.GetComponent<MainMenuRouter>();
            if (router != null)
            {
                return router;
            }
        }

        return FindObjectOfType<MainMenuRouter>();
    }

    string ResolveActionName()
    {
        if (gameObject.name == "Button" && transform.parent != null)
        {
            return transform.parent.name;
        }

        return gameObject.name;
    }
}
