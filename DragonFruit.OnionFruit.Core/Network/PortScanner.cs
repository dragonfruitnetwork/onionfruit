// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace DragonFruit.OnionFruit.Core.Network
{
    public static class PortScanner
    {
        /// <summary>
        /// Produces a set of all active TCP ports on the local system
        /// </summary>
        public static IReadOnlySet<int> GetActiveTcpPorts()
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var connections = properties.GetActiveTcpConnections();

            var ports = new HashSet<int>(connections.Length);

            foreach (var connection in connections)
            {
                if (connection.State == TcpState.Closed)
                {
                    continue;
                }

                ports.Add(connection.LocalEndPoint.Port);
            }

            return ports;
        }
    }
}