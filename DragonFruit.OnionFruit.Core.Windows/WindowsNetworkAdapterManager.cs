// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.Sockets;
using System.Security;
using System.Security.AccessControl;
using DragonFruit.OnionFruit.Core.Network;
using Microsoft.Win32;

namespace DragonFruit.OnionFruit.Core.Windows
{
    public class WindowsNetworkAdapterManager : IDisposable, INetworkAdapterManager
    {
        private const string WmiNamespace = @"root\cimv2";
        private const string WmiNetworkAdapterQuery = "SELECT Description,SettingID FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = 'TRUE'";
        private const string WmiNetworkAdapterEventQuery = "SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_NetworkAdapterConfiguration' AND TargetInstance.IPEnabled = TRUE";

        private const string DnsRegistryPath = @"SYSTEM\CurrentControlSet\Services\Tcpip{0}\Parameters\Interfaces";
        private const string ProxySettingsPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        private const RegistryRights RequiredRegistryAccess = RegistryRights.ReadPermissions
                                                              | RegistryRights.ReadKey
                                                              | RegistryRights.WriteKey
                                                              | RegistryRights.SetValue
                                                              | RegistryRights.Delete;

        private ManagementEventWatcher _wmiEventWatcher;
        private RegistryKey _proxyRegistry, _tcpipConfigRegistry, _tcpip6ConfigRegistry;

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

            _wmiEventWatcher = new ManagementEventWatcher(WmiNamespace, WmiNetworkAdapterEventQuery);
            _wmiEventWatcher.EventArrived += WmiEventHandler;
            _wmiEventWatcher.Start();
        }

        public INetworkAdapter GetAdapter(string id)
        {
            using var objectSearcher = new ManagementObjectSearcher(new ManagementScope(WmiNamespace), new WqlObjectQuery($"{WmiNetworkAdapterQuery} AND SettingID = '{id}'"));
            var adapters = objectSearcher.Get();

            switch (adapters.Count)
            {
                case 0:
                {
                    return null;
                }

                case 1:
                {
                    var adapter = adapters.Cast<ManagementObject>().SingleOrDefault();
                    return adapter == null ? null : AdapterFromCimInstance(adapter);
                }

                default:
                {
                    throw new InvalidOperationException($"Multiple adapters found with SettingID '{id}'.");
                }
            }
        }

        public IList<INetworkAdapter> GetAdapters()
        {
            List<INetworkAdapter> adapters = [new WinGlobalProxyAdapter(_proxyRegistry, false)];

            using var objectSearcher = new ManagementObjectSearcher(new ManagementScope(WmiNamespace), new WqlObjectQuery(WmiNetworkAdapterQuery));
            using var results = objectSearcher.Get();

            if (results == null || results.Count == 0)
            {
                return adapters;
            }

            adapters.AddRange(results.Cast<ManagementObject>().Select(AdapterFromCimInstance));
            return adapters;
        }

        private void WmiEventHandler(object sender, EventArrivedEventArgs e)
        {
            if (e.NewEvent == null)
            {
                return;
            }

            using (e.NewEvent)
            {
                var newAdapter = (ManagementBaseObject)e.NewEvent["TargetInstance"];

                var id = newAdapter["SettingID"].ToString();
                var name = newAdapter["Description"].ToString();

                AdapterConnected?.Invoke(this, new NetworkAdapterInfo(id, name, true));
            }
        }

        public void Dispose()
        {
            _wmiEventWatcher?.Stop();
            _wmiEventWatcher?.Dispose();

            _proxyRegistry?.Dispose();
            _tcpipConfigRegistry?.Dispose();
            _tcpip6ConfigRegistry?.Dispose();

            GC.SuppressFinalize(this);
        }

        private WinNetworkAdapter AdapterFromCimInstance(ManagementObject instance)
        {
            var id = instance["SettingID"].ToString();
            var name = instance["Description"].ToString();

            var tcpipKey = Socket.OSSupportsIPv4 ? _tcpipConfigRegistry?.OpenSubKey(id, RequiredRegistryAccess) : null;
            var tcpip6Key = Socket.OSSupportsIPv6 ? _tcpip6ConfigRegistry?.OpenSubKey(id, RequiredRegistryAccess) : null;

            return new WinNetworkAdapter(id, name, !string.IsNullOrEmpty(name), tcpipKey, tcpip6Key);
        }
    }
}