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

        /// <summary>
        /// Returns the closest open port to the provided <see cref="target"/>
        /// </summary>
        /// <param name="target">The preferred port to use</param>
        /// <param name="range">The range to check for open ports (i.e. if range = 10, try 10 above and 10 below <see cref="target"/>)</param>
        /// <returns>The closest port within an "acceptable" range, or null if none available</returns>
        public static int? GetClosestFreePort(int target, int range = 20)
        {
            var ports = GetActiveTcpPorts();

            for (int i = 0; i < range; i++)
            {
                var nextPort = target + i;

                // try +1
                if (!ports.Contains(nextPort))
                {
                    return nextPort;
                }

                nextPort = target - i;

                // try -1
                if (!ports.Contains(nextPort))
                {
                    return nextPort;
                }
            }

            return null;
        }
    }
}