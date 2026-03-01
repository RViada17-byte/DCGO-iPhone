using System.Collections;

public interface IMatchTransport
{
    GameMode Mode { get; }

    bool IsConnectedAndReady { get; }
    bool InLobby { get; }
    bool InRoom { get; }

    IEnumerator ConnectToMasterServer();
    IEnumerator ConnectToLobby();
    IEnumerator EnsureSoloRoom();
    IEnumerator Disconnect();
}
