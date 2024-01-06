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
    /// <summary>
    /// Exposes options to configure the control server
    /// </summary>
    /// <remarks>
    /// By setting this option, you can use a TCP client to interact with the Tor process while it's running.
    /// </remarks>
    public class ControlPortConfig : TorrcConfigEntry
    {
        /// <summary>
        /// Creates a new <see cref="ControlPortConfig"/> using localhost and the given port
        /// </summary>
        /// <param name="port">The port to use</param>
        /// <param name="password">The password to use for authenticating with the control server</param>
        public ControlPortConfig(int port, string password)
            : this(new IPEndPoint(IPAddress.Loopback, port), password)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ControlPortConfig"/> using the given endpoint
        /// </summary>
        public ControlPortConfig(IPEndPoint endpoint, string password)
        {
            Endpoints = new List<IPEndPoint>([endpoint]);
            Password = password;
        }

        /// <summary>
        /// Creates a new <see cref="ControlPortConfig"/> using the given endpoints
        /// </summary>
        public ControlPortConfig(IEnumerable<IPEndPoint> endpoints, string password)
        {
            Endpoints = new List<IPEndPoint>(endpoints);
            Password = password;
        }

        /// <summary>
        /// Gets or sets a list of endpoints to use for the control port
        /// </summary>
        public ICollection<IPEndPoint> Endpoints { get; set; }

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

            const int count = (16 + (indicator & 15)) << ((indicator >> 4) + expbias);

            // generate random salt value
            var saltData = RandomNumberGenerator.GetBytes(8);
            var saltedPassword = saltData.Concat(Encoding.ASCII.GetBytes(Password)).ToArray();

            Span<byte> data = stackalloc byte[count];
            Span<byte> hash = stackalloc byte[20];

            var offset = 0;

            while (offset < count)
            {
                var remaining = count - offset;
                var copyLength = Math.Min(remaining, saltedPassword.Length);

                saltedPassword.AsSpan()[..copyLength].CopyTo(data[offset..]);

                offset += copyLength;
            }

            SHA1.HashData(data, hash);
            return $"{prefix}{Convert.ToHexString(saltData)}{indicator:X}{Convert.ToHexString(hash)}";
        }
    }
}