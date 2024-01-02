// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
        /// <param name="password">The password to use for authenticating with the control server</param>
        public ControlPortOption(int port, string password)
            : this(new IPEndPoint(IPAddress.Loopback, port), password)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ControlPortOption"/> using the given endpoint
        /// </summary>
        public ControlPortOption(IPEndPoint endpoint, string password)
        {
            Endpoints = new List<IPEndPoint>([endpoint]);
            Password = password;
        }

        /// <summary>
        /// Creates a new <see cref="ControlPortOption"/> using the given endpoints
        /// </summary>
        public ControlPortOption(IEnumerable<IPEndPoint> endpoints, string password)
        {
            Endpoints = new List<IPEndPoint>(endpoints);
            Password = password;
        }

        /// <summary>
        /// Gets or sets a list of endpoints to use for the control port
        /// </summary>
        public IList<IPEndPoint> Endpoints { get; set; }

        /// <summary>
        /// Gets or sets the password to use for the control port.
        /// This should be the raw password, not a hashed version
        /// </summary>
        public string Password { get; set; }

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

            if (!string.IsNullOrWhiteSpace(Password))
            {
                await writer.WriteLineAsync($"HashedControlPassword {CreateHashedPassword()}");
            }
        }

        /// <summary>
        /// Generates a Tor-accepted hash from the <see cref="Password"/>
        /// </summary>
        /// <remarks>
        /// Algorithm derived from https://www.antitree.com/2012/10/27/onion-porn-hashedcontrolpassword/
        /// </remarks>
        private string CreateHashedPassword()
        {
            const string prefix = "16:"; // prefix (base 16 - hex)
            const byte indicator = 0x60; // the iteration count (repeat the salt+password upto this count)
            const int expbias = 6;

            // generate random salt value
            var saltData = RandomNumberGenerator.GetBytes(8);

            // generate an even number that can be divided nicely below
            var count = (16 + (indicator & 15)) << ((indicator >> 4) + expbias);

            var hashCandidates = new List<byte>(count);
            var tmp = saltData.Concat(Encoding.ASCII.GetBytes(Password)).ToArray();

            var length = tmp.Length;
            while (count != 0)
            {
                if (count > length)
                {
                    hashCandidates.AddRange(tmp);
                    count -= length;
                }
                else
                {
                    hashCandidates.AddRange(tmp.Take(count));
                    count = 0;
                }
            }

            var hash = SHA1.HashData(hashCandidates.ToArray());
            return $"{prefix}{Convert.ToHexString(saltData)}{indicator:X}{Convert.ToHexString(hash)}";
        }
    }
}