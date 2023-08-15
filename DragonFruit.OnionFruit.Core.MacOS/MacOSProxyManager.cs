using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DragonFruit.OnionFruit.Core.MacOS.Native;
using DragonFruit.OnionFruit.Core.MacOS.Xpc;
using DragonFruit.OnionFruit.Core.Network;
using Foundation;

namespace DragonFruit.OnionFruit.Core.MacOS
{
    public class MacOSProxyManager : IProxyManager, IHasUserInteractionComponent, IDisposable
    {
        private readonly IntPtr? _serviceManagerHandle;

        private NSXpcConnection _xpcConnection;
        private Task<int> _xpcServiceVersion;

        public MacOSProxyManager()
        {
            if (NativeLibrary.GetApiVersion() != NativeLibrary.CurrentNativeApiVersion)
            {
                return;
            }

            // create install manager
            _serviceManagerHandle = NativeLibrary.CreateServiceManager();
        }

        public void OpenUserInteractionComponent()
        {
            NativeLibrary.OpenLoginItemsSettings();
        }

        public ProxyAccessState GetState()
        {
            if (!_serviceManagerHandle.HasValue)
            {
                return ProxyAccessState.ServiceFailure;
            }

            var serviceInstallState = NativeLibrary.GetServiceInstallState(_serviceManagerHandle.Value);

            // perform installation and use new result in switch below
            if (serviceInstallState == ServiceInstallState.NotRegistered)
            {
                // todo log native error codes for debugging
                NativeLibrary.PerformServiceInstallation(_serviceManagerHandle.Value, out serviceInstallState);
            }

            switch (serviceInstallState)
            {
                case ServiceInstallState.Enabled:
                    InitialiseXpcConnection();
                    return ProxyAccessState.Accessible;

                case ServiceInstallState.RequiresApproval:
                    return ProxyAccessState.PendingApproval;

                default:
                    return ProxyAccessState.ServiceFailure;
            }
        }

        public ValueTask<IEnumerable<NetworkProxy>> GetProxy()
        {
            // for now, proxyd won't return anything but will as development progresses
            return ValueTask.FromResult(Enumerable.Empty<NetworkProxy>());
        }

        public async ValueTask<bool> SetProxy(params NetworkProxy[] proxies)
        {
            var protocol = await EnsureXpcReady().ConfigureAwait(false);
            var tcs = new TaskCompletionSource<bool>();

            // handle clearing
            if (!proxies.Any() || proxies.All(x => !x.Enabled))
            {
                protocol.ClearProxy(s => tcs.TrySetResult(s));
                return await tcs.Task.ConfigureAwait(false);
            }

            bool? globalState = null;
            var proxyUrlBuilder = new StringBuilder();

            foreach (var proxy in proxies)
            {
                // set globalState if not set
                globalState ??= proxy.Enabled;

                if (globalState != proxy.Enabled)
                {
                    throw new ArgumentException($"Enabled state is not mutually exclusive (expected {globalState}, got {proxy.Enabled}");
                }

                proxyUrlBuilder.Append($"{proxy.Address.Scheme}={proxy.Address.IdnHost}:{proxy.Address.Port};");
            }

            if (!globalState.HasValue)
            {
                throw new ArgumentException("At least one proxy is required to change settings");
            }

            protocol.SetProxy(proxyUrlBuilder.ToString().TrimEnd(';'), s => tcs.TrySetResult(s));
            return await tcs.Task.ConfigureAwait(false);
        }

        public void InitialiseXpcConnection()
        {
            // create xpc server instance
            _xpcConnection = new NSXpcConnection("network.dragonfruit.proxyd", NSXpcConnectionOptions.Privileged);
            _xpcConnection.RemoteInterface = NSXpcInterface.Create(typeof(IXpcProtocol));

            // start connection
            _xpcConnection.Resume();

            // get api version and set result
            var task = new TaskCompletionSource<int>();
            var proxy = _xpcConnection.CreateRemoteObjectProxy<IXpcProtocol>();

            _xpcServiceVersion = task.Task;
            proxy.GetApiVersion(v => task.TrySetResult(v));
        }

        private async Task<IXpcProtocol> EnsureXpcReady(int minXpcVersion = 1)
        {
            if (_xpcConnection == null || _xpcServiceVersion == null)
            {
                throw new InvalidOperationException("XPC service has not been started.");
            }

            var xpcVersion = await _xpcServiceVersion.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            if (xpcVersion < minXpcVersion)
            {
                throw new Exception(
                    $"The current XPC server version is too low (min: {minXpcVersion}, current: {xpcVersion})");
            }

            return _xpcConnection.CreateRemoteObjectProxy<IXpcProtocol>();
        }

        public void Dispose()
        {
            _xpcConnection?.Invalidate();

            _xpcConnection?.Dispose();
            _xpcServiceVersion?.Dispose();

            if (_serviceManagerHandle.HasValue)
            {
                NativeLibrary.CleanupServiceManager(_serviceManagerHandle.Value);
            }
        }
    }
}