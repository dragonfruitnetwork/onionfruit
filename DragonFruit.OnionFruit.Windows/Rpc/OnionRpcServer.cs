// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DragonFruit.OnionFruit.Rpc;
using GrpcDotNetNamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DragonFruit.OnionFruit.Windows.Rpc
{
    /// <summary>
    /// Manages the gRPC server responsible for accepting and forwarding <see cref="OnionRpcService"/> requests
    /// </summary>
    public class OnionRpcServer(IServiceProvider provider) : IHostedService
    {
        /// <summary>
        /// Gets the name of the RPC pipe for the current user
        /// </summary>
        internal static string RpcPipeName = $"onionfruit-rpc-{Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes(Environment.UserName))).ToLowerInvariant()}";

        private NamedPipeServer _grpcServer;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var securityPolicy = new PipeSecurity();

            securityPolicy.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
            securityPolicy.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));

            // deny remote access
            securityPolicy.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));

            // CurrentUserOnly won't work when running as admin vs non-admin
            // use SecurityPolicy to protect the RPC instance instead
            _grpcServer = new NamedPipeServer(RpcPipeName, new NamedPipeServerOptions
            {
                PipeSecurity = securityPolicy
            });

            OnionRpc.BindService(_grpcServer.ServiceBinder, ActivatorUtilities.CreateInstance<OnionRpcService>(provider));
            _grpcServer.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _grpcServer.Kill();
            _grpcServer.Dispose();

            _grpcServer = null;

            return Task.CompletedTask;
        }
    }
}