// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security;
using System.Security.AccessControl;
using System.Threading;
using DragonFruit.OnionFruit.Core.Network;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Generic;
using Microsoft.Management.Infrastructure.Options;
using Microsoft.Win32;

namespace DragonFruit.OnionFruit.Core.Windows
{
    public class WindowsNetworkAdapterManager : IDisposable, INetworkAdapterManager
    {
        private RegistryKey _proxyRegistry, _tcpipConfigRegistry, _tcpip6ConfigRegistry;

        private CimSession _cimSession;
        private CancellationTokenSource _cimWatcherCancellation;
        private CimAsyncMultipleResults<CimSubscriptionResult> _cimSessionWatcher;

        private const string CimNamespace = @"root\cimv2";
        private const string CimNetworkAdapterQuery = "SELECT Description,SettingID FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = 'TRUE'";

        private const string DnsRegistryPath = @"SYSTEM\CurrentControlSet\Services\Tcpip{0}\Parameters\Interfaces";
        private const string ProxySettingsPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        private const RegistryRights RequiredRegistryAccess = RegistryRights.ReadPermissions
                                                              | RegistryRights.ReadKey
                                                              | RegistryRights.WriteKey
                                                              | RegistryRights.SetValue
                                                              | RegistryRights.Delete;

        public NetworkComponentState DnsState => _tcpipConfigRegistry == null && _tcpip6ConfigRegistry == null ? NetworkComponentState.MissingPermissions : NetworkComponentState.Available;
        public NetworkComponentState ProxyState => _proxyRegistry == null ? NetworkComponentState.MissingPermissions : NetworkComponentState.Available;

        public event EventHandler<NetworkAdapterInfo> AdapterConnected;

        public void Init()
        {
            try
            {
                _proxyRegistry = Registry.CurrentUser.OpenSubKey(ProxySettingsPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RequiredRegistryAccess);
            }
            catch (SecurityException)
            {
            }

            try
            {
                _tcpipConfigRegistry = Registry.LocalMachine.OpenSubKey(string.Format(DnsRegistryPath, string.Empty), RegistryKeyPermissionCheck.ReadWriteSubTree, RequiredRegistryAccess);
                _tcpip6ConfigRegistry = Registry.LocalMachine.OpenSubKey(string.Format(DnsRegistryPath, "6"), RegistryKeyPermissionCheck.ReadWriteSubTree, RequiredRegistryAccess);
            }
            catch (SecurityException)
            {
            }

            _cimSession = CimSession.Create(null);

            _cimWatcherCancellation = new CancellationTokenSource();
            _cimSessionWatcher = _cimSession.SubscribeAsync(CimNamespace, "WQL", CimNetworkAdapterQuery, new CimOperationOptions
            {
                CancellationToken = _cimWatcherCancellation.Token
            });

            _cimSessionWatcher.Subscribe(new WimNetworkAdapterSettingsObserver(this));
        }

        public INetworkAdapter GetAdapter(string id)
        {
            var cimAdapter = _cimSession.QueryInstances(CimNamespace, "WQL", $"{CimNetworkAdapterQuery} AND SettingID = '{id}'").SingleOrDefault();
            return cimAdapter == null ? null : AdapterFromCimInstance(cimAdapter);
        }

        public IList<INetworkAdapter> GetAdapters()
        {
            List<INetworkAdapter> adapters = [new WinGlobalProxyAdapter(_proxyRegistry, false)];

            var results = _cimSession?.QueryInstances(CimNamespace, "WQL", CimNetworkAdapterQuery);
            if (results != null)
            {
                adapters.AddRange(results.Select(AdapterFromCimInstance));
            }

            return adapters;
        }

        public void Dispose()
        {
            _cimWatcherCancellation?.Cancel();
            _cimSession?.Dispose();

            _proxyRegistry?.Dispose();
            _tcpipConfigRegistry?.Dispose();
            _tcpip6ConfigRegistry?.Dispose();

            GC.SuppressFinalize(this);
        }

        private WinNetworkAdapter AdapterFromCimInstance(CimInstance instance)
        {
            var id = instance.CimInstanceProperties["SettingID"].Value.ToString();
            var name = instance.CimInstanceProperties["Description"].Value.ToString();

            var tcpipKey = Socket.OSSupportsIPv4 ? _tcpipConfigRegistry?.OpenSubKey(id, RequiredRegistryAccess) : null;
            var tcpip6Key = Socket.OSSupportsIPv6 ? _tcpip6ConfigRegistry?.OpenSubKey(id, RequiredRegistryAccess) : null;

            return new WinNetworkAdapter(id, name, !string.IsNullOrEmpty(name), tcpipKey, tcpip6Key);
        }

        private class WimNetworkAdapterSettingsObserver(WindowsNetworkAdapterManager instance) : IObserver<CimSubscriptionResult>
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
                    var name = value.Instance.CimInstanceProperties["Description"].Value.ToString();

                    instance.AdapterConnected?.Invoke(this, new NetworkAdapterInfo(id, name, true));
                }
            }
        }
    }
}