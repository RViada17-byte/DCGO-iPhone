using System;
using System.Collections;
using Photon.Pun;
using UnityEngine;

public class LocalLoopbackTransport : IMatchTransport
{
    public GameMode Mode => GameMode.OfflineLocal;

    public bool IsConnectedAndReady => PhotonNetwork.IsConnectedAndReady;
    public bool InLobby => PhotonNetwork.InLobby;
    public bool InRoom => PhotonNetwork.InRoom;

    static IEnumerator WaitForState(Func<bool> condition, float timeoutSeconds, string context)
    {
        float elapsed = 0f;

        while (!condition())
        {
            if (elapsed >= timeoutSeconds)
            {
                Debug.LogWarning($"[LocalLoopbackTransport] Timeout while waiting for {context}. Continuing with fallback.");
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public IEnumerator ConnectToMasterServer()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            yield return WaitForState(() => !PhotonNetwork.InRoom, 6f, "leave room");
        }

        if (PhotonNetwork.InLobby)
        {
            PhotonNetwork.LeaveLobby();
            yield return WaitForState(() => !PhotonNetwork.InLobby, 6f, "leave lobby");
        }

        if (PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.Disconnect();
            yield return WaitForState(() => !PhotonNetwork.IsConnected, 8f, "disconnect online session");
        }

        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.NickName = ContinuousController.instance.PlayerName;
        PhotonNetwork.GameVersion = ContinuousController.instance.GameVerString;

        // In offline mode, Photon can operate without a real server connection.
        if (!PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.ConnectUsingSettings();
        }

        // Keep startup responsive on mobile by avoiding long waits for online-ready states.
        yield return WaitForState(() => PhotonNetwork.IsConnectedAndReady || PhotonNetwork.OfflineMode, 0.75f, "offline connect ready");
    }

    public IEnumerator ConnectToLobby()
    {
        yield return ContinuousController.instance.StartCoroutine(ConnectToMasterServer());

        if (PhotonNetwork.OfflineMode)
        {
            yield break;
        }

        if (!PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinLobby();
        }

        yield return WaitForState(() => PhotonNetwork.InLobby && PhotonNetwork.IsConnectedAndReady, 2f, "join lobby");
    }

    public IEnumerator EnsureSoloRoom()
    {
        yield return ContinuousController.instance.StartCoroutine(ConnectToLobby());

        if (!PhotonNetwork.InRoom)
        {
            var roomOptions = new Photon.Realtime.RoomOptions
            {
                IsVisible = false,
                IsOpen = false,
                PublishUserId = true,
                MaxPlayers = 1,
            };

            string roomName = $"offline-{Guid.NewGuid():N}";
            PhotonNetwork.CreateRoom(roomName, roomOptions, null);
        }

        yield return WaitForState(() => PhotonNetwork.InRoom, 2f, "create/join solo room");

        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[LocalLoopbackTransport] Solo room was not established. Proceeding in offline fallback mode.");
        }
    }

    public IEnumerator Disconnect()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        yield return WaitForState(() => !PhotonNetwork.InRoom, 6f, "leave room on disconnect");

        if (PhotonNetwork.InLobby)
        {
            PhotonNetwork.LeaveLobby();
        }

        yield return WaitForState(() => !PhotonNetwork.InLobby, 6f, "leave lobby on disconnect");

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        yield return WaitForState(() => !PhotonNetwork.IsConnected, 8f, "disconnect transport");

        PhotonNetwork.OfflineMode = false;
    }
}
