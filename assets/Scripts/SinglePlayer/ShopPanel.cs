using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ShopPanel : MonoBehaviour
{
    private const int PackCost = 1000;

    [Header("UI Refs")]
    [SerializeField] private Text CurrencyText;
    [SerializeField] private Transform PackButtonParent;
    [SerializeField] private Button PackButtonPrefab;
    [SerializeField] private Text LastOpenResultsText;

    private bool _packButtonsBuilt;
    private readonly List<Button> _runtimeButtons = new List<Button>();

    private void OnEnable()
    {
        ProgressionManager.Instance.LoadOrCreate();
        RefreshCurrency();
        BuildPackButtonsIfNeeded();
    }

    public void RefreshCurrency()
    {
        if (CurrencyText == null)
        {
            return;
        }

        CurrencyText.text = "$ " + ProgressionManager.Instance.GetCurrency();
    }

    public void BuildPackButtonsIfNeeded()
    {
        if (_packButtonsBuilt)
        {
            return;
        }

        if (PackButtonParent == null || PackButtonPrefab == null)
        {
            return;
        }

        foreach (string setId in PackService.DefaultPackSetIds)
        {
            string capturedSetId = setId;
            Button button = Instantiate(PackButtonPrefab, PackButtonParent);
            button.name = $"PackButton_{capturedSetId}";

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = capturedSetId;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => TryBuyAndOpen(capturedSetId));
            _runtimeButtons.Add(button);
        }

        _packButtonsBuilt = true;
    }

    public void TryBuyAndOpen(string setId)
    {
        if (string.IsNullOrWhiteSpace(setId))
        {
            return;
        }

        if (!ProgressionManager.Instance.TrySpendCurrency(PackCost))
        {
            SetResultsText("Not enough currency");
            RefreshCurrency();
            return;
        }

        List<CEntity_Base> pulls = PackService.OpenPack(setId);

        for (int i = 0; i < pulls.Count; i++)
        {
            CEntity_Base card = pulls[i];
            if (card != null)
            {
                ProgressionManager.Instance.UnlockCard(card.CardID);
            }
        }

        ProgressionManager.Instance.Save();
        RefreshCurrency();
        SetResultsText(BuildPullResults(setId, pulls));
    }

    private static string BuildPullResults(string setId, List<CEntity_Base> pulls)
    {
        if (pulls == null || pulls.Count == 0)
        {
            return $"Opened {setId} (no cards)";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Opened {setId}:");

        for (int i = 0; i < pulls.Count; i++)
        {
            CEntity_Base card = pulls[i];
            if (card == null)
            {
                continue;
            }

            string cardName = !string.IsNullOrWhiteSpace(card.CardName_ENG) ? card.CardName_ENG : card.CardID;
            builder.AppendLine($"{i + 1}. {card.CardID} - {cardName}");
        }

        return builder.ToString().TrimEnd();
    }

    private void SetResultsText(string message)
    {
        if (LastOpenResultsText == null)
        {
            return;
        }

        LastOpenResultsText.text = message;
    }
}
