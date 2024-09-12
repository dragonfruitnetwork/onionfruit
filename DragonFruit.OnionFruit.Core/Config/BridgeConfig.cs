// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Config
{
    /// <summary>
    /// Describes the pluggable transport executable responsible for handling a specific type of traffic
    /// </summary>
    /// <param name="Type">The type of traffic the pluggable transport handles</param>
    /// <param name="ExecutablePathAndArgs">The path to the transport executable</param>
    public record PluggableTransport(string Type, string ExecutablePathAndArgs);

    /// <summary>
    /// Represents a bridge entry to use
    /// </summary>
    /// <param name="Type">The type of obfuscation to use. Leave as <c>null</c> for no obfuscation (plain)</param>
    /// <param name="Endpoint">The endpoint of the bridge</param>
    /// <param name="Fingerprint">The node's fingerprint</param>
    /// <param name="Options">Additional options as key-value pairs</param>
    public partial record BridgeEntry(string Type, IPEndPoint Endpoint, string Fingerprint, ICollection<KeyValuePair<string, string>> Options)
    {
        /// <summary>
        /// Attempts to parse a bridge line to a <see cref="BridgeEntry"/>
        /// </summary>
        /// <param name="line">The line to process</param>
        /// <param name="entry">Resultant <see cref="BridgeEntry"/></param>
        /// <returns>Whether the parse was successful</returns>
        public static bool TryParse(string line, out BridgeEntry entry)
        {
            var match = ValidationRegex().Match(line);

            if (!match.Success)
            {
                entry = null;
                return false;
            }

            var address = IPEndPoint.Parse(match.Groups["address"].Value);
            var options = new List<KeyValuePair<string, string>>();

            foreach (var pair in match.Groups["options"].Value.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var split = pair.IndexOf('=');
                options.Add(new KeyValuePair<string, string>(pair[..split], pair[(split + 1)..]));
            }

            entry = new BridgeEntry(match.Groups["type"].Value, address, match.Groups["fingerprint"].Value, options);
            return true;
        }

        public override string ToString()
        {
            var builder = new StringBuilder(Type);

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append($"{Endpoint} {Fingerprint}");

            if (Options?.Count > 0)
            {
                builder.Append(' ');

                foreach (var option in Options)
                {
                    builder.Append($"{option.Key}={option.Value} ");
                }

                builder.Length--;
            }

            return builder.ToString();
        }

        [GeneratedRegex(@"^(?:(?<type>\w+) )?(?<address>[\[0-9a-f:\.\]]+) (?<fingerprint>[0-9a-f]{40})(?: (?<options>.+))?$", RegexOptions.IgnoreCase, "en-US")]
        public static partial Regex ValidationRegex();
    }

    /// <summary>
    /// Allows configuration of unlisted entry nodes and obfuscated, pluggable transports
    /// </summary>
    public class BridgeConfig : TorrcConfigEntry
    {
        /// <summary>
        /// <see cref="BridgeEntry"/> instances to use when connecting to the Tor network
        /// </summary>
        public ICollection<BridgeEntry> Bridges { get; set; } = null;

        /// <summary>
        /// The <see cref="PluggableTransport"/>s to use to pass traffic from the client to the bridge.
        /// Leaving this empty with <see cref="Bridges"/> set will result in the bridges being unable to be used (unless they are unobfuscated)
        /// </summary>
        public ICollection<PluggableTransport> Transports { get; set; } = null;

        public override async Task WriteAsync(StreamWriter writer)
        {
            if (Bridges?.Count > 0)
            {
                await writer.WriteLineAsync("UseBridges 1");

                foreach (var bridge in Bridges)
                {
                    await writer.WriteAsync("Bridge ").ConfigureAwait(false);
                    await writer.WriteLineAsync(bridge.ToString()).ConfigureAwait(false);
                }
            }

            foreach (var transport in Transports ?? Enumerable.Empty<PluggableTransport>())
            {
                await writer.WriteLineAsync($"ClientTransportPlugin {transport.Type} exec {transport.ExecutablePathAndArgs}").ConfigureAwait(false);
            }
        }
    }
}