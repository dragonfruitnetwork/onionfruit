// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DragonFruit.OnionFruit.Core.Transports
{
    public class PluggableTransportConfig
    {
        [JsonPropertyName("recommendedDefault")]
        public string RecommendedDefault { get; set; }

        [JsonPropertyName("pluggableTransports")]
        public IReadOnlyDictionary<string, string> PluggableTransports { get; set; }

        [JsonPropertyName("bridges")]
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Bridges { get; set; }
    }
}