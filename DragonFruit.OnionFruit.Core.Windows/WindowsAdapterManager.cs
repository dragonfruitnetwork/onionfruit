// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Principal;
using System.Threading;
using DragonFruit.OnionFruit.Core.Network;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Generic;
using Microsoft.Management.Infrastructure.Options;
using Microsoft.Win32;

namespace DragonFruit.OnionFruit.Core.Windows
{
    public class WindowsAdapterManager : IDisposable, IAdapterManager
    {
        private CimSession _cimSession;
        private CancellationTokenSource _cimWatcherCancellation;
        private CimAsyncMultipleResults<CimSubscriptionResult> _cimSessionWatcher;

        private const string AdapterQuery = "SELECT Description,SettingID FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = 'TRUE'";

        // {0} - IPv4 = string.Empty, IPv6 = "6"
        // {1} - Adapter Id (found in SettingID of WMI query)
        private const string DnsRegistryPath = @"SYSTEM\CurrentControlSet\Services\Tcpip{0}\Parameters\Interfaces\{1}";

        public DnsManagerState DnsState { get; private set; }

        public event EventHandler<string> AdapterChanged;

        public void Init()
        {
            _cimSession = CimSession.Create(null);

            if (!_cimSession.TestConnection())
            {
                DnsState = DnsManagerState.Unavailable;
                return;
            }

            if (!CheckUserPermissions())
            {
                DnsState = DnsManagerState.MissingPermissions;
                return;
            }

            _cimWatcherCancellation = new CancellationTokenSource();
            _cimSessionWatcher = _cimSession.SubscribeAsync(@"root\cmv2", "WQL", AdapterQuery, new CimOperationOptions
            {
                CancellationToken = _cimWatcherCancellation.Token
            });

            _cimSessionWatcher.Subscribe(new WimNetworkAdapterSettingsObserver(this));
        }

        public INetworkAdapter GetAdapter(string id)
        {
            var adapter = _cimSession.QueryInstances(@"root\cmv2", "WQL", $"{AdapterQuery} AND SettingID = '{id}'").SingleOrDefault();
            return adapter == null ? null : FromCimInstance(adapter);
        }

        public IReadOnlyCollection<INetworkAdapter> GetAdapters()
        {
            if (DnsState != DnsManagerState.Available)
            {
                return [];
            }

            var results = _cimSession.QueryInstances(@"root\cmv2", "WQL", AdapterQuery);
            return results.Select(FromCimInstance).ToList();
        }

        public void Dispose()
        {
            _cimSession?.Dispose();
            _cimWatcherCancellation?.Dispose();
        }

        private static WindowsNetworkAdapter FromCimInstance(CimInstance instance)
        {
            var id = instance.CimInstanceProperties["SettingID"].Value.ToString();
            var name = instance.CimInstanceProperties["Description"].Value.ToString();

            var tcpipKey = Socket.OSSupportsIPv4 ? Registry.LocalMachine.OpenSubKey(string.Format(DnsRegistryPath, string.Empty, id), true) : null;
            var tcpip6Key = Socket.OSSupportsIPv6 ? Registry.LocalMachine.OpenSubKey(string.Format(DnsRegistryPath, "6", id), true) : null;

            return new WindowsNetworkAdapter(id, name, tcpipKey, tcpip6Key);
        }

        private static bool CheckUserPermissions()
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private class WimNetworkAdapterSettingsObserver(WindowsAdapterManager instance) : IObserver<CimSubscriptionResult>
        {
            public void OnCompleted()
            {
                // do nothing
            }

            public void OnError(Exception error)
            {
                // do nothing
            }

            public void OnNext(CimSubscriptionResult value)
            {
                using (value)
                {
                    if (value.Instance == null)
                    {
                        return;
                    }

                    var id = value.Instance.CimInstanceProperties["SettingID"].Value.ToString();
                    instance.AdapterChanged?.Invoke(this, id);
                }
            }
        }
    }
}