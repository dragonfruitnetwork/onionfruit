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
    public class WindowsNetworkAdapter : INetworkAdapter, IDisposable
    {
        private readonly RegistryKey _tcpipKey, _tcpip6Key;

        public WindowsNetworkAdapter(string id, string name, RegistryKey tcpipKey, RegistryKey tcpip6Key)
        {
            Id = id;
            Name = name;

            _tcpipKey = tcpipKey;
            _tcpip6Key = tcpip6Key;
        }

        public string Id { get; }
        public string Name { get; }

        public IReadOnlyCollection<IPAddress> GetDnsServers()
        {
            return new[] {_tcpipKey, _tcpip6Key}
                .Where(x => x != null)
                .SelectMany(key => key.GetValue("NameServer", string.Empty).ToString()!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(IPAddress.Parse)
                .ToList();
        }

        public void SetDnsServers(IReadOnlyCollection<IPAddress> servers, bool clearExisting)
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
                    return;
                }

                targetRegistryKey.SetValue("NameServer", string.Join(",", group.Select(server => server.ToString())), RegistryValueKind.String);
                targetKeys.Remove(targetRegistryKey);
            }

            if (clearExisting)
            {
                foreach (var key in targetKeys)
                {
                    key?.SetValue("NameServer", string.Empty, RegistryValueKind.String);
                }
            }
        }

        public void Dispose()
        {
            _tcpipKey?.Dispose();
            _tcpip6Key?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}