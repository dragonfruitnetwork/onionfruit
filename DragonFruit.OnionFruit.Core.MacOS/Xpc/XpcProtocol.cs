// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using Foundation;
using ObjCRuntime;

namespace DragonFruit.OnionFruit.Core.MacOS.Xpc
{
    [Preserve]
    [XpcInterface]
    [Protocol(Name = "RpcProtocol")]
    public interface IXpcProtocol : INativeObject
    {
        [Export("getVersion:")]
        void GetApiVersion(ApiVersionCallback callback);

        [Export("clearProxies:")]
        void ClearProxy(ProxyUpdateCallback callback);

        [Export("setProxy:reply:")]
        void SetProxy(string url, ProxyUpdateCallback callback);
    }
}