// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DragonFruit.OnionFruit.Core.Transports
{
    /// <summary>
    /// Represents the contents of a pt_config.json file bundled with pluggable transports
    /// </summary>
    public class PluggableTransportConfig
    {
        /// <summary>
        /// The currently recommended transport for general use
        /// </summary>
        [JsonPropertyName("recommendedDefault")]
        public string RecommendedDefault { get; set; }

        /// <summary>
        /// Gets the torrc entries for the pluggable transports available in the provided bundle
        /// </summary>
        [JsonPropertyName("pluggableTransports")]
        public IReadOnlyDictionary<string, string> PluggableTransports { get; set; }

        /// <summary>
        /// Default bridge entries for selected transports.
        /// </summary>
        [JsonPropertyName("bridges")]
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Bridges { get; set; }
    }
}