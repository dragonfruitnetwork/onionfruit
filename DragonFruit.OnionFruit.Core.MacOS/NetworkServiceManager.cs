// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using DragonFruit.OnionFruit.Core.MacOS.NativeStructs;
using DragonFruit.OnionFruit.Core.Network;

namespace DragonFruit.OnionFruit.Core.MacOS
{
    /// <summary>
    /// Encapsulates a connection to the "onionfruitd" service, used to delegate network service management tasks.
    /// </summary>
    public class NetworkServiceManager : CriticalFinalizerObject, IDisposable
    {
        public NetworkServiceManager(string machServiceName)
        {
            var xpcHandle = NativeMethods.CreateXpcConnection(machServiceName, out var version, out var errorCode);
            if (xpcHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to create XPC connection for service '{machServiceName}'. Error code: {errorCode}");
            }

            XpcHandle = xpcHandle;
            Version = version;
        }

        ~NetworkServiceManager()
        {
            ReleaseUnmanagedResources();
        }

        public int Version { get; }

        internal IntPtr XpcHandle { get; private set; }

        /// <summary>
        /// Gets the DNS resolvers for a specified network service.
        /// </summary>
        /// <param name="serviceId">The network service to retrieve resolvers for</param>
        /// <returns>An array of <see cref="IPAddress"/>es used to resolve DNS queries</returns>
        public unsafe IPAddress[] GetDnsResolvers(string serviceId)
        {
            if (XpcHandle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(NetworkServiceManager), "Cannot access DNS resolvers after the connection has been disposed.");
            }

            if (NativeMethods.GetServiceDnsResolvers(XpcHandle, serviceId, out var resolverList, out var resolverCount) != 0)
            {
                throw new InvalidOperationException($"Failed to retrieve DNS resolvers for service '{serviceId}'.");
            }

            try
            {
                var resolvers = new IPAddress[resolverCount];
                var current = resolverList;

                for (int i = 0; i < resolverCount; i++)
                {
                    var ipAddressString = Marshal.PtrToStringUTF8(current);

                    if (string.IsNullOrEmpty(ipAddressString) || !IPAddress.TryParse(ipAddressString, out var ipAddress))
                    {
                        throw new InvalidOperationException($"Invalid IP address '{ipAddressString}' for resolver {i + 1}.");
                    }

                    resolvers[i] = ipAddress;
                    current = IntPtr.Add(current, sizeof(IntPtr));
                }

                return resolvers;
            }
            finally
            {
                NativeMethods.DestroyDnsResolverList(resolverList, resolverCount);
            }
        }

        /// <summary>
        /// Applies the specified DNS resolvers to a network service.
        /// </summary>
        /// <param name="serviceId">The network service to apply configurations to</param>
        /// <param name="resolvers">The DNS resolvers to set (or <c>null</c> if the current resolvers are to be cleared)</param>
        public void SetDnsResolvers(string serviceId, [MaybeNull] IPAddress[] resolvers)
        {
            if (XpcHandle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(NetworkServiceManager), "Cannot set DNS resolvers after the connection has been disposed.");
            }

            string[] convertedAddresses;

            if (resolvers == null)
            {
                convertedAddresses = [];
            }
            else
            {
                convertedAddresses = new string[resolvers.Length];

                for (int i = 0; i < resolvers.Length; i++)
                {
                    convertedAddresses[i] = resolvers[i].ToString();
                }
            }

            if (NativeMethods.SetServiceDnsResolvers(XpcHandle, serviceId, convertedAddresses, convertedAddresses.Length) != 0)
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
            if (XpcHandle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(NetworkServiceManager), "Cannot fetch proxy config after the connection has been disposed.");
            }

            if (NativeMethods.GetServiceProxyConfig(XpcHandle, serviceId, out var proxyConfigPtr) != 0)
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
        /// <param name="proxies">The proxies to set. Duplicate proxies will overwrite previous settings</param>
        /// <param name="clearExisting">
        /// Whether existing configuration values should be cleared.
        /// If <c>false</c>, the existing configuration will be requested to overwrite affected values first.
        /// </param>
        public void SetProxyServers(string serviceId, IEnumerable<NetworkProxy> proxies, bool clearExisting = false)
        {
            // get the current proxy config, duplicate and replace the values before sending it back
            if (XpcHandle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(NetworkServiceManager), "Cannot set proxy config after the connection has been disposed.");
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
                    if (NativeMethods.GetServiceProxyConfig(XpcHandle, serviceId, out proxyConfigPtr) != 0)
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

            foreach (var proxy in proxies)
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

            if (NativeMethods.SetServiceProxyConfig(XpcHandle, serviceId, proxyConfig) != 0)
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

        private void ReleaseUnmanagedResources()
        {
            if (XpcHandle == IntPtr.Zero)
            {
                return;
            }

            NativeMethods.DestroyXpcConnection(XpcHandle);
            XpcHandle = IntPtr.Zero;
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }
    }
}