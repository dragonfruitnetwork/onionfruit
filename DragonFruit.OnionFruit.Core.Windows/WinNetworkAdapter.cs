// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using DragonFruit.OnionFruit.Core.Network;
using Microsoft.Win32;

namespace DragonFruit.OnionFruit.Core.Windows
{
    internal class WinNetworkAdapter : INetworkAdapter, IDisposable
    {
        private const string DnsKey = "NameServer";

        private readonly RegistryKey _tcpipKey;
        private readonly RegistryKey _tcpip6Key;

        public WinNetworkAdapter(string id, string name, bool isVisible, RegistryKey tcpipKey, RegistryKey tcpip6Key)
        {
            Id = id;
            Name = name;
            IsVisible = isVisible;

            _tcpipKey = tcpipKey;
            _tcpip6Key = tcpip6Key;
        }

        public string Id { get; }
        public string Name { get; }

        public bool IsVisible { get; }

        public IList<NetworkProxy> GetProxyServers()
        {
            return [];
        }

        public bool SetProxyServers(IReadOnlyList<NetworkProxy> servers)
        {
            return true;
        }

        public IList<IPAddress> GetDnsServers()
        {
            return new[] {_tcpipKey, _tcpip6Key}
                .Where(x => x != null)
                .SelectMany(key => key.GetValue(DnsKey, string.Empty).ToString()!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(IPAddress.Parse)
                .ToList();
        }

        public bool SetDnsServers(IList<IPAddress> servers, bool clearExisting)
        {
            var targetKeys = new List<RegistryKey> {_tcpipKey, _tcpip6Key};

            foreach (var group in servers?.GroupBy(server => server.AddressFamily) ?? [])
            {
                var targetRegistryKey = group.Key switch
                {
                    AddressFamily.InterNetwork => _tcpipKey,
                    AddressFamily.InterNetworkV6 => _tcpip6Key,

                    _ => throw new ArgumentOutOfRangeException()
                };

                if (targetRegistryKey == null)
                {
                    continue;
                }

                targetRegistryKey.SetValue(DnsKey, string.Join(",", group.Select(server => server.ToString())), RegistryValueKind.String);
                targetKeys.Remove(targetRegistryKey);
            }

            if (clearExisting)
            {
                foreach (var key in targetKeys)
                {
                    key?.SetValue(DnsKey, string.Empty, RegistryValueKind.String);
                }
            }

            return true;
        }

        public void Dispose()
        {
            _tcpipKey?.Dispose();
            _tcpip6Key?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}