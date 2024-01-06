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
    public class ClientConfig : TorrcConfigEntry
    {
        private bool _enableV4 = Socket.OSSupportsIPv4;
        private bool _enableV6 = Socket.OSSupportsIPv6;

        public ClientConfig()
        {
        }

        public ClientConfig(int port)
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

        public ClientConfig(IPEndPoint endpoint)
        {
            Endpoints = [endpoint];
        }

        public ClientConfig(IEnumerable<IPEndPoint> endpoints)
        {
            Endpoints = new List<IPEndPoint>(endpoints);
        }

        /// <summary>
        /// Whether to enable client-only mode. Defaults to <c>true</c>.
        /// </summary>
        public bool ClientOnly { get; set; } = true;

        /// <summary>
        /// Whether to enable scrubbing of log files. Also known as <c>ShowPII</c> in other libraries.
        /// </summary>
        public bool EnableLogScrubbing { get; set; } = true;

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
        /// This is useful for proxying .onion hostnames through apps that don't know how to handle them.
        /// </summary>
        public bool AutomapHostsOnResolve { get; set; }

        /// <summary>
        /// How often padding messages should be sent over the connection to prevent firewalls from closing the connection
        /// </summary>
        public TimeSpan? ExternalConnectionKeepAlive { get; set; }

        /// <summary>
        /// How long a circuit should be retained with no activity before being destroyed.
        /// </summary>
        public TimeSpan? CircuitIdleTimeout { get; set; }

        /// <summary>
        /// Whether Tor should reject connections that are using unsafe variants of the socks protocol
        /// (ones that only provide an IP address, indicating a DNS lookup occured beforehand)
        /// </summary>
        public bool RejectUnsafeSocksConnections { get; set; }

        /// <summary>
        /// Gets or sets a list of ports that should not be used for plaintext connections. Tor will refuse to connect to these ports.
        /// </summary>
        public ICollection<int> RejectPlaintextPorts { get; set; } = null;

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
                yield return new ConfigEntryValidationResult(true, $"{nameof(FirewallPorts)} contains one or more invalid ports");
            }

            if (RejectPlaintextPorts?.Any(x => x is < 0 or > 65535) == true)
            {
                yield return new ConfigEntryValidationResult(true, $"{nameof(RejectPlaintextPorts)} contains one or more invalid ports");
            }
        }

        public override async Task WriteAsync(StreamWriter writer)
        {
            await writer.WriteLineAsync($"ClientOnly {(ClientOnly ? 1 : 0)}").ConfigureAwait(false);
            await writer.WriteLineAsync($"ClientUseIPv4 {(EnableIPv4 ? 1 : 0)}").ConfigureAwait(false);
            await writer.WriteLineAsync($"ClientUseIPv6 {(EnableIPv6 ? 1 : 0)}").ConfigureAwait(false);

            // we expose the option the other way around to make more sense to consumers (i.e. show pii)
            await writer.WriteLineAsync($"SafeLogging {(EnableLogScrubbing ? 0 : 1)}").ConfigureAwait(false);

            if (Endpoints?.Count > 0)
            {
                await WriteEndpointsAsync(writer, "SocksPort", Endpoints).ConfigureAwait(false);
            }

            if (DnsEndpoints?.Count > 0)
            {
                await WriteEndpointsAsync(writer, "DNSPort", DnsEndpoints).ConfigureAwait(false);
            }

            await writer.WriteLineAsync($"AutomapHostsOnResolve {(AutomapHostsOnResolve ? 1 : 0)}").ConfigureAwait(false);

            if (AutomappedSuffixes?.Count > 0)
            {
                await writer.WriteLineAsync($"AutomapHostsSuffixes {string.Join(',', AutomappedSuffixes)}").ConfigureAwait(false);
            }

            if (ExternalConnectionKeepAlive > TimeSpan.Zero)
            {
                await writer.WriteLineAsync($"KeepAlivePeriod {(int)ExternalConnectionKeepAlive.Value.TotalSeconds}").ConfigureAwait(false);
            }

            if (CircuitIdleTimeout > TimeSpan.Zero)
            {
                await writer.WriteLineAsync($"MaxCircuitDirtiness {(int)CircuitIdleTimeout.Value.TotalSeconds}").ConfigureAwait(false);
            }

            await writer.WriteLineAsync($"SafeSocks {(RejectUnsafeSocksConnections ? 1 : 0)}").ConfigureAwait(false);

            if (RejectPlaintextPorts?.Count > 0)
            {
                await writer.WriteLineAsync($"RejectPlaintextPorts {string.Join(',', RejectPlaintextPorts)}").ConfigureAwait(false);
            }

            if (FacistFirewall)
            {
                await writer.WriteLineAsync("FacistFirewall 1").ConfigureAwait(false);

                if (FirewallPorts?.Count > 0)
                {
                    await writer.WriteLineAsync($"FirewallPorts {string.Join(',', FirewallPorts)}").ConfigureAwait(false);
                }
            }
        }

        private static async Task WriteEndpointsAsync(StreamWriter writer, string keyword, IEnumerable<IPEndPoint> endpoints)
        {
            foreach (var endpoint in endpoints ?? Enumerable.Empty<IPEndPoint>())
            {
                await writer.WriteLineAsync($"{keyword} {endpoint}").ConfigureAwait(false);
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