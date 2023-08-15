// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;

namespace DragonFruit.OnionFruit.Core.Network
{
    public record NetworkProxy(bool Enabled, Uri Address);
}