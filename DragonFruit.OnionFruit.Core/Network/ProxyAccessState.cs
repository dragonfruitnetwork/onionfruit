namespace DragonFruit.OnionFruit.Core.Network
{
    public enum ProxyAccessState
    {
        Accessible,
        BlockedByUser,
        BlockedBySystem,
        PendingApproval,
        ServiceInvalid,
        ServiceFailure
    }
}