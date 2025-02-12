// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Threading.Tasks;
using DragonFruit.OnionFruit.Rpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace DragonFruit.OnionFruit.Windows.Rpc
{
    public class OnionRpcService(WindowsAppInstanceManager appInstanceManager) : OnionRpc.OnionRpcBase
    {
        public override Task<SecondInstanceLaunchedResponse> SecondInstanceLaunched(Empty request, ServerCallContext context)
        {
            // if the old instance launched a new one, we should close
            if (appInstanceManager.ReplacementProcess?.HasExited == false)
            {
                return Task.FromResult(new SecondInstanceLaunchedResponse
                {
                    ShouldClose = false,
                    WaitForPidExit = appInstanceManager.CurrentProcess.Id
                });
            }

            App.Instance.ActivateApp();
            return Task.FromResult(new SecondInstanceLaunchedResponse
            {
                ShouldClose = true
            });
        }
    }
}