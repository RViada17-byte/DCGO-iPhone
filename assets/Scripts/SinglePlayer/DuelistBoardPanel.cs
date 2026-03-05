using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DuelistBoardPanel : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Transform DuelButtonParent;
    [SerializeField] private Button DuelButtonPrefab;
    [SerializeField] private Text CurrencyText;
    [SerializeField] private Text InfoText;

    private void OnEnable()
    {
        ProgressionManager.Instance.LoadOrCreate();
        RefreshCurrency();
        BuildDuelButtons();
    }

    public void RefreshCurrency()
    {
        if (CurrencyText == null)
        {
            return;
        }

        CurrencyText.text = "$ " + ProgressionManager.Instance.GetCurrency();
    }

    public void BuildDuelButtons()
    {
        if (DuelButtonParent == null || DuelButtonPrefab == null)
        {
            return;
        }

        for (int i = DuelButtonParent.childCount - 1; i >= 0; i--)
        {
            Destroy(DuelButtonParent.GetChild(i).gameObject);
        }

        IReadOnlyList<DuelBoardDuelDef> duels = DuelBoardDatabase.Instance.Duels;
        if (duels == null || duels.Count == 0)
        {
            SetInfoText("No duelist board duels found.");
            return;
        }

        for (int i = 0; i < duels.Count; i++)
        {
            DuelBoardDuelDef duel = duels[i];
            if (duel == null)
            {
                continue;
            }

            bool locked = IsDuelLocked(duel);
            bool completed = ProgressionManager.Instance.IsBoardCompleted(duel.id);

            Button button = Instantiate(DuelButtonPrefab, DuelButtonParent);
            button.name = $"DuelBoardButton_{duel.id}";

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = BuildDuelLabel(duel, locked, completed);
            }

            button.interactable = !locked;
            button.onClick.RemoveAllListeners();

            if (!locked)
            {
                DuelBoardDuelDef capturedDuel = duel;
                button.onClick.AddListener(() => StartBoardDuel(capturedDuel));
            }
        }
    }

    public void StartBoardDuel(DuelBoardDuelDef duel)
    {
        if (duel == null || string.IsNullOrWhiteSpace(duel.enemyDeckCode))
        {
            SetInfoText("Invalid board duel.");
            return;
        }

        ContinuousController.instance.isAI = true;
        ContinuousController.instance.isRandomMatch = false;
        ContinuousController.instance.EnemyDeckData = new DeckData(duel.enemyDeckCode);

        GameSessionContext.Instance.StartSession(
            SessionMode.DuelistBoard,
            duel.id,
            duel.rewardCurrency,
            duel.rewardPromoCardId,
            duel.promoOneTime);

        StartCoroutine(SinglePlayerBattleLoader.LoadBattleSceneAdditiveCoroutine());
    }

    private bool IsDuelLocked(DuelBoardDuelDef duel)
    {
        if (duel == null || duel.prereqStoryNodeIds == null || duel.prereqStoryNodeIds.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < duel.prereqStoryNodeIds.Length; i++)
        {
            string prereqId = duel.prereqStoryNodeIds[i];
            if (string.IsNullOrWhiteSpace(prereqId))
            {
                continue;
            }

            if (!ProgressionManager.Instance.IsStoryCompleted(prereqId))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildDuelLabel(DuelBoardDuelDef duel, bool locked, bool completed)
    {
        string prefix = string.Empty;

        if (locked)
        {
            prefix = "[LOCKED] ";
        }
        else if (completed)
        {
            prefix = "[DONE] ";
        }

        return prefix + (string.IsNullOrWhiteSpace(duel.title) ? duel.id : duel.title);
    }

    private void SetInfoText(string message)
    {
        if (InfoText == null)
        {
            return;
        }

        InfoText.text = message ?? string.Empty;
    }
}
