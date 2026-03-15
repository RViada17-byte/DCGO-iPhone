using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarterDeck : MonoBehaviour
{
    public List<StarterDeckData> starterDeckDatas = new List<StarterDeckData>();

    public bool SetStarterDecks()
    {
        bool changed = false;

        if (ContinuousController.instance.DeckDatas.Count == 0)
        {
            foreach (StarterDeckData starterDeckData in starterDeckDatas)
            {
                starterDeckData.AddDeckData();
                changed = true;
            }
        }

        else
        {
            foreach(StarterDeckData starterDeckData in starterDeckDatas)
            {
                if(!starterDeckData.HasPlayerPrefs())
                {
                    starterDeckData.AddDeckData();
                    changed = true;
                }
            }
        }

        return changed;
    }
}

[System.Serializable]
public class StarterDeckData
{
    public string Key;
    public string DeckCode;

    public void AddDeckData()
    {
        string deckCode = ContinuousController.instance.ShuffleDeckCode.GetDeckCode(DeckCode);
        ContinuousController.instance.DeckDatas.Add(new DeckData(deckCode));
        GameSaveManager.RecordStarterDeckGrant(Key);
    }

    public bool HasPlayerPrefs()
    {
        return GameSaveManager.HasStarterDeckGrant(Key, ContinuousController.instance);
    }
}
