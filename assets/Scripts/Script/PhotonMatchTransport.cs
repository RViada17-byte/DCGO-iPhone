using System.Collections;
using Photon.Pun;
using UnityEngine;

public class PhotonMatchTransport : IMatchTransport
{
    public GameMode Mode => GameMode.Online;

    public bool IsConnectedAndReady => PhotonNetwork.IsConnectedAndReady;
    public bool InLobby => PhotonNetwork.InLobby;
    public bool InRoom => PhotonNetwork.InRoom;

    public IEnumerator ConnectToMasterServer()
    {
        if (PhotonNetwork.OfflineMode || !PhotonNetwork.IsConnected || ContinuousController.instance.LastConnectServerRegion != ContinuousController.instance.serverRegion)
        {
            if (PhotonNetwork.IsConnected)
            {
                yield return ContinuousController.instance.StartCoroutine(Disconnect());
                yield return new WaitWhile(() => PhotonNetwork.IsConnected);
            }

            PhotonNetwork.OfflineMode = false;
            PhotonNetwork.NetworkingClient.AppId = PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime;
            PhotonNetwork.ConnectToRegion(ContinuousController.instance.serverRegion);
            PhotonNetwork.NickName = ContinuousController.instance.PlayerName;
            PhotonNetwork.GameVersion = ContinuousController.instance.GameVerString;
            ContinuousController.instance.LastConnectServerRegion = ContinuousController.instance.serverRegion;
        }

        yield return new WaitWhile(() => !PhotonNetwork.IsConnectedAndReady);
    }

    public IEnumerator ConnectToLobby()
    {
        yield return ContinuousController.instance.StartCoroutine(ConnectToMasterServer());

        if (!PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinLobby();
        }

        yield return new WaitUntil(() => PhotonNetwork.InLobby && PhotonNetwork.IsConnectedAndReady);
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

            string roomName = StringUtils.GeneratePassword_AlpahabetNum(50);
            PhotonNetwork.CreateRoom(roomName, roomOptions, null);
        }

        yield return new WaitWhile(() => !PhotonNetwork.InRoom);
    }

    public IEnumerator Disconnect()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        yield return new WaitWhile(() => PhotonNetwork.InRoom);

        if (PhotonNetwork.InLobby)
        {
            PhotonNetwork.LeaveLobby();
        }

        yield return new WaitWhile(() => PhotonNetwork.InLobby);

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        yield return new WaitWhile(() => PhotonNetwork.IsConnected);
    }
}
