// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using DragonFruit.OnionFruit.Core.MacOS.Native;
using DragonFruit.OnionFruit.Core.Network;

namespace DragonFruit.OnionFruit.Core.MacOS
{
    /// <summary>
    /// Encapsulates a connection to the "onionfruitd" service, used to delegate network service management tasks.
    /// </summary>
    public class OnionFruitDaemonConnection : IDisposable
    {
        private readonly XpcConnectionHandle _xpcHandle;

        public OnionFruitDaemonConnection(string machServiceName)
        {
            var status = NativeMethods.CreateXpcConnection(machServiceName, out var xpcHandle, out var version);

            if (status == NativeStatus.Ok)
            {
                _xpcHandle = xpcHandle;
                Version = version;
            }
            else
            {
                _xpcHandle = null;
                Version = -1;
            }
        }

        public int Version { get; }

        public bool IsValid => _xpcHandle?.IsClosed == false;

        /// <summary>
        /// Gets the DNS resolvers for a specified network service.
        /// </summary>
        /// <param name="serviceId">The network service to retrieve resolvers for</param>
        /// <returns>An array of <see cref="IPAddress"/>es used to resolve DNS queries</returns>
        public unsafe IList<IPAddress> GetDnsResolvers(string serviceId)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(OnionFruitDaemonConnection), "Cannot access DNS resolvers after the connection has been disposed.");
            }

            if (NativeMethods.GetServiceDnsResolvers(_xpcHandle, serviceId, out var resolverList, out var resolverCount) != NativeStatus.Ok)
            {
                throw new InvalidOperationException($"Failed to retrieve DNS resolvers for service '{serviceId}'.");
            }

            try
            {
                var resolvers = new List<IPAddress>(resolverCount);
                var current = resolverList;

                for (int i = 0; i < resolverCount; i++)
                {
                    var ipAddressString = Marshal.PtrToStringUTF8(current);

                    if (IPAddress.TryParse(ipAddressString, out var ipAddress))
                    {
                        resolvers.Add(ipAddress);
                    }

                    current = IntPtr.Add(current, sizeof(IntPtr));
                }

                return resolvers;
            }
            finally
            {
                NativeMethods.DestroyDnsResolverList(resolverList);
            }
        }

        /// <summary>
        /// Applies the specified DNS resolvers to a network service.
        /// </summary>
        /// <param name="serviceId">The network service to apply configurations to</param>
        /// <param name="resolvers">The DNS resolvers to set (or <c>null</c> if the current resolvers are to be cleared)</param>
        public void SetDnsResolvers(string serviceId, [MaybeNull] IList<IPAddress> resolvers)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(OnionFruitDaemonConnection), "Cannot set DNS resolvers after the connection has been disposed.");
            }

            string[] convertedAddresses;

            if (resolvers == null)
            {
                convertedAddresses = [];
            }
            else
            {
                convertedAddresses = new string[resolvers.Count];

                for (int i = 0; i < resolvers.Count; i++)
                {
                    convertedAddresses[i] = resolvers[i].ToString();
                }
            }

            if (NativeMethods.SetServiceDnsResolvers(_xpcHandle, serviceId, convertedAddresses, convertedAddresses.Length) != NativeStatus.Ok)
            {
                throw new InvalidOperationException($"Failed to set DNS resolvers for service '{serviceId}'.");
            }
        }

        /// <summary>
        /// Retrieves the currently set proxy servers for a specified network service.
        /// </summary>
        /// <param name="serviceId">The network service to fetch configurations for</param>
        /// <returns>A collection of network proxies held by the service</returns>
        public IEnumerable<NetworkProxy> GetProxyServers(string serviceId)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(OnionFruitDaemonConnection), "Cannot fetch proxy config after the connection has been disposed.");
            }

            if (NativeMethods.GetServiceProxyConfig(_xpcHandle, serviceId, out var proxyConfigPtr) != NativeStatus.Ok)
            {
                throw new InvalidOperationException($"Failed to retrieve proxy configuration for service '{serviceId}'.");
            }

            try
            {
                var proxyConfig = Marshal.PtrToStructure<ServiceProxyConfig>(proxyConfigPtr);

                if (!string.IsNullOrEmpty(proxyConfig.HttpProxyHost))
                {
                    yield return new NetworkProxy(proxyConfig.HttpProxyEnabled, CreateProxyUri(proxyConfig.HttpProxyHost, proxyConfig.HttpProxyPort, "http"));
                }

                if (!string.IsNullOrEmpty(proxyConfig.HttpsProxyHost))
                {
                    yield return new NetworkProxy(proxyConfig.HttpsProxyEnabled, CreateProxyUri(proxyConfig.HttpsProxyHost, proxyConfig.HttpsProxyPort, "https"));
                }

                if (!string.IsNullOrEmpty(proxyConfig.SocksProxyHost))
                {
                    yield return new NetworkProxy(proxyConfig.SocksProxyEnabled, CreateProxyUri(proxyConfig.SocksProxyHost, proxyConfig.SocksProxyPort, "socks"));
                }
            }
            finally
            {
                NativeMethods.DestroyProxyConfig(proxyConfigPtr);
            }
        }

        /// <summary>
        /// Sets the proxy configuration for a specified network service.
        /// </summary>
        /// <param name="serviceId">The network service to modify configurations for</param>
        /// <param name="proxies">The proxies to set. Duplicate proxies do not overwrite previous values</param>
        /// <param name="clearExisting">
        /// Whether existing configuration values should be cleared.
        /// If <c>false</c>, the existing configuration will be requested to overwrite affected values first.
        /// </param>
        public void SetProxyServers(string serviceId, IReadOnlyList<NetworkProxy> proxies, bool clearExisting = false)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(OnionFruitDaemonConnection), "Cannot set proxy config after the connection has been disposed.");
            }

            ServiceProxyConfig proxyConfig;

            if (clearExisting)
            {
                proxyConfig = new ServiceProxyConfig();
            }
            else
            {
                IntPtr proxyConfigPtr = IntPtr.Zero;

                try
                {
                    if (NativeMethods.GetServiceProxyConfig(_xpcHandle, serviceId, out proxyConfigPtr) != NativeStatus.Ok)
                    {
                        throw new InvalidOperationException($"Failed to retrieve proxy configuration for service '{serviceId}'.");
                    }

                    proxyConfig = Marshal.PtrToStructure<ServiceProxyConfig>(proxyConfigPtr);
                }
                finally
                {
                    if (proxyConfigPtr != IntPtr.Zero)
                    {
                        NativeMethods.DestroyProxyConfig(proxyConfigPtr);
                    }
                }
            }

            foreach (var proxy in proxies.Reverse())
            {
                if (!proxy.Address.IsAbsoluteUri)
                {
                    throw new ArgumentException("Proxy address must be an absolute URI.", nameof(proxies));
                }

                switch (proxy.Address.Scheme)
                {
                    case "http":
                        proxyConfig.HttpProxyEnabled = proxy.Enabled;
                        proxyConfig.HttpProxyHost = proxy.Address.DnsSafeHost;
                        proxyConfig.HttpProxyPort = (ushort)proxy.Address.Port;
                        break;

                    case "https":
                        proxyConfig.HttpsProxyEnabled = proxy.Enabled;
                        proxyConfig.HttpsProxyHost = proxy.Address.DnsSafeHost;
                        proxyConfig.HttpsProxyPort = (ushort)proxy.Address.Port;
                        break;

                    case "socks":
                        proxyConfig.SocksProxyEnabled = proxy.Enabled;
                        proxyConfig.SocksProxyHost = proxy.Address.DnsSafeHost;
                        proxyConfig.SocksProxyPort = (ushort)proxy.Address.Port;
                        break;

                    default:
                        throw new ArgumentException($"Unsupported proxy scheme: {proxy.Address.Scheme}", nameof(proxies));
                }
            }

            if (NativeMethods.SetServiceProxyConfig(_xpcHandle, serviceId, proxyConfig) != NativeStatus.Ok)
            {
                throw new InvalidOperationException($"Failed to set proxy configuration for service '{serviceId}'.");
            }
        }

        private static Uri CreateProxyUri(string host, int port, string scheme)
        {
            var builder = new UriBuilder
            {
                Host = host,
                Port = port,
                Scheme = scheme
            };

            return builder.Uri;
        }

        public void Dispose()
        {
            _xpcHandle?.Dispose();
        }
    }
}