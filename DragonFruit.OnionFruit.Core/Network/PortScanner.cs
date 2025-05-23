// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DragonFruit.OnionFruit.Core.Network
{
    public static class PortScanner
    {
        /// <summary>
        /// Returns the closest port available for listening on to the provided <see cref="target"/>
        /// </summary>
        /// <param name="target">The preferred port to use</param>
        /// <param name="range">The number of ports above and below the <see cref="target"/> to check</param>
        /// <param name="excludedPorts">Optional list of ports to exclude from being selected.</param>
        /// <returns>The closest port within an "acceptable" range, or null if none available</returns>
        public static int? GetClosestFreePort(int target, int range = 20, IEnumerable<int> excludedPorts = null)
        {
            var ports = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Where(x => x.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                .Select(x => x.Port)
                .Concat(excludedPorts ?? [])
                .ToHashSet();

            return GenerateValueSequence(target, range).FirstOrDefault(x => !ports.Contains(x));
        }

        /// <summary>
        /// Gets whether a specified port is available on the local machine for listening on
        /// </summary>
        public static bool IsPortAvailable(int port)
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            var isInUse = listeners.Any(x => x.Port == port && x.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6);

            return !isInUse;
        }

        private static IEnumerable<int> GenerateValueSequence(int start, int iterations)
        {
            yield return start;

            for (int i = 1; i <= iterations; i++)
            {
                yield return start + i;
                yield return start - i;
            }
        }
    }
}