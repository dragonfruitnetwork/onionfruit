// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Config
{
    /// <summary>
    /// Exposes options commonly used by clients
    /// </summary>
    public class ClientOptions : TorrcConfigEntry
    {
        private bool _enableV4 = Socket.OSSupportsIPv4;
        private bool _enableV6 = Socket.OSSupportsIPv6;

        public ClientOptions(int port)
        {
            Endpoints = new List<IPEndPoint>(2);

            if (_enableV4)
            {
                Endpoints.Add(new IPEndPoint(IPAddress.Loopback, port));
            }

            if (_enableV6)
            {
                Endpoints.Add(new IPEndPoint(IPAddress.IPv6Loopback, port));
            }
        }

        public ClientOptions(IPEndPoint endpoint)
        {
            Endpoints = [endpoint];
        }

        public ClientOptions(IEnumerable<IPEndPoint> endpoints)
        {
            Endpoints = new List<IPEndPoint>(endpoints);
        }

        /// <summary>
        /// Whether to enable client-only mode. Defaults to <c>true</c>.
        /// </summary>
        public bool ClientOnly { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable IPv4 for connections to nodes.
        /// </summary>
        public bool EnableIPv4
        {
            get => _enableV4;
            set => _enableV4 = SetNetworkEnabled(value, Socket.OSSupportsIPv4);
        }

        /// <summary>
        /// Gets or sets whether to use IPv6 when connecting to nodes.
        /// </summary>
        public bool EnableIPv6
        {
            get => _enableV6;
            set => _enableV6 = SetNetworkEnabled(value, Socket.OSSupportsIPv6);
        }

        /// <summary>
        /// Gets or sets a list of endpoints to expose as a proxy server.
        /// </summary>
        public ICollection<IPEndPoint> Endpoints { get; set; }

        /// <summary>
        /// Gets or sets a list of endpoints to expose as DNS resolvers.
        /// </summary>
        public ICollection<IPEndPoint> DnsEndpoints { get; set; }

        public override async Task WriteAsync(StreamWriter writer)
        {
            await writer.WriteLineAsync($"ClientOnly {(ClientOnly ? 1 : 0)}").ConfigureAwait(false);
            await writer.WriteLineAsync($"ClientUseIPv4 {(EnableIPv4 ? 1 : 0)}").ConfigureAwait(false);
            await writer.WriteLineAsync($"ClientUseIPv6 {(EnableIPv6 ? 1 : 0)}").ConfigureAwait(false);

            if (Endpoints?.Any() == true)
            {
                await WriteEndpointsAsync(writer, "SocksPort", Endpoints).ConfigureAwait(false);
            }

            if (DnsEndpoints?.Any() == true)
            {
                await WriteEndpointsAsync(writer, "DNSPort", DnsEndpoints).ConfigureAwait(false);
            }
        }

        private static async Task WriteEndpointsAsync(TextWriter writer, string keyword, IEnumerable<IPEndPoint> endpoints)
        {
            foreach (var endpoint in endpoints ?? Enumerable.Empty<IPEndPoint>())
            {
                var address = endpoint.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{endpoint.Address}]" : endpoint.Address.ToString();
                await writer.WriteLineAsync($"{keyword} {address}:{endpoint.Port}").ConfigureAwait(false);
            }
        }

        private bool SetNetworkEnabled(bool value, bool supported)
        {
            if (!supported && value)
            {
                return false;
            }

            return supported;
        }
    }
}