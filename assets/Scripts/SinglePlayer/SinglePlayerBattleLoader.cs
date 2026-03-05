using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SinglePlayerBattleLoader
{
    public static IEnumerator LoadBattleSceneAdditiveCoroutine()
    {
        Opening opening = Opening.instance;
        if (opening == null)
        {
            yield break;
        }

        ContinuousController controller = ContinuousController.instance;

        if (opening.OpeningBGM != null)
        {
            if (controller != null)
            {
                controller.StartCoroutine(opening.OpeningBGM.FadeOut(0.1f));
            }
            else
            {
                yield return opening.OpeningBGM.FadeOut(0.1f);
            }
        }

        if (opening.LoadingObject != null)
        {
            if (controller != null)
            {
                yield return controller.StartCoroutine(opening.LoadingObject.StartLoading("Now Loading"));
            }
            else
            {
                yield return opening.LoadingObject.StartLoading("Now Loading");
            }
        }

        if (controller != null && controller.isAI)
        {
            yield return controller.StartCoroutine(MatchTransportFactory.CurrentTransport.EnsureSoloRoom());
        }

        if (opening.openingCameras != null)
        {
            foreach (Camera camera in opening.openingCameras)
            {
                if (camera != null)
                {
                    camera.gameObject.SetActive(false);
                }
            }
        }

        opening.OffYesNoObjects();

        if (opening.deck != null)
        {
            if (opening.deck.trialDraw != null)
            {
                opening.deck.trialDraw.Close();
            }

            if (opening.deck.deckListPanel != null)
            {
                opening.deck.deckListPanel.Close();
            }
        }

        yield return new WaitForSeconds(0.1f);
        SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Additive);
    }
}
