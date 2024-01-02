// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Config
{
    /// <summary>
    /// Exposes options commonly used by clients including endpoints, automapping hosts, dns settings and firewall settings.
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
        public ICollection<IPEndPoint> DnsEndpoints { get; set; } = null;

        /// <summary>
        /// Whether to automatically map hostnames with a tld specified in <see cref="AutomappedSuffixes"/> to a local address for the purpose of resolving them over Tor.
        /// This is useful for proxying .onion hostnames on apps that don't know how to resolve them.
        /// </summary>
        public bool AutomapHostsOnResolve { get; set; } = true;

        /// <summary>
        /// How often padding messages should be sent over the connection to prevent firewalls from closing the connection
        /// </summary>
        public TimeSpan? KeepAlive { get; set; }

        /// <summary>
        /// Whether the client is behind a firewall that only allows traffic on specific ports.
        /// By setting this to <c>true</c>, the client will select nodes that are available on ports specified by <see cref="FirewallPorts"/>
        /// </summary>
        public bool FacistFirewall { get; set; }

        /// <summary>
        /// Gets or sets a list of ports that the client should use when <see cref="FacistFirewall"/> is <c>true</c>
        /// </summary>
        public ICollection<int> FirewallPorts { get; set; } = null;

        /// <summary>
        /// Gets or sets a list of suffixes to automatically map to a local address for the purpose of resolving them over Tor.
        /// </summary>
        public ICollection<string> AutomappedSuffixes { get; set; } = null;

        public override IEnumerable<ConfigEntryValidationResult> PerformValidation()
        {
            if (FirewallPorts?.Count > 0 && !FacistFirewall)
            {
                yield return new ConfigEntryValidationResult(false, $"{nameof(FirewallPorts)} is redundant when {nameof(FacistFirewall)} is not enabled");
            }

            if (FirewallPorts?.Any(x => x is < 0 or > 65535) == true)
            {
                yield return new ConfigEntryValidationResult(false, $"{nameof(FirewallPorts)} contains one or more invalid ports");
            }
        }

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

            await writer.WriteLineAsync($"AutomapHostsOnResolve {(AutomapHostsOnResolve ? 1 : 0)}").ConfigureAwait(false);

            if (AutomappedSuffixes?.Any() == true)
            {
                await writer.WriteLineAsync($"AutomapHostsSuffixes {string.Join(',', AutomappedSuffixes)}").ConfigureAwait(false);
            }

            if (KeepAlive > TimeSpan.Zero)
            {
                await writer.WriteLineAsync($"KeepAlivePeriod {KeepAlive.Value.TotalSeconds}").ConfigureAwait(false);
            }

            if (FacistFirewall)
            {
                await writer.WriteLineAsync("FacistFirewall 1").ConfigureAwait(false);

                // filter out invalid ports
                var validPorts = FirewallPorts?.Where(x => x is > 0 and < 65536).ToList();
                if (validPorts?.Count > 0)
                {
                    await writer.WriteLineAsync($"FirewallPorts {string.Join(',', validPorts)}").ConfigureAwait(false);
                }
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