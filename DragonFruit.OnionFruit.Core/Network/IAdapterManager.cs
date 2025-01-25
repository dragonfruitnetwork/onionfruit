// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using DragonFruit.OnionFruit.Core.Network;

namespace DragonFruit.OnionFruit.Core.Windows
{
    public interface IAdapterManager
    {
        DnsManagerState DnsState { get; }
        event EventHandler<string> AdapterChanged;
        void Init();
        INetworkAdapter GetAdapter(string id);
        IReadOnlyCollection<INetworkAdapter> GetAdapters();
    }
}