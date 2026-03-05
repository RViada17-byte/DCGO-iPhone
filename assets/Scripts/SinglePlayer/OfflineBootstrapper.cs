using Photon.Pun;
using UnityEngine;

public sealed class OfflineBootstrapper : MonoBehaviour
{
    private const string OfflineRoomName = "OFFLINE";
    private static bool _logged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBootstrapper()
    {
        if (FindObjectOfType<OfflineBootstrapper>() != null)
        {
            return;
        }

        var go = new GameObject(nameof(OfflineBootstrapper));
        DontDestroyOnLoad(go);
        go.AddComponent<OfflineBootstrapper>();
    }

    public static void EnsureOfflineReady()
    {
        if (FindObjectOfType<OfflineBootstrapper>() == null)
        {
            CreateBootstrapper();
            return;
        }

        PhotonNetwork.OfflineMode = true;

        if (!_logged)
        {
            Debug.Log("OfflineBootstrapper: Photon OfflineMode enabled");
            _logged = true;
        }

        if (PhotonNetwork.InRoom)
        {
            return;
        }

        if (!PhotonNetwork.CreateRoom(OfflineRoomName))
        {
            PhotonNetwork.JoinRoom(OfflineRoomName);
        }
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        EnsureOfflineReady();
    }
}
