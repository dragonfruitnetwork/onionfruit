// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

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