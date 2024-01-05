// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

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
        public static int? GetClosestFreePort(int target, int range = 20, params int[] excludedPorts)
        {
            var ports = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(x => x.Port).ToHashSet();

            foreach (var candidate in GenerateValueSequence(target, range))
            {
                if (!ports.Contains(candidate) && !excludedPorts.Contains(candidate))
                {
                    return candidate;
                }
            }

            return null;
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