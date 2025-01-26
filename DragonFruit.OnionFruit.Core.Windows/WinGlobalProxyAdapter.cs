// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Win32;
using DragonFruit.OnionFruit.Core.Network;
using Microsoft.Win32;

namespace DragonFruit.OnionFruit.Core.Windows
{
    /// <summary>
    /// Internal adapter for processing global proxy settings on Windows (as there is no adapter-specific setting)
    /// </summary>
    internal partial class WinGlobalProxyAdapter : INetworkAdapter, IDisposable
    {
        private const string ProxyEnabledKey = "ProxyEnable";
        private const string ProxyServerKey = "ProxyServer";

        private const int InternetOptionSettingsChanged = 0x27;
        private const int InternetOptionRefresh = 0x25;

        private readonly RegistryKey _registry;
        private readonly bool _shouldDisposeRegistryKey;

        public WinGlobalProxyAdapter(RegistryKey registry, bool shouldDisposeRegistryKey)
        {
            ArgumentNullException.ThrowIfNull(registry, nameof(registry));

            _registry = registry;
            _shouldDisposeRegistryKey = shouldDisposeRegistryKey;
        }

        public string Id => "onionfruit-global-proxy";
        public string Name => "OnionFruit Global Proxy Adapter (for Windows)";

        public bool IsVisible => false;

        public IList<NetworkProxy> GetProxyServers()
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

            return networkProxies;
        }

        public bool SetProxyServers(IReadOnlyList<NetworkProxy> proxies)
        {
            if (proxies.Count == 0)
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

            return SignalSettingsChanged();
        }

        public IList<IPAddress> GetDnsServers()
        {
            return [];
        }

        public bool SetDnsServers(IReadOnlyList<IPAddress> servers, bool clearExisting)
        {
            // global adapter can only handle proxies...
            return true;
        }

        public void Dispose()
        {
            if (_shouldDisposeRegistryKey)
            {
                _registry.Dispose();
            }
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