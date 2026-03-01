public static class MatchTransportFactory
{
    static readonly IMatchTransport _photon = new PhotonMatchTransport();
    static readonly IMatchTransport _localLoopback = new LocalLoopbackTransport();

    public static IMatchTransport CurrentTransport => GetTransport(BootstrapConfig.Mode);

    public static IMatchTransport GetTransport(GameMode mode)
    {
        if (mode == GameMode.OfflineLocal)
        {
            return _localLoopback;
        }

        return _photon;
    }
}
