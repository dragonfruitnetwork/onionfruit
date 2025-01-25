// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Win32;
using DragonFruit.OnionFruit.Core.Network;
using Microsoft.Win32;

namespace DragonFruit.OnionFruit.Core.Windows
{
    public partial class WindowsProxyManager : IProxyManager, IDisposable
    {
        private const string ProxyEnabledKey = "ProxyEnable";
        private const string ProxyServerKey = "ProxyServer";

        private const int InternetOptionSettingsChanged = 0x27;
        private const int InternetOptionRefresh = 0x25;

        private const string RegistryKeyName = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
        private const RegistryRights RequiredRights = RegistryRights.ReadKey | RegistryRights.ReadPermissions | RegistryRights.WriteKey | RegistryRights.SetValue | RegistryRights.Delete;

        private readonly RegistryKey _registry;

        public WindowsProxyManager()
        {
            try
            {
                _registry = Registry.CurrentUser.OpenSubKey(RegistryKeyName, RegistryKeyPermissionCheck.ReadWriteSubTree, RequiredRights);
            }
            catch (SecurityException)
            {
                // the user doesn't have the required access level to edit the proxy.
            }
        }

        public void Dispose()
        {
            _registry?.Dispose();
        }

        public ProxyAccessState GetState()
        {
            return _registry != null ? ProxyAccessState.Accessible : ProxyAccessState.BlockedBySystem;
        }

        public ValueTask<IEnumerable<NetworkProxy>> GetProxy()
        {
            var proxyEnabled = (int)_registry.GetValue(ProxyEnabledKey, 0) != 0;
            var proxyUrlString = (string)_registry.GetValue(ProxyServerKey, string.Empty);

            var proxyUrls = proxyUrlString.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var networkProxies = new List<NetworkProxy>(proxyUrls.Length);

            foreach (var url in proxyUrls)
            {
                var urlMatch = ProxyUrlRegex().Match(url);

                if (!urlMatch.Success)
                {
                    continue;
                }

                var uriBuilder = new UriBuilder
                {
                    Scheme = urlMatch.Groups[1].Value,
                    Host = urlMatch.Groups[2].Value,
                    Port = ushort.Parse(urlMatch.Groups[3].Value),
                    Path = urlMatch.Groups[4].Value
                };

                networkProxies.Add(new NetworkProxy(proxyEnabled, uriBuilder.Uri));
            }

            return ValueTask.FromResult<IEnumerable<NetworkProxy>>(networkProxies);
        }

        public ValueTask<bool> SetProxy(params NetworkProxy[] proxies)
        {
            if (proxies.Length == 0)
            {
                _registry.SetValue(ProxyEnabledKey, 0, RegistryValueKind.DWord);
                _registry.DeleteValue(ProxyServerKey, false);
            }
            else
            {
                bool globalState = proxies[0].Enabled;
                var proxyUrlBuilder = new StringBuilder();

                foreach (var proxy in proxies)
                {
                    if (globalState != proxy.Enabled)
                    {
                        throw new ArgumentException($"{nameof(NetworkProxy.Enabled)} must be the same for all proxies (expected {globalState}, got {proxy.Enabled}");
                    }

                    // IPv6 addresses need to be wrapped in square brackets
                    var address = IPAddress.TryParse(proxy.Address.DnsSafeHost, out var ipAddress) && ipAddress.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{ipAddress}]" : proxy.Address.DnsSafeHost;
                    proxyUrlBuilder.Append($"{proxy.Address.Scheme}={address}:{proxy.Address.Port};");
                }

                proxyUrlBuilder.Length--;

                _registry.SetValue(ProxyEnabledKey, globalState ? 1 : 0, RegistryValueKind.DWord);
                _registry.SetValue(ProxyServerKey, proxyUrlBuilder.ToString(), RegistryValueKind.String);
            }

            var updateResult = SignalSettingsChanged();
            return ValueTask.FromResult(updateResult);
        }

        private static unsafe bool SignalSettingsChanged()
        {
            return PInvoke.InternetSetOption(null, InternetOptionSettingsChanged, null, 0) &&
                   PInvoke.InternetSetOption(null, InternetOptionRefresh, null, 0);
        }

        [GeneratedRegex(@"^(socks|http|https|ftp)=(.+):(\d{1,5})(/.+)?$", RegexOptions.IgnoreCase, "en-GB")]
        private static partial Regex ProxyUrlRegex();
    }
}