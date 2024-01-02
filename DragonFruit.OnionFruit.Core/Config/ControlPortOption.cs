// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Config
{
    public class ControlPortOption : TorrcConfigEntry
    {
        /// <summary>
        /// Creates a new <see cref="ControlPortOption"/> using localhost and the given port
        /// </summary>
        /// <param name="port">The port to use</param>
        public ControlPortOption(int port)
            : this(new IPEndPoint(IPAddress.Loopback, port))
        {
        }

        /// <summary>
        /// Creates a new <see cref="ControlPortOption"/> using the given endpoint
        /// </summary>
        public ControlPortOption(IPEndPoint endpoint)
        {
            Endpoints = new List<IPEndPoint>([endpoint]);
        }

        /// <summary>
        /// Creates a new <see cref="ControlPortOption"/> using the given endpoints
        /// </summary>
        public ControlPortOption(IEnumerable<IPEndPoint> endpoints)
        {
            Endpoints = new List<IPEndPoint>(endpoints);
        }

        /// <summary>
        /// Gets or sets a list of endpoints to use for the control port
        /// </summary>
        public IList<IPEndPoint> Endpoints { get; set; }

        public override IEnumerable<ConfigEntryValidationResult> PerformValidation()
        {
            if (Endpoints == null)
            {
                yield return new ConfigEntryValidationResult(false, "No endpoints specified");

                yield break;
            }

            if (Endpoints.Any(x => x.AddressFamily is not AddressFamily.InterNetwork and not AddressFamily.InterNetworkV6))
            {
                yield return new ConfigEntryValidationResult(false, "One or more are not valid - only IPv4 and IPv6 are supported");
            }
        }

        public override async Task WriteAsync(StreamWriter writer)
        {
            foreach (var entry in Endpoints ?? Enumerable.Empty<IPEndPoint>())
            {
                // only IPv4 and IPv6 are supported
                if (entry.AddressFamily is not AddressFamily.InterNetwork and not AddressFamily.InterNetworkV6)
                {
                    continue;
                }

                var addressBuilder = new StringBuilder(entry.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{entry.Address}]" : entry.Address.ToString());
                addressBuilder.Append($":{entry.Port}");

                await writer.WriteLineAsync($"ControlPort {addressBuilder}");
            }
        }
    }
}