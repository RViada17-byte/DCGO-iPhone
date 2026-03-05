using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StoryPanel : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Transform NodeButtonParent;
    [SerializeField] private Button NodeButtonPrefab;
    [SerializeField] private Text CurrencyText;
    [SerializeField] private Text InfoText;

    private void OnEnable()
    {
        ProgressionManager.Instance.LoadOrCreate();
        RefreshCurrency();
        BuildNodeButtons();
    }

    public void RefreshCurrency()
    {
        if (CurrencyText == null)
        {
            return;
        }

        CurrencyText.text = "$ " + ProgressionManager.Instance.GetCurrency();
    }

    public void BuildNodeButtons()
    {
        if (NodeButtonParent == null || NodeButtonPrefab == null)
        {
            return;
        }

        for (int i = NodeButtonParent.childCount - 1; i >= 0; i--)
        {
            Destroy(NodeButtonParent.GetChild(i).gameObject);
        }

        IReadOnlyList<StoryNodeDef> nodes = StoryDatabase.Instance.Nodes;
        if (nodes == null || nodes.Count == 0)
        {
            SetInfoText("No story nodes found.");
            return;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            StoryNodeDef node = nodes[i];
            if (node == null)
            {
                continue;
            }

            bool locked = IsNodeLocked(node);
            bool completed = ProgressionManager.Instance.IsStoryCompleted(node.id);

            Button button = Instantiate(NodeButtonPrefab, NodeButtonParent);
            button.name = $"StoryNodeButton_{node.id}";

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = BuildNodeLabel(node, locked, completed);
            }

            button.interactable = !locked;
            button.onClick.RemoveAllListeners();

            if (!locked)
            {
                StoryNodeDef capturedNode = node;
                button.onClick.AddListener(() => StartStoryDuel(capturedNode));
            }
        }
    }

    public void StartStoryDuel(StoryNodeDef node)
    {
        if (node == null || string.IsNullOrWhiteSpace(node.enemyDeckCode))
        {
            SetInfoText("Invalid story node.");
            return;
        }

        DeckData enemyDeck = new DeckData(node.enemyDeckCode);

        ContinuousController.instance.isAI = true;
        ContinuousController.instance.isRandomMatch = false;
        ContinuousController.instance.EnemyDeckData = enemyDeck;

        GameSessionContext.Instance.StartSession(
            SessionMode.Story,
            node.id,
            node.rewardCurrency,
            node.rewardPromoCardId,
            promoOneTime: true);

        StartCoroutine(SinglePlayerBattleLoader.LoadBattleSceneAdditiveCoroutine());
    }

    private bool IsNodeLocked(StoryNodeDef node)
    {
        if (node == null || node.prereqNodeIds == null || node.prereqNodeIds.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < node.prereqNodeIds.Length; i++)
        {
            string prereqId = node.prereqNodeIds[i];
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

    private static string BuildNodeLabel(StoryNodeDef node, bool locked, bool completed)
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

        return prefix + (string.IsNullOrWhiteSpace(node.title) ? node.id : node.title);
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
