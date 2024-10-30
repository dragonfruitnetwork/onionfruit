// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Threading.Tasks;
using DragonFruit.OnionFruit.Rpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace DragonFruit.OnionFruit.Windows.Rpc
{
    public class OnionRpcService : OnionRpc.OnionRpcBase
    {
        public override Task<SecondInstanceLaunchedResponse> SecondInstanceLaunched(Empty request, ServerCallContext context)
        {
            App.Instance.ActivateApp();
            return Task.FromResult(new SecondInstanceLaunchedResponse
            {
                ShouldClose = true
            });
        }
    }
}